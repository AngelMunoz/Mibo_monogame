module MiboSample.Program

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Input
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Input

open MiboSample.Domain
open MiboSample.SpriteLoader
open MiboSample.Physics
open MiboSample.Terrain
open MiboSample.Player
open MiboSample.Camera
open MiboSample.UI

// ─────────────────────────────────────────────────────────────
// Messages
// ─────────────────────────────────────────────────────────────

type Msg =
  | Tick of GameTime
  | InputMapped of ActionState<GameAction>

// Shared ref for input map (allows dynamic remapping)
let private inputMapRef: InputMap<GameAction> ref = ref InputMap.empty

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
    |> InputMap.key GameAction.Respawn Keys.R

  inputMapRef.Value <- inputMap

  // Load sprites
  let struct (playerAssets, terrainAssets) = SpriteLoader.load ctx

  // Generate random seed for procedural terrain
  let seed = 78494612

  // Generate initial terrain
  let tiles = Terrain.generateInitialTerrain seed
  let platforms = Terrain.createPlatformsFromTiles tiles

  // Create player
  let playerId = Helpers.newEntityId()

  // Spawn player on ground
  let spawnY = 576.0f

  {
    PlayerId = playerId
    PlayerPosition = Vector2(200.0f, spawnY)
    PlayerVelocity = Vector2.Zero
    PlayerFacing = 1.0f
    IsGrounded = true
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
    LastGeneratedChunk = 1
  },
  Cmd.none

// ─────────────────────────────────────────────────────────────
// Update: Orchestration using System Pipeline
// ─────────────────────────────────────────────────────────────

let private updateSystems dt model =
  System.start { model with TotalTime = model.TotalTime + dt }
  |> System.pipe(Physics.update dt)
  |> System.pipe Terrain.update
  |> System.pipe(Player.update dt)
  |> System.pipe(fun model ->
    if Physics.checkKillPlane model || model.Actions.Started.Contains Respawn then
      let model = Physics.respawnPlayer model
      let model = Terrain.reset model
      model, Cmd.none
    else
      model, Cmd.none)
  |> System.finish Camera.update

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
  match msg with
  | InputMapped actions -> { model with Actions = actions }, Cmd.none
  | Tick gt -> updateSystems (float32 gt.ElapsedGameTime.TotalSeconds) model


// ─────────────────────────────────────────────────────────────
// View: Orchestration of rendering modules
// ─────────────────────────────────────────────────────────────

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let viewport = ctx.GraphicsDevice.Viewport


  // 1. Setup World Camera
  let worldCamera = Camera.createWorldCamera ctx model
  buffer.Camera(worldCamera, 0<RenderLayer>) |> ignore

  // 2. Draw World Elements
  // Parallax Background (placeholder - moves slower than camera)
  // let bgX = model.CameraX * 0.5f

  // Terrain
  Terrain.view model buffer

  // Player
  Player.view ctx model buffer


  // 3. Setup UI Camera
  let uiCamera = Camera.createUICamera ctx
  buffer.Camera(uiCamera, 100<RenderLayer>) |> ignore

  // 4. Draw UI
  UI.view ctx model buffer

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
      game.Window.Title <- "Mibo 2D Platformer - Modular Architecture"
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