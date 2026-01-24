module MiboSample.Program

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Input
open MiboSample.Domain
open MiboSample.SpriteLoader
open Mibo.Elmish.Graphics2D

// ─────────────────────────────────────────────────────────────
// Messages
// ─────────────────────────────────────────────────────────────

type Msg =
  | Tick of GameTime
  | InputMapped of ActionState<GameAction>

// Shared ref for input map (allows dynamic remapping)
let private inputMapRef: InputMap<GameAction> ref = ref InputMap.empty

// Import modules
open MiboSample.Physics
open MiboSample.Terrain
open MiboSample.Animation
open MiboSample.Player

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init(ctx: GameContext) : struct (Model * Cmd<Msg>) =
  // Configure input map
  let inputMap =
    InputMap.empty
    |> InputMap.key MoveLeft Keys.A
    |> InputMap.key MoveLeft Keys.Left
    |> InputMap.key MoveRight Keys.D
    |> InputMap.key MoveRight Keys.Right
    |> InputMap.key GameAction.Jump Keys.Space

  inputMapRef.Value <- inputMap

  // Load sprites
  let struct (playerAssets, terrainAssets) = SpriteLoader.load ctx

  // Generate random seed for procedural terrain
  let seed = 78494612

  // Generate initial terrain
  let tiles = generateInitialTerrain seed
  let platforms = createPlatformsFromTiles tiles

  // Create player
  let playerId = Helpers.newEntityId()

  // Spawn player on ground
  // Ground tiles are at tile index 10, which is y=640 in world coordinates
  // Collision box is 64x64, origin at top-left
  // Player needs to be at Y = groundY - playerHeight = 640 - 64 = 576
  let spawnY = 576.0f

  {
    PlayerId = playerId
    PlayerPosition = Vector2(200.0f, spawnY)
    PlayerVelocity = Vector2.Zero
    PlayerFacing = 1.0f
    IsGrounded = true // Player spawns on ground
    IsJumping = false
    CoyoteTimer = 0.0f
    JumpBufferTimer = 0.0f
    Actions = ActionState.empty
    InputMap = inputMap
    Tiles = tiles
    Platforms = platforms
    PlayerAssets = playerAssets
    TerrainAssets = terrainAssets
    CameraX = 0.0f
    TotalTime = 0.0f
    Seed = seed
    // Initial terrain generates chunks -1, 0, 1. So the last generated is 1.
    LastGeneratedChunk = 1
  },
  Cmd.none

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
  match msg with
  | InputMapped actions -> { model with Actions = actions }, Cmd.none

  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds
    let totalTime = model.TotalTime + dt

    // Update physics
    let model = Physics.update dt model

    // Update terrain generation as player moves
    let model = updateTerrain model

    // Update platforms when terrain changes
    let model = updatePlatforms model

    // Update player animations
    let model = Player.update dt model

    // Update camera to follow player
    let viewportWidth = float32 1280 // Default window width
    let targetCameraX = model.PlayerPosition.X - viewportWidth * 0.3f
    let cameraX = Math.Max(0.0f, targetCameraX)

    // Check if player fell off world
    let model =
      if checkKillPlane model then
        let model = respawnPlayer model
        // Regenerate initial terrain so the player has somewhere to land
        let tiles = generateInitialTerrain model.Seed
        // Rebuild platforms from new tiles
        let platforms = createPlatformsFromTiles tiles

        {
          model with
              Tiles = tiles
              Platforms = platforms
              LastGeneratedChunk = 1
        }
      else
        model

    {
      model with
          CameraX = cameraX
          TotalTime = totalTime
    },
    Cmd.none

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let viewport = ctx.GraphicsDevice.Viewport
  let viewportSize = Vector2(float32 viewport.Width, float32 viewport.Height)

  // Set up camera to follow player
  // Camera position is top-left of the viewable area
  let cameraX = Math.Max(0.0f, model.PlayerPosition.X - viewportSize.X * 0.3f)

  // Camera2D.create expects the CENTER of the view, so we offset by half viewport
  let cameraCenter =
    Vector2(cameraX + viewportSize.X * 0.5f, viewportSize.Y * 0.5f)

  let camera =
    Camera2D.create cameraCenter 1.0f (Point(viewport.Width, viewport.Height))

  buffer.Camera(camera, 0<RenderLayer>) |> ignore

  // UI Camera (Screen Space)
  let uiCamera =
    Camera2D.create
      (viewportSize * 0.5f)
      1.0f
      (Point(viewport.Width, viewport.Height))

  buffer.Camera(uiCamera, 100<RenderLayer>) |> ignore

  // DEBUG: Show camera and player position
  let debugText =
    $"CamX: {cameraX:F0} PlayerX: {model.PlayerPosition.X:F0} Grounded: {model.IsGrounded}"

  // Draw background with parallax (moves slower than camera)
  let bgX = cameraX * 0.5f // 50% parallax

  // No background for now

  // DEBUG: Show camera and player position
  let debugText =
    $"CamX: {cameraX:F0} PlayerX: {model.PlayerPosition.X:F0} Grounded: {model.IsGrounded}"

  // Get visible tiles for culling
  let visibleTiles = getVisibleTiles(cameraX, viewportSize.X, model.Tiles)

  // Draw terrain tiles
  for tile in visibleTiles do
    let rect =
      match tile.TileType with
      | Ground -> TileRegions.getGroundVariant tile.Variant
      | Platform -> TileRegions.getPlatformVariant tile.Variant
      | Hazard -> TileRegions.getHazardTile()
      | Empty -> Rectangle.Empty // Skip empty tiles

    if rect <> Rectangle.Empty then
      buffer.Sprite(
        sprite {
          texture model.TerrainAssets.GroundTile
          sourceRect rect
          at tile.Position.X tile.Position.Y
          size Constants.tileSize Constants.tileSize
          layer 0<RenderLayer>
        }
      )
      |> ignore

  // Draw player
  Player.view ctx model buffer

  // Draw UI (no camera reset - test if UI moves)
  let uiFont = Assets.font "Fonts/monogram" ctx

  // DEBUG line
  buffer.Text(
    text {
      font uiFont
      content debugText
      at 10.0f 10.0f
      color Color.Yellow
      layer 100<RenderLayer>
    }
  )
  |> ignore

  buffer.Text(
    text {
      font uiFont

      content
        $"Position: ({int model.PlayerPosition.X}, {int model.PlayerPosition.Y})"

      at 10.0f 30.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  buffer.Text(
    text {
      font uiFont
      content $"Chunk: {worldXToChunkX model.PlayerPosition.X}"
      at 10.0f 50.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  buffer.Text(
    text {
      font uiFont
      content $"Tiles: {model.Tiles.Length}"
      at 10.0f 70.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  // Controls help
  buffer.Text(
    text {
      font uiFont
      content "Controls: A/D or Arrow Keys to move, Space to jump"
      at 10.0f (float32 viewport.Height - 30.0f)
      color Color.Yellow
      layer 100<RenderLayer>
    }
  )
  |> ignore

// ─────────────────────────────────────────────────────────────
// Subscribe
// ─────────────────────────────────────────────────────────────

let subscribe (ctx: GameContext) (_model: Model) =
  Sub.batch [
    InputMapper.subscribe (fun () -> inputMapRef.Value) InputMapped ctx
  ]

// ─────────────────────────────────────────────────────────────
// Entry Point
// ─────────────────────────────────────────────────────────────

[<EntryPoint>]
let main _ =
  let program =
    Program.mkProgram init update
    |> Program.withConfig(fun (game, graphics) ->
      game.Content.RootDirectory <- "Content"
      game.Window.Title <- "Mibo 2D Platformer - Procedural Terrain"
      graphics.PreferredBackBufferWidth <- 1280
      graphics.PreferredBackBufferHeight <- 720
      game.IsMouseVisible <- true)
    |> Program.withInput
    |> Program.withAssets
    |> Program.withTick Tick
    |> Program.withSubscription subscribe
    |> Program.withRenderer(fun game ->
      Batch2DRenderer.createWithConfig
        game
        {
          Batch2DConfig.defaults with
              ClearColor = ValueSome Color.CornflowerBlue
        }
        view)

  use game = new ElmishGame<Model, Msg>(program)
  game.Run()
  0
