---
title: Shaders
category: Rendering
categoryindex: 3
index: 15
---

# Shaders

Shaders are GPU programs that transform vertices and determine pixel colors. They run on the graphics card in parallel, making them efficient for complex visual effects.

## What They Are

- **Vertex shaders** transform 3D model vertices into screen space
- **Pixel shaders** (also called fragment shaders) determine the final color of each pixel
- **Effects** (in MonoGame terminology) package shaders with parameters and techniques

## Why Use Them

Use shaders when you need visual effects beyond what built-in rendering provides:

- Custom lighting models (toon shading, stylized PBR)
- Post-processing effects (bloom, tone mapping, color grading)
- Special effects (holograms, distortion, pixelation)
- Optimized rendering for specific art styles

## When to Write Them

You don't need custom shaders to start. Mibo's built-in renderers work without them:

- **2D games**: Use `Batch2DRenderer` with standard SpriteBatch (no shaders required)
- **3D games**: Use `PipelineRenderer` with BasicEffect fallback (works without custom shaders)

Write shaders when:
- You have specific visual requirements
- You need performance optimizations for your target hardware
- You're building advanced rendering features

## Loading Shaders

Shaders are `.fx` HLSL files compiled to `.xnb` via the MonoGame Content Pipeline.

```fsharp
// Load an effect from your Content directory
let myEffect = ctx.Content.Load<Effect>("shaders/MyEffect")
```

Asset paths are relative to your Content directory without extension.

## Setting Parameters

Set shader parameters before drawing:

```fsharp
effect.Parameters["World"].SetValue(worldMatrix)
effect.Parameters["Color"].SetValue(color)

// Or use Mibo's SafeSetParam (ignores missing parameters)
effect.SafeSetParam("World", worldMatrix)
```

## Where to Learn More

Shader binding contracts (what parameters each shader type expects) are documented in the rendering sections:

- **2D rendering shaders**: See [Rendering 2D](rendering2d.html#shader-contracts) for LitSprite, ShadowCaster, and Post-Process contracts
- **3D rendering shaders**: See [3D Custom Shaders](3d-rendering/custom-shaders.html) for PBRForward, Unlit, ShadowCaster, Bloom, and PostProcess contracts

See also: [MonoGame Effects Documentation](https://docs.monogame.net/articles/content/custom_effects.html)
