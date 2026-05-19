module _3DSample.Systems.Respawn

open Microsoft.Xna.Framework
open Mibo.Elmish
open _3DSample.Domain

// ============================================================================
// Respawn System: Fall detection and player reset
// ============================================================================

/// <summary>Check if player has fallen below the fall limit and respawn if needed.</summary>
let update<'Msg>(state: State) : struct (State * Cmd<'Msg>) =
  if state.PlayerPosition.Y <= Constants.fallLimit then
    {
      state with
          PlayerPosition = Vector3(24f, 2f, 24f)
          Velocity = Vector3.Zero
          IsGrounded = false
    },
    Cmd.none
  else
    state, Cmd.none
