namespace Mibo.Elmish.Graphics2D

open System
open System.Buffers
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open FSharp.UMX
open Mibo.Elmish
open Mibo.Rendering

/// <summary>Unit of measure for render layer ordering.</summary>
/// <remarks>Lower values are drawn first (background), higher values drawn last (foreground).</remarks>
[<Measure>]
type RenderLayer

/// <summary>Convenience alias for a render buffer keyed by <see cref="T:Mibo.Elmish.Graphics2D.RenderLayer"/>.</summary>
/// <remarks>This preserves the simple <c>RenderBuffer&lt;'Cmd&gt;</c> API at call sites while the core buffer remains generic (<see cref="T:Mibo.Elmish.RenderBuffer`2"/>).</remarks>
type RenderBuffer<'Cmd> = RenderBuffer<int<RenderLayer>, 'Cmd>

/// <summary>Unified state for a sprite draw call.</summary>
[<Struct>]
type SpriteState = {
  Texture: Texture2D
  NormalMap: Texture2D voption
  DestX: int
  DestY: int
  Width: int
  Height: int
  SourceRect: Rectangle voption
  Color: Color
  Rotation: float32
  Origin: Vector2
  Effects: SpriteEffects
  Depth: float32
  Layer: int<RenderLayer>
}

/// <summary>Unified state for a particle draw call.</summary>
[<Struct>]
type Particle2DState = {
  Position: Vector2
  Size: Vector2
  Rotation: float32
  Color: Color
  Uv: UvRect
}

// ============================================================================
// Internal Lighting Types (Must be defined early for RendererBuffers)
// ============================================================================

module internal Lighting2DInternal =
  [<Struct>]
  type LightType2D =
    | Point
    | Directional

  [<Struct>]
  type ShadowCasterEntry = {
    Type: LightType2D
    Index: int
    Priority: float32
  }

  type ShadowCasterComparer() =
    interface IComparer<ShadowCasterEntry> with
      member _.Compare(a, b) = b.Priority.CompareTo(a.Priority)

  let shadowCasterComparer =
    ShadowCasterComparer() :> IComparer<ShadowCasterEntry>

// ============================================================================
// 2D Lighting System Types
// ============================================================================

/// <summary>Per-light shadow settings.</summary>
[<Struct>]
type ShadowSettings2D = {
  /// <summary>Optional override for global shadow bias.</summary>
  Bias: float32 voption
}

module ShadowSettings2D =
  let defaults: ShadowSettings2D = { Bias = ValueNone }
  let withBias b : ShadowSettings2D = { Bias = ValueSome b }

/// <summary>A 2D point light.</summary>
[<Struct>]
type PointLight2D = {
  Position: Vector2
  Color: Color
  Intensity: float32
  Radius: float32
  Falloff: float32
  /// <summary>Shadow settings. ValueSome means the light casts shadows.</summary>
  Shadow: ShadowSettings2D voption
}

/// <summary>A 2D directional light.</summary>
[<Struct>]
type DirectionalLight2D = {
  Direction: Vector2
  Color: Color
  Intensity: float32
  /// <summary>Shadow settings. ValueSome means the light casts shadows.</summary>
  Shadow: ShadowSettings2D voption
}

/// <summary>A 2D ambient light.</summary>
[<Struct>]
type AmbientLight2D = { Color: Color }

/// <summary>State of the 2D lighting system for a single frame.</summary>
[<Struct>]
type LightingState2D = {
  Ambient: AmbientLight2D voption
  PointLights: PointLight2D[]
  DirectionalLights: DirectionalLight2D[]
}

/// <summary>A 2D shadow occluder (line segment).</summary>
[<Struct>]
type Occluder2D = {
  P1: Vector2
  P2: Vector2
  /// <summary>Z-height for pseudo-3D shadows (default: 1.0).</summary>
  Height: float32
}

/// <summary>Custom vertex type for 2D line occluders.</summary>
[<Struct>]
type internal VertexPosition2D = { Position: Vector2 }

/// <summary>Quality level for 2D soft shadows.</summary>
[<Struct>]
type SoftShadowQuality2D =
  | NoValue
  | Low
  | Medium
  | High

/// <summary>Configuration for 2D shadows.</summary>
[<Struct>]
type Shadows2DConfig = {
  Enabled: bool
  /// <summary>Angular resolution per shadow strip (e.g., 512).</summary>
  Resolution: int
  /// <summary>Maximum number of shadow-casting lights.</summary>
  MaxShadowLights: int
  SoftShadowQuality: SoftShadowQuality2D
  /// <summary>Global default shadow bias.</summary>
  ShadowBias: float32
}

module Shadows2DConfig =
  let defaults: Shadows2DConfig = {
    Enabled = true
    Resolution = 512
    MaxShadowLights = 16
    SoftShadowQuality = SoftShadowQuality2D.Low
    ShadowBias = 0.001f
  }

// ============================================================================
// Core State & Buffer Types
// ============================================================================

/// <summary>Persistent buffers to avoid per-frame allocations.</summary>
type internal RendererBuffers = {
  mutable PointPositions: Vector2[]
  mutable PointColors: Vector4[]
  mutable PointRadii: float32[]
  mutable PointFalloffs: float32[]
  mutable DirDirections: Vector2[]
  mutable DirColors: Vector4[]
  mutable DirShadowOrigins: Vector2[]
  mutable PointShadowIndices: float32[]
  mutable DirShadowIndices: float32[]
  mutable ScreenSpaceLights: PointLight2D[]
  mutable ShadowIndicesPoint: int[]
  mutable ShadowIndicesDirectional: int[]
  mutable TileCounts: int[]
  mutable TileData: int[]
  mutable TileDataTex: Texture2D
  mutable TileDataBuffer: float32[]
  mutable LastViewMatrix: Matrix
  mutable LastProjectionMatrix: Matrix
  mutable LightingPrepared: bool
  PointLights: ResizeArray<PointLight2D>
  DirectionalLights: ResizeArray<DirectionalLight2D>
  Occluders: ResizeArray<Occluder2D>
  ShadowCasters: ResizeArray<Lighting2DInternal.ShadowCasterEntry>
}

