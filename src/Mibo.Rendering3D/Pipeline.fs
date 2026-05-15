namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering3D.PipelineInternals

type IRenderPipeline3D =
  abstract member Initialize: GraphicsDevice -> unit

  abstract member Render:
    GameContext * RenderBuffer<unit, RenderCommand> * GameTime -> unit

module RenderPipeline3D =

  let create (config: Pipeline3DConfig) (game: Game) : IRenderPipeline3D =
    let state = State.create config

    { new IRenderPipeline3D with
        member _.Initialize(gd) = State.initialize state game gd

        member _.Render(ctx, buffer, gameTime) =
          Orchestrate.render state ctx buffer gameTime
    }
