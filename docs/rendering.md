---
title: Rendering in Mibo
category: Rendering
categoryindex: 3
index: 10
---

# Rendering in Mibo

Mibo separates what you draw from how you draw it. Your `view` function decides what should appear on screen. An `IRenderer` handles the GPU work. This split lets you write pure view logic that is easy to test while keeping rendering details where they belong.

### There's no dispatch?

Traditional MVU frameworks let views emit messages through event handlers. Mibo does not. This is because unlike usual MVU setups, what you render on screen does not depend on user input or state changes. Instead, the `update` function and subscriptions handle those concerns.

```fsharp
// mibo usually requires a function like this
let view ctx model buffer =
    buffer.Draw(draw {
        // Your drawing code here
    })
      .Submit()
```

You can test this function by verifying the commands it produces. No graphics device required.

## The IRenderer Contract

`IRenderer<'Model>` is the seam between your code and the GPU:

```fsharp
type IRenderer<'Model> =
    abstract member Draw: GameContext * 'Model * GameTime -> unit
```

The runtime calls each registered renderer once per frame, passing the current model. How the renderer uses that model is up to its implementation. Some renderers accept a view function and translate model state into commands. Others access the model directly and issue draw calls immediately.

Register renderers when building your program:

```fsharp
Program.mkProgram init update
|> Program.withRenderer (fun game -> myRenderer :> IRenderer<Model>)
```

## Multiple Renderers

You can register multiple renderers. They execute in the order you added them.

Each renderer receives the same model instance but can interpret it differently. A common setup uses two renderers: one for a 3D world and another for 2D UI overlays.

```fsharp
Program.mkProgram init update
|> Program.withRenderer (fun game ->
    PipelineRenderer.create config worldView game :> IRenderer<Model>)
|> Program.withRenderer (fun game ->
    Batch2DRenderer.create uiView game :> IRenderer<Model>)
```

Renderers are independent unless you explicitly share state through the model. The first renderer can clear the screen, render a 3D scene, and leave the depth buffer. The second can then draw 2D elements on top. Or they can each clear and manage their own render targets. The runtime does not impose a specific policy.

## RenderBuffer Pattern

Built-in renderers like `Batch2DRenderer` and `PipelineRenderer` use a buffer-based approach:

1. The renderer creates a `RenderBuffer` specialized to its command types
2. Each frame it clears the buffer and calls your view function to fill it
3. It sorts and batches commands for efficient GPU submission
4. It executes the commands

The buffer type determines what commands you can submit. A 2D renderer uses `RenderBuffer<int<RenderLayer>, RenderCmd2D>`. A 3D pipeline renderer uses `RenderBuffer<unit, RenderCommand>`. The renderer's factory function captures your view and determines the buffer type you work with.

This pattern works well when you need sorting, batching, or complex scene management. The renderer handles those details. Your view stays focused on what to draw.

## Custom Renderers

If the built in renderers do not do the work you need, or you feel constrained and need direct GPU control, you may implement `IRenderer` yourself. You decide whether to use a buffer or draw immediately.

**Buffer approach** (when you need sorting or batching):

```fsharp
type BufferedRenderer<'Model>(game: Game, view) =
    let buffer = RenderBuffer<unit, MyCommand>()

    interface IRenderer<'Model> with
        member _.Draw(ctx, model, gameTime) =
            buffer.Clear()
            view ctx model buffer
            // Sort, batch, and execute commands
```

**Immediate approach** (when you want direct control):

```fsharp
type ImmediateRenderer(game: Game) =
    interface IRenderer<Model> with
        member _.Draw(ctx, model, gameTime) =
            // Direct GPU access
            ctx.GraphicsDevice.Clear(Color.CornflowerBlue)
            // Custom draw logic based on model
```

Both approaches are valid. Use what fits your rendering needs.

## Built-in Offerings

Mibo provides renderers that handle common scenarios so you can focus on game logic rather than rendering boilerplate:

- **Batch2DRenderer**: SpriteBatch-based 2D rendering with optional lighting, shadows, and post-processing
- **PipelineRenderer**: Advanced 3D pipeline with PBR materials, dynamic lighting, shadows, and post-processing

These are available if you want them. You are not required to use them. If they do not fit your needs, implement `IRenderer` and take full control.

See the dedicated pages for details on specific renderers and features:

- [Rendering 2D](rendering2d.html)
- [Rendering3D Overview](3d-rendering/overview.html)
- [Rendering3D: Materials](3d-rendering/materials.html)
- [Rendering3D: Lighting](3d-rendering/lighting.html)
- [Rendering3D: Primitives](3d-rendering/primitives.html)
- [Rendering3D: Post-Processing](3d-rendering/postprocessing.html)
- [Rendering3D: Custom Shaders](3d-rendering/custom-shaders.html)
- [Rendering3D: Pipeline](3d-rendering/pipeline.html)
- [Camera](camera.html)
- [Culling](culling.html)
- [Shaders](shaders.html)

**Note:** The older `Batch3DRenderer` is deprecated. Use `PipelineRenderer` for new 3D projects.
