module MiboSample.SpriteLoader

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Animation
open MiboSample.Domain

// ─────────────────────────────────────────────────────────────
// SpriteLoader: Load and organize Kenney platformer assets
// ─────────────────────────────────────────────────────────────

/// Create a simple 1x1 white texture for rendering
let createWhiteTexture(device: GraphicsDevice) =
  let texture = new Texture2D(device, 1, 1)
  texture.SetData([| Color.White |])
  texture

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

  // Star animation (for celestial bodies)
  let starAnim: Animation = {
    Frames = [| Rectangle(195, 455, 64, 64) |]
    FrameDuration = 1.0f
    Loop = false
  }

  let decorationSheet =
    SpriteSheet.fromFrames tileTex (Vector2(32.0f, 64.0f)) [|
      "torch", torchAnim
      "star", starAnim
    |]

  let decorationAssets = {
    Torch = AnimatedSprite.create decorationSheet "torch"
  }

  // ─────────────────────────────────────────────────────────────
  // Terrain Assets
  // ─────────────────────────────────────────────────────────────

  // Reuse the decoration sheet's star for sun/moon since it's the same texture
  // Just need a centered origin for rotation
  let celestialSheet =
    SpriteSheet.fromFrames tileTex (Vector2(32.0f, 32.0f)) [|
      "star", starAnim
    |]

  let particleFx = new BasicEffect(ctx.GraphicsDevice)
  particleFx.TextureEnabled <- true
  particleFx.VertexColorEnabled <- true

  let terrainAssets = {
    GroundTile = tileTex
    PlatformTile = tileTex
    HazardTile = tileTex
    WhiteTexture = createWhiteTexture ctx.GraphicsDevice
    ParticleEffect = particleFx
    SkyEffect = Assets.effect "Shaders/Sky" ctx
    LightingEffect = Assets.effect "Shaders/lighting" ctx
    SunSprite = AnimatedSprite.create celestialSheet "star"
    MoonSprite = AnimatedSprite.create celestialSheet "star"
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

  // ─────────────────────────────────────────────────────────────
  // Decorations (layered, non-collidable)
  // Coordinates from spritesheet-tiles-default.xml
  // ─────────────────────────────────────────────────────────────

  module DecorationVariants =
    // Natural / surface clutter
    [<Literal>]
    let Grass = 0

    [<Literal>]
    let GrassPurple = 1

    [<Literal>]
    let Bush = 2

    [<Literal>]
    let Cactus = 3

    [<Literal>]
    let MushroomBrown = 4

    [<Literal>]
    let MushroomRed = 5

    [<Literal>]
    let Rock = 6

    // Small props
    [<Literal>]
    let Fence = 7

    [<Literal>]
    let FenceBroken = 8

    [<Literal>]
    let Sign = 9

    [<Literal>]
    let SignExit = 10

    [<Literal>]
    let SignLeft = 11

    [<Literal>]
    let SignRight = 12

    // Background-y props
    [<Literal>]
    let Window = 13

    [<Literal>]
    let Hill = 14

    [<Literal>]
    let HillTop = 15

    [<Literal>]
    let HillTopSmile = 16

    // Vertical / hanging
    [<Literal>]
    let LadderBottom = 17

    [<Literal>]
    let LadderMiddle = 18

    [<Literal>]
    let LadderTop = 19

    [<Literal>]
    let Rope = 20

    [<Literal>]
    let Chain = 21

    // Sparkly accents
    [<Literal>]
    let GemBlue = 22

    [<Literal>]
    let GemGreen = 23

    [<Literal>]
    let GemRed = 24

    [<Literal>]
    let GemYellow = 25

    // Animated (special-cased by renderer)
    [<Literal>]
    let Torch = -1

    let surfaceClutter: int[] = [|
      Grass
      GrassPurple
      Bush
      Cactus
      MushroomBrown
      MushroomRed
      Rock
    |]

    let surfaceProps: int[] = [|
      Fence
      FenceBroken
      Sign
      SignExit
      SignLeft
      SignRight
    |]

    let hanging: int[] = [| Rope; Chain |]

    let sparkles: int[] = [| GemBlue; GemGreen; GemRed; GemYellow |]

  let private decorationRects: Rectangle[] = [|
    // 0..6: surface clutter
    Rectangle(455, 195, 64, 64) // grass
    Rectangle(520, 195, 64, 64) // grass_purple
    Rectangle(845, 65, 64, 64) // bush
    Rectangle(910, 65, 64, 64) // cactus
    Rectangle(390, 390, 64, 64) // mushroom_brown
    Rectangle(455, 390, 64, 64) // mushroom_red
    Rectangle(585, 390, 64, 64) // rock

    // 7..12: surface props
    Rectangle(585, 130, 64, 64) // fence
    Rectangle(650, 130, 64, 64) // fence_broken
    Rectangle(845, 390, 64, 64) // sign
    Rectangle(910, 390, 64, 64) // sign_exit
    Rectangle(975, 390, 64, 64) // sign_left
    Rectangle(1040, 390, 64, 64) // sign_right

    // 13..16: background-y props
    Rectangle(455, 1105, 64, 64) // window
    Rectangle(650, 195, 64, 64) // hill
    Rectangle(715, 195, 64, 64) // hill_top
    Rectangle(780, 195, 64, 64) // hill_top_smile

    // 17..21: vertical / hanging
    Rectangle(715, 325, 64, 64) // ladder_bottom
    Rectangle(780, 325, 64, 64) // ladder_middle
    Rectangle(845, 325, 64, 64) // ladder_top
    Rectangle(715, 390, 64, 64) // rope
    Rectangle(975, 65, 64, 64) // chain

    // 22..25: sparkles
    Rectangle(195, 195, 64, 64) // gem_blue
    Rectangle(260, 195, 64, 64) // gem_green
    Rectangle(325, 195, 64, 64) // gem_red
    Rectangle(390, 195, 64, 64) // gem_yellow
  |]

  let private torchOnA = Rectangle(65, 1105, 64, 64)
  let private torchOnB = Rectangle(130, 1105, 64, 64)
  let private torchOff = Rectangle(0, 1105, 64, 64)

  /// Get the source rectangle for a decoration tile.
  ///
  /// Note: some variants are special-cased (e.g. animated torch).
  let getDecorationVariant
    (variant: int, totalTimeSeconds: float32)
    : Rectangle =
    if variant = DecorationVariants.Torch then
      // Cheap "animation" without keeping per-tile state.
      // 8 FPS flicker between A/B frames.
      let frame = int(totalTimeSeconds * 8.0f) &&& 1
      if frame = 0 then torchOnA else torchOnB
    elif variant < 0 then
      // Unknown negative variants: default to off.
      torchOff
    else
      decorationRects.[variant % decorationRects.Length]

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
