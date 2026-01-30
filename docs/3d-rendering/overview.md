---
title: Rendering 3D
category: Rendering
index: 12
---

# Rendering3D Overview

The `PipelineRenderer` provides a complete 3D rendering pipeline with PBR materials, dynamic lighting, shadows, and post-processing effects. It handles the complex GPU state management so you can focus on what to render rather than how to render it.

## What It Provides

- **PBR Materials**: Albedo, normal, metallic, roughness, and ambient occlusion maps
- **Dynamic Lighting**: Tiled forward rendering supporting up to 31 dynamic lights per frame
- **Shadows**: Cascaded shadow maps for directional lights, cube maps for point lights
- **Post-Processing**: Bloom, SSAO, tone mapping with ACES/Filmic/AgX algorithms
- **Batching**: Automatic batching for drawables, sprites, billboards, and lines
- **Culling**: Frustum culling and distance-based light culling

These features are available if your project needs them. You are not required to use all of them.

## Getting Started

A minimal setup using the fluent API:

```fsharp
open Mibo.Rendering.Graphics3D

let view (ctx: GameContext) (model: Model) (buffer: PipelineBuffer<RenderCommand>) =
    buffer
        .Camera(model.Camera)
        .Clear(Color.CornflowerBlue)
        .ClearDepth()
        .Lighting(model.Lighting)
        .DrawMany(model.Entities |> Seq.map (fun e ->
            draw {
                mesh e.Mesh
                at e.Position
                withAlbedo Color.White
                withMaterial e.Material
            }))
        .Submit()

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPostProcess {
      PostProcessConfig.defaults with
          ToneMapping = ToneMappingConfig.ACES
    }

Program.mkProgram init update
|> Program.withPipeline config view
```

The view function receives a `PipelineBuffer<RenderCommand>` (an alias for `RenderBuffer<unit, RenderCommand>`). Use the fluent API to chain commands, or use the `Buffer` module for a functional style:

```fsharp
let view ctx model buffer =
    buffer
    |> Buffer.camera model.Camera
    |> Buffer.clear Color.CornflowerBlue
    |> Buffer.clearDepth
    |> Buffer.lighting model.Lighting
    |> Buffer.drawMany (model.Entities |> Seq.map createDrawable)
    |> Buffer.submit
```

## Creating Drawables

Use the `draw` computation expression to create drawables:

```fsharp
let drawable =
    draw {
        mesh playerMesh
        at 0.0f 2.0f 0.0f
        withAlbedo Color.Gold
        withMetallic 1.0f
        withRoughness 0.3f
        withEmissive Color.Orange 2.0f
    }

buffer.Draw(drawable)
```

Or use the fluent `Draw` method which accepts `Drawable voption`:

```fsharp
model.Entities
|> Seq.map (fun e ->
    draw { mesh e.Mesh; at e.Position; withMaterial e.Material })
|> buffer.DrawMany
```

## When to Use It

Use the 3D pipeline when you want:
- Realistic PBR materials and lighting
- Dynamic shadows from multiple light sources
- Post-processing effects
- Automatic batching and culling for performance
- Custom shader integration

For simpler 3D needs, you might prefer a custom `IRenderer` implementation that draws directly. For 2D games, use `Batch2DRenderer` instead.

## Combining with Other Renderers

You can combine the 3D pipeline with other renderers:

```fsharp
Program.mkProgram init update
|> Program.withPipeline config3d view3d
|> Program.withRenderer (fun game -> Batch2DRenderer.create view game)
```

The pipeline runs in registration order. The 3D renderer typically clears the screen and renders the world. A 2D UI renderer can then draw on top.

## Configuration

Configure the pipeline via `PipelineConfig`:

```fsharp
let config =
  PipelineConfig.defaults
  |> PipelineConfig.withShadows (ShadowConfig.defaults |> ShadowConfig.withBias 0.001f 0.005f)
  |> PipelineConfig.withPostProcess (
      PostProcessConfig.defaults
      |> PostProcessConfig.withBloom { Threshold = 0.9f; Intensity = 1.5f; Scatter = 0.7f }
      |> PostProcessConfig.withToneMapping ACES
  )
  |> PipelineConfig.withDefaultLighting {
      AmbientColor = Color(20, 20, 30)
      AmbientIntensity = 0.5f
      Lights = [| Light.Directional { ... } |]
  }
  |> PipelineConfig.withShader ShaderBase.PBRForward "Effects/PBR"
  |> PipelineConfig.withShader ShaderBase.ShadowCaster "Effects/ShadowCaster"
```

See [Pipeline](pipeline.html) for the complete command reference.

See also: [Camera](../camera.html), [Lighting](lighting.html), [Materials](materials.html), [Custom Shaders](custom-shaders.html)
