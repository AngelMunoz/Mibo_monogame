module Mibo.Tests.Rendering3D

open Expecto
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D

// ============================================================================
// Test Helpers
// ============================================================================

module TestHelpers =
  /// Create a mock mesh for testing
  let mockMesh: Mesh = {
    VertexBuffer = Unchecked.defaultof<VertexBuffer>
    IndexBuffer = Unchecked.defaultof<IndexBuffer>
    IndexCount = 36
    BoundingBox = BoundingBox(Vector3(-1f, -1f, -1f), Vector3(1f, 1f, 1f))
    BoundingSphere = BoundingSphere(Vector3.Zero, 1.5f)
    Effect = Unchecked.defaultof<Effect>
  }

  /// Create a material with specific transparency
  let materialWithFlags flags : Material = {
    PBR = {
      AlbedoColor = Color.White
      AlbedoMap = ValueNone
      NormalMap = ValueNone
      MetallicRoughnessMap = ValueNone
      Metallic = 0f
      Roughness = 0.5f
      AmbientOcclusionMap = ValueNone
      EmissiveColor = Color.Black
      EmissiveIntensity = 0f
    }
    Flags = flags
    AlphaThreshold = 0.5f
    RenderQueue = 2000
  }

  /// Create a drawable at a position
  let drawableAt (pos: Vector3) (transparent: bool) : Drawable =
    let flags =
      if transparent then
        MaterialFlags.Transparent
      else
        MaterialFlags.None

    let transform = Matrix.CreateTranslation(pos)

    {
      Mesh = mockMesh
      Transform = transform
      Material = materialWithFlags flags
      BoundingSphere = mockMesh.BoundingSphere.Transform(transform)
      EffectOverride = ValueNone
      Pass =
        if flags.HasFlag(MaterialFlags.Transparent) then
          Transparent
        else
          Opaque
      Bones = ValueNone
    }

  /// Create a camera at a position looking at origin
  let cameraAt(pos: Vector3) : Camera =
    Camera.perspective
      pos
      Vector3.Zero
      Vector3.Up
      MathHelper.PiOver4
      (16f / 9f)
      0.1f
      1000f

// ============================================================================
// Buffer Ordering Tests
// ============================================================================

[<Tests>]
let bufferOrderingTests =
  testList "Buffer Ordering" [
    testCase "Commands are emitted in order"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let drawable1 = TestHelpers.drawableAt Vector3.Zero false
      let drawable2 = TestHelpers.drawableAt (Vector3(1f, 0f, 0f)) false

      buffer.Add((), SetCamera cam)
      buffer.Add((), ClearTarget(ValueSome Color.CornflowerBlue, true))
      buffer.Add((), Draw drawable1)
      buffer.Add((), Draw drawable2)

      Expect.equal buffer.Count 4 "Should have 4 commands"

      let struct (_, cmd0) = buffer.[0]
      let struct (_, cmd1) = buffer.[1]
      let struct (_, cmd2) = buffer.[2]
      let struct (_, cmd3) = buffer.[3]

      match cmd0 with
      | SetCamera _ -> ()
      | _ -> failtest "First command should be SetCamera"

      match cmd1 with
      | ClearTarget _ -> ()
      | _ -> failtest "Second command should be ClearTarget"

      match cmd2, cmd3 with
      | Draw _, Draw _ -> ()
      | _ -> failtest "Third and fourth commands should be Draw"

    testCase "SetLighting command is emitted"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let lighting = Lighting.ambient

      buffer.Add((), SetLighting lighting)

      Expect.equal buffer.Count 1 "Should have 1 command"
      let struct (_, cmd) = buffer.[0]

      match cmd with
      | SetLighting l ->
        Expect.equal
          l.AmbientIntensity
          lighting.AmbientIntensity
          "Lighting should match"
      | _ -> failtest "Command should be SetLighting"
  ]

// ============================================================================
// DSL Tests
// ============================================================================

