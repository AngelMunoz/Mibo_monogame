namespace _3DSample

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open Mibo.Input
open Mibo.Layout3D

module Program =

  // ─────────────────────────────────────────────────────────────
  // Messages
  // ─────────────────────────────────────────────────────────────

  type Msg =
    | InputMapped of ActionState<GameAction>
    | Tick of GameTime

  // Shared ref for input map (allows dynamic remapping)
  let private inputMapRef: InputMap<GameAction> ref = ref InputMap.empty

  // ─────────────────────────────────────────────────────────────
  // Init
  // ─────────────────────────────────────────────────────────────

  let init(ctx: GameContext) : struct (State * Cmd<Msg>) =
    let inputMap =
      InputMap.empty
      // Keyboard controls
      |> InputMap.key MoveLeft Keys.A
      |> InputMap.key MoveLeft Keys.Left
      |> InputMap.key MoveRight Keys.D
      |> InputMap.key MoveRight Keys.Right
      |> InputMap.key MoveForward Keys.W
      |> InputMap.key MoveForward Keys.Up
      |> InputMap.key MoveBackward Keys.S
      |> InputMap.key MoveBackward Keys.Down
      |> InputMap.key Jump Keys.Space
      // Player 1 gamepad controls
      |> InputMap.gamepadButton MoveLeft PlayerIndex.One Buttons.DPadLeft
      |> InputMap.gamepadButton MoveRight PlayerIndex.One Buttons.DPadRight
      |> InputMap.gamepadButton MoveForward PlayerIndex.One Buttons.DPadUp
      |> InputMap.gamepadButton MoveBackward PlayerIndex.One Buttons.DPadDown
      |> InputMap.gamepadButton
        MoveLeft
        PlayerIndex.One
        Buttons.LeftThumbstickLeft
      |> InputMap.gamepadButton
        MoveRight
        PlayerIndex.One
        Buttons.LeftThumbstickRight
      |> InputMap.gamepadButton
        MoveForward
        PlayerIndex.One
        Buttons.LeftThumbstickUp
      |> InputMap.gamepadButton
        MoveBackward
        PlayerIndex.One
        Buttons.LeftThumbstickDown
      |> InputMap.gamepadButton Jump PlayerIndex.One Buttons.A

    inputMapRef.Value <- inputMap

    // Create level grid using Layout3D DSL
    let levelGrid = Level.create()

    // Load player model and compute bounds
    let playerModel = Assets.model "Models/Platform/ball_blue" ctx
    let playerBounds = Platform.computeBounds playerModel

    // Placeholder for grid rendering (can be removed or simplified)
    let gridEffect = Assets.effect "Effects/Grid" ctx

    let assets = {
      PlayerModel = playerModel
      PlayerBounds = playerBounds
      PlatformModel = playerModel // Not used now, kept for compatibility
      PlatformBounds = playerBounds
      PlatformGrid = [||]
      PlatformGridLineCount = 0
      GridEffect = gridEffect
    }

    {
      PlayerPosition = Vector3(24f, 2f, 24f) // Start near center of 64x64 level
      Velocity = Vector3.Zero
      Rotation = Quaternion.Identity
      IsGrounded = false
      Actions = ActionState.empty
      InputMap = inputMap
      Assets = assets
      LevelGrid = levelGrid
    },
    Cmd.none

  // ─────────────────────────────────────────────────────────────
  // Update: Composable System Pipeline
  // ─────────────────────────────────────────────────────────────

  let update (msg: Msg) (state: State) : struct (State * Cmd<Msg>) =
    match msg with
    | InputMapped actions -> { state with Actions = actions }, Cmd.none

    | Tick gt ->
      let dt = float32 gt.ElapsedGameTime.TotalSeconds

      // Composable system pipeline using Mibo.Elmish.System
      System.start state
      |> System.pipe(Movement.update dt)
      |> System.pipe(Physics.update dt)
      |> System.pipe(Rotation.update dt)
      |> System.pipe Player.checkRespawn
      |> System.finish id

  // ─────────────────────────────────────────────────────────────
  // View: Render the 3D scene using grid iteration
  // ─────────────────────────────────────────────────────────────

  let view
    (ctx: GameContext)
    (state: State)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    // Camera follows player
    let cameraOffset = Vector3(12f, 12f, 12f)
    let cameraPos = state.PlayerPosition + cameraOffset

    let camera =
      Camera3D.lookAt
        cameraPos
        state.PlayerPosition
        Vector3.Up
        (MathHelper.ToRadians 45f)
        (1280f / 720f)
        0.1f
        1000f

    Draw3D.camera camera buffer

    // Create view bounds for frustum culling (large radius around player)
    let viewRadius = 50f

    let viewBounds =
      BoundingBox(
        Vector3(
          state.PlayerPosition.X - viewRadius,
          -10f,
          state.PlayerPosition.Z - viewRadius
        ),
        Vector3(
          state.PlayerPosition.X + viewRadius,
          50f,
          state.PlayerPosition.Z + viewRadius
        )
      )

    // Render level geometry using iterVolume for frustum culling
    // Only render anchor cells (Render = true), skip collision markers
    state.LevelGrid
    |> CellGrid3D.iterVolume viewBounds (fun x y z cell ->
      if cell.Render then
        let model = Assets.model cell.AssetName ctx
        let worldPos = CellGrid3D.getWorldPos x y z state.LevelGrid

        let matrix =
          Matrix.CreateFromQuaternion(cell.Rotation)
          * Matrix.CreateTranslation(worldPos)

        Draw3D.mesh model matrix |> Draw3D.submit buffer)

    // Draw the player
    Player.view ctx state buffer

  // ─────────────────────────────────────────────────────────────
  // Subscribe
  // ─────────────────────────────────────────────────────────────

  let subscribe (ctx: GameContext) (_state: State) =
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
        game.Window.Title <- "Mibo 3D Layout Engine Demo"
        graphics.PreferredBackBufferWidth <- 1280
        graphics.PreferredBackBufferHeight <- 720
        game.IsMouseVisible <- true)
      |> Program.withInput
      |> Program.withAssets
      |> Program.withTick Tick
      |> Program.withSubscription subscribe
      |> Program.withRenderer(
        Batch3DRenderer.createWithConfig
          {
            Batch3DConfig.defaults with
                ClearColor = ValueSome Color.CornflowerBlue
          }
          view
      )

    use game = new ElmishGame<State, Msg>(program)
    game.Run()
    0
