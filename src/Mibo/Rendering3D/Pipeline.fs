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
      let dirs = lightDirs |> Array.truncate 3
      let colors = lightColors |> Array.truncate 3

      let pDirs = effect.Parameters.["LightDirections"]

      if not(isNull pDirs) then
        pDirs.SetValue(dirs)

      let pCols = effect.Parameters.["LightColors"]

      if not(isNull pCols) then
        pCols.SetValue(colors)

    setParam "AlbedoColor" drawable.Material.PBR.AlbedoColor
    setParam "Metallic" drawable.Material.PBR.Metallic
    setParam "Roughness" drawable.Material.PBR.Roughness

    match drawable.Material.PBR.AlbedoMap with
    | ValueSome tex -> setParam "AlbedoMap" tex
    | ValueNone -> ()

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

          // Compute Light View fitting the camera frustum
          let lightView =
            Matrix.CreateLookAt(Vector3.Zero, dl.Direction, Vector3.Up)

          // Transform frustum corners to light space to find bounds
          let mutable minX, minY, minZ = infinityf, infinityf, infinityf
          let mutable maxX, maxY, maxZ = -infinityf, -infinityf, -infinityf

          for i in 0 .. corners.Length - 1 do
            let lp = Vector3.Transform(corners.[i], lightView)
            minX <- min minX lp.X
            minY <- min minY lp.Y
            minZ <- min minZ lp.Z
            maxX <- max maxX lp.X
            maxY <- max maxY lp.Y
            maxZ <- max maxZ lp.Z

          // Add some padding and depth room
          let lightProj =
            Matrix.CreateOrthographicOffCenter(
              minX,
              maxX,
              minY,
              maxY,
              minZ - 50.0f,
              maxZ
            )

          for i in 0 .. state.OpaqueDrawables.Count - 1 do
            let struct (_, drawable) = state.OpaqueDrawables.[i]

            if drawable.Material.Flags.HasFlag(MaterialFlags.CastsShadow) then
              renderDrawableShadow
                state
                drawable
                shadowEffect
                lightView
                lightProj

          shadowMapIndex <- shadowMapIndex + 1
        | _ -> ()

      match state.MainSceneTarget with
      | ValueSome rt -> setTarget state.Device rt
      | ValueNone -> setTarget state.Device null
    | false, _ -> ()

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

  let cullLights(state: PipelineState) : LightGrid =
    let viewport =
      if isNull(box state.Device) then
        Viewport(0, 0, 1280, 720)
      else
        state.Device.Viewport

    let tilesX = (viewport.Width + TileSize - 1) / TileSize
    let tilesY = (viewport.Height + TileSize - 1) / TileSize

    let tileData = Array.init (tilesX * tilesY) (fun _ -> ResizeArray<int>())

    let lights = state.CurrentLighting.Lights

    for i = 0 to lights.Length - 1 do
      match lights.[i] with
      | Directional _ ->
        // Directional lights affect all tiles
        for j = 0 to tileData.Length - 1 do
          tileData.[j].Add(i)
      | Point pl ->
        let struct (l, t, r, b) =
          projectSphere state.CurrentCamera viewport pl.Position pl.Range

        for ty = t / TileSize to b / TileSize do
          if ty >= 0 && ty < tilesY then
            for tx = l / TileSize to r / TileSize do
              if tx >= 0 && tx < tilesX then
                tileData.[ty * tilesX + tx].Add(i)
      | Spot sl ->
        let struct (l, t, r, b) =
          projectSphere state.CurrentCamera viewport sl.Position sl.Range

        for ty = t / TileSize to b / TileSize do
          if ty >= 0 && ty < tilesY then
            for tx = l / TileSize to r / TileSize do
              if tx >= 0 && tx < tilesX then
                tileData.[ty * tilesX + tx].Add(i)

    {
      TilesX = tilesX
      TilesY = tilesY
      TileData = tileData |> Array.map(fun x -> x.ToArray())
    }

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
      let _lightGrid = cullLights state

      // 3. Render Opaque
      state.Device.DepthStencilState <- DepthStencilState.Default

      let draw(d: Drawable) =
        match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
        | true, effect ->
          // Bind tile data
          if not(isNull effect.Parameters.["TilesX"]) then
            effect.Parameters.["TilesX"].SetValue(_lightGrid.TilesX)

          if not(isNull effect.Parameters.["TilesY"]) then
            effect.Parameters.["TilesY"].SetValue(_lightGrid.TilesY)
          // Note: TileData binding would typically involve a Texture2D or StructuredBuffer in HLSL

          Shared.renderDrawableWithEffect state d effect
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

      // Acquire scene target if needed for post-processing
      let sceneTarget =
        match state.Config.PostProcess with
        | ValueSome _ when not(isNull(box state.RtPool)) ->
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
        | _ -> ValueNone

      for i in 0 .. buffer.Count - 1 do
        let struct (_, cmd) = buffer.[i]
        processCommand state cmd

      flushDrawBatch state

      // Post-process
      sceneTarget
      |> ValueOption.iter(fun rt -> Shared.renderPostProcess state rt)

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

      let rtAlbedo = state.RtPool.Acquire albedoSpec

      let rtNormal =
        state.RtPool.Acquire {
          normalSpec with
              DepthFormat = DepthFormat.Depth24
        }

      match state.CustomShaders.TryGetValue(ShaderBase.GBufferFill) with
      | true, effect ->
        state.Device.SetRenderTargets(
          new RenderTargetBinding(rtAlbedo),
          new RenderTargetBinding(rtNormal)
        )

        state.Device.Clear(Color.Transparent)

        // Render all opaque objects to G-Buffer
        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]
          Shared.renderDrawableWithEffect state drawable effect

        // Restore scene target or backbuffer for lighting pass
        match state.MainSceneTarget with
        | ValueSome rt -> Shared.setTarget state.Device rt
        | ValueNone -> Shared.setTarget state.Device null

        ValueSome(rtAlbedo, rtNormal)
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
    let setParam (name: string) (value: obj) =
      let p = effect.Parameters.[name]

      if not(isNull p) then
        match value with
        | :? Vector3 as v -> p.SetValue(v)
        | :? float32 as f -> p.SetValue(f)
        | :? (Vector3[]) as va -> p.SetValue(va)
        | _ -> ()

    setParam
      "AmbientColor"
      (lighting.AmbientColor.ToVector3() * lighting.AmbientIntensity)

    let lightDirs =
      lighting.Lights
      |> Array.choose (function
        | Directional dl -> Some dl.Direction
        | _ -> Microsoft.FSharp.Core.Option.None)

    let lightColors =
      lighting.Lights
      |> Array.choose (function
        | Directional dl -> Some(dl.Color.ToVector3() * dl.Intensity)
        | _ -> Microsoft.FSharp.Core.Option.None)

    if lightDirs.Length > 0 then
      setParam "LightDirections" lightDirs
      setParam "LightColors" lightColors

  let renderLighting
    (state: PipelineState)
    (gBuffer: (RenderTarget2D * RenderTarget2D) voption)
    =
    match gBuffer with
    | ValueSome(albedo, normal) ->
      match state.CustomShaders.TryGetValue(ShaderBase.DeferredLighting) with
      | true, effect ->
        // Bind G-Buffer textures
        if not(isNull effect.Parameters.["AlbedoMap"]) then
          effect.Parameters.["AlbedoMap"].SetValue(albedo)

        if not(isNull effect.Parameters.["NormalMap"]) then
          effect.Parameters.["NormalMap"].SetValue(normal)

        bindLighting effect state.CurrentLighting

        Shared.renderFullScreenQuad state.Device effect
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
      let color = colorOpt |> ValueOption.defaultValue Color.Black

      state.Device.Clear(
        ClearOptions.Target ||| ClearOptions.DepthBuffer,
        color,
        1f,
        0
      )
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

      // Acquire scene target if needed (always for Deferred to composite correctly)
      let sceneTarget =
        if not(isNull(box state.RtPool)) then
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

      // Final Blit if no post-processing
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
// Orchestrate - Dispatches to the correct render mode
// ============================================================================

module internal Orchestrate =

  let inline render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    match state.Config.Mode with
    | PipelineMode.Forward -> Forward.render state buffer
    | PipelineMode.ForwardPlus -> ForwardPlus.render state buffer
    | PipelineMode.Deferred -> Deferred.render state buffer

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
