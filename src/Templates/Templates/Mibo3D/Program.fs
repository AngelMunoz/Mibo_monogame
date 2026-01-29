module Mibo3D.Program

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open Mibo.Input

// ─────────────────────────────────────────────────────────────
// Input
// ─────────────────────────────────────────────────────────────

type GameAction =
  | MoveForward
  | MoveBackward
  | MoveLeft
  | MoveRight
  | MoveUp
  | MoveDown

let inputMap =
  InputMap.empty
  |> InputMap.key MoveForward Keys.W
  |> InputMap.key MoveForward Keys.Up
  |> InputMap.key MoveBackward Keys.S
  |> InputMap.key MoveBackward Keys.Down
  |> InputMap.key MoveLeft Keys.A
  |> InputMap.key MoveLeft Keys.Left
  |> InputMap.key MoveRight Keys.D
  |> InputMap.key MoveRight Keys.Right
  |> InputMap.key MoveUp Keys.Space
  |> InputMap.key MoveDown Keys.LeftShift

// ─────────────────────────────────────────────────────────────
// Model
// ─────────────────────────────────────────────────────────────

type Model = {
  Position: Vector3
  Velocity: Vector3
  Input: ActionState<GameAction>
}

// ─────────────────────────────────────────────────────────────
// Messages
// ─────────────────────────────────────────────────────────────

type Msg =
  | Tick of GameTime
  | InputChanged of ActionState<GameAction>

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init(ctx: GameContext) : struct (Model * Cmd<Msg>) =
  let model = {
    Position = Vector3.Zero
    Velocity = Vector3(2.f, 1.5f, 2.f)
    Input = ActionState.empty
  }

  model, Cmd.none

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
  match msg with
  | InputChanged input -> { model with Input = input }, Cmd.none

  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    // Manual movement
    let speed = 5.0f
    let mutable manualVelocity = Vector3.Zero

    if model.Input.Held.Contains MoveForward then
      manualVelocity.Z <- manualVelocity.Z - speed

    if model.Input.Held.Contains MoveBackward then
      manualVelocity.Z <- manualVelocity.Z + speed

    if model.Input.Held.Contains MoveLeft then
      manualVelocity.X <- manualVelocity.X - speed

    if model.Input.Held.Contains MoveRight then
      manualVelocity.X <- manualVelocity.X + speed

    if model.Input.Held.Contains MoveUp then
      manualVelocity.Y <- manualVelocity.Y + speed

    if model.Input.Held.Contains MoveDown then
      manualVelocity.Y <- manualVelocity.Y - speed

    // Bouncing logic
    let mutable velocity = model.Velocity

    let mutable position =
      model.Position + (velocity * dt) + (manualVelocity * dt)

    let bounds = 5.f

    if position.X < -bounds || position.X > bounds then
      velocity.X <- -velocity.X

    if position.Z < -bounds || position.Z > bounds then
      velocity.Z <- -velocity.Z

    if position.Y < -bounds || position.Y > bounds then
      velocity.Y <- -velocity.Y

    {
      model with
          Position = position
          Velocity = velocity
    },
    Cmd.none

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view
  (ctx: GameContext)
  (model: Model)
  (buffer: RenderBuffer<unit, RenderCommand>)
  =
  // Setup camera using the property-driven API
  let camera =
    Camera.perspectiveDefaults
    |> Camera.withAspect(800.f / 600.f)
    |> Camera.lookAt (Vector3(12.f, 12.f, 12.f)) Vector3.Zero

  buffer |> Buffer.clear Color.CornflowerBlue |> Buffer.camera camera |> ignore

  // Load the cube model mesh
  let cubeModel = Assets.model "cube" ctx
  let cubeMesh = Mesh.fromModel cubeModel |> Seq.head

  // Build and draw the player cube using the declarative DSL
  draw {
    mesh cubeMesh
    at model.Position
    withAlbedo Color.Red
  }
  |> buffer.Draw
  |> ignore

// ─────────────────────────────────────────────────────────────
// Program
// ─────────────────────────────────────────────────────────────

[<EntryPoint>]
let main _ =
  let program =
    Program.mkProgram init update
    |> Program.withAssets
    |> Program.withRenderer(fun game ->
      RenderPipeline.create PipelineConfig.defaults game
      |> PipelineRenderer.create game view)
    |> Program.withInput
    |> Program.withSubscription(fun ctx _ ->
      InputMapper.subscribeStatic inputMap InputChanged ctx)
    |> Program.withTick Tick
    |> Program.withConfig(fun (game, graphics) ->
      game.Content.RootDirectory <- "Content"
      game.Window.Title <- "Mibo 3D Game"
      game.IsMouseVisible <- true
      graphics.PreferredBackBufferWidth <- 800
      graphics.PreferredBackBufferHeight <- 600)

  use game = new ElmishGame<_, _>(program)
  game.Run()
  0
