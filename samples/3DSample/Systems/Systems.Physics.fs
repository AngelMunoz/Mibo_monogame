module _3DSample.Systems.Physics

open Microsoft.Xna.Framework
open Mibo.Elmish
open Mibo.Input
open Mibo.Layout3D
open _3DSample.Domain

// ============================================================================
// Physics System: Gravity, jump, position updates, collisions
// ============================================================================

/// <summary>Apply gravity to velocity.</summary>
let private applyGravity (dt: float32) (velocity: Vector3) : Vector3 =
  velocity + Vector3(0f, Constants.gravity * dt, 0f)

/// <summary>Apply jump if grounded and jump action started.</summary>
let private applyJump(state: State) : Vector3 =
  if state.IsGrounded && state.Actions.Started.Contains Jump then
    Vector3(state.Velocity.X, Constants.jumpSpeed, state.Velocity.Z)
  else
    state.Velocity

/// <summary>Get player radius from bounds.</summary>
let private getPlayerRadius(assets: GameAssets) : float32 =
  (assets.PlayerBounds.Max.Y - assets.PlayerBounds.Min.Y) / 2f

/// <summary>Check if a cell exists at the given grid position.</summary>
let private hasCell (x: int) (y: int) (z: int) (grid: CellGrid3D<Cell>) : bool =
  match CellGrid3D.get x y z grid with
  | ValueSome _ -> true
  | ValueNone -> false

/// <summary>Convert world position to grid coordinates.</summary>
let private worldToGrid
  (pos: Vector3)
  (grid: CellGrid3D<Cell>)
  : struct (int * int * int) =
  let gx = int((pos.X - grid.Origin.X) / grid.CellSize.X)
  let gy = int((pos.Y - grid.Origin.Y) / grid.CellSize.Y)
  let gz = int((pos.Z - grid.Origin.Z) / grid.CellSize.Z)
  struct (gx, gy, gz)

/// <summary>Full 3D collision check - handles ground, ceiling, and walls.</summary>
let private checkGridCollision
  (playerRadius: float32)
  (prevPos: Vector3)
  (newPos: Vector3)
  (velocity: Vector3)
  (grid: CellGrid3D<Cell>)
  : struct (Vector3 * Vector3 * bool) =

  let mutable pos = newPos
  let mutable vel = velocity
  let mutable grounded = false

  let feetY = pos.Y - playerRadius
  let struct (gx, _, gz) = worldToGrid pos grid
  let feetGridY = int((feetY - grid.Origin.Y) / grid.CellSize.Y)

  for dx in -1 .. 1 do
    for dz in -1 .. 1 do
      let checkX = gx + dx
      let checkZ = gz + dz

      let cellMinX = grid.Origin.X + float32 checkX * grid.CellSize.X
      let cellMaxX = cellMinX + grid.CellSize.X
      let cellMinZ = grid.Origin.Z + float32 checkZ * grid.CellSize.Z
      let cellMaxZ = cellMinZ + grid.CellSize.Z

      let overlapX =
        pos.X + playerRadius > cellMinX && pos.X - playerRadius < cellMaxX

      let overlapZ =
        pos.Z + playerRadius > cellMinZ && pos.Z - playerRadius < cellMaxZ

      if overlapX && overlapZ then
        for checkY in feetGridY .. -1 .. max 0 (feetGridY - 2) do
          if hasCell checkX checkY checkZ grid && not grounded then
            let cellTop = grid.Origin.Y + float32(checkY + 1) * grid.CellSize.Y

            if
              vel.Y <= 0f
              && prevPos.Y - playerRadius >= cellTop - 0.1f
              && feetY < cellTop
            then
              pos <- Vector3(pos.X, cellTop + playerRadius, pos.Z)
              vel <- Vector3(vel.X, 0f, vel.Z)
              grounded <- true

        let headY = pos.Y + playerRadius
        let headGridY = int((headY - grid.Origin.Y) / grid.CellSize.Y)

        for checkY in headGridY .. headGridY + 1 do
          if hasCell checkX checkY checkZ grid then
            let cellBottom = grid.Origin.Y + float32 checkY * grid.CellSize.Y

            if
              vel.Y > 0f
              && prevPos.Y + playerRadius <= cellBottom + 0.1f
              && headY > cellBottom
            then
              pos <- Vector3(pos.X, cellBottom - playerRadius, pos.Z)
              vel <- Vector3(vel.X, 0f, vel.Z)

  struct (pos, vel, grounded)

/// <summary>Physics system update: gravity, jump, position, collision.</summary>
let update<'Msg> (dt: float32) (state: State) : struct (State * Cmd<'Msg>) =
  let playerRadius = getPlayerRadius state.Assets

  let velocity = applyJump state
  let velocity = applyGravity dt velocity
  let newPos = state.PlayerPosition + velocity * dt

  let struct (finalPos, finalVel, grounded) =
    checkGridCollision
      playerRadius
      state.PlayerPosition
      newPos
      velocity
      state.LevelGrid

  {
    state with
        PlayerPosition = finalPos
        Velocity = finalVel
        IsGrounded = grounded
  },
  Cmd.none
