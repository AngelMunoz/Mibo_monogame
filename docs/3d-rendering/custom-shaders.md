---
title: Custom Shaders
category: 3D Rendering
categoryindex: 4
index: 14
---

# Custom Shaders

The 3D pipeline uses shaders for all rendering stages. You can replace default shaders or provide custom effects for specific drawables.

## Shader Base Types

```fsharp
type ShaderBase =
  | PBRForward     // Main lit surface shader (PBR materials)
  | Unlit          // Self-illuminated, no lighting
  | ShadowCaster   // Depth-only shadow map rendering
  | Bloom          // Brightness extraction for bloom effect
  | PostProcess    // Final tone mapping and composite
```

## Overriding Default Shaders

Register custom shaders in `PipelineConfig`:

```fsharp
let config =
  PipelineConfig.defaults
  |> PipelineConfig.withShader ShaderBase.PBRForward "custom_toon_pbr"
  |> PipelineConfig.withShader ShaderBase.Unlit "custom_hologram"
```

Asset paths are relative to Content directory without extension.

## Per-Draw Effect Override

Override effect for individual drawables:

```fsharp
let customEffect = ctx.Content.Load<Effect>("shaders/CustomShader")

let drawable = Drawable.create mesh transform material
buffer.Add((), Draw { drawable with EffectOverride = ValueSome customEffect })
```

## Effect Setup Callback

Configure effect parameters per-draw:

```fsharp
type EffectSetup = Effect -> EffectContext -> unit

type EffectContext = {
  World: Matrix
  View: Matrix
  Projection: Matrix
}

let setupEffect (effect: Effect) (ctx: EffectContext) =
    effect.SafeSetParam("World", ctx.World)
    effect.SafeSetParam("View", ctx.View)
    effect.SafeSetParam("Projection", ctx.Projection)
    effect.SafeSetParam("CustomParam", 1.0f)

let drawable = {
    drawable with
        EffectOverride = ValueSome customEffect
        Setup = ValueSome setupEffect
}
```

The renderer invokes setup before each draw.

## Lighting Binder Override

Override how lighting data binds to shaders:

```fsharp
let customBinder (effect: Effect) (cam: Camera) (lighting: LightingState) =
    effect.SafeSetParam("AmbientColor", lighting.AmbientColor.ToVector3())
    effect.SafeSetParam("AmbientIntensity", lighting.AmbientIntensity)
    // Bind lights in custom format...

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withLightingBinder customBinder
```

Use only if you need custom light parameter layout. Default expects packed light textures.

## Pre-Render Callback

Execute custom logic before main pass:

```fsharp
let preRender (device: GraphicsDevice) (cam: Camera) (lighting: LightingState) =
    // Custom depth pre-pass, compute shaders, global uniform updates...

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPreRenderCallback preRender
```

## Shader Contracts

Your shaders must implement these parameter contracts. The pipeline sets these automatically before drawing.

### Common Parameters (All Shaders)

**Transforms:**
- `World` (Matrix): Object transform
- `View` (Matrix): Camera view matrix
- `Projection` (Matrix): Camera projection matrix

**Bones (for skinned meshes):**
- `Bones` (Matrix[]): Bone transform array

### PBRForward Shader Contract

Used for standard PBR materials.

**Material:**
- `AlbedoColor` (Vector4): Base color with alpha
- `Metallic` (float): Metalness (0-1)
- `Roughness` (float): Surface roughness (0-1)
- `EmissiveColor` (Vector3): Self-illumination color
- `EmissiveIntensity` (float): Emissive multiplier
- `Intensity` (float): Alias for EmissiveIntensity (for HDR/unlit effects)

**Textures:**
- `AlbedoMap` (Texture2D): Base color texture
- `NormalMap` (Texture2D): Surface normal map
- `MetallicRoughnessMap` (Texture2D): Packed metallic/roughness
- `AmbientOcclusionMap` (Texture2D): AO texture
- `HasAlbedoMap` (float): 1.0 if albedo map present, 0.0 otherwise

**Lighting:**
- `AmbientColor` (Vector3): Global ambient color
- `AmbientIntensity` (float): Ambient multiplier
- `LightDataTexture` (Texture2D): Packed light data (4xN texture, see format below)
- `LightCount` (float): Number of active lights

