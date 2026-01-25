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

// ============================================================================
// 2D Lighting System (Phase 3)
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
[<StructLayout(LayoutKind.Sequential)>]
type VertexPosition2D = { Position: Vector2 }

/// <summary>Quality level for 2D soft shadows.</summary>
type SoftShadowQuality2D =
  | None = 0
  | Low = 1
  | Medium = 3
  | High = 5

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

/// <summary>Batcher for rendering 2D shadow occluders as line segments.</summary>
module OccluderBatch =

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

/// <summary>Legacy struct for text draw command parameters.</summary>
[<Struct>]
type TextDrawCmd = {
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
/// <remarks>
/// Users provide custom effects via <see cref="T:Mibo.Elmish.Graphics2D.Batch2DConfig"/>.ShaderOverrides,
/// and Mibo selects which effect to use for each rendering stage.
/// </remarks>
[<Struct>]
type ShaderBase2D =
  | LitSprite
  | ShadowCaster
  | PostProcess

/// <summary>A 2D render command.</summary>
/// <remarks>These commands are queued to a <see cref="T:Mibo.Elmish.RenderBuffer`1"/> and executed by <see cref="T:Mibo.Elmish.Graphics2D.Batch2DRenderer`1"/>.</remarks>
[<Struct>]
type RenderCmd2D =
  /// Set viewport for multi-camera rendering (split-screen, minimaps, etc).
  | SetViewport of viewport: Viewport

  /// Clear the render target. Use between cameras in multi-camera setups.
  | ClearTarget of clearColor: Color voption * clearDepth: bool

  /// Changes the camera transform for subsequent draws.
  | SetCamera of camera: Camera

  /// Set the SpriteBatch effect for subsequent draws.
  ///
  /// ValueNone means "use the renderer's configured default".
  | SetEffect of effect: Effect voption

  /// Set SpriteBatch blend state for subsequent draws.
  | SetBlendState of blendState: BlendState

  /// Set SpriteBatch sampler state for subsequent draws.
  | SetSamplerState of samplerState: SamplerState

  /// Set SpriteBatch depth-stencil state for subsequent draws.
  | SetDepthStencilState of depthStencilState: DepthStencilState

  /// Set SpriteBatch rasterizer state for subsequent draws.
  | SetRasterizerState of rasterizerState: RasterizerState

  /// Escape hatch: run an arbitrary draw function.
  ///
  /// The function is invoked outside of SpriteBatch (SpriteBatch is ended before calling it).
  | DrawCustom of draw: (GameContext -> unit)

  /// Draws a textured quad (legacy).
  | DrawTexture of
    texture: Texture2D *
    dest: Rectangle *
    source: Nullable<Rectangle> *
    color: Color *
    rotation: float32 *
    origin: Vector2 *
    effects: SpriteEffects *
    depth: float32

  /// Draws a textured quad via SpriteState.
  | DrawSprite of sprite: SpriteState

  /// Draws text using a SpriteFont.
  | DrawText of text: TextState

  /// Draws text (legacy).
  | DrawTextLegacy of textCmd: TextDrawCmd

  // --- Phase 3 Lighting Commands ---

  /// Set the overall lighting state (tiered: overrides ambient, keeps accumulated lights).
  | SetLighting of lightingState: LightingState2D

  /// Add to lighting state (tiered: ambient only, keeps accumulated lights).
  | AddLighting of ambient: AmbientLight2D

  /// Add a point light to the current frame.
  | AddPointLight of pointLightVal: PointLight2D

  /// Add a directional light to the current frame.
  | AddDirectionalLight of directionalLightVal: DirectionalLight2D

  /// Add an occluder for shadows.
  | AddOccluder of occluderVal: Occluder2D

// ============================================================================
// Post-Processing Configuration (Phase 2)
// ============================================================================

/// <summary>Configuration for vignette effect.</summary>
[<Struct>]
type VignetteConfig = {
  /// The shader effect to use.
  Effect: Effect
  /// Radius of the clear center area. Typical: 0.5f - 0.8f
  Radius: float32
  /// Softness of the edge falloff. Typical: 0.2f - 0.5f
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
  /// Effect that extracts bright areas.
  ExtractEffect: Effect
  /// Effect that performs the blur pass.
  BlurEffect: Effect
  /// Effect that composites bloom with scene.
  CompositeEffect: Effect
  /// Minimum brightness for bloom contribution. Typical: 0.7f - 1.2f
  Threshold: float32
  /// Bloom brightness multiplier. Typical: 0.5f - 2.0f
  Intensity: float32
  /// How far bloom spreads. Typical: 0.5f - 1.0f
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
  /// The shader effect to use.
  Effect: Effect
  /// The 3D LUT texture.
  LutTexture: Texture3D
  /// Size of the LUT (typically 32).
  LutSize: int
  /// Blend between original (0) and graded (1).
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
  /// Custom passes executed after built-in effects.
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
  let withVignette = PostProcess2DConfig.withVignette
  let withBloom = PostProcess2DConfig.withBloom
  let withColorGrade = PostProcess2DConfig.withColorGrade
  let withCustomPasses = PostProcess2DConfig.withCustomPasses

/// <summary>Configuration for <see cref="T:Mibo.Elmish.Graphics2D.Batch2DRenderer`1"/>.</summary>
/// <remarks>These settings configure the *rendering pass* (Clear + SpriteBatch.Begin parameters), not individual sprites (those are controlled by <see cref="T:Mibo.Elmish.Graphics2D.RenderCmd2D"/> / <see cref="T:Mibo.Elmish.Graphics2D.Draw2DBuilder"/>).</remarks>
[<Struct>]
type Batch2DConfig = {
  /// Optional color to clear the screen with before drawing.
  ClearColor: Color voption
  /// Whether to sort the command buffer by `RenderLayer` before issuing draws.
  /// Keep this enabled if you rely on `RenderLayer` for deterministic ordering.
  SortCommands: bool
  /// SpriteBatch sort mode (Deferred, Immediate, etc).
  SortMode: SpriteSortMode
  /// Blend state for sprite drawing.
  BlendState: BlendState
  /// Sampler state for texture filtering.
  SamplerState: SamplerState
  /// Depth stencil state.
  DepthStencilState: DepthStencilState
  /// Rasterizer state.
  RasterizerState: RasterizerState
  /// Optional shader effect for all sprites.
  Effect: Effect
  /// Global transform matrix for the batch.
  /// Note: If using `SetCamera` commands, this initial matrix might be overridden during the pass.
  TransformMatrix: Matrix voption
  /// Post-processing configuration.
  PostProcess: PostProcess2DConfig voption
  /// Lighting configuration.
  Lighting: Lighting2DConfig voption
  /// Shader overrides for specific rendering stages.
  /// Users provide their own effects; Mibo selects which to use for each stage.
  ShaderOverrides: Dictionary<ShaderBase2D, Effect>
  /// Blend state for the final blit to screen (useful for layering/overlays).
  /// Defaults to Opaque to match standard behavior.
  FinalBlendState: BlendState
}

module Batch2DConfig =

  /// Sensible defaults for a typical 2D game.
  /// Matches the current historical behavior of this renderer (clears CornflowerBlue).
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

  let withClearColor (color: Color voption) (cfg: Batch2DConfig) = {
    cfg with
        ClearColor = color
  }

  let withSortCommands (sort: bool) (cfg: Batch2DConfig) = {
    cfg with
        SortCommands = sort
  }

  let withSortMode (mode: SpriteSortMode) (cfg: Batch2DConfig) = {
    cfg with
        SortMode = mode
  }

  let withBlendState (state: BlendState) (cfg: Batch2DConfig) = {
    cfg with
        BlendState = state
  }

  let withSamplerState (state: SamplerState) (cfg: Batch2DConfig) = {
    cfg with
        SamplerState = state
  }

  let withDepthStencilState (state: DepthStencilState) (cfg: Batch2DConfig) = {
    cfg with
        DepthStencilState = state
  }

  let withRasterizerState (state: RasterizerState) (cfg: Batch2DConfig) = {
    cfg with
        RasterizerState = state
  }

  let withEffect (effect: Effect) (cfg: Batch2DConfig) = {
    cfg with
        Effect = effect
  }

  let withTransform (matrix: Matrix voption) (cfg: Batch2DConfig) = {
    cfg with
        TransformMatrix = matrix
  }

  let withPostProcess (pp: PostProcess2DConfig) (cfg: Batch2DConfig) = {
    cfg with
        PostProcess = ValueSome pp
  }

  let withLighting (lighting: Lighting2DConfig) (cfg: Batch2DConfig) = {
    cfg with
        Lighting = ValueSome lighting
  }

  let withShader
    (baseType: ShaderBase2D)
    (effect: Effect)
    (cfg: Batch2DConfig)
    =
    cfg.ShaderOverrides.Add(baseType, effect)
    cfg

  let withLitSprite (effect: Effect) (cfg: Batch2DConfig) =
    withShader ShaderBase2D.LitSprite effect cfg

  let withShadowCaster (effect: Effect) (cfg: Batch2DConfig) =
    withShader ShaderBase2D.ShadowCaster effect cfg

  let withFinalBlendState (state: BlendState) (cfg: Batch2DConfig) = {
    cfg with
        FinalBlendState = state
  }


module Lighting2DInternal =
  [<Struct>]
  type LightBinResults = {
    TileData: int[]
    TileCounts: int[]
    TilesX: int
    TilesY: int
  }

  let binPointLights
    (device: GraphicsDevice)
    (tileSize: int)
    (maxLightsPerTile: int)
    (lights: PointLight2D[])
    (lightCount: int)
    : LightBinResults =
    let viewport = device.Viewport
    let tw = (viewport.Width + tileSize - 1) / tileSize
    let th = (viewport.Height + tileSize - 1) / tileSize

    let tileCounts = Array.zeroCreate<int>(tw * th)
    // Initialize with -1 (no light marker) instead of 0 (which is a valid light index!)
    let tileData = Array.create (tw * th * maxLightsPerTile) -1

    for i = 0 to lightCount - 1 do
      let l = lights.[i]
      let r = l.Radius
      let minX = max 0 (int(l.Position.X - r) / tileSize)
      let maxX = min (tw - 1) (int(l.Position.X + r) / tileSize)
      let minY = max 0 (int(l.Position.Y - r) / tileSize)
      let maxY = min (th - 1) (int(l.Position.Y + r) / tileSize)

      for ty = minY to maxY do
        for tx = minX to maxX do
          let tileIdx = ty * tw + tx
          let count = tileCounts.[tileIdx]

          if count < maxLightsPerTile then
            tileData.[tileIdx * maxLightsPerTile + count] <- i
            tileCounts.[tileIdx] <- count + 1

    {
      TileData = tileData
      TileCounts = tileCounts
      TilesX = tw
      TilesY = th
    }


/// <summary>Standard 2D Renderer using <see cref="T:Microsoft.Xna.Framework.Graphics.SpriteBatch"/>.</summary>
type Batch2DRenderer<'Model>
  (
    game: Game,
    config: Batch2DConfig,
    [<InlineIfLambda>] view:
      GameContext * 'Model * RenderBuffer<RenderCmd2D> -> unit
  ) =
  let mutable spriteBatch: SpriteBatch = null
  let mutable rtPool: IRenderTargetPool voption = ValueNone
  let mutable sceneTarget: RenderTarget2D voption = ValueNone
  let mutable lightTileDataTex: Texture2D = null
  let mutable lightTileDataBuffer: float32[] = Array.empty
  let mutable shadowAtlas: RenderTarget2D voption = ValueNone
  let mutable shadowIndicesPoint: int[] = Array.zeroCreate 16
  let mutable shadowIndicesDirectional: int[] = Array.zeroCreate 8
  let mutable occluderBatch: OccluderBatch.State option = None
  let mutable defaultNormalMap: Texture2D = null
  let mutable currentNormalMap: Texture2D = null

  // Robust blend state for shadows: the minimum distance wins
  let shadowMinBlend =
    new BlendState(
      ColorSourceBlend = Blend.One,
      ColorDestinationBlend = Blend.One,
      ColorBlendFunction = BlendFunction.Min,
      AlphaSourceBlend = Blend.One,
      AlphaDestinationBlend = Blend.One,
      AlphaBlendFunction = BlendFunction.Min
    )

  // Reusable buffers for light data to avoid per-frame allocations (GC pressure)
  let mutable pointPositions: Vector2[] = Array.empty
  let mutable pointColors: Vector4[] = Array.empty
  let mutable pointRadii: float32[] = Array.empty
  let mutable pointFalloffs: float32[] = Array.empty
  let mutable dirDirections: Vector2[] = Array.empty
  let mutable dirColors: Vector4[] = Array.empty
  let mutable dirShadowOrigins: Vector2[] = Array.empty
  let mutable pointShadowIndicesBuf: float32[] = Array.empty
  let mutable dirShadowIndicesBuf: float32[] = Array.empty
  let mutable screenSpaceLightsBuf: PointLight2D[] = Array.empty

  let buffer = RenderBuffer<RenderCmd2D>()

  let ensureCapacity (needed: int) (current: 'T[] byref) =
    if current.Length < needed then
      current <- Array.zeroCreate(max (current.Length * 2) needed)

  let ensureDefaultNormalMap() =
    if isNull defaultNormalMap then
      defaultNormalMap <- new Texture2D(game.GraphicsDevice, 1, 1)
      defaultNormalMap.SetData [| Color(128, 128, 255, 255) |]

    defaultNormalMap

  interface IDisposable with
    member _.Dispose() =
      if not(isNull spriteBatch) then
        spriteBatch.Dispose()
        spriteBatch <- null

      if not(isNull lightTileDataTex) then
        lightTileDataTex.Dispose()
        lightTileDataTex <- null

      if not(isNull defaultNormalMap) then
        defaultNormalMap.Dispose()
        defaultNormalMap <- null

      match occluderBatch with
      | Some s -> OccluderBatch.dispose s
      | None -> ()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, gameTime: GameTime) =
      if isNull spriteBatch then
        spriteBatch <- new SpriteBatch(ctx.GraphicsDevice)

      buffer.Clear()
      view(ctx, model, buffer)

      if config.SortCommands then
        buffer.Sort()

      match rtPool with
      | ValueNone ->
        rtPool <- ValueSome(RenderTargetPool.create ctx.GraphicsDevice)
      | _ -> ()

      // Current SpriteBatch state (mutable via commands)
      let mutable currentSortMode = config.SortMode
      let mutable currentBlend = config.BlendState
      let mutable currentSampler = config.SamplerState
      let mutable currentDepthStencil = config.DepthStencilState
      let mutable currentRasterizer = config.RasterizerState

      let mutable currentEffect =
        if
          config.Lighting.IsSome
          && config.ShaderOverrides.ContainsKey ShaderBase2D.LitSprite
        then
          config.ShaderOverrides.[ShaderBase2D.LitSprite]
        else
          config.Effect
      // Reset tracked state at start of frame
      currentNormalMap <- null

      let mutable currentTransform =
        match config.TransformMatrix with
        | ValueSome m -> Nullable m
        | ValueNone -> Nullable()

      // Lighting tracking (Phase 3)
      let mutable currentLightingState: LightingState2D voption =
        config.Lighting
        |> ValueOption.map(fun l -> {
          Ambient = l.DefaultAmbient
          PointLights = [||]
          DirectionalLights = [||]
        })
        |> ValueOption.defaultValue {
          Ambient = ValueNone
          PointLights = [||]
          DirectionalLights = [||]
        }
        |> ValueSome

      let pointLights = ResizeArray<PointLight2D>()
      let directionalLights = ResizeArray<DirectionalLight2D>()
      let occluders = ResizeArray<Occluder2D>()

      // 1. Initial collection pass (Phase 3) - Collect all lights/occluders for global shadows
      let mutable capturedCamera: Camera voption = ValueNone
      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item i

        match cmd with
        | AddPointLight l -> pointLights.Add l
        | AddDirectionalLight l -> directionalLights.Add l
        | AddOccluder o -> occluders.Add o
        | SetCamera c when capturedCamera.IsNone -> capturedCamera <- ValueSome c
        | _ -> ()

      let hasPostProcess = config.PostProcess.IsSome

      // ======================================================================
      // Shadow Generation Pass (Phase 3.7) - Shared World State
      // ======================================================================
      let shadowsEnabled =
        config.Lighting.IsSome
        && config.Lighting.Value.Shadows.IsSome
        && config.ShaderOverrides.ContainsKey ShaderBase2D.ShadowCaster

      let shadowPass() =
        if shadowsEnabled then
          let shadowCfg = config.Lighting.Value.Shadows.Value
          let device = ctx.GraphicsDevice

          match occluderBatch with
          | None -> occluderBatch <- Some(OccluderBatch.create device)
          | Some _ -> ()

          let shadowCasters = ResizeArray<int * int * float32>()

          for i = 0 to pointLights.Count - 1 do
            let l = pointLights.[i]

            if l.Shadow.IsSome then
              let distSq = Vector2.DistanceSquared(l.Position, Vector2.Zero)
              let priority = l.Intensity / (max 1.0f distSq)
              shadowCasters.Add(0, i, priority)

          for i = 0 to directionalLights.Count - 1 do
            let l = directionalLights.[i]

            if l.Shadow.IsSome then
              let priority = l.Intensity
              shadowCasters.Add(1, i, priority)

          let sortedCasters =
            shadowCasters
            |> Seq.sortByDescending(fun (_, _, p) -> p)
            |> Seq.truncate shadowCfg.MaxShadowLights
            |> Seq.toArray

          if shadowIndicesPoint.Length < pointLights.Count then
            shadowIndicesPoint <-
              Array.create
                (max (shadowIndicesPoint.Length * 2) pointLights.Count)
                -1
          else
            for i = 0 to shadowIndicesPoint.Length - 1 do
              shadowIndicesPoint.[i] <- -1

          if shadowIndicesDirectional.Length < directionalLights.Count then
            shadowIndicesDirectional <-
              Array.create
                (max
                  (shadowIndicesDirectional.Length * 2)
                  directionalLights.Count)
                -1
          else
            for i = 0 to shadowIndicesDirectional.Length - 1 do
              shadowIndicesDirectional.[i] <- -1

          let mutable shadowRow = 0

          // Initialize shadow origins buffer
          ensureCapacity directionalLights.Count &dirShadowOrigins

          // Calculate shadow origin based on camera center
          let shadowOrigin = 
            match capturedCamera with
            | ValueSome cam -> 
                let invView = Matrix.Invert(cam.View)
                let viewport = ctx.GraphicsDevice.Viewport
                // Center of the viewport in world space
                let centerWorld = Vector3.Transform(Vector3(float32 viewport.Width * 0.5f, float32 viewport.Height * 0.5f, 0f), invView)
                // Snap to integer to prevent shimmering
                let snappedX = floor(centerWorld.X)
                let snappedY = floor(centerWorld.Y)
                Vector2(float32 snappedX, float32 snappedY)
            | ValueNone -> Vector2.Zero

          for lightType, lightIdx, _ in sortedCasters do
            if lightType = 0 then
              if lightIdx < shadowIndicesPoint.Length then
                shadowIndicesPoint.[lightIdx] <- shadowRow
            else if lightIdx < shadowIndicesDirectional.Length then
              shadowIndicesDirectional.[lightIdx] <- shadowRow
              // Store origin for this light
              dirShadowOrigins.[lightIdx] <- shadowOrigin

            shadowRow <- shadowRow + 1

          let atlasWidth = shadowCfg.Resolution
          let atlasHeight = shadowCfg.MaxShadowLights

          match shadowAtlas with
          | ValueSome rt when rt.Width = atlasWidth && rt.Height = atlasHeight ->
            ()
          | ValueSome rt ->
            rt.Dispose()

            shadowAtlas <-
              ValueSome(
                rtPool.Value.Acquire {
                  Width = atlasWidth
                  Height = atlasHeight
                  Format = SurfaceFormat.Single
                  DepthFormat = DepthFormat.Depth24
                }
              )
          | ValueNone ->
            shadowAtlas <-
              ValueSome(
                rtPool.Value.Acquire {
                  Width = atlasWidth
                  Height = atlasHeight
                  Format = SurfaceFormat.Single
                  DepthFormat = DepthFormat.Depth24
                }
              )

          let shadowRt = shadowAtlas.Value
          let shadowShader = config.ShaderOverrides.[ShaderBase2D.ShadowCaster]

          device.SetRenderTarget(shadowRt)
          // Clear entire atlas once to White (1.0 = infinitely far)
          device.Clear(Color.White)

          device.DepthStencilState <- DepthStencilState.None
          device.BlendState <- shadowMinBlend

          let viewport = device.Viewport

          for lightType, lightIdx, _ in sortedCasters do
            let row =
              if lightType = 0 then
                shadowIndicesPoint.[lightIdx]
              else
                shadowIndicesDirectional.[lightIdx]

            if row >= 0 then
              if lightType = 0 then
                let pl = pointLights.[lightIdx]
                // Each light gets its own 1px row
                device.Viewport <- Viewport(0, row, atlasWidth, 1, 0f, 1f)

                shadowShader.SafeSetParam("LightPosition", pl.Position)
                shadowShader.SafeSetParam("LightRadius", pl.Radius)
                shadowShader.SafeSetParam("AtlasWidth", float32 atlasWidth)
                shadowShader.SafeSetParam("AtlasHeight", float32 atlasHeight)

                shadowShader.CurrentTechnique.Passes.[0].Apply()

                let batchState = occluderBatch.Value
                OccluderBatch.begin' batchState

                for o in occluders do
                  // Tessellate long occluders to handle polar distortion
                  let segments = 8

                  for s = 0 to segments - 1 do
                    let t1 = float32 s / float32 segments
                    let t2 = float32(s + 1) / float32 segments
                    let p1 = Vector2.Lerp(o.P1, o.P2, t1)
                    let p2 = Vector2.Lerp(o.P1, o.P2, t2)
                    OccluderBatch.addLine p1 p2 o.Height batchState

                OccluderBatch.end' batchState
              else
                let dl = directionalLights.[lightIdx]
                device.Viewport <- Viewport(0, row, atlasWidth, 1, 0f, 1f)

                shadowShader.SafeSetParam("LightDirection", dl.Direction)
                shadowShader.SafeSetParam("LightRadius", -1.0f)
                shadowShader.SafeSetParam("AtlasWidth", float32 atlasWidth)
                shadowShader.SafeSetParam("AtlasHeight", float32 atlasHeight)
                shadowShader.SafeSetParam("ShadowOrigin", dirShadowOrigins.[lightIdx])

                shadowShader.CurrentTechnique.Passes.[0].Apply()

                let batchState = occluderBatch.Value
                OccluderBatch.begin' batchState

                for o in occluders do
                  OccluderBatch.addOccluder o batchState

                OccluderBatch.end' batchState

          device.Viewport <- viewport
          device.SetRenderTarget null

      shadowPass()

      // Current SpriteBatch state (mutable via commands)
      let mutable currentSortMode = config.SortMode
      let mutable currentBlend = config.BlendState
      let mutable currentSampler = config.SamplerState
      let mutable currentDepthStencil = config.DepthStencilState
      let mutable currentRasterizer = config.RasterizerState

      let mutable currentEffect =
        if
          config.Lighting.IsSome
          && config.ShaderOverrides.ContainsKey ShaderBase2D.LitSprite
        then
          config.ShaderOverrides.[ShaderBase2D.LitSprite]
        else
          config.Effect
      // Reset tracked state at start of frame
      currentNormalMap <- null

      let mutable currentTransform =
        match config.TransformMatrix with
        | ValueSome m -> Nullable m
        | ValueNone -> Nullable()

      let mutable activeViewMatrix =
        match config.TransformMatrix with
        | ValueSome m -> m
        | ValueNone -> Matrix.Identity

      // 2D identity camera (used before first SetCamera command)
      let mutable currentCamera = {
        View = Matrix.Identity
        Projection =
          Matrix.CreateOrthographicOffCenter(
            0.0f,
            1280.0f,
            720.0f,
            0.0f,
            0.0f,
            1.0f
          )
      }

      // Lighting tracking (Phase 3)
      let mutable currentLightingState: LightingState2D voption =
        config.Lighting
        |> ValueOption.map(fun l -> {
          Ambient = l.DefaultAmbient
          PointLights = [||]
          DirectionalLights = [||]
        })
        |> ValueOption.defaultValue {
          Ambient = ValueNone
          PointLights = [||]
          DirectionalLights = [||]
        }
        |> ValueSome

      // Helper to update lighting params on an effect (Phase 3.7)
      let updateLighting(fx: Effect, viewMatrix: Matrix) =
        if fx <> null then
          config.Lighting
          |> ValueOption.iter(fun lCfg ->
            currentLightingState
            |> ValueOption.iter(fun state ->
              state.Ambient
              |> ValueOption.iter(fun ambient ->
                fx.SafeSetParam("AmbientColor", ambient.Color)))

            // Transform lights to screen space FOR THIS VIEW (Binning needs Screen Space)
            let pCount = pointLights.Count
            ensureCapacity pCount &screenSpaceLightsBuf

            for i = 0 to pCount - 1 do
              let l = pointLights.[i]
              let screenPos = Vector2.Transform(l.Position, activeViewMatrix)
              screenSpaceLightsBuf.[i] <- { l with Position = screenPos }

            let bin =
              Lighting2DInternal.binPointLights
                ctx.GraphicsDevice
                lCfg.TileSize
                lCfg.MaxLightsPerTile
                screenSpaceLightsBuf
                pCount

            let pCounts = pCount
            ensureCapacity pCounts &pointPositions
            ensureCapacity pCounts &pointColors
            ensureCapacity pCounts &pointRadii
            ensureCapacity pCounts &pointFalloffs

            for i = 0 to pCounts - 1 do
              // Use ORIGINAL World Space lights for the shader
              let l = pointLights.[i]
              pointPositions.[i] <- l.Position
              pointColors.[i] <- l.Color.ToVector4() * l.Intensity
              pointRadii.[i] <- l.Radius
              pointFalloffs.[i] <- l.Falloff

            fx.SafeSetParam("PointLightPositions", pointPositions)
            fx.SafeSetParam("PointLightColors", pointColors)
            fx.SafeSetParam("PointLightRadii", pointRadii)
            fx.SafeSetParam("PointLightFalloffs", pointFalloffs)
            fx.SafeSetParam("PointLightCount", pCounts)

            // Directional lights (no position/radius, just direction)
            let dirCounts = directionalLights.Count
            fx.SafeSetParam("DirectionalLightCount", dirCounts)

            if dirCounts > 0 then
              ensureCapacity dirCounts &dirDirections
              ensureCapacity dirCounts &dirColors

              for i = 0 to dirCounts - 1 do
                let l = directionalLights.[i]

                // Keep directional lights in world space
                dirDirections.[i] <-
                  if l.Direction.LengthSquared() > 0.0001f then
                    Vector2.Normalize(l.Direction)
                  else
                    l.Direction

                dirColors.[i] <- l.Color.ToVector4() * l.Intensity

              fx.SafeSetParam("DirectionalLightDirections", dirDirections)
              fx.SafeSetParam("DirectionalLightColors", dirColors)
              fx.SafeSetParam("DirectionalLightShadowOrigins", dirShadowOrigins)

            // Handle tiered tile data texture
            let requiredBufferSize = bin.TileData.Length

            if
              isNull lightTileDataTex
              || lightTileDataTex.Width <> bin.TileData.Length
            then
              if not(isNull lightTileDataTex) then
                lightTileDataTex.Dispose()

              lightTileDataTex <-
                new Texture2D(
                  ctx.GraphicsDevice,
                  bin.TileData.Length,
                  1,
                  false,
                  SurfaceFormat.Single
                )

              lightTileDataBuffer <- Array.zeroCreate requiredBufferSize

            // Zero-allocation copy (mostly)
            for j = 0 to bin.TileData.Length - 1 do
              lightTileDataBuffer.[j] <- float32 bin.TileData.[j]

            lightTileDataTex.SetData(lightTileDataBuffer)

            fx.SafeSetParam("LightIndexBuffer", lightTileDataTex :> Texture)
            fx.SafeSetParam("TileSize", float32 lCfg.TileSize)
            fx.SafeSetParam("TilesX", float32 bin.TilesX)
            fx.SafeSetParam("MaxLightsPerTile", lCfg.MaxLightsPerTile)

            // Viewport dimensions (for screen-space calculations)
            let vpW = float32 ctx.GraphicsDevice.Viewport.Width
            let vpH = float32 ctx.GraphicsDevice.Viewport.Height
            let viewportSize = Vector2(vpW, vpH)

            fx.SafeSetParam("ViewportSize", viewportSize)
            fx.SafeSetParam("ViewportSizeInv", Vector2(1.0f / vpW, 1.0f / vpH))

            // Camera matrices - shader authors choose coordinate space
            fx.SafeSetParam("ViewMatrix", viewMatrix)
            fx.SafeSetParam("ProjectionMatrix", currentCamera.Projection)
            fx.SafeSetParam("InverseViewMatrix", Matrix.Invert(viewMatrix))

            fx.SafeSetParam(
              "InverseProjectionMatrix",
              Matrix.Invert(currentCamera.Projection)
            )

            fx.SafeSetParam(
              "ViewProjectionMatrix",
              viewMatrix * currentCamera.Projection
            )

            fx.SafeSetParam(
              "LightIndexBufferWidth",
              float32 lightTileDataTex.Width
            )

            fx.SafeSetParam(
              "LightIndexBufferHeight",
              float32 lightTileDataTex.Height
            )


            lCfg.Shadows
            |> ValueOption.iter(fun shadowCfg ->
              shadowAtlas
              |> ValueOption.iter(fun atlas ->
                fx.SafeSetParam("ShadowAtlas", atlas :> Texture)

                fx.SafeSetParam(
                  "ShadowAtlasSize",
                  Vector2(float32 atlas.Width, float32 atlas.Height)
                )

                fx.SafeSetParam("ShadowBias", shadowCfg.ShadowBias)

                // Map to float32 for shader array passing without per-frame allocation
                let pIndicesCount = shadowIndicesPoint.Length
                let dIndicesCount = shadowIndicesDirectional.Length
                ensureCapacity pIndicesCount &pointShadowIndicesBuf
                ensureCapacity dIndicesCount &dirShadowIndicesBuf

                for i = 0 to pIndicesCount - 1 do
                  pointShadowIndicesBuf.[i] <- float32 shadowIndicesPoint.[i]

                for i = 0 to dIndicesCount - 1 do
                  dirShadowIndicesBuf.[i] <-
                    float32 shadowIndicesDirectional.[i]

                fx.SafeSetParam(
                  "PointLightShadowIndices",
                  pointShadowIndicesBuf
                )

                fx.SafeSetParam(
                  "DirectionalLightShadowIndices",
                  dirShadowIndicesBuf
                ))))





      // CRITICAL: Set RenderTarget BEFORE Clear to avoid accumulation trails
      if hasPostProcess then
        rtPool
        |> ValueOption.iter(fun pool ->
          let rt =
            pool.Acquire {
              Width = ctx.GraphicsDevice.PresentationParameters.BackBufferWidth
              Height =
                ctx.GraphicsDevice.PresentationParameters.BackBufferHeight
              Format = SurfaceFormat.Color
              DepthFormat = DepthFormat.None
            }

          ctx.GraphicsDevice.SetRenderTarget rt
          // Mandatory initial clear of the internal scene target to avoid pooled garbage.
          // This does NOT affect the backbuffer yet.
          ctx.GraphicsDevice.Clear(Color.Transparent)
          sceneTarget <- ValueSome rt)
      else
        ctx.GraphicsDevice.SetRenderTarget null
        sceneTarget <- ValueNone

      config.ClearColor |> ValueOption.iter(fun c -> ctx.GraphicsDevice.Clear c)

      let beginBatch() =
        updateLighting(currentEffect, activeViewMatrix)

        // Explicitly set all matrix parameters for maximum compatibility
        if currentEffect <> null then
          currentEffect.SafeSetParam("World", Matrix.Identity)
          currentEffect.SafeSetParam("View", activeViewMatrix)
          currentEffect.SafeSetParam("ViewMatrix", activeViewMatrix)
          currentEffect.SafeSetParam("Projection", currentCamera.Projection)

          currentEffect.SafeSetParam(
            "ProjectionMatrix",
            currentCamera.Projection
          )

        // If using a custom lighting effect, we must pass Identity to SpriteBatch
        // because the shader handles the View transform itself.
        // If we pass currentTransform, SpriteBatch applies it to the vertices first,
        // causing a double-transform (and wrong WorldPos for lighting).
        let transformToUse =
          if currentEffect <> null && config.Lighting.IsSome then
            Nullable Matrix.Identity
          else
            currentTransform

        spriteBatch.Begin(
          currentSortMode,
          currentBlend,
          currentSampler,
          currentDepthStencil,
          currentRasterizer,
          currentEffect,
          transformToUse
        )

      let endBatch() = spriteBatch.End()

      let mutable isBatching = true
      beginBatch()

      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item(i)

        match cmd with
        | SetViewport vp ->
          if isBatching then
            endBatch()
            isBatching <- false

          ctx.GraphicsDevice.Viewport <- vp

          beginBatch()
          isBatching <- true

        | ClearTarget(colorOpt, clearDepth) ->
          if isBatching then
            endBatch()
            isBatching <- false

          match colorOpt, clearDepth with
          | ValueSome c, true ->
            ctx.GraphicsDevice.Clear(
              ClearOptions.Target ||| ClearOptions.DepthBuffer,
              c,
              1.0f,
              0
            )
          | ValueSome c, false ->
            ctx.GraphicsDevice.Clear(ClearOptions.Target, c, 1.0f, 0)
          | ValueNone, true ->
            ctx.GraphicsDevice.Clear(
              ClearOptions.DepthBuffer,
              Color.Black,
              1.0f,
              0
            )
          | ValueNone, false -> ()

          beginBatch()
          isBatching <- true

        | SetCamera cam ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentTransform <- Nullable cam.View
          activeViewMatrix <- cam.View
          currentCamera <- cam
          beginBatch()
          isBatching <- true

        | SetEffect effectOpt ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentEffect <-
            match effectOpt with
            | ValueSome e -> e
            | ValueNone -> config.Effect

          currentNormalMap <- null

          // Force update of lighting params for the new effect immediately
          updateLighting(currentEffect, activeViewMatrix)

          beginBatch()
          isBatching <- true

        | SetBlendState bs ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentBlend <- bs
          beginBatch()
          isBatching <- true

        | SetSamplerState ss ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentSampler <- ss
          beginBatch()
          isBatching <- true

        | SetDepthStencilState ds ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentDepthStencil <- ds
          beginBatch()
          isBatching <- true

        | SetRasterizerState rs ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentRasterizer <- rs
          beginBatch()
          isBatching <- true

        | DrawCustom draw ->
          if isBatching then
            endBatch()
            isBatching <- false

          draw ctx

          beginBatch()
          isBatching <- true

        | DrawTexture(tex, dest, src, color, rot, origin, fx, depth) ->
          let targetNM = ensureDefaultNormalMap()

          if
            isBatching && currentEffect <> null && targetNM <> currentNormalMap
          then
            endBatch()
            isBatching <- false

          if not isBatching then
            beginBatch()
            isBatching <- true

          if currentEffect <> null && targetNM <> currentNormalMap then
            currentEffect.SafeSetParam("NormalMap", targetNM :> Texture)
            currentNormalMap <- targetNM

          if src.HasValue then
            spriteBatch.Draw(
              tex,
              dest,
              src.Value,
              color,
              rot,
              origin,
              fx,
              depth
            )
          else
            spriteBatch.Draw(tex, dest, color)

        | DrawSprite s ->
          let targetNM =
            match s.NormalMap with
            | ValueSome tex -> tex
            | ValueNone -> ensureDefaultNormalMap()

          // If using a custom effect (lighting), we must flush if the normal map changes
          // because SpriteBatch only supports one set of effect parameters per batch.
          if
            isBatching && currentEffect <> null && targetNM <> currentNormalMap
          then
            endBatch()
            isBatching <- false

          if not isBatching then
            beginBatch()
            isBatching <- true

          if currentEffect <> null && targetNM <> currentNormalMap then
            currentEffect.SafeSetParam("NormalMap", targetNM :> Texture)
            currentNormalMap <- targetNM

          let src = s.SourceRect |> ValueOption.toNullable

          spriteBatch.Draw(
            s.Texture,
            Rectangle(s.DestX, s.DestY, s.Width, s.Height),
            src,
            s.Color,
            s.Rotation,
            s.Origin,
            s.Effects,
            s.Depth
          )

        | DrawText s ->
          let targetNM = ensureDefaultNormalMap()

          if
            isBatching && currentEffect <> null && targetNM <> currentNormalMap
          then
            endBatch()
            isBatching <- false

          if not isBatching then
            beginBatch()
            isBatching <- true

          if currentEffect <> null && targetNM <> currentNormalMap then
            currentEffect.SafeSetParam("NormalMap", targetNM :> Texture)
            currentNormalMap <- targetNM

          spriteBatch.DrawString(
            s.Font,
            s.Text,
            Vector2(float32 s.DestX, float32 s.DestY),
            s.Color,
            s.Rotation,
            s.Origin,
            1f,
            s.Effects,
            0f
          )

        | DrawTextLegacy cmd ->
          let targetNM = ensureDefaultNormalMap()

          if
            isBatching && currentEffect <> null && targetNM <> currentNormalMap
          then
            endBatch()
            isBatching <- false

          if not isBatching then
            beginBatch()
            isBatching <- true

          if currentEffect <> null && targetNM <> currentNormalMap then
            currentEffect.SafeSetParam("NormalMap", targetNM :> Texture)
            currentNormalMap <- targetNM

          spriteBatch.DrawString(
            cmd.Font,
            cmd.Text,
            cmd.Position,
            cmd.Color,
            cmd.Rotation,
            cmd.Origin,
            cmd.Scale,
            cmd.Effects,
            cmd.Depth
          )

        | SetLighting l ->
          currentLightingState <-
            ValueSome {
              currentLightingState.Value with
                  Ambient = l.Ambient
            }
        | AddLighting ambient ->
          currentLightingState <-
            ValueSome {
              currentLightingState.Value with
                  Ambient = ValueSome ambient
            }
        | AddPointLight _
        | AddDirectionalLight _
        | AddOccluder _ -> ()

      if isBatching then
        endBatch()

      // ======================================================================
      // Post-Processing Phase (Phase 2)
      // ======================================================================
      match sceneTarget with
      | ValueSome sceneRt ->
        let ppConfig = config.PostProcess.Value
        let device = ctx.GraphicsDevice
        let viewport = device.Viewport
        let mutable currentInput = sceneRt

        let getTempTarget() =
          rtPool.Value.Acquire {
            Width = viewport.Width
            Height = viewport.Height
            Format = SurfaceFormat.Color
            DepthFormat = DepthFormat.None
          }

        let mutable currentOutput = ValueSome(getTempTarget())

        let swap() =
          let tmp = currentInput

          match currentOutput with
          | ValueSome out ->
            currentInput <- out
            currentOutput <- ValueSome tmp
          | ValueNone -> ()

        let drawPass (effect: Effect) (setup: (Effect -> unit) voption) =
          currentOutput
          |> ValueOption.iter(fun out -> device.SetRenderTarget out)

          device.Clear(Color.Transparent)
          setup |> ValueOption.iter(fun s -> s effect)

          spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            null,
            null,
            effect
          )

          spriteBatch.Draw(currentInput, viewport.Bounds, Color.White)
          spriteBatch.End()

          swap()

        // 1. Vignette
        ppConfig.Vignette
        |> ValueOption.iter(fun v ->
          drawPass
            v.Effect
            (ValueSome(fun fx ->
              fx.SafeSetParam("Radius", v.Radius)
              fx.SafeSetParam("Softness", v.Softness))))

        // 2. Bloom (Simplified)
        ppConfig.Bloom
        |> ValueOption.iter(fun b ->
          let sceneInput = currentInput
          // Extract
          drawPass
            b.ExtractEffect
            (ValueSome(fun fx -> fx.SafeSetParam("Threshold", b.Threshold)))
          // Blur
          drawPass
            b.BlurEffect
            (ValueSome(fun fx -> fx.SafeSetParam("Intensity", b.Intensity)))
          // Composite
          currentOutput
          |> ValueOption.iter(fun out -> device.SetRenderTarget out)

          device.Clear(Color.Transparent)

          b.CompositeEffect.SafeSetParam(
            "BloomTexture",
            currentInput :> Texture
          )

          spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            null,
            null,
            b.CompositeEffect
          )

          spriteBatch.Draw(sceneInput, viewport.Bounds, Color.White)
          spriteBatch.End()

          swap())

        // 3. Color Grade
        ppConfig.ColorGrade
        |> ValueOption.iter(fun cg ->
          drawPass
            cg.Effect
            (ValueSome(fun fx ->
              fx.SafeSetParam("LutTexture", cg.LutTexture :> Texture)
              fx.SafeSetParam("LutSize", float32 cg.LutSize)
              fx.SafeSetParam("Blend", cg.Blend))))

        // 4. Custom Passes
        ppConfig.CustomPasses
        |> ValueOption.iter(fun passes ->
          for pass in passes do
            drawPass
              pass.Effect
              (pass.SetupEffect
               |> ValueOption.map(fun s ->
                 fun fx -> s fx gameTime currentInput)))

        // Final Blit to screen
        device.SetRenderTarget null

        spriteBatch.Begin(
          SpriteSortMode.Immediate,
          config.FinalBlendState,
          SamplerState.LinearClamp,
          null,
          null,
          null
        )

        spriteBatch.Draw(currentInput, viewport.Bounds, Color.White)
        spriteBatch.End()

        rtPool |> ValueOption.iter _.ReleaseAll()
      | ValueNone -> ()

