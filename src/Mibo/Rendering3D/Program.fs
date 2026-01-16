namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Program Integration
// ============================================================================

/// Internal renderer that wraps IRenderPipeline
type internal PipelineRenderer<'Model>
  (
    pipeline: IRenderPipeline,
    [<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<unit, RenderCommand> -> unit
  ) =

  let buffer = RenderBuffer<unit, RenderCommand>()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, _gameTime: GameTime) =
      buffer.Clear()
      view ctx model buffer
      pipeline.Render(ctx, buffer)

module PipelineRenderer =
  let create
    (game: Game)
    (view: GameContext -> 'Model -> RenderBuffer<unit, RenderCommand> -> unit)
    (pipeline: IRenderPipeline)
    : IRenderer<'Model> =
    pipeline.Initialize(game.GraphicsDevice)
    PipelineRenderer(pipeline, view)
