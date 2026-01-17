namespace Mibo.Rendering.Graphics3D

open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Render Pipeline Implementation
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
  DiscreteShadowMaps: ResizeArray<RenderTarget2D>
  mutable ShadowMapArray: Texture2D voption // Texture2DArray for DX
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
// Internal Modules - Semantic Organization
// ============================================================================

module internal State =

  let create(config: PipelineConfig) : PipelineState = {
    Config = config
    Device = Unchecked.defaultof<_>
    RtPool = Unchecked.defaultof<_>
    BasicEffect = Unchecked.defaultof<_>
    SpriteBatch = Unchecked.defaultof<_>
    CustomShaders = Dictionary<ShaderBase, Effect>()
    DiscreteShadowMaps = ResizeArray<RenderTarget2D>()
    ShadowMapArray = ValueNone
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

  let reset(state: PipelineState) =
    state.CurrentCamera <- Camera.identity

    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    state.CameraWasSet <- false
    state.OpaqueDrawables.Clear()
    state.TransparentDrawables.Clear()
    state.ShadowViewMatrices.Clear()
    state.ShadowProjectionMatrices.Clear()
    state.MainSceneTarget <- ValueNone

  let initialize
    (state: PipelineState)
    (game: Game)
    (gd: GraphicsDevice)
    =
    state.Device <- gd
    state.RtPool <- RenderTargetPool.create gd
    state.SpriteBatch <- new SpriteBatch(gd)

    // BasicEffect as fallback
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

    // Pre-allocate shadow maps
    if state.CustomShaders.ContainsKey(ShaderBase.ShadowCaster) then
      state.Config.Shadows
      |> ValueOption.iter(fun cfg ->
        // Use the user-defined toggle, default to Discrete for Auto
        let useArray =
          match state.Config.ShadowPath with
          | ForceArray -> true
          | ForceDiscrete
          | Auto -> false

        if useArray then
          // Allocation for Texture2DArray/Modern path
          // Note: In MonoGame, we can use a RenderTarget2D with ArraySize > 1 on supported platforms
          // For now, we'll keep the discrete collection but flag the intent to use Array binding
          ()

        // Ensure we have enough discrete maps for the chosen or fallback path
        let totalNeeded = cfg.CascadeCount + cfg.MaxPointShadows * 6 + 16
        if state.DiscreteShadowMaps.Count < totalNeeded then
          for _ in state.DiscreteShadowMaps.Count .. totalNeeded - 1 do
            state.DiscreteShadowMaps.Add(
              new RenderTarget2D(
                gd,
                cfg.Resolution,
                cfg.Resolution,
                false,
                SurfaceFormat.Single,
                DepthFormat.Depth24
              )
            ))

module internal EffectHelpers =
  /// Helper to find a parameter by name, avoiding indexer ambiguity in F#
  let findParam (name: string) (effect: Effect) =
    effect.Parameters
    |> Seq.cast<EffectParameter>
    |> Seq.tryFind (fun p -> p.Name = name)

  let setParam (name: string) (value: obj) (effect: Effect) =
    match findParam name effect with
    | Some p ->
        match value with
        | :? Matrix as m -> p.SetValue(m)
        | :? (Matrix[]) as ma -> p.SetValue(ma)
        | :? Vector3 as v -> p.SetValue(v)
        | :? (Vector3[]) as va -> p.SetValue(va)
        | :? Vector4 as v -> p.SetValue(v)
        | :? (Vector4[]) as va -> p.SetValue(va)
        | :? float32 as f -> p.SetValue(f)
        | :? bool as b -> p.SetValue(b)
        | :? Texture as t -> p.SetValue(t)
        | :? Color as c -> p.SetValue(c.ToVector4())
        | _ -> ()
    | None -> ()

module internal Culling =

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

  let batchDrawable (state: PipelineState) (drawable: Drawable) =
#if DEBUG
    if not state.CameraWasSet then
      System.Diagnostics.Debug.WriteLine(
        "[Pipeline] Warning: Draw called without SetCamera"
      )
#endif

    if not(isVisible state.CurrentCamera drawable) then
      ()
    else
      let distance = distanceToCamera state.CurrentCamera drawable

      if isTransparent drawable then
        state.TransparentDrawables.Add(struct (distance, drawable))
      else
        state.OpaqueDrawables.Add(struct (distance, drawable))

module internal Tiling =
  let TileSize = 16

  /// Projects a sphere to screen-space AABB
  let private projectSphere
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (viewport: Viewport)
    (center: Vector3)
    (radius: float32)
    =
    let viewPos = Vector3.Transform(center, camera.View)

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

    let toScreen x size = (x + 1f) * 0.5f * float32 size
    let left = toScreen minX viewport.Width |> int |> max 0
    let right = toScreen maxX viewport.Width |> int |> min viewport.Width
    let top = toScreen -maxY viewport.Height |> int |> max 0 // Flip Y
    let bottom = toScreen -minY viewport.Height |> int |> min viewport.Height

    struct (left, top, right, bottom)

  let cullLights(state: PipelineState) : uint32[] =
    let viewport =
      if isNull (box state.Device) then
        Viewport(0, 0, 1280, 720)
      else
        state.Device.Viewport

    let tilesX = (viewport.Width + TileSize - 1) / TileSize
    let tilesY = (viewport.Height + TileSize - 1) / TileSize

    let tileMasks = Array.zeroCreate<uint32>(tilesX * tilesY)
    let lights = state.CurrentLighting.Lights

    for i = 0 to min 31 (lights.Length - 1) do
      match lights.[i] with
      | Directional _ ->
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
        let struct (l, t, r, b) =
          projectSphere state.CurrentCamera viewport sl.Position sl.Range

        for ty = t / TileSize to b / TileSize do
          if ty >= 0 && ty < tilesY then
            for tx = l / TileSize to r / TileSize do
              if tx >= 0 && tx < tilesX then
                tileMasks.[ty * tilesX + tx] <-
                  tileMasks.[ty * tilesX + tx] ||| (1u <<< i)

    tileMasks

module internal ShadowPass =

  let computeSpotShadowMatrices
    (sl: SpotLight)
    : struct (Matrix * Matrix) =
    let view =
      Matrix.CreateLookAt(sl.Position, sl.Position + sl.Direction, Vector3.Up)

    let proj =
      Matrix.CreatePerspectiveFieldOfView(
        sl.OuterConeAngle * 2.0f,
        1.0f,
        0.1f,
        sl.Range
      )

    struct (view, proj)

  let computePointShadowMatrices
    (pl: PointLight)
    (face: int)
    : struct (Matrix * Matrix) =
    let view =
      match face with
      | 0 -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Right, Vector3.Up) // +X
      | 1 -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Left, Vector3.Up) // -X
      | 2 -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Up, Vector3.Backward) // +Y
      | 3 -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Down, Vector3.Forward) // -Y
      | 4 -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Forward, Vector3.Up) // +Z
      | _ -> Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Backward, Vector3.Up) // -Z

    let proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver2, 1.0f, 0.1f, pl.Range)
    struct (view, proj)

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

    EffectHelpers.setParam "World" drawable.Transform effect
    EffectHelpers.setParam "View" view effect
    EffectHelpers.setParam "Projection" projection effect

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let render (state: PipelineState) =
    match state.CustomShaders.TryGetValue(ShaderBase.ShadowCaster) with
    | true, shadowEffect ->
      let mainFrustum =
        BoundingFrustum(
          state.CurrentCamera.View * state.CurrentCamera.Projection
        )

      let corners = mainFrustum.GetCorners()
      let mutable shadowMapIndex = 0

      // Common shadow rendering state
      state.Device.DepthStencilState <- DepthStencilState.Default
      state.Device.RasterizerState <- RasterizerState.CullNone
      state.Device.BlendState <- BlendState.Opaque

      let renderPass (view: Matrix) (proj: Matrix) =
        let lightFrustum = BoundingFrustum(view * proj)

        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]

          if
            drawable.Material.Flags.HasFlag(MaterialFlags.CastsShadow)
            && lightFrustum.Contains(drawable.BoundingSphere)
               <> ContainmentType.Disjoint
          then
            renderDrawableShadow
              state
              drawable
              shadowEffect
              view
              proj

      for light in state.CurrentLighting.Lights do
        if shadowMapIndex < state.DiscreteShadowMaps.Count then
          match light with
          | Directional dl when ValueOption.isSome dl.Shadow ->
            let shadowMap = state.DiscreteShadowMaps.[shadowMapIndex]
            state.Device.SetRenderTarget(shadowMap)

            state.Device.Clear(
              ClearOptions.Target ||| ClearOptions.DepthBuffer,
              Color.White,
              1.0f,
              0
            )

            let mutable center = Vector3.Zero
            for i in 0 .. corners.Length - 1 do center <- center + corners.[i]
            center <- center / float32 corners.Length

            let mutable radius = 0f
            for i in 0 .. corners.Length - 1 do
              radius <- max radius (Vector3.Distance(center, corners.[i]))

            let lightPos = center - dl.Direction * (radius + 100f)
            let lightView = Matrix.CreateLookAt(lightPos, center, Vector3.Up)

            let mutable minX, minY = infinityf, infinityf
            let mutable maxX, maxY = -infinityf, -infinityf

            for i in 0 .. corners.Length - 1 do
              let lp = Vector3.Transform(corners.[i], lightView)
              minX <- min minX lp.X
              maxX <- max maxX lp.X
              minY <- min minY lp.Y
              maxY <- max maxY lp.Y

            for i in 0 .. state.OpaqueDrawables.Count - 1 do
              let struct (_, d) = state.OpaqueDrawables.[i]
              let lp = Vector3.Transform(d.BoundingSphere.Center, lightView)
              let r = d.BoundingSphere.Radius
              minX <- min minX (lp.X - r)
              maxX <- max maxX (lp.X + r)
              minY <- min minY (lp.Y - r)
              maxY <- max maxY (lp.Y + r)

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

            renderPass lightView lightProj

            state.ShadowViewMatrices.Add(lightView)
            state.ShadowProjectionMatrices.Add(lightProj)
            shadowMapIndex <- shadowMapIndex + 1

          | Spot sl when ValueOption.isSome sl.Shadow ->
            let shadowMap = state.DiscreteShadowMaps.[shadowMapIndex]
            state.Device.SetRenderTarget(shadowMap)
            state.Device.Clear(ClearOptions.Target ||| ClearOptions.DepthBuffer, Color.White, 1.0f, 0)

            let struct (view, proj) = computeSpotShadowMatrices sl
            renderPass view proj

            state.ShadowViewMatrices.Add(view)
            state.ShadowProjectionMatrices.Add(proj)
            shadowMapIndex <- shadowMapIndex + 1

          | Point pl when ValueOption.isSome pl.Shadow ->
            if shadowMapIndex + 6 <= state.DiscreteShadowMaps.Count then
              for face = 0 to 5 do
                let shadowMap = state.DiscreteShadowMaps.[shadowMapIndex + face]
                state.Device.SetRenderTarget(shadowMap)
                state.Device.Clear(ClearOptions.Target ||| ClearOptions.DepthBuffer, Color.White, 1.0f, 0)

                let struct (view, proj) = computePointShadowMatrices pl face
                renderPass view proj

                state.ShadowViewMatrices.Add(view)
                state.ShadowProjectionMatrices.Add(proj)

              shadowMapIndex <- shadowMapIndex + 6
          | _ -> ()

      state.Device.RasterizerState <- RasterizerState.CullCounterClockwise

      match state.MainSceneTarget with
      | ValueSome rt -> state.Device.SetRenderTarget(rt)
      | ValueNone -> state.Device.SetRenderTarget(null)
    | false, _ -> ()

