namespace _3DSample

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo
open Mibo.Elmish
open Mibo.Input
open Mibo.Layout3D
open Mibo.Rendering.Graphics3D
open _3DSample.Domain
open _3DSample.Systems
open _3DSample.Level
open _3DSample.Rendering

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
    let levelGrid = Build.create()

    // Load player model and compute bounds
    let playerModel = Assets.model "Models/Platform/ball_blue" ctx
    let playerBounds = Platform.computeBounds playerModel

    // Load grid effect
    let gridEffect = Assets.effect "Effects/Grid" ctx

    // Load PBR effect directly from file (not through content pipeline)
    let pbrEffectPath = System.IO.Path.Combine(ctx.Content.RootDirectory, "Effects/PBR.fx")
    let pbrEffect = new Effect(ctx.GraphicsDevice, System.IO.File.ReadAllBytes(pbrEffectPath))

    // Extract platform data from level grid for grid rendering
    let platforms =
      let acc = ResizeArray<PlatformData>()

      CellGrid3D.iter
        (fun x y z cell ->
          if cell.Render then
            let worldPos = CellGrid3D.getWorldPos x y z levelGrid

            acc.Add(
              {
                Position = worldPos
                Bounds = BoundingBox(worldPos, worldPos + cell.Size)
              }
            ))
        levelGrid

      Seq.toList acc

    // Create grid vertices
    let gridVertices, gridLineCount = Grid.create platforms 0.5f Color.White

    let assets = {
      PlayerModel = playerModel
      PlayerBounds = playerBounds
      PlatformModel = playerModel
      PlatformBounds = playerBounds
      PlatformGrid = gridVertices
      PlatformGridLineCount = gridLineCount
      GridEffect = gridEffect
      PbrEffect = pbrEffect
    }

    {
      PlayerPosition = Vector3(24f, 2f, 24f)
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

      System.start state
      |> System.pipe(Movement.update dt)
      |> System.pipe(Physics.update dt)
      |> System.pipe(Rotation.update dt)
      |> System.pipe Respawn.update
      |> System.finish id

  // ─────────────────────────────────────────────────────────────
  // View: Render the 3D scene
  // ─────────────────────────────────────────────────────────────

  let view
    (ctx: GameContext)
    (state: State)
    (buffer: RenderBuffer3D<Commands.SampleCmd>)
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

    buffer.AddCmd(Commands.SampleCmd.SetCamera camera)
    buffer.AddCmd(Commands.SampleCmd.Clear(Color.CornflowerBlue, true))

    // Create view bounds for frustum culling
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
    let pbrMat =
      Materials.Types.PBR {
        AlbedoColor = Vector4.One
        AlbedoTexture = None
        NormalTexture = None
        Metallic = 0.3f
        Roughness = 0.7f
        EmissiveColor = Vector4.Zero
        EmissiveIntensity = 0.0f
        LightDirection = Vector3.Normalize(Vector3(0.5f, -1.0f, 0.3f))
        LightColor = Vector3.One
        LightIntensity = 1.2f
        AmbientColor = Vector3(0.15f, 0.15f, 0.2f)
      }

    state.LevelGrid
    |> CellGrid3D.iterVolume viewBounds (fun x y z cell ->
      if cell.Render then
        let model = Assets.model cell.AssetName ctx
        let worldPos = CellGrid3D.getWorldPos x y z state.LevelGrid

        let matrix =
          Matrix.CreateFromQuaternion(cell.Rotation)
          * Matrix.CreateTranslation(worldPos)

        buffer.AddCmd(
          Commands.DrawMeshWithMaterial(
            state.Assets.PbrEffect,
            pbrMat,
            model,
            matrix
          )
        ))

    // Draw the grid
    Grid.draw
      state.PlayerPosition
      50f
      state.Assets.GridEffect
      state.Assets.PlatformGrid
      state.Assets.PlatformGridLineCount
      buffer

    // Draw the player
    Player.draw ctx state buffer

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
      |> Program.withRenderer(fun game ->
        Batch3DRenderer.create game view Processor.processCommands)

    use game = new ElmishGame<State, Msg>(program)
    game.Run()
    0
