module MiboSample.Physics

open System
open Microsoft.Xna.Framework
open FSharp.UMX
open Mibo.Elmish
open Mibo.Input
open MiboSample.Domain

// ─────────────────────────────────────────────────────────────
// Physics System: Platformer movement and collision
// ─────────────────────────────────────────────────────────────

/// Apply gravity to velocity
let private applyGravity (velocity: Vector2) (dt: float32) : Vector2 =
  let newVy = velocity.Y + Constants.gravity * dt
  // Clamp to terminal velocity
  Vector2(velocity.X, min newVy Constants.maxFallSpeed)

/// Handle horizontal movement with input and acceleration
let private handleMovement
  (actions: ActionState<GameAction>)
  (velocity: Vector2)
  (isGrounded: bool)
  (dt: float32)
  : Vector2 =
  let acceleration =
    if isGrounded then
      Constants.groundAcceleration
    else
      Constants.airAcceleration

  let moveDirection =
    if actions.Held.Contains MoveLeft then -1.0f
    elif actions.Held.Contains MoveRight then 1.0f
    else 0.0f

  // Calculate target speed based on input
  let targetSpeed = moveDirection * Constants.moveSpeed

  // Calculate difference between current and target speed
  let speedDiff = targetSpeed - velocity.X

  // Calculate maximum change in speed for this frame
  let maxChange = acceleration * dt

  // Move velocity towards target speed
  let newVx =
    if abs speedDiff <= maxChange then
      targetSpeed // Snap to target if close enough
    else
      velocity.X + float32(sign speedDiff) * maxChange

  Vector2(newVx, velocity.Y)

/// Handle jumping with coyote time and jump buffering
let private handleJump
  (actions: ActionState<GameAction>)
  (isGrounded: bool)
  (isJumping: bool)
  (coyoteTimer: float32)
  (jumpBufferTimer: float32)
  (velocity: Vector2)
  (dt: float32)
  : struct (Vector2 * bool * float32 * float32) =

  // Update jump buffer (remember jump input for a short time)
  let jumpPressed = actions.Started.Contains Jump

  let newJumpBuffer =
    if jumpPressed then
      Constants.jumpBufferTime
    else
      max 0.0f jumpBufferTimer - dt

  // Check if we can jump (grounded or within coyote time window)
  let canJump = isGrounded || coyoteTimer > 0.0f

  // Perform jump if buffered and we can jump
  let (newVelocity, newIsJumping) =
    if newJumpBuffer > 0.0f && canJump && not isJumping then
      (Vector2(velocity.X, Constants.jumpSpeed), true)
    else
      // Check if we should stop the jump (player released jump button early for variable jump height)
      let shouldStopJump =
        isJumping && actions.Released.Contains Jump && velocity.Y > 0.0f

      if shouldStopJump then
        (Vector2(velocity.X, velocity.Y * 0.5f), false) // Cut velocity in half
      else
        (velocity, isJumping)

  // Update coyote timer (time since last grounded)
  let newCoyoteTimer =
    if isGrounded then
      Constants.coyoteTime
    else
      max 0.0f coyoteTimer - dt

  struct (newVelocity, newIsJumping, newCoyoteTimer, newJumpBuffer)

/// Check collision between a point and platform bounds
let private checkPointCollision(point: Vector2, platform: Platform) : bool =
  point.X >= float32 platform.Bounds.X
  && point.X <= float32(platform.Bounds.X + platform.Bounds.Width)
  && point.Y >= float32 platform.Bounds.Y
  && point.Y <= float32(platform.Bounds.Y + platform.Bounds.Height)

// Player collision bounds
let private playerWidth = 40.0f
let private playerHeight = 54.0f

/// Resolve horizontal collisions
let private resolveX
  (position: Vector2)
  (velocity: Vector2)
  (platforms: Platform array)
  : Vector2 =

  let mutable newPos = position

  let bounds =
    Rectangle(int newPos.X, int newPos.Y, int playerWidth, int playerHeight)

  for platform in platforms do
    let intersection = Rectangle.Intersect(bounds, platform.Bounds)

    if intersection.Width > 0 && intersection.Height > 0 then
      // Resolve X
      if velocity.X > 0.0f then
        // Moving right, snap to left
        newPos.X <- float32 platform.Bounds.X - playerWidth
      elif velocity.X < 0.0f then
        // Moving left, snap to right
        newPos.X <- float32(platform.Bounds.X + platform.Bounds.Width)

  newPos

/// Resolve vertical collisions