module internal RendererBuffers =
  let createEmpty() : RendererBuffers = {
    PointPositions = Array.empty
    PointColors = Array.empty
    PointRadii = Array.empty
    PointFalloffs = Array.empty
    DirDirections = Array.empty
    DirColors = Array.empty
    DirShadowOrigins = Array.empty
    PointShadowIndices = Array.empty
    DirShadowIndices = Array.empty
    ScreenSpaceLights = Array.empty
    ShadowIndicesPoint = Array.zeroCreate 16
    ShadowIndicesDirectional = Array.zeroCreate 8
    TileCounts = Array.empty
    TileData = Array.empty
    TileDataTex = null
    TileDataBuffer = Array.empty
    LastViewMatrix = Matrix.Identity
    LastProjectionMatrix = Matrix.Identity
    LightingPrepared = false
    PointLights = ResizeArray()
    DirectionalLights = ResizeArray()
    Occluders = ResizeArray()
    ShadowCasters = ResizeArray()
  }

  let ensureCapacity (needed: int) (current: 'T[] byref) =
    if current.Length < needed then
      current <- Array.zeroCreate(Math.Max(current.Length * 2, needed))

  let dispose(b: RendererBuffers) =
    if not(isNull b.TileDataTex) then
      b.TileDataTex.Dispose()

/// <summary>Encapsulates core rendering services for the 2D pipeline.</summary>
[<Struct>]
type internal RenderingServices = {
  Device: GraphicsDevice
  Pool: IRenderTargetPool
  SpriteBatch: SpriteBatch
  DefaultNormalMap: Texture2D
}

/// <summary>Batcher for rendering 2D shadow occluders as line segments.</summary>
module internal OccluderBatch =

  let private vertexDeclaration =
    new VertexDeclaration [|
      VertexElement(
        0,
        VertexElementFormat.Vector2,
        VertexElementUsage.Position,
        0
      )
    |]

  type State = {
    mutable Vertices: VertexPosition2D[]
    mutable VertexBuffer: DynamicVertexBuffer
    mutable VertexCount: int
    GraphicsDevice: GraphicsDevice
  }

  let private ensureBuffers(state: State) =
    if isNull state.VertexBuffer then
      state.VertexBuffer <-
        new DynamicVertexBuffer(
          state.GraphicsDevice,
          vertexDeclaration,
          state.Vertices.Length,
          BufferUsage.WriteOnly
        )

  let private ensureCapacity (numVerts: int) (state: State) =
    let required = state.VertexCount + numVerts

    if required > state.Vertices.Length then
      let newSize = Math.Max(state.Vertices.Length * 2, required)
      let newVerts = ArrayPool.Shared.Rent(newSize)
      state.Vertices.AsSpan().CopyTo(newVerts.AsSpan())
      ArrayPool.Shared.Return(state.Vertices)
      state.Vertices <- newVerts

      if not(isNull state.VertexBuffer) then
        state.VertexBuffer.Dispose()

      state.VertexBuffer <-
        new DynamicVertexBuffer(
          state.GraphicsDevice,
          vertexDeclaration,
          state.Vertices.Length,
          BufferUsage.WriteOnly
        )

  [<Literal>]
  let private DefaultVertexCapacity = 256

  let create(graphicsDevice: GraphicsDevice) = {
    Vertices = ArrayPool.Shared.Rent DefaultVertexCapacity
    VertexBuffer = null
    VertexCount = 0
    GraphicsDevice = graphicsDevice
  }

  let dispose(state: State) =
    if not(isNull state.Vertices) then
      ArrayPool.Shared.Return state.Vertices
      state.Vertices <- null

    if not(isNull state.VertexBuffer) then
      state.VertexBuffer.Dispose()
      state.VertexBuffer <- null

  let begin'(state: State) = state.VertexCount <- 0

  let addLine (p1: Vector2) (p2: Vector2) (height: float32) (state: State) =
    ensureCapacity 2 state

    let idx = state.VertexCount
    state.Vertices.[idx + 0] <- { Position = p1 }
    state.Vertices.[idx + 1] <- { Position = p2 }
    state.VertexCount <- state.VertexCount + 2

  let addOccluder (occluder: Occluder2D) (state: State) =
    addLine occluder.P1 occluder.P2 occluder.Height state

  let end'(state: State) =
    if state.VertexCount > 0 then
      ensureBuffers state
      state.VertexBuffer.SetData(state.Vertices, 0, state.VertexCount)
      state.GraphicsDevice.SetVertexBuffer(state.VertexBuffer)

      state.GraphicsDevice.DrawPrimitives(
        PrimitiveType.LineList,
        0,
        state.VertexCount / 2
      )

/// <summary>Groups persistent hardware dependencies and stateful batchers.</summary>
[<Struct>]
type internal RendererEnvironment = {
  Services: RenderingServices
  Game: Game
  OccluderBatch: OccluderBatch.State voption
  BillboardBatch: BillboardBatch.State voption
  LineBatch: LineBatch.State voption
  PrimitiveEffect: BasicEffect
  ShadowMinBlend: BlendState
  Buffers: RendererBuffers
}

/// <summary>Current active state of the renderer during a Draw pass.</summary>
[<Struct>]
type internal ActiveRenderState = {
  mutable Effect: Effect
  mutable Transform: Nullable<Matrix>
  mutable Camera: Camera
  mutable ViewMatrix: Matrix
  mutable NormalMap: Texture2D
  mutable SortMode: SpriteSortMode
  mutable Blend: BlendState
  mutable Sampler: SamplerState
  mutable DepthStencil: DepthStencilState
  mutable Rasterizer: RasterizerState
  mutable IsBatching: bool
}

/// <summary>Main configuration for 2D lighting.</summary>
[<Struct>]
type Lighting2DConfig = {
  Enabled: bool
  DefaultAmbient: AmbientLight2D voption
  /// <summary>Screen-space tile size for CPU light culling (default: 32).</summary>
  TileSize: int
  /// <summary>Maximum lights per tile (default: 8).</summary>
  MaxLightsPerTile: int
  /// <summary>Shadow configuration.</summary>
  Shadows: Shadows2DConfig voption
}

module Lighting2DConfig =
  let disabled: Lighting2DConfig = {
    Enabled = false
    DefaultAmbient = ValueNone
    TileSize = 32
    MaxLightsPerTile = 8
    Shadows = ValueNone
  }

  let enabled ambient : Lighting2DConfig = {
    Enabled = true
    DefaultAmbient = ValueSome ambient
    TileSize = 32
    MaxLightsPerTile = 8
    Shadows = ValueNone
  }

  let withShadows (cfg: Shadows2DConfig) (lighting: Lighting2DConfig) = {
    lighting with
        Shadows = ValueSome cfg
  }

// ============================================================================
// Internal Lighting Logic
// ============================================================================

module internal Lighting2DInternalLogic =
  open Lighting2DInternal

  [<Struct>]
  type LightBinResults = { TilesX: int; TilesY: int }

  [<Struct>]
  type LightingGrid = {
    TW: int
    TH: int
    TileSize: int
    MaxLightsPerTile: int
  }

  [<Struct>]
  type ShadowBuffers = {
    IndicesPoint: int[]
    IndicesDirectional: int[]
    OriginsDirectional: Vector2[]
    Casters: ResizeArray<ShadowCasterEntry>
  }

  [<Struct>]
  type LightingBuffers = {
    PointPositions: Vector2[]
    PointColors: Vector4[]
    PointRadii: float32[]
    PointFalloffs: float32[]
    PointShadowIndices: float32[]
    DirDirections: Vector2[]
    DirColors: Vector4[]
    DirShadowIndices: float32[]
    OriginsDirectional: Vector2[]
    IndicesPoint: int[]
    IndicesDirectional: int[]
    ScreenSpaceLights: PointLight2D[]
    TileCounts: int[]
    TileData: int[]
    TileDataBuffer: float32[]
  }

  [<Struct>]
  type LightingEnvironment = {
    Device: GraphicsDevice
    Pool: IRenderTargetPool
    Camera: Camera
    ViewMatrix: Matrix
  }

  [<Struct>]
  type LightingScene = {
    PointLights: ResizeArray<PointLight2D>
    DirectionalLights: ResizeArray<DirectionalLight2D>
    Occluders: ResizeArray<Occluder2D>
  }

  let binPointLights
    (grid: inref<LightingGrid>)
    (lights: PointLight2D[])
    (lightCount: int)
    (bufs: inref<LightingBuffers>)
    : LightBinResults =
    Array.Clear(bufs.TileCounts, 0, grid.TW * grid.TH)
    Array.Fill(bufs.TileData, -1, 0, grid.TW * grid.TH * grid.MaxLightsPerTile)

    for i = 0 to lightCount - 1 do
      let l = lights.[i]
      let r = l.Radius
      let minX = Math.Max(0, int(l.Position.X - r) / grid.TileSize)
      let maxX = Math.Min(grid.TW - 1, int(l.Position.X + r) / grid.TileSize)
      let minY = Math.Max(0, int(l.Position.Y - r) / grid.TileSize)
      let maxY = Math.Min(grid.TH - 1, int(l.Position.Y + r) / grid.TileSize)

      for ty = minY to maxY do
        let rowOffset = ty * grid.TW

        for tx = minX to maxX do
          let tileIdx = rowOffset + tx
          let count = bufs.TileCounts.[tileIdx]

          if count < grid.MaxLightsPerTile then
            bufs.TileData.[tileIdx * grid.MaxLightsPerTile + count] <- i
            bufs.TileCounts.[tileIdx] <- count + 1

    { TilesX = grid.TW; TilesY = grid.TH }

  let calculateProjectionParams (camera: Camera) (viewport: Viewport) =
    let inv = Matrix.Invert(camera.View)

    let cw =
      Vector3.Transform(
        Vector3(
          float32 viewport.Width * 0.5f,
          float32 viewport.Height * 0.5f,
          0f
        ),
        inv
      )

    let corner = Vector3.Transform(Vector3.Zero, inv)
    let dist = Vector3.Distance(cw, corner)

    Vector2(float32(floor cw.X), float32(floor cw.Y)), dist

  module Shadows =
    let private gatherCasters
      (scene: inref<LightingScene>)
      (bufs: inref<ShadowBuffers>)
      =
      bufs.Casters.Clear()

      for i = 0 to scene.PointLights.Count - 1 do
        let l = scene.PointLights.[i]

        if l.Shadow.IsSome then
          let distSq = Vector2.DistanceSquared(l.Position, Vector2.Zero)

          bufs.Casters.Add(
            {
              Type = Point
              Index = i
              Priority = l.Intensity / (Math.Max(1.0f, distSq))
            }
          )

      for i = 0 to scene.DirectionalLights.Count - 1 do
        if scene.DirectionalLights.[i].Shadow.IsSome then
          bufs.Casters.Add(
            {
              Type = Directional
              Index = i
              Priority = scene.DirectionalLights.[i].Intensity
            }
          )

      bufs.Casters.Sort(shadowCasterComparer)

    let private setupAtlas
      (env: inref<LightingEnvironment>)
      (cfg: inref<Shadows2DConfig>)
      (atlas: RenderTarget2D voption byref)
      =
      let aw, ah = cfg.Resolution, cfg.MaxShadowLights

      let mutable needsNew = false

      match atlas with
      | ValueSome rt when rt.Width = aw && rt.Height = ah -> ()
      | ValueSome rt ->
        rt.Dispose()
        needsNew <- true
      | ValueNone -> needsNew <- true

      if needsNew then
        atlas <-
          ValueSome(
            env.Pool.Acquire {
              Width = aw
              Height = ah
              Format = SurfaceFormat.Single
              DepthFormat = DepthFormat.Depth24
            }
          )

    let render
      (env: inref<LightingEnvironment>)
      (cfg: inref<Shadows2DConfig>)
      (shader: Effect)
      (blend: BlendState)
      (ocBatch: OccluderBatch.State)
      (scene: inref<LightingScene>)
      (bufs: inref<ShadowBuffers>)
      (atlas: RenderTarget2D voption byref)
      =
      gatherCasters &scene &bufs
      let count = Math.Min(bufs.Casters.Count, cfg.MaxShadowLights)
      Array.Fill(bufs.IndicesPoint, -1)
      Array.Fill(bufs.IndicesDirectional, -1)

      let origin, projectionSize =
        calculateProjectionParams env.Camera env.Device.Viewport

      for i = 0 to count - 1 do
        let entry = bufs.Casters.[i]

        match entry.Type with
        | Point -> bufs.IndicesPoint.[entry.Index] <- i
        | Directional ->
          bufs.IndicesDirectional.[entry.Index] <- i
          bufs.OriginsDirectional.[entry.Index] <- origin

      setupAtlas &env &cfg &atlas
      let sRt = atlas.Value in
      env.Device.SetRenderTarget(sRt)
      env.Device.Clear(Color.White)
      env.Device.DepthStencilState <- DepthStencilState.None
      env.Device.BlendState <- blend
      let vp = env.Device.Viewport
      let aw, ah = cfg.Resolution, cfg.MaxShadowLights

      for i = 0 to count - 1 do
        let entry = bufs.Casters.[i]

        let r =
          match entry.Type with
          | Point -> bufs.IndicesPoint.[entry.Index]
          | Directional -> bufs.IndicesDirectional.[entry.Index]

        if r >= 0 then
          env.Device.Viewport <- Viewport(0, r, aw, 1, 0f, 1f)
          shader.SafeSetParam("AtlasWidth", float32 aw)
          shader.SafeSetParam("AtlasHeight", float32 ah)
          shader.SafeSetParam("ProjectionSize", projectionSize)

          match entry.Type with
          | Point ->
            let l = scene.PointLights.[entry.Index] in
            shader.SafeSetParam("LightPosition", l.Position)
            shader.SafeSetParam("LightRadius", l.Radius)
            shader.CurrentTechnique.Passes.[0].Apply()
            OccluderBatch.begin' ocBatch

            for j = 0 to scene.Occluders.Count - 1 do
              let o = scene.Occluders.[j] in

              for s = 0 to 7 do
                let t1, t2 = float32 s / 8.0f, float32(s + 1) / 8.0f in

                OccluderBatch.addLine
                  (Vector2.Lerp(o.P1, o.P2, t1))
                  (Vector2.Lerp(o.P1, o.P2, t2))
                  o.Height
                  ocBatch

            OccluderBatch.end' ocBatch
          | Directional ->
            let l = scene.DirectionalLights.[entry.Index] in
            shader.SafeSetParam("LightDirection", l.Direction)
            shader.SafeSetParam("LightRadius", -1.0f)

            shader.SafeSetParam(
              "ShadowOrigin",
              bufs.OriginsDirectional.[entry.Index]
            )

            shader.CurrentTechnique.Passes.[0].Apply()
            OccluderBatch.begin' ocBatch

            for j = 0 to scene.Occluders.Count - 1 do
              OccluderBatch.addOccluder scene.Occluders.[j] ocBatch

            OccluderBatch.end' ocBatch

      env.Device.Viewport <- vp
      env.Device.SetRenderTarget null

  module Pipeline =
    let prepare
      (env: inref<LightingEnvironment>)
      (grid: inref<LightingGrid>)
      (scene: inref<LightingScene>)
      (bufs: inref<LightingBuffers>)
      (tileTex: Texture2D byref)
      (tileBuf: float32[] byref)
      =
      let pCount = scene.PointLights.Count

      for i = 0 to pCount - 1 do
        bufs.ScreenSpaceLights.[i] <- {
          scene.PointLights.[i] with
              Position =
                Vector2.Transform(
                  scene.PointLights.[i].Position,
                  env.ViewMatrix
                )
        }

      let bin = binPointLights &grid bufs.ScreenSpaceLights pCount &bufs

      for i = 0 to pCount - 1 do
        let l = scene.PointLights.[i] in
        bufs.PointPositions.[i] <- l.Position
        bufs.PointColors.[i] <- l.Color.ToVector4() * l.Intensity
        bufs.PointRadii.[i] <- l.Radius
        bufs.PointFalloffs.[i] <- l.Falloff

      let req = bin.TilesX * bin.TilesY * grid.MaxLightsPerTile
      let texWidth = Math.Max(1, req)

      if isNull tileTex || tileTex.Width <> texWidth then
        if not(isNull tileTex) then
          tileTex.Dispose()

        tileTex <-
          new Texture2D(env.Device, texWidth, 1, false, SurfaceFormat.Single)

        RendererBuffers.ensureCapacity req &tileBuf

      for j = 0 to req - 1 do
        tileBuf.[j] <- float32 bufs.TileData.[j]

      tileTex.SetData(tileBuf, 0, req)
      bin

    let apply
      (env: inref<LightingEnvironment>)
      (grid: inref<LightingGrid>)
      (cfg: inref<Lighting2DConfig>)
      (state: inref<LightingState2D>)
      (scene: inref<LightingScene>)
      (fx: Effect)
      (bufs: inref<LightingBuffers>)
      (tileTex: Texture2D)
      (bin: LightBinResults)
      (shadowAtlas: RenderTarget2D voption)
      =
      if state.Ambient.IsSome then
        fx.SafeSetParam("AmbientColor", state.Ambient.Value.Color)

      let pCount = scene.PointLights.Count
      fx.SafeSetParam("PointLightPositions", bufs.PointPositions)
      fx.SafeSetParam("PointLightColors", bufs.PointColors)
      fx.SafeSetParam("PointLightRadii", bufs.PointRadii)
      fx.SafeSetParam("PointLightFalloffs", bufs.PointFalloffs)
      fx.SafeSetParam("PointLightCount", pCount)

      let dCount = scene.DirectionalLights.Count in
      fx.SafeSetParam("DirectionalLightCount", dCount)

      if dCount > 0 then
        for i = 0 to dCount - 1 do
          let l = scene.DirectionalLights.[i] in

          bufs.DirDirections.[i] <-
            (if l.Direction.LengthSquared() > 0.0001f then
               Vector2.Normalize(l.Direction)
             else
               l.Direction)

          bufs.DirColors.[i] <- l.Color.ToVector4() * l.Intensity

        fx.SafeSetParam("DirectionalLightDirections", bufs.DirDirections)
        fx.SafeSetParam("DirectionalLightColors", bufs.DirColors)

        fx.SafeSetParam(
          "DirectionalLightShadowOrigins",
          bufs.OriginsDirectional
        )

      fx.SafeSetParam("LightIndexBuffer", tileTex :> Texture)
      fx.SafeSetParam("TileSize", float32 grid.TileSize)
      fx.SafeSetParam("TilesX", float32 bin.TilesX)
      fx.SafeSetParam("MaxLightsPerTile", grid.MaxLightsPerTile)
      let vp = env.Device.Viewport in

      fx.SafeSetParam(
        "ViewportSize",
        Vector2(float32 vp.Width, float32 vp.Height)
      )

      fx.SafeSetParam(
        "ViewportSizeInv",
        Vector2(1.0f / float32 vp.Width, 1.0f / float32 vp.Height)
      )

      fx.SafeSetParam("ViewMatrix", env.ViewMatrix)
      fx.SafeSetParam("ProjectionMatrix", env.Camera.Projection)
      fx.SafeSetParam("InverseViewMatrix", Matrix.Invert(env.ViewMatrix))

      fx.SafeSetParam(
        "InverseProjectionMatrix",
        Matrix.Invert(env.Camera.Projection)
      )

      fx.SafeSetParam(
        "ViewProjectionMatrix",
        env.ViewMatrix * env.Camera.Projection
      )

      fx.SafeSetParam("LightIndexBufferWidth", float32 tileTex.Width)
      fx.SafeSetParam("LightIndexBufferHeight", float32 tileTex.Height)

      match cfg.Shadows, shadowAtlas with
      | ValueSome sCfg, ValueSome atl ->
        fx.SafeSetParam("ShadowAtlas", atl :> Texture)

        fx.SafeSetParam(
          "ShadowAtlasSize",
          Vector2(float32 atl.Width, float32 atl.Height)
        )

        fx.SafeSetParam("ShadowBias", (sCfg.ShadowBias: float32))

        let _, projectionSize =
          calculateProjectionParams env.Camera env.Device.Viewport

        fx.SafeSetParam("ProjectionSize", projectionSize)

        let pc, dc =
          Math.Min(bufs.IndicesPoint.Length, pCount),
          Math.Min(bufs.IndicesDirectional.Length, dCount)

        for i = 0 to pc - 1 do
          bufs.PointShadowIndices.[i] <- float32 bufs.IndicesPoint.[i]

        for i = 0 to dc - 1 do
          bufs.DirShadowIndices.[i] <- float32 bufs.IndicesDirectional.[i]

        fx.SafeSetParam("PointLightShadowIndices", bufs.PointShadowIndices)
        fx.SafeSetParam("DirectionalLightShadowIndices", bufs.DirShadowIndices)
      | _ -> ()

// ============================================================================
// Render Command Types
// ============================================================================

/// <summary>Unified state for a text draw call.</summary>
[<Struct>]
type TextState = {
  Font: SpriteFont
  Text: string
  DestX: int
  DestY: int
  Color: Color
  Scale: float32
  Rotation: float32
  Origin: Vector2
  Effects: SpriteEffects
  Layer: int<RenderLayer>
}

module internal Text =
  let empty: TextState = {
    Font = null
    Text = ""
    DestX = 0
    DestY = 0
    Color = Color.White
    Scale = 1f
    Rotation = 0f
    Origin = Vector2.Zero
    Effects = SpriteEffects.None
    Layer = 0<RenderLayer>
  }

/// <summary>Legacy struct for text draw command parameters.</summary>
[<Struct>]
type internal TextDrawCmd = {
  Font: SpriteFont
  Text: string
  Position: Vector2
  Color: Color
  Rotation: float32
  Origin: Vector2
  Scale: float32
  Effects: SpriteEffects
  Depth: float32
}

/// <summary>Shader override types for 2D rendering stages.</summary>
[<Struct>]
type ShaderBase2D =
  | LitSprite
  | ShadowCaster
  | PostProcess

/// <summary>Batch of 2D particles command parameters.</summary>
[<Struct>]
type DrawParticlesCmd = {
  Particles: Particle2DState[]
  Count: int
  Texture: Texture2D
  Effect: Effect
  ParticlesLayer: int<RenderLayer>
}

/// <summary>2D line command parameters.</summary>
[<Struct>]
type DrawLine2DCmd = {
  P1: Vector2
  P2: Vector2
  LineColor: Color
  LineLayer: int<RenderLayer>
}

/// <summary>2D rectangle command parameters.</summary>
[<Struct>]
type DrawRect2DCmd = {
  Rect: Rectangle
  RectColor: Color
  RectLayer: int<RenderLayer>
}

/// <summary>2D circle command parameters.</summary>
[<Struct>]
type DrawCircle2DCmd = {
  Center: Vector2
  Radius: float32
  Segments: int
  CircleColor: Color
  CircleLayer: int<RenderLayer>
}

/// <summary>A 2D render command.</summary>
[<Struct>]
type RenderCmd2D =
  | SetViewport of viewport: Viewport
  | ClearTarget of clearColor: Color voption * clearDepth: bool
  | SetCamera of camera: Camera
  | SetEffect of effect: Effect voption
  | SetBlendState of blendState: BlendState
  | SetSamplerState of samplerState: SamplerState
  | SetDepthStencilState of depthStencilState: DepthStencilState
  | SetRasterizerState of rasterizerState: RasterizerState
  | DrawCustom of draw: (GameContext -> unit)
  | DrawSprite of sprite: SpriteState
  | DrawText of text: TextState
  | SetLighting of lightingState: LightingState2D
  | AddLighting of ambient: AmbientLight2D
  | AddPointLight of pointLightVal: PointLight2D
  | AddDirectionalLight of directionalLightVal: DirectionalLight2D
  | AddOccluder of occluderVal: Occluder2D
  | DrawParticles of particlesCmd: DrawParticlesCmd
  | DrawLine2D of lineCmd: DrawLine2DCmd
  | DrawRect2D of rectCmd: DrawRect2DCmd
  | DrawCircle2D of circleCmd: DrawCircle2DCmd

// ============================================================================
// Post-Processing Configuration
// ============================================================================

/// <summary>Configuration for vignette effect.</summary>
[<Struct>]
type VignetteConfig = {
  Effect: Effect
  Radius: float32
  Softness: float32
}

module VignetteConfig =
  let defaults(effect: Effect) : VignetteConfig = {
    Effect = effect
    Radius = 0.7f
    Softness = 0.3f
  }

/// <summary>Configuration for bloom effect.</summary>
[<Struct>]
type BloomConfig2D = {
  ExtractEffect: Effect
  BlurEffect: Effect
  CompositeEffect: Effect
  Threshold: float32
  Intensity: float32
  Scatter: float32
}

module BloomConfig2D =
  let defaults
    (extractFx: Effect)
    (blurFx: Effect)
    (compositeFx: Effect)
    : BloomConfig2D =
    {
      ExtractEffect = extractFx
      BlurEffect = blurFx
      CompositeEffect = compositeFx
      Threshold = 0.8f
      Intensity = 1.0f
      Scatter = 0.7f
    }

/// <summary>Configuration for color grading via LUT.</summary>
[<Struct>]
type ColorGradeConfig = {
  Effect: Effect
  LutTexture: Texture3D
  LutSize: int
  Blend: float32
}

module ColorGradeConfig =
  let defaults (effect: Effect) (lut: Texture3D) : ColorGradeConfig = {
    Effect = effect
    LutTexture = lut
    LutSize = 32
    Blend = 1.0f
  }

/// <summary>Custom pass for effects not covered by built-in configs.</summary>
[<Struct>]
type CustomPostProcessPass = {
  Effect: Effect
  SetupEffect: (Effect -> GameTime -> RenderTarget2D -> unit) voption
}

/// <summary>Main post-processing configuration.</summary>
[<Struct>]
type PostProcess2DConfig = {
  Vignette: VignetteConfig voption
  Bloom: BloomConfig2D voption
  ColorGrade: ColorGradeConfig voption
  CustomPasses: CustomPostProcessPass[] voption
}

module PostProcess2DConfig =
  let none: PostProcess2DConfig = {
    Vignette = ValueNone
    Bloom = ValueNone
    ColorGrade = ValueNone
    CustomPasses = ValueNone
  }

  let withVignette (cfg: VignetteConfig) (pp: PostProcess2DConfig) = {
    pp with
        Vignette = ValueSome cfg
  }

  let withBloom (cfg: BloomConfig2D) (pp: PostProcess2DConfig) = {
    pp with
        Bloom = ValueSome cfg
  }

  let withColorGrade (cfg: ColorGradeConfig) (pp: PostProcess2DConfig) = {
    pp with
        ColorGrade = ValueSome cfg
  }

  let withCustomPasses
    (passes: CustomPostProcessPass[])
    (pp: PostProcess2DConfig)
    =
    {
      pp with
          CustomPasses = ValueSome passes
    }

module PostProcess2D =
  let none = PostProcess2DConfig.none
  let inline withVignette cfg pp = PostProcess2DConfig.withVignette cfg pp
  let inline withBloom cfg pp = PostProcess2DConfig.withBloom cfg pp

  let inline withColorGrade cfg pp =
    PostProcess2DConfig.withColorGrade cfg pp

  let inline withCustomPasses cfg pp =
    PostProcess2DConfig.withCustomPasses cfg pp

// ============================================================================
// Batcher Configuration
// ============================================================================

/// <summary>Configuration for <see cref="T:Mibo.Elmish.Graphics2D.Batch2DRenderer`1"/>.</summary>
[<Struct>]
type Batch2DConfig = {
  ClearColor: Color voption
  SortCommands: bool
  SortMode: SpriteSortMode
  BlendState: BlendState
  SamplerState: SamplerState
  DepthStencilState: DepthStencilState
  RasterizerState: RasterizerState
  Effect: Effect
  TransformMatrix: Matrix voption
  PostProcess: PostProcess2DConfig voption
  Lighting: Lighting2DConfig voption
  ShaderOverrides: Dictionary<ShaderBase2D, Effect>
  FinalBlendState: BlendState
}

module Batch2DConfig =
  let defaults: Batch2DConfig = {
    ClearColor = ValueSome Color.CornflowerBlue
    SortCommands = true
    SortMode = SpriteSortMode.Deferred
    BlendState = BlendState.AlphaBlend
    SamplerState = SamplerState.LinearClamp
    DepthStencilState = DepthStencilState.None
    RasterizerState = RasterizerState.CullCounterClockwise
    Effect = null
    TransformMatrix = ValueNone
    PostProcess = ValueNone
    Lighting = ValueNone
    ShaderOverrides = Dictionary()
    FinalBlendState = BlendState.Opaque
  }

  let inline withClearColor (color: Color voption) (cfg: Batch2DConfig) = {
    cfg with
        ClearColor = color
  }

  let inline withSortCommands (sort: bool) (cfg: Batch2DConfig) = {
    cfg with
        SortCommands = sort
  }

  let inline withSortMode (mode: SpriteSortMode) (cfg: Batch2DConfig) = {
    cfg with
        SortMode = mode
  }

  let inline withBlendState (state: BlendState) (cfg: Batch2DConfig) = {
    cfg with
        BlendState = state
  }

  let inline withSamplerState (state: SamplerState) (cfg: Batch2DConfig) = {
    cfg with
        SamplerState = state
  }

  let inline withDepthStencilState
    (state: DepthStencilState)
    (cfg: Batch2DConfig)
    =
    { cfg with DepthStencilState = state }

  let inline withRasterizerState (state: RasterizerState) (cfg: Batch2DConfig) = {
    cfg with
        RasterizerState = state
  }

  let inline withEffect (effect: Effect) (cfg: Batch2DConfig) = {
    cfg with
        Effect = effect
  }

  let inline withTransform (matrix: Matrix voption) (cfg: Batch2DConfig) = {
    cfg with
        TransformMatrix = matrix
  }

  let inline withPostProcess (pp: PostProcess2DConfig) (cfg: Batch2DConfig) = {
    cfg with
        PostProcess = ValueSome pp
  }

  let inline withLighting (lighting: Lighting2DConfig) (cfg: Batch2DConfig) = {
    cfg with
        Lighting = ValueSome lighting
  }

  let inline withShader
    (baseType: ShaderBase2D)
    (effect: Effect)
    (cfg: Batch2DConfig)
    =
    cfg.ShaderOverrides.Add(baseType, effect)
    cfg

  let inline withLitSprite (effect: Effect) (cfg: Batch2DConfig) =
    withShader ShaderBase2D.LitSprite effect cfg

  let inline withShadowCaster (effect: Effect) (cfg: Batch2DConfig) =
    withShader ShaderBase2D.ShadowCaster effect cfg

  let inline withFinalBlendState (state: BlendState) (cfg: Batch2DConfig) = {
    cfg with
        FinalBlendState = state
  }

// ============================================================================
// Semantic Modules
// ============================================================================

module internal LightingProcessor =
  open Lighting2DInternalLogic

  let apply
    (env: inref<RendererEnvironment>)
    (state: inref<ActiveRenderState>)
    (lCfg: Lighting2DConfig)
    (fx: Effect)
    (shadowAtlas: RenderTarget2D voption)
    =
    if fx <> null then
      let b = env.Buffers

      let tw =
        (env.Services.Device.Viewport.Width + lCfg.TileSize - 1) / lCfg.TileSize

      let th =
        (env.Services.Device.Viewport.Height + lCfg.TileSize - 1)
        / lCfg.TileSize

      let lEnv: LightingEnvironment = {
        Device = env.Services.Device
        Pool = env.Services.Pool
        Camera = state.Camera
        ViewMatrix = state.ViewMatrix
      }

      let lGrid: LightingGrid = {
        TW = tw
        TH = th
        TileSize = lCfg.TileSize
        MaxLightsPerTile = lCfg.MaxLightsPerTile
      }

      let lScene: LightingScene = {
        PointLights = b.PointLights
        DirectionalLights = b.DirectionalLights
        Occluders = b.Occluders
      }

      let lBufs: LightingBuffers = {
        PointPositions = b.PointPositions
        PointColors = b.PointColors
        PointRadii = b.PointRadii
        PointFalloffs = b.PointFalloffs
        PointShadowIndices = b.PointShadowIndices
        DirDirections = b.DirDirections
        DirColors = b.DirColors
        DirShadowIndices = b.DirShadowIndices
        OriginsDirectional = b.DirShadowOrigins
        IndicesPoint = b.ShadowIndicesPoint
        IndicesDirectional = b.ShadowIndicesDirectional
        ScreenSpaceLights = b.ScreenSpaceLights
        TileCounts = b.TileCounts
        TileData = b.TileData
        TileDataBuffer = b.TileDataBuffer
      }

      if
        not b.LightingPrepared
        || b.LastViewMatrix <> state.ViewMatrix
        || b.LastProjectionMatrix <> state.Camera.Projection
      then
        RendererBuffers.ensureCapacity (tw * th) &b.TileCounts

        RendererBuffers.ensureCapacity
          (tw * th * lCfg.MaxLightsPerTile)
          &b.TileData

        RendererBuffers.ensureCapacity b.PointLights.Count &b.PointPositions
        RendererBuffers.ensureCapacity b.PointLights.Count &b.PointColors
        RendererBuffers.ensureCapacity b.PointLights.Count &b.PointRadii
        RendererBuffers.ensureCapacity b.PointLights.Count &b.PointFalloffs

        RendererBuffers.ensureCapacity
          b.DirectionalLights.Count
          &b.DirDirections

        RendererBuffers.ensureCapacity b.DirectionalLights.Count &b.DirColors
        RendererBuffers.ensureCapacity b.PointLights.Count &b.PointShadowIndices

        RendererBuffers.ensureCapacity
          b.DirectionalLights.Count
          &b.DirShadowIndices

        RendererBuffers.ensureCapacity b.PointLights.Count &b.ScreenSpaceLights

        // Ensure shadow mapping arrays can hold an entry for every light
        RendererBuffers.ensureCapacity b.PointLights.Count &b.ShadowIndicesPoint

        RendererBuffers.ensureCapacity
          b.DirectionalLights.Count
          &b.ShadowIndicesDirectional

        RendererBuffers.ensureCapacity
          b.DirectionalLights.Count
          &b.DirShadowOrigins

        let lBufsUpdated: LightingBuffers = {
          PointPositions = b.PointPositions
          PointColors = b.PointColors
          PointRadii = b.PointRadii
          PointFalloffs = b.PointFalloffs
          PointShadowIndices = b.PointShadowIndices
          DirDirections = b.DirDirections
          DirColors = b.DirColors
          DirShadowIndices = b.DirShadowIndices
          OriginsDirectional = b.DirShadowOrigins
          IndicesPoint = b.ShadowIndicesPoint
          IndicesDirectional = b.ShadowIndicesDirectional
          ScreenSpaceLights = b.ScreenSpaceLights
          TileCounts = b.TileCounts
          TileData = b.TileData
          TileDataBuffer = b.TileDataBuffer
        }

        Pipeline.prepare
          &lEnv
          &lGrid
          &lScene
          &lBufsUpdated
          &b.TileDataTex
          &b.TileDataBuffer
        |> ignore

        b.LightingPrepared <- true
        b.LastViewMatrix <- state.ViewMatrix
        b.LastProjectionMatrix <- state.Camera.Projection

      let lBufsFinal: LightingBuffers = {
        PointPositions = b.PointPositions
        PointColors = b.PointColors
        PointRadii = b.PointRadii
        PointFalloffs = b.PointFalloffs
        PointShadowIndices = b.PointShadowIndices
        DirDirections = b.DirDirections
        DirColors = b.DirColors
        DirShadowIndices = b.DirShadowIndices
        OriginsDirectional = b.DirShadowOrigins
        IndicesPoint = b.ShadowIndicesPoint
        IndicesDirectional = b.ShadowIndicesDirectional
        ScreenSpaceLights = b.ScreenSpaceLights
        TileCounts = b.TileCounts
        TileData = b.TileData
        TileDataBuffer = b.TileDataBuffer
      }

      let bin: LightBinResults = { TilesX = tw; TilesY = th }

      let lState: LightingState2D = {
        Ambient = lCfg.DefaultAmbient
        PointLights = [||]
        DirectionalLights = [||]
      }

      Pipeline.apply
        &lEnv
        &lGrid
        &lCfg
        &lState
        &lScene
        fx
        &lBufsFinal
        b.TileDataTex
        bin
        shadowAtlas

module internal CommandDispatcher =
  let beginBatch
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    =
    if lCfg.IsSome then
      LightingProcessor.apply &env &state lCfg.Value state.Effect shadowAtlas

    if state.Effect <> null then
      state.Effect.SafeSetParam("World", Matrix.Identity)
      state.Effect.SafeSetParam("View", state.ViewMatrix)
      state.Effect.SafeSetParam("ViewMatrix", state.ViewMatrix)
      state.Effect.SafeSetParam("Projection", state.Camera.Projection)
      state.Effect.SafeSetParam("ProjectionMatrix", state.Camera.Projection)

    let transform =
      if state.Effect <> null && lCfg.IsSome then
        Nullable Matrix.Identity
      else
        state.Transform

    env.Services.SpriteBatch.Begin(
      state.SortMode,
      state.Blend,
      state.Sampler,
      state.DepthStencil,
      state.Rasterizer,
      state.Effect,
      transform
    )

    state.IsBatching <- true

  let endBatch
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    =
    env.Services.SpriteBatch.End()
    state.IsBatching <- false

  let private renderSprite
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    (s: inref<SpriteState>)
    =
    let targetNM =
      s.NormalMap |> ValueOption.defaultValue env.Services.DefaultNormalMap

    if
      state.IsBatching && state.Effect <> null && targetNM <> state.NormalMap
    then
      endBatch &env &state

    if not state.IsBatching then
      beginBatch &env &state lCfg shadowAtlas

    if state.Effect <> null && targetNM <> state.NormalMap then
      state.Effect.SafeSetParam("NormalMap", targetNM :> Texture)
      state.NormalMap <- targetNM

    env.Services.SpriteBatch.Draw(
      s.Texture,
      Rectangle(s.DestX, s.DestY, s.Width, s.Height),
      (s.SourceRect |> ValueOption.toNullable),
      s.Color,
      s.Rotation,
      s.Origin,
      s.Effects,
      s.Depth
    )

  let private renderText
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    (s: inref<TextState>)
    =
    if
      state.IsBatching
      && state.Effect <> null
      && env.Services.DefaultNormalMap <> state.NormalMap
    then
      endBatch &env &state

    if not state.IsBatching then
      beginBatch &env &state lCfg shadowAtlas

    if
      state.Effect <> null && env.Services.DefaultNormalMap <> state.NormalMap
    then
      state.Effect.SafeSetParam(
        "NormalMap",
        env.Services.DefaultNormalMap :> Texture
      )

      state.NormalMap <- env.Services.DefaultNormalMap

    env.Services.SpriteBatch.DrawString(
      s.Font,
      s.Text,
      Vector2(float32 s.DestX, float32 s.DestY),
      s.Color,
      s.Rotation,
      s.Origin,
      s.Scale,
      s.Effects,
      0f
    )

  let private renderParticles
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    (pCmd: inref<DrawParticlesCmd>)
    =
    if state.IsBatching then
      endBatch &env &state

    let batch = env.BillboardBatch.Value
    let fx = pCmd.Effect

    match fx with
    | :? BasicEffect as be ->
      be.World <- Matrix.Identity
      be.View <- state.ViewMatrix
      be.Projection <- state.Camera.Projection
      be.Texture <- pCmd.Texture
    | _ ->
      fx.SafeSetParam("Texture", pCmd.Texture)
      fx.SafeSetParam("DiffuseTexture", pCmd.Texture)
      fx.SafeSetParam("World", Matrix.Identity)
      fx.SafeSetParam("View", state.ViewMatrix)
      fx.SafeSetParam("Projection", state.Camera.Projection)

    BillboardBatch.begin' batch

    for pIdx = 0 to pCmd.Count - 1 do
      let p = pCmd.Particles.[pIdx] in
      BillboardBatch.draw2D p.Position p.Size p.Rotation p.Color p.Uv batch

    BillboardBatch.end' fx batch
    beginBatch &env &state lCfg shadowAtlas

  let private renderShape
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    (draw: LineBatch.State -> unit)
    =
    if state.IsBatching then
      endBatch &env &state

    let batch = env.LineBatch.Value
    let fx = env.PrimitiveEffect
    fx.World <- Matrix.Identity
    fx.View <- state.ViewMatrix
    fx.Projection <- state.Camera.Projection
    LineBatch.begin' batch
    draw batch
    LineBatch.end' fx batch
    beginBatch &env &state lCfg shadowAtlas

  let execute
    (env: inref<RendererEnvironment>)
    (state: byref<ActiveRenderState>)
    (lCfg: Lighting2DConfig voption)
    (shadowAtlas: RenderTarget2D voption)
    (cmd: inref<RenderCmd2D>)
    =
    match cmd with
    | SetViewport vp ->
      if state.IsBatching then
        endBatch &env &state

      env.Services.Device.Viewport <- vp
      beginBatch &env &state lCfg shadowAtlas
    | ClearTarget(colorOpt, clearDepth) ->
      if state.IsBatching then
        endBatch &env &state

      match colorOpt, clearDepth with
      | ValueSome c, true ->
        env.Services.Device.Clear(
          ClearOptions.Target ||| ClearOptions.DepthBuffer,
          c,
          1.0f,
          0
        )
      | ValueSome c, false ->
        env.Services.Device.Clear(ClearOptions.Target, c, 1.0f, 0)
      | ValueNone, true ->
        env.Services.Device.Clear(
          ClearOptions.DepthBuffer,
          Color.Black,
          1.0f,
          0
        )
      | ValueNone, false -> ()

      beginBatch &env &state lCfg shadowAtlas
    | SetCamera cam ->
      if state.IsBatching then
        endBatch &env &state

      state.Transform <- Nullable cam.View
      state.ViewMatrix <- cam.View
      state.Camera <- cam
      beginBatch &env &state lCfg shadowAtlas
    | SetEffect effectOpt ->
      if state.IsBatching then
        endBatch &env &state

      state.Effect <-
        match effectOpt with
        | ValueSome e -> e
        | ValueNone -> state.Effect

      state.NormalMap <- null
      beginBatch &env &state lCfg shadowAtlas
    | SetBlendState bs ->
      if state.IsBatching then
        endBatch &env &state
        state.Blend <- bs
        beginBatch &env &state lCfg shadowAtlas
    | SetSamplerState ss ->
      if state.IsBatching then
        endBatch &env &state
        state.Sampler <- ss
        beginBatch &env &state lCfg shadowAtlas
    | SetDepthStencilState ds ->
      if state.IsBatching then
        endBatch &env &state
        state.DepthStencil <- ds
        beginBatch &env &state lCfg shadowAtlas
    | SetRasterizerState rs ->
      if state.IsBatching then
        endBatch &env &state
        state.Rasterizer <- rs
        beginBatch &env &state lCfg shadowAtlas
    | DrawCustom draw ->
      if state.IsBatching then
        endBatch &env &state

      draw {
        GraphicsDevice = env.Services.Device
        Content = env.Game.Content
        Game = env.Game
      }

      beginBatch &env &state lCfg shadowAtlas
    | DrawSprite s -> renderSprite &env &state lCfg shadowAtlas &s
    | DrawText s -> renderText &env &state lCfg shadowAtlas &s
    | DrawParticles pCmd -> renderParticles &env &state lCfg shadowAtlas &pCmd
    | DrawLine2D lCmd ->
      renderShape &env &state lCfg shadowAtlas (fun b ->
        LineBatch.addLine2D lCmd.P1 lCmd.P2 lCmd.LineColor b)
    | DrawRect2D rCmd ->
      renderShape &env &state lCfg shadowAtlas (fun b ->
        LineBatch.addRect2D rCmd.Rect rCmd.RectColor b)
    | DrawCircle2D cCmd ->
      renderShape &env &state lCfg shadowAtlas (fun b ->
        LineBatch.addCircle2D
          cCmd.Center
          cCmd.Radius
          cCmd.Segments
          cCmd.CircleColor
          b)
    | _ -> ()

module internal PostProcessPipeline =
  let private drawPass
    (services: inref<RenderingServices>)
    (input: Texture2D)
    (output: RenderTarget2D voption)
    (effect: Effect)
    =
    if output.IsSome then
      services.Device.SetRenderTarget output.Value

    services.Device.Clear(Color.Transparent)

    services.SpriteBatch.Begin(
      SpriteSortMode.Immediate,
      BlendState.Opaque,
      SamplerState.LinearClamp,
      null,
      null,
      effect
    )

    services.SpriteBatch.Draw(
      input,
      services.Device.Viewport.Bounds,
      Color.White
    )

    services.SpriteBatch.End()

  let apply
    (env: inref<RendererEnvironment>)
    (cfg: inref<PostProcess2DConfig>)
    (sceneRt: RenderTarget2D)
    (gameTime: GameTime)
    : RenderTarget2D =
    let services = env.Services
    let viewport = services.Device.Viewport
    let mutable currentInput = sceneRt

    let getTempTarget() =
      services.Pool.Acquire {
        Width = viewport.Width
        Height = viewport.Height
        Format = SurfaceFormat.Color
        DepthFormat = DepthFormat.None
      }

    let mutable currentOutput = ValueSome(getTempTarget())

    let swap() =
      let tmp = currentInput in

      match currentOutput with
      | ValueSome out ->
        currentInput <- out
        currentOutput <- ValueSome tmp
      | ValueNone -> ()

    if cfg.Vignette.IsSome then
      let v = cfg.Vignette.Value in
      v.Effect.SafeSetParam("Radius", v.Radius)
      v.Effect.SafeSetParam("Softness", v.Softness)
      drawPass &services currentInput currentOutput v.Effect
      swap()

    if cfg.Bloom.IsSome then
      let b = cfg.Bloom.Value in
      let sceneInput = currentInput
      b.ExtractEffect.SafeSetParam("Threshold", b.Threshold)
      drawPass &services currentInput currentOutput b.ExtractEffect
      swap()
      b.BlurEffect.SafeSetParam("Intensity", b.Intensity)
      drawPass &services currentInput currentOutput b.BlurEffect
      swap()

      if currentOutput.IsSome then
        services.Device.SetRenderTarget currentOutput.Value

      services.Device.Clear(Color.Transparent)
      b.CompositeEffect.SafeSetParam("BloomTexture", currentInput :> Texture)

      services.SpriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.Opaque,
        SamplerState.LinearClamp,
        null,
        null,
        b.CompositeEffect
      )

      services.SpriteBatch.Draw(sceneInput, viewport.Bounds, Color.White)
      services.SpriteBatch.End()
      swap()

    if cfg.ColorGrade.IsSome then
      let cg = cfg.ColorGrade.Value in
      cg.Effect.SafeSetParam("LutTexture", cg.LutTexture :> Texture)
      cg.Effect.SafeSetParam("LutSize", float32 cg.LutSize)
      cg.Effect.SafeSetParam("Blend", cg.Blend)
      drawPass &services currentInput currentOutput cg.Effect
      swap()

    if cfg.CustomPasses.IsSome then
      for pass in cfg.CustomPasses.Value do
        if pass.SetupEffect.IsSome then
          pass.SetupEffect.Value pass.Effect gameTime currentInput

        drawPass &services currentInput currentOutput pass.Effect
        swap()

    currentInput

