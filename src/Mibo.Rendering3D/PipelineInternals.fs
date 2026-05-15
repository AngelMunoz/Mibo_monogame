namespace Mibo.Rendering3D.PipelineInternals

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D
open Mibo.Rendering3D

module SpriteQuadBatch = Mibo.Elmish.Graphics3D.SpriteQuadBatch

module ShadowAtlas =

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

    Viewport(
      col * state.TileSize,
      row * state.TileSize,
      state.TileSize,
      state.TileSize
    )

[<Sealed>]
type DeviceContext() =
  member val Device: GraphicsDevice = Unchecked.defaultof<_> with get, set
  member val RtPool: IRenderTargetPool = Unchecked.defaultof<_> with get, set
  member val SpriteBatch: SpriteBatch = Unchecked.defaultof<_> with get, set

  member val SpriteQuadBatch: SpriteQuadBatch.State =
    Unchecked.defaultof<_> with get, set

  member val BillboardBatch: BillboardBatch.State =
    Unchecked.defaultof<_> with get, set

  member val LineBatch: LineBatch.State = Unchecked.defaultof<_> with get, set
  member val SpriteEffect: BasicEffect = Unchecked.defaultof<_> with get, set
  member val LineEffect: BasicEffect = Unchecked.defaultof<_> with get, set
  member val CustomShaders = Dictionary<string, Effect>()


  member this.Initialize(gd: GraphicsDevice) =
    this.Device <- gd
    this.RtPool <- RenderTargetPool.create gd
    this.SpriteBatch <- new SpriteBatch(gd)
    this.SpriteQuadBatch <- SpriteQuadBatch.create gd
    this.BillboardBatch <- BillboardBatch.create gd
    this.LineBatch <- LineBatch.create gd
    let spriteEffect = new BasicEffect(gd)
    spriteEffect.LightingEnabled <- false
    spriteEffect.TextureEnabled <- true
    spriteEffect.VertexColorEnabled <- true
    this.SpriteEffect <- spriteEffect
    let lineEffect = new BasicEffect(gd)
    lineEffect.LightingEnabled <- false
    lineEffect.TextureEnabled <- false
    lineEffect.VertexColorEnabled <- true
    this.LineEffect <- lineEffect

[<Sealed>]
type FrameContext() =
  member val Camera: Camera = Camera.identity with get, set
  member val Lighting: LightingState = Lighting.ambient with get, set
  member val CameraWasSet: bool = false with get, set
  member val LightingPrepared: bool = false with get, set
  member val AccumulatedLights = ResizeArray<Light>(32)
  member val OpaqueDrawables = ResizeArray<struct (float32 * Drawable)>(256)
  member val TransparentDrawables = ResizeArray<struct (float32 * Drawable)>(64)

  member val OpaqueSpriteCommands =
    ResizeArray<struct (float32 * RenderCommand)>(128)

  member val TransparentSpriteCommands =
    ResizeArray<struct (float32 * RenderCommand)>(64)

  member val Frustum = BoundingFrustum(Matrix.Identity)
  member val ShadowFrustum = BoundingFrustum(Matrix.Identity)
  member val FrustumCorners: Vector3[] = Array.zeroCreate 8
  member val LightDataBuffer: Vector4[] = Array.empty with get, set
  member val ShadowMatrixBuffer: Vector4[] = Array.empty with get, set
  member val TileMasks: uint32[] = Array.empty with get, set
  member val TileDataBuffer: float32[] = Array.empty with get, set
  member val LightDataTexture: Texture2D voption = ValueNone with get, set
  member val ShadowMatrixTexture: Texture2D voption = ValueNone with get, set
  member val TileDataTexture: Texture2D voption = ValueNone with get, set
  member val MainSceneTarget: RenderTarget2D voption = ValueNone with get, set
  member val ShadowViewMatrices = ResizeArray<Matrix>(16)
  member val ShadowProjectionMatrices = ResizeArray<Matrix>(16)
  member val GameCtx: GameContext voption = ValueNone with get, set

[<Sealed>]
type BindingCache() =
  member val RenderContext: RenderContext voption = ValueNone with get, set
  member val LastGlobalEffect: Effect voption = ValueNone with get, set
  member val LastMaterialKey: int<MaterialKey> voption = ValueNone with get, set

