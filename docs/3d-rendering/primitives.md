---
title: Primitives
category: 3D Rendering
index: 12
---

# Primitives

The 3D pipeline provides primitives for lines, sprites, and billboards in 3D space. All are batched automatically.

## Lines

Lines draw 3D line segments for debug visualization, collision display, grids, and wireframes.

### Single Line

```fsharp
buffer.Line(p1: Vector3, p2: Vector3, color: Color)
```

Draws one line segment from p1 to p2:

```fsharp
// Debug ray visualization
buffer.Line(ray.Origin, ray.Origin + ray.Direction * 10f, Color.Yellow)

// Collision edge
buffer.Line(contact.Point, contact.Point + contact.Normal * 0.5f, Color.Red)
```

### Batched Lines

```fsharp
buffer.Lines(vertices: VertexPositionColor[], lineCount: int)
```

Efficient for drawing multiple connected or disconnected lines:

```fsharp
let gridVerts =
    [| for x in -5 .. 5 do
         for z in -5 .. 5 do
           if x % 5 = 0 || z % 5 = 0 then
             VertexPositionColor(Vector3(float32 x, 0f, float32 z), Color.Gray) |]

buffer.Lines(gridVerts, gridVerts.Length / 2)
```

### Custom Effect Lines

Use for glowing lines, dashed lines, or custom styling:

```fsharp
let glowEffect = ctx.Content.Load<Effect>("shaders/GlowLine")

let setupEffect (effect: Effect) (ctx: EffectContext) =
    effect.SafeSetParam("World", ctx.World)
    effect.SafeSetParam("View", ctx.View)
    effect.SafeSetParam("Projection", ctx.Projection)
    effect.SafeSetParam("GlowColor", Color.Cyan.ToVector3())
    effect.SafeSetParam("GlowIntensity", 1.5f)

buffer
    .LinesEffect(Opaque, glowEffect, ValueSome setupEffect, outlineVerts, outlineVerts.Length / 2)
```

### Line Batching

Lines are batched by pass. Batch flushes occur on:
- Pass change (Opaque → Transparent)
- Camera change
- Effect change

Lines within same pass are combined into single draw call.

## Sprite Quads

Quads are textured rectangles positioned in 3D space with custom orientation. Use the `quad` computation expression to create them.

### Creating Quads

```fsharp
let decal = 
    quad {
        at (Vector3(0f, 0.1f, 0f))
        onXZ (Vector2(4f, 4f))    // 4x4 units on ground
        color Color.White
    }

buffer.Quad(decalTexture, decal)
```

**Quad configuration:**

- `at position` - Sets center position
- `onXZ size` - Lies flat on ground (for decals, markers)
- `onXY size` - Stands upright (for signs, wall elements)
- `color c` - Tint color
- `uv rect` - UV coordinates for texture atlases
- `relativeTo matrix` - Parent transform

### Custom Orientation

For arbitrary orientations, use helper functions:

```fsharp
open Mibo.Rendering.Graphics3D.SpriteHelpers

// Quad facing arbitrary direction
let quad = quad3D center rightVector upVector
let tinted = quad |> withQuadColor Color.Gold
let atlased = quad |> withQuadUv (UvRect(0f, 0f, 0.5f, 0.5f))

buffer.Quad(texture, atlased)
```

## Billboards

Billboards are camera-facing sprites. Use the `billboard` computation expression to create them.

### Creating Billboards

```fsharp
let particle = 
    billboard {
        at (Vector3(0f, 5f, 0f))
        size (Vector2(1f, 1f))
        color Color.White
    }

buffer.Billboard(particleTexture, particle)
```

**Billboard configuration:**

- `at position` - Center position
- `size (Vector2(w, h))` - Width and height
- `color c` - Tint color
- `rotate angle` - Rotation around facing axis
- `facing Spherical` - Faces camera from all angles (default)
- `facing (Cylindrical upAxis)` - Rotates only around axis
- `uv rect` - UV coordinates
- `relativeTo matrix` - Parent transform

### Billboard Modes

**Spherical**: Always faces camera. Use for particles, UI, floating text.

**Cylindrical upAxis**: Rotates around up-axis only. Use for vertical-facing objects like grass or trees.

```fsharp
let uprightParticle = 
    billboard {
        at position
        size (Vector2(0.5f, 1f))
        facing (Cylindrical Vector3.Up)
    }

buffer.Billboard(foliageTexture, uprightParticle)
```

### Billboard Variants

Submit to different passes based on needs:

```fsharp
// Opaque billboard (rare, but fast for solid impostors)
buffer.BillboardOpaque(texture, billboard)

// Transparent billboard (default, sorted back-to-front)
buffer.Billboard(texture, billboard)
```

## Complete Example

Combining primitives in a view function:

```fsharp
let view ctx model (buffer: PipelineBuffer<RenderCommand>) =
    buffer
        .Camera(model.Camera)
        .Clear(Color.CornflowerBlue)
        
        // Draw grid lines
        .Lines(model.GridVerts, model.GridLineCount)
        
        // Draw debug lines
        .Line(model.PlayerPos, model.PlayerPos + model.Velocity, Color.Green)
        
        // Draw ground decals
        .Quad(model.DecalTexture, 
            quad { at model.MarkerPos; onXZ (Vector2(2f, 2f)) })
        
        // Draw particles
        .Billboard(model.ParticleTexture,
            billboard { at model.ExplosionPos; size (Vector2(3f, 3f)); color Color.Orange })
        
        .Submit()
```

## Automatic Batching

Primitives are automatically batched for performance:

**Batch by texture** - SpriteQuadBatch groups same-texture quads.

**Batch by effect** - Custom-effect draws flush on effect change.

**Distance sorting** - Transparent primitives sorted back-to-front.

**Pass separation** - Opaque and transparent draws separated.

Batch flush occurs on:
- Camera change
- Viewport change
- Texture change (sprites)
- Effect change (custom-effect draws)
- End of frame

See also: [Materials](materials.html), [Lighting](lighting.html), [Pipeline](pipeline.html)
