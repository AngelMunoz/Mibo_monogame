namespace PipelineSample.Core

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open Mibo.Input

module Game =

  // ─────────────────────────────────────────────────────────────
  // Messages
  // ─────────────────────────────────────────────────────────────

  type Msg =
    | InputMapped of ActionState<GameAction>
    | Tick of GameTime
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
      Time = 0f
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
      System.start { state with Time = state.Time + dt }
      |> System.pipe(Movement.update dt)
      |> System.pipe(Physics.update dt)
      |> System.pipe(Rotation.update dt)
      |> System.pipe Player.checkRespawn
      |> System.finish id

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

    // Setup rendering environment with directional sunlight + rotating colored point lights
    let lights = [|
      // Primary Sunlight (White)
      yield Lighting.defaultSunlight.Lights.[0]

      // Spot Lights above each platform
      let spotColors = [| Color.Cyan; Color.Magenta; Color.Yellow; Color.Orange; Color.Lime; Color.DeepPink |]
      for i in 0 .. state.Platforms.Length - 1 do
        let plat = state.Platforms.[i]
        let color = spotColors.[i % spotColors.Length]
        yield Light.Spot {
            Position = plat.Position + Vector3(0f, 5f, 0f) // 5 units above platform
            Direction = Vector3.Down
            Color = color
            Intensity = 1.2f
            Range = 15.0f
            InnerConeAngle = MathHelper.ToRadians(20f)
            OuterConeAngle = MathHelper.ToRadians(30f)
            Shadow = ValueNone
        }

      // Add 16 colorful moving point lights
      for i in 0..15 do
        let angle = (float32 i / 16.0f) * MathHelper.TwoPi + state.Time
        let radius = 10.0f
        let x = cos(angle) * radius
        let z = sin(angle) * radius
        let h = (float32 i / 16.0f) // Hue

        // Improved Hue-based color selection
        let color =
          if h < 0.16f then Color.Red
          elif h < 0.33f then Color.Orange
          elif h < 0.5f then Color.Yellow
          elif h < 0.66f then Color.Lime
          elif h < 0.83f then Color.Cyan
          else Color.Magenta

        yield
          Light.Point {
            Position = Vector3(x, 3f, z)
            Color = color
            Intensity = 1.0f
            Range = 8.0f
            Shadow = ValueNone
          }
    |]

    let lighting = {
      Lighting.defaultSunlight with
          Lights = lights
    }

    buffer
    |> RenderBuilder.camera camera
    |> RenderBuilder.lighting lighting
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
          | _ -> Noop)
        ctx
    ]

  // ─────────────────────────────────────────────────────────────
  // Program Factory
  // ─────────────────────────────────────────────────────────────

  /// Creates the program for the platform-specific game to use
  let create() =
    Program.mkProgram init update
    |> Program.withConfig(fun (game, graphics) ->
      game.Content.RootDirectory <- "Content"
      game.Window.Title <- "Mibo Render Pipeline Sample"
      graphics.PreferredBackBufferWidth <- 1280
      graphics.PreferredBackBufferHeight <- 720
      graphics.GraphicsProfile <- GraphicsProfile.HiDef
      game.IsMouseVisible <- true)
    |> Program.withInput
    |> Program.withAssets
    |> Program.withTick Tick
    |> Program.withSubscription subscribe
    |> Program.withPipeline
      (PipelineConfig.defaults
#if OPENGL
       |> PipelineConfig.withShadowPath ForceDiscrete
#else
       |> PipelineConfig.withShadowPath ForceArray
#endif
       |> PipelineConfig.withShadows(
         ShadowConfig.defaults
         |> ShadowConfig.withResolution 2048
         |> ShadowConfig.withCascades 3
       )
       |> PipelineConfig.withShader
         ShaderBase.ShadowCaster
         "Effects/ShadowCaster"
       |> PipelineConfig.withShader ShaderBase.PBRForward "Effects/PBR")
      view