[<Sealed>]
type PipelineState
  (
    config: Pipeline3DConfig,
    devices: DeviceContext,
    frame: FrameContext,
    cache: BindingCache,
    shadowAtlas: ShadowAtlas.State voption
  ) =

  member val Config = config
  member val Devices = devices
  member val Frame = frame
  member val Cache = cache
  member val ShadowAtlas: ShadowAtlas.State voption = shadowAtlas with get, set

  member this.Reset() =
    this.Frame.Camera <- Camera.identity

    this.Frame.Lighting <-
      this.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    this.Frame.AccumulatedLights.Clear()
    this.Frame.Lighting.Lights |> Array.iter this.Frame.AccumulatedLights.Add
    this.Frame.CameraWasSet <- false
    this.Frame.LightingPrepared <- false
    this.Frame.OpaqueDrawables.Clear()
    this.Frame.TransparentDrawables.Clear()
    this.Frame.OpaqueSpriteCommands.Clear()
    this.Frame.TransparentSpriteCommands.Clear()
    this.Frame.ShadowViewMatrices.Clear()
    this.Frame.ShadowProjectionMatrices.Clear()
    this.Frame.MainSceneTarget <- ValueNone
    this.Cache.RenderContext <- ValueNone
    this.Cache.LastGlobalEffect <- ValueNone
    this.Cache.LastMaterialKey <- ValueNone

  member this.Initialize(game: Game) =
    let gd = game.GraphicsDevice

    this.Frame.Lighting <-
      this.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    this.Config.Shadows
    |> ValueOption.iter(fun cfg ->
      this.ShadowAtlas <- ValueSome(ShadowAtlas.create gd cfg))

    this.Config.ShadowCasterAsset
    |> ValueOption.iter(fun asset ->
      let effect = game.Content.Load<Effect>(asset)
      this.Devices.CustomShaders.["ShadowCaster"] <- effect)

    this.Config.BloomEffectAsset
    |> ValueOption.iter(fun asset ->
      let effect = game.Content.Load<Effect>(asset)
      this.Devices.CustomShaders.["Bloom"] <- effect)

    this.Config.PostProcessEffectAsset
    |> ValueOption.iter(fun asset ->
      let effect = game.Content.Load<Effect>(asset)
      this.Devices.CustomShaders.["PostProcess"] <- effect)

  member this.BuildRenderContext() : RenderContext =
    let shadowBias, shadowNormalBias =
      this.Config.Shadows
      |> ValueOption.map(fun cfg -> cfg.Bias, cfg.NormalBias)
      |> ValueOption.defaultValue(0.005f, 0.01f)

    let shadowAtlasTilesX, shadowAtlasSize =
      this.ShadowAtlas
      |> ValueOption.map(fun atlas -> atlas.TilesAcross, float32 atlas.Size)
      |> ValueOption.defaultValue(0, 0f)

    let tileSize = this.Config.TileSize
    let viewport = this.Devices.Device.Viewport
    let tilesX = (viewport.Width + tileSize - 1) / tileSize
    let tilesY = (viewport.Height + tileSize - 1) / tileSize

    {
      Camera = this.Frame.Camera
      LightingState = this.Frame.Lighting
      LightDataTexture = this.Frame.LightDataTexture
      LightCount = this.Frame.AccumulatedLights.Count
      TileDataTexture = this.Frame.TileDataTexture
      TileSize = tileSize
      TilesX = tilesX
      TilesY = tilesY
      MaxLightsPerTile = 32
      ShadowAtlas =
        this.ShadowAtlas
        |> ValueOption.map(fun a -> a.RenderTarget :> Texture2D)
      ShadowMatrixTexture = this.Frame.ShadowMatrixTexture
      ShadowMatrixCount = this.Frame.ShadowViewMatrices.Count
      ShadowAtlasTilesX = shadowAtlasTilesX
      ShadowAtlasSize = shadowAtlasSize
      ShadowBias = shadowBias
      ShadowNormalBias = shadowNormalBias
      AmbientColor = this.Frame.Lighting.AmbientColor.ToVector3()
      AmbientIntensity = this.Frame.Lighting.AmbientIntensity
    }

  member this.UpdateFrustum() =
    this.Frame.Frustum.Matrix <-
      this.Frame.Camera.View * this.Frame.Camera.Projection

