namespace _3DSample

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo.Input
open Mibo.Layout3D

// ─────────────────────────────────────────────────────────────
// Core Types
// ─────────────────────────────────────────────────────────────

/// Semantic game actions
[<Struct>]
type GameAction =
  | MoveLeft
  | MoveRight
  | MoveForward
  | MoveBackward
  | Jump

/// Lightweight cell content - stores asset info and layout data
/// - AssetName: Path to model asset
/// - Rotation: Orientation
/// - Size: Footprint in cells (e.g., 4x1x4 for a 4x4 floor tile)
/// - Render: true = this is the anchor cell (renders model), false = collision marker only
[<Struct>]
type Cell = {
  AssetName: string
  Rotation: Quaternion
  Size: Vector3
  Render: bool
}

/// Platform with position and computed bounds (kept for player collision)
[<Struct>]
type PlatformData = {
  Position: Vector3
  Bounds: BoundingBox
}

/// Assets loaded at init, available for update and view
[<Struct>]
type GameAssets = {
  PlayerModel: Model
  PlayerBounds: BoundingBox
  PlatformModel: Model
  PlatformBounds: BoundingBox
  PlatformGrid: VertexPositionColor[]
  PlatformGridLineCount: int
  GridEffect: Effect
}

/// Game state - fully immutable, idiomatic F#
[<Struct>]
type State = {
  PlayerPosition: Vector3
  Velocity: Vector3
  Rotation: Quaternion
  IsGrounded: bool
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>
  Assets: GameAssets
  /// The level grid - source of truth for all level geometry
  LevelGrid: CellGrid3D<Cell>
}

// ─────────────────────────────────────────────────────────────
// Physics Constants
// ─────────────────────────────────────────────────────────────

module Constants =
  let gravity = -20.0f
  let jumpSpeed = 15.0f
  let moveSpeed = 10.0f
  let acceleration = 25.0f
  let friction = 8.0f
  let fallLimit = -20.0f
  let rollSpeed = 2.0f
