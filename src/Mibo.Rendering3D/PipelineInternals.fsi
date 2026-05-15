namespace Mibo.Rendering3D.PipelineInternals

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering3D

[<Sealed>]
type DeviceContext

[<Sealed>]
type FrameContext

[<Sealed>]
type BindingCache

[<Sealed>]
type PipelineState

module State =
  val create: config: Pipeline3DConfig -> PipelineState

  val initialize:
    state: PipelineState -> game: Game -> gd: GraphicsDevice -> unit

module Orchestrate =
  val render:
    state: PipelineState ->
    gameCtx: GameContext ->
    buffer: RenderBuffer<unit, RenderCommand> ->
    gameTime: GameTime ->
      unit
