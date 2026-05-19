module _3DSample.Systems.Rotation

open Microsoft.Xna.Framework
open Mibo.Elmish
open _3DSample.Domain

// ============================================================================
// Rotation System: Ball rolling based on velocity
// ============================================================================

/// <summary>Compute rotation delta from velocity (rolling ball effect).</summary>
let private computeRollDelta (dt: float32) (velocity: Vector3) : Quaternion =
  let rollX = velocity.Z * dt * Constants.rollSpeed
  let rollZ = -velocity.X * dt * Constants.rollSpeed
  Quaternion.CreateFromYawPitchRoll(0f, rollX, rollZ)

/// <summary>Rotation system update: applies rolling rotation based on velocity.</summary>
/// <remarks>Ball keeps rotating even when airborne for momentum effect.</remarks>
let update<'Msg> (dt: float32) (state: State) : struct (State * Cmd<'Msg>) =
  let rotationDelta = computeRollDelta dt state.Velocity
  let newRotation = Quaternion.Concatenate(state.Rotation, rotationDelta)

  { state with Rotation = newRotation }, Cmd.none