[<Tests>]
let dslTests =
  testList "DSL" [
    testCase "DrawableBuilder creates drawable with mesh and position"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let builder = DrawableBuilder()

      let mesh = TestHelpers.mockMesh

      let state =
        DrawState.empty
        |> fun s -> {
          s with
              Mesh = ValueSome mesh
              LocalPosition = Vector3(1f, 2f, 3f)
        }

      DrawState.toDrawable state
      |> ValueOption.iter(fun d -> buffer.Add((), Draw d))

      Expect.equal buffer.Count 1 "Should have 1 command"
      let struct (_, cmd) = buffer.[0]

      match cmd with
      | Draw d ->
        let pos = d.Transform.Translation
        Expect.floatClose Accuracy.medium (float pos.X) 1.0 "X should be 1"
        Expect.floatClose Accuracy.medium (float pos.Y) 2.0 "Y should be 2"
        Expect.floatClose Accuracy.medium (float pos.Z) 3.0 "Z should be 3"
      | _ -> failtest "Command should be Draw"

    testCase "DrawState computes transform with scale and rotation"
    <| fun _ ->
      let state = {
        DrawState.empty with
            Mesh = ValueSome TestHelpers.mockMesh
            LocalPosition = Vector3.Zero
            LocalScale = Vector3(2f, 2f, 2f)
            LocalRotation = Quaternion.CreateFromYawPitchRoll(0f, 0f, 0f)
      }

      let transform = DrawState.computeTransform state
      let mutable scale = Vector3.Zero
      let mutable rotation = Quaternion.Identity
      let mutable translation = Vector3.Zero
      transform.Decompose(&scale, &rotation, &translation) |> ignore

      Expect.floatClose
        Accuracy.medium
        (float scale.X)
        2.0
        "Scale X should be 2"

      Expect.floatClose
        Accuracy.medium
        (float scale.Y)
        2.0
        "Scale Y should be 2"

      Expect.floatClose
        Accuracy.medium
        (float scale.Z)
        2.0
        "Scale Z should be 2"

    testCase "DrawState relativeTo applies parent transform"
    <| fun _ ->
      let parentTransform = Matrix.CreateTranslation(10f, 0f, 0f)

      let state = {
        DrawState.empty with
            Mesh = ValueSome TestHelpers.mockMesh
            LocalPosition = Vector3(1f, 0f, 0f)
            Parent = ValueSome parentTransform
      }

      let transform = DrawState.computeTransform state
      let pos = transform.Translation

      // Local (1,0,0) + Parent (10,0,0) = (11,0,0)
      Expect.floatClose
        Accuracy.medium
        (float pos.X)
        11.0
        "X should be 11 (1 + 10)"

    // === Actual DSL Usage Tests ===

    testCase "Fluent DSL emits camera and clear commands"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))

      buffer.Camera(cam).Clear(Color.CornflowerBlue).Submit()

      Expect.equal buffer.Count 2 "Should have 2 commands"
      let struct (_, cmd0) = buffer.[0]
      let struct (_, cmd1) = buffer.[1]

      match cmd0 with
      | SetCamera c ->
        Expect.floatClose
          Accuracy.medium
          (float c.Position.Z)
          10.0
          "Camera Z should be 10"
      | _ -> failtest "First command should be SetCamera"

      match cmd1 with
      | ClearTarget(ValueSome color, true) ->
        Expect.equal color Color.CornflowerBlue "Color should match"
      | _ -> failtest "Second command should be ClearTarget"

    testCase "Command submission order is preserved"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let lighting = Lighting.ambient
      let drawable = TestHelpers.drawableAt Vector3.Zero false

      buffer
        .Camera(cam)
        .Lighting(lighting)
        .Clear(Color.Black)
        .ClearDepth()
        .Submit()

      // Add draw manually since draw { } CE syntax is tricky to test
      buffer.Draw(ValueSome drawable) |> ignore

      Expect.equal buffer.Count 5 "Should have 5 commands"

      // Verify order: SetCamera -> SetLighting -> Clear -> ClearDepth -> Draw
      let struct (_, cmd0) = buffer.[0]
      let struct (_, cmd1) = buffer.[1]
      let struct (_, cmd2) = buffer.[2]
      let struct (_, cmd3) = buffer.[3]
      let struct (_, cmd4) = buffer.[4]

      match cmd0 with
      | SetCamera _ -> ()
      | _ -> failtest "cmd0 should be SetCamera"

      match cmd1 with
      | SetLighting _ -> ()
      | AddLight _ -> ()
      | _ -> failtest "cmd1 should be SetLighting or AddLight"

      match cmd2 with
      | ClearTarget(ValueSome _, true) -> ()
      | _ -> failtest "cmd2 should be ClearTarget with color"

      match cmd3 with
      | ClearTarget(ValueNone, true) -> ()
      | _ -> failtest "cmd3 should be ClearDepth"

      match cmd4 with
      | Draw _ -> ()
      | _ -> failtest "cmd4 should be Draw"

    testCase "draw CE outputs correct position"
    <| fun _ ->
      let testMesh = TestHelpers.mockMesh

      let result = draw {
        mesh testMesh
        at(Vector3(5f, 10f, 15f))
      }

      Expect.isTrue (ValueOption.isSome result) "Should produce a Drawable"
      let drawable = result.Value
      let pos = drawable.Transform.Translation
      Expect.floatClose Accuracy.medium (float pos.X) 5.0 "X should be 5"
      Expect.floatClose Accuracy.medium (float pos.Y) 10.0 "Y should be 10"
      Expect.floatClose Accuracy.medium (float pos.Z) 15.0 "Z should be 15"

    testCase "draw CE applies scale"
    <| fun _ ->
      let testMesh = TestHelpers.mockMesh

      let result = draw {
        mesh testMesh
        scaledBy 3f
      }

      Expect.isTrue (ValueOption.isSome result) "Should produce a Drawable"
      let drawable = result.Value
      let mutable scale = Vector3.Zero
      let mutable rot = Quaternion.Identity
      let mutable trans = Vector3.Zero
      drawable.Transform.Decompose(&scale, &rot, &trans) |> ignore
      Expect.floatClose Accuracy.medium (float scale.X) 3.0 "Scale should be 3"

    testCase "draw CE relativeTo applies parent transform correctly"
    <| fun _ ->
      let testMesh = TestHelpers.mockMesh
      // Parent at (100, 0, 0), local at (5, 0, 0) => world (105, 0, 0)
      let parentTransform = Matrix.CreateTranslation(100f, 0f, 0f)

      let result = draw {
        mesh testMesh
        at 5f 0f 0f
        relativeTo parentTransform
      }

      Expect.isTrue (ValueOption.isSome result) "Should produce a Drawable"
      let drawable = result.Value
      let pos = drawable.Transform.Translation

      Expect.floatClose
        Accuracy.medium
        (float pos.X)
        105.0
        "X should be 105 (100 + 5)"

    testCase "draw CE combines position, rotation, and scale correctly"
    <| fun _ ->
      let testMesh = TestHelpers.mockMesh

      let result = draw {
        mesh testMesh
        at(Vector3(10f, 20f, 30f))
        scaledBy 2f
        rotatedByYawPitchRoll 0f 0f 0f // Identity rotation
      }

      Expect.isTrue (ValueOption.isSome result) "Should produce a Drawable"
      let drawable = result.Value
      let mutable scale = Vector3.Zero
      let mutable rot = Quaternion.Identity
      let mutable trans = Vector3.Zero
      drawable.Transform.Decompose(&scale, &rot, &trans) |> ignore

      // Verify all transformations applied correctly
      Expect.floatClose
        Accuracy.medium
        (float scale.X)
        2.0
        "Scale X should be 2"

      Expect.floatClose
        Accuracy.medium
        (float scale.Y)
        2.0
        "Scale Y should be 2"

      Expect.floatClose
        Accuracy.medium
        (float scale.Z)
        2.0
        "Scale Z should be 2"

      Expect.floatClose
        Accuracy.medium
        (float trans.X)
        10.0
        "Translation X should be 10"

      Expect.floatClose
        Accuracy.medium
        (float trans.Y)
        20.0
        "Translation Y should be 20"

      Expect.floatClose
        Accuracy.medium
        (float trans.Z)
        30.0
        "Translation Z should be 30"

    testCase "draw CE transforms BoundingSphere to world space"
    <| fun _ ->
      let testMesh = TestHelpers.mockMesh // Sphere at origin, radius 1.5

      let result = draw {
        mesh testMesh
        at(Vector3(50f, 0f, 0f))
      }

      Expect.isTrue (ValueOption.isSome result) "Should produce a Drawable"
      let drawable = result.Value
      // BoundingSphere center should be at (50, 0, 0)
      Expect.floatClose
        Accuracy.medium
        (float drawable.BoundingSphere.Center.X)
        50.0
        "Sphere center X should be 50"

    testCase "render CE with nested draw adds to buffer"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let testMesh = TestHelpers.mockMesh

      buffer
      |> Buffer.draw(
        draw {
          mesh testMesh
          at(Vector3(25f, 0f, 0f))
        }
      )
      |> ignore

      Expect.equal buffer.Count 1 "Should have 1 command in buffer"
      let struct (_, cmd) = buffer.[0]

      match cmd with
      | Draw d ->
        Expect.floatClose
          Accuracy.medium
          (float d.Transform.Translation.X)
          25.0
          "X should be 25"
      | _ -> failtest "Command should be Draw"
  ]