// ============================================================================
// Batch2DRenderer Class
// ============================================================================

type Batch2DRenderer<'Model>
  (
    game: Game,
    config: Batch2DConfig,
    [<InlineIfLambda>] view:
      GameContext * 'Model * RenderBuffer<RenderCmd2D> -> unit
  ) =
  let buffers = RendererBuffers.createEmpty()
  let mutable spriteBatch: SpriteBatch = null
  let mutable rtPool: IRenderTargetPool voption = ValueNone
  let mutable sceneTarget: RenderTarget2D voption = ValueNone
  let mutable shadowAtlas: RenderTarget2D voption = ValueNone
  let mutable occluderBatch: OccluderBatch.State voption = ValueNone
  let mutable billboardBatch: BillboardBatch.State voption = ValueNone
  let mutable lineBatch: LineBatch.State voption = ValueNone
  let mutable primitiveEffect: BasicEffect = null
  let mutable defaultNormalMap: Texture2D = null

  let shadowMinBlend =
    new BlendState(
      ColorSourceBlend = Blend.One,
      ColorDestinationBlend = Blend.One,
      ColorBlendFunction = BlendFunction.Min,
      AlphaSourceBlend = Blend.One,
      AlphaDestinationBlend = Blend.One,
      AlphaBlendFunction = BlendFunction.Min
    )

  let buffer = RenderBuffer<RenderCmd2D>()

  let ensureDefaultNormalMap() =
    if isNull defaultNormalMap then
      defaultNormalMap <- new Texture2D(game.GraphicsDevice, 1, 1)
      defaultNormalMap.SetData [| Color(128, 128, 255, 255) |]

    defaultNormalMap

  interface IDisposable with
    member _.Dispose() =
      if not(isNull spriteBatch) then
        spriteBatch.Dispose()

      if not(isNull defaultNormalMap) then
        defaultNormalMap.Dispose()

      RendererBuffers.dispose buffers
      occluderBatch |> ValueOption.iter OccluderBatch.dispose
      billboardBatch |> ValueOption.iter BillboardBatch.dispose
      lineBatch |> ValueOption.iter LineBatch.dispose

      if not(isNull primitiveEffect) then
        primitiveEffect.Dispose()
        shadowMinBlend.Dispose()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, gameTime: GameTime) =
      if isNull spriteBatch then
        spriteBatch <- new SpriteBatch(ctx.GraphicsDevice)

      buffer.Clear()
      view(ctx, model, buffer)

      if config.SortCommands then
        buffer.Sort()

      if rtPool.IsNone then
        rtPool <- ValueSome(RenderTargetPool.create ctx.GraphicsDevice)

      buffers.PointLights.Clear()
      buffers.DirectionalLights.Clear()
      buffers.Occluders.Clear()
      buffers.LightingPrepared <- false

      let mutable capturedCamera, needsBillboard, needsLine =
        ValueNone, false, false

      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item i

        match cmd with
        | AddPointLight l -> buffers.PointLights.Add l
        | AddDirectionalLight l -> buffers.DirectionalLights.Add l
        | AddOccluder o -> buffers.Occluders.Add o
        | SetCamera c when capturedCamera.IsNone ->
          capturedCamera <- ValueSome c
        | DrawParticles _ -> needsBillboard <- true
        | DrawLine2D _
        | DrawRect2D _
        | DrawCircle2D _ -> needsLine <- true
        | _ -> ()

      if
        config.Lighting.IsSome
        && config.Lighting.Value.Shadows.IsSome
        && config.ShaderOverrides.ContainsKey ShaderBase2D.ShadowCaster
      then
        if occluderBatch.IsNone then
          occluderBatch <- ValueSome(OccluderBatch.create ctx.GraphicsDevice)

        RendererBuffers.ensureCapacity
          buffers.PointLights.Count
          &buffers.ShadowIndicesPoint

        RendererBuffers.ensureCapacity
          buffers.DirectionalLights.Count
          &buffers.ShadowIndicesDirectional

        RendererBuffers.ensureCapacity
          buffers.DirectionalLights.Count
          &buffers.DirShadowOrigins

        let cam =
          capturedCamera
          |> ValueOption.defaultValue {
            View = Matrix.Identity
            Projection = Matrix.Identity
          }

        let lEnv: Lighting2DInternalLogic.LightingEnvironment = {
          Device = ctx.GraphicsDevice
          Pool = rtPool.Value
          Camera = cam
          ViewMatrix = cam.View
        }

        let lScene: Lighting2DInternalLogic.LightingScene = {
          PointLights = buffers.PointLights
          DirectionalLights = buffers.DirectionalLights
          Occluders = buffers.Occluders
        }

        let lBufs: Lighting2DInternalLogic.ShadowBuffers = {
          IndicesPoint = buffers.ShadowIndicesPoint
          IndicesDirectional = buffers.ShadowIndicesDirectional
          OriginsDirectional = buffers.DirShadowOrigins
          Casters = buffers.ShadowCasters
        }

        let sCfg = config.Lighting.Value.Shadows.Value

        Lighting2DInternalLogic.Shadows.render
          &lEnv
          &sCfg
          config.ShaderOverrides.[ShaderBase2D.ShadowCaster]
          shadowMinBlend
          occluderBatch.Value
          &lScene
          &lBufs
          &shadowAtlas

      if needsBillboard && billboardBatch.IsNone then
        billboardBatch <- ValueSome(BillboardBatch.create ctx.GraphicsDevice)

      if needsLine then
        if lineBatch.IsNone then
          lineBatch <- ValueSome(LineBatch.create ctx.GraphicsDevice)

        if isNull primitiveEffect then
          primitiveEffect <- new BasicEffect(ctx.GraphicsDevice)
          primitiveEffect.LightingEnabled <- false
          primitiveEffect.TextureEnabled <- false
          primitiveEffect.VertexColorEnabled <- true

      let services = {
        Device = ctx.GraphicsDevice
        Pool = rtPool.Value
        SpriteBatch = spriteBatch
        DefaultNormalMap = ensureDefaultNormalMap()
      }

      let env: RendererEnvironment = {
        Services = services
        Game = game
        OccluderBatch = occluderBatch
        BillboardBatch = billboardBatch
        LineBatch = lineBatch
        PrimitiveEffect = primitiveEffect
        ShadowMinBlend = shadowMinBlend
        Buffers = buffers
      }

      if config.PostProcess.IsSome then
        let rt =
          services.Pool.Acquire {
            Width = services.Device.PresentationParameters.BackBufferWidth
            Height = services.Device.PresentationParameters.BackBufferHeight
            Format = SurfaceFormat.Color
            DepthFormat = DepthFormat.None
          }

        services.Device.SetRenderTarget rt
        services.Device.Clear(Color.Transparent)
        sceneTarget <- ValueSome rt
      else
        services.Device.SetRenderTarget null
        sceneTarget <- ValueNone

      config.ClearColor |> ValueOption.iter services.Device.Clear

      let mutable state = {
        Effect =
          if
            config.Lighting.IsSome
            && config.ShaderOverrides.ContainsKey ShaderBase2D.LitSprite
          then
            config.ShaderOverrides.[ShaderBase2D.LitSprite]
          else
            config.Effect
        Transform = config.TransformMatrix |> ValueOption.toNullable
        Camera = {
          View = Matrix.Identity
          Projection =
            Matrix.CreateOrthographicOffCenter(
              0f,
              float32 services.Device.PresentationParameters.BackBufferWidth,
              float32 services.Device.PresentationParameters.BackBufferHeight,
              0f,
              0f,
              1f
            )
        }
        ViewMatrix = Matrix.Identity
        NormalMap = null
        SortMode = config.SortMode
        Blend = config.BlendState
        Sampler = config.SamplerState
        DepthStencil = config.DepthStencilState
        Rasterizer = config.RasterizerState
        IsBatching = false
      }

      CommandDispatcher.beginBatch &env &state config.Lighting shadowAtlas

      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item i in
        CommandDispatcher.execute &env &state config.Lighting shadowAtlas &cmd

      if state.IsBatching then
        CommandDispatcher.endBatch &env &state

      match sceneTarget with
      | ValueSome sceneRt ->
        let ppCfg = config.PostProcess.Value

        let final = PostProcessPipeline.apply &env &ppCfg sceneRt gameTime

        services.Device.SetRenderTarget null

        services.SpriteBatch.Begin(
          SpriteSortMode.Immediate,
          config.FinalBlendState,
          SamplerState.LinearClamp,
          null,
          null,
          null
        )

        services.SpriteBatch.Draw(
          final,
          services.Device.Viewport.Bounds,
          Color.White
        )

        services.SpriteBatch.End()
      | ValueNone -> ()

      services.Pool.ReleaseAll()

