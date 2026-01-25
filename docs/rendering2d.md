---
title: Rendering 2D
category: Rendering
index: 11
---

# Rendering 2D

2D rendering in Mibo lives in `Mibo.Elmish.Graphics2D`.

The core building blocks are:

- `RenderBuffer<int<RenderLayer>, RenderCmd2D>` (submission + sorting)
- `Batch2DRenderer` (executes commands with `SpriteBatch`)

Mibo provides **multiple API styles** for 2D rendering, so you can choose the one that fits your coding style:

- **DSL API**: Computation expressions for declarative sprite and text configuration
- **Draw2D API**: Fluent builder for common operations
- **Buffer2D API**: Functional style with pipelined operations
- **Extension methods**: .NET-style methods on `RenderBuffer`

## Minimal 2D renderer

```fsharp
open Mibo.Elmish.Graphics2D

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  Draw2D.sprite model.PlayerTex model.PlayerRect
  |> Draw2D.atLayer 10<RenderLayer>
  |> Draw2D.submit buffer

let program =
  Program.mkProgram init update
  |> Program.withRenderer (Batch2DRenderer.create view)
```

## Render layers

`RenderLayer` is an `int` unit-of-measure.

- lower layers draw first
- higher layers draw last

If you enable sorting (default), the renderer sorts the buffer by layer each frame.

## Cameras

The 2D renderer responds to `SetCamera` commands.

```fsharp
let cam = Camera2D.create model.CamPos model.Zoom viewportSize

Draw2D.camera cam 0<RenderLayer> buffer
```

### Multi-camera in 2D

You can switch cameras multiple times in a single frame by emitting multiple camera/view commands and grouping your draws under each camera.

The built-in 2D renderer supports the same multi-camera "amenities" as 3D:

- `SetViewport` (split-screen / minimaps)
- `ClearTarget` between cameras

Example (two viewports, two cameras):

```fsharp
let leftVp = Viewport(0, 0, viewportSize.X / 2, viewportSize.Y)
let rightVp = Viewport(viewportSize.X / 2, 0, viewportSize.X / 2, viewportSize.Y)

Draw2D.viewport leftVp 0<RenderLayer> buffer
Draw2D.clear (ValueSome Color.CornflowerBlue) false 1<RenderLayer> buffer
Draw2D.camera leftCam 2<RenderLayer> buffer
// left-side sprites...

Draw2D.viewport rightVp 10<RenderLayer> buffer
Draw2D.clear (ValueSome Color.DarkSlateGray) false 11<RenderLayer> buffer
Draw2D.camera rightCam 12<RenderLayer> buffer
// right-side sprites...
```

Note on ordering: if `Batch2DConfig.SortCommands = true` (the default), only _layer_ ordering is guaranteed (commands within the same layer may reorder). For stateful sequences (viewport/clear/camera/effect), either:

- put each state change on its own layer (as above), or
- set `SortCommands = false` and rely on submission order.

## API styles

### DSL API (Computation Expressions)

The DSL API uses F# computation expressions for declarative sprite and text configuration:

```fsharp
open Mibo.Elmish.Graphics2D.DSL

let sprite =
  sprite {
    texture playerTexture
    at 100 100
    size 64 64
    color Color.White
    sourceRect (Rectangle(0, 0, 32, 32))
    rotatedBy System.Math.PI / 4.0f
    centered
    flippedH
    depth 0.5f
    layer 10<RenderLayer>
  }

sprite |> buffer.Sprite

let text =
  text {
    font myFont
    content "Hello, World!"
    at 50 50
    color Color.Yellow
    scale 1.5f
    rotatedBy 0.1f
    origin Vector2.Zero
    layer 5<RenderLayer>
  }

text |> buffer.Text
```

#### DSL Sprite Options

- `texture tex`: Set the texture (automatically sets size to texture dimensions)
- `normalMap tex`: Set the normal map for lighting
- `at x y`: Position (supports `int*int`, `float32*float32`, or `Vector2`)
- `size w h`: Set width and height
- `sourceRect rect`: Use a sub-rectangle of the texture
- `color c`: Tint color
- `rotatedBy radians`: Rotation in radians
- `origin o`: Origin point for rotation/scaling
- `centered`: Set origin to center of sprite
- `flippedH`: Flip horizontally
- `flippedV`: Flip vertically
- `depth d`: Depth value for sorting within layer
- `layer l`: Render layer

