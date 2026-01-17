namespace Mibo.Rendering.Graphics3D

open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Render Pipeline
// ============================================================================

/// Render pipeline interface
type IRenderPipeline =
  abstract member Initialize: GraphicsDevice -> unit

  abstract member Render:
    GameContext * RenderBuffer<unit, RenderCommand> -> unit

/// Pipeline state container
type internal PipelineState = {
  mutable Config: PipelineConfig
  mutable Device: GraphicsDevice
  mutable RtPool: IRenderTargetPool
  mutable BasicEffect: BasicEffect
  mutable SpriteBatch: SpriteBatch
  CustomShaders: Dictionary<ShaderBase, Effect>
  ShadowMaps: ResizeArray<RenderTarget2D>
  ShadowViewMatrices: ResizeArray<Matrix>
  ShadowProjectionMatrices: ResizeArray<Matrix>
  mutable LightGridTexture: Texture2D voption
  mutable MainSceneTarget: RenderTarget2D voption
  mutable CurrentCamera: Mibo.Rendering.Graphics3D.Camera
  mutable CurrentLighting: LightingState
  mutable CameraWasSet: bool
  // Draw batching for sorting
  OpaqueDrawables: ResizeArray<struct (float32 * Drawable)> // (distance, drawable)
  TransparentDrawables: ResizeArray<struct (float32 * Drawable)>
}

// ============================================================================
// Shared - Utilities used by all render modes
// ============================================================================

