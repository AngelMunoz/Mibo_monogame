module PipelineSample.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open PipelineSample

// ─────────────────────────────────────────────────────────────
// Player System: Respawn and rendering
// ─────────────────────────────────────────────────────────────

/// Check if player has fallen and respawn if needed
let checkRespawn<'Msg>(state: State) : struct (State * Cmd<'Msg>) =
  if state.PlayerPosition.Y <= Constants.fallLimit then
    {
      state with
          PlayerPosition = Vector3(0f, 2f, 0f)
          Velocity = Vector3.Zero
          IsGrounded = false
    },
    Cmd.none
  else
    state, Cmd.none

/// Render the player ball with rotation
let view
  (_ctx: GameContext)
  (state: State)
  (buffer: RenderBuffer<unit, RenderCommand>)
  : unit =
  
  // Using the new Render DSL
  View.render buffer {
      View.draw {
          mesh state.Assets.PlayerMesh
          at state.PlayerPosition
          rotatedBy state.Rotation
      }
  }