module Batch2DRenderer =
  let inline create<'Model>
    (game: Game)
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit)
    : IRenderer<'Model> =
    new Batch2DRenderer<'Model>(
      game,
      Batch2DConfig.defaults,
      fun (ctx, model, buffer) -> view ctx model buffer
    )

  let inline createWithConfig<'Model>
    (game: Game)
    (config: Batch2DConfig)
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit)
    : IRenderer<'Model> =
    new Batch2DRenderer<'Model>(
      game,
      config,
      fun (ctx, model, buffer) -> view ctx model buffer
    )

// ============================================================================
// Builder & DSL
// ============================================================================

[<Struct>]
type Draw2DBuilder = {
  Texture: Texture2D
  Dest: Rectangle
  Source: Nullable<Rectangle>
  Color: Color
  Rotation: float32
  Origin: Vector2
  Effects: SpriteEffects
  Depth: float32
  Layer: int<RenderLayer>
}

module Draw2D =
  let sprite tex dest = {
    Texture = tex
    Dest = dest
    Source = Nullable()
    Color = Color.White
    Rotation = 0.0f
    Origin = Vector2.Zero
    Effects = SpriteEffects.None
    Depth = 0.0f
    Layer = 0<RenderLayer>
  }

  let withSource (src: Rectangle) (b: Draw2DBuilder) = {
    b with
        Source = Nullable src
  }

  let withColor col (b: Draw2DBuilder) = { b with Color = col }
  let atLayer layer (b: Draw2DBuilder) = { b with Layer = layer }

  let submit (buffer: RenderBuffer<RenderCmd2D>) (b: Draw2DBuilder) =
    buffer.Add(
      b.Layer,
      DrawSprite {
        Texture = b.Texture
        NormalMap = ValueNone
        DestX = b.Dest.X
        DestY = b.Dest.Y
        Width = b.Dest.Width
        Height = b.Dest.Height
        SourceRect = b.Source |> ValueOption.ofNullable
        Color = b.Color
        Rotation = b.Rotation
        Origin = b.Origin
        Effects = b.Effects
        Depth = b.Depth
        Layer = b.Layer
      }
    )

  let camera
    (cam: Camera)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetCamera cam)

  let viewport
    (vp: Viewport)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetViewport vp)

  let clear
    (color: Color voption)
    (clearDepth: bool)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, ClearTarget(color, clearDepth))

  let effect
    (effect: Effect voption)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetEffect effect)

  let blendState
    (blendState: BlendState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetBlendState blendState)

  let samplerState
    (samplerState: SamplerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetSamplerState samplerState)

  let depthStencilState
    (depthStencilState: DepthStencilState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetDepthStencilState depthStencilState)

  let rasterizerState
    (rasterizerState: RasterizerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetRasterizerState rasterizerState)

  let custom
    (draw: GameContext -> unit)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, DrawCustom draw)