#### DSL Text Options

- `font f`: Sprite font to use
- `content s`: Text string to render
- `at x y`: Position (supports `int*int`, `float32*float32`, or `Vector2`)
- `color c`: Text color
- `scale s`: Scale multiplier
- `rotatedBy radians`: Rotation in radians
- `origin o`: Origin point
- `layer l`: Render layer

### Draw2D API (Fluent Builder)

The Draw2D API uses pipelined functions for common operations:

```fsharp
Draw2D.sprite model.PlayerTex model.PlayerRect
|> Draw2D.withSource (Rectangle(0, 0, 32, 32))
|> Draw2D.withColor Color.White
|> Draw2D.atLayer 10<RenderLayer>
|> Draw2D.submit buffer

Draw2D.camera cam 0<RenderLayer> buffer
Draw2D.viewport vp 0<RenderLayer> buffer
Draw2D.clear (ValueSome Color.Black) false 0<RenderLayer> buffer
Draw2D.effect (ValueSome myEffect) 0<RenderLayer> buffer
Draw2D.blendState BlendState.AlphaBlend 0<RenderLayer> buffer
Draw2D.samplerState SamplerState.PointClamp 0<RenderLayer> buffer
Draw2D.depthStencilState DepthStencilState.Default 0<RenderLayer> buffer
Draw2D.rasterizerState RasterizerState.CullNone 0<RenderLayer> buffer
```

### Buffer2D API (Functional Style)

The Buffer2D module provides functional-style operations:

```fsharp
open Mibo.Elmish.Graphics2D.DSL.Buffer2D

buffer
|> camera cam
|> clear Color.Black
|> sprite mySprite
|> text myText
|> blendState BlendState.Additive
|> effect myShader
|> lighting myLightingConfig
|> pointLight myPointLight
|> directionalLight myDirLight
|> occluder myOccluder
|> particles tex fx particleArray count
|> line p1 p2 Color.Red
|> rect myRect Color.Blue
|> circle center radius Color.Green
|> submit
```

### Extension Methods API

.NET-style extension methods on `RenderBuffer`:

```fsharp
buffer.Sprite(playerTexture, 100, 100, layer = 10<RenderLayer>)
buffer.Text(myFont, "Hello", 50, 50, layer = 5<RenderLayer>)
buffer.Camera(cam, 0<RenderLayer>)
buffer.Clear(Color.Black, 0<RenderLayer>)
buffer.BlendState(BlendState.AlphaBlend, 0<RenderLayer>)
buffer.Effect(myEffect, 0<RenderLayer>)
buffer.Lighting(lightingState, 0<RenderLayer>)
buffer.AddLighting(ambientLight, 0<RenderLayer>)
buffer.PointLight(pointLight, 0<RenderLayer>)
buffer.DirectionalLight(dirLight, 0<RenderLayer>)
buffer.Occluder(occluder, 0<RenderLayer>)
buffer.Particles(texture, effect, particles, count, 10<RenderLayer>)
buffer.Line(p1, p2, Color.Red, 10<RenderLayer>)
buffer.Rect(rect, Color.Blue, 10<RenderLayer>)
buffer.Circle(center, radius, Color.Green, segments = 32, layer = 10<RenderLayer>)
```

## Text Rendering

Text can be rendered using any of the API styles:

```fsharp
// Using DSL
let text =
  text {
    font myFont
    content "Score: 100"
    at 10 10
    color Color.White
    layer 100<RenderLayer>
  } |> buffer.Text

// Using Draw2D
Draw2D.text myFont "Score: 100" position
|> Draw2D.atLayer 100<RenderLayer>
|> Draw2D.submit buffer

// Using extensions
buffer.Text(myFont, "Score: 100", 10, 10, layer = 100<RenderLayer>)
```

## Shape Drawing

