# Deferred Rendering Lighting Debug Report

## Problem Summary

The deferred rendering pipeline shows **darkened models** and **no visible shadows** compared to Forward and Forward+ rendering modes which work correctly.

## Visual Comparison

| Mode | Appearance | Status |
|------|------------|--------|
| Forward | Bright cyan surfaces, visible shadow | ✓ Works |
| Forward+ | Bright cyan surfaces, visible shadow | ✓ Works |
| Deferred | Dark models, no shadow | ✗ **Broken** |

## What We've Confirmed

### F# Binding (100% Correct)
The F# code correctly:
- Binds 3 directional lights (padded from 1 actual light)
- Sets `LightDirections[0]` = `{-0.19, -0.96, -0.19}` (normalized)
- Sets `LightColors[0]` = `{0.8, 0.8, 0.8}`
- Binds shadow maps (Count=3)
- Creates G-buffer (albedo, normal, worldPos)
- Finds DeferredLighting shader

Console output confirms all parameters are SET successfully each frame.

### Shader Parameters Exist
`effect.Parameters["LightDirections"]` and `effect.Parameters["LightColors"]` are NOT null - `SetValue` is called successfully.

## The Issue

Despite correct F# binding, the `DeferredLighting.fx` shader does not produce directional light contribution. The scene only shows ambient light.

## Relevant Files

### Working Shader (Forward/Forward+)
`samples/PipelineSample/Content/Effects/PBR.fx` - Lines 130-135:
```hlsl
for(int i = 0; i < 3; i++)
{
    float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
    float atten = (i == 0) ? shadow : 1.0;
    diffuse += ndotl * LightColors[i] * atten;
}
```

### Broken Shader (Deferred)
`samples/PipelineSample/Content/Effects/DeferredLighting.fx` - Lines 112-117:
```hlsl
for(int i = 0; i < 3; i++)
{
    float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
    float atten = (i == 0) ? shadow : 1.0;
    diffuse += ndotl * LightColors[i] * atten;
}
```

**The code is IDENTICAL**, yet only DeferredLighting fails.

## Key Differences Between Shaders

| Aspect | PBR.fx (Works) | DeferredLighting.fx (Fails) |
|--------|----------------|----------------------------|
| Normal source | `normalize(input.Normal)` (vertex shader output) | `normalize(tex2D(NormalSampler, input.TexCoord).rgb)` (G-buffer texture) |
| World position | `input.WorldPos` (vertex shader output) | `tex2D(WorldPosSampler, input.TexCoord).rgb` (G-buffer texture) |
| When params set | Per-drawable, before each draw call | Once, before fullscreen quad |
| Draw method | Standard geometry rendering | Fullscreen quad post-process |

## What We've Tested

1. **Array padding** - Padded `LightDirections` and `LightColors` to exactly 3 elements. No effect.
2. **Direct parameter access** - Changed from helper function to direct `effect.Parameters["..."].SetValue()`. No effect.
3. **Point lights** - When enabled, point lights DO work in Deferred (proves G-buffer normal/worldPos are correct).

## Current Debug State

The shader currently has a **hardcoded light test** (not yet run):
```hlsl
// In DeferredLighting.fx, lines 112-124
float3 hardcodedLightDir = normalize(float3(-0.2, -1.0, -0.2));
float3 hardcodedLightColor = float3(0.8, 0.8, 0.8);
float ndotl = max(dot(normal, -hardcodedLightDir), 0.0);
diffuse += ndotl * hardcodedLightColor * shadow;
```

If this works, the issue is with how MonoGame passes array uniforms to this specific shader.

## Hypothesis

The `LightDirections` and `LightColors` array uniforms may not be correctly received by the shader despite `SetValue` being called. Possible causes:

1. **MonoGame array parameter handling** - The way `EffectParameter.SetValue(Vector3[])` maps to HLSL `float3[3]` might differ between per-drawable calls (Forward) and once-per-frame calls (Deferred).

2. **Shader constant buffer layout** - The fullscreen quad shader might have different constant buffer packing.

3. **Effect state** - Something about the effect state between G-buffer pass and lighting pass.

## To Reproduce

1. Run `dotnet run --project samples/PipelineSample`
2. Press 1 for Forward (works), 2 for Forward+ (works), 3 for Deferred (broken)
3. Observe: Forward/Forward+ show bright cyan with shadow, Deferred shows dark models

## Files to Review

- `samples/PipelineSample/Content/Effects/DeferredLighting.fx` - The broken shader
- `samples/PipelineSample/Content/Effects/PBR.fx` - The working shader (for comparison)
- `src/Mibo/Rendering3D/Pipeline.fs` - Lines 1124-1192 (`Deferred.bindLighting` and `Deferred.renderLighting`)