let private resolveY

  (position: Vector2)

  (velocity: Vector2)

  (previousPosition: Vector2)

  (platforms: Platform array)

  : struct (Vector2 * Vector2 * bool) =



  let mutable newPos = position

  let mutable newVelocity = velocity

  let mutable grounded = false

  let bounds =
    Rectangle(int newPos.X, int newPos.Y, int playerWidth, int playerHeight + 1) // +1 for ground check stability



  for platform in platforms do

    let intersection = Rectangle.Intersect(bounds, platform.Bounds)

    if intersection.Width > 0 && intersection.Height > 0 then

      // Check if we are landing on top

      let prevFeetY = previousPosition.Y + playerHeight

      let currFeetY = newPos.Y + playerHeight

      let platformTop = float32 platform.Bounds.Y

      let tolerance = 5.0f // Snap tolerance for high speed

      let restingTolerance = 1.0f // Tolerance for standing still



      let crossedSurface =
        prevFeetY <= platformTop + tolerance && currFeetY > platformTop

      let movingDown = velocity.Y >= 0.0f



      // Check if we are just standing on it (resting)

      // We are resting if feet are very close to top and not moving up

      let isResting =
        abs(currFeetY - platformTop) <= restingTolerance && velocity.Y >= -0.1f



      if (crossedSurface && movingDown) || isResting then

        // Landed or Staying Grounded

        newPos.Y <- float32 platform.Bounds.Y - playerHeight

        grounded <- true

      elif velocity.Y < 0.0f then

        // Hit head - stop upward movement

        newPos.Y <- float32(platform.Bounds.Y + platform.Bounds.Height)

        newVelocity.Y <- 0.0f



  struct (newPos, newVelocity, grounded)

/// Main physics update function
/// Integrates gravity, movement, jumping, and collision
let update (dt: float32) (model: Model) : struct (Model * Cmd<'Msg>) =
  let previousPosition = model.PlayerPosition

  // 1. Apply gravity (only when in air)
  let velocity =
    if model.IsGrounded then
      model.PlayerVelocity
    else
      applyGravity model.PlayerVelocity dt

  // 2. Handle movement input
  let velocity = handleMovement model.Actions velocity model.IsGrounded dt

  // 3. Handle jumping
  let struct (velocity, isJumping, coyoteTimer, jumpBufferTimer) =
    handleJump
      model.Actions
      model.IsGrounded
      model.IsJumping
      model.CoyoteTimer
      model.JumpBufferTimer
      velocity
      dt

  // 4. Axis-Separated Integration & Resolution

  // Phase X
  let posAfterX = previousPosition + Vector2(velocity.X * dt, 0.0f)
  let resolvedPosX = resolveX posAfterX velocity model.Platforms

  // Constrain to left side of map (camera view)
  let constrainedPosX =
    if resolvedPosX.X < model.CameraX then
      Vector2(model.CameraX, resolvedPosX.Y)
    else
      resolvedPosX

  // Phase Y
  let posAfterY = constrainedPosX + Vector2(0.0f, velocity.Y * dt)

  let struct (finalPos, velocityAfterY, isGrounded) =
    resolveY posAfterY velocity previousPosition model.Platforms

  // Update velocity after collision (grounded already handled, velocityAfterY handles head hits)
  let newVelocity =
    if isGrounded then
      Vector2(velocity.X, 0.0f)
    else
      velocityAfterY

  // Update facing direction
  let newFacing = Helpers.calculateFacing(newVelocity, model.PlayerFacing)

  // Reset jump state if grounded
  let finalIsJumping = if isGrounded then false else isJumping

  // Update model
  {
    model with
        PlayerPosition = finalPos
        PlayerVelocity = newVelocity
        PlayerFacing = newFacing
        IsGrounded = isGrounded
        IsJumping = finalIsJumping
        CoyoteTimer = coyoteTimer
        JumpBufferTimer = jumpBufferTimer
  },
  Cmd.none

/// Check if player has fallen off the world (below kill plane)
let checkKillPlane(model: Model) : bool =
  // Kill plane at 2x world height below zero
  let killY = float32 Constants.worldHeight * Constants.tileSize * 2.0f
  model.PlayerPosition.Y > killY

/// Respawn player at safe location
let respawnPlayer(model: Model) : Model =
  // Generate initial terrain so the player has somewhere to land
  // This is a bit heavy-handed for a simple property update,
  // but it ensures the world is consistent on respawn.
  // We'll handle the actual tile regeneration in the update loop or here.
  {
    model with
        PlayerPosition = Vector2(200.0f, 576.0f)
        PlayerVelocity = Vector2.Zero
        IsGrounded = true
        IsJumping = false
        CoyoteTimer = 0.0f
        JumpBufferTimer = 0.0f
        CameraX = 0.0f
  }
