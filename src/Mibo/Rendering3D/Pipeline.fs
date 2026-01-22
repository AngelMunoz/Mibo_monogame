namespace Mibo.Rendering.Graphics3D

open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Render Pipeline Implementation
// ============================================================================

/// <summary>
/// Render pipeline interface for 3D rendering.
/// Implementations process render commands and produce final frame output.
/// </summary>
type IRenderPipeline =
  /// <summary>
  /// Initialize the pipeline with a graphics device.
  /// Called once during program setup.
  /// </summary>
  /// <param name="gd">The GraphicsDevice to use for rendering.</param>
  abstract member Initialize: GraphicsDevice -> unit

  /// <summary>
  /// Render a frame using the provided render buffer.
  /// Called every frame to process accumulated render commands.
  /// </summary>
  /// <param name="ctx">The game context.</param>
  /// <param name="buffer">Buffer containing render commands for this frame.</param>
  abstract member Render:
    GameContext * RenderBuffer<unit, RenderCommand> * GameTime -> unit

module internal ShadowAtlas =

  type State = {
    RenderTarget: RenderTarget2D
    Size: int
    TileSize: int
    TilesAcross: int
    MaxShadows: int
  }

  let create (device: GraphicsDevice) (config: ShadowConfig) =
    let tilesAcross = config.AtlasTiles
    let maxShadows = tilesAcross * tilesAcross
    let atlasSize = config.Resolution * tilesAcross

    let maxTextureSize = config.MaxAtlasSize
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
  AccumulatedLights: ResizeArray<Light>
  OpaqueDrawables: ResizeArray<struct (float32 * Drawable)>
  TransparentDrawables: ResizeArray<struct (float32 * Drawable)>
  // Batchers
  mutable SpriteQuadBatch: SpriteQuadBatch.State
  mutable BillboardBatch: BillboardBatch.State
  mutable LineBatch: LineBatch.State
  // Built-in effects for batching
  mutable SpriteEffect: BasicEffect
  mutable LineEffect: BasicEffect
  // Lists for batched commands
  OpaqueSpriteCommands: ResizeArray<struct (float32 * RenderCommand)>
  TransparentSpriteCommands: ResizeArray<struct (float32 * RenderCommand)>
  // Reusable buffers
  Frustum: BoundingFrustum
  ShadowFrustum: BoundingFrustum
  FrustumCorners: Vector3[]
  mutable LightDataBuffer: Vector4[]
  mutable ShadowMatrixBuffer: Vector4[]
  mutable TileMasks: uint32[]
  PostProcessVertices: VertexPositionTexture[]
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
    ShadowViewMatrices = ResizeArray<Matrix>(16)
    ShadowProjectionMatrices = ResizeArray<Matrix>(16)
    LightDataTexture = ValueNone
    ShadowMatrixTexture = ValueNone
    MainSceneTarget = ValueNone
    CurrentCamera = Camera.identity
    CurrentLighting = Lighting.ambient
    CameraWasSet = false
    AccumulatedLights = ResizeArray<Light>(32)
    OpaqueDrawables = ResizeArray<struct (float32 * Drawable)>(256)
    TransparentDrawables = ResizeArray<struct (float32 * Drawable)>(64)
    SpriteQuadBatch = Unchecked.defaultof<_>
    BillboardBatch = Unchecked.defaultof<_>
    LineBatch = Unchecked.defaultof<_>
    SpriteEffect = Unchecked.defaultof<_>
    LineEffect = Unchecked.defaultof<_>
    OpaqueSpriteCommands = ResizeArray<struct (float32 * RenderCommand)>(128)
    TransparentSpriteCommands =
      ResizeArray<struct (float32 * RenderCommand)>(64)
    Frustum = BoundingFrustum(Matrix.Identity)
    ShadowFrustum = BoundingFrustum(Matrix.Identity)
    FrustumCorners = Array.zeroCreate 8
    LightDataBuffer = Array.empty
    ShadowMatrixBuffer = Array.empty
    TileMasks = Array.empty
    PostProcessVertices = [|
      VertexPositionTexture(Vector3(-1f, 1f, 0f), Vector2(0f, 0f))
      VertexPositionTexture(Vector3(1f, 1f, 0f), Vector2(1f, 0f))
      VertexPositionTexture(Vector3(-1f, -1f, 0f), Vector2(0f, 1f))
      VertexPositionTexture(Vector3(1f, -1f, 0f), Vector2(1f, 1f))
    |]
  }

  let reset(state: PipelineState) =
    state.CurrentCamera <- Camera.identity

    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    state.AccumulatedLights.Clear()

    state.CurrentLighting.Lights |> Array.iter state.AccumulatedLights.Add

    state.CameraWasSet <- false
    state.OpaqueDrawables.Clear()
    state.TransparentDrawables.Clear()
    state.OpaqueSpriteCommands.Clear()
    state.TransparentSpriteCommands.Clear()
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

    state.SpriteQuadBatch <- SpriteQuadBatch.create gd
    state.BillboardBatch <- BillboardBatch.create gd
    state.LineBatch <- LineBatch.create gd

    state.SpriteEffect <- new BasicEffect(gd)
    state.SpriteEffect.LightingEnabled <- false
    state.SpriteEffect.TextureEnabled <- true
    state.SpriteEffect.VertexColorEnabled <- true

    state.LineEffect <- new BasicEffect(gd)
    state.LineEffect.LightingEnabled <- false
    state.LineEffect.TextureEnabled <- false
    state.LineEffect.VertexColorEnabled <- true

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
  let private paramCache =
    ConditionalWeakTable<Effect, Dictionary<string, EffectParameter>>()

  let findParam (name: string) (effect: Effect) =
    let dict =
      match paramCache.TryGetValue(effect) with
      | true, d -> d
      | false, _ ->
        let d = Dictionary<string, EffectParameter>()

        for i = 0 to effect.Parameters.Count - 1 do
          let p = effect.Parameters.[i]
          d.[p.Name] <- p

        paramCache.Add(effect, d)
        d

    match dict.TryGetValue(name) with
    | true, p -> ValueSome p
    | false, _ -> ValueNone

  type Effect with
    member inline this.SafeSetParam(name: string, value: bool) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: int) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Matrix) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Matrix[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Quaternion) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: float32) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: float32[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Texture) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Texture[]) =
      findParam name this
      |> ValueOption.iter(fun p ->
        for i = 0 to min (value.Length - 1) (p.Elements.Count - 1) do
          p.Elements.[i].SetValue(value.[i]))

    member inline this.SafeSetParam(name: string, value: Vector2) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector2[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector3) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector3[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector4) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector4[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Color) =
      findParam name this
      |> ValueOption.iter(fun p -> p.SetValue(value.ToVector4()))

