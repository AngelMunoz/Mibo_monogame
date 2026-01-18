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

module internal ShadowAtlas =

  type State = {
    RenderTarget: RenderTarget2D
    Size: int
    TileSize: int
    TilesAcross: int
    MaxShadows: int
  }

  let create (device: GraphicsDevice) (config: ShadowConfig) =
    let tilesAcross = 4
    let maxShadows = tilesAcross * tilesAcross
    let atlasSize = config.Resolution * tilesAcross

    let maxTextureSize = 8192
    let actualAtlasSize = min atlasSize maxTextureSize
    let actualTileSize = actualAtlasSize / tilesAcross

    let rt =
      new RenderTarget2D(
        device,
        actualAtlasSize,
        actualAtlasSize,
        false,
        SurfaceFormat.Single,
        DepthFormat.Depth24,
        0,
        RenderTargetUsage.DiscardContents
      )

    {
      RenderTarget = rt
      Size = actualAtlasSize
      TileSize = actualTileSize
      TilesAcross = tilesAcross
      MaxShadows = maxShadows
    }

  let getViewport (state: State) (shadowIndex: int) : Viewport =
    let col = shadowIndex % state.TilesAcross
    let row = shadowIndex / state.TilesAcross
    let x = col * state.TileSize
    let y = row * state.TileSize
    Viewport(x, y, state.TileSize, state.TileSize)

  let dispose(state: State) = state.RenderTarget.Dispose()

/// Pipeline state container
type internal PipelineState = {
  mutable Config: PipelineConfig
  mutable Device: GraphicsDevice
  mutable RtPool: IRenderTargetPool
  mutable BasicEffect: BasicEffect
  mutable SpriteBatch: SpriteBatch
  CustomShaders: Dictionary<ShaderBase, Effect>
  mutable ShadowAtlas: ShadowAtlas.State voption
  ShadowViewMatrices: ResizeArray<Matrix>
  ShadowProjectionMatrices: ResizeArray<Matrix>
  mutable LightDataTexture: Texture2D voption
  mutable ShadowMatrixTexture: Texture2D voption
  mutable MainSceneTarget: RenderTarget2D voption
  mutable CurrentCamera: Mibo.Rendering.Graphics3D.Camera
  mutable CurrentLighting: LightingState
  mutable CameraWasSet: bool
  OpaqueDrawables: ResizeArray<struct (float32 * Drawable)>
  TransparentDrawables: ResizeArray<struct (float32 * Drawable)>
}