module internal Shared =

  let createState(config: PipelineConfig) : PipelineState = {
    Config = config
    Device = Unchecked.defaultof<_>
    RtPool = Unchecked.defaultof<_>
    BasicEffect = Unchecked.defaultof<_>
    SpriteBatch = Unchecked.defaultof<_>
    CustomShaders = Dictionary<ShaderBase, Effect>()
    ShadowMaps = ResizeArray<RenderTarget2D>()
    ShadowViewMatrices = ResizeArray<Matrix>()
    ShadowProjectionMatrices = ResizeArray<Matrix>()
    LightGridTexture = ValueNone
    MainSceneTarget = ValueNone
    CurrentCamera = Camera.identity
    CurrentLighting = Lighting.ambient
    CameraWasSet = false
    OpaqueDrawables = ResizeArray<struct (float32 * Drawable)>(256)
    TransparentDrawables = ResizeArray<struct (float32 * Drawable)>(64)
  }

  let resetFrameState(state: PipelineState) =
    state.CurrentCamera <- Camera.identity

    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    state.CameraWasSet <- false
    state.OpaqueDrawables.Clear()
    state.TransparentDrawables.Clear()
    state.ShadowViewMatrices.Clear()
    state.ShadowProjectionMatrices.Clear()
    state.MainSceneTarget <- ValueNone

  let isVisible
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    : bool =
    let frustum = BoundingFrustum(camera.View * camera.Projection)
    frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

  let distanceToCamera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    : float32 =
    Vector3.DistanceSquared(camera.Position, drawable.BoundingSphere.Center)

  let isTransparent(drawable: Drawable) : bool =
    drawable.Material.Flags.HasFlag(MaterialFlags.Transparent)
    || drawable.Material.PBR.AlbedoColor.A < 255uy

  let configureBasicEffectLighting
    (effect: BasicEffect)
    (lighting: LightingState)
    =
    effect.LightingEnabled <- true

    effect.AmbientLightColor <-
      lighting.AmbientColor.ToVector3() * lighting.AmbientIntensity

    // Disable all lights first
    effect.DirectionalLight0.Enabled <- false
    effect.DirectionalLight1.Enabled <- false
    effect.DirectionalLight2.Enabled <- false

    // Configure up to 3 directional lights
    let mutable lightIndex = 0

    for light in lighting.Lights do
      if lightIndex < 3 then
        match light with
        | Directional dl ->
          let beLight =
            match lightIndex with
            | 0 -> effect.DirectionalLight0
            | 1 -> effect.DirectionalLight1
            | _ -> effect.DirectionalLight2

          beLight.Enabled <- true
          beLight.Direction <- dl.Direction
          beLight.DiffuseColor <- dl.Color.ToVector3() * dl.Intensity
          beLight.SpecularColor <- Vector3.Zero
          lightIndex <- lightIndex + 1
        | Point _
        | Spot _ ->
          // BasicEffect doesn't support point/spot lights
          ()

  let configureBasicEffectCamera
    (effect: BasicEffect)
    (camera: Mibo.Rendering.Graphics3D.Camera)
    =
    effect.View <- camera.View
    effect.Projection <- camera.Projection

  let renderDrawableWithEffect
    (state: PipelineState)
    (drawable: Drawable)
    (effect: Effect)
    =
    let mesh = drawable.Mesh
    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    let setParam (name: string) (value: obj) =
      let p = effect.Parameters.[name]

      if not(isNull p) then
        match value with
        | :? Matrix as m -> p.SetValue(m)
        | :? Vector3 as v -> p.SetValue(v)
        | :? Vector4 as v -> p.SetValue(v)
        | :? float32 as f -> p.SetValue(f)
        | :? bool as b -> p.SetValue(b)
        | :? Texture2D as t -> p.SetValue(t)
        | :? Color as c -> p.SetValue(c.ToVector4())
        | _ -> ()

    setParam "World" drawable.Transform
    setParam "View" state.CurrentCamera.View
    setParam "Projection" state.CurrentCamera.Projection

    // Lighting
    setParam
      "AmbientColor"
      (state.CurrentLighting.AmbientColor.ToVector3()
       * state.CurrentLighting.AmbientIntensity)

    let lightDirs =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Directional dl -> Some dl.Direction
        | _ -> None)

    let lightColors =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Directional dl -> Some(dl.Color.ToVector3() * dl.Intensity)
        | _ -> None)

    if lightDirs.Length > 0 then
      let pDirs = effect.Parameters.["LightDirections"]

      if not(isNull pDirs) then
        pDirs.SetValue(lightDirs)
      else
        // Fallback: Try binding single directional light
        let pDir = effect.Parameters.["LightDirection"]
        if not(isNull pDir) then pDir.SetValue(lightDirs.[0])

      let pCols = effect.Parameters.["LightColors"]

      if not(isNull pCols) then
        pCols.SetValue(lightColors)
      else
        // Fallback: Try binding single directional light color
        let pCol = effect.Parameters.["LightColor"]
        if not(isNull pCol) then pCol.SetValue(lightColors.[0])

      let pCount = effect.Parameters.["DirectionalLightCount"]

      if not(isNull pCount) then
        pCount.SetValue(float32 lightDirs.Length)
    else
      let pCount = effect.Parameters.["DirectionalLightCount"]

      if not(isNull pCount) then
        pCount.SetValue(0.0f)

    // Point Lights
    let pointLightData =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Point pl ->
          Some(Vector4(pl.Position.X, pl.Position.Y, pl.Position.Z, pl.Range))
        | _ -> None)

    let pointLightColors =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Point pl -> Some(Vector4(pl.Color.ToVector3() * pl.Intensity, 1.0f))
        | _ -> None)

    if pointLightData.Length > 0 then
      let pParams = effect.Parameters.["PointLightData"]

      if not(isNull pParams) then
        pParams.SetValue(pointLightData)

      let pColors = effect.Parameters.["PointLightColors"]

      if not(isNull pColors) then
        pColors.SetValue(pointLightColors)

      let pCount = effect.Parameters.["PointLightCount"]

      if not(isNull pCount) then
        pCount.SetValue(float32 pointLightData.Length)
    else
      let pCount = effect.Parameters.["PointLightCount"]

      if not(isNull pCount) then
        pCount.SetValue(0.0f)

    // Spot Lights
    let spotLightData =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(Vector4(sl.Position.X, sl.Position.Y, sl.Position.Z, sl.Range))
        | _ -> None)

    let spotLightArgs =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(
            Vector4(
              sl.Direction.X,
              sl.Direction.Y,
              sl.Direction.Z,
              float32(System.Math.Cos(float sl.OuterConeAngle))
            )
          )
        | _ -> None)

    let spotLightColors =
      state.CurrentLighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(
            Vector4(
              sl.Color.ToVector3() * sl.Intensity,
              float32(System.Math.Cos(float sl.InnerConeAngle))
            )
          )
        | _ -> None)

    if spotLightData.Length > 0 then
      let pData = effect.Parameters.["SpotLightData"]
      if not(isNull pData) then pData.SetValue(spotLightData)

      let pArgs = effect.Parameters.["SpotLightArgs"]
      if not(isNull pArgs) then pArgs.SetValue(spotLightArgs)

      let pCols = effect.Parameters.["SpotLightColors"]
      if not(isNull pCols) then pCols.SetValue(spotLightColors)

      let pCount = effect.Parameters.["SpotLightCount"]
      if not(isNull pCount) then pCount.SetValue(float32 spotLightData.Length)
    else
      let pCount = effect.Parameters.["SpotLightCount"]
      if not(isNull pCount) then pCount.SetValue(0.0f)

    // Shadow Mapping
    if state.ShadowMaps.Count > 0 && state.ShadowViewMatrices.Count > 0 then
      let pShadowMap = effect.Parameters.["ShadowMap"]

      if not(isNull pShadowMap) then
        pShadowMap.SetValue(state.ShadowMaps.[0])

      let pLightView = effect.Parameters.["LightView"]

      if not(isNull pLightView) then
        pLightView.SetValue(state.ShadowViewMatrices.[0])

      let pLightProj = effect.Parameters.["LightProjection"]

      if not(isNull pLightProj) then
        pLightProj.SetValue(state.ShadowProjectionMatrices.[0])

    // Try to get color/texture from Material, fallback to mesh's BasicEffect
    let albedoColor, hasTexture, albedoTex =
      match drawable.Material.PBR.AlbedoMap with
      | ValueSome tex -> drawable.Material.PBR.AlbedoColor, true, tex
      | ValueNone ->
        // Fallback: try to extract from mesh's embedded BasicEffect
        match mesh.Effect with
        | :? BasicEffect as be ->
          let color =
            Color(be.DiffuseColor.X, be.DiffuseColor.Y, be.DiffuseColor.Z)

          if not(isNull be.Texture) then
            color, true, be.Texture
          else
            color, false, Unchecked.defaultof<_>
        | _ -> drawable.Material.PBR.AlbedoColor, false, Unchecked.defaultof<_>

    setParam "AlbedoColor" albedoColor
    setParam "Metallic" drawable.Material.PBR.Metallic
    setParam "Roughness" drawable.Material.PBR.Roughness

    if hasTexture then
      setParam "HasAlbedoMap" 1.0f
      setParam "AlbedoMap" albedoTex
    else
      setParam "HasAlbedoMap" 0.0f

    match drawable.Material.PBR.NormalMap with
    | ValueSome tex -> setParam "NormalMap" tex
    | ValueNone -> ()

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let setTarget (device: GraphicsDevice) (target: RenderTarget2D) =
    let current = device.GetRenderTargets()

    if isNull(box target) then
      if current.Length > 0 then
        device.SetRenderTarget(null)
    elif
      current.Length <> 1 || current.[0].RenderTarget <> (target :> Texture)
    then
      device.SetRenderTarget(target)

  let renderDrawableFallback (state: PipelineState) (drawable: Drawable) =
    let mesh = drawable.Mesh
    // Use per-drawable effect override if present, otherwise fall back to mesh effect
    let effect =
      match drawable.EffectOverride with
      | ValueSome e -> e
      | ValueNone -> mesh.Effect

    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    // Set standard matrices
    match box effect with
    | :? IEffectMatrices as em ->
      em.World <- drawable.Transform
      em.View <- state.CurrentCamera.View
      em.Projection <- state.CurrentCamera.Projection
    | _ ->
      // Try to set parameters by name if not implementing IEffectMatrices
      let set (name: string) (m: Matrix) =
        let p = effect.Parameters.[name]

        if not(isNull p) then
          p.SetValue(m)

      set "World" drawable.Transform
      set "View" state.CurrentCamera.View
      set "Projection" state.CurrentCamera.Projection

    // Apply Material overrides ONLY if they are not default
    match effect with
    | :? BasicEffect as be ->
      // Lighting
      configureBasicEffectLighting be state.CurrentLighting

      // Color override
      if drawable.Material.PBR.AlbedoColor <> Color.White then
        be.DiffuseColor <- drawable.Material.PBR.AlbedoColor.ToVector3()
        be.Alpha <- float32 drawable.Material.PBR.AlbedoColor.A / 255f

      // Texture override
      match drawable.Material.PBR.AlbedoMap with
      | ValueSome tex ->
        be.TextureEnabled <- true
        be.Texture <- tex
      | ValueNone -> ()
    | _ -> ()

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let renderDrawableShadow
    (state: PipelineState)
    (drawable: Drawable)
    (effect: Effect)
    (view: Matrix)
    (projection: Matrix)
    =
    let mesh = drawable.Mesh
    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    effect.Parameters.["World"].SetValue(drawable.Transform)
    effect.Parameters.["View"].SetValue(view)
    effect.Parameters.["Projection"].SetValue(projection)

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let renderShadowPass(state: PipelineState) =
    match state.CustomShaders.TryGetValue(ShaderBase.ShadowCaster) with
    | true, shadowEffect ->
      let frustum =
        BoundingFrustum(
          state.CurrentCamera.View * state.CurrentCamera.Projection
        )

      let corners = frustum.GetCorners()

      let mutable shadowMapIndex = 0

      for light in state.CurrentLighting.Lights do
        match light with
        | Directional dl when
          ValueOption.isSome dl.Shadow
          && shadowMapIndex < state.ShadowMaps.Count
          ->
          let shadowMap = state.ShadowMaps.[shadowMapIndex]
          state.Device.SetRenderTarget(shadowMap)

          state.Device.Clear(
            ClearOptions.Target ||| ClearOptions.DepthBuffer,
            Color.White,
            1.0f,
            0
          )

          // Compute frustum center and sphere
          let mutable center = Vector3.Zero

          for i in 0 .. corners.Length - 1 do
            center <- center + corners.[i]

          center <- center / float32 corners.Length

          let mutable radius = 0f

          for i in 0 .. corners.Length - 1 do
            radius <- max radius (Vector3.Distance(center, corners.[i]))

          // Compute Light View looking at frustum center
          // Move light position back along direction to ensure it covers everything
          let lightPos = center - dl.Direction * (radius + 100f)
          let lightView = Matrix.CreateLookAt(lightPos, center, Vector3.Up)

          // Transform frustum corners to light space to find bounds
          let mutable minX, minY = infinityf, infinityf
          let mutable maxX, maxY = -infinityf, -infinityf

          for i in 0 .. corners.Length - 1 do
            let lp = Vector3.Transform(corners.[i], lightView)
            minX <- min minX lp.X
            maxX <- max maxX lp.X
            minY <- min minY lp.Y
            maxY <- max maxY lp.Y

          // Include the actual objects to be shadowed to tighten the frustum
          for i in 0 .. state.OpaqueDrawables.Count - 1 do
            let struct (_, d) = state.OpaqueDrawables.[i]
            let lp = Vector3.Transform(d.BoundingSphere.Center, lightView)
            let r = d.BoundingSphere.Radius
            minX <- min minX (lp.X - r)
            maxX <- max maxX (lp.X + r)
            minY <- min minY (lp.Y - r)
            maxY <- max maxY (lp.Y + r)

          // Set safe Z range relative to light source
          // Since lightPos = center - lightDir * (radius + 100)
          // Near plane can be small, far plane covers the whole radius-based range
          let padding = 10f

          let lightProj =
            Matrix.CreateOrthographicOffCenter(
              minX - padding,
              maxX + padding,
              minY - padding,
              maxY + padding,
              0.1f,
              (radius + 100f) * 2f
            )

          // Set state for shadow rendering
          state.Device.DepthStencilState <- DepthStencilState.Default
          state.Device.RasterizerState <- RasterizerState.CullNone
          state.Device.BlendState <- BlendState.Opaque

          for i in 0 .. state.OpaqueDrawables.Count - 1 do
            let struct (_, drawable) = state.OpaqueDrawables.[i]

            if drawable.Material.Flags.HasFlag(MaterialFlags.CastsShadow) then
              renderDrawableShadow
                state
                drawable
                shadowEffect
                lightView
                lightProj

          // Reset rasterizer state for main pass
          state.Device.RasterizerState <- RasterizerState.CullCounterClockwise

          state.ShadowViewMatrices.Add(lightView)
          state.ShadowProjectionMatrices.Add(lightProj)

          shadowMapIndex <- shadowMapIndex + 1
        | _ -> ()

      match state.MainSceneTarget with
      | ValueSome rt ->
        System.Diagnostics.Debug.WriteLine(
          $"[ShadowPass] Restoring MainSceneTarget {rt.Width}x{rt.Height}"
        )

        setTarget state.Device rt
      | ValueNone ->
        System.Diagnostics.Debug.WriteLine(
          "[ShadowPass] Restoring to backbuffer (null)"
        )

        setTarget state.Device null
    | false, _ ->
      System.Diagnostics.Debug.WriteLine(
        "[ShadowPass] No ShadowCaster shader - skipped"
      )

  let renderFullScreenQuad (device: GraphicsDevice) (effect: Effect) =
    let vertices = [|
      VertexPositionTexture(Vector3(-1f, 1f, 0f), Vector2(0f, 0f))
      VertexPositionTexture(Vector3(1f, 1f, 0f), Vector2(1f, 0f))
      VertexPositionTexture(Vector3(-1f, -1f, 0f), Vector2(0f, 1f))
      VertexPositionTexture(Vector3(1f, -1f, 0f), Vector2(1f, 1f))
    |]

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()
      device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2)

  let renderPostProcess (state: PipelineState) (sceneTarget: RenderTarget2D) =
    match state.Config.PostProcess with
    | ValueSome pp ->
      // 1. Bloom Pass (Structural)
      pp.Bloom
      |> ValueOption.iter(fun bloomCfg ->
        match state.CustomShaders.TryGetValue(ShaderBase.Bloom) with
        | true, bloomEffect ->
          if not(isNull bloomEffect.Parameters.["Threshold"]) then
            bloomEffect.Parameters.["Threshold"].SetValue(bloomCfg.Threshold)

          if not(isNull bloomEffect.Parameters.["Intensity"]) then
            bloomEffect.Parameters.["Intensity"].SetValue(bloomCfg.Intensity)
          // Shared.renderFullScreenQuad state.Device bloomEffect
          ()
        | false, _ -> ())

      // 2. Final Post-Process (ToneMapping, Gamma, SSAO composite)
      let tmMode =
        match pp.ToneMapping with
        | ToneMappingConfig.NoToneMapping -> 0
        | Reinhard -> 1
        | ACES -> 2
        | Filmic -> 3
        | AgX -> 4

      match state.CustomShaders.TryGetValue(ShaderBase.PostProcess) with
      | true, ppEffect ->
        setTarget state.Device null // Back to backbuffer

        if not(isNull ppEffect.Parameters.["SceneTexture"]) then
          ppEffect.Parameters.["SceneTexture"].SetValue(sceneTarget)

        if not(isNull ppEffect.Parameters.["ToneMapping"]) then
          ppEffect.Parameters.["ToneMapping"].SetValue(tmMode)

        renderFullScreenQuad state.Device ppEffect
      | false, _ ->
        setTarget state.Device null

        if not(isNull(box state.SpriteBatch)) then
          state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          state.Device.SamplerStates.[0] <- SamplerState.PointClamp

          state.SpriteBatch.Draw(
            sceneTarget,
            state.Device.Viewport.Bounds,
            Color.White
          )

          state.SpriteBatch.End()
    | ValueNone -> ()

  /// Batch a drawable into the appropriate list (opaque or transparent)
  /// Performs frustum culling.
  let batchDrawable (state: PipelineState) (drawable: Drawable) =