module DSL =
  open System.Runtime.CompilerServices

  module Sprite =
    let empty: SpriteState = {
      Texture = null
      NormalMap = ValueNone
      DestX = 0
      DestY = 0
      Width = 0
      Height = 0
      SourceRect = ValueNone
      Color = Color.White
      Rotation = 0f
      Origin = Vector2.Zero
      Effects = SpriteEffects.None
      Depth = 0f
      Layer = 0<RenderLayer>
    }

    let inline fromTexture(tex: Texture2D) : SpriteState = {
      empty with
          Texture = tex
          Width = tex.Width
          Height = tex.Height
    }

    let inline at x y (s: SpriteState) = { s with DestX = x; DestY = y }
    let inline size w h (s: SpriteState) = { s with Width = w; Height = h }
    let inline color c (s: SpriteState) = { s with Color = c }
    let inline layer l (s: SpriteState) = { s with Layer = l }

    let inline sourceRect r (s: SpriteState) = {
      s with
          SourceRect = ValueSome r
    }

    let inline rotatedBy r (s: SpriteState) = { s with Rotation = r }
    let inline depth d (s: SpriteState) = { s with Depth = d }
    let inline origin o (s: SpriteState) = { s with Origin = o }

    let inline centered(s: SpriteState) = {
      s with
          Origin = Vector2(float32 s.Width / 2f, float32 s.Height / 2f)
    }

    let inline flippedH(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipHorizontally
    }

    let inline flippedV(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipVertically
    }

  type SpriteBuilder() =
    member inline _.Yield(_: unit) : SpriteState = Sprite.empty

    [<CustomOperation("texture")>]
    member inline _.Texture(s: SpriteState, tex: Texture2D) = {
      s with
          Texture = tex
          Width = tex.Width
          Height = tex.Height
    }

    [<CustomOperation("normalMap")>]
    member inline _.NormalMap(s: SpriteState, tex: Texture2D) = {
      s with
          NormalMap = ValueSome tex
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, x: int, y: int) = {
      s with
          DestX = x
          DestY = y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, v: Vector2) = {
      s with
          DestX = int v.X
          DestY = int v.Y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, x: float32, y: float32) = {
      s with
          DestX = int x
          DestY = int y
    }

    [<CustomOperation("size")>]
    member inline _.Size(s: SpriteState, w: int, h: int) = {
      s with
          Width = w
          Height = h
    }

    [<CustomOperation("size")>]
    member inline _.Size(s: SpriteState, w: float32, h: float32) = {
      s with
          Width = int w
          Height = int h
    }

    [<CustomOperation("sourceRect")>]
    member inline _.SourceRect(s: SpriteState, r: Rectangle) = {
      s with
          SourceRect = ValueSome r
    }

    [<CustomOperation("color")>]
    member inline _.Color(s: SpriteState, c: Color) = { s with Color = c }

    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedBy(s: SpriteState, radians: float32) = {
      s with
          Rotation = radians
    }

    [<CustomOperation("centered")>]
    member inline _.Centered(s: SpriteState) = {
      s with
          Origin = Vector2(float32 s.Width / 2f, float32 s.Height / 2f)
    }

    [<CustomOperation("origin")>]
    member inline _.Origin(s: SpriteState, o: Vector2) = { s with Origin = o }

    [<CustomOperation("flippedH")>]
    member inline _.FlippedH(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipHorizontally
    }

    [<CustomOperation("flippedV")>]
    member inline _.FlippedV(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipVertically
    }

    [<CustomOperation("depth")>]
    member inline _.Depth(s: SpriteState, d: float32) = { s with Depth = d }

    [<CustomOperation("layer")>]
    member inline _.Layer(s: SpriteState, l: int<RenderLayer>) = {
      s with
          Layer = l
    }

    member inline _.Run(s: SpriteState) : SpriteState = s

  module Text =
    let empty: TextState = {
      Font = null
      Text = ""
      DestX = 0
      DestY = 0
      Color = Color.White
      Scale = 1f
      Rotation = 0f
      Origin = Vector2.Zero
      Effects = SpriteEffects.None
      Layer = 0<RenderLayer>
    }

    let inline at x y (s: TextState) = { s with DestX = x; DestY = y }
    let inline color c (s: TextState) = { s with Color = c }
    let inline scale sc (s: TextState) = { s with Scale = sc }
    let inline layer l (s: TextState) = { s with Layer = l }

  type TextBuilder() =
    member inline _.Yield(_: unit) : TextState = Text.empty

    [<CustomOperation("font")>]
    member inline _.Font(s: TextState, f: SpriteFont) = { s with Font = f }

    [<CustomOperation("content")>]
    member inline _.Content(s: TextState, t: string) = { s with Text = t }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, x: float32, y: float32) = {
      s with
          DestX = int x
          DestY = int y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, x: int, y: int) = {
      s with
          DestX = x
          DestY = y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, v: Vector2) = {
      s with
          DestX = int v.X
          DestY = int v.Y
    }

    [<CustomOperation("color")>]
    member inline _.Color(s: TextState, c: Color) = { s with Color = c }

    [<CustomOperation("scale")>]
    member inline _.Scale(s: TextState, sc: float32) = { s with Scale = sc }

    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedBy(s: TextState, radians: float32) = {
      s with
          Rotation = radians
    }

    [<CustomOperation("origin")>]
    member inline _.Origin(s: TextState, o: Vector2) = { s with Origin = o }

    [<CustomOperation("layer")>]
    member inline _.Layer(s: TextState, l: int<RenderLayer>) = {
      s with
          Layer = l
    }

    member inline _.Run(s: TextState) : TextState = s

  [<AutoOpen>]
  module RB2DExtensions =
    [<Extension>]
    type RenderBuffer2DExtensions =
      [<Extension>]
      static member inline Sprite
        (this: RenderBuffer<RenderCmd2D>, s: SpriteState)
        =
        this.Add(s.Layer, DrawSprite s)

        this

      [<Extension>]
      static member inline Sprite
        (
          this: RenderBuffer<RenderCmd2D>,
          tex: Texture2D,
          x: int,
          y: int,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(
          defaultValueArg layer 0<RenderLayer>,
          DrawSprite {
            Sprite.empty with
                Texture = tex
                DestX = x
                DestY = y
                Width = tex.Width
                Height = tex.Height
          }
        )

        this

      [<Extension>]
      static member inline Text(this: RenderBuffer<RenderCmd2D>, t: TextState) =
        if not(isNull t.Font) then
          this.Add(t.Layer, DrawText t)

        this

      [<Extension>]
      static member inline Text
        (
          this: RenderBuffer<RenderCmd2D>,
          font: SpriteFont,
          text: string,
          x: int,
          y: int,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(
          defaultValueArg layer 0<RenderLayer>,
          DrawText {
            Text.empty with
                Font = font
                Text = text
                DestX = x
                DestY = y
          }
        )

        this

      [<Extension>]
      static member inline Camera
        (
          this: RenderBuffer<RenderCmd2D>,
          cam: Camera,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, SetCamera cam)
        this

      [<Extension>]
      static member inline Clear
        (
          this: RenderBuffer<RenderCmd2D>,
          color: Color,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(
          defaultValueArg layer 0<RenderLayer>,
          ClearTarget(ValueSome color, false)
        )

        this

      [<Extension>]
      static member inline BlendState
        (
          this: RenderBuffer<RenderCmd2D>,
          bs: BlendState,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, SetBlendState bs)

        this

      [<Extension>]
      static member inline Effect
        (
          this: RenderBuffer<RenderCmd2D>,
          effect: Effect,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(
          defaultValueArg layer 0<RenderLayer>,
          SetEffect(ValueSome effect)
        )

        this

      [<Extension>]
      static member inline Lighting
        (
          this: RenderBuffer<RenderCmd2D>,
          lighting: LightingState2D,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, SetLighting lighting)

        this

      [<Extension>]
      static member inline AddLighting
        (
          this: RenderBuffer<RenderCmd2D>,
          ambient: AmbientLight2D,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, AddLighting ambient)

        this

      [<Extension>]
      static member inline PointLight
        (
          this: RenderBuffer<RenderCmd2D>,
          light: PointLight2D,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, AddPointLight light)

        this

      [<Extension>]
      static member inline DirectionalLight
        (
          this: RenderBuffer<RenderCmd2D>,
          light: DirectionalLight2D,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(
          defaultValueArg layer 0<RenderLayer>,
          AddDirectionalLight light
        )

        this

      [<Extension>]
      static member inline Occluder
        (
          this: RenderBuffer<RenderCmd2D>,
          occluder: Occluder2D,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        this.Add(defaultValueArg layer 0<RenderLayer>, AddOccluder occluder)

        this

      [<Extension>]
      static member inline Particles
        (
          this: RenderBuffer<RenderCmd2D>,
          texture: Texture2D,
          effect: Effect,
          particles: Particle2DState[],
          count: int,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        let l = defaultValueArg layer 0<RenderLayer>

        this.Add(
          l,
          DrawParticles {
            Particles = particles
            Count = count
            Texture = texture
            Effect = effect
            ParticlesLayer = l
          }
        )

        this

      [<Extension>]
      static member inline Line
        (
          this: RenderBuffer<RenderCmd2D>,
          p1: Vector2,
          p2: Vector2,
          color: Color,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        let l = defaultValueArg layer 0<RenderLayer>

        this.Add(
          l,
          DrawLine2D {
            P1 = p1
            P2 = p2
            LineColor = color
            LineLayer = l
          }
        )

        this

      [<Extension>]
      static member inline Rect
        (
          this: RenderBuffer<RenderCmd2D>,
          rect: Rectangle,
          color: Color,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        let l = defaultValueArg layer 0<RenderLayer>

        this.Add(
          l,
          DrawRect2D {
            Rect = rect
            RectColor = color
            RectLayer = l
          }
        )

        this

      [<Extension>]
      static member inline Circle
        (
          this: RenderBuffer<RenderCmd2D>,
          center: Vector2,
          radius: float32,
          color: Color,
          [<Struct>] ?segments: int,
          [<Struct>] ?layer: int<RenderLayer>
        ) =
        let l = defaultValueArg layer 0<RenderLayer>

        this.Add(
          l,
          DrawCircle2D {
            Center = center
            Radius = radius
            Segments = defaultValueArg segments 16
            CircleColor = color
            CircleLayer = l
          }
        )

        this

      [<Extension>]
      static member inline Submit(this: RenderBuffer<RenderCmd2D>) = ()

    let sprite = SpriteBuilder()
    let text = TextBuilder()

  module Buffer2D =
    let inline sprite s (buffer: RenderBuffer<RenderCmd2D>) = buffer.Sprite(s)
    let inline text t (buffer: RenderBuffer<RenderCmd2D>) = buffer.Text(t)

    let inline camera cam (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Camera(cam)

    let inline clear color (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Clear(color)

    let inline blendState bs (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.BlendState(bs)

    let inline effect fx (buffer: RenderBuffer<RenderCmd2D>) = buffer.Effect(fx)

    let inline lighting l (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Lighting(l)

    let inline addLighting ambient (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.AddLighting(ambient)

    let inline pointLight l (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.PointLight(l)

    let inline directionalLight l (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.DirectionalLight(l)

    let inline occluder o (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Occluder(o)

    let inline particles
      texture
      effect
      particlesArr
      count
      (buffer: RenderBuffer<RenderCmd2D>)
      =
      buffer.Particles(texture, effect, particlesArr, count)

    let inline line p1 p2 color (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Line(p1, p2, color)

    let inline rect r color (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Rect(r, color)

    let inline circle center radius color (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Circle(center, radius, color)

    let inline submit(buffer: RenderBuffer<RenderCmd2D>) = buffer.Submit()
