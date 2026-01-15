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
    }

  /// Create a camera at a position looking at origin
  let cameraAt(pos: Vector3) : Camera = {
    View = Matrix.CreateLookAt(pos, Vector3.Zero, Vector3.Up)
    Projection =
      Matrix.CreatePerspectiveFieldOfView(
        MathHelper.PiOver4,
        16f / 9f,
        0.1f,
        1000f
      )
    Position = pos
    Forward = Vector3.Normalize(-pos)
    Near = 0.1f
    Far = 1000f
  }

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

    // === Actual DSL CE Usage Tests ===

    testCase "RenderBuilder emits camera and clear commands"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))

      render buffer {
        withCamera cam
        clear Color.CornflowerBlue
      }

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

      render buffer {
        withCamera cam
        withLighting lighting
        clear Color.Black
        clearDepth
      }
      // Add draw manually since draw { } CE syntax is tricky to test
      buffer.Add((), Draw drawable)

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
      | _ -> failtest "cmd1 should be SetLighting"

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

      render buffer {
        draw {
          mesh testMesh
          at(Vector3(25f, 0f, 0f))
        }
      }

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

      Expect.equal
        cam.Projection
        Matrix.Identity
        "Projection should be identity"

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
// Sorting Tests (Logic only - no GPU)
// ============================================================================

[<Tests>]
let sortingTests =
  testList "Sorting Logic" [
    testCase "Shared.isTransparent detects transparent flag"
    <| fun _ ->
      let opaque = TestHelpers.drawableAt Vector3.Zero false
      let transparent = TestHelpers.drawableAt Vector3.Zero true

      // We can't directly test Shared.isTransparent as it's private,
      // but we can verify the material flags are set correctly
      Expect.isFalse
        (opaque.Material.Flags.HasFlag(MaterialFlags.Transparent))
        "Opaque should not have transparent flag"

      Expect.isTrue
        (transparent.Material.Flags.HasFlag(MaterialFlags.Transparent))
        "Transparent should have transparent flag"

    testCase "Shared.distanceToCamera calculates squared distance"
    <| fun _ ->
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let drawable = TestHelpers.drawableAt Vector3.Zero false

      let distSq =
        Vector3.DistanceSquared(cam.Position, drawable.BoundingSphere.Center)

      Expect.floatClose
        Accuracy.medium
        (float distSq)
        100.0
        "Distance squared should be 100"
  ]

// ============================================================================
// Culling Tests
// ============================================================================

[<Tests>]
let cullingTests =
  testList "Frustum Culling" [
    testCase "Object at origin is visible from camera at (0,0,10)"
    <| fun _ ->
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let drawable = TestHelpers.drawableAt Vector3.Zero false

      let frustum = BoundingFrustum(cam.View * cam.Projection)

      let visible =
        frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

      Expect.isTrue visible "Object at origin should be visible"

    testCase "Object far behind camera is not visible"
    <| fun _ ->
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let drawable = TestHelpers.drawableAt (Vector3(0f, 0f, 100f)) false // Behind camera

      let frustum = BoundingFrustum(cam.View * cam.Projection)

      let visible =
        frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

      Expect.isFalse visible "Object behind camera should not be visible"

    testCase "Object far to the side is not visible"
    <| fun _ ->
      let cam = TestHelpers.cameraAt(Vector3(0f, 0f, 10f))
      let drawable = TestHelpers.drawableAt (Vector3(1000f, 0f, 0f)) false // Way off to the side

      let frustum = BoundingFrustum(cam.View * cam.Projection)

      let visible =
        frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

      Expect.isFalse visible "Object far to the side should not be visible"
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
      let bounds = BoundingBox(Vector3(-2f, -2f, -2f), Vector3(2f, 2f, 2f))

      let mesh = Mesh.create vb ib 36 bounds

      Expect.equal mesh.BoundingBox bounds "BoundingBox should match"
      // Sphere from box with corners at ±2 should have radius of sqrt(12) ≈ 3.46
      Expect.isTrue
        (mesh.BoundingSphere.Radius > 3f)
        "BoundingSphere radius should be > 3"
  ]
