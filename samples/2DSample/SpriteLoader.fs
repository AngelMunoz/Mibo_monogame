module MiboSample.SpriteLoader

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Animation
open MiboSample.Domain

// ─────────────────────────────────────────────────────────────
// SpriteLoader: Load and organize Kenney platformer assets
// ─────────────────────────────────────────────────────────────

/// Load all assets required for the platformer
let load(ctx: GameContext) =
  // Load character spritesheet
  let characterTex =
    Assets.texture
      "kenney_platformer/Spritesheets/spritesheet-characters-default"
      ctx

  // Load tile spritesheet
  let tileTex =
    Assets.texture
      "kenney_platformer/Spritesheets/spritesheet-tiles-default"
      ctx

  // Load background
  let backgroundTex =
    Assets.texture
      "kenney_platformer/Spritesheets/spritesheet-backgrounds-default"
      ctx

  // ─────────────────────────────────────────────────────────────
  // Player Animations
  // Origin is at bottom center of 128x128 sprite for proper ground alignment
  // ─────────────────────────────────────────────────────────────

  // Idle animation (single frame)
  // character_beige_idle: x="645" y="0" width="128" height="128"
  let idleAnim: Animation = {
    Frames = [| Rectangle(645, 0, 128, 128) |]
    FrameDuration = 1.0f
    Loop = false
  }

  // Walk animation (two alternating frames)
  // character_beige_walk_a: x="0" y="129" width="128" height="128"
  // character_beige_walk_b: x="129" y="129" width="128" height="128"
  let walkAnim: Animation = {
    Frames = [| Rectangle(0, 129, 128, 128); Rectangle(129, 129, 128, 128) |]
    FrameDuration = 1.0f / 10.0f // 10 FPS
    Loop = true
  }

  // Jump animation
  // character_beige_jump: x="774" y="0" width="128" height="128"
  let jumpAnim: Animation = {
    Frames = [| Rectangle(774, 0, 128, 128) |]
    FrameDuration = 1.0f
    Loop = false
  }

  // Fall animation (use jump frame for now)
  let fallAnim: Animation = {
    Frames = [| Rectangle(774, 0, 128, 128) |]
    FrameDuration = 1.0f
    Loop = false
  }

  // Create player sprite sheet with bottom-center origin (64, 128) for 128x128 sprites
  // This ensures feet align with ground tiles
  let playerSheet =
    SpriteSheet.fromFrames characterTex (Vector2(64.0f, 128.0f)) [|
      "idle", idleAnim
      "walk", walkAnim
      "jump", jumpAnim
      "fall", fallAnim
    |]

  // Create animated sprites for player states
  let idleSprite = AnimatedSprite.create playerSheet "idle"
  let walkSprite = AnimatedSprite.create playerSheet "walk"
  let jumpSprite = AnimatedSprite.create playerSheet "jump"
  let fallSprite = AnimatedSprite.create playerSheet "fall"

  let playerAssets = {
    Idle = idleSprite
    Walk = walkSprite
    Jump = jumpSprite
    Fall = fallSprite
  }

  // ─────────────────────────────────────────────────────────────
  // Decoration Animations
  // ─────────────────────────────────────────────────────────────

  // Torch animation (two frames)
  let torchAnim: Animation = {
    Frames = [| Rectangle(65, 1105, 64, 64); Rectangle(130, 1105, 64, 64) |]
    FrameDuration = 1.0f / 8.0f // 8 FPS
    Loop = true
  }

  let decorationSheet =
    SpriteSheet.fromFrames tileTex (Vector2(32.0f, 64.0f)) [|
      "torch", torchAnim
    |]

  let decorationAssets = {
    Torch = AnimatedSprite.create decorationSheet "torch"
  }

  // ─────────────────────────────────────────────────────────────
  // Terrain Assets
  // ─────────────────────────────────────────────────────────────

  let terrainAssets = {
    GroundTile = tileTex
    PlatformTile = tileTex
    HazardTile = tileTex
    Background = backgroundTex
  }

  struct (playerAssets, terrainAssets, decorationAssets)

// ─────────────────────────────────────────────────────────────

// Sprite Region Helpers for Tile Rendering
// Coordinates from spritesheet-tiles-default.xml
// ─────────────────────────────────────────────────────────────