module internal LightPacking =
  let packLightData(state: PipelineState) =
    let lights = state.AccumulatedLights

    if lights.Count = 0 then
      state.LightDataTexture <- ValueNone
    else
      let requiredSize = lights.Count * 4

      if state.LightDataBuffer.Length < requiredSize then
        state.LightDataBuffer <- Array.zeroCreate requiredSize

      let data = state.LightDataBuffer
      let mutable shadowMapIndex = 0

      for i = 0 to lights.Count - 1 do
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

          data.[offset + 3] <- Vector4(dl.Color.ToVector3(), dl.SourceRadius)
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
          data.[offset + 3] <- Vector4(pl.Color.ToVector3(), pl.SourceRadius)
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

          data.[offset + 3] <- Vector4(sl.Color.ToVector3(), sl.SourceRadius)

      let tex =
        match state.LightDataTexture with
        | ValueSome t when t.Height = lights.Count -> t
        | ValueSome t ->
          t.Dispose()

          new Texture2D(
            state.Device,
            4,
            lights.Count,
            false,
            SurfaceFormat.Vector4
          )
        | ValueNone ->
          new Texture2D(
            state.Device,
            4,
            lights.Count,
            false,
            SurfaceFormat.Vector4
          )

      tex.SetData(data, 0, requiredSize)
      state.LightDataTexture <- ValueSome tex

  let packShadowMatrices(state: PipelineState) =
    if state.ShadowViewMatrices.Count = 0 then
      state.ShadowMatrixTexture <- ValueNone
    else
      let count = state.ShadowViewMatrices.Count
      let requiredSize = count * 8

      if state.ShadowMatrixBuffer.Length < requiredSize then
        state.ShadowMatrixBuffer <- Array.zeroCreate requiredSize

      let data = state.ShadowMatrixBuffer

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
        match state.ShadowMatrixTexture with
        | ValueSome t when t.Height = count * 2 -> t
        | ValueSome t ->
          t.Dispose()

          new Texture2D(
            state.Device,
            4,
            count * 2,
            false,
            SurfaceFormat.Vector4
          )
        | ValueNone ->
          new Texture2D(
            state.Device,
            4,
            count * 2,
            false,
            SurfaceFormat.Vector4
          )

      tex.SetData(data, 0, requiredSize)
      state.ShadowMatrixTexture <- ValueSome tex