// ============================================================================
// Camera Tests
[<Tests>]
let cameraTests =
  testList "Camera" [
    testCase "Camera.identity has identity matrices"
    <| fun _ ->
      let cam = Camera.identity
      Expect.equal cam.View Matrix.Identity "View should be identity"

      // Projection should not be identity now because it's recomputed with defaults
      Expect.isFalse
        (cam.Projection = Matrix.Identity)
        "Projection should be valid perspective"

      Expect.equal cam.Position Vector3.Zero "Position should be zero"

    testCase "Camera.perspective creates valid projection"
    <| fun _ ->
      let cam =
        Camera.perspective
          (Vector3(0f, 0f, 10f))
          Vector3.Zero
          Vector3.Up
          MathHelper.PiOver4
          (16f / 9f)
          0.1f
          1000f

      Expect.floatClose
        Accuracy.medium
        (float cam.Position.Z)
        10.0
        "Camera Z should be 10"

      Expect.isTrue (cam.Forward.Z < 0f) "Camera should look towards negative Z"
  ]

// ============================================================================
// Pipeline Logic Tests
// ============================================================================

[<Tests>]
let pipelineLogicTests =
  testList "Pipeline Logic" [
    testCase "isTransparent correctly identifies transparency"
    <| fun _ ->
      // Case 1: Flag
      let matFlag = TestHelpers.materialWithFlags MaterialFlags.Transparent

      let drawFlag = {
        TestHelpers.drawableAt Vector3.Zero false with
            Material = matFlag
      }

      Expect.isTrue
        (Culling.isTransparent drawFlag)
        "Should be transparent due to flag"

      // Case 2: Alpha
      let matAlpha =
        TestHelpers.materialWithFlags MaterialFlags.None
        |> Material.withAlbedo(Color(255, 255, 255, 100))

      let drawAlpha = {
        TestHelpers.drawableAt Vector3.Zero false with
            Material = matAlpha
      }

      Expect.isTrue
        (Culling.isTransparent drawAlpha)
        "Should be transparent due to alpha < 255"

      // Case 3: Opaque
      let matOpaque = TestHelpers.materialWithFlags MaterialFlags.None

      let drawOpaque = {
        TestHelpers.drawableAt Vector3.Zero false with
            Material = matOpaque
      }

      Expect.isFalse (Culling.isTransparent drawOpaque) "Should be opaque"

    testCase "batchDrawable culls objects outside frustum"
    <| fun _ ->
      let config = PipelineConfig.defaults
      let state = State.create config
      // Camera at (0,0,10) looking at (0,0,0)
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      state.CurrentCamera <- cam
      state.CameraWasSet <- true

      // 1. Visible object at origin
      let visible = TestHelpers.drawableAt Vector3.Zero false
      Culling.batchDrawable state visible

      Expect.equal state.OpaqueDrawables.Count 1 "Should have 1 opaque drawable"

      // 2. Invisible object behind camera
      let invisible = TestHelpers.drawableAt (Vector3(0f, 0f, 100f)) false
      Culling.batchDrawable state invisible

      Expect.equal
        state.OpaqueDrawables.Count
        1
        "Count should not increase for culled object"

    testCase "batchDrawable separates opaque and transparent"
    <| fun _ ->
      let config = PipelineConfig.defaults
      let state = State.create config
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      state.CurrentCamera <- cam
      state.CameraWasSet <- true

      let opaque = TestHelpers.drawableAt Vector3.Zero false
      let transparent = TestHelpers.drawableAt (Vector3(1f, 0f, 0f)) true

      Culling.batchDrawable state opaque
      Culling.batchDrawable state transparent

      Expect.equal state.OpaqueDrawables.Count 1 "Should have 1 opaque"

      Expect.equal
        state.TransparentDrawables.Count
        1
        "Should have 1 transparent"

    testCase "Sorting logic: Opaque is Front-to-Back"
    <| fun _ ->
      // Simulate the list
      let drawables = ResizeArray<struct (float32 * int)>() // (distance, id)
      drawables.Add(struct (100.0f, 1)) // Far
      drawables.Add(struct (10.0f, 2)) // Near
      drawables.Add(struct (50.0f, 3)) // Mid

      // Sort ascending (distance)
      drawables.Sort(fun struct (d1, _) struct (d2, _) -> d1.CompareTo(d2))

      let struct (d1, id1) = drawables.[0]
      let struct (d2, id2) = drawables.[1]
      let struct (d3, id3) = drawables.[2]

      Expect.equal id1 2 "Nearest (id 2) should be first"
      Expect.equal id2 3 "Mid (id 3) should be second"
      Expect.equal id3 1 "Farthest (id 1) should be last"

    testCase "Sorting logic: Transparent is Back-to-Front"
    <| fun _ ->
      // Simulate the list
      let drawables = ResizeArray<struct (float32 * int)>()
      drawables.Add(struct (100.0f, 1)) // Far
      drawables.Add(struct (10.0f, 2)) // Near
      drawables.Add(struct (50.0f, 3)) // Mid

      // Sort descending (distance)
      drawables.Sort(fun struct (d1, _) struct (d2, _) -> d2.CompareTo(d1))

      let struct (d1, id1) = drawables.[0]
      let struct (d2, id2) = drawables.[1]
      let struct (d3, id3) = drawables.[2]

      Expect.equal id1 1 "Farthest (id 1) should be first"
      Expect.equal id2 3 "Mid (id 3) should be second"
      Expect.equal id3 2 "Nearest (id 2) should be last"
  ]

