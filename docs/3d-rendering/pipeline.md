---
title: Pipeline
category: 3D Rendering
index: 10
---

# Pipeline

The 3D pipeline provides a fluent API for submitting render commands. Commands are collected in a `PipelineBuffer<RenderCommand>` and processed each frame for sorting, batching, and GPU submission.

## Using the Fluent API

The recommended way to build a frame is using the fluent extension methods:

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
                withMaterial e.Material
            }))
        .Submit()
```

Or use the `Buffer` module for a functional style:

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

Use the `draw` computation expression to create drawables with transforms and materials:

```fsharp
let playerDrawable =
    draw {
        mesh playerMesh
        at 0.0f 2.0f 0.0f
        withAlbedo Color.Gold
        withMetallic 1.0f
        withRoughness 0.3f
        withEmissive Color.Orange 2.0f
    }

buffer.Draw(playerDrawable)
```

Or create many drawables from a sequence:

```fsharp
model.Entities
|> Seq.map (fun e ->
    draw { mesh e.Mesh; at e.Position; withMaterial e.Material })
|> buffer.DrawMany
```

## Creating Quads and Billboards

Use the `quad` and `billboard` computation expressions for sprite rendering:

```fsharp
// Ground decal
let groundMarker =
    quad {
        at (Vector3(0f, 0.01f, 0f))
        onXZ (Vector2(2f, 2f))
        color Color.White
    }

buffer.Quad(decalTexture, groundMarker)

// Particle billboard
let particle =
    billboard {
        at (Vector3(1f, 2f, 1f))
        size (Vector2(0.5f, 0.5f))
        color Color.Yellow
    }

buffer.Billboard(particleTexture, particle)
```

## Command Reference

The following commands are available via the fluent API:

### Camera

```fsharp
buffer.Camera(camera: Camera)
```

Sets the view and projection matrices for subsequent draws. Flushes pending opaque/transparent batches.

### Lighting

```fsharp
buffer.Lighting(lighting: LightingState)
buffer.AddLight(light: Light)
```

Sets global lighting or adds individual lights. Maximum 31 lights per frame (tiled culling limit).

```fsharp
buffer
    .Lighting(Lighting.ambient)
    .AddLight(Light.Point { Position = p; Color = c; Intensity = 1.0f; Range = 10f })
```

### Viewport

```fsharp
buffer.Viewport(viewport: Viewport)
```

Changes viewport for multi-camera rendering (split-screen, minimaps).

### Clear

```fsharp
buffer.Clear(color: Color)
buffer.ClearTarget(color: Color, clearDepth: bool)
buffer.ClearDepth()
```

Clears render target. `ClearDepth()` clears only the depth buffer, useful for drawing overlays.

### Draw

```fsharp
buffer.Draw(drawable: Drawable voption)
buffer.DrawMany(drawables: seq<Drawable voption>)
```

Submits drawables created via the `draw` computation expression.

### Quads and Billboards

```fsharp
buffer.Quad(texture: Texture2D, quad: Quad3D)
buffer.QuadTransparent(texture: Texture2D, quad: Quad3D)
buffer.Billboard(texture: Texture2D, billboard: Billboard3D)
buffer.BillboardOpaque(texture: Texture2D, billboard: Billboard3D)
```

Submits textured quads and billboards. Use the `quad` and `billboard` computation expressions to create them.

### Lines

```fsharp
buffer.Line(p1: Vector3, p2: Vector3, color: Color)
buffer.Lines(verts: VertexPositionColor[], lineCount: int)
buffer.LinesEffect(pass, effect, setup, verts, lineCount)
```

Draws line segments for debug visualization, grids, and wireframes.

### Custom

```fsharp
buffer.Custom(drawFn: GraphicsDevice -> Camera -> unit)
```

Escape hatch for arbitrary GPU operations. Flushes pending batches.

```fsharp
buffer.Custom(fun gd cam ->
    gd.DepthStencilState <- DepthStencilState.None
    // Custom drawing...
)
```

## Pipeline Config

```fsharp
type PipelineConfig = {
  Shadows: ShadowConfig voption
  PostProcess: PostProcessConfig voption
  DefaultLighting: LightingState voption
  ShaderOverrides: Map<ShaderBase, string>
  PreRenderCallback: (GraphicsDevice -> Camera -> LightingState -> unit) voption
  LightingBinder: (Effect -> Camera -> LightingState -> unit) voption
  TileSize: int
}
```

**Shadows**: Shadow mapping configuration. `ValueNone` to disable.

**PostProcess**: Post-processing effects (bloom, SSAO, tone mapping).

**DefaultLighting**: Fallback lighting if no `SetLighting` command issued.

**ShaderOverrides**: Custom shader asset names.

**PreRenderCallback**: Execute before main pass (custom depth pre-pass, compute dispatch).

**LightingBinder**: Override light data binding to shaders.

**TileSize**: Screen-space tile size for light culling (default: 32).

### Configuration Examples

```fsharp
// Shadows + Bloom + ACES
let fullConfig =
  PipelineConfig.defaults
  |> PipelineConfig.withShadows {
      Resolution = 2048
      CascadeCount = 4
      AtlasTiles = 4
      Bias = 0.005f
      NormalBias = 0.01f
    }
  |> PipelineConfig.withPostProcess {
      SSAO = ValueNone
      Bloom = ValueSome {
          Threshold = 1.0f
          Intensity = 0.5f
          Scatter = 0.7f
      }
      ToneMapping = ToneMappingConfig.ACES
    }

// Custom shader override
let customConfig =
  PipelineConfig.defaults
  |> PipelineConfig.withShader ShaderBase.PBRForward "my_pbr_shader"
  |> PipelineConfig.withShader ShaderBase.Unlit "my_unlit_shader"
```

## Render Flow

### Per Frame

1. Reset pipeline state
2. Process commands:
   - Accumulate lights
   - Batch drawables by pass (opaque/transparent)
   - Track camera changes
3. Execute opaque pass:
   - Render shadow maps (if enabled)
   - Pack light data to textures
   - Draw opaque mesh batch
   - Draw opaque primitives batch
4. Execute transparent pass:
   - Sort back-to-front
   - Draw transparent mesh batch
   - Draw transparent primitives batch
5. Post-processing (if enabled):
   - Bloom (if enabled)
   - Tone mapping

### Batch Flush Conditions

Opaque and transparent batches flush on:
- Camera change
- Viewport change
- Effect change (custom-effect draws)
- End of opaque/transparent pass

Sprite/billboard/line batches flush on:
- Texture change
- Effect change
- Pass change

## Performance Tips

**Minimize state changes**: Group draws by camera, then by pass, then by texture.

**Frustum culling**: Enable for large scenes. Automatic for mesh drawables.

**Light limits**: Keep under 31 lights. Use light pools or spatial partitioning.

**Batch size**: Larger batches reduce draw calls. Pre-allocate buffer sizes.

**Shadow cost**: Shadows are expensive. Limit shadow-casting lights.

**Post-processing**: Bloom/SSAO add full-screen passes. Profile before enabling.

See also: [Overview](overview.html), [Materials](materials.html), [Lighting](lighting.html), [Primitives](primitives.html)
