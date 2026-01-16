namespace PipelineSample

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open Mibo.Input

module PipelineSampleGame =

  // ─────────────────────────────────────────────────────────────
  // Messages
  // ─────────────────────────────────────────────────────────────

  type Msg =
    | InputMapped of ActionState<GameAction>
    | Tick of GameTime
    | SetPipelineMode of PipelineMode
    | Noop

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

    // Load models and convert to Mesh
    let playerModel = Assets.model "Models/Platform/ball_blue" ctx
    let playerMesh = Mesh.fromModel playerModel |> Seq.head
    let playerBounds = Platform.computeBounds playerMesh

    let platformModel = Assets.model "Models/Platform/platform_4x4x1_blue" ctx
    let platformMesh = Mesh.fromModel platformModel |> Seq.head
    let platformBounds = Platform.computeBounds platformMesh

    // Create platforms with computed bounds
    let platforms =
      Platform.positions |> List.map(Platform.create platformBounds)

    let gridVerts, gridLineCount = Grid.create platforms 3.0f Color.White
    let gridEffect = Assets.effect "Effects/Grid" ctx

    // Load texture
    let platformTexture =
      Assets.texture "Models/Platform/platformer_texture_0" ctx

    let assets = {
      PlayerMesh = playerMesh
      PlayerBounds = playerBounds
      PlatformMesh = platformMesh
      PlatformTexture = platformTexture
      PlatformBounds = platformBounds
      PlatformGrid = gridVerts
      PlatformGridLineCount = gridLineCount
      GridEffect = gridEffect
    }

    {
      PlayerPosition = Vector3(0f, 2f, 0f)
      Velocity = Vector3.Zero
      Rotation = Quaternion.Identity
      IsGrounded = false
      Actions = ActionState.empty
      InputMap = inputMap
      Assets = assets
      Platforms = platforms
      PipelineMode = PipelineMode.Forward
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

    | SetPipelineMode mode -> { state with PipelineMode = mode }, Cmd.none
    | Noop -> state, Cmd.none

  // ─────────────────────────────────────────────────────────────
  // View: Render the 3D scene
  // ─────────────────────────────────────────────────────────────

  let view
    (ctx: GameContext)
    (state: State)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    // Camera follows player
    let cameraOffset = Vector3(8f, 8f, 8f)
    let cameraPos = state.PlayerPosition + cameraOffset

    let camera =
      Camera.perspective
        cameraPos
        state.PlayerPosition
        Vector3.Up
        (MathHelper.ToRadians 45f)
        (1280f / 720f)
        0.1f
        200f

    // Setup rendering environment using DSL
    buffer
    |> RenderBuilder.mode state.PipelineMode
    |> RenderBuilder.camera camera
    |> RenderBuilder.lighting Lighting.defaultSunlight
    |> RenderBuilder.clear Color.CornflowerBlue
    |> RenderBuilder.clearDepth
    |> RenderBuilder.drawMany(
      [|
        for plat in state.Platforms do
          draw {
            mesh state.Assets.PlatformMesh
            at plat.Position
          }
      |]
    )
    |> RenderBuilder.draw(
      draw {
        mesh state.Assets.PlayerMesh
        at state.PlayerPosition
        rotatedBy state.Rotation
      }
    )
    |> RenderBuilder.custom(
      Grid.draw
        state.PlayerPosition
        7.0f
        state.Assets.GridEffect
        state.Assets.PlatformGrid
        state.Assets.PlatformGridLineCount
    )
    |> RenderBuilder.submit


  // ─────────────────────────────────────────────────────────────
  // Subscribe
  // ─────────────────────────────────────────────────────────────

  let subscribe (ctx: GameContext) (state: State) =
    Sub.batch [
      InputMapper.subscribe (fun () -> inputMapRef.Value) InputMapped ctx
      Keyboard.onPressed
        (fun key ->
          match key with
          | Keys.D1 -> SetPipelineMode Forward
          | Keys.D2 -> SetPipelineMode ForwardPlus
          | Keys.D3 -> SetPipelineMode Deferred
          | _ -> Noop)
        ctx
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
        game.Window.Title <- "Mibo Render Pipeline Sample"
        graphics.PreferredBackBufferWidth <- 1280
        graphics.PreferredBackBufferHeight <- 720
        game.IsMouseVisible <- true)
      |> Program.withInput
      |> Program.withAssets
      |> Program.withTick Tick
      |> Program.withSubscription subscribe
      // The pipeline mode is now switchable at runtime via withMode DSL.
      |> Program.withPipeline
        (PipelineConfig.forward
         |> PipelineConfig.withShadows(
           ShadowConfig.defaults
           |> ShadowConfig.withResolution 2048
           |> ShadowConfig.withCascades 3
         )
         |> PipelineConfig.withShader
           ShaderBase.ShadowCaster
           "Effects/ShadowCaster"
         |> PipelineConfig.withShader ShaderBase.PBRForward "Effects/PBR"
        // |> PipelineConfig.withShader ShaderBase.GBufferFill "Effects/GBuffer"
        // |> PipelineConfig.withShader ShaderBase.DeferredLighting "Effects/DeferredLighting"
        )
        view

    use game = new ElmishGame<State, Msg>(program)
    game.Run()
    0
