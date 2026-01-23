open System
open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input
open FSharp.UMX

open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Input
open Mibo.Animation
open MiboSample
open MiboSample.Domain
open MiboSample.Crates
open MiboSample.DemoComponents

// Shared ref used by the subscription to support dynamic remapping without requiring
// subscription replacement. The user can ignore this if they never remap.
let private inputMapRef: InputMap<GameAction> ref = ref InputMap.empty

// ─────────────────────────────────────────────────────────────
// Messages
// ─────────────────────────────────────────────────────────────

[<Struct>]
type Msg =
  | Tick of gt: GameTime
  | InputMapped of actions: ActionState<GameAction>
  | PlayerFired of id: Guid<EntityId> * position: Vector2
  | EmitParticles of position: Vector2 * count: int
  | DemoBoxBounced of count: int
  | SpawnCrate
  | CrateHit of crateId: Guid<EntityId>

let crateRetryMode = RetryMode.Immediate

// ─────────────────────────────────────────────────────────────
// Init
// ─────────────────────────────────────────────────────────────

let init(ctx: GameContext) : struct (Model * Cmd<Msg>) =
  let playerId = Guid.NewGuid() |> UMX.tag<EntityId>

  let positions = Dictionary<Guid<EntityId>, Vector2>()
  positions[playerId] <- Vector2(100.f, 100.f)

  // Configure Input Map
  let inputMap =
    InputMap.empty
    |> InputMap.key MoveLeft Keys.Left
    |> InputMap.key MoveRight Keys.Right
    |> InputMap.key MoveUp Keys.Up
    |> InputMap.key MoveDown Keys.Down
    |> InputMap.key Fire Keys.Space

  inputMapRef.Value <- inputMap

  // Load animation assets
  let animations = Animation.load ctx

  {
    Positions = positions
    Actions = ActionState.empty
    InputMap = inputMap
    Particles = ResizeArray()
    Crates = ResizeArray()
    Speeds = Map.ofList [ playerId, 200.f ]
    Hues = Map.ofList [ playerId, 0.f ]
    TargetHues = Map.ofList [ playerId, 0.f ]
    Sizes = Map.ofList [ playerId, Vector2(32.f, 32.f) ]
    PlayerId = playerId
    BoxBounces = 0
    CrateHits = 0
    PlayerSprite = animations.PlayerSprite
    Decoration = animations.ChestSprite
    CrateSprite = animations.CrateSprite
    ItemSprite = animations.ItemSprite
    VignetteEffect = Assets.effect "Shaders/vignette" ctx
    GrayscaleEffect = Assets.effect "Shaders/grayscale" ctx
    LightingEffect = Assets.effect "Shaders/lighting" ctx
    SphereNormalMap = Assets.texture "sphere_normal" ctx
    TotalTime = 0.0
  },
  Cmd.none

// ─────────────────────────────────────────────────────────────
// Update
// ─────────────────────────────────────────────────────────────

