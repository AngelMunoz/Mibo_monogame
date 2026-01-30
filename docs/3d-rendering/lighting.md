---
title: Lighting
category: 3D Rendering
index: 11
---

# Lighting

Rendering3D uses tiled forward rendering with dynamic light accumulation. Submit lights via `AddLight` command.

## Light Types

### Directional Light

```fsharp
type DirectionalLight = {
  Direction: Vector3
  Color: Color
  Intensity: float32
  Shadow: ShadowSettings voption
  CascadeCount: int
  CascadeSplits: float32[]
  SourceRadius: float32
}
```

Represents sun or moon. Infinite distance, parallel rays covering entire scene.

**Direction**: Normalized vector pointing toward scene (e.g., `Vector3(-1f, -1f, -1f)`).

**Color**: Light tint. Use white for neutral, or tint for mood.

**Intensity**: Brightness multiplier. Typical sunlight: 0.8 - 1.5.

**CascadeCount**: Shadow map cascades (0-4). More cascades = sharper shadows at range, more cost.

**CascadeSplits**: Manual cascade distribution (0-1 range). Adjust to concentrate resolution near camera.

**SourceRadius**: Light source size for soft shadows (PCSS).

Example:

```fsharp
let sun = Light.Directional {
    Direction = Vector3.Normalize(Vector3(-0.3f, -1f, -0.2f))
    Color = Color(255, 250, 240)
    Intensity = 1.0f
    Shadow = ValueSome { Bias = 0.001f; NormalBias = 0.02f }
    CascadeCount = 4
    CascadeSplits = [| 0.05f; 0.15f; 0.5f; 1f |]
    SourceRadius = 0.05f
}

buffer.Add((), AddLight sun)
```

### Point Light

```fsharp
type PointLight = {
  Position: Vector3
  Color: Color
  Intensity: float32
  Range: float32
  Shadow: ShadowSettings voption
  SourceRadius: float32
}
```

Omnidirectional light (bulb, torch, explosion). Falls off with distance.

**Position**: Light center in world space.

**Range**: Maximum influence distance. Light attenuates to zero at range.

**Shadow**: Optional. Uses 6-face cube map (expensive). Use sparingly.

Example:

```fsharp
let torch = Light.Point {
    Position = Vector3(2f, 1f, 3f)
    Color = Color(255, 200, 100)
    Intensity = 2.0f
    Range = 10f
    Shadow = ValueNone  // No shadow for performance
    SourceRadius = 0.02f
}

buffer.Add((), AddLight torch)
```

### Spot Light

```fsharp
type SpotLight = {
  Position: Vector3
  Direction: Vector3
  Color: Color
  Intensity: float32
  Range: float32
  InnerConeAngle: float32
  OuterConeAngle: float32
  Shadow: ShadowSettings voption
  SourceRadius: float32
}
```

Conical light (flashlight, street lamp, headlights).

**InnerConeAngle**: Full-brightness hotspot. Use `MathHelper.ToRadians(degrees)`.

**OuterConeAngle**: Falloff edge where intensity reaches zero. Larger than inner.

**Direction**: Normalized vector along cone center.

Example:

```fsharp
let flashlight = Light.Spot {
    Position = Vector3(0f, 1.5f, 0f)
    Direction = Vector3.Forward
    Color = Color(255, 255, 240)
    Intensity = 3.0f
    Range = 15f
    InnerConeAngle = MathHelper.ToRadians(10f)
    OuterConeAngle = MathHelper.ToRadians(25f)
    Shadow = ValueNone
    SourceRadius = 0.01f
}

buffer.Add((), AddLight flashlight)
```

## Lighting State

```fsharp
type LightingState = {
  AmbientColor: Color
  AmbientIntensity: float32
  Lights: Light[]
}
```

Container for all scene lights plus global ambient.

**AmbientColor**: Base illumination. Use desaturated dark colors (RGB 30-50) for realism.

**AmbientIntensity**: Ambient strength multiplier. Higher = brighter shadows.

**Lights**: Array of active lights. Maximum 31 per frame (tiled culling limit).

## Light Accumulation

Add lights per-frame:

```fsharp
// In view function
let lighting = {
    AmbientColor = Color(40, 40, 45)
    AmbientIntensity = 1f
    Lights = [||]
}

buffer.Add((), SetLighting lighting)

// Accumulate additional lights
lights |> Array.iter (fun light ->
    buffer.Add((), AddLight light)
)
```

Or use preset:

```fsharp
buffer.Add((), SetLighting Lighting.defaultSunlight)
```

## Shadow Settings

```fsharp
type ShadowSettings = {
  Bias: float32
  NormalBias: float32
}
```

Prevent shadow acne (self-shadowing artifacts).

**Bias**: Constant depth offset. Too high = detached shadows (Peter Panning). Too low = moire patterns.

**NormalBias**: Offset along surface normal. Fixes acne on curved surfaces.

Adjust per-light:

```fsharp
Light.Point {
    // ... other fields ...
    Shadow = ValueSome { Bias = 0.002f; NormalBias = 0.01f }
}
```

## Global Shadow Config

Configure shadow subsystem via `PipelineConfig`:

```fsharp
type ShadowConfig = {
  Resolution: int
  CascadeCount: int
  PCFSamples: int
  SoftShadows: SoftShadowConfig voption
  Bias: float32
  NormalBias: float32
  AtlasTiles: int
  MaxAtlasSize: int
}
```

**Resolution**: Per-tile shadow map size. Higher = sharper, more VRAM.

**AtlasTiles**: Grid size (N×N). Total slots = N². 4×4 = 16 slots.

**MaxAtlasSize**: Hard texture limit. Prevents VRAM blowout.

Enable shadows:

```fsharp
let config =
  PipelineConfig.defaults
  |> PipelineConfig.withShadows {
      Resolution = 2048
      CascadeCount = 4
      AtlasTiles = 4
      Bias = 0.005f
      NormalBias = 0.01f
      MaxAtlasSize = 8192
      // ... other fields
    }
```

## Light Culling

Renderer uses CPU-tiled forward culling:

- Screen divided into tiles (default: 32×32 pixels)
- Each tile stores bitmask of overlapping lights
- Pixel shader tests bitmask to determine active lights

Configure tile size:

```fsharp
PipelineConfig.withTileSize 32
```

**16**: Tighter culling, higher CPU. Good for many small lights.

**32**: Balanced default.

**64**: Faster CPU, looser. Good for fewer, larger lights.

See also: [Materials](materials.html), [Post-Processing](postprocessing.html)
