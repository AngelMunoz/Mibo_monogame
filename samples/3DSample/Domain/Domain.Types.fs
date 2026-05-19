namespace _3DSample.Domain

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo.Input
open Mibo.Layout3D

// ============================================================================
// Core Domain Types
// ============================================================================
// These types define the fundamental concepts of the game: actions, cells,
// platforms, assets, and the immutable game state.

/// <summary>Semantic game actions mapped from input devices.</summary>
[<Struct>]
type GameAction =
  | MoveLeft
  | MoveRight
  | MoveForward
  | MoveBackward
  | Jump

/// <summary>Lightweight cell content for the 3D level grid.</summary>
/// <remarks>
/// - <c>AssetName</c>: Path to the model asset.
/// - <c>Rotation</c>: Orientation of the cell.
/// - <c>Size</c>: Footprint in cells (e.g., 4x1x4 for a 4x4 floor tile).
/// - <c>Render</c>: <c>true</c> = anchor cell (renders model), <c>false</c> = collision marker only.
/// </remarks>
[<Struct>]
type Cell = {
  AssetName: string
  Rotation: Quaternion
  Size: Vector3
  Render: bool
}

/// <summary>Platform with position and computed bounds, used for player collision.</summary>
[<Struct>]
type PlatformData = {
  Position: Vector3
  Bounds: BoundingBox
}

/// <summary>Assets loaded at init, available for update and view.</summary>
[<Struct>]
type GameAssets = {
  PlayerModel: Model
  PlayerBounds: BoundingBox
  PlatformModel: Model
  PlatformBounds: BoundingBox
  PlatformGrid: VertexPositionColor[]
  PlatformGridLineCount: int
  GridEffect: Effect
  PbrEffect: Effect
}

/// <summary>Game state - fully immutable, idiomatic F#.</summary>
[<Struct>]
type State = {
  PlayerPosition: Vector3
  Velocity: Vector3
  Rotation: Quaternion
  IsGrounded: bool
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>
  Assets: GameAssets
  /// <summary>The level grid - source of truth for all level geometry.</summary>
  LevelGrid: CellGrid3D<Cell>
}

/// <summary>Physics and gameplay constants.</summary>
module Constants =
  let gravity = -20.0f
  let jumpSpeed = 15.0f
  let moveSpeed = 10.0f
  let acceleration = 25.0f
  let friction = 8.0f
  let fallLimit = -20.0f
  let rollSpeed = 2.0f