module LightPacking =

  let packLightData(state: PipelineState) =
    let lights = state.Frame.AccumulatedLights

    if lights.Count = 0 then
      state.Frame.LightDataTexture <- ValueNone
    else
      let requiredSize = lights.Count * 4

      if state.Frame.LightDataBuffer.Length < requiredSize then
        state.Frame.LightDataBuffer <- Array.zeroCreate requiredSize

      let data = state.Frame.LightDataBuffer
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
              float32(Math.Cos(float sl.OuterConeAngle))
            )

          data.[offset + 2] <-
            Vector4(
              sl.Direction.X,
              sl.Direction.Y,
              sl.Direction.Z,
              float32(Math.Cos(float sl.InnerConeAngle))
            )

          data.[offset + 3] <- Vector4(sl.Color.ToVector3(), sl.SourceRadius)

      let tex =
        match state.Frame.LightDataTexture with
        | ValueSome t when t.Height = lights.Count -> t
        | ValueSome t ->
          t.Dispose()

          new Texture2D(
            state.Devices.Device,
            4,
            lights.Count,
            false,
            SurfaceFormat.Vector4
          )
        | ValueNone ->
          new Texture2D(
            state.Devices.Device,
            4,
            lights.Count,
            false,
            SurfaceFormat.Vector4
          )

      tex.SetData(data, 0, requiredSize)
      state.Frame.LightDataTexture <- ValueSome tex

  let packShadowMatrices(state: PipelineState) =
    if state.Frame.ShadowViewMatrices.Count = 0 then
      state.Frame.ShadowMatrixTexture <- ValueNone
    else
      let count = state.Frame.ShadowViewMatrices.Count
      let requiredSize = count * 8

      if state.Frame.ShadowMatrixBuffer.Length < requiredSize then
        state.Frame.ShadowMatrixBuffer <- Array.zeroCreate requiredSize

      let data = state.Frame.ShadowMatrixBuffer

      for i = 0 to count - 1 do
        let v = state.Frame.ShadowViewMatrices.[i]
        let p = state.Frame.ShadowProjectionMatrices.[i]
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
        match state.Frame.ShadowMatrixTexture with
        | ValueSome t when t.Height = count * 2 -> t
        | ValueSome t ->
          t.Dispose()

          new Texture2D(
            state.Devices.Device,
            4,
            count * 2,
            false,
            SurfaceFormat.Vector4
          )
        | ValueNone ->
          new Texture2D(
            state.Devices.Device,
            4,
            count * 2,
            false,
            SurfaceFormat.Vector4
          )

      tex.SetData(data, 0, requiredSize)
      state.Frame.ShadowMatrixTexture <- ValueSome tex

module Culling =

  let isVisible (frame: FrameContext) (drawable: Drawable) =
    frame.Frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

  let distanceToCamera (camera: Camera) (drawable: Drawable) =
    Vector3.DistanceSquared(camera.Position, drawable.BoundingSphere.Center)

  let batchDrawable (frame: FrameContext) (drawable: Drawable) =
    if not(isVisible frame drawable) then
      ()
    else
      let distance = distanceToCamera frame.Camera drawable

      match drawable.Pass with
      | Opaque -> frame.OpaqueDrawables.Add(struct (distance, drawable))
      | Transparent ->
        frame.TransparentDrawables.Add(struct (distance, drawable))

  let batchSpriteCommand
    (frame: FrameContext)
    (pass: RenderPass)
    (distance: float32)
    (cmd: RenderCommand)
    =
    match pass with
    | Opaque -> frame.OpaqueSpriteCommands.Add(struct (distance, cmd))
    | Transparent -> frame.TransparentSpriteCommands.Add(struct (distance, cmd))