module internal Drawing =

  let configureBasicEffectLighting
    (effect: BasicEffect)
    (lighting: LightingState)
    =
    effect.LightingEnabled <- true

    effect.AmbientLightColor <-
      lighting.AmbientColor.ToVector3() * lighting.AmbientIntensity

    effect.DirectionalLight0.Enabled <- false
    effect.DirectionalLight1.Enabled <- false
    effect.DirectionalLight2.Enabled <- false

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
        | _ -> ()

  let configureBasicEffectCamera
    (effect: BasicEffect)
    (camera: Mibo.Rendering.Graphics3D.Camera)
    =
    effect.View <- camera.View
    effect.Projection <- camera.Projection

  let drawWithEffect
    (state: PipelineState)
    (drawable: Drawable)
    (effect: Effect)
    =
    let mesh = drawable.Mesh
    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    EffectHelpers.setParam "World" drawable.Transform effect
    EffectHelpers.setParam "View" state.CurrentCamera.View effect
    EffectHelpers.setParam "Projection" state.CurrentCamera.Projection effect

    // Phase 6: Custom Lighting Binding Override
    match state.Config.LightingBinder with
    | ValueSome binder ->
        binder effect state.CurrentCamera state.CurrentLighting
    | ValueNone ->
        // Standard Lighting Binding Logic
        EffectHelpers.setParam
          "AmbientColor"
          (state.CurrentLighting.AmbientColor.ToVector3()
           * state.CurrentLighting.AmbientIntensity)
          effect

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
          match EffectHelpers.findParam "LightDirections" effect with
          | Some p -> p.SetValue(lightDirs)
          | None ->
              EffectHelpers.setParam "LightDirection" lightDirs.[0] effect

          match EffectHelpers.findParam "LightColors" effect with
          | Some p -> p.SetValue(lightColors)
          | None ->
              EffectHelpers.setParam "LightColor" lightColors.[0] effect

          EffectHelpers.setParam "DirectionalLightCount" (float32 lightDirs.Length) effect
        else
          EffectHelpers.setParam "DirectionalLightCount" 0.0f effect

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
          match EffectHelpers.findParam "PointLightData" effect with
          | Some p -> p.SetValue(pointLightData)
          | None -> ()

          match EffectHelpers.findParam "PointLightColors" effect with
          | Some p -> p.SetValue(pointLightColors)
          | None -> ()

          EffectHelpers.setParam "PointLightCount" (float32 pointLightData.Length) effect
        else
          EffectHelpers.setParam "PointLightCount" 0.0f effect

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
          match EffectHelpers.findParam "SpotLightData" effect with
          | Some p -> p.SetValue(spotLightData)
          | None -> ()

          match EffectHelpers.findParam "SpotLightArgs" effect with
          | Some p -> p.SetValue(spotLightArgs)
          | None -> ()

          match EffectHelpers.findParam "SpotLightColors" effect with
          | Some p -> p.SetValue(spotLightColors)
          | None -> ()

          EffectHelpers.setParam "SpotLightCount" (float32 spotLightData.Length) effect
        else
          EffectHelpers.setParam "SpotLightCount" 0.0f effect

    if state.ShadowViewMatrices.Count > 0 then
      // 1. Matrices
      match EffectHelpers.findParam "LightViews" effect with
      | Some p -> p.SetValue(state.ShadowViewMatrices.ToArray())
      | None ->
          EffectHelpers.setParam "LightView" state.ShadowViewMatrices.[0] effect

      match EffectHelpers.findParam "LightProjections" effect with
      | Some p -> p.SetValue(state.ShadowProjectionMatrices.ToArray())
      | None ->
          EffectHelpers.setParam "LightProjection" state.ShadowProjectionMatrices.[0] effect

      // 2. Textures
      let useArray =
        match state.Config.ShadowPath with
        | ForceArray -> true
        | ForceDiscrete
        | Auto -> false

      if useArray then
        // Modern Path: Bind as array if possible
        match state.ShadowMapArray with
        | ValueSome texArray ->
            EffectHelpers.setParam "ShadowMapArray" texArray effect
        | ValueNone ->
            if state.DiscreteShadowMaps.Count > 0 then
              EffectHelpers.setParam "ShadowMap" (state.DiscreteShadowMaps.[0] :> Texture) effect
      else
        // Restricted Path: Bind to discrete slots
        let findParam (name: string) =
          effect.Parameters
          |> Seq.cast<EffectParameter>
          |> Seq.tryFind (fun p -> p.Name = name)

        for i = 0 to min 7 (state.DiscreteShadowMaps.Count - 1) do
          match findParam $"ShadowMap{i}" with
          | Some p -> p.SetValue(state.DiscreteShadowMaps.[i] :> Texture)
          | None -> ()

      // 4. Shadow Config
      state.Config.Shadows |> ValueOption.iter (fun cfg ->
        EffectHelpers.setParam "ShadowBias" cfg.Bias effect
        EffectHelpers.setParam "ShadowNormalBias" cfg.NormalBias effect
      )

    let albedoColor, hasTexture, albedoTex =
      match drawable.Material.PBR.AlbedoMap with
      | ValueSome tex -> drawable.Material.PBR.AlbedoColor, true, tex
      | ValueNone ->
        match mesh.Effect with
        | :? BasicEffect as be ->
          let color =
            Color(be.DiffuseColor.X, be.DiffuseColor.Y, be.DiffuseColor.Z)

          if not(isNull be.Texture) then
            color, true, be.Texture
          else
            color, false, Unchecked.defaultof<_>
        | _ -> drawable.Material.PBR.AlbedoColor, false, Unchecked.defaultof<_>

    EffectHelpers.setParam "AlbedoColor" albedoColor effect
    EffectHelpers.setParam "Metallic" drawable.Material.PBR.Metallic effect
    EffectHelpers.setParam "Roughness" drawable.Material.PBR.Roughness effect

    if hasTexture then
      EffectHelpers.setParam "HasAlbedoMap" 1.0f effect
      EffectHelpers.setParam "AlbedoMap" albedoTex effect
    else
      EffectHelpers.setParam "HasAlbedoMap" 0.0f effect

    match drawable.Material.PBR.NormalMap with
    | ValueSome tex -> EffectHelpers.setParam "NormalMap" tex effect
    | ValueNone -> ()

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let drawFallback (state: PipelineState) (drawable: Drawable) =
    let mesh = drawable.Mesh
    let effect =
      match drawable.EffectOverride with
      | ValueSome e -> e
      | ValueNone -> mesh.Effect

    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    match box effect with
    | :? IEffectMatrices as em ->
      em.World <- drawable.Transform
      em.View <- state.CurrentCamera.View
      em.Projection <- state.CurrentCamera.Projection
    | _ ->
      EffectHelpers.setParam "World" drawable.Transform effect
      EffectHelpers.setParam "View" state.CurrentCamera.View effect
      EffectHelpers.setParam "Projection" state.CurrentCamera.Projection effect

    match effect with
    | :? BasicEffect as be ->
      configureBasicEffectLighting be state.CurrentLighting

      if drawable.Material.PBR.AlbedoColor <> Color.White then
        be.DiffuseColor <- drawable.Material.PBR.AlbedoColor.ToVector3()
        be.Alpha <- float32 drawable.Material.PBR.AlbedoColor.A / 255f

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

  let flush (state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      ()
    else
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      ShadowPass.render state

      let _tileMasks = Tiling.cullLights state

      state.Device.DepthStencilState <- DepthStencilState.Default

      let draw(d: Drawable) =
        match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
        | true, effect -> drawWithEffect state d effect
        | false, _ -> drawFallback state d

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        draw drawable

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

module internal PostProcess =

  let render (state: PipelineState) (sceneTarget: RenderTarget2D) =
    match state.Config.PostProcess with
    | ValueSome pp ->
      pp.Bloom
      |> ValueOption.iter(fun bloomCfg ->
        match state.CustomShaders.TryGetValue(ShaderBase.Bloom) with
        | true, bloomEffect ->
          EffectHelpers.setParam "Threshold" bloomCfg.Threshold bloomEffect
          EffectHelpers.setParam "Intensity" bloomCfg.Intensity bloomEffect
          ()
        | false, _ -> ())

      let tmMode =
        match pp.ToneMapping with
        | ToneMappingConfig.NoToneMapping -> 0
        | Reinhard -> 1
        | ACES -> 2
        | Filmic -> 3
        | AgX -> 4

      match state.CustomShaders.TryGetValue(ShaderBase.PostProcess) with
      | true, ppEffect ->
        state.Device.SetRenderTarget(null)
        EffectHelpers.setParam "SceneTexture" sceneTarget ppEffect
        EffectHelpers.setParam "ToneMapping" tmMode ppEffect

        let vertices = [|
          VertexPositionTexture(Vector3(-1f, 1f, 0f), Vector2(0f, 0f))
          VertexPositionTexture(Vector3(1f, 1f, 0f), Vector2(1f, 0f))
          VertexPositionTexture(Vector3(-1f, -1f, 0f), Vector2(0f, 1f))
          VertexPositionTexture(Vector3(1f, -1f, 0f), Vector2(1f, 1f))
        |]

        for pass in ppEffect.CurrentTechnique.Passes do
          pass.Apply()
          state.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2)
      | false, _ ->
        state.Device.SetRenderTarget(null)

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

