module MiboSample.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open MiboSample.Domain
open MiboSample.Animation

// ─────────────────────────────────────────────────────────────
// Player Module: Platformer-specific player logic
// ─────────────────────────────────────────────────────────────

/// Update player-specific state (separate from physics)
let update (dt: float32) (model: Model) : struct (Model * Cmd<'Msg>) =
  // Update player animations based on current state
  Animation.update(dt, model), Cmd.none


/// Get the player's current animation state
let getAnimationState(model: Model) : AnimationState =
  Animation.getAnimationState
    model.IsGrounded
    model.PlayerVelocity
    model.Actions

// ─────────────────────────────────────────────────────────────
// Player View
// ─────────────────────────────────────────────────────────────

/// Draw the player character
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let animationState = getAnimationState model

  Animation.view
    ctx
    model.PlayerPosition
    model.PlayerFacing
    animationState
    model.PlayerAssets
    buffer

// ─────────────────────────────────────────────────────────────
// Player Utilities
// ─────────────────────────────────────────────────────────────

/// Get the player's bounding box for collision checks
let getBounds(model: Model) : Rectangle =
  let size = 64.0f

  Rectangle(
    int model.PlayerPosition.X,
    int model.PlayerPosition.Y,
    int size,
    int size
  )

/// Get the player's center position (for spawning effects, etc.)
let getCenter(model: Model) : Vector2 =
  let size = 64.0f

  Vector2(
    model.PlayerPosition.X + size / 2.0f,
    model.PlayerPosition.Y + size / 2.0f
  )

/// Get the player's "feet" position (for landing effects, dust particles, etc.)
let getFeetPosition(model: Model) : Vector2 =
  let size = 64.0f
  Vector2(model.PlayerPosition.X + size / 2.0f, model.PlayerPosition.Y + size)

/// Check if player is moving horizontally
let isMovingHorizontally(model: Model) : bool =
  abs model.PlayerVelocity.X > 10.0f

/// Check if player is in the air (not grounded and not climbing)
let isInAir(model: Model) : bool = not model.IsGrounded

/// Get player speed magnitude
let getSpeed(model: Model) : float32 = model.PlayerVelocity.Length()
