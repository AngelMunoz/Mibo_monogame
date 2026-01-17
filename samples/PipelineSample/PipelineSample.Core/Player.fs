module PipelineSample.Core.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open PipelineSample.Core

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