#if DEBUG
    if not state.CameraWasSet then
      System.Diagnostics.Debug.WriteLine(
        "[Pipeline] Warning: Draw called without SetCamera"
      )
#endif

    // Frustum culling
    if not(isVisible state.CurrentCamera drawable) then
      () // Culled, do nothing
    else
      let distance = distanceToCamera state.CurrentCamera drawable

      if isTransparent drawable then
        state.TransparentDrawables.Add(struct (distance, drawable))
      else
        state.OpaqueDrawables.Add(struct (distance, drawable))

// ============================================================================
// Forward - Forward rendering implementation
// ============================================================================

module internal Forward =

  // --- Core batch operations (no external dependencies) ---

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      () // Nothing to flush
    else
      // Sort opaque: front-to-back (ascending distance = less overdraw)
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      // Sort transparent: back-to-front (descending distance = correct blending)
      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // Render Shadows
      Shared.renderShadowPass state

      // Render opaque first (with depth write)
      state.Device.DepthStencilState <- DepthStencilState.Default

      let draw(d: Drawable) =
        match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
        | true, effect -> Shared.renderDrawableWithEffect state d effect
        | false, _ -> Shared.renderDrawableFallback state d

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        draw drawable

      // Render transparent (with alpha blending, depth read but no write)
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          draw drawable
        // Restore defaults
        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default

      // Clear batches for next camera pass
      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  // --- State commands (call flushDrawBatch before changing state) ---

  let processSetCamera
    (state: PipelineState)
    (camera: Mibo.Rendering.Graphics3D.Camera)
    =
    flushDrawBatch state
    state.CurrentCamera <- camera
    state.CameraWasSet <- true
    Shared.configureBasicEffectCamera state.BasicEffect camera
    Shared.configureBasicEffectLighting state.BasicEffect state.CurrentLighting

  let processSetLighting (state: PipelineState) (lighting: LightingState) =
    state.CurrentLighting <- lighting
    Shared.configureBasicEffectLighting state.BasicEffect lighting

  let processSetViewport (state: PipelineState) (viewport: Viewport) =
    flushDrawBatch state
    state.Device.Viewport <- viewport

  let processClearTarget
    (state: PipelineState)
    (colorOpt: Color voption)
    (clearDepth: bool)
    =
    flushDrawBatch state

    let flags =
      match colorOpt, clearDepth with
      | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
      | ValueSome _, false -> ClearOptions.Target
      | ValueNone, true -> ClearOptions.DepthBuffer
      | ValueNone, false -> ClearOptions.Target

    let color = colorOpt |> ValueOption.defaultValue Color.Black
    state.Device.Clear(flags, color, 1f, 0)

  let processCustomDraw
    (state: PipelineState)
    (drawFn: GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit)
    =
    flushDrawBatch state
    drawFn state.Device state.CurrentCamera

  // --- Command dispatch ---

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match cmd with
    | SetCamera camera -> processSetCamera state camera
    | SetLighting lighting -> processSetLighting state lighting
    | SetViewport viewport -> processSetViewport state viewport
    | SetMode mode ->
      flushDrawBatch state
      state.Config <- { state.Config with Mode = mode }
    | ClearTarget(colorOpt, clearDepth) ->
      processClearTarget state colorOpt clearDepth
    | Draw drawable -> Shared.batchDrawable state drawable
    | DrawCustom drawFn -> processCustomDraw state drawFn

  // --- Main render entry point ---

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    Shared.resetFrameState state

    // Acquire scene target if needed (for post-processing, shadows, or advanced modes)
    let needsTarget =
      state.Config.PostProcess.IsSome
      || state.Config.Shadows.IsSome
      || state.Config.Mode <> PipelineMode.Forward

    let sceneTarget =
      if needsTarget && not(isNull(box state.RtPool)) then
        let spec = {
          Width = state.Device.PresentationParameters.BackBufferWidth
          Height = state.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Color
          DepthFormat = DepthFormat.Depth24
        }

        let rt = state.RtPool.Acquire spec
        Shared.setTarget state.Device rt
        state.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    // Process all commands
    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    // Flush any remaining draws
    flushDrawBatch state

    // Post-process
    sceneTarget |> ValueOption.iter(fun rt -> Shared.renderPostProcess state rt)

    // Final Blit to backbuffer if we used an intermediate target and didn't post-process (which blits to null)
    match sceneTarget with
    | ValueSome rt when state.Config.PostProcess.IsNone ->
      System.Diagnostics.Debug.WriteLine(
        $"[Pipeline] Blitting RT to backbuffer. RT size: {rt.Width}x{rt.Height}"
      )

      Shared.setTarget state.Device null

      if not(isNull(box state.SpriteBatch)) then
        state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
        state.Device.SamplerStates.[0] <- SamplerState.PointClamp
        state.SpriteBatch.Draw(rt, state.Device.Viewport.Bounds, Color.White)
        state.SpriteBatch.End()
        System.Diagnostics.Debug.WriteLine("[Pipeline] Blit complete")
      else
        System.Diagnostics.Debug.WriteLine(
          "[Pipeline] WARNING: SpriteBatch is null!"
        )
    | ValueSome _ ->
      System.Diagnostics.Debug.WriteLine("[Pipeline] PostProcess handles blit")
    | ValueNone ->
      System.Diagnostics.Debug.WriteLine(
        "[Pipeline] No RT - rendering directly to backbuffer"
      )

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()
// ============================================================================
// ForwardPlus - Forward+ rendering
// ============================================================================