module Batch2DRenderer =
  /// <summary>Creates a standard 2D renderer.</summary>
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

  /// <summary>Creates a 2D renderer with custom configuration.</summary>
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


/// <summary>Fluent builder for <see cref="T:Mibo.Elmish.Graphics2D.RenderCmd2D"/>.</summary>
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

/// <summary>Functions for building and submitting 2D draw commands.</summary>
module Draw2D =
  /// <summary>Starts a sprite drawing command.</summary>
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

  let withSource src (b: Draw2DBuilder) = { b with Source = Nullable src }
  let withColor col (b: Draw2DBuilder) = { b with Color = col }
  let atLayer layer (b: Draw2DBuilder) = { b with Layer = layer }

  /// <summary>Submits the draw command to the renderer's buffer.</summary>
  let submit (buffer: RenderBuffer<RenderCmd2D>) (b: Draw2DBuilder) =
    buffer.Add(
      b.Layer,
      DrawTexture(
        b.Texture,
        b.Dest,
        b.Source,
        b.Color,
        b.Rotation,
        b.Origin,
        b.Effects,
        b.Depth
      )
    )

  /// <summary>Submits a camera change command to the buffer.</summary>
  let camera
    (cam: Camera)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetCamera cam)

  /// <summary>Submits a viewport change command to the buffer.</summary>
  let viewport
    (vp: Viewport)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetViewport vp)

  /// <summary>Clear color and/or depth buffer. Useful between cameras in multi-camera setups.</summary>
  let clear
    (color: Color voption)
    (clearDepth: bool)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, ClearTarget(color, clearDepth))

  /// <summary>Set the SpriteBatch effect for subsequent draws.</summary>
  /// <remarks>Use ValueNone to revert to the renderer's configured default.</remarks>
  let effect
    (effect: Effect voption)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetEffect effect)

  /// <summary>Set the SpriteBatch blend state for subsequent draws.</summary>
  let blendState
    (blendState: BlendState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetBlendState blendState)

  /// <summary>Set the SpriteBatch sampler state for subsequent draws.</summary>
  let samplerState
    (samplerState: SamplerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetSamplerState samplerState)

  /// <summary>Set the SpriteBatch depth-stencil state for subsequent draws.</summary>
  let depthStencilState
    (depthStencilState: DepthStencilState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetDepthStencilState depthStencilState)

  /// <summary>Set the SpriteBatch rasterizer state for subsequent draws.</summary>
  let rasterizerState
    (rasterizerState: RasterizerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetRasterizerState rasterizerState)

  /// <summary>Submits a custom drawing command to the buffer.</summary>
  /// <remarks>The SpriteBatch is ended before calling <c>draw</c>, and restarted after.</remarks>
  let custom
    (draw: GameContext -> unit)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, DrawCustom draw)

