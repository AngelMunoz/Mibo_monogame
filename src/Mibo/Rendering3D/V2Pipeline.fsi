namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

type IRenderPipeline3D =
  abstract member Initialize: GraphicsDevice -> unit

  abstract member Render:
    GameContext * RenderBuffer<unit, RenderCommand> * GameTime -> unit

module RenderPipeline3D =
  val create: config: Pipeline3DConfig -> game: Game -> IRenderPipeline3D
