---
title: Materials
category: 3D Rendering
categoryindex: 4
index: 11
---

# Materials

Materials define how surfaces interact with light in the Rendering3D pipeline.

## PBR Material Properties

```fsharp
type PBRMaterial = {
  AlbedoColor: Color
  AlbedoMap: Texture2D voption
  NormalMap: Texture2D voption
  MetallicRoughnessMap: Texture2D voption
  Metallic: float32
  Roughness: float32
  AmbientOcclusionMap: Texture2D voption
  EmissiveColor: Color
  EmissiveIntensity: float32
}
```

### Properties

**AlbedoColor**: Base color of the surface. Use `Color.White` for texture-only materials.

**AlbedoMap**: Albedo texture (optional). Falls back to AlbedoColor if not set.

**NormalMap**: Surface detail normals for bump mapping. Use for adding fine geometry detail without additional polys.

**MetallicRoughnessMap**: Combined metallic/roughness map (optional). Green channel = roughness, Blue channel = metallic.

**Metallic**: 0.0 = dielectric (plastic, wood), 1.0 = conductor (metal). Default: 0.0.

**Roughness**: 0.0 = perfectly smooth (mirror-like), 1.0 = fully matte. Default: 0.5.

**AmbientOcclusionMap**: Darkens crevices and contact areas. Optional.

**EmissiveColor**: Self-illumination color. Use for glowing elements (LEDs, neon, fire).

**EmissiveIntensity**: Brightness multiplier for emissive. Higher = brighter glow.

## Material Flags

```fsharp
type MaterialFlags =
  | None = 0
  | CastsShadow = 1
  | ReceivesShadow = 2
  | Transparent = 4
  | DoubleSided = 8
  | Unlit = 16
  | AlphaTest = 32
```

**CastsShadow**: Object contributes to shadow maps. Required for shadow casting.

**ReceivesShadow**: Object is affected by shadows. Disabled for unlit materials.

**Transparent**: Enables alpha blending with `BlendState.AlphaBlend`. Required for semi-transparent surfaces.

**DoubleSided**: Disables backface culling. Use for thin geometry (foliage, fences, UI in 3D).

**Unlit**: Bypasses lighting calculations. Surface color appears at full brightness regardless of lights.

**AlphaTest**: Enables alpha testing for cutout materials (leaves, chain-link fences).

## Material Type

```fsharp
type Material = {
  PBR: PBRMaterial
  Flags: MaterialFlags
  AlphaThreshold: float32
  RenderQueue: int
}
```

**AlphaThreshold**: Cutoff value for alpha test (0.0 - 1.0). Used when `AlphaTest` flag is set.

**RenderQueue**: Draw order override. Lower values render first. Use for controlling transparency layering.

## Presets

```fsharp
module Material =
  let defaultOpaque: Material
  let unlit: Material
  let transparent: Material
```

**defaultOpaque**: Standard opaque material with shadows enabled. RenderQueue: 2000.

**unlit**: Self-illuminated surface, no lighting. Useful for UI in 3D, glow effects.

**transparent**: Alpha-blended material. RenderQueue: 3000. Depth writes disabled.

## Builders

```fsharp
Material.withAlbedo color
Material.withAlbedoMap tex
Material.withNormalMap tex
Material.withMetallic value
Material.withRoughness value
Material.withEmissive color intensity
Material.withFlags flags
```

## Effect Override

Override the default effect for a specific draw:

```fsharp
{ drawable with
    EffectOverride = ValueSome customEffect
}
```

Useful for:
- Toon/cel-shaded materials
- Holographic effects
- Custom shader techniques
- Performance-critical batched draws

## Example

```fsharp
let metallicFloor = {
  PBR = {
    AlbedoColor = Color(150, 150, 160)
    AlbedoMap = ValueSome floorTexture
    NormalMap = ValueSome floorNormal
    MetallicRoughnessMap = ValueNone
    Metallic = 0.8f
    Roughness = 0.3f
    AmbientOcclusionMap = ValueNone
    EmissiveColor = Color.Black
    EmissiveIntensity = 0f
  }
  Flags = MaterialFlags.CastsShadow ||| MaterialFlags.ReceivesShadow
  AlphaThreshold = 0.5f
  RenderQueue = 2000
}
```

See also: [Lighting](lighting.html), [Custom Shaders](custom-shaders.html)