module internal CameraState =
  type CameraBasis = { Right: Vector3; Up: Vector3 }

  type CameraInfo = {
    Position: Vector3
    Basis: CameraBasis
  }

  let getWorldPosition(view: Matrix) : Vector3 =
    let inv = Matrix.Invert(view)
    inv.Translation

  let private calculateBasis(view: Matrix) : CameraBasis =
    let invView = Matrix.Invert(view)

    {
      Right = Vector3(invView.M11, invView.M21, invView.M31)
      Up = Vector3(invView.M12, invView.M22, invView.M32)
    }

  let createInfo(view: Matrix) : CameraInfo = {
    Position = getWorldPosition view
    Basis = calculateBasis view
  }

  let billboardBasis
    (mode: BillboardMode)
    (camInfo: CameraInfo)
    (position: Vector3)
    : struct (Vector3 * Vector3) =
    match mode with
    | Spherical -> struct (camInfo.Basis.Right, camInfo.Basis.Up)
    | Cylindrical upAxis ->
      let viewDir = camInfo.Position - position
      let mutable right = Vector3.Cross(upAxis, viewDir)

      if right.LengthSquared() < 0.000001f then
        struct (camInfo.Basis.Right, camInfo.Basis.Up)
      else
        right.Normalize()
        let mutable up = Vector3.Cross(viewDir, right)

        if up.LengthSquared() < 0.000001f then
          struct (camInfo.Basis.Right, camInfo.Basis.Up)
        else
          up.Normalize()
          struct (right, up)

module internal Culling =
  let updateFrustum(state: PipelineState) =
    state.Frustum.Matrix <-
      state.CurrentCamera.View * state.CurrentCamera.Projection

  let isVisible (state: PipelineState) (drawable: Drawable) =
    state.Frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

  let distanceToCamera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    =
    Vector3.DistanceSquared(camera.Position, drawable.BoundingSphere.Center)

  let isTransparent(drawable: Drawable) =
    drawable.Material.Flags.HasFlag(MaterialFlags.Transparent)
    || drawable.Material.PBR.AlbedoColor.A < 255uy

  let batchDrawable (state: PipelineState) (drawable: Drawable) =
    if not(isVisible state drawable) then
      ()
    else
      let distance = distanceToCamera state.CurrentCamera drawable

      if isTransparent drawable then
        state.TransparentDrawables.Add(struct (distance, drawable))
      else
        state.OpaqueDrawables.Add(struct (distance, drawable))

  let batchSpriteCommand
    (state: PipelineState)
    (pass: RenderPass)
    (distance: float32)
    (cmd: RenderCommand)
    =
    match pass with
    | Opaque -> state.OpaqueSpriteCommands.Add(struct (distance, cmd))
    | Transparent -> state.TransparentSpriteCommands.Add(struct (distance, cmd))