module internal ForwardPlus =
  // Forward+ handles many lights via tile-based light culling.
  let TileSize = 16

  type LightGrid = {
    TilesX: int
    TilesY: int
    TileData: int[][] // Indices of lights per tile
  }

  /// Projects a sphere to screen-space AABB
  let private projectSphere
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (viewport: Viewport)
    (center: Vector3)
    (radius: float32)
    =
    let viewPos = Vector3.Transform(center, camera.View)

    // Simplistic AABB projection: find min/max in view space and project
    // In a real impl, this would be more precise
    let points = [|
      viewPos + Vector3(-radius, -radius, 0f)
      viewPos + Vector3(radius, radius, 0f)
    |]

    let mutable minX, minY = 1f, 1f
    let mutable maxX, maxY = -1f, -1f

    for p in points do
      let clip = Vector4.Transform(p, camera.Projection)
      let ndc = Vector2(clip.X / clip.W, clip.Y / clip.W)
      minX <- min minX ndc.X
      minY <- min minY ndc.Y
      maxX <- max maxX ndc.X
      maxY <- max maxY ndc.Y

    // NDC [-1, 1] to Viewport [0, Size]
    let toScreen x size = (x + 1f) * 0.5f * float32 size
    let left = toScreen minX viewport.Width |> int |> max 0
    let right = toScreen maxX viewport.Width |> int |> min viewport.Width
    let top = toScreen -maxY viewport.Height |> int |> max 0 // Flip Y
    let bottom = toScreen -minY viewport.Height |> int |> min viewport.Height

    struct (left, top, right, bottom)

  let cullLights(state: PipelineState) : LightGrid * uint32[] =
    let viewport =
      if isNull(box state.Device) then
        Viewport(0, 0, 1280, 720)
      else
        state.Device.Viewport

    let tilesX = (viewport.Width + TileSize - 1) / TileSize
    let tilesY = (viewport.Height + TileSize - 1) / TileSize

    let tileMasks = Array.zeroCreate<uint32>(tilesX * tilesY)

    let lights = state.CurrentLighting.Lights

    // Only cull point/spot lights (Directional lights affect all tiles)
    // We only support up to 32 lights in the bitmask
    for i = 0 to min 31 (lights.Length - 1) do
      match lights.[i] with
      | Directional _ ->
        // Directional lights affect all tiles
        for j = 0 to tileMasks.Length - 1 do
          tileMasks.[j] <- tileMasks.[j] ||| (1u <<< i)
      | Point pl ->
        let struct (l, t, r, b) =
          projectSphere state.CurrentCamera viewport pl.Position pl.Range

        for ty = t / TileSize to b / TileSize do
          if ty >= 0 && ty < tilesY then
            for tx = l / TileSize to r / TileSize do
              if tx >= 0 && tx < tilesX then
                tileMasks.[ty * tilesX + tx] <-
                  tileMasks.[ty * tilesX + tx] ||| (1u <<< i)
      | Spot sl ->
        // Use conservative sphere projection for spot lights
        let struct (l, t, r, b) =
          projectSphere state.CurrentCamera viewport sl.Position sl.Range

        for ty = t / TileSize to b / TileSize do
          if ty >= 0 && ty < tilesY then
            for tx = l / TileSize to r / TileSize do
              if tx >= 0 && tx < tilesX then
                tileMasks.[ty * tilesX + tx] <-
                  tileMasks.[ty * tilesX + tx] ||| (1u <<< i)

    {
      TilesX = tilesX
      TilesY = tilesY
      TileData = [||] // No longer used, using bitmasks instead
    },
    tileMasks

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      ()
    else
      // 1. Sort
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // Render Shadows
      Shared.renderShadowPass state

      // 2. Light Culling
      let _lightGrid, _tileMasks = cullLights state

      // 3. Render Opaque
      state.Device.DepthStencilState <- DepthStencilState.Default

      let draw(d: Drawable) =
        match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
        | true, effect -> Shared.renderDrawableWithEffect state d effect
        | false, _ -> Shared.renderDrawableFallback state d

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        draw drawable

      // 4. Render Transparent
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          draw drawable

        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default

      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    // Reusing Forward's logic for command processing, but calling our flush
    match cmd with
    | SetCamera camera ->
      flushDrawBatch state
      state.CurrentCamera <- camera
      state.CameraWasSet <- true
      Shared.configureBasicEffectCamera state.BasicEffect camera

      Shared.configureBasicEffectLighting
        state.BasicEffect
        state.CurrentLighting
    | SetLighting lighting ->
      state.CurrentLighting <- lighting
      Shared.configureBasicEffectLighting state.BasicEffect lighting
    | SetMode mode ->
      flushDrawBatch state
      state.Config <- { state.Config with Mode = mode }
    | SetViewport viewport ->
      flushDrawBatch state
      state.Device.Viewport <- viewport
    | ClearTarget(colorOpt, clearDepth) ->
      flushDrawBatch state
      // Same clear logic
      let flags =
        match colorOpt, clearDepth with
        | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
        | ValueSome _, false -> ClearOptions.Target
        | ValueNone, true -> ClearOptions.DepthBuffer
        | ValueNone, false -> ClearOptions.Target

      let color = colorOpt |> ValueOption.defaultValue Color.Black
      state.Device.Clear(flags, color, 1f, 0)
    | Draw drawable -> Shared.batchDrawable state drawable
    | DrawCustom drawFn ->
      flushDrawBatch state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    if not(state.CustomShaders.ContainsKey(ShaderBase.PBRForward)) then
      Forward.render state buffer
    else
      Shared.resetFrameState state

      // Acquire scene target if needed (for post-processing, shadows, or advanced modes)
      let needsTarget =
        state.Config.PostProcess.IsSome
        || state.Config.Shadows.IsSome
        || state.Config.Mode <> PipelineMode.Forward

      let sceneTarget =
        if needsTarget && not(isNull(box state.RtPool)) then
          let spec = {
            Width = state.Device.PresentationParameters.BackBufferWidth
            Height = state.Device.PresentationParameters.BackBufferHeight
            Format = SurfaceFormat.Color
            DepthFormat = DepthFormat.Depth24
          }

          let rt = state.RtPool.Acquire spec
          Shared.setTarget state.Device rt
          state.MainSceneTarget <- ValueSome rt
          ValueSome rt
        else
          ValueNone

      for i in 0 .. buffer.Count - 1 do
        let struct (_, cmd) = buffer.[i]
        processCommand state cmd

      flushDrawBatch state

      // Post-process
      sceneTarget
      |> ValueOption.iter(fun rt -> Shared.renderPostProcess state rt)

      // Final Blit to backbuffer
      match sceneTarget with
      | ValueSome rt when state.Config.PostProcess.IsNone ->
        Shared.setTarget state.Device null

        if not(isNull(box state.SpriteBatch)) then
          state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          state.Device.SamplerStates.[0] <- SamplerState.PointClamp
          state.SpriteBatch.Draw(rt, state.Device.Viewport.Bounds, Color.White)
          state.SpriteBatch.End()
      | _ -> ()

      if not(isNull(box state.RtPool)) then
        state.RtPool.ReleaseAll()

