# 2D Renderer Enhancement Specification

> Enhance `Graphics2D.fs` with DSL parity, post-processing, and 2D lighting while maintaining zero GC pressure and excellent runtime performance.

---

## Table of Contents

1. [Design Principles](#design-principles)
2. [Phase 1: DSL Parity](#phase-1-dsl-parity)
3. [Phase 2: Post-Processing Pipeline](#phase-2-post-processing-pipeline)
4. [Phase 3: 2D Lighting System](#phase-3-2d-lighting-system)
5. [Phase 4: Advanced Features](#phase-4-advanced-features)
6. [Performance Guidelines](#performance-guidelines)
7. [API Reference](#api-reference)

---

## Design Principles

### Zero-Allocation Hot Path

All rendering operations must avoid heap allocations per frame:

- Use `[<Struct>]` for all builder types and intermediate states
- Use `ResizeArray<T>` with pre-allocated capacity for command lists
- Use `ArrayPool<T>.Shared` for dynamic buffers (already implemented in `RenderBuffer`)
- Use `[<InlineIfLambda>]` for callback parameters to prevent closure allocation
- Use `voption` instead of `option` for value-type optionals

### Additive Design

New features must not break existing code:

- Users upgrade without code changes
- Opt-in to new features explicitly
- 2D renderer works standalone or layered with 3D pipeline

### Batching Efficiency

SpriteBatch performance depends on minimizing `End()`/`Begin()` pairs:

```fsharp
// Efficient: Group by state
buffer.BlendState(BlendState.Additive)
for sprite in additiveSprites do
  buffer.Sprite(sprite)
buffer.BlendState(BlendState.AlphaBlend)
for sprite in normalSprites do
  buffer.Sprite(sprite)

// Inefficient: State change per sprite
for sprite in sprites do
  buffer.BlendState(sprite.Blend)
  buffer.Sprite(sprite)
```

### Layer Sorting Considerations

When `SortCommands = true`, commands are sorted by `RenderLayer`. State commands (`SetCamera`, `SetBlendState`, etc.) must be placed at distinct layer boundaries to maintain correct ordering:

```fsharp
// Correct: Camera at distinct layer boundary
buffer.Sprite(worldSprite, layer = 0<RenderLayer>)
buffer.Camera(uiCamera, layer = 100<RenderLayer>)
buffer.Sprite(uiSprite, layer = 100<RenderLayer>)
```

### SpriteBatch Texture Binding Constraints

> **Critical:** MonoGame's `SpriteBatch` only binds a single texture (the sprite diffuse) per draw call. Lighting requires binding additional textures (`NormalTexture`, `LightDataTexture`, `ShadowAtlasTexture`) via `Effect.Parameters`.

**Implications for Lit Rendering:**

1. **Texture Binding Strategy:** Before `SpriteBatch.Begin()`, bind auxiliary textures to the Effect:

   ```fsharp
   litEffect.Parameters.["NormalTexture"].SetValue(normalAtlas)
   litEffect.Parameters.["LightDataTexture"].SetValue(lightData)
   litEffect.Parameters.["ShadowAtlasTexture"].SetValue(shadowAtlas)
   spriteBatch.Begin(effect = litEffect, ...)
   ```

2. **Normal Map Atlasing Requirement:** SpriteBatch batches by primary texture only. Switching `NormalTexture` mid-batch requires `End()`/`Begin()` (expensive). **Pack normal maps into a texture atlas** matching your diffuse atlas to avoid batch breaks.

3. **Lit vs Unlit Sprites:** Use layer boundaries to separate lit and unlit sprites:

   ```fsharp
   // Unlit background (no effect)
   buffer.Effect(ValueNone, layer = 0<RenderLayer>)
   buffer.Sprite(background, layer = 0<RenderLayer>)

   // Lit game objects (custom effect)
   buffer.Effect(ValueSome litEffect, layer = 100<RenderLayer>)
   buffer.Sprite(player, layer = 100<RenderLayer>)
   ```

---

## Phase 1: DSL Parity

Match the ergonomics of `Rendering3D/View.fs` with computation expressions, fluent extensions, and a `Buffer2D` module.

> **Note:** `SpriteBuilder` supersedes the existing `Draw2DBuilder` and `Draw2D` module. The legacy API will be deprecated in a future release.

### 1.1 SpriteState Struct

```fsharp
[<Struct>]
type SpriteState = {
  Texture: Texture2D
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
```

### 1.2 SpriteBuilder Computation Expression

```fsharp
type SpriteBuilder() =
  member inline _.Yield(_: unit) : SpriteState = { ... }

  [<CustomOperation("texture")>]
  member inline _.Texture(s, tex: Texture2D) =
    { s with Texture = tex; Width = tex.Width; Height = tex.Height }

  [<CustomOperation("at")>]
  member inline _.At(s, x: int, y: int) = { s with DestX = x; DestY = y }

  [<CustomOperation("size")>]
  member inline _.Size(s, w: int, h: int) = { s with Width = w; Height = h }

  [<CustomOperation("sourceRect")>]
  member inline _.SourceRect(s, r: Rectangle) = { s with SourceRect = ValueSome r }

  [<CustomOperation("color")>]
  member inline _.Color(s, c: Color) = { s with Color = c }

  [<CustomOperation("rotatedBy")>]
  member inline _.RotatedBy(s, radians: float32) = { s with Rotation = radians }

  [<CustomOperation("centered")>]
  member inline _.Centered(s) =
    { s with Origin = Vector2(float32 s.Width / 2f, float32 s.Height / 2f) }

  [<CustomOperation("flippedH")>]
  member inline _.FlippedH(s) =
    { s with Effects = s.Effects ||| SpriteEffects.FlipHorizontally }

  [<CustomOperation("layer")>]
  member inline _.Layer(s, l: int<RenderLayer>) = { s with Layer = l }

  member inline _.Run(s) : SpriteState = s

[<AutoOpen>]
module View2D =
  let sprite = SpriteBuilder()
  let text = TextBuilder()
```

### 1.3 TextState Struct

```fsharp
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

type TextBuilder() =
  member inline _.Yield(_: unit) : TextState = { ... }

  [<CustomOperation("font")>]
  member inline _.Font(s, f: SpriteFont) = { s with Font = f }

  [<CustomOperation("content")>]
  member inline _.Content(s, t: string) = { s with Text = t }

  [<CustomOperation("at")>]
  member inline _.At(s, x: int, y: int) = { s with DestX = x; DestY = y }

  [<CustomOperation("color")>]
  member inline _.Color(s, c: Color) = { s with Color = c }

  [<CustomOperation("scale")>]
  member inline _.Scale(s, sc: float32) = { s with Scale = sc }

  [<CustomOperation("layer")>]
  member inline _.Layer(s, l: int<RenderLayer>) = { s with Layer = l }

  member inline _.Run(s) : TextState = s

type RenderCmd2D =
  // ... existing ...
  | DrawText of TextState
```

> **Text and Lighting:** Text is rendered as standard sprites via `SpriteBatch.DrawString()`. Text does NOT support normal maps and is typically unlit. Place text on a UI layer with no lighting effect applied to avoid batch flushes with lit content.

### 1.4 Fluent Buffer Extensions

```fsharp
[<Extension>]
type RenderBuffer2DExtensions =

  [<Extension>]
  static member inline Sprite(this: RenderBuffer<RenderCmd2D>, s: SpriteState) =
    this.Add(s.Layer, DrawTexture(...))
    this

  [<Extension>]
  static member inline Sprite(this: RenderBuffer<RenderCmd2D>, tex: Texture2D, x: int, y: int) =
    this.Add(0<RenderLayer>, DrawTexture(...))
    this

  [<Extension>]
  static member inline Camera(this: RenderBuffer<RenderCmd2D>, cam: Camera, ?layer: int<RenderLayer>) =
    this.Add(defaultArg layer 0<RenderLayer>, SetCamera cam)
    this

  [<Extension>]
  static member inline Clear(this: RenderBuffer<RenderCmd2D>, color: Color) =
    this.Add(0<RenderLayer>, ClearTarget(ValueSome color, false))
    this

  [<Extension>]
  static member inline BlendState(this: RenderBuffer<RenderCmd2D>, bs: BlendState) =
    this.Add(0<RenderLayer>, SetBlendState bs)
    this

  [<Extension>]
  static member inline Submit(this: RenderBuffer<RenderCmd2D>) = ()
```

### 1.4 Buffer2D Module (Pipeline Style)

```fsharp
module Buffer2D =
  let inline sprite s (buffer: RenderBuffer<RenderCmd2D>) = buffer.Sprite(s)
  let inline camera cam (buffer: RenderBuffer<RenderCmd2D>) = buffer.Camera(cam)
  let inline clear color (buffer: RenderBuffer<RenderCmd2D>) = buffer.Clear(color)
  let inline blendState bs (buffer: RenderBuffer<RenderCmd2D>) = buffer.BlendState(bs)

  // Phase 3 Extensions
  let inline lighting (state: LightingState2D) (buffer: RenderBuffer<RenderCmd2D>) =
    buffer.Add(0<RenderLayer>, SetLighting state); buffer

  let inline pointLight (light: PointLight2D) (buffer: RenderBuffer<RenderCmd2D>) =
    buffer.Add(0<RenderLayer>, AddPointLight light); buffer

  let inline directionalLight (light: DirectionalLight2D) (buffer: RenderBuffer<RenderCmd2D>) =
    buffer.Add(0<RenderLayer>, AddDirectionalLight light); buffer
```

### Phase 1 Tasks

- [ ] Add `SpriteState` struct to `Graphics2D.fs`
- [ ] Implement `SpriteBuilder` computation expression
- [ ] Add `View2D.sprite` global builder instance
- [ ] Add `TextState` struct and `TextBuilder` CE (wrapping `SpriteFont`)
- [ ] Add `DrawText` command to `RenderCmd2D`
- [ ] Implement `RenderBuffer2DExtensions` fluent API
- [ ] Implement `Buffer2D` module
  - [ ] Add lighting extensions (Phase 3)
- [ ] Write unit tests
- [ ] Update samples
- [ ] Refactor `SafeSetParam` to `Mibo.Rendering.Common` module
- [ ] Mark `Draw2DBuilder` and `Draw2D` module as `[<Obsolete>]`

---

## Phase 2: Post-Processing Pipeline

Enable post-processing effects by rendering to an intermediate `RenderTarget2D` and applying multi-pass shader chains.

### 2.1 Effect Configuration Types

Matching the 3D pipeline pattern, use config structs with defaults:

```fsharp
/// Configuration for vignette effect.
[<Struct>]
type VignetteConfig = {
  Effect: Effect
  /// Radius of the clear center area. Typical: 0.5f - 0.8f
  Radius: float32
  /// Softness of the edge falloff. Typical: 0.2f - 0.5f
  Softness: float32
}

module VignetteConfig =
  let defaults (effect: Effect) = {
    Effect = effect
    Radius = 0.7f
    Softness = 0.3f
  }

/// Configuration for bloom effect.
[<Struct>]
type BloomConfig2D = {
  ExtractEffect: Effect
  BlurEffect: Effect
  CompositeEffect: Effect
  /// Minimum brightness for bloom contribution. Typical: 0.7f - 1.2f
  Threshold: float32
  /// Bloom brightness multiplier. Typical: 0.5f - 2.0f
  Intensity: float32
  /// How far bloom spreads. Typical: 0.5f - 1.0f
  Scatter: float32
}

module BloomConfig2D =
  let defaults (extractFx: Effect) (blurFx: Effect) (compositeFx: Effect) = {
    ExtractEffect = extractFx
    BlurEffect = blurFx
    CompositeEffect = compositeFx
    Threshold = 0.8f
    Intensity = 1.0f
    Scatter = 0.7f
  }

/// Configuration for color grading via LUT.
[<Struct>]
type ColorGradeConfig = {
  Effect: Effect
  LutTexture: Texture3D
  LutSize: int
  /// Blend between original (0) and graded (1). Allows animated transitions.
  Blend: float32
}

module ColorGradeConfig =
  let defaults (effect: Effect) (lut: Texture3D) = {
    Effect = effect
    LutTexture = lut
    LutSize = 32
    Blend = 1.0f
  }
```

### 2.2 Post-Process Pipeline Config

```fsharp
/// Custom pass for effects not covered by built-in configs.
[<Struct>]
type CustomPostProcessPass = {
  Effect: Effect
  SetupEffect: (Effect -> GameTime -> RenderTarget2D -> unit) voption
}

/// Main post-processing configuration.
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

  let withVignette (cfg: VignetteConfig) (pp: PostProcess2DConfig) =
    { pp with Vignette = ValueSome cfg }

  let withBloom (cfg: BloomConfig2D) (pp: PostProcess2DConfig) =
    { pp with Bloom = ValueSome cfg }

  let withColorGrade (cfg: ColorGradeConfig) (pp: PostProcess2DConfig) =
    { pp with ColorGrade = ValueSome cfg }

  let withCustomPasses (passes: CustomPostProcessPass[]) (pp: PostProcess2DConfig) =
    { pp with CustomPasses = ValueSome passes }

/// Extended Batch2DConfig with post-processing and lighting support.
type Batch2DConfig = {
  // ... existing fields ...
  ClearColor: Color voption
  SortCommands: bool
  SortMode: SpriteSortMode
  BlendState: BlendState
  SamplerState: SamplerState
  DepthStencilState: DepthStencilState
  RasterizerState: RasterizerState
  Effect: Effect
  TransformMatrix: Matrix voption
  // NEW: Post-processing configuration
  PostProcess: PostProcess2DConfig voption
  // NEW: Lighting configuration (Phase 3)
  Lighting: Lighting2DConfig voption
}
```

> **UI vs World Rendering:** Post-processing affects ALL sprites rendered by the batcher. For games with both world content (affected by bloom/vignette) and UI elements (not affected), use two separate `Batch2DRenderer` instances:
>
> - World renderer: `PostProcess = ValueSome ppConfig`
> - UI renderer: `PostProcess = ValueNone`

> **Configuration Scope:** `PostProcess2DConfig` and effect configs are set at renderer initialization, NOT during the render loop. Do not recreate configs per-frame. Store your config as a static or model-level value and pass it when creating the renderer.

> **Shader Model Constraint:** MonoGame targets `ps_3_0`/`vs_3_0` for OpenGL compatibility. Features like Texture Arrays are NOT available. Any data binding technique (e.g., light data textures) must use `Texture2D` sampling, not texture arrays. Users may write shaders targeting higher models for DirectX-only, but Mibo's documented contracts target the lowest common denominator.

### 2.3 Usage Examples

```fsharp
// Simple bloom with defaults
let config = {
  Batch2DConfig.defaults with
    PostProcess =
      PostProcess2DConfig.none
      |> PostProcess2DConfig.withBloom (BloomConfig2D.defaults extractFx blurFx compositeFx)
}

// Bloom with custom threshold + vignette
let config = {
  Batch2DConfig.defaults with
    PostProcess =
      PostProcess2DConfig.none
      |> PostProcess2DConfig.withBloom { BloomConfig2D.defaults extractFx blurFx compositeFx with Threshold = 0.5f }
      |> PostProcess2DConfig.withVignette { VignetteConfig.defaults vignetteFx with Softness = 0.4f }
}
```

### 2.4 Render Target Management

Internal to `Batch2DRenderer`:

```fsharp
let mutable sceneTarget: RenderTarget2D voption = ValueNone
let mutable pingTarget: RenderTarget2D voption = ValueNone
let mutable pongTarget: RenderTarget2D voption = ValueNone

let ensureTarget (device: GraphicsDevice) (current: RenderTarget2D voption) =
  match current with
  | ValueSome rt when rt.Width = device.PresentationParameters.BackBufferWidth
                   && rt.Height = device.PresentationParameters.BackBufferHeight -> rt
  | ValueSome rt -> rt.Dispose(); createNewTarget device
  | ValueNone -> createNewTarget device
```

### 2.5 Multi-Pass Execution

```fsharp
// After scene rendering to sceneTarget:
let mutable input = sceneTarget
let mutable output = pingTarget
let mutable usePing = true

for pass in config.PostProcess.Passes do
  device.SetRenderTarget(output)
  pass.SetupEffect |> ValueOption.iter (fun setup -> setup pass.Effect gameTime input)
  pass.Effect.Parameters.["InputTexture"].SetValue(input)
  drawFullscreenQuad pass.Effect

  input <- output
  output <- if usePing then pongTarget else pingTarget
  usePing <- not usePing

// Final blit to backbuffer
device.SetRenderTarget(null)
blitToScreen input
```

### 2.6 DrawCustom vs PostProcess

| Use Case                             | Approach                   |
| ------------------------------------ | -------------------------- |
| Full-frame effects (bloom, vignette) | `PostProcess2DConfig`      |
| Mid-frame custom rendering           | `DrawCustom`               |
| Render-to-texture for scene use      | `DrawCustom` with user RTs |

### Phase 2 Tasks

- [ ] Add `PostProcessPass` struct
- [ ] Add `PostProcess2DConfig` to `Batch2DConfig` (inject `IRenderTargetPool` via constructor or config)
- [ ] Reuse `Mibo.Rendering.Graphics3D.RenderTargetPool` for ping-pong targets
- [ ] Implement multi-pass execution in `Draw` method
- [ ] Create `PostProcess2D` factory module
- [ ] Document reference shaders for testing (not shipped)
- [ ] Add sample: neon game with bloom
- [ ] Performance test: verify no per-frame allocations

---

## Phase 3: 2D Lighting System

Dynamic 2D lighting via normal maps and CPU Tiled Forward, matching the 3D pipeline architecture.

### 3.1 Light Types

```fsharp
/// Per-light shadow settings (optional override for global bias)
[<Struct>]
type ShadowSettings2D = {
  Bias: float32 voption  // If None, uses global Shadows2DConfig.ShadowBias
}

module ShadowSettings2D =
  let defaults = { Bias = ValueNone }
  let withBias b = { Bias = ValueSome b }

[<Struct>]
type PointLight2D = {
  Position: Vector2
  Color: Color
  Intensity: float32
  Radius: float32
  Falloff: float32
  Shadow: ShadowSettings2D voption  // ValueSome = casts shadows
}

[<Struct>]
type DirectionalLight2D = {
  Direction: Vector2
  Color: Color
  Intensity: float32
  Shadow: ShadowSettings2D voption  // ValueSome = casts shadows
}

[<Struct>]
type AmbientLight2D = {
  Color: Color
  // Intensity is pre-multiplied into Color for the shader
}

[<Struct>]
type LightingState2D = {
  Ambient: AmbientLight2D
  PointLights: PointLight2D[]
  DirectionalLights: DirectionalLight2D[]
}
```

### 3.2 Lighting Commands

```fsharp
type RenderCmd2D =
  // ... existing ...
  | SetLighting of LightingState2D
  | AddPointLight of PointLight2D
  | AddDirectionalLight of DirectionalLight2D
```

`SetLighting` replaces the current lighting state. `AddPointLight`/`AddDirectionalLight` append to accumulated lights.

### 3.3 Normal Map Support in Sprites

Extend `SpriteState` for API consistency:

```fsharp
[<Struct>]
type SpriteState = {
  // ... existing ...
  NormalMap: Texture2D voption
}

[<CustomOperation("normalMap")>]
member inline _.NormalMap(s, tex: Texture2D) = { s with NormalMap = ValueSome tex }
```

### 3.4 CPU Tiled Forward Architecture

Matching the 3D pipeline, use CPU Tiled Forward:

1. **CPU Light Binning**: Divide screen into tiles (default 32×32 pixels). For each tile, compute which lights overlap. Light positions are stored in **world coordinates** to support multi-camera/split-screen setups correctly.
2. **Single Forward Pass**: Lit sprites use a shader that samples the diffuse + normal map. Per-pixel, the shader transforms world-space light data to screen-space using the `CameraMatrix`.
3. **No Multi-Pass RT Switching**: Better batching than deferred.

```
Frame Flow:
┌────────────────────────────┐
│ CPU: Bin lights into tiles │
└─────────────┬──────────────┘
              │
              ▼
┌────────────────────────────┐
│ GPU: Single Forward Pass   │
│ • Sample diffuse + normal  │
│ • Look up tile light list  │
│ • Evaluate N lights        │
└────────────────────────────┘
```

### 3.5 Configuration

```fsharp
[<Struct>]
type Lighting2DConfig = {
  Enabled: bool
  DefaultAmbient: AmbientLight2D
  /// Screen-space tile size for CPU light culling (default: 32).
  TileSize: int
  /// Maximum lights per tile (default: 8).
  MaxLightsPerTile: int
  /// Shadow configuration (ValueNone = shadows disabled).
  Shadows: Shadows2DConfig voption
}

module Lighting2DConfig =
  let disabled = {
    Enabled = false
    DefaultAmbient = { Color = Color.White }
    TileSize = 32
    MaxLightsPerTile = 8
    Shadows = ValueNone
  }

  let enabled ambient = {
    Enabled = true
    DefaultAmbient = ambient
    TileSize = 32
    MaxLightsPerTile = 8
    Shadows = ValueNone
  }

  let withShadows (cfg: Shadows2DConfig) (lighting: Lighting2DConfig) =
    { lighting with Shadows = ValueSome cfg }
```

#### Texture Lifecycle Management

| Texture              | Created When                         | Updated When          | Resized When                    |
| -------------------- | ------------------------------------ | --------------------- | ------------------------------- |
| `LightDataTexture`   | Renderer init (size from config)     | Every frame (SetData) | Config change (MaxLights)       |
| `LightGridTexture`   | Renderer init (viewport-based)       | Every frame (SetData) | Viewport resize                 |
| `ShadowAtlasTexture` | Renderer init (via RenderTargetPool) | Shadow pass (render)  | Config change (MaxShadowLights) |

> **Zero-Allocation Pattern:** Use `Texture2D.SetData()` for per-frame updates without reallocation. Never use `new Texture2D()` in the render loop.

### 3.6 Shader Contract (API Bindings)

> **Shader Model Constraint:** MonoGame targets `ps_3_0`/`vs_3_0` for OpenGL compatibility. Texture Arrays are NOT available. All data binding uses `Texture2D` sampling.

> **Mibo does not include any shader files - you must provide your own.** This section defines the parameter bindings that the renderer will provide to your shaders.

#### Texture Register Bindings

| Register | Texture              | Description                               | SamplerState |
| -------- | -------------------- | ----------------------------------------- | ------------ |
| `s0`     | `DiffuseTexture`     | Sprite albedo (controlled by SpriteBatch) | LinearClamp  |
| `s1`     | `NormalTexture`      | Sprite tangent-space normal map           | PointClamp   |
| `s2`     | `LightDataTexture`   | All light properties packed               | PointClamp   |
| `s3`     | `LightGridTexture`   | Tile → light indices map                  | PointClamp   |
| `s4`     | `ShadowAtlasTexture` | 1D shadow depth strips per light          | LinearClamp  |

#### Uniform Parameters

| Parameter          | Type     | Description                                            |
| ------------------ | -------- | ------------------------------------------------------ |
| `LightCount`       | float    | Number of rows in LightDataTexture (shader loop bound) |
| `DirectionalCount` | float    | Number of directional lights (first N rows in texture) |
| `AmbientColor`     | float3   | Ambient light color × intensity                        |
| `TileSize`         | float2   | Screen-space tile dimensions (default: 32×32)          |
| `ScreenSize`       | float2   | Viewport dimensions in pixels                          |
| `CameraMatrix`     | float4x4 | Camera view matrix for world→screen transform          |
| `ShadowAtlasSize`  | float2   | Shadow atlas dimensions (Resolution, MaxShadowLights)  |
| `ShadowBias`       | float    | Global shadow bias (acne prevention)                   |

> **Note:** `LightCount` and `DirectionalCount` are `float` for ps_3_0 shader compatibility.

#### LightDataTexture Layout

**Resolution:** 2 × N where N = actual light count (resized dynamically)

| Row | Pixel X | Components | Description                                                 |
| --- | ------- | ---------- | ----------------------------------------------------------- |
| i   | 0       | x, y       | Position (point) or Direction (directional)                 |
| i   | 0       | z, w       | Radius (-1 = directional), **ShadowIndex** (-1 = no shadow) |
| i   | 1       | x, y, z    | Color (RGB)                                                 |
| i   | 1       | w          | Falloff (moved from Row 0 to accommodate ShadowIndex)       |

> **ShadowIndex:** Absolute row in `ShadowAtlasTexture`. Value of -1 means light does not cast shadows.

#### LightGridTexture Layout

**Resolution:** `ceil(ScreenWidth / TileSize) × ceil(ScreenHeight / TileSize)`

| Channel | Description                    |
| ------- | ------------------------------ |
| R       | Light index 0 (or 255 if none) |
| G       | Light index 1 (or 255 if none) |
| B       | Light index 2 (or 255 if none) |
| A       | Light index 3 (or 255 if none) |

> **Light Index Limit:** 8-bit RGBA encoding allows indices 0-254. Index 255 = "no light" sentinel. Maximum 255 unique lights per scene.

> **Overflow:** When >4 lights affect a tile, highest intensity first (computed as `Color × Intensity / Distance²`).

#### Default Flat Normal Map

Sprites without `NormalMap` use a 1×1 texture: `Color(128, 128, 255, 255)` (normal pointing straight out in tangent space). Stored as per-renderer instance, created at init, disposed with renderer.

#### Post-Process Shader Bindings

| Parameter      | Type      | Description                          |
| -------------- | --------- | ------------------------------------ |
| `InputTexture` | Texture2D | Previous pass output / scene texture |
| `TexelSize`    | float2    | `(1/Width, 1/Height)` for sampling   |
| `Time`         | float     | Game time in seconds                 |

Effect-specific parameters are set via the config's `SetupEffect` callback.

### 3.7 Dynamic Shadows

Dynamic shadow casting for point and directional lights via 1D Radial Shadow Atlas.

#### Shadow Atlas Architecture

The 2D shadow system uses a **1D Radial Shadow Atlas** (equivalent to 3D Shadow Atlas but using polar projection):

- **X-Axis:** Projection coordinate (Angle for Point, Distance for Directional)
- **Y-Axis:** Light Index (one row per shadow-casting light)
- **Value:** Distance to nearest occluder (R32_Float format)

Rows are assigned **dynamically** each frame—no reserved slots. The CPU tracks light type per row using the `Radius = -1` sentinel.

#### Occluder Types

```fsharp
[<Struct>]
type Occluder2D = {
  P1: Vector2        // Start point of wall segment
  P2: Vector2        // End point of wall segment
  Height: float32    // Z-height for pseudo-3D (default: 1.0)
}

type RenderCmd2D =
  // ... existing ...
  | AddOccluder of Occluder2D
```

> **Height Purpose:** Enables shadows that respect "tall" vs "short" walls (e.g., platformer foreground/background). Set to `1.0` for uniform shadows.

#### Shadow Configuration

```fsharp
type SoftShadowQuality2D = None = 0 | Low = 1 | Medium = 3 | High = 5

[<Struct>]
type Shadows2DConfig = {
  Enabled: bool
  Resolution: int             // Angular resolution per strip (e.g., 512)
  MaxShadowLights: int        // Total atlas height (rows)
  SoftShadowQuality: SoftShadowQuality2D
  ShadowBias: float32         // Global default, overridable per-light
}

module Shadows2DConfig =
  let defaults = {
    Enabled = true
    Resolution = 512
    MaxShadowLights = 16
    SoftShadowQuality = SoftShadowQuality2D.Low
    ShadowBias = 0.001f
  }
```

#### Shadow Generation Pass

Executed between light prep and main pass as a distinct render phase.

**Frame Lifecycle:**

1. **Collect:** Iterate buffer, separate commands into buckets (`Sprite`, `Light`, `Occluder`)
2. **Prep:** Bin lights, select shadow casters, upload `LightDataTexture` (shadow indices needed here)
3. **Shadow Pass:** `SetRenderTarget(Atlas)` → Loop lights → Draw occluders
4. **Main Pass:** `SetRenderTarget(Scene)` → `SpriteBatch.Begin` (lit shader sampling atlas) → Draw sprites
5. **Post-Process:** `SetRenderTarget(Backbuffer)` → Apply bloom/tonemap

**Light Selection:**

```fsharp
// Sort lights with Shadow=Some by priority
let shadowLights =
  lights
  |> Seq.filter (fun l -> l.Shadow.IsSome)
  |> Seq.sortByDescending (fun l -> l.Intensity / (distanceToCamera l) ** 2f)
  |> Seq.truncate config.Shadows.MaxShadowLights
  |> Seq.indexed
  |> Seq.map (fun (row, l) -> { l with ShadowIndex = row })
```

**Shader Fallback:** Shadows are disabled if user doesn't provide `ShaderBase2D.ShadowCaster`:

```fsharp
let shadowsEnabled =
  config.Shadows.IsSome
  && shaderOverrides.ContainsKey(ShaderBase2D.ShadowCaster)

type ShaderBase2D =
  | LitSprite      // Main Tiled Forward shader
  | ShadowCaster   // Polar/Orthographic projection shader
  | PostProcess    // Bloom/ToneMap
```

#### OccluderBatch Implementation

A specialized batcher for line segment occluders using minimal vertex format.

**Vertex Format:**

```fsharp
[<Struct>]
type VertexPosition2D = { Position: Vector2 }

module VertexPosition2D =
  let declaration = VertexDeclaration([|
    VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0)
  |])
```

**Batch Implementation:**

```fsharp
module OccluderBatch =
  type State = {
    mutable Vertices: VertexPosition2D[]
    mutable VertexBuffer: DynamicVertexBuffer
    mutable VertexCount: int
    GraphicsDevice: GraphicsDevice
  }

  // Uses ArrayPool.Shared for zero-allocation
  // Renders as LineList primitive
  // Depth write enabled, no color write
```

**Render State:**

```fsharp
let shadowDepthState = DepthStencilState(
  DepthBufferEnable = true,
  DepthBufferWriteEnable = true,
  DepthBufferFunction = CompareFunction.Less
)
// ColorWriteChannels = Red only (R32_Float depth)
```

#### Shadow Projection Shaders

The `ShadowCaster` vertex shader handles both point (polar) and directional (orthographic) modes.

**Polar Projection (Point Lights):**

```hlsl
// Input: Occluder endpoint position in world space
// Output: Position in clip space where X=angle, Z=depth

float2 diff = Input.Position - LightPosition;
float angle = atan2(diff.y, diff.x);     // -PI to PI
float x = angle / 3.14159;                // -1 to 1 (Clip Space X)
float depth = length(diff) / MaxRange;   // 0 to 1 (Depth)
Output.Position = float4(x, 0.0, depth, 1.0);
```

**Orthographic Projection (Directional Lights):**

```hlsl
// Input: Occluder endpoint position in world space
// Output: Position in clip space where X=perpendicular projection, Z=parallel depth

float2 perpDir = float2(LightDir.y, -LightDir.x);  // Perpendicular to light direction
float proj = dot(Input.Position, perpDir);
float x = (proj / AtlasWidth + 1.0) * 0.5;         // Map negatives to 0..1
float depth = dot(Input.Position, LightDir) / MaxRange;
Output.Position = float4(x * 2.0 - 1.0, 0.0, depth, 1.0);
```

**Type Detection:** The CPU stores `Radius = -1` sentinel for directional lights. The shader branches based on this value.

#### Main Shader Shadow Sampling

The lit sprite shader samples the shadow atlas based on light type.

```hlsl
float ComputeShadow(float2 pixelPos, LightData light) {
  if (light.ShadowIndex < 0) return 1.0; // No shadow

  float u, dist;

  if (light.Radius > 0) {
    // Point Light: Polar sampling
    float2 diff = pixelPos - light.Position;
    dist = length(diff);
    float angle = atan2(diff.y, diff.x);
    u = (angle + 3.14159) / 6.28318;  // 0..1
  } else {
    // Directional Light: Orthographic sampling
    float2 perpDir = float2(light.Direction.y, -light.Direction.x);
    float proj = dot(pixelPos, perpDir);
    u = (proj / ScreenWidth + 1.0) * 0.5;
    dist = dot(pixelPos, light.Direction);
  }

  float v = (light.ShadowIndex + 0.5) / AtlasHeight;
  float occluderDist = tex2D(ShadowAtlasSampler, float2(u, v)).r;

  // Per-light bias (resolved by CPU from per-light or global)
  if (dist > occluderDist + light.ShadowBias) return 0.0;
  return 1.0;
}
```

> **Polar Seam Handling:** Polar projection has discontinuity at angle ±π (u=0/1). Use `SamplerState.LinearClamp`. For soft shadows, clamp sample offsets to avoid wrap artifacts.

#### Soft Shadows

PCF-style sampling with configurable tap counts:

```fsharp
type SoftShadowQuality2D = None = 0 | Low = 1 | Medium = 3 | High = 5
```

- `None` = Shadows disabled (skip sampling)
- `Low` = 1 tap (hard shadows)
- `Medium` = 3 taps (center + left/right)
- `High` = 5 taps (center + 2 left/right)

#### ShadowAtlasTexture Resource

| Property        | Value                              |
| --------------- | ---------------------------------- |
| **Format**      | `SurfaceFormat.Single` (R32_Float) |
| **Width**       | `Shadows2DConfig.Resolution`       |
| **Height**      | `Shadows2DConfig.MaxShadowLights`  |
| **Acquisition** | Via `IRenderTargetPool`            |
| **Reuse**       | Same texture each frame            |

#### Multi-Camera Behavior

Shadows are rendered **once per frame** in world-space:

- All cameras sample the same shadow atlas
- Split-screen does NOT require additional atlas rows
- Shadow generation uses world origin as reference, not camera position

### Phase 3 Tasks

#### Lighting Tasks

- [ ] Add `PointLight2D`, `DirectionalLight2D`, `AmbientLight2D` structs
- [ ] Add `ShadowSettings2D` struct with per-light bias
- [ ] Add `LightingState2D` struct
- [ ] Add `SetLighting`, `AddPointLight`, `AddDirectionalLight` commands
- [ ] Add `NormalMap` to `SpriteState`
- [ ] Implement CPU light binning (Tiled Forward)
- [ ] Implement light data binding to user-provided effects
- [ ] Document shader contract (parameter names/types)
- [ ] Add buffer extensions for lights

#### Shadow Tasks

- [ ] Add `Occluder2D` struct and `AddOccluder` command
- [ ] Add `Shadows2DConfig` and nest in `Lighting2DConfig`
- [ ] Implement `OccluderBatch` using `VertexPosition2D`
- [ ] Implement shadow atlas generation pass (polar projection)
- [ ] Implement directional shadow generation (orthographic projection)
- [ ] Add `ShadowAtlasTexture` binding to shader contract
- [ ] Document shadow shader requirements

#### Validation

- [ ] Create reference shader for testing (not shipped)
- [ ] Add sample: dungeon with dynamic torches and shadows
- [ ] Performance test: 100 lights at 60fps
- [ ] Performance test: 16 shadow lights at 60fps

---

## Phase 4: Advanced Features

High-performance particle rendering and primitive drawing utilities.

### 4.1 BillboardBatch2D

Repurpose the existing `BillboardBatch` (from `Mibo.Elmish.Graphics3D`) for 2D particle systems. The 3D rendering pipeline has its own internal `BillboardBatch` copy, so modifying the shared version is safe.

**Approach:** Add 2D-specific overloads that use fixed camera vectors:

```fsharp
module BillboardBatch =
  // Existing 3D draw with camera vectors
  let drawUv (position: Vector3) (size: Vector2) (rotation: float32) (color: Color)
             (uv: UvRect) (camRight: Vector3) (camUp: Vector3) (state: State) = ...

  // New 2D draw overload (fixed camera = screen-aligned)
  let draw2D (position: Vector2) (size: Vector2) (rotation: float32) (color: Color)
             (uv: UvRect) (state: State) =
    drawUv (Vector3(position, 0f)) size rotation color uv Vector3.UnitX Vector3.UnitY state

  let draw2DSimple (position: Vector2) (size: Vector2) (color: Color) (state: State) =
    draw2D position size 0f color UvRect.full state
```

**Usage in 2D renderer:**

```fsharp
// High-performance particle burst (1000+ particles)
billboardBatch
|> BillboardBatch.begin' particleEffect
|> fun batch ->
    for p in particles do
      BillboardBatch.draw2D p.Position p.Size p.Rotation p.Color p.Uv &batch
    batch
|> BillboardBatch.end'
```

> **Note:** `QuadBatch` is NOT suitable for 2D—it renders quads on the X-Z plane (floor quads). Use `BillboardBatch` with fixed camera vectors instead.

### 4.2 Primitive Rendering (Deferred)

Simple primitives for debugging and UI. Consider adding in a future update:

```fsharp
// Potential future API
type RenderCmd2D =
  | DrawLine of p1: Vector2 * p2: Vector2 * color: Color * thickness: float32
  | DrawRect of bounds: Rectangle * color: Color * filled: bool
  | DrawCircle of center: Vector2 * radius: float32 * color: Color * filled: bool
```

These would bypass SpriteBatch and use `DrawUserPrimitives` with a simple `VertexPositionColor` batch.

> **Primitives and Lighting:** Primitives are intended for debug overlays and UI elements. They do NOT receive lighting or cast shadows. For lit geometry, use textured sprites with normal maps instead.

### Phase 4 Tasks

- [ ] Add `BillboardBatch.draw2D` and `draw2DSimple` overloads to `BillboardBatch.fs`
- [ ] Add `Particle2DState` struct with position, size, rotation, color, uv
- [ ] Add `Buffer2D.particles` extension for batch rendering
- [ ] Document particle rendering pattern in samples
- [ ] (Deferred) Add `DrawLine`, `DrawRect`, `DrawCircle` commands
- [ ] (Deferred) Implement primitive batch renderer

---

## Performance Guidelines

### Critical Rules

1. **All builder types must be `[<Struct>]`**
2. **Use `voption` instead of `option`**
3. **Use `Nullable<T>` for MonoGame interop**
4. **Use `[<InlineIfLambda>]` for callback parameters**
5. **Pre-allocate internal lists**: `ResizeArray<T>(capacity)`
6. **Reuse RenderTarget2D instances** (only recreate on resize)
7. **Use ArrayPool for dynamic vertex data**

---

## API Reference

### Usage: Computation Expression

```fsharp
let view ctx model buffer =
  let player = sprite {
    texture model.PlayerTex
    at model.X model.Y
    centered
    color Color.White
    layer 10<RenderLayer>
  }

  buffer.Sprite(player).Submit()
```

### Usage: Pipeline Style

```fsharp
let view ctx model buffer =
  buffer
  |> Buffer2D.clear Color.Black
  |> Buffer2D.sprite (Sprite.fromTexture model.Bg |> Sprite.at 0 0)
  |> Buffer2D.sprite (Sprite.fromTexture model.Player |> Sprite.at model.X model.Y)
  |> Buffer2D.submit
```

### Usage: Post-Processing

```fsharp
let config = {
  Batch2DConfig.defaults with
    ClearColor = ValueNone  // For layering with 3D
    PostProcess =
      PostProcess2DConfig.none
      |> PostProcess2DConfig.withBloom (BloomConfig2D.defaults extractFx blurFx compositeFx)
}
```

### Usage: 2D Lighting

```fsharp
let view ctx model buffer =
  // Set lighting state for all sprites in this frame
  buffer.Lighting({
    Ambient = { Color = Color.DarkBlue; Intensity = 0.2f }
    PointLights = [||]
    DirectionalLights = [||]
  }) |> ignore

  for torch in model.Torches do
    buffer.PointLight({ Position = torch.Pos; Color = Color.Orange; Intensity = 1.5f; Radius = 150f; Falloff = 2f }) |> ignore

  let wall = sprite { texture model.WallDiffuse; normalMap model.WallNormal; at 100 100 }
  buffer.Sprite(wall).Submit()
```

---

## Timeline

| Phase                    | Effort   | Dependencies |
| ------------------------ | -------- | ------------ |
| Phase 1: DSL Parity      | 2-3 days | None         |
| Phase 2: Post-Processing | 3-5 days | Phase 1      |
| Phase 3: 2D Lighting     | 5-7 days | Phase 2      |

Each phase can be shipped independently.

---

## Annex: Implementation Reference

### Texture Management

To maintain zero-GC hot path, textures are reused and only resized when necessary (e.g., light count increases or screen resolution changes).

```fsharp
// Internal pattern for resizing data/grid textures
let ensureTexture (device: GraphicsDevice) (current: Texture2D voption) (width: int) (height: int) (format: SurfaceFormat) =
  match current with
  | ValueSome tex when tex.Width = width && tex.Height = height -> tex
  | ValueSome tex -> tex.Dispose(); new Texture2D(device, width, height, false, format)
  | ValueNone -> new Texture2D(device, width, height, false, format)
```

### Effect Parameter Binding (`SafeSetParam`)

The `SafeSetParam` extension (currently internal to `Rendering3D`) will be extracted to a shared `Mibo.Rendering.Common` module. This provides null-safe parameter setting and cached lookups for both 2D and 3D pipelines.

### CPU Binning

- **Binning Cost:** 200,000 checks (100 lights \* 2000 tiles) is typically < 1ms.
- **Future Optimizations:** Spatial partitioning (quadtree) or SIMD vectorization can be applied if benchmarks show hot spots.
- **Tile Sensitivity:** Larger tile sizes reduce CPU cost but increase GPU work per pixel. 32x32 is the recommended starting balance.