let update
  (boxRef: ComponentRef<InteractiveBoxOverlay>)
  (msg: Msg)
  (model: Model)
  : struct (Model * Cmd<Msg>) =
  match msg with
  | Tick gt ->
    let dt = float32 gt.ElapsedGameTime.TotalSeconds

    let struct (finalModel, allCmds) =
      System.start {
        model with
            TotalTime = model.TotalTime + float gt.ElapsedGameTime.TotalSeconds
      }
      |> System.pipeMutable(Physics.update dt)
      |> System.pipeMutable(Particles.update dt)
      |> System.snapshot Model.toSnapshot
      |> System.pipe(HueColor.update dt 5.f)
      |> System.pipe(Crates.ensureTarget crateRetryMode (fun () -> SpawnCrate))
      |> System.pipe(Crates.detectFirstOverlap(fun id -> CrateHit id))
      |> System.pipe(Player.processActions(fun id pos -> PlayerFired(id, pos)))
      |> System.pipe(Animation.update dt)
      |> System.finish Model.fromSnapshot

    let interopCmd =
      Cmd.ofEffect(
        Effect<Msg>(fun _ ->
          boxRef.TryGet()
          |> ValueOption.iter(fun box ->
            let isFiring = finalModel.Actions.Held.Contains Fire
            box.SpeedScale <- if isFiring then 2.5f else 1.0f
            box.Tint <- if isFiring then Color.HotPink else Color.DeepSkyBlue
            box.Sprite <- ValueSome finalModel.Decoration
            box.SetVisible(true)))
      )

    finalModel, Cmd.batch2(allCmds, interopCmd)

  | InputMapped actions -> { model with Actions = actions }, Cmd.none

  | PlayerFired(id, pos) ->
    let newModel = HueColor.shiftTarget id 15.f model
    newModel, Cmd.ofMsg(EmitParticles(pos, 50)) |> Cmd.deferNextFrame

  | EmitParticles(pos, count) ->
    Particles.emit pos count model
    model, Cmd.none

  | DemoBoxBounced count ->
    { model with BoxBounces = count },
    Cmd.ofMsg(EmitParticles(model.Positions[model.PlayerId], 20))
    |> Cmd.deferNextFrame

  | SpawnCrate ->
    let struct (m, cmd) =
      Crates.spawnOne crateRetryMode (fun () -> SpawnCrate) 800 600 model

    m, cmd

  | CrateHit crateId ->
    let m = Crates.removeCrate crateId model

    m,
    Cmd.batch [
      Cmd.ofMsg(EmitParticles(model.Positions[model.PlayerId], 40))
      |> Cmd.deferNextFrame
      match crateRetryMode with
      | Deferred -> Cmd.ofMsg SpawnCrate |> Cmd.deferNextFrame
      | Immediate -> Cmd.ofMsg SpawnCrate
    ]

// ─────────────────────────────────────────────────────────────
// View
// ─────────────────────────────────────────────────────────────

let view(ctx: GameContext, model: Model, buffer: RenderBuffer<RenderCmd2D>) =
  let uiFont = ctx |> Assets.font "Fonts/monogram"
  let egg = Assets.texture "Objects/Egg_item" ctx
  let snapshot = Model.toSnapshot model
  let playerId = model.PlayerId
  let pos = model.Positions[playerId]
  let hue = model.Hues |> Map.tryFind playerId |> Option.defaultValue 0f
  let color = HueColor.hueToColor hue

  // --- Grayscale Layer (for crates at layer 2) ---
  buffer.Effect(model.GrayscaleEffect, 1<RenderLayer>) |> ignore
  Crates.view ctx snapshot buffer // draws at layer 2

  // --- Lighting Layer (for everything else at layer 5+) ---
  buffer.Effect(model.LightingEffect, 4<RenderLayer>) |> ignore

  // Draw player lit (layer 5)
  Player.view ctx pos model.PlayerSprite color buffer

  // Draw Torch Lit (layer 5)
  let torchPos = Vector2(400f, 300f)
  model.ItemSprite |> AnimatedSprite.draw torchPos 5<RenderLayer> buffer

  // Demo sphere with normal map (layer 5)
  buffer.Sprite(
    sprite {
      texture egg
      at 100 100
      size 64 64
      normalMap model.SphereNormalMap
      layer 5<RenderLayer>
    }
  )
  |> ignore

  // Draw Wall Lit (layer 5)
  for y in 100.f..32.f..500.f do
    model.CrateSprite
    |> AnimatedSprite.draw (Vector2(600.f, y)) 5<RenderLayer> buffer

  // Draw Particles (layer 7, emissive/unlit - no lighting shader)
  buffer.Add(6<RenderLayer>, SetEffect ValueNone)
  Particles.view ctx model.Particles buffer

  // --- Lighting Submission ---
  buffer.PointLight {
    Position = pos
    Color = Color.YellowGreen
    Intensity = 1.5f
    Radius = 256f
    Falloff = 0.5f
    Shadow = ValueSome ShadowSettings2D.defaults
  }
  |> ignore

  let flicker = 1.0f + (float32(Math.Sin(model.TotalTime * 12.0)) * 0.2f)

  buffer.PointLight {
    Position = torchPos
    Color = Color.Orange
    Intensity = 2.5f * flicker
    Radius = 350f
    Falloff = 1.0f
    Shadow = ValueSome ShadowSettings2D.defaults
  }
  |> ignore

  // Directional light (moonlight from upper-left) - made obvious for testing
  buffer.DirectionalLight {
    Direction = Vector2.Normalize(Vector2(1f, 1f)) // Coming from upper-left
    Color = Color.LightYellow
    Intensity = 0.2f
    Shadow = ValueNone
  }
  |> ignore

  buffer.Occluder {
    P1 = Vector2(600f, 100f)
    P2 = Vector2(600f, 500f)
    Height = 1.0f
  }
  |> ignore

  // UI
  buffer
    .Text(
      text {
        font uiFont
        content $"Hits: {snapshot.CrateHits}  Crates: {snapshot.Crates.Count}"
        at pos.X (pos.Y - 15.f)
        color Color.White
        layer 100<RenderLayer>
      }
    )
    .Submit()