// ============================================================================
// Phase 1 DSL Module
// ============================================================================

/// <summary>
/// DSL for building 2D sprites and text with computation expressions and pipeline-style functions.
/// </summary>
/// <remarks>
/// This module contains all types, builders, and utilities for the declarative 2D rendering DSL.
/// Use the View2D module for global access to the computation expression builders.
/// </remarks>
module DSL =

  open System.Runtime.CompilerServices

  // --------------------------------------------------------------------------
  // Sprite Types and Builder
  // --------------------------------------------------------------------------

  // Logic remains using top-level structs for performance and CMD consistency.
  module Sprite =
    /// <summary>Creates a default empty sprite state.</summary>
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

    /// <summary>Creates a sprite state from a texture.</summary>
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

  /// <summary>Computation expression builder for sprites.</summary>
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

  // --------------------------------------------------------------------------
  // Text Types and Builder
  // --------------------------------------------------------------------------

  module Text =
    /// <summary>Creates a default empty text state.</summary>
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

  /// <summary>Computation expression builder for text.</summary>
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

  // --------------------------------------------------------------------------
  // Buffer Extensions
  // --------------------------------------------------------------------------

  /// <summary>Fluent extension methods for RenderBuffer.</summary>
  [<Extension>]
  type RenderBuffer2DExtensions =

    [<Extension>]
    static member inline Sprite
      (this: RenderBuffer<RenderCmd2D>, s: SpriteState)
      =
      if not(isNull s.Texture) then
        this.Add(s.Layer, DrawSprite s)

      this

    [<Extension>]
    static member inline Sprite
      (this: RenderBuffer<RenderCmd2D>, tex: Texture2D, x: int, y: int)
      =
      let w, h =
        (if isNull tex then 0 else tex.Width),
        (if isNull tex then 0 else tex.Height)

      this.Add(
        0<RenderLayer>,
        DrawTexture(
          tex,
          Rectangle(x, y, w, h),
          Nullable(),
          Color.White,
          0f,
          Vector2.Zero,
          SpriteEffects.None,
          0f
        )
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
        y: int
      ) =
      this.Add(
        0<RenderLayer>,
        DrawTextLegacy {
          Font = font
          Text = text
          Position = Vector2(float32 x, float32 y)
          Color = Color.White
          Rotation = 0f
          Origin = Vector2.Zero
          Scale = 1f
          Effects = SpriteEffects.None
          Depth = 0f
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
    static member inline Clear(this: RenderBuffer<RenderCmd2D>, color: Color) =
      this.Add(0<RenderLayer>, ClearTarget(ValueSome color, false))
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
      this.Add(defaultValueArg layer 0<RenderLayer>, AddDirectionalLight light)
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
    static member inline Submit(this: RenderBuffer<RenderCmd2D>) = ()

  // --------------------------------------------------------------------------
  // Pipeline-Style Functions
  // --------------------------------------------------------------------------

  /// <summary>Pipeline-style functions for buffer operations.</summary>
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

    let inline submit(buffer: RenderBuffer<RenderCmd2D>) = buffer.Submit()

  // --------------------------------------------------------------------------
  // Global Builder Instances
  // --------------------------------------------------------------------------

  /// <summary>Global computation expression builders.</summary>
  [<AutoOpen>]
  module View2D =
    /// <summary>Builder for sprites. Usage: sprite { texture tex; at x y; ... }</summary>
    let sprite = SpriteBuilder()

    /// <summary>Builder for text. Usage: text { font f; content "Hello"; at x y; ... }</summary>
    let text = TextBuilder()