module internal Orchestrate =

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match cmd with
    | SetCamera camera ->
      Drawing.flush state
      state.CurrentCamera <- camera
      state.CameraWasSet <- true
      Drawing.configureBasicEffectCamera state.BasicEffect camera
      Drawing.configureBasicEffectLighting
        state.BasicEffect
        state.CurrentLighting
    | SetLighting lighting ->
      state.CurrentLighting <- lighting
      Drawing.configureBasicEffectLighting state.BasicEffect lighting
    | SetViewport viewport ->
      Drawing.flush state
      state.Device.Viewport <- viewport
    | ClearTarget(colorOpt, clearDepth) ->
      Drawing.flush state
      let flags =
        match colorOpt, clearDepth with
        | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
        | ValueSome _, false -> ClearOptions.Target
        | ValueNone, true -> ClearOptions.DepthBuffer
        | ValueNone, false -> ClearOptions.Target

      let color = colorOpt |> ValueOption.defaultValue Color.Black
      state.Device.Clear(flags, color, 1f, 0)
    | Draw drawable -> Culling.batchDrawable state drawable
    | DrawCustom drawFn ->
      Drawing.flush state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    State.reset state

    let needsTarget =
      state.Config.PostProcess.IsSome
      || state.Config.Shadows.IsSome

    let sceneTarget =
      if needsTarget && not(isNull(box state.RtPool)) then
        let spec = {
          Width = state.Device.PresentationParameters.BackBufferWidth
          Height = state.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Color
          DepthFormat = DepthFormat.Depth24
        }

        let rt = state.RtPool.Acquire spec
        state.Device.SetRenderTarget(rt)
        state.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    state.Config.PreRenderCallback |> ValueOption.iter (fun cb ->
      cb state.Device state.CurrentCamera state.CurrentLighting
    )

    Drawing.flush state

    sceneTarget
    |> ValueOption.iter(fun rt -> PostProcess.render state rt)

    match sceneTarget with
    | ValueSome rt when state.Config.PostProcess.IsNone ->
      state.Device.SetRenderTarget(null)

      if not(isNull(box state.SpriteBatch)) then
        state.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
        state.Device.SamplerStates.[0] <- SamplerState.PointClamp
        state.SpriteBatch.Draw(rt, state.Device.Viewport.Bounds, Color.White)
        state.SpriteBatch.End()
    | _ -> ()

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()

// ============================================================================
// RenderPipeline - Public API
// ============================================================================

module RenderPipeline =

  let create (config: PipelineConfig) (game: Game) : IRenderPipeline =
    let state = State.create config

    { new IRenderPipeline with
        member _.Initialize(gd) = State.initialize state game gd
        member _.Render(_, buffer) = Orchestrate.render state buffer
    }