Mibo provides built-in support for drawing basic shapes:

### Lines

```fsharp
// Buffer2D
buffer |> line p1 p2 Color.Red

// Extensions
buffer.Line(Vector2(0f, 0f), Vector2(100f, 100f), Color.Red, 5<RenderLayer>)
```

### Rectangles

```fsharp
// Buffer2D
buffer |> rect (Rectangle(10, 10, 100, 50)) Color.Blue

// Extensions
buffer.Rect(myRect, Color.Blue, 10<RenderLayer>)
```

### Circles

```fsharp
// Buffer2D
buffer |> circle (Vector2(50f, 50f)) 25f Color.Green

// Extensions (with custom segment count)
buffer.Circle(center, radius, Color.Green, segments = 32, layer = 10<RenderLayer>)
```

## Particle System

For particle rendering, use the particle drawing command:

```fsharp
let particles = [| createParticle(); createParticle(); ... |]

// Buffer2D
buffer
|> particles texture effect particles particles.Length

// Extensions
buffer.Particles(texture, effect, particles, particles.Length, 15<RenderLayer>)
```

## Post-Processing

Mibo provides a flexible post-processing system with built-in effects:

### Configuration

```fsharp
open Mibo.Elmish.Graphics2D

let postProcessConfig =
  PostProcess2DConfig.none
  |> PostProcess2DConfig.withVignette (VignetteConfig.defaults vignetteEffect)
  |> PostProcess2DConfig.withBloom (BloomConfig2D.defaults extractFx blurFx compositeFx)
  |> PostProcess2DConfig.withColorGrade (ColorGradeConfig.defaults effect lutTexture)
  |> PostProcess2DConfig.withCustomPasses [| { Effect = customEffect; SetupEffect = ValueNone } |]

let renderer =
  Batch2DRenderer.createWithConfig
    { Batch2DConfig.defaults with
        PostProcess = ValueSome postProcessConfig }
    view
```

### Vignette

Adds a darkening effect around the edges:

```fsharp
let vignette = VignetteConfig.defaults vignetteEffect
let config = PostProcess2DConfig.none |> PostProcess2DConfig.withVignette vignette
```

Adjust `Radius` and `Softness` to control the effect.

### Bloom

Creates a glowing effect from bright areas:

```fsharp
let bloom = BloomConfig2D.defaults extractFx blurFx compositeFx
let config = PostProcess2DConfig.none |> PostProcess2DConfig.withBloom bloom
```

Adjust `Threshold`, `Intensity`, and `Scatter` to control the effect.

### Color Grading

Apply color grading using a lookup table (LUT):

```fsharp
let colorGrade = ColorGradeConfig.defaults effect lutTexture
let config = PostProcess2DConfig.none |> PostProcess2DConfig.withColorGrade colorGrade
```

### Custom Passes

Add your own post-processing effects:

```fsharp
let customPass = {
  Effect = myEffect
  SetupEffect = ValueSome (fun effect gameTime target ->
    effect.Parameters["Time"].SetValue(single gameTime.TotalGameTime.TotalSeconds)
    effect.Parameters["Resolution"].SetValue(Vector2(single target.Width, single target.Height))
  )
}

let config = PostProcess2DConfig.none |> PostProcess2DConfig.withCustomPasses [| customPass |]
```

## 2D Lighting

Mibo includes a comprehensive 2D lighting system with support for point lights, directional lights, ambient lighting, and dynamic shadows.

### Basic Lighting Setup

```fsharp
open Mibo.Elmish.Graphics2D

let lightingConfig =
  { Lighting2DConfig.defaults with
      Enabled = true
      Shadows = ValueSome { Shadows2DConfig.defaults with Quality = SoftShadowQuality2D.High } }

let renderer =
  Batch2DRenderer.createWithConfig
    { Batch2DConfig.defaults with Lighting = ValueSome lightingConfig }
    view
```

### Lighting Commands

#### Set Lighting State

```fsharp
// Buffer2D
buffer |> lighting lightingState

// Extensions
buffer.Lighting(lightingState, 0<RenderLayer>)
```

#### Add Ambient Light