// ============================================================================
// Deferred - Deferred rendering implementation
// ============================================================================

module internal Deferred =

  let renderGBuffer(state: PipelineState) =
    // Setup G-Buffer RenderTargets
    if not(isNull(box state.RtPool)) then
      let width = state.Device.PresentationParameters.BackBufferWidth
      let height = state.Device.PresentationParameters.BackBufferHeight

      let albedoSpec = {
        Width = width
        Height = height
        Format = SurfaceFormat.Color
        DepthFormat = DepthFormat.Depth24
      }

      let normalSpec = {
        Width = width
        Height = height
        Format = SurfaceFormat.Vector4
        DepthFormat = DepthFormat.None
      }

      let worldPosSpec = {
        Width = width
        Height = height
        Format = SurfaceFormat.Vector4
        DepthFormat = DepthFormat.None
      }

      let rtAlbedo = state.RtPool.Acquire albedoSpec
      let rtNormal = state.RtPool.Acquire normalSpec

      let rtWorldPos =
        state.RtPool.Acquire {
          worldPosSpec with
              DepthFormat = DepthFormat.Depth24
        }

      match state.CustomShaders.TryGetValue(ShaderBase.GBufferFill) with
      | true, effect ->
        // Clear targets individually to ensure correct default values
        // Albedo: Transparent + Clear its Depth Buffer (master depth)
        state.Device.SetRenderTarget(rtAlbedo)

        state.Device.Clear(
          ClearOptions.Target ||| ClearOptions.DepthBuffer,
          Color.Transparent,
          1.0f,
          0
        )

        // Normal: Transparent
        state.Device.SetRenderTarget(rtNormal)
        state.Device.Clear(Color.Transparent)

        // WorldPos: Transparent (0,0,0,0)
        state.Device.SetRenderTarget(rtWorldPos)
        state.Device.Clear(Color.Transparent)

        // Bind all for MRT.
        // Note: state.Device will use rtAlbedo's depth buffer because it's first.
        state.Device.SetRenderTargets(
          new RenderTargetBinding(rtAlbedo),
          new RenderTargetBinding(rtNormal),
          new RenderTargetBinding(rtWorldPos)
        )

        // Render all opaque objects to G-Buffer
        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]
          Shared.renderDrawableWithEffect state drawable effect

        // Restore scene target or backbuffer for lighting pass
        match state.MainSceneTarget with
        | ValueSome rt -> Shared.setTarget state.Device rt
        | ValueNone -> Shared.setTarget state.Device null

        ValueSome(rtAlbedo, rtNormal, rtWorldPos)
      | false, _ ->
        // Fallback: Ensure correct target is set before drawing
        match state.MainSceneTarget with
        | ValueSome rt -> Shared.setTarget state.Device rt
        | ValueNone -> Shared.setTarget state.Device null

        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]
          Shared.renderDrawableFallback state drawable

        ValueNone
    else
      match state.MainSceneTarget with
      | ValueSome rt -> Shared.setTarget state.Device rt
      | ValueNone -> Shared.setTarget state.Device null

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        Shared.renderDrawableFallback state drawable

      ValueNone

  let bindLighting (effect: Effect) (lighting: LightingState) =
    // Set ambient color
    let pAmbient = effect.Parameters.["AmbientColor"]

    if not(isNull pAmbient) then
      pAmbient.SetValue(
        lighting.AmbientColor.ToVector3() * lighting.AmbientIntensity
      )

    // Bind Directional Lights
    let lightDirs =
      lighting.Lights
      |> Array.choose (function
        | Directional dl -> Some dl.Direction
        | _ -> None)

    let lightColors =
      lighting.Lights
      |> Array.choose (function
        | Directional dl -> Some(dl.Color.ToVector3() * dl.Intensity)
        | _ -> None)

    let pDirs = effect.Parameters.["LightDirections"]
    if not(isNull pDirs) then
      pDirs.SetValue(lightDirs)
    else
      // Fallback: Try binding single directional light
      let pDir = effect.Parameters.["LightDirection"]
      if not(isNull pDir) && lightDirs.Length > 0 then
        pDir.SetValue(lightDirs.[0])

    let pCols = effect.Parameters.["LightColors"]
    if not(isNull pCols) then
      pCols.SetValue(lightColors)
    else
      // Fallback: Try binding single directional light color
      let pCol = effect.Parameters.["LightColor"]
      if not(isNull pCol) && lightColors.Length > 0 then
        pCol.SetValue(lightColors.[0])

    let pDirCount = effect.Parameters.["DirectionalLightCount"]
    if not(isNull pDirCount) then
      pDirCount.SetValue(float32 lightDirs.Length)

    // Bind Point Lights
    let pointLightData =
      lighting.Lights
      |> Array.choose (function
        | Point pl ->
          Some(Vector4(pl.Position.X, pl.Position.Y, pl.Position.Z, pl.Range))
        | _ -> None)

    let pointLightColors =
      lighting.Lights
      |> Array.choose (function
        | Point pl ->
          Some(Vector4(pl.Color.ToVector3() * pl.Intensity, 1.0f))
        | _ -> None)

    if pointLightData.Length > 0 then
      let pData = effect.Parameters.["PointLightData"]
      if not(isNull pData) then pData.SetValue(pointLightData)

      let pColors = effect.Parameters.["PointLightColors"]
      if not(isNull pColors) then pColors.SetValue(pointLightColors)

      let pCount = effect.Parameters.["PointLightCount"]
      if not(isNull pCount) then pCount.SetValue(float32 pointLightData.Length)
    else
      let pCount = effect.Parameters.["PointLightCount"]
      if not(isNull pCount) then pCount.SetValue(0.0f)

    // Bind Spot Lights
    let spotLightData =
      lighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(Vector4(sl.Position.X, sl.Position.Y, sl.Position.Z, sl.Range))
        | _ -> None)

    let spotLightArgs =
      lighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(
            Vector4(
              sl.Direction.X,
              sl.Direction.Y,
              sl.Direction.Z,
              float32(System.Math.Cos(float sl.OuterConeAngle))
            )
          )
        | _ -> None)

    let spotLightColors =
      lighting.Lights
      |> Array.choose (function
        | Spot sl ->
          Some(
            Vector4(
              sl.Color.ToVector3() * sl.Intensity,
              float32(System.Math.Cos(float sl.InnerConeAngle))
            )
          )
        | _ -> None)

    if spotLightData.Length > 0 then
      let pData = effect.Parameters.["SpotLightData"]
      if not(isNull pData) then pData.SetValue(spotLightData)

      let pArgs = effect.Parameters.["SpotLightArgs"]
      if not(isNull pArgs) then pArgs.SetValue(spotLightArgs)

      let pCols = effect.Parameters.["SpotLightColors"]
      if not(isNull pCols) then pCols.SetValue(spotLightColors)

      let pCount = effect.Parameters.["SpotLightCount"]
      if not(isNull pCount) then pCount.SetValue(float32 spotLightData.Length)
    else
      let pCount = effect.Parameters.["SpotLightCount"]
      if not(isNull pCount) then pCount.SetValue(0.0f)

  let renderLighting
    (state: PipelineState)
    (gBuffer: (RenderTarget2D * RenderTarget2D * RenderTarget2D) voption)
    =


    match gBuffer with
    | ValueSome(albedo, normal, depth) ->
      let hasDeferredShader =
        state.CustomShaders.ContainsKey(ShaderBase.DeferredLighting)



      match state.CustomShaders.TryGetValue(ShaderBase.DeferredLighting) with
      | true, effect ->

        // Bind G-Buffer textures
        if not(isNull effect.Parameters.["AlbedoMap"]) then
          effect.Parameters.["AlbedoMap"].SetValue(albedo)

        if not(isNull effect.Parameters.["NormalMap"]) then
          effect.Parameters.["NormalMap"].SetValue(normal)

        if not(isNull effect.Parameters.["WorldPosMap"]) then
          effect.Parameters.["WorldPosMap"].SetValue(depth)

        // Bind Point Light Data
        let pointLightData =
          state.CurrentLighting.Lights
          |> Array.choose (function
            | Point pl ->
              Some(
                Vector4(pl.Position.X, pl.Position.Y, pl.Position.Z, pl.Range)
              )
            | _ -> None)

        let pointLightColors =
          state.CurrentLighting.Lights
          |> Array.choose (function
            | Point pl ->
              Some(Vector4(pl.Color.ToVector3() * pl.Intensity, 1.0f))
            | _ -> None)

        if pointLightData.Length > 0 then
          let pData = pointLightData |> Array.truncate 32
          let pCols = pointLightColors |> Array.truncate 32

          let pParams = effect.Parameters.["PointLightData"]

          if not(isNull pParams) then
            pParams.SetValue(pData)

          let pColors = effect.Parameters.["PointLightColors"]

          if not(isNull pColors) then
            pColors.SetValue(pCols)

          let pCount = effect.Parameters.["PointLightCount"]

          if not(isNull pCount) then
            pCount.SetValue(float32 pData.Length)
        else
          let pCount = effect.Parameters.["PointLightCount"]

          if not(isNull pCount) then
            pCount.SetValue(0.0f)

        // Bind Shadow Data
        if state.ShadowMaps.Count > 0 && state.ShadowViewMatrices.Count > 0 then


          let pShadowMap = effect.Parameters.["ShadowMap"]

          if not(isNull pShadowMap) then
            pShadowMap.SetValue(state.ShadowMaps.[0])

          let pLightView = effect.Parameters.["LightView"]

          if not(isNull pLightView) then
            pLightView.SetValue(state.ShadowViewMatrices.[0])

          let pLightProj = effect.Parameters.["LightProjection"]

          if not(isNull pLightProj) then
            pLightProj.SetValue(state.ShadowProjectionMatrices.[0])
        else
          printfn "[Deferred.renderLighting] NO shadow maps available!"

        bindLighting effect state.CurrentLighting

        state.Device.DepthStencilState <- DepthStencilState.None
        Shared.renderFullScreenQuad state.Device effect
        state.Device.DepthStencilState <- DepthStencilState.Default
      | false, _ ->
        // Fallback: blit albedo to current target
        if not(isNull(box state.SpriteBatch)) then
          // Use AlphaBlend if we want to respect existing background,
          // but Opaque is faster if we assume GBuffer is full screen.
          state.SpriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend
          )

          state.Device.SamplerStates.[0] <- SamplerState.PointClamp

          state.SpriteBatch.Draw(
            albedo,
            state.Device.Viewport.Bounds,
            Color.White
          )

          state.SpriteBatch.End()
    | ValueNone -> ()

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      ()
    else
      // Sort
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // Render Shadows
      Shared.renderShadowPass state

      // Set Opaque state
      state.Device.DepthStencilState <- DepthStencilState.Default

      // Pass 1: G-Buffer (Opaque)
      let gBuffer = renderGBuffer state

      // Pass 2: Lighting (Opaque)
      renderLighting state gBuffer

      // Pass 3: Transparent (Forward)
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend

        let draw(d: Drawable) =
          match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
          | true, effect -> Shared.renderDrawableWithEffect state d effect
          | false, _ -> Shared.renderDrawableFallback state d

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          draw drawable

        state.Device.BlendState <- BlendState.Opaque

      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match cmd with
    | SetCamera camera ->
      flushDrawBatch state
      state.CurrentCamera <- camera
      state.CameraWasSet <- true
      Shared.configureBasicEffectCamera state.BasicEffect camera
    | SetLighting lighting -> state.CurrentLighting <- lighting
    | SetMode mode ->
      flushDrawBatch state
      state.Config <- { state.Config with Mode = mode }
    | SetViewport viewport ->
      flushDrawBatch state
      state.Device.Viewport <- viewport
    | ClearTarget(colorOpt, clearDepth) ->
      flushDrawBatch state

      let flags =
        match colorOpt, clearDepth with
        | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
        | ValueSome _, false -> ClearOptions.Target
        | ValueNone, true -> ClearOptions.DepthBuffer
        | ValueNone, false -> ClearOptions.Target

      let color = colorOpt |> ValueOption.defaultValue Color.Black

      state.Device.Clear(flags, color, 1f, 0)
    | Draw drawable -> Shared.batchDrawable state drawable
    | DrawCustom drawFn ->
      flushDrawBatch state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    let hasShaders =
      state.CustomShaders.ContainsKey(ShaderBase.GBufferFill)
      && state.CustomShaders.ContainsKey(ShaderBase.DeferredLighting)

    if not hasShaders then
      Forward.render state buffer
    else
      Shared.resetFrameState state

      // Acquire scene target if needed (for post-processing, shadows, or advanced modes)
      let needsTarget =
        state.Config.PostProcess.IsSome
        || state.Config.Shadows.IsSome
        || state.Config.Mode <> PipelineMode.Forward

      let sceneTarget =
        if needsTarget && not(isNull(box state.RtPool)) then
          let spec = {
            Width = state.Device.PresentationParameters.BackBufferWidth
            Height = state.Device.PresentationParameters.BackBufferHeight
            Format = SurfaceFormat.Color
            DepthFormat = DepthFormat.Depth24
          }

          let rt = state.RtPool.Acquire spec
          Shared.setTarget state.Device rt
          state.MainSceneTarget <- ValueSome rt
          ValueSome rt
        else
          ValueNone

      for i in 0 .. buffer.Count - 1 do
        let struct (_, cmd) = buffer.[i]
        processCommand state cmd

      flushDrawBatch state

      // Post-process
      sceneTarget
      |> ValueOption.iter(fun rt -> Shared.renderPostProcess state rt)

      // Final Blit to backbuffer
      match sceneTarget with
      | ValueSome rt when state.Config.PostProcess.IsNone ->
        Shared.setTarget state.Device null

        if not(isNull(box state.SpriteBatch)) then
          state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          state.Device.SamplerStates.[0] <- SamplerState.PointClamp
          state.SpriteBatch.Draw(rt, state.Device.Viewport.Bounds, Color.White)
          state.SpriteBatch.End()
      | _ -> ()

      if not(isNull(box state.RtPool)) then
        state.RtPool.ReleaseAll()