module TileRegions =
  /// Get the source rectangle for a ground tile variant
  /// Variant encoding: ThemeIndex * 10 + ShapeIndex
  /// Themes: 0=Grass, 1=Dirt, 2=Sand, 3=Stone, 4=Snow, 5=Purple
  /// Shapes:
  /// 0: Top Center (Standard Surface)
  /// 1: Center (Fill)
  /// 2: Center Left (Wall Left)
  /// 3: Center Right (Wall Right)
  /// 4: Top Left (Corner)
  /// 5: Top Right (Corner)
  /// 6: Bottom Center (Ceiling)
  /// 7: Bottom Left (Corner)
  /// 8: Bottom Right (Corner)
  let getGroundVariant(variant: int) : Rectangle =
    let theme = variant / 10
    let shape = variant % 10

    // Coordinates [TopCenter, Center, Left, Right, TopLeft, TopRight, Bottom, BottomLeft, BottomRight]
    // Derived from spritesheet-tiles-default.xml
    let coords =
      match theme with
      | 0 -> // Grass (Row ~585)
          [|
            Rectangle(260, 585, 64, 64) // 0: Top (block)
            Rectangle(520, 585, 64, 64) // 1: Center
            Rectangle(585, 585, 64, 64) // 2: Left
            Rectangle(650, 585, 64, 64) // 3: Right
            Rectangle(780, 585, 64, 64) // 4: Top Left
            Rectangle(845, 585, 64, 64) // 5: Top Right
            Rectangle(325, 585, 64, 64) // 6: Bottom
            Rectangle(390, 585, 64, 64) // 7: Bottom Left
            Rectangle(455, 585, 64, 64) // 8: Bottom Right
          |]
      | 1 -> // Dirt (Row ~455)
          [|
            Rectangle(780, 455, 64, 64) // 0: Top
            Rectangle(1040, 455, 64, 64) // 1: Center
            Rectangle(1105, 455, 64, 64) // 2: Left
            Rectangle(0, 520, 64, 64) // 3: Right
            Rectangle(130, 520, 64, 64) // 4: Top Left
            Rectangle(195, 520, 64, 64) // 5: Top Right
            Rectangle(845, 455, 64, 64) // 6: Bottom
            Rectangle(910, 455, 64, 64) // 7: Bottom Left
            Rectangle(975, 455, 64, 64) // 8: Bottom Right
          |]
      | 2 -> // Sand (Row ~780)
          [|
            Rectangle(390, 780, 64, 64) // 0: Top
            Rectangle(650, 780, 64, 64) // 1: Center
            Rectangle(715, 780, 64, 64) // 2: Left
            Rectangle(780, 780, 64, 64) // 3: Right
            Rectangle(910, 780, 64, 64) // 4: Top Left
            Rectangle(975, 780, 64, 64) // 5: Top Right
            Rectangle(455, 780, 64, 64) // 6: Bottom
            Rectangle(520, 780, 64, 64) // 7: Bottom Left
            Rectangle(585, 780, 64, 64) // 8: Bottom Right
          |]
      | 3 -> // Stone (Row ~975)
          [|
            Rectangle(520, 975, 64, 64) // 0: Top
            Rectangle(780, 975, 64, 64) // 1: Center
            Rectangle(845, 975, 64, 64) // 2: Left
            Rectangle(910, 975, 64, 64) // 3: Right
            Rectangle(1040, 975, 64, 64) // 4: Top Left
            Rectangle(1105, 975, 64, 64) // 5: Top Right
            Rectangle(585, 975, 64, 64) // 6: Bottom
            Rectangle(650, 975, 64, 64) // 7: Bottom Left
            Rectangle(715, 975, 64, 64) // 8: Bottom Right
          |]
      | 4 -> // Snow (Row ~845/910)
          [|
            Rectangle(1040, 845, 64, 64) // 0: Top
            Rectangle(130, 910, 64, 64) // 1: Center
            Rectangle(195, 910, 64, 64) // 2: Left
            Rectangle(260, 910, 64, 64) // 3: Right
            Rectangle(390, 910, 64, 64) // 4: Top Left
            Rectangle(455, 910, 64, 64) // 5: Top Right
            Rectangle(1105, 845, 64, 64) // 6: Bottom
            Rectangle(0, 910, 64, 64) // 7: Bottom Left
            Rectangle(65, 910, 64, 64) // 8: Bottom Right
          |]
      | _ -> // Purple (Row ~650/715)
          [|
            Rectangle(910, 650, 64, 64) // 0: Top
            Rectangle(0, 715, 64, 64) // 1: Center
            Rectangle(65, 715, 64, 64) // 2: Left
            Rectangle(130, 715, 64, 64) // 3: Right
            Rectangle(260, 715, 64, 64) // 4: Top Left
            Rectangle(325, 715, 64, 64) // 5: Top Right
            Rectangle(975, 650, 64, 64) // 6: Bottom
            Rectangle(1040, 650, 64, 64) // 7: Bottom Left
            Rectangle(1105, 650, 64, 64) // 8: Bottom Right
          |]

    if shape >= 0 && shape < coords.Length then
      coords.[shape]
    else
      coords.[0]

  /// Get the source rectangle for a platform tile variant
  let getPlatformVariant(variant: int) : Rectangle =
    let variants = [|
      Rectangle(520, 0, 64, 64) // block_plank
      Rectangle(585, 0, 64, 64) // block_planks
    |]

    variants[variant % variants.Length]

  /// Get the source rectangle for hazard tiles
  let getHazardTile() : Rectangle = Rectangle(715, 0, 64, 64) // block_spikes

  /// Get the source rectangle for background tiles
  let getBackgroundVariant(variant: int) : Rectangle =
    // Backgrounds from spritesheet-backgrounds-default.xml
    // For now, use a simple placeholder - adjust based on actual sprite size
    let variants = [|
      Rectangle(0, 0, 128, 128) // First background tile
    |]

    variants[variant % variants.Length]

  // ─────────────────────────────────────────────────────────────
  // Asset Loading Utilities
  // ─────────────────────────────────────────────────────────────

  /// Create a simple static sprite sheet from a single frame
  let createStaticSprite
    (texture: Texture2D, sourceRect: Rectangle)
    : AnimatedSprite =
    let sheet = SpriteSheet.static' texture sourceRect
    AnimatedSprite.create sheet "default"

  /// Create an animated sprite from a series of frames
  let createAnimatedSprite
    (texture: Texture2D)
    (frames: Rectangle[])
    (fps: float32)
    (origin: Vector2)
    : AnimatedSprite =
    let anim: Animation = {
      Frames = frames
      FrameDuration = 1.0f / fps
      Loop = true
    }

    let sheet = SpriteSheet.fromFrames texture origin [| "default", anim |]
    AnimatedSprite.create sheet "default"