```fsharp
let ambient = { AmbientLight2D.defaults with Color = Color(0.1f, 0.1f, 0.15f, 1.0f) }

// Buffer2D
buffer |> addLighting ambient

// Extensions
buffer.AddLighting(ambient, 0<RenderLayer>)
```

#### Add Point Light

```fsharp
let pointLight = {
  Position = Vector2(100f, 100f)
  Color = Color.Orange
  Radius = 200f
  Intensity = 1.5f
}

// Buffer2D
buffer |> pointLight pointLight

// Extensions
buffer.PointLight(pointLight, 0<RenderLayer>)
```

#### Add Directional Light

```fsharp
let dirLight = {
  Direction = Vector2(1f, -1f) |> Vector2.Normalize
  Color = Color.White
  Intensity = 0.8f
}

// Buffer2D
buffer |> directionalLight dirLight

// Extensions
buffer.DirectionalLight(dirLight, 0<RenderLayer>)
```

#### Add Occluder (for Shadows)

```fsharp
let occluder = {
  Vertices = [| Vector2(50f, 50f); Vector2(150f, 50f); Vector2(100f, 150f) |]
  Color = Color.Gray
}

// Buffer2D
buffer |> occluder occluder

// Extensions
buffer.Occluder(occluder, 0<RenderLayer>)
```

### Shadow Quality

Control shadow quality with the `SoftShadowQuality2D` enum:

- `NoValue`: No shadows
- `Low`: Low quality, better performance
- `Medium`: Balanced quality and performance
- `High`: High quality shadows

```fsharp
let shadowConfig = {
  Shadows2DConfig.defaults with
    Quality = SoftShadowQuality2D.High
    Bias = 0.001f  // Adjust to prevent shadow acne
}
```

## Configuration

### Batch2DConfig

The `Batch2DConfig` record controls the renderer's behavior:

```fsharp
type Batch2DConfig = {
  ClearColor: Color voption
  SortCommands: bool
  SortMode: SortMode
  BlendState: BlendState voption
  SamplerState: SamplerState voption
  DepthStencilState: DepthStencilState voption
  RasterizerState: RasterizerState voption
  Effect: Effect voption
  Transform: Matrix voption
  PostProcess: PostProcess2DConfig voption
  Lighting: Lighting2DConfig voption
  Shader: ShaderBase2D voption
  LitSprite: ShaderBase2D voption
  ShadowCaster: ShaderBase2D voption
  FinalBlendState: BlendState voption
}
```

Create a custom configuration:

```fsharp
Batch2DRenderer.createWithConfig
  { Batch2DConfig.defaults with
      ClearColor = ValueSome Color.Black
      SamplerState = ValueSome SamplerState.PointClamp
      Lighting = ValueSome lightingConfig
      PostProcess = ValueSome postProcessConfig }
  view
```

You can also change key SpriteBatch state _within_ a frame via commands:

- `Draw2D.effect` (per-segment effect; `ValueNone` restores default)
- `Draw2D.blendState`
- `Draw2D.samplerState`
- `Draw2D.depthStencilState`
- `Draw2D.rasterizerState`

### Shaders

Mibo provides shader base types for 2D rendering:

- `LitSprite`: For sprites with normal maps and lighting
- `ShadowCaster`: For rendering shadow casters
- `PostProcess`: For post-processing effects

Configure shaders via `Batch2DConfig`:

```fsharp
{ Batch2DConfig.defaults with
    Shader = ValueSome ShaderBase2D.LitSprite  // Indicates lighting should be used
    LitSprite = ValueSome myLitSpriteShader      // YOUR shader effect
    ShadowCaster = ValueSome myShadowShader }    // YOUR shader effect
```

**Note:** All shader effects are user-provided. Mibo only defines the contracts that your shaders must implement.

## Shader Contracts

Mibo **does not provide shaders** - you are responsible for authoring your own HLSL shaders that conform to the parameter contracts described below. The 2D sample shader files are merely examples demonstrating one possible implementation of these contracts.

When implementing custom shaders for 2D rendering, your shaders must conform to specific parameter contracts. Mibo automatically sets these parameters before drawing.