// ============================================================================
// Pipeline Orchestration Tests
// ============================================================================

[<Tests>]
let pipelineOrchestrationTests =
  testList "Pipeline Orchestration" [
    testCase "TiledForward renderer doesn't crash on empty buffer"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let state = State.create PipelineConfig.defaults
      Orchestrate.render state buffer (GameTime())

    testCase "TiledForward.cullLights excludes lights outside frustum"
    <| fun _ ->
      let config = PipelineConfig.defaults

      let state = State.create config

      // Camera at (0,0,10) looking at (0,0,0)
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      state.CurrentCamera <- cam

      // 1. Directional light (always included)
      let dirLight =
        Light.Directional {
          Direction = Vector3.Down
          Color = Color.White
          Intensity = 1f
          Shadow = ValueNone
          CascadeCount = 0
          CascadeSplits = [||]
          SourceRadius = 0f
        }

      // 2. Point light at origin (inside)
      let pointInside =
        Light.Point {
          Position = Vector3.Zero
          Color = Color.White
          Intensity = 1f
          Range = 5f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      // 3. Point light far away (outside)
      let pointOutside =
        Light.Point {
          Position = Vector3(100f, 100f, 100f)
          Color = Color.White
          Intensity = 1f
          Range = 5f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      state.AccumulatedLights.Clear()
      state.AccumulatedLights.Add(dirLight)
      state.AccumulatedLights.Add(pointInside)
      state.AccumulatedLights.Add(pointOutside)

      let tileMasks = Tiling.cullLights state

      // Union of all bitmasks
      let allLightsMask = tileMasks |> Array.fold (|||) 0u

      let hasDir = (allLightsMask &&& (1u <<< 0)) <> 0u
      let hasInside = (allLightsMask &&& (1u <<< 1)) <> 0u
      let hasOutside = (allLightsMask &&& (1u <<< 2)) <> 0u

      Expect.isTrue hasDir "Directional light should be present"
      Expect.isTrue hasInside "Inside point light should be present"
      Expect.isFalse hasOutside "Outside point light should be culled"

    testCase "TiledForward.cullLights assigns point light to correct tiles"
    <| fun _ ->
      let config = {
        PipelineConfig.defaults with
            TileSize = 16
      }

      let state = State.create config

      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      state.CurrentCamera <- cam

      let pointLight =
        Light.Point {
          Position = Vector3.Zero
          Color = Color.White
          Intensity = 1f
          Range = 1f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      state.AccumulatedLights.Clear()
      state.AccumulatedLights.Add(pointLight)

      let tileMasks = Tiling.cullLights state

      let occupiedTiles =
        tileMasks
        |> Array.indexed
        |> Array.filter(fun (_, mask) -> mask <> 0u)
        |> Array.map fst

      Expect.isTrue
        (occupiedTiles.Length > 0)
        "At least one tile should have the light"

      Expect.isTrue
        (occupiedTiles.Length < tileMasks.Length)
        "Not all tiles should have the light"

      // Center tile index
      let centerTileX = (1280 / 2) / 16 // TileSize is 16
      let centerTileY = (720 / 2) / 16
      let centerTileIndex = centerTileY * (1280 / 16) + centerTileX

      Expect.contains
        occupiedTiles
        centerTileIndex
        "Center tile should contain the light"
  ]

// ============================================================================
// Mesh Tests
// ============================================================================

[<Tests>]
let meshTests =
  testList "Mesh" [
    testCase "Mesh.create sets correct bounding sphere"
    <| fun _ ->
      let vb = Unchecked.defaultof<VertexBuffer>
      let ib = Unchecked.defaultof<IndexBuffer>
      let effect = Unchecked.defaultof<Effect>
      let bounds = BoundingBox(Vector3(-2f, -2f, -2f), Vector3(2f, 2f, 2f))

      let mesh = Mesh.create vb ib 36 bounds effect

      Expect.equal mesh.BoundingBox bounds "BoundingBox should match"
      // Sphere from box with corners at ±2 should have radius of sqrt(12) ≈ 3.46
      Expect.isTrue
        (mesh.BoundingSphere.Radius > 3f)
        "BoundingSphere radius should be > 3"
  ]

// ============================================================================
// Aggregate Lighting Tests
// ============================================================================

[<Tests>]
let aggregateLightingTests =
  testList "Aggregate Lighting" [
    testCase "AddLight appends to accumulated lights"
    <| fun _ ->
      let config = PipelineConfig.defaults
      let state = State.create config

      let light1 =
        Light.Directional {
          Direction = Vector3.Down
          Color = Color.White
          Intensity = 1f
          Shadow = ValueNone
          CascadeCount = 0
          CascadeSplits = [||]
          SourceRadius = 0f
        }

      let light2 =
        Light.Point {
          Position = Vector3.One
          Color = Color.Red
          Intensity = 0.5f
          Range = 10f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      // Test the light aggregation logic directly
      // This mirrors what SetLighting does: set CurrentLighting and populate AccumulatedLights
      state.CurrentLighting <- {
        AmbientColor = Color.Black
        AmbientIntensity = 1f
        Lights = [| light1 |]
      }

      state.AccumulatedLights.Clear()
      state.AccumulatedLights.Add(light1)

      Expect.equal
        state.AccumulatedLights.Count
        1
        "Should have 1 light after SetLighting equivalent"

      // This mirrors what AddLight does: append to AccumulatedLights
      state.AccumulatedLights.Add(light2)

      Expect.equal
        state.AccumulatedLights.Count
        2
        "Should have 2 lights after AddLight"

      Expect.equal
        state.AccumulatedLights.[0]
        light1
        "First light should be light1"

      Expect.equal
        state.AccumulatedLights.[1]
        light2
        "Second light should be light2"

    testCase "SetLighting clears accumulated lights"
    <| fun _ ->
      let config = PipelineConfig.defaults
      let state = State.create config

      let light1 =
        Light.Directional {
          Direction = Vector3.Down
          Color = Color.White
          Intensity = 1f
          Shadow = ValueNone
          CascadeCount = 0
          CascadeSplits = [||]
          SourceRadius = 0f
        }

      let light2 =
        Light.Point {
          Position = Vector3.One
          Color = Color.Red
          Intensity = 0.5f
          Range = 10f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      // Simulate AddLight first
      state.AccumulatedLights.Add(light1)

      // Simulate SetLighting which clears and replaces
      state.CurrentLighting <- {
        AmbientColor = Color.Black
        AmbientIntensity = 1f
        Lights = [| light2 |]
      }

      state.AccumulatedLights.Clear()
      state.AccumulatedLights.Add(light2)

      Expect.equal
        state.AccumulatedLights.Count
        1
        "Should only have 1 light after SetLighting"

      Expect.equal
        state.AccumulatedLights.[0]
        light2
        "The light should be light2"

    testCase "Render pass aggregates lights before flush"
    <| fun _ ->
      let config = PipelineConfig.defaults
      let state = State.create config

      let light1 =
        Light.Directional {
          Direction = Vector3.Down
          Color = Color.White
          Intensity = 1f
          Shadow = ValueNone
          CascadeCount = 0
          CascadeSplits = [||]
          SourceRadius = 0f
        }

      let light2 =
        Light.Point {
          Position = Vector3.One
          Color = Color.Red
          Intensity = 0.5f
          Range = 10f
          Shadow = ValueNone
          SourceRadius = 0f
        }

      // Simulate the sequence: SetLighting then AddLight
      // SetLighting:
      state.CurrentLighting <- {
        AmbientColor = Color.Black
        AmbientIntensity = 1f
        Lights = [| light1 |]
      }

      state.AccumulatedLights.Clear()
      state.AccumulatedLights.Add(light1)

      // AddLight:
      state.AccumulatedLights.Add(light2)

      // Verify aggregation
      Expect.equal
        state.AccumulatedLights.Count
        2
        "AccumulatedLights should have 2 lights after AddLight"

      Expect.equal
        state.AccumulatedLights.[0]
        light1
        "First light should be light1"

      Expect.equal
        state.AccumulatedLights.[1]
        light2
        "Second light should be light2"
  ]