// ============================================================================
// Render Pipeline - Unified Orchestration
// ============================================================================

module internal Orchestrate =

  /// Dispatches a command to the appropriate mode-specific handler
  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match state.Config.Mode with
    | PipelineMode.Forward -> Forward.processCommand state cmd
    | PipelineMode.ForwardPlus -> ForwardPlus.processCommand state cmd
    | PipelineMode.Deferred -> Deferred.processCommand state cmd

  /// Flushes any pending draws for the current mode
  let flushDrawBatch(state: PipelineState) =
    match state.Config.Mode with
    | PipelineMode.Forward -> Forward.flushDrawBatch state
    | PipelineMode.ForwardPlus -> ForwardPlus.flushDrawBatch state
    | PipelineMode.Deferred -> Deferred.flushDrawBatch state

  /// Main entry point for the rendering pipeline
  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    Shared.resetFrameState state

    // 1. Determine if we need an intermediate scene target
    let needsTarget =
      state.Config.PostProcess.IsSome
      || state.Config.Shadows.IsSome
      || state.Config.Mode <> PipelineMode.Forward

    let sceneTarget =
      if needsTarget && not(isNull(box state.RtPool)) then
        let spec = {
          Width = state.Device.PresentationParameters.BackBufferWidth
          Height = state.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Color
          DepthFormat = DepthFormat.Depth24
        }

        let rt = state.RtPool.Acquire spec
        Shared.setTarget state.Device rt
        state.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    // 2. Process all commands in the buffer
    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    // 3. Final flush for any remaining drawables
    flushDrawBatch state

    // 4. Post-processing (handles final blit to backbuffer if present)
    sceneTarget |> ValueOption.iter(fun rt -> Shared.renderPostProcess state rt)

    // 5. Final Blit to backbuffer if we used an intermediate target but didn't post-process
    match sceneTarget with
    | ValueSome rt when state.Config.PostProcess.IsNone ->
      Shared.setTarget state.Device null

      if not(isNull(box state.SpriteBatch)) then
        state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
        state.Device.SamplerStates.[0] <- SamplerState.PointClamp
        state.SpriteBatch.Draw(rt, state.Device.Viewport.Bounds, Color.White)
        state.SpriteBatch.End()
    | _ -> ()

    // 6. Cleanup
    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()

  let inline initialize
    (state: PipelineState)
    (game: Game)
    (gd: GraphicsDevice)
    =
    state.Device <- gd
    state.RtPool <- RenderTargetPool.create gd
    state.SpriteBatch <- new SpriteBatch(gd)

    // BasicEffect as fallback (all modes can use this)
    state.BasicEffect <- new BasicEffect(gd)
    state.BasicEffect.EnableDefaultLighting()
    state.BasicEffect.PreferPerPixelLighting <- true
    state.BasicEffect.SpecularColor <- Vector3.Zero
    state.BasicEffect.SpecularPower <- 1f

    // Set default lighting from config
    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    // Load custom shaders from content
    for KeyValue(shaderBase, assetName) in state.Config.ShaderOverrides do
      state.CustomShaders.[shaderBase] <- game.Content.Load<Effect>(assetName)

    // Pre-allocate shadow maps (only if shadow shader available)
    if state.CustomShaders.ContainsKey(ShaderBase.ShadowCaster) then
      state.Config.Shadows
      |> ValueOption.iter(fun cfg ->
        for _ in 0 .. cfg.CascadeCount - 1 do
          state.ShadowMaps.Add(
            new RenderTarget2D(
              gd,
              cfg.Resolution,
              cfg.Resolution,
              false,
              SurfaceFormat.Single,
              DepthFormat.Depth24
            )
          ))

// ============================================================================
// RenderPipeline - Public API
// ============================================================================

module RenderPipeline =

  /// Create a rendering pipeline based on configuration
  let create (config: PipelineConfig) (game: Game) : IRenderPipeline =
    let state = Shared.createState config

    { new IRenderPipeline with
        member _.Initialize(gd) = Orchestrate.initialize state game gd
        member _.Render(_, buffer) = Orchestrate.render state buffer
    }