### Common Parameters

These parameters are set for all sprite-based rendering (sprites, text, particles):

#### Matrix Parameters

- `World` (Matrix): Identity matrix for sprites
- `View` (Matrix): Camera view matrix
- `ViewMatrix` (Matrix): Same as View (alias)
- `Projection` (Matrix): Camera projection matrix
- `ProjectionMatrix` (Matrix): Same as Projection (alias)

#### Texture Parameters

- `NormalMap` (Texture2D): Normal map texture (if available), otherwise a default flat normal map is provided
- `Texture` (Texture2D): Main texture alias (for particles)
- `DiffuseTexture` (Texture2D): Diffuse texture alias (for particles)

#### Vertex Input Structure

Vertex shaders should expect the following input structure:

```hlsl
struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};
```

#### Vertex Output Structure

For custom vertex shaders, pass world position to pixel shader:

```hlsl
struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float2 WorldPos : TEXCOORD1;  // Required for lighting
};
```

### LitSprite Shader Contract

When using `ShaderBase2D.LitSprite` (a user-provided effect set via `Batch2DConfig.LitSprite`), the renderer sets comprehensive lighting parameters.

#### Lighting Parameters

**Ambient Lighting:**

- `AmbientColor` (Vector4): Ambient light color and intensity

**Point Lights (dynamic count):**

- `PointLightPositions[]` (Vector2 array): World space positions
- `PointLightColors[]` (Vector4 array): RGBA colors
- `PointLightRadii[]` (float array): Maximum influence radius
- `PointFalloffs[]` (float array): Falloff exponent values
- `PointLightCount` (int): Number of active point lights

**Note:** The F# side has no constraints on the number of lights. Arrays are dynamically sized to accommodate however many lights you add. Your shader should declare array sizes based on the maximum number of lights you want to support (considering performance).

**Directional Lights (dynamic count):**

- `DirectionalLightDirections[]` (Vector2 array): Normalized direction vectors
- `DirectionalLightColors[]` (Vector4 array): RGBA colors
- `DirectionalLightCount` (int): Number of active directional lights

**Note:** Similar to point lights, directional lights have no inherent limit in the F# system. Your shader determines the maximum supported count.

**Light Index Buffer (Tiled Lighting):**

- `LightIndexBuffer` (Texture2D): Texture containing light indices per tile
- `TileSize` (float): World space size of each tile
- `TilesX` (float): Number of tiles horizontally
- `MaxLightsPerTile` (int): Maximum lights per tile
- `LightIndexBufferWidth` (float): Width of index buffer texture
- `LightIndexBufferHeight` (float): Height of index buffer texture

#### Camera and Viewport Parameters

- `ViewportSize` (Vector2): Current viewport dimensions
- `ViewportSizeInv` (Vector2): 1.0 / ViewportSize
- `ViewProjectionMatrix` (Matrix): Combined view and projection
- `InverseViewMatrix` (Matrix): Inverse of view matrix
- `InverseProjectionMatrix` (Matrix): Inverse of projection matrix

#### Shadow Parameters (Optional, when shadows enabled)

- `ShadowAtlas` (Texture2D): Shadow map texture
- `ShadowAtlasSize` (Vector2): Atlas dimensions
- `ShadowBias` (float): Depth bias to prevent shadow acne
- `ProjectionSize` (float): World space size for shadow projection
- `PointLightShadowIndices[]` (float array): Atlas row indices for point lights
- `DirectionalLightShadowIndices[]` (float array): Atlas row indices for directional lights

**Note:** Shadow index arrays are dynamically sized. Your shader should declare these based on your maximum light counts.

**Note:** Shadow parameters are only set when `Shadows2DConfig` is enabled.

#### Example LitSprite Implementation

The following is an **example** implementation showing one way to implement the LitSprite contract. You may implement it differently as long as you respect the parameter contract.