// ─────────────────────────────────────────────────────────────
// Subscribe
// ─────────────────────────────────────────────────────────────

let subscribe
  (boxRef: ComponentRef<InteractiveBoxOverlay>)
  (ctx: GameContext)
  (_model: Model)
  =
  Sub.batch [
    InputMapper.subscribe (fun () -> inputMapRef.Value) InputMapped ctx
    InteractiveBoxOverlayBridge.subscribeBounced boxRef DemoBoxBounced
  ]

// ─────────────────────────────────────────────────────────────
// Entry Point
// ─────────────────────────────────────────────────────────────

[<EntryPoint>]
let main argv =
  let interactiveBoxRef = ComponentRef<InteractiveBoxOverlay>()

  let program =
    Program.mkProgram init (update interactiveBoxRef)
    |> Program.withAssets
    |> Program.withRenderer(fun game ->
      let vignetteFx = game.Content.Load<Effect>("Shaders/vignette")
      let shadowCasterFx = game.Content.Load<Effect>("Shaders/shadowcaster")

      let ppConfig =
        PostProcess2D.none
        |> PostProcess2D.withVignette {
          Effect = vignetteFx
          Radius = 7.0f // Almost 0 visibility
          Softness = 0.05f
        }

      let lightingConfig =
        Lighting2DConfig.enabled { Color = Color.White * 0.2f }
        |> Lighting2DConfig.withShadows {
          Shadows2DConfig.defaults with
              SoftShadowQuality = SoftShadowQuality2D.Low
        }
        |> ValueSome

      let shaderOverrides =
        let dict =
          System.Collections.Generic.Dictionary<ShaderBase2D, Effect>()

        dict[ShaderBase2D.ShadowCaster] <- shadowCasterFx

        dict
        :> System.Collections.Generic.IReadOnlyDictionary<ShaderBase2D, Effect>

      Batch2DRenderer.createWithConfig
        game
        {
          Batch2DConfig.defaults with
              PostProcess = ValueSome ppConfig
              Lighting = lightingConfig
              ShaderOverrides = shaderOverrides
        }
        (fun ctx model buffer -> view(ctx, model, buffer)))
    |> Program.withInputMapper inputMapRef.Value
    |> Program.withTick Tick
    |> Program.withSubscription(subscribe interactiveBoxRef)
    |> Program.withComponent BouncingBoxOverlay.create
    |> Program.withComponentRef
      interactiveBoxRef
      InteractiveBoxOverlayBridge.create

  use game = new ElmishGame<Model, Msg>(program)
  game.Run()
  0