**Shadows:**
- `ShadowAtlas` (Texture2D): Shadow map atlas
- `ShadowAtlasTilesX` (float): Number of tiles across atlas
- `ShadowAtlasSize` (float): Atlas dimension in pixels
- `ShadowMatrixTexture` (Texture2D): View/projection matrices (4x(N*2) texture)
- `ShadowMatrixCount` (float): Number of shadow matrices * 2
- `ShadowBias` (float): Depth bias for shadow acne prevention
- `ShadowNormalBias` (float): Normal-based bias

### Unlit Shader Contract

Used for self-illuminated materials.

**Material:**
- `AlbedoColor` (Vector4): Color with alpha
- `EmissiveIntensity` (float): Intensity multiplier
- `Intensity` (float): Alias for EmissiveIntensity

**Textures:**
- `AlbedoMap` (Texture2D): Base texture
- `HasAlbedoMap` (float): 1.0 if texture present

**Note:** Unlit materials don't receive lighting or shadows.

### ShadowCaster Shader Contract

Used for rendering depth to shadow maps.

**Transforms:**
- `World` (Matrix): Object transform
- `View` (Matrix): Light view matrix
- `Projection` (Matrix): Light projection matrix

**Bones:**
- `Bones` (Matrix[]): For skinned shadow casters

**Output:** Write depth to color buffer. The pipeline expects normalized depth [0,1] in the red channel.

### Bloom Shader Contract

Used for brightness extraction and blur passes.

**Parameters:**
- `Threshold` (float): Brightness threshold for extraction (default 0.8)
- `Intensity` (float): Bloom intensity multiplier (default 1.0)
- `SceneTexture` (Texture2D): Input scene texture

**For Composite Pass:**
- `BloomTexture` (Texture2D): Blurred bloom texture to add to scene

### PostProcess Shader Contract

Used for final tone mapping and effects.

**Parameters:**
- `SceneTexture` (Texture2D): Input scene render target
- `ToneMapping` (float): Mode (0=None, 1=Reinhard, 2=ACES, 3=Filmic, 4=AgX)
- `Time` (float): Game time in seconds
- `BloomTexture` (Texture2D): Optional bloom input (if bloom enabled)

## Light Data Texture Format

The pipeline packs lights into a `LightDataTexture` (4xN Vector4 texture):

**Each light occupies one row (4 Vector4s):**

**Row N (Light type and params):**
- X: Light type (0=Directional, 1=Point, 2=Spot)
- Y: Intensity
- Z: Range (Point/Spot only)
- W: Shadow map index (-1 if no shadow)

**Row N+1 (Position/Spot params):**
- X, Y, Z: Position (Point/Spot) or unused (Directional)
- W: cos(OuterConeAngle) for spot lights

**Row N+2 (Direction/Spot params):**
- X, Y, Z: Direction (Directional/Spot)
- W: cos(InnerConeAngle) for spot lights

**Row N+3 (Color and size):**
- X, Y, Z: Color
- W: Source radius (for soft shadows)

## Shadow Matrix Texture Format

Shadow view and projection matrices are packed into `ShadowMatrixTexture` (4x(N*2) Vector4 texture):

- Rows 0-3: First shadow view matrix (4 Vector4s for 4x4 matrix)
- Rows 4-7: First shadow projection matrix
- Rows 8-11: Second shadow view matrix
- etc.

Point lights use 6 consecutive entries (one per cube face).

## Minimal PBR Shader Example

```hlsl
matrix World;
matrix View;
matrix Projection;
float4 AlbedoColor;
texture2D AlbedoMap;
float HasAlbedoMap;

struct VSInput
{
    float3 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    float4 worldPos = mul(float4(input.Position, 1), World);
    float4 viewPos = mul(worldPos, View);
    output.Position = mul(viewPos, Projection);
    output.UV = input.UV;
    return output;
}

float4 PSMain(VSOutput input) : COLOR0
{
    float4 color = AlbedoColor;
    if (HasAlbedoMap > 0.5)
        color *= tex2D(AlbedoMapSampler, input.UV);
    return color;
}

technique PBRForward
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VSMain();
        PixelShader = compile ps_4_0_level_9_1 PSMain();
    }
}
```

See also: [Shaders](../shaders.html), [Materials](materials.html), [Lighting](lighting.html)