```hlsl
// Vertex shader transforms to clip space
VSOutput SpriteVS(VSInput input)
{
    VSOutput output;
    float4 worldPos = mul(input.Position, World);
    float4 viewPos = mul(worldPos, View);
    output.Position = mul(viewPos, Projection);
    
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    output.WorldPos = input.Position.xy;  // Pass world position for lighting
    return output;
}

// Pixel shader samples texture and applies lighting
float4 MainPS(VSOutput input) : COLOR0
{
    // Sample texture
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord) * input.Color;
    
    // Sample normal map
    float3 normal = tex2D(NormalSampler, input.TexCoord).rgb * 2.0 - 1.0;
    
    // Apply lighting calculations here...
    // Use AmbientColor, PointLights, DirectionalLights, etc.
    
    return float4(litColor, texColor.a);
}
```

### ShadowCaster Shader Contract

Shadow casters render occluder geometry to a shadow atlas. When you enable shadows via `Lighting2DConfig.Shadows`, you must provide a shadow caster shader via `Batch2DConfig.ShadowCaster` that conforms to this contract.

#### Vertex Input Structure

```hlsl
struct VSInput
{
    float2 Position : POSITION;
};
```

#### Light Parameters

**Point Light (Polar Projection):**

- `LightPosition` (Vector2): World space position
- `LightRadius` (float): Maximum range for depth calculation

**Directional Light (Orthographic Projection):**

- `LightDirection` (Vector2): Normalized direction vector
- `ShadowOrigin` (Vector2): World space origin for shadow map (snapped)
- `ProjectionSize` (float): World space half-extent of projection volume

**Detection:**

- `LightRadius < 0.0` indicates directional light
- `LightRadius >= 0.0` indicates point light

#### Atlas Parameters

- `AtlasWidth` (float): Width of shadow atlas (angular resolution for point lights)
- `AtlasHeight` (float): Height (number of light rows)

#### Vertex Output Structure

```hlsl
struct VSOutput
{
    float4 Position : SV_POSITION;
    float Depth : TEXCOORD0;
};
```

**Output Requirements:**

- `Position.x`: Projected to [-1, 1] based on light type (angle for point, perpendicular projection for directional)
- `Position.y`: Always 0.0 (each light gets its own row via viewport scissor)
- `Position.z`: Normalized depth [0, 1]
- `Depth`: Same as Position.z (passed through to pixel shader)

#### Example ShadowCaster Implementation

The following is an **example** implementation showing one way to implement the ShadowCaster contract. You may implement it differently as long as you respect the parameter contract.

```hlsl
VSOutput MainVS(VSInput input)
{
    VSOutput output;
    
    if (LightRadius < 0.0)
    {
        // Directional light - orthographic projection
        float2 perpDir = float2(LightDirection.y, -LightDirection.x);
        float2 relPos = input.Position - ShadowOrigin;
        float proj = dot(relPos, perpDir);
        output.Position.x = (proj / ProjectionSize + 1.0) * 2.0 - 1.0;
        
        float depth = (dot(relPos, LightDirection) + ProjectionSize) / (ProjectionSize * 2.0);
        output.Position.z = saturate(depth);
    }
    else
    {
        // Point light - polar projection
        float2 diff = input.Position - LightPosition;
        float angle = atan2(diff.y, diff.x);
        output.Position.x = angle / 3.14159;  // Map [-PI, PI] to [-1, 1]
        
        float depth = length(diff) / max(LightRadius, 0.001);
        output.Position.z = saturate(depth);
    }
    
    output.Position.y = 0.0;
    output.Depth = output.Position.z;
    return output;
}
```

### Post-Process Shader Contract

Post-processing effects operate on full-screen quads. When you configure post-processing via `Batch2DConfig.PostProcess`, you provide your own effect instances (e.g., vignette, bloom, color grading, or custom passes). These effects must conform to the parameter contracts described below.

**Important:** Mibo does not provide post-process shaders - you author them yourself. The 2D sample includes example implementations for reference.

#### Common Parameters

Post-process shaders may use custom parameters, but common ones include:

- `Time` (float): Current game time (set by custom pass SetupEffect)
- `Resolution` (Vector2): Render target dimensions (set by custom pass SetupEffect)
- `SceneTexture` (Texture2D): Input scene texture (name may vary)

