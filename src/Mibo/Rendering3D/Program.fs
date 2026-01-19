namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Program Integration
// ============================================================================

/// <summary>
/// Internal renderer that wraps IRenderPipeline for Elmish-style integration.
/// Manages render buffer clearing and command submission each frame.
/// </summary>
type internal PipelineRenderer<'Model>
  (
    pipeline: IRenderPipeline,
    [<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<unit, RenderCommand> -> unit
  ) =

  let buffer = RenderBuffer<unit, RenderCommand>()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, gameTime: GameTime) =
      buffer.Clear()
      view ctx model buffer
      pipeline.Render(ctx, buffer, gameTime)

module PipelineRenderer =
  /// <summary>
  /// Create a pipeline-based renderer that integrates with Mibo's Elmish-style game loop.
  /// The renderer calls your view function each frame to populate a render buffer.
  /// </summary>
  /// <param name="game">The Game instance (used for device initialization).</param>
  /// <param name="view">Function that transforms model into render commands.</param>
  /// <param name="pipeline">The 3D render pipeline to use.</param>
  /// <returns>An IRenderer implementation for use with Program.withRenderer.</returns>
  let create
    (game: Game)
    (view: GameContext -> 'Model -> RenderBuffer<unit, RenderCommand> -> unit)
    (pipeline: IRenderPipeline)
    : IRenderer<'Model> =
    pipeline.Initialize(game.GraphicsDevice)
    PipelineRenderer(pipeline, view)
