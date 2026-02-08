module MiboSample.Animation

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Animation
open Mibo.Input
open MiboSample.Domain

// ─────────────────────────────────────────────────────────────
// Animation State Management
// ─────────────────────────────────────────────────────────────

/// Current animation state for the player
type AnimationState =
  | Idle
  | Walk
  | Jump
  | Fall
  | Climb

// ─────────────────────────────────────────────────────────────
// Animation Update System
// ─────────────────────────────────────────────────────────────

/// Determine which animation state should be active based on player state
let getAnimationState
  (isGrounded: bool)
  (velocity: Vector2)
  (actions: ActionState<GameAction>)
  : AnimationState =

  if not isGrounded then
    if velocity.Y > 0.0f then Fall else Jump
  elif velocity.LengthSquared() > 100.0f then
    Walk
  else
    Idle

/// Get the appropriate animated sprite for the current state
let getCurrentSprite
  (assets: PlayerAssets)
  (state: AnimationState)
  : AnimatedSprite =
  match state with
  | Idle -> assets.Idle
  | Walk -> assets.Walk
  | Jump -> assets.Jump
  | Fall -> assets.Fall
  | Climb -> assets.Jump // Use jump frame for climbing for now

/// Update animation based on player state
let update(dt: float32, model: Model) : Model =
  let animationState =
    getAnimationState model.IsGrounded model.PlayerVelocity model.Actions

  // Update only the active sprite to advance its frame timer
  let assets = model.PlayerAssets

  let newAssets =
    match animationState with
    | Idle -> {
        assets with
            Idle = AnimatedSprite.update dt assets.Idle
      }
    | Walk -> {
        assets with
            Walk = AnimatedSprite.update dt assets.Walk
      }
    | Jump -> {
        assets with
            Jump = AnimatedSprite.update dt assets.Jump
      }
    | Fall -> {
        assets with
            Fall = AnimatedSprite.update dt assets.Fall
      }
    | Climb ->
        {
          assets with
              Jump = AnimatedSprite.update dt assets.Jump
        }

  { model with PlayerAssets = newAssets }

// ─────────────────────────────────────────────────────────────
// Animation View
// ─────────────────────────────────────────────────────────────

/// Draw the player sprite with proper facing and animation
let view
  (ctx: GameContext)
  (position: Vector2)
  (facing: float32)
  (animationState: AnimationState)
  (assets: PlayerAssets)
  (buffer: RenderBuffer<RenderCmd2D>)
  =
  let currentSprite = getCurrentSprite assets animationState

  // Apply facing direction (FlipX) and scale
  // Character sprites are 128x128, scale by 0.5 for 64x64 visual size
  let sprite =
    currentSprite
    |> AnimatedSprite.withScale 0.5f
    |> if facing < 0.0f then
         AnimatedSprite.facingLeft
       else
         AnimatedSprite.facingRight

  // Draw the sprite at player position
  // Position is Top-Left of collision box (40x54).
  // Sprite origin is Bottom-Center (feet).
  // We want to align feet to Bottom-Center of collision box (Pos + (20, 54)).
  let drawPos = position + Vector2(20.0f, 54.0f)

  // Keep the player above terrain/decorations.
  AnimatedSprite.draw drawPos 2<RenderLayer> buffer sprite

// ─────────────────────────────────────────────────────────────
// Animation Utilities
// ─────────────────────────────────────────────────────────────

/// Check if animation just completed (useful for one-shot animations)
let isAnimationComplete(sprite: AnimatedSprite) : bool =
  // This would need to be implemented in the Mibo.Animation module
  // For now, return false as placeholder
  false

/// Reset animation to start
let resetAnimation(sprite: AnimatedSprite) : AnimatedSprite =
  // This would need to be implemented in the Mibo.Animation module
  // For now, return sprite unchanged
  sprite

/// Get current animation frame index (for effects, etc.)
let getCurrentFrame(sprite: AnimatedSprite) : int =
  // This would need to be implemented in the Mibo.Animation module
  // For now, return 0 as placeholder
  0