#### Vertex Input/Output

```hlsl
struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};
```

**Transform:** Typically `Position` is already in clip space (-1 to 1), or apply `mul(Position, mul(View, Projection))`.

#### Built-in Post-Process Parameters

**Vignette:**

- `Radius` (float): Effect radius (default 0.7)
- `Softness` (float): Edge softness (default 0.3)

**Bloom:**

- `Threshold` (float): Brightness threshold for bloom extraction (default 0.8)
- `Intensity` (float): Bloom intensity (default 1.0)
- `Scatter` (float): Bloom scatter/spread (default 0.7)
- `BloomTexture` (Texture2D): Bloom texture input (for composite pass)

**Color Grading:**

- `LutTexture` (Texture3D): Lookup table texture
- `LutSize` (float): LUT dimension (default 32)
- `Blend` (float): Blend factor (0.0 = no grading, 1.0 = full grading, default 1.0)

#### Example Post-Process Implementation

The following is an **example** implementation showing one way to implement a post-process shader. You may implement it differently as long as you respect the parameter contract for your specific effect type.

```hlsl
VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, mul(View, Projection));
    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 color = tex2D(SceneSampler, input.TexCoord);
    
    // Apply post-processing effect here
    // Use custom parameters like Time, Resolution, etc.
    
    return color;
}
```

### Technique Naming

Mibo uses `SpriteBatch.Begin()` with your effect, which will apply the first pass of the first technique. Your shaders should define a technique named appropriately:

- For sprites/text: `technique SpriteBatch { pass P0 { ... } }`
- For shadow casters: `technique ShadowCaster { pass P0 { ... } }`
- For post-processing: Name appropriately (e.g., `technique BloomExtract`)

**Note:** Technique naming is a convention to make your shader code readable. Mibo simply applies the first technique's first pass.

### Safe Parameter Access

Mibo uses `SafeSetParam` which silently ignores missing parameters. This allows your shader to be compatible even if it doesn't use all parameters. Only declare the parameters you need.

### Choosing Light Limits

The F# lighting system has **no inherent limits** on the number of lights. However, your shader must declare maximum array sizes. Consider these factors when choosing limits:

- **GPU registers:** Each array element consumes registers
- **Performance:** More lights = more calculations per pixel
- **Use case:** UI overlays may need fewer lights than complex scenes

**Example shader configuration:**

```hlsl
// Define your maximums based on performance requirements
#define MAX_POINT_LIGHTS 32
#define MAX_DIRECTIONAL_LIGHTS 8
#define MAX_LIGHTS_PER_TILE 16

// Declare arrays with your chosen limits
float2 PointLightPositions[MAX_POINT_LIGHTS];
float4 PointLightColors[MAX_POINT_LIGHTS];
float PointLightRadii[MAX_POINT_LIGHTS];
float PointFalloffs[MAX_POINT_LIGHTS];

float2 DirectionalLightDirections[MAX_DIRECTIONAL_LIGHTS];
float4 DirectionalLightColors[MAX_DIRECTIONAL_LIGHTS];

float PointLightShadowIndices[MAX_POINT_LIGHTS];
float DirectionalLightShadowIndices[MAX_DIRECTIONAL_LIGHTS];
```

The system will only send data for the actual number of active lights, but your arrays should be large enough to handle your worst-case scenario.

### Shader Model Requirements

- OpenGL: `vs_3_0`, `ps_3_0`
- DirectX: `vs_5_0`, `ps_5_0` (or lower for compatibility)

## Custom GPU work (escape hatch)

The built-in 2D SpriteBatch renderer exposes `DrawCustom` for "do whatever you want" rendering.

```fsharp
Draw2D.custom
  (fun ctx ->
     // Raw GPU work (primitives, render targets, post-effects, etc.)
     // Note: SpriteBatch is ended before this runs, and restarted after.
     ())
  50<RenderLayer>
  buffer
```

If you need a fully bespoke pipeline, you can still implement your own `IRenderer<'Model>` and add it with `Program.withRenderer`.

See also: [Rendering overview](rendering.html) (overall renderer composition) and [Camera](camera.html).