module internal Tiling =
  let private projectSphere
    (state: PipelineState)
    (viewport: Viewport)
    (center: Vector3)
    (radius: float32)
    =
    let camera = state.CurrentCamera
    let sphere = BoundingSphere(center, radius)

    if state.Frustum.Contains(sphere) = ContainmentType.Disjoint then
      struct (0, 0, -1, -1) // Invalid range
    else
      let viewPos = Vector3.Transform(center, camera.View)

      // Use a local fixed-size array or individual points to avoid allocation
      let p0 = viewPos + Vector3(-radius, -radius, 0f)
      let p1 = viewPos + Vector3(radius, radius, 0f)
      let p2 = viewPos + Vector3(-radius, radius, 0f)
      let p3 = viewPos + Vector3(radius, -radius, 0f)

      let mutable minX, minY = 1f, 1f
      let mutable maxX, maxY = -1f, -1f

      let updateMinMax(p: Vector3) =
        let clip = Vector4.Transform(p, camera.Projection)

        if clip.W > 0.0001f then
          let ndc = Vector2(clip.X / clip.W, clip.Y / clip.W)
          minX <- min minX ndc.X
          minY <- min minY ndc.Y
          maxX <- max maxX ndc.X
          maxY <- max maxY ndc.Y

      updateMinMax p0
      updateMinMax p1
      updateMinMax p2
      updateMinMax p3

      if maxX < minX || maxY < minY then
        struct (0, 0, -1, -1)
      else
        let toScreen x size = (x + 1f) * 0.5f * float32 size
        let left = toScreen minX viewport.Width |> int |> max 0
        let right = toScreen maxX viewport.Width |> int |> min viewport.Width
        let top = toScreen -maxY viewport.Height |> int |> max 0

        let bottom =
          toScreen -minY viewport.Height |> int |> min viewport.Height

        struct (left, top, right, bottom)

  let cullLights(state: PipelineState) =
    let viewport =
      match state.Device with
      | null -> Viewport(0, 0, 1280, 720)
      | _ -> state.Device.Viewport

    let tileSize = state.Config.TileSize
    let tilesX = (viewport.Width + tileSize - 1) / tileSize
    let tilesY = (viewport.Height + tileSize - 1) / tileSize
    let requiredTiles = tilesX * tilesY

    if state.TileMasks.Length < requiredTiles then
      state.TileMasks <- Array.zeroCreate requiredTiles
    else
      System.Array.Clear(state.TileMasks, 0, requiredTiles)

    let tileMasks = state.TileMasks
    let lights = state.AccumulatedLights

    for i = 0 to min 31 (lights.Count - 1) do
      match lights.[i] with
      | Directional _ ->
        for j = 0 to requiredTiles - 1 do
          tileMasks.[j] <- tileMasks.[j] ||| (1u <<< i)
      | Point pl ->
        let struct (l, t, r, b) =
          projectSphere state viewport pl.Position pl.Range

        if r >= l && b >= t then
          for ty = t / tileSize to b / tileSize do
            if ty >= 0 && ty < tilesY then
              for tx = l / tileSize to r / tileSize do
                if tx >= 0 && tx < tilesX then
                  tileMasks.[ty * tilesX + tx] <-
                    tileMasks.[ty * tilesX + tx] ||| (1u <<< i)
      | Spot sl ->
        let struct (l, t, r, b) =
          projectSphere state viewport sl.Position sl.Range

        if r >= l && b >= t then
          for ty = t / tileSize to b / tileSize do
            if ty >= 0 && ty < tilesY then
              for tx = l / tileSize to r / tileSize do
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

    drawable.Bones
    |> ValueOption.iter(fun bones ->
      match effect with
      | :? SkinnedEffect as se -> se.SetBoneTransforms bones
      | _ -> effect.SafeSetParam("Bones", bones))

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
      state.Frustum.GetCorners(state.FrustumCorners)
      let corners = state.FrustumCorners
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
          state.ShadowFrustum.Matrix <- view * proj

          for i in 0 .. state.OpaqueDrawables.Count - 1 do
            let struct (_, drawable) = state.OpaqueDrawables.[i]

            if
              drawable.Material.Flags.HasFlag(MaterialFlags.CastsShadow)
              && state.ShadowFrustum.Contains(drawable.BoundingSphere)
                 <> ContainmentType.Disjoint
            then
              renderDrawableShadow state drawable shadowEffect view proj

      for light in state.AccumulatedLights do
        if shadowMapIndex < atlas.MaxShadows then
          match light with
          | Directional dl when ValueOption.isSome dl.Shadow ->
            // ... (omitted directional light code as it's unchanged)
            // I'll be careful to include enough context in old_string.
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
              let proj =
                Matrix.CreatePerspectiveFieldOfView(
                  MathHelper.PiOver2,
                  1.0f,
                  0.1f,
                  pl.Range
                )

              for face = 0 to 5 do
                let struct (view, _) = computePointShadowMatrices pl face
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
    (ambientColor: Color)
    (ambientIntensity: float32)
    (lights: seq<Light>)
    =
    effect.LightingEnabled <- true

    effect.AmbientLightColor <- ambientColor.ToVector3() * ambientIntensity

    effect.DirectionalLight0.Enabled <- false
    effect.DirectionalLight1.Enabled <- false
    effect.DirectionalLight2.Enabled <- false
    let mutable lightIndex = 0

    for light in lights do
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

    drawable.Bones
    |> ValueOption.iter(fun bones ->
      match effect with
      | :? SkinnedEffect as se -> se.SetBoneTransforms bones
      | _ -> effect.SafeSetParam("Bones", bones))

    if not(drawable.Material.Flags.HasFlag(MaterialFlags.Unlit)) then
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
            float32 state.AccumulatedLights.Count
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
          // Use material color, but fallback to mesh texture if available
          let c = drawable.Material.PBR.AlbedoColor

          match be.Texture with
          | null -> c, false, Unchecked.defaultof<_>
          | tex -> c, true, tex
        | _ -> drawable.Material.PBR.AlbedoColor, false, Unchecked.defaultof<_>

    effect.SafeSetParam("AlbedoColor", albedoColor)
    effect.SafeSetParam("Metallic", drawable.Material.PBR.Metallic)
    effect.SafeSetParam("Roughness", drawable.Material.PBR.Roughness)
    // Pass as Vector3 to avoid mismatch if shader expects float3, or let shader handle float4
    effect.SafeSetParam(
      "EmissiveColor",
      drawable.Material.PBR.EmissiveColor.ToVector3()
    )

    effect.SafeSetParam(
      "EmissiveIntensity",
      drawable.Material.PBR.EmissiveIntensity
    )

    if hasTexture then
      effect.SafeSetParam("HasAlbedoMap", 1.0f)
      effect.SafeSetParam("AlbedoMap", albedoTex)
    else
      effect.SafeSetParam("HasAlbedoMap", 0.0f)

    // Bind Emissive Intensity to 'Intensity' for Unlit/HDR effects
    effect.SafeSetParam("Intensity", drawable.Material.PBR.EmissiveIntensity)

    match drawable.Material.PBR.NormalMap with
    | ValueSome tex -> effect.SafeSetParam("NormalMap", tex)
    | ValueNone -> ()

    match drawable.Material.PBR.MetallicRoughnessMap with
    | ValueSome tex -> effect.SafeSetParam("MetallicRoughnessMap", tex)
    | ValueNone -> ()

    match drawable.Material.PBR.AmbientOcclusionMap with
    | ValueSome tex -> effect.SafeSetParam("AmbientOcclusionMap", tex)
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

    drawable.Bones
    |> ValueOption.iter(fun bones ->
      match effect with
      | :? SkinnedEffect as se -> se.SetBoneTransforms bones
      | _ -> effect.SafeSetParam("Bones", bones))

    match effect with
    | :? BasicEffect as be ->
      if not(drawable.Material.Flags.HasFlag(MaterialFlags.Unlit)) then
        configureBasicEffectLighting
          be
          state.CurrentLighting.AmbientColor
          state.CurrentLighting.AmbientIntensity
          state.AccumulatedLights
      else
        be.LightingEnabled <- false
        be.AmbientLightColor <- Vector3.One
        be.DirectionalLight0.Enabled <- false
        be.DirectionalLight1.Enabled <- false
        be.DirectionalLight2.Enabled <- false

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

  let drawSpritesInList
    (state: PipelineState)
    (pass: RenderPass)
    (items: ResizeArray<struct (float32 * RenderCommand)>)
    =
    let camInfo = CameraState.createInfo state.CurrentCamera.View
    let mutable currentSpriteQuadTexture: Texture2D = null
    let mutable currentSpriteBillboardTexture: Texture2D = null

    // Reset batches
    SpriteQuadBatch.begin' state.SpriteQuadBatch
    BillboardBatch.begin' state.BillboardBatch

    let applySpriteStates(pass: RenderPass) =
      match pass with
      | Opaque ->
        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default
      | Transparent ->
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

      state.Device.RasterizerState <- RasterizerState.CullNone
      state.Device.SamplerStates.[0] <- SamplerState.LinearClamp

    let flushPendingQuads() =
      if
        state.SpriteQuadBatch.QuadCount > 0
        && not(isNull currentSpriteQuadTexture)
      then
        applySpriteStates pass
        state.SpriteEffect.View <- state.CurrentCamera.View
        state.SpriteEffect.Projection <- state.CurrentCamera.Projection
        SpriteQuadBatch.end' state.SpriteEffect state.SpriteQuadBatch
        SpriteQuadBatch.begin' state.SpriteQuadBatch

    let flushPendingBillboards() =
      if state.BillboardBatch.SpriteCount > 0 then
        applySpriteStates pass
        state.SpriteEffect.View <- state.CurrentCamera.View
        state.SpriteEffect.Projection <- state.CurrentCamera.Projection
        BillboardBatch.end' state.SpriteEffect state.BillboardBatch
        BillboardBatch.begin' state.BillboardBatch

    for i = 0 to items.Count - 1 do
      let struct (_, cmd) = items[i]

      match cmd with
      | DrawSpriteQuad s ->
        flushPendingBillboards()

        if s.Texture <> currentSpriteQuadTexture then
          flushPendingQuads()
          currentSpriteQuadTexture <- s.Texture
          state.SpriteEffect.Texture <- s.Texture

        let q = s.Quad

        SpriteQuadBatch.draw
          q.Center
          q.Right
          q.Up
          q.Color
          q.Uv
          state.SpriteQuadBatch

      | DrawSpriteBillboard s ->
        flushPendingQuads()

        if s.Texture <> currentSpriteBillboardTexture then
          flushPendingBillboards()
          currentSpriteBillboardTexture <- s.Texture
          state.SpriteEffect.Texture <- s.Texture

        let b = s.Billboard

        let struct (right, up) =
          CameraState.billboardBasis b.Mode camInfo b.Position

        BillboardBatch.drawUv
          b.Position
          b.Size
          b.Rotation
          b.Color
          b.Uv
          right
          up
          state.BillboardBatch

      | DrawQuadEffect e ->
        flushPendingBillboards()
        flushPendingQuads()

        let effect = e.Effect

        e.Setup
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = state.CurrentCamera.View
            Projection = state.CurrentCamera.Projection
          })

        applySpriteStates pass
        SpriteQuadBatch.begin' state.SpriteQuadBatch
        let q = e.Quad

        SpriteQuadBatch.draw
          q.Center
          q.Right
          q.Up
          q.Color
          q.Uv
          state.SpriteQuadBatch

        SpriteQuadBatch.end' effect state.SpriteQuadBatch

      | DrawBillboardEffect e ->
        flushPendingQuads()
        flushPendingBillboards()

        let effect = e.Effect

        e.Setup
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = state.CurrentCamera.View
            Projection = state.CurrentCamera.Projection
          })

        applySpriteStates pass
        BillboardBatch.begin' state.BillboardBatch
        let b = e.Billboard

        let struct (right, up) =
          CameraState.billboardBasis b.Mode camInfo b.Position

        BillboardBatch.drawUv
          b.Position
          b.Size
          b.Rotation
          b.Color
          b.Uv
          right
          up
          state.BillboardBatch

        BillboardBatch.end' effect state.BillboardBatch

      | DrawLine(p1, p2, color, _) ->
        flushPendingQuads()
        flushPendingBillboards()

        applySpriteStates pass
        state.LineEffect.View <- state.CurrentCamera.View
        state.LineEffect.Projection <- state.CurrentCamera.Projection

        LineBatch.begin' state.LineBatch
        LineBatch.addLine p1 p2 color state.LineBatch
        LineBatch.end' state.LineEffect state.LineBatch

      | DrawLines(verts, lineCount, _) ->
        flushPendingQuads()
        flushPendingBillboards()

        applySpriteStates pass
        state.LineEffect.View <- state.CurrentCamera.View
        state.LineEffect.Projection <- state.CurrentCamera.Projection

        LineBatch.begin' state.LineBatch
        LineBatch.addLines verts lineCount state.LineBatch
        LineBatch.end' state.LineEffect state.LineBatch

      | DrawLinesEffect(verts, lineCount, effect, setupOpt, _) ->
        flushPendingQuads()
        flushPendingBillboards()

        setupOpt
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = state.CurrentCamera.View
            Projection = state.CurrentCamera.Projection
          })

        applySpriteStates pass
        LineBatch.begin' state.LineBatch
        LineBatch.addLines verts lineCount state.LineBatch
        LineBatch.end' effect state.LineBatch

      | _ -> ()

    flushPendingQuads()
    flushPendingBillboards()

  let flush(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0
      && state.TransparentDrawables.Count = 0
      && state.OpaqueSpriteCommands.Count = 0
      && state.TransparentSpriteCommands.Count = 0
    then
      ()
    else
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      state.CurrentLighting <- {
        state.CurrentLighting with
            Lights = state.AccumulatedLights.ToArray()
      }

      state.Config.PreRenderCallback
      |> ValueOption.iter(fun cb ->
        cb state.Device state.CurrentCamera state.CurrentLighting)

      ShadowPass.render state
      LightPacking.packLightData state
      LightPacking.packShadowMatrices state

      let _tileMasks = Tiling.cullLights state
      state.Device.DepthStencilState <- DepthStencilState.Default

      let draw(d: Drawable) =
        if d.Material.Flags.HasFlag(MaterialFlags.Unlit) then
          match state.CustomShaders.TryGetValue(ShaderBase.Unlit) with
          | true, effect -> drawWithEffect state d effect
          | false, _ -> drawFallback state d
        else
          match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
          | true, effect -> drawWithEffect state d effect
          | false, _ -> drawFallback state d

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        draw drawable

      drawSpritesInList state Opaque state.OpaqueSpriteCommands

      if
        state.TransparentDrawables.Count > 0
        || state.TransparentSpriteCommands.Count > 0
      then
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          draw drawable

        drawSpritesInList state Transparent state.TransparentSpriteCommands

        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default

      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()
      state.OpaqueSpriteCommands.Clear()
      state.TransparentSpriteCommands.Clear()

module internal PostProcess =
  let render
    (state: PipelineState)
    (gameTime: GameTime)
    (sceneTarget: RenderTarget2D)
    =
    match state.Config.PostProcess with
    | ValueSome pp ->
      let bloomTarget =
        match pp.Bloom with
        | ValueSome bloomCfg ->
          match state.CustomShaders.TryGetValue(ShaderBase.Bloom) with
          | true, bloomEffect ->
            bloomEffect.SafeSetParam("Threshold", bloomCfg.Threshold)
            bloomEffect.SafeSetParam("Intensity", bloomCfg.Intensity)
            bloomEffect.SafeSetParam("SceneTexture", sceneTarget)

            let texelSize =
              Vector2(
                1.0f / float32 sceneTarget.Width,
                1.0f / float32 sceneTarget.Height
              )

            bloomEffect.SafeSetParam("TexelSize", texelSize)

            let spec = {
              Width = sceneTarget.Width / 2
              Height = sceneTarget.Height / 2
              Format = SurfaceFormat.Color
              DepthFormat = DepthFormat.None
            }

            let rt = state.RtPool.Acquire spec
            state.Device.SetRenderTarget(rt)

            for pass in bloomEffect.CurrentTechnique.Passes do
              pass.Apply()

              state.Device.DrawUserPrimitives(
                PrimitiveType.TriangleStrip,
                state.PostProcessVertices,
                0,
                2
              )

            ValueSome rt
          | false, _ -> ValueNone
        | ValueNone -> ValueNone

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

        ppEffect.SafeSetParam(
          "Time",
          float32 gameTime.TotalGameTime.TotalSeconds
        )

        bloomTarget
        |> ValueOption.iter(fun rt ->
          ppEffect.SafeSetParam("BloomTexture", rt :> Texture))

        for pass in ppEffect.CurrentTechnique.Passes do
          pass.Apply()

          state.Device.DrawUserPrimitives(
            PrimitiveType.TriangleStrip,
            state.PostProcessVertices,
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
      Culling.updateFrustum state
      Drawing.configureBasicEffectCamera state.BasicEffect camera

      Drawing.configureBasicEffectLighting
        state.BasicEffect
        state.CurrentLighting.AmbientColor
        state.CurrentLighting.AmbientIntensity
        state.AccumulatedLights
    | SetLighting lighting ->
      state.CurrentLighting <- lighting
      state.AccumulatedLights.Clear()

      lighting.Lights |> Array.iter state.AccumulatedLights.Add

      Drawing.configureBasicEffectLighting
        state.BasicEffect
        state.CurrentLighting.AmbientColor
        state.CurrentLighting.AmbientIntensity
        state.AccumulatedLights
    | AddLight light ->
      state.AccumulatedLights.Add(light)

      // BasicEffect only supports 3 directional lights, so we only reconfigure if it's directional
      match light with
      | Directional _ ->
        // This is a bit inefficient as it reconfigures the whole state, but BasicEffect is fallback
        Drawing.configureBasicEffectLighting
          state.BasicEffect
          state.CurrentLighting.AmbientColor
          state.CurrentLighting.AmbientIntensity
          state.AccumulatedLights
      | _ -> ()
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
    | DrawSpriteQuad s ->
      let distSq =
        Vector3.DistanceSquared(state.CurrentCamera.Position, s.Quad.Center)

      Culling.batchSpriteCommand state s.Pass distSq cmd
    | DrawSpriteBillboard s ->
      let distSq =
        Vector3.DistanceSquared(
          state.CurrentCamera.Position,
          s.Billboard.Position
        )

      Culling.batchSpriteCommand state s.Pass distSq cmd
    | DrawQuadEffect e ->
      let distSq =
        Vector3.DistanceSquared(state.CurrentCamera.Position, e.Quad.Center)

      Culling.batchSpriteCommand state e.Pass distSq cmd
    | DrawBillboardEffect e ->
      let distSq =
        Vector3.DistanceSquared(
          state.CurrentCamera.Position,
          e.Billboard.Position
        )

      Culling.batchSpriteCommand state e.Pass distSq cmd
    | DrawLine(_, _, _, pass) -> Culling.batchSpriteCommand state pass 0f cmd
    | DrawLines(_, _, pass) -> Culling.batchSpriteCommand state pass 0f cmd
    | DrawLinesEffect(_, _, _, _, pass) ->
      Culling.batchSpriteCommand state pass 0f cmd
    | DrawCustom drawFn ->
      Drawing.flush state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    (gameTime: GameTime)
    =
    State.reset state
    Culling.updateFrustum state

    let needsTarget =
      state.Config.PostProcess.IsSome || state.Config.Shadows.IsSome

    let sceneTarget =
      if needsTarget then
        let spec = {
          Width = state.Device.PresentationParameters.BackBufferWidth
          Height = state.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Vector4
          DepthFormat = DepthFormat.Depth24
        }

        let rt = state.RtPool.Acquire spec
        state.Device.SetRenderTarget(rt)
        state.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i] in processCommand state cmd

    Drawing.flush state
    sceneTarget |> ValueOption.iter(PostProcess.render state gameTime)

    match sceneTarget with
    | ValueSome rt when state.Config.PostProcess.IsNone ->
      state.Device.SetRenderTarget(null)

      match state.SpriteBatch with
      | null -> ()
      | sprite ->
        sprite.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
        state.Device.SamplerStates.[0] <- SamplerState.PointClamp
        sprite.Draw(rt, state.Device.Viewport.Bounds, Color.White)
        sprite.End()
    | _ -> ()

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()

module RenderPipeline =
  /// <summary>
  /// Create a 3D render pipeline with the specified configuration.
  /// The pipeline will be initialized with the game's graphics device when the renderer is created.
  /// </summary>
  /// <param name="config">Configuration for shadows, post-processing, lighting, and advanced features.</param>
  /// <param name="game">The Game instance (used to load custom shaders).</param>
  /// <returns>A configured IRenderPipeline instance.</returns>
  let create (config: PipelineConfig) (game: Game) : IRenderPipeline =
    let state = State.create config

    { new IRenderPipeline with
        member _.Initialize(gd) = State.initialize state game gd

        member _.Render(_, buffer, gameTime) =
          Orchestrate.render state buffer gameTime
    }