module internal State =

  let create(config: PipelineConfig) : PipelineState = {
    Config = config
    Device = Unchecked.defaultof<_>
    RtPool = Unchecked.defaultof<_>
    BasicEffect = Unchecked.defaultof<_>
    SpriteBatch = Unchecked.defaultof<_>
    CustomShaders = Dictionary<ShaderBase, Effect>()
    ShadowAtlas = ValueNone
    ShadowViewMatrices = ResizeArray<Matrix>()
    ShadowProjectionMatrices = ResizeArray<Matrix>()
    LightDataTexture = ValueNone
    ShadowMatrixTexture = ValueNone
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

  let initialize (state: PipelineState) (game: Game) (gd: GraphicsDevice) =
    state.Device <- gd
    state.RtPool <- RenderTargetPool.create gd
    state.SpriteBatch <- new SpriteBatch(gd)

    state.BasicEffect <- new BasicEffect(gd)
    state.BasicEffect.EnableDefaultLighting()
    state.BasicEffect.PreferPerPixelLighting <- true
    state.BasicEffect.SpecularColor <- Vector3.Zero
    state.BasicEffect.SpecularPower <- 1f

    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    for KeyValue(shaderBase, assetName) in state.Config.ShaderOverrides do
      state.CustomShaders.[shaderBase] <- game.Content.Load<Effect>(assetName)

    if state.CustomShaders.ContainsKey(ShaderBase.ShadowCaster) then
      state.Config.Shadows
      |> ValueOption.iter(fun cfg ->
        state.ShadowAtlas <- ValueSome(ShadowAtlas.create gd cfg))

[<AutoOpen>]
module internal EffectHelpers =
  let findParam (name: string) (effect: Effect) =
    let mutable found = ValueNone
    use enum = effect.Parameters.GetEnumerator()

    while enum.MoveNext() do
      let p = enum.Current

      if p.Name = name then
        found <- ValueSome p

    found

  type Effect with
    member this.SafeSetParam(name: string, value: bool) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: int) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Matrix) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Matrix[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Quaternion) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: float32) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: float32[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Texture) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Texture[]) =
      findParam name this
      |> ValueOption.iter(fun p ->
        for i = 0 to min (value.Length - 1) (p.Elements.Count - 1) do
          p.Elements.[i].SetValue(value.[i]))

    member this.SafeSetParam(name: string, value: Vector2) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Vector2[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Vector3) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Vector3[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Vector4) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Vector4[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member this.SafeSetParam(name: string, value: Color) =
      findParam name this
      |> ValueOption.iter(fun p -> p.SetValue(value.ToVector4()))

module internal LightPacking =
  let packLightData(state: PipelineState) =
    let lights = state.CurrentLighting.Lights

    if lights.Length = 0 then
      state.LightDataTexture <- ValueNone
    else
      let data = Array.zeroCreate<Vector4>(lights.Length * 4)
      let mutable shadowMapIndex = 0

      for i = 0 to lights.Length - 1 do
        let offset = i * 4

        match lights.[i] with
        | Directional dl ->
          let shadowIdx =
            if ValueOption.isSome dl.Shadow then
              let s = shadowMapIndex in
              shadowMapIndex <- shadowMapIndex + 1
              float32 s
            else
              -1.0f

          data.[offset + 0] <- Vector4(0.0f, dl.Intensity, 0.0f, shadowIdx)
          data.[offset + 1] <- Vector4.Zero

          data.[offset + 2] <-
            Vector4(dl.Direction.X, dl.Direction.Y, dl.Direction.Z, 0.0f)

          data.[offset + 3] <- Vector4(dl.Color.ToVector3(), 0.0f)
        | Point pl ->
          let shadowIdx =
            if ValueOption.isSome pl.Shadow then
              let s = shadowMapIndex in
              shadowMapIndex <- shadowMapIndex + 6
              float32 s
            else
              -1.0f

          data.[offset + 0] <- Vector4(1.0f, pl.Intensity, pl.Range, shadowIdx)

          data.[offset + 1] <-
            Vector4(pl.Position.X, pl.Position.Y, pl.Position.Z, 0.0f)

          data.[offset + 2] <- Vector4.Zero
          data.[offset + 3] <- Vector4(pl.Color.ToVector3(), 0.0f)
        | Spot sl ->
          let shadowIdx =
            if ValueOption.isSome sl.Shadow then
              let s = shadowMapIndex in
              shadowMapIndex <- shadowMapIndex + 1
              float32 s
            else
              -1.0f

          data.[offset + 0] <- Vector4(2.0f, sl.Intensity, sl.Range, shadowIdx)

          data.[offset + 1] <-
            Vector4(
              sl.Position.X,
              sl.Position.Y,
              sl.Position.Z,
              float32(System.Math.Cos(float sl.OuterConeAngle))
            )

          data.[offset + 2] <-
            Vector4(
              sl.Direction.X,
              sl.Direction.Y,
              sl.Direction.Z,
              float32(System.Math.Cos(float sl.InnerConeAngle))
            )

          data.[offset + 3] <- Vector4(sl.Color.ToVector3(), 0.0f)

      let tex =
        new Texture2D(
          state.Device,
          4,
          lights.Length,
          false,
          SurfaceFormat.Vector4
        )

      tex.SetData(data)
      state.LightDataTexture <- ValueSome tex

  let packShadowMatrices(state: PipelineState) =
    if state.ShadowViewMatrices.Count = 0 then
      state.ShadowMatrixTexture <- ValueNone
    else
      let count = state.ShadowViewMatrices.Count
      let data = Array.zeroCreate<Vector4>(count * 2 * 4)

      for i = 0 to count - 1 do
        let v = state.ShadowViewMatrices.[i]
        let p = state.ShadowProjectionMatrices.[i]
        let offset = i * 8
        data.[offset + 0] <- Vector4(v.M11, v.M12, v.M13, v.M14)
        data.[offset + 1] <- Vector4(v.M21, v.M22, v.M23, v.M24)
        data.[offset + 2] <- Vector4(v.M31, v.M32, v.M33, v.M34)
        data.[offset + 3] <- Vector4(v.M41, v.M42, v.M43, v.M44)
        data.[offset + 4] <- Vector4(p.M11, p.M12, p.M13, p.M14)
        data.[offset + 5] <- Vector4(p.M21, p.M22, p.M23, p.M24)
        data.[offset + 6] <- Vector4(p.M31, p.M32, p.M33, p.M34)
        data.[offset + 7] <- Vector4(p.M41, p.M42, p.M43, p.M44)

      let tex =
        new Texture2D(state.Device, 4, count * 2, false, SurfaceFormat.Vector4)

      tex.SetData(data)
      state.ShadowMatrixTexture <- ValueSome tex

module internal Culling =
  let isVisible
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    =
    let frustum = BoundingFrustum(camera.View * camera.Projection)
    frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

  let distanceToCamera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    =
    Vector3.DistanceSquared(camera.Position, drawable.BoundingSphere.Center)

  let isTransparent(drawable: Drawable) =
    drawable.Material.Flags.HasFlag(MaterialFlags.Transparent)
    || drawable.Material.PBR.AlbedoColor.A < 255uy

  let batchDrawable (state: PipelineState) (drawable: Drawable) =
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
    let top = toScreen -maxY viewport.Height |> int |> max 0
    let bottom = toScreen -minY viewport.Height |> int |> min viewport.Height
    struct (left, top, right, bottom)

  let cullLights(state: PipelineState) =
    let viewport =
      match state.Device with
      | null -> Viewport(0, 0, 1280, 720)
      | _ -> state.Device.Viewport

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
  let computeSpotShadowMatrices(sl: SpotLight) =
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

  let computePointShadowMatrices (pl: PointLight) (face: int) =
    let view =
      match face with
      | 0 ->
        Matrix.CreateLookAt(
          pl.Position,
          pl.Position + Vector3.Right,
          Vector3.Up
        )
      | 1 ->
        Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.Left, Vector3.Up)
      | 2 ->
        Matrix.CreateLookAt(
          pl.Position,
          pl.Position + Vector3.Up,
          Vector3.Backward
        )
      | 3 ->
        Matrix.CreateLookAt(
          pl.Position,
          pl.Position + Vector3.Down,
          Vector3.Forward
        )
      | 4 ->
        Matrix.CreateLookAt(
          pl.Position,
          pl.Position + Vector3.Forward,
          Vector3.Up
        )
      | _ ->
        Matrix.CreateLookAt(
          pl.Position,
          pl.Position + Vector3.Backward,
          Vector3.Up
        )

    let proj =
      Matrix.CreatePerspectiveFieldOfView(
        MathHelper.PiOver2,
        1.0f,
        0.1f,
        pl.Range
      )

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
    effect.SafeSetParam("World", drawable.Transform)
    effect.SafeSetParam("View", view)
    effect.SafeSetParam("Projection", projection)

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let render(state: PipelineState) =
    match
      state.CustomShaders.TryGetValue(ShaderBase.ShadowCaster),
      state.ShadowAtlas
    with
    | (true, shadowEffect), ValueSome atlas ->
      let mainFrustum =
        BoundingFrustum(
          state.CurrentCamera.View * state.CurrentCamera.Projection
        )

      let corners = mainFrustum.GetCorners()
      let mutable shadowMapIndex = 0

      state.Device.SetRenderTarget(atlas.RenderTarget)

      state.Device.Clear(
        ClearOptions.Target ||| ClearOptions.DepthBuffer,
        Color.White,
        1.0f,
        0
      )

      state.Device.DepthStencilState <- DepthStencilState.Default
      state.Device.RasterizerState <- RasterizerState.CullNone
      state.Device.BlendState <- BlendState.Opaque

      let renderPass (view: Matrix) (proj: Matrix) =
        if shadowMapIndex < atlas.MaxShadows then
          let vp = ShadowAtlas.getViewport atlas shadowMapIndex
          state.Device.Viewport <- vp
          let lightFrustum = BoundingFrustum(view * proj)

          for i in 0 .. state.OpaqueDrawables.Count - 1 do
            let struct (_, drawable) = state.OpaqueDrawables.[i]

            if
              drawable.Material.Flags.HasFlag(MaterialFlags.CastsShadow)
              && lightFrustum.Contains(drawable.BoundingSphere)
                 <> ContainmentType.Disjoint
            then
              renderDrawableShadow state drawable shadowEffect view proj

      for light in state.CurrentLighting.Lights do
        if shadowMapIndex < atlas.MaxShadows then
          match light with
          | Directional dl when ValueOption.isSome dl.Shadow ->
            let mutable center = Vector3.Zero

            for i in 0 .. corners.Length - 1 do
              center <- center + corners.[i]

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
            let struct (view, proj) = computeSpotShadowMatrices sl
            renderPass view proj
            state.ShadowViewMatrices.Add(view)
            state.ShadowProjectionMatrices.Add(proj)
            shadowMapIndex <- shadowMapIndex + 1
          | Point pl when ValueOption.isSome pl.Shadow ->
            if shadowMapIndex + 6 <= atlas.MaxShadows then
              for face = 0 to 5 do
                let struct (view, proj) = computePointShadowMatrices pl face
                renderPass view proj
                state.ShadowViewMatrices.Add(view)
                state.ShadowProjectionMatrices.Add(proj)
                shadowMapIndex <- shadowMapIndex + 1
          | _ -> ()

      state.Device.RasterizerState <- RasterizerState.CullCounterClockwise

      match state.MainSceneTarget with
      | ValueSome rt -> state.Device.SetRenderTarget(rt)
      | ValueNone -> state.Device.SetRenderTarget(null)
    | _ -> ()

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
    effect.SafeSetParam("World", drawable.Transform)
    effect.SafeSetParam("View", state.CurrentCamera.View)
    effect.SafeSetParam("Projection", state.CurrentCamera.Projection)

    match state.Config.LightingBinder with
    | ValueSome binder ->
      binder effect state.CurrentCamera state.CurrentLighting
    | ValueNone ->
      effect.SafeSetParam(
        "AmbientColor",
        state.CurrentLighting.AmbientColor.ToVector3()
        * state.CurrentLighting.AmbientIntensity
      )

      match state.LightDataTexture with
      | ValueSome tex ->
        effect.SafeSetParam("LightDataTexture", tex :> Texture)

        effect.SafeSetParam(
          "LightCount",
          float32 state.CurrentLighting.Lights.Length
        )
      | ValueNone -> effect.SafeSetParam("LightCount", 0.0f)

    match state.ShadowMatrixTexture with
    | ValueSome tex ->
      effect.SafeSetParam("ShadowMatrixTexture", tex)

      effect.SafeSetParam(
        "ShadowMatrixCount",
        float32(state.ShadowViewMatrices.Count * 2)
      )
    | ValueNone -> effect.SafeSetParam("ShadowMatrixCount", 0.0f)

    match state.ShadowAtlas with
    | ValueSome atlas ->
      effect.SafeSetParam("ShadowAtlas", atlas.RenderTarget :> Texture)
      effect.SafeSetParam("ShadowAtlasTilesX", float32 atlas.TilesAcross)
      effect.SafeSetParam("ShadowAtlasSize", float32 atlas.Size)
    | ValueNone -> ()

    state.Config.Shadows
    |> ValueOption.iter(fun cfg ->
      effect.SafeSetParam("ShadowBias", cfg.Bias)
      effect.SafeSetParam("ShadowNormalBias", cfg.NormalBias))

    let albedoColor, hasTexture, albedoTex =
      match drawable.Material.PBR.AlbedoMap with
      | ValueSome tex -> drawable.Material.PBR.AlbedoColor, true, tex
      | ValueNone ->
        match mesh.Effect with
        | :? BasicEffect as be ->
          let c = Color(be.DiffuseColor.X, be.DiffuseColor.Y, be.DiffuseColor.Z) in

          match be.Texture with
          | null -> c, false, Unchecked.defaultof<_>
          | tex -> c, true, tex
        | _ -> drawable.Material.PBR.AlbedoColor, false, Unchecked.defaultof<_>

    effect.SafeSetParam("AlbedoColor", albedoColor)
    effect.SafeSetParam("Metallic", drawable.Material.PBR.Metallic)
    effect.SafeSetParam("Roughness", drawable.Material.PBR.Roughness)

    if hasTexture then
      effect.SafeSetParam("HasAlbedoMap", 1.0f)
      effect.SafeSetParam("AlbedoMap", albedoTex)
    else
      effect.SafeSetParam("HasAlbedoMap", 0.0f)

    match drawable.Material.PBR.NormalMap with
    | ValueSome tex -> effect.SafeSetParam("NormalMap", tex)
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
      effect.SafeSetParam("World", drawable.Transform)
      effect.SafeSetParam("View", state.CurrentCamera.View)
      effect.SafeSetParam("Projection", state.CurrentCamera.Projection)

    match effect with
    | :? BasicEffect as be ->
      configureBasicEffectLighting be state.CurrentLighting

      if drawable.Material.PBR.AlbedoColor <> Color.White then
        (be.DiffuseColor <- drawable.Material.PBR.AlbedoColor.ToVector3()
         be.Alpha <- float32 drawable.Material.PBR.AlbedoColor.A / 255f)

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

  let flush(state: PipelineState) =
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
      LightPacking.packLightData state
      LightPacking.packShadowMatrices state

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
          bloomEffect.SafeSetParam("Threshold", bloomCfg.Threshold)
          bloomEffect.SafeSetParam("Intensity", bloomCfg.Intensity)
        | false, _ -> ())

      let tmMode =
        match pp.ToneMapping with
        | ToneMappingConfig.NoToneMapping -> 0
        | Reinhard -> 1
        | ACES -> 2
        | Filmic -> 3
        | AgX -> 4

      match state.CustomShaders.TryGetValue ShaderBase.PostProcess with
      | true, ppEffect ->
        state.Device.SetRenderTarget(null)
        ppEffect.SafeSetParam("SceneTexture", sceneTarget)
        ppEffect.SafeSetParam("ToneMapping", float32 tmMode)

        let vertices = [|
          VertexPositionTexture(Vector3(-1f, 1f, 0f), Vector2(0f, 0f))
          VertexPositionTexture(Vector3(1f, 1f, 0f), Vector2(1f, 0f))
          VertexPositionTexture(Vector3(-1f, -1f, 0f), Vector2(0f, 1f))
          VertexPositionTexture(Vector3(1f, -1f, 0f), Vector2(1f, 1f))
        |] in

        for pass in ppEffect.CurrentTechnique.Passes do
          pass.Apply()

          state.Device.DrawUserPrimitives(
            PrimitiveType.TriangleStrip,
            vertices,
            0,
            2
          )
      | false, _ ->
        state.Device.SetRenderTarget(null)

        match state.SpriteBatch with
        | null -> ()
        | sprite ->
          sprite.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          state.Device.SamplerStates.[0] <- SamplerState.PointClamp
          sprite.Draw(sceneTarget, state.Device.Viewport.Bounds, Color.White)
          sprite.End()
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
        | ValueNone, false -> ClearOptions.Target in

      let color = colorOpt |> ValueOption.defaultValue Color.Black in
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
      state.Config.PostProcess.IsSome || state.Config.Shadows.IsSome

    let sceneTarget =
      if needsTarget then
        let spec = {
          Width = state.Device.PresentationParameters.BackBufferWidth
          Height = state.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Color
          DepthFormat = DepthFormat.Depth24
        } in

        let rt = state.RtPool.Acquire spec in
        state.Device.SetRenderTarget(rt)
        state.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i] in processCommand state cmd

    state.Config.PreRenderCallback
    |> ValueOption.iter(fun cb ->
      cb state.Device state.CurrentCamera state.CurrentLighting)

    Drawing.flush state
    sceneTarget |> ValueOption.iter(PostProcess.render state)

    match sceneTarget with
    | ValueSome rt when state.Config.PostProcess.IsNone ->
      (state.Device.SetRenderTarget(null)

       match state.SpriteBatch with
       | null -> ()
       | sprite ->
         (sprite.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          state.Device.SamplerStates.[0] <- SamplerState.PointClamp
          sprite.Draw(rt, state.Device.Viewport.Bounds, Color.White)
          sprite.End()))
    | _ -> ()

    state.RtPool.ReleaseAll()

module RenderPipeline =
  let create (config: PipelineConfig) (game: Game) : IRenderPipeline =
    let state = State.create config

    { new IRenderPipeline with
        member _.Initialize(gd) = State.initialize state game gd
        member _.Render(_, buffer) = Orchestrate.render state buffer
    }