module CameraState =
  type CameraBasis = { Right: Vector3; Up: Vector3 }

  type CameraInfo = {
    Position: Vector3
    Basis: CameraBasis
  }

  let createInfo(view: Matrix) : CameraInfo =
    let inv = Matrix.Invert(view)

    {
      Position = inv.Translation
      Basis = {
        Right = Vector3(inv.M11, inv.M21, inv.M31)
        Up = Vector3(inv.M12, inv.M22, inv.M32)
      }
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

module Tiling =

  let private projectSphere
    (frame: FrameContext)
    (viewport: Viewport)
    (center: Vector3)
    (radius: float32)
    =
    let sphere = BoundingSphere(center, radius)

    if frame.Frustum.Contains(sphere) = ContainmentType.Disjoint then
      struct (0, 0, -1, -1)
    else
      let viewPos = Vector3.Transform(center, frame.Camera.View)
      let p0 = viewPos + Vector3(-radius, -radius, 0f)
      let p1 = viewPos + Vector3(radius, radius, 0f)
      let p2 = viewPos + Vector3(-radius, radius, 0f)
      let p3 = viewPos + Vector3(radius, -radius, 0f)
      let mutable minX, minY = 1f, 1f
      let mutable maxX, maxY = -1f, -1f

      let updateMinMax(p: Vector3) =
        let clip = Vector4.Transform(p, frame.Camera.Projection)

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

        struct (toScreen minX viewport.Width |> int |> max 0,
                toScreen -maxY viewport.Height |> int |> max 0,
                toScreen maxX viewport.Width |> int |> min viewport.Width,
                toScreen -minY viewport.Height |> int |> min viewport.Height)

  let cullLights(state: PipelineState) =
    let viewport =
      match state.Devices.Device with
      | null -> Viewport(0, 0, 1280, 720)
      | _ -> state.Devices.Device.Viewport

    let tileSize = state.Config.TileSize
    let tilesX = (viewport.Width + tileSize - 1) / tileSize
    let tilesY = (viewport.Height + tileSize - 1) / tileSize
    let requiredTiles = tilesX * tilesY
    let frame = state.Frame

    if frame.TileMasks.Length < requiredTiles then
      frame.TileMasks <- Array.zeroCreate requiredTiles
    else
      Array.Clear(frame.TileMasks, 0, requiredTiles)

    let tileMasks = frame.TileMasks
    let lights = frame.AccumulatedLights

    for i = 0 to min 31 (lights.Count - 1) do
      match lights.[i] with
      | Directional _ ->
        for j = 0 to requiredTiles - 1 do
          tileMasks.[j] <- tileMasks.[j] ||| (1u <<< i)
      | Point pl ->
        let struct (l, t, r, b) =
          projectSphere frame viewport pl.Position pl.Range

        if r >= l && b >= t then
          for ty = t / tileSize to b / tileSize do
            if ty >= 0 && ty < tilesY then
              for tx = l / tileSize to r / tileSize do
                if tx >= 0 && tx < tilesX then
                  tileMasks.[ty * tilesX + tx] <-
                    tileMasks.[ty * tilesX + tx] ||| (1u <<< i)
      | Spot sl ->
        let struct (l, t, r, b) =
          projectSphere frame viewport sl.Position sl.Range

        if r >= l && b >= t then
          for ty = t / tileSize to b / tileSize do
            if ty >= 0 && ty < tilesY then
              for tx = l / tileSize to r / tileSize do
                if tx >= 0 && tx < tilesX then
                  tileMasks.[ty * tilesX + tx] <-
                    tileMasks.[ty * tilesX + tx] ||| (1u <<< i)

    tileMasks

  let packTileData(state: PipelineState) =
    let tileMasks = cullLights state
    let frame = state.Frame
    let tileSize = state.Config.TileSize
    let viewport = state.Devices.Device.Viewport
    let tilesX = (viewport.Width + tileSize - 1) / tileSize
    let tilesY = (viewport.Height + tileSize - 1) / tileSize
    let requiredTiles = tilesX * tilesY

    let needed = requiredTiles
    if frame.TileDataBuffer.Length < needed then
      frame.TileDataBuffer <- Array.zeroCreate needed
    else
      Array.Clear(frame.TileDataBuffer, 0, needed)

    for i in 0 .. min (tileMasks.Length - 1) (needed - 1) do
      frame.TileDataBuffer.[i] <- float32 tileMasks.[i]

    let gd = state.Devices.Device

    let tex =
      match frame.TileDataTexture with
      | ValueSome t when t.Width = needed -> t
      | ValueSome t ->
        t.Dispose()
        let newTex = new Texture2D(gd, needed, 1, false, SurfaceFormat.Single)
        frame.TileDataTexture <- ValueSome newTex
        newTex
      | ValueNone ->
        let newTex = new Texture2D(gd, needed, 1, false, SurfaceFormat.Single)
        frame.TileDataTexture <- ValueSome newTex
        newTex

    tex.SetData(frame.TileDataBuffer, 0, needed)

module ShadowPass =

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
    (devices: DeviceContext)
    (frame: FrameContext)
    (drawable: Drawable)
    (shadowEffect: Effect)
    (view: Matrix)
    (projection: Matrix)
    =
    let mesh = drawable.Mesh
    devices.Device.SetVertexBuffer(mesh.VertexBuffer)
    devices.Device.Indices <- mesh.IndexBuffer
    shadowEffect.SafeSetParam("World", drawable.Transform)
    shadowEffect.SafeSetParam("View", view)
    shadowEffect.SafeSetParam("Projection", projection)

    drawable.Bones
    |> ValueOption.iter(fun bones -> shadowEffect.SafeSetParam("Bones", bones))

    for pass in shadowEffect.CurrentTechnique.Passes do
      pass.Apply()

      devices.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

  let render(state: PipelineState) =
    match state.ShadowAtlas with
    | ValueSome atlas ->
      let shadowEffect =
        match state.Devices.CustomShaders.TryGetValue("ShadowCaster") with
        | true, e -> e
        | false, _ -> null

      if isNull shadowEffect then
        ()
      else
        state.Frame.Frustum.GetCorners(state.Frame.FrustumCorners)
        let corners = state.Frame.FrustumCorners
        let mutable shadowMapIndex = 0
        let devices = state.Devices
        let frame = state.Frame

        devices.Device.SetRenderTarget(atlas.RenderTarget)

        devices.Device.Clear(
          ClearOptions.Target ||| ClearOptions.DepthBuffer,
          Color.White,
          1.0f,
          0
        )

        devices.Device.DepthStencilState <- DepthStencilState.Default
        devices.Device.RasterizerState <- RasterizerState.CullNone
        devices.Device.BlendState <- BlendState.Opaque

        let renderPass (view: Matrix) (proj: Matrix) =
          if shadowMapIndex < atlas.MaxShadows then
            let vp = ShadowAtlas.getViewport atlas shadowMapIndex
            devices.Device.Viewport <- vp
            frame.ShadowFrustum.Matrix <- view * proj

            for i in 0 .. frame.OpaqueDrawables.Count - 1 do
              let struct (_, drawable) = frame.OpaqueDrawables.[i]

              if
                frame.ShadowFrustum.Contains(drawable.BoundingSphere)
                <> ContainmentType.Disjoint
              then
                renderDrawableShadow
                  devices
                  frame
                  drawable
                  shadowEffect
                  view
                  proj

        for light in frame.AccumulatedLights do
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

              for i in 0 .. frame.OpaqueDrawables.Count - 1 do
                let struct (_, d) = frame.OpaqueDrawables.[i]
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
              frame.ShadowViewMatrices.Add(lightView)
              frame.ShadowProjectionMatrices.Add(lightProj)
              shadowMapIndex <- shadowMapIndex + 1
            | Spot sl when ValueOption.isSome sl.Shadow ->
              let struct (view, proj) = computeSpotShadowMatrices sl
              renderPass view proj
              frame.ShadowViewMatrices.Add(view)
              frame.ShadowProjectionMatrices.Add(proj)
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
                  frame.ShadowViewMatrices.Add(view)
                  frame.ShadowProjectionMatrices.Add(proj)
                  shadowMapIndex <- shadowMapIndex + 1
            | _ -> ()

        devices.Device.RasterizerState <- RasterizerState.CullCounterClockwise

        match frame.MainSceneTarget with
        | ValueSome rt -> devices.Device.SetRenderTarget(rt)
        | ValueNone -> devices.Device.SetRenderTarget(null)
    | ValueNone -> ()

module PostProcess =

  let private quadVertices: VertexPositionTexture[] = [|
    VertexPositionTexture(Vector3(-1f, 1f, 0f), Vector2(0f, 0f))
    VertexPositionTexture(Vector3(1f, 1f, 0f), Vector2(1f, 0f))
    VertexPositionTexture(Vector3(-1f, -1f, 0f), Vector2(0f, 1f))
    VertexPositionTexture(Vector3(1f, -1f, 0f), Vector2(1f, 1f))
  |]

  let render
    (state: PipelineState)
    (gameTime: GameTime)
    (sceneTarget: RenderTarget2D)
    =
    match state.Config.PostProcess with
    | ValueSome pp ->
      let devices = state.Devices

      let bloomTarget =
        match pp.Bloom with
        | ValueSome bloomCfg ->
          match devices.CustomShaders.TryGetValue("Bloom") with
          | true, bloomEffect ->
            bloomEffect.SafeSetParam("Threshold", bloomCfg.Threshold)
            bloomEffect.SafeSetParam("Intensity", bloomCfg.Intensity)
            bloomEffect.SafeSetParam("SceneTexture", sceneTarget)

            let texelSize =
              Vector2(
                1f / float32 sceneTarget.Width,
                1f / float32 sceneTarget.Height
              )

            bloomEffect.SafeSetParam("TexelSize", texelSize)

            let spec = {
              Width = sceneTarget.Width / 2
              Height = sceneTarget.Height / 2
              Format = SurfaceFormat.Color
              DepthFormat = DepthFormat.None
            }

            let rt = devices.RtPool.Acquire spec
            devices.Device.SetRenderTarget(rt)

            for pass in bloomEffect.CurrentTechnique.Passes do
              pass.Apply()

              devices.Device.DrawUserPrimitives(
                PrimitiveType.TriangleStrip,
                quadVertices,
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

      match devices.CustomShaders.TryGetValue("PostProcess") with
      | true, ppEffect ->
        devices.Device.SetRenderTarget(null)
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

          devices.Device.DrawUserPrimitives(
            PrimitiveType.TriangleStrip,
            quadVertices,
            0,
            2
          )
      | false, _ ->
        devices.Device.SetRenderTarget(null)

        match devices.SpriteBatch with
        | null -> ()
        | sprite ->
          sprite.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
          devices.Device.SamplerStates.[0] <- SamplerState.PointClamp
          sprite.Draw(sceneTarget, devices.Device.Viewport.Bounds, Color.White)
          sprite.End()
    | ValueNone -> ()

module Drawing =

  let prepare(state: PipelineState) =
    if not state.Frame.LightingPrepared then
      ShadowPass.render state
      LightPacking.packLightData state
      LightPacking.packShadowMatrices state
      Tiling.packTileData state
      state.Cache.RenderContext <- ValueSome(state.BuildRenderContext())
      state.Frame.LightingPrepared <- true

  let apply(state: PipelineState) =
    let devices = state.Devices
    let frame = state.Frame

    if
      frame.OpaqueDrawables.Count = 0
      && frame.TransparentDrawables.Count = 0
      && frame.OpaqueSpriteCommands.Count = 0
      && frame.TransparentSpriteCommands.Count = 0
    then
      ()
    else
      prepare state

      let ctx =
        state.Cache.RenderContext
        |> ValueOption.defaultValue(state.BuildRenderContext())

      let opaqueComparer
        (struct (da, (d1: Drawable)))
        (struct (db, (d2: Drawable)))
        =
        let c =
          compare
            (RuntimeHelpers.GetHashCode d1.Binding.Effect)
            (RuntimeHelpers.GetHashCode d2.Binding.Effect)

        if c <> 0 then
          c
        else
          let c = compare d1.MaterialKey d2.MaterialKey
          if c <> 0 then c else compare da db

      frame.OpaqueDrawables.Sort opaqueComparer

      let transparentComparer
        (struct (da, (d1: Drawable)))
        (struct (db, (d2: Drawable)))
        =
        let c = compare db da

        if c <> 0 then
          c
        else
          compare
            (RuntimeHelpers.GetHashCode d1.Binding.Effect)
            (RuntimeHelpers.GetHashCode d2.Binding.Effect)

      frame.TransparentDrawables.Sort transparentComparer

      frame.Lighting <- {
        frame.Lighting with
            Lights = frame.AccumulatedLights.ToArray()
      }

      devices.Device.DepthStencilState <- DepthStencilState.Default

      let mutable lastEffect: Effect voption = ValueNone
      let mutable lastMaterialKey: int<MaterialKey> voption = ValueNone

      let drawDrawable(drawable: Drawable) =
        let binding = drawable.Binding

        match lastEffect with
        | ValueSome e when obj.ReferenceEquals(e, binding.Effect) -> ()
        | _ ->
          binding.BindGlobal ctx
          lastEffect <- ValueSome binding.Effect
          lastMaterialKey <- ValueNone

        match drawable.MaterialData with
        | ValueSome data ->
          binding.BindPerMaterial(ValueSome data)
          lastMaterialKey <- ValueNone
        | ValueNone ->
          match lastMaterialKey with
          | ValueSome mk when mk = binding.MaterialKey -> ()
          | _ ->
            binding.BindPerMaterial(ValueNone)
            lastMaterialKey <- ValueSome binding.MaterialKey

        binding.BindPerInstance drawable.Transform drawable.Bones

        for pass in binding.Effect.CurrentTechnique.Passes do
          pass.Apply()

          devices.Device.DrawIndexedPrimitives(
            PrimitiveType.TriangleList,
            0,
            0,
            drawable.Mesh.IndexCount / 3
          )

      for i in 0 .. frame.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = frame.OpaqueDrawables.[i]
        drawDrawable drawable

      let camInfo = CameraState.createInfo frame.Camera.View

      let drawSpritesInList
        (items: ResizeArray<struct (float32 * RenderCommand)>)
        (pass: RenderPass)
        =
        let mutable currentSpriteQuadTexture: Texture2D = null
        let mutable currentSpriteBillboardTexture: Texture2D = null
        SpriteQuadBatch.begin' devices.SpriteQuadBatch
        BillboardBatch.begin' devices.BillboardBatch

        let applySpriteStates(p: RenderPass) =
          match p with
          | Opaque ->
            devices.Device.BlendState <- BlendState.Opaque
            devices.Device.DepthStencilState <- DepthStencilState.Default
          | Transparent ->
            devices.Device.BlendState <- BlendState.AlphaBlend
            devices.Device.DepthStencilState <- DepthStencilState.DepthRead

          devices.Device.RasterizerState <- RasterizerState.CullNone
          devices.Device.SamplerStates.[0] <- SamplerState.LinearClamp

        let flushPendingQuads() =
          if
            devices.SpriteQuadBatch.QuadCount > 0
            && not(isNull currentSpriteQuadTexture)
          then
            applySpriteStates pass
            devices.SpriteEffect.View <- frame.Camera.View
            devices.SpriteEffect.Projection <- frame.Camera.Projection
            SpriteQuadBatch.end' devices.SpriteEffect devices.SpriteQuadBatch
            SpriteQuadBatch.begin' devices.SpriteQuadBatch

        let flushPendingBillboards() =
          if devices.BillboardBatch.SpriteCount > 0 then
            BillboardBatch.end' devices.SpriteEffect devices.BillboardBatch
            BillboardBatch.begin' devices.BillboardBatch

        for i = 0 to items.Count - 1 do
          let struct (_, cmd) = items.[i]

          match cmd with
          | RenderCommand.DrawSpriteQuad s ->
            flushPendingBillboards()

            if s.Texture <> currentSpriteQuadTexture then
              flushPendingQuads()
              currentSpriteQuadTexture <- s.Texture
              devices.SpriteEffect.Texture <- s.Texture

            let q = s.Quad

            SpriteQuadBatch.draw
              q.Center
              q.Right
              q.Up
              q.Color
              q.Uv
              devices.SpriteQuadBatch

          | RenderCommand.DrawSpriteBillboard s ->
            flushPendingQuads()

            if s.Texture <> currentSpriteBillboardTexture then
              flushPendingBillboards()
              currentSpriteBillboardTexture <- s.Texture
              devices.SpriteEffect.Texture <- s.Texture

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
              devices.BillboardBatch

          | RenderCommand.DrawQuadEffect e ->
            flushPendingBillboards()
            flushPendingQuads()
            let effect = e.Effect

            e.Setup
            |> ValueOption.iter(fun setup ->
              setup effect {
                EffectContext.World = Matrix.Identity
                View = frame.Camera.View
                Projection = frame.Camera.Projection
              })

            applySpriteStates pass
            SpriteQuadBatch.begin' devices.SpriteQuadBatch
            let q = e.Quad

            SpriteQuadBatch.draw
              q.Center
              q.Right
              q.Up
              q.Color
              q.Uv
              devices.SpriteQuadBatch

            SpriteQuadBatch.end' effect devices.SpriteQuadBatch

          | RenderCommand.DrawBillboardEffect e ->
            flushPendingQuads()
            flushPendingBillboards()
            let effect = e.Effect

            e.Setup
            |> ValueOption.iter(fun setup ->
              setup effect {
                EffectContext.World = Matrix.Identity
                View = frame.Camera.View
                Projection = frame.Camera.Projection
              })

            applySpriteStates pass
            BillboardBatch.begin' devices.BillboardBatch
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
              devices.BillboardBatch

            BillboardBatch.end' effect devices.BillboardBatch

          | RenderCommand.DrawLine(p1, p2, color, _) ->
            flushPendingQuads()
            flushPendingBillboards()
            applySpriteStates pass
            devices.LineEffect.View <- frame.Camera.View
            devices.LineEffect.Projection <- frame.Camera.Projection
            LineBatch.begin' devices.LineBatch
            LineBatch.addLine p1 p2 color devices.LineBatch
            LineBatch.end' devices.LineEffect devices.LineBatch

          | RenderCommand.DrawLines(verts, lineCount, _) ->
            flushPendingQuads()
            flushPendingBillboards()
            applySpriteStates pass
            devices.LineEffect.View <- frame.Camera.View
            devices.LineEffect.Projection <- frame.Camera.Projection
            LineBatch.begin' devices.LineBatch
            LineBatch.addLines verts lineCount devices.LineBatch
            LineBatch.end' devices.LineEffect devices.LineBatch

          | RenderCommand.DrawLinesEffect(verts, lineCount, effect, setupOpt, _) ->
            flushPendingQuads()
            flushPendingBillboards()

            setupOpt
            |> ValueOption.iter(fun setup ->
              setup effect {
                EffectContext.World = Matrix.Identity
                View = frame.Camera.View
                Projection = frame.Camera.Projection
              })

            applySpriteStates pass
            LineBatch.begin' devices.LineBatch
            LineBatch.addLines verts lineCount devices.LineBatch
            LineBatch.end' effect devices.LineBatch

          | _ -> ()

        flushPendingQuads()
        flushPendingBillboards()

      drawSpritesInList frame.OpaqueSpriteCommands Opaque

      if
        frame.TransparentDrawables.Count > 0
        || frame.TransparentSpriteCommands.Count > 0
      then
        devices.Device.BlendState <- BlendState.AlphaBlend
        devices.Device.DepthStencilState <- DepthStencilState.DepthRead
        lastEffect <- ValueNone
        lastMaterialKey <- ValueNone

        for i in 0 .. frame.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = frame.TransparentDrawables.[i]
          drawDrawable drawable

        drawSpritesInList frame.TransparentSpriteCommands Transparent
        devices.Device.BlendState <- BlendState.Opaque
        devices.Device.DepthStencilState <- DepthStencilState.Default

      frame.OpaqueDrawables.Clear()
      frame.TransparentDrawables.Clear()
      frame.OpaqueSpriteCommands.Clear()
      frame.TransparentSpriteCommands.Clear()

module State =

  let create(config: Pipeline3DConfig) : PipelineState =
    let devices = DeviceContext()
    let frame = FrameContext()
    let cache = BindingCache()
    PipelineState(config, devices, frame, cache, ValueNone)

  let initialize (state: PipelineState) (game: Game) (gd: GraphicsDevice) =
    let devices = state.Devices
    devices.Initialize(gd)
    state.Initialize(game)

module Orchestrate =

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    let frame = state.Frame

    match cmd with
    | RenderCommand.SetCamera camera ->
      Drawing.apply state
      frame.Camera <- camera
      frame.CameraWasSet <- true
      frame.LightingPrepared <- false
      state.UpdateFrustum()
    | RenderCommand.SetLighting lighting ->
      frame.Lighting <- lighting
      frame.AccumulatedLights.Clear()
      lighting.Lights |> Array.iter frame.AccumulatedLights.Add
      frame.LightingPrepared <- false
    | RenderCommand.AddLight light ->
      frame.AccumulatedLights.Add(light)
      frame.LightingPrepared <- false
    | RenderCommand.SetViewport viewport ->
      Drawing.apply state
      state.Devices.Device.Viewport <- viewport
    | RenderCommand.ClearTarget(colorOpt, clearDepth) ->
      Drawing.apply state

      let flags =
        match colorOpt, clearDepth with
        | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
        | ValueSome _, false -> ClearOptions.Target
        | ValueNone, true -> ClearOptions.DepthBuffer
        | ValueNone, false -> ClearOptions.Target

      let color = colorOpt |> ValueOption.defaultValue Color.Black
      state.Devices.Device.Clear(flags, color, 1f, 0)
    | RenderCommand.Draw drawable -> Culling.batchDrawable frame drawable
    | RenderCommand.DrawSpriteQuad s ->
      Culling.batchSpriteCommand
        frame
        s.Pass
        (Vector3.DistanceSquared(frame.Camera.Position, s.Quad.Center))
        cmd
    | RenderCommand.DrawSpriteBillboard s ->
      Culling.batchSpriteCommand
        frame
        s.Pass
        (Vector3.DistanceSquared(frame.Camera.Position, s.Billboard.Position))
        cmd
    | RenderCommand.DrawQuadEffect e ->
      Culling.batchSpriteCommand
        frame
        e.Pass
        (Vector3.DistanceSquared(frame.Camera.Position, e.Quad.Center))
        cmd
    | RenderCommand.DrawBillboardEffect e ->
      Culling.batchSpriteCommand
        frame
        e.Pass
        (Vector3.DistanceSquared(frame.Camera.Position, e.Billboard.Position))
        cmd
    | RenderCommand.DrawLine(_, _, _, pass) ->
      Culling.batchSpriteCommand frame pass 0f cmd
    | RenderCommand.DrawLines(_, _, pass) ->
      Culling.batchSpriteCommand frame pass 0f cmd
    | RenderCommand.DrawLinesEffect(_, _, _, _, pass) ->
      Culling.batchSpriteCommand frame pass 0f cmd
    | RenderCommand.DrawCustom(_, drawFn) ->
      Drawing.apply state

      let ctx =
        match state.Cache.RenderContext with
        | ValueSome c -> c
        | ValueNone -> state.BuildRenderContext()

      match frame.GameCtx with
      | ValueSome gameCtx -> drawFn(gameCtx, ctx)
      | ValueNone -> ()

  let render
    (state: PipelineState)
    (gameCtx: GameContext)
    (buffer: RenderBuffer<unit, RenderCommand>)
    (gameTime: GameTime)
    =
    state.Frame.GameCtx <- ValueSome gameCtx
    state.Reset()
    state.UpdateFrustum()

    let needsTarget =
      state.Config.PostProcess.IsSome || state.Config.Shadows.IsSome

    let sceneTarget =
      if needsTarget then
        let spec = {
          Width = state.Devices.Device.PresentationParameters.BackBufferWidth
          Height = state.Devices.Device.PresentationParameters.BackBufferHeight
          Format = SurfaceFormat.Vector4
          DepthFormat = DepthFormat.Depth24
        }

        let rt = state.Devices.RtPool.Acquire spec
        state.Devices.Device.SetRenderTarget(rt)
        state.Frame.MainSceneTarget <- ValueSome rt
        ValueSome rt
      else
        ValueNone

    for i = 0 to buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    Drawing.prepare state
    Drawing.apply state

    match sceneTarget, state.Config.PostProcess with
    | ValueSome rt, ValueNone ->
      state.Devices.Device.SetRenderTarget(null)

      match state.Devices.SpriteBatch with
      | null -> ()
      | sprite ->
        sprite.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
        state.Devices.Device.SamplerStates.[0] <- SamplerState.PointClamp
        sprite.Draw(rt, state.Devices.Device.Viewport.Bounds, Color.White)
        sprite.End()
    | ValueSome rt, ValueSome _ -> PostProcess.render state gameTime rt
    | _ -> ()

    if not(isNull(box state.Devices.RtPool)) then
      state.Devices.RtPool.ReleaseAll()
