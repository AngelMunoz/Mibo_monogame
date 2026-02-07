module MiboSample.Domain

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open FSharp.UMX
open Mibo.Input
open Mibo.Animation
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Layout

// ─────────────────────────────────────────────────────────────
// Core Types
// ─────────────────────────────────────────────────────────────

/// Entity identifier measure type
[<Measure>]
type EntityId

/// Semantic game actions for platformer controls
type GameAction =
  | MoveLeft
  | MoveRight
  | Jump
  | Respawn

/// Platform tile type for terrain generation
[<Struct>]
type TileType =
  | Empty
  | Ground
  | Platform
  | Decoration
  | Hazard

/// Lightweight tile data for storage in the Grid
[<Struct>]
type GridTile = { TileType: TileType; Variant: int }

/// Structured map definition containing chunked grid data
type GameMap = {
  /// Active chunks keyed by Chunk X index
  Chunks: Map<int, LayeredGrid2D<GridTile>>
  Occluders: Occluder2D array
  PointLights: PointLight2D array
}

module GameMap =
  let empty: GameMap = {
    Chunks = Map.empty
    Occluders = [||]
    PointLights = [||]
  }

/// Platform collision box
[<Struct>]
type Platform = {
  Bounds: Rectangle
  Type: TileType
  Variant: int
}

/// Simple particle structure
[<Struct>]
type Particle = {
  Position: Vector2
  Velocity: Vector2
  Lifetime: float32
  MaxLifetime: float32
  Color: Color
  Size: Vector2
  Rotation: float32
}

// ─────────────────────────────────────────────────────────────
// Physics Constants
// ─────────────────────────────────────────────────────────────

module Constants =
  let gravity = 1200.0f // pixels/second^2 (positive = down in MonoGame)
  let moveSpeed = 300.0f // pixels/second
  let jumpSpeed = -700.0f // pixels/second (negative = up in MonoGame)
  let airAcceleration = 1500.0f
  let groundAcceleration = 2500.0f
  let friction = 12.0f
  let maxFallSpeed = 1000.0f // positive max fall speed
  let coyoteTime = 0.1f // Time you can jump after leaving ground
  let jumpBufferTime = 0.15f // Time jump input is remembered

  // Terrain generation
  let tileSize = 64.0f
  let chunkWidth = 20 // Tiles per chunk
  let worldHeight = 12 // Total tiles high (12 * 64 = 768 pixels)

  let chunkSize =
    Vector2(float32 chunkWidth * tileSize, float32 worldHeight * tileSize)

// ─────────────────────────────────────────────────────────────
// Sprite Assets
// ─────────────────────────────────────────────────────────────

/// Loaded animation assets for the player
type PlayerAssets = {
  Idle: AnimatedSprite
  Walk: AnimatedSprite
  Jump: AnimatedSprite
  Fall: AnimatedSprite
}

/// Loaded terrain sprite assets
type TerrainAssets = {
  GroundTile: Texture2D
  PlatformTile: Texture2D
  HazardTile: Texture2D
  WhiteTexture: Texture2D
  ParticleEffect: BasicEffect
  SkyEffect: Effect
  LightingEffect: Effect
  SunSprite: AnimatedSprite
  MoonSprite: AnimatedSprite
}

/// Loaded animation assets for decorative elements
type DecorationAssets = { Torch: AnimatedSprite }

// ─────────────────────────────────────────────────────────────
// Model: The World State
// ─────────────────────────────────────────────────────────────

[<Struct>]
type Model = {
  // Player state
  PlayerId: Guid<EntityId>
  PlayerPosition: Vector2
  PlayerVelocity: Vector2
  PlayerFacing: float32 // -1.0f left, 1.0f right
  IsGrounded: bool
  IsJumping: bool

  // Physics state
  CoyoteTimer: float32 // Time since last grounded
  JumpBufferTimer: float32 // Time since jump pressed

  // Input state
  Actions: ActionState<GameAction>
  InputMap: InputMap<GameAction>

  // World Data
  Map: GameMap
  Platforms: Platform array // Collision boxes cached from map for physics

  // Assets
  PlayerAssets: PlayerAssets
  TerrainAssets: TerrainAssets
  DecorationAssets: DecorationAssets

  // World state
  CameraX: float32 // Camera scroll position
  TotalTime: float32
  Seed: int // For procedural generation
  LastGeneratedChunk: int
  DayNight: DayNight.State
  Particles: Particle list
}

// ─────────────────────────────────────────────────────────────
// Helper Functions
// ─────────────────────────────────────────────────────────────

module Helpers =
  /// Create a new entity ID
  let newEntityId() : Guid<EntityId> = Guid.NewGuid() |> UMX.tag<EntityId>

  /// Get the chunk X coordinate from a world X position
  let worldXToChunkX(worldX: float32) : int =
    // Use floor to correctly handle negative coordinates
    int(floor(worldX / Constants.chunkSize.X))

  /// Convert world position to a Chunk Index and Local Tile Index
  let worldToChunkAndTile(position: Vector2) : int * Point =
    let chunkX = worldXToChunkX position.X
    let chunkOriginX = float32 chunkX * Constants.chunkSize.X
    let localX = int((position.X - chunkOriginX) / Constants.tileSize)
    let localY = int(position.Y / Constants.tileSize)
    chunkX, Point(localX, localY)

  /// Check if a point is within screen bounds (for culling)
  let isVisible
    (position: Vector2, cameraX: float32, screenWidth: float32, margin: float32)
    : bool =
    position.X >= cameraX - margin
    && position.X <= cameraX + screenWidth + margin

  /// Calculate facing direction from velocity
  let calculateFacing(velocity: Vector2, currentFacing: float32) : float32 =
    if Math.Abs(velocity.X) > 1.0f then
      Math.Sign(velocity.X) |> float32
    else
      currentFacing
