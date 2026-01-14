module Mibo.Tests.Graphics3D

open Expecto
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics3D

[<Tests>]
let tests =
  testList "Graphics3D" [
    testList "CameraState" [
      testCase "billboardBasis Spherical uses camera basis" <| fun _ ->
        let camBasis : CameraState.CameraBasis = {
            Right = Vector3.Right
            Up = Vector3.Up
        }
        let camInfo : CameraState.CameraInfo = {
            Position = Vector3(0f, 0f, 10f)
            Basis = camBasis
        }
        let pos = Vector3.Zero
        let struct (right, up) = CameraState.billboardBasis Spherical camInfo pos
        
        Expect.equal right Vector3.Right "Right should match camera right"
        Expect.equal up Vector3.Up "Up should match camera up"
 
      testCase "billboardBasis Cylindrical faces camera on Y axis" <| fun _ ->
        let camInfo : CameraState.CameraInfo = {
            Position = Vector3(10f, 0f, 0f)
            Basis = { Right = Vector3.Forward; Up = Vector3.Up }
        }
        let pos = Vector3.Zero
        let struct (right, up) = CameraState.billboardBasis (Cylindrical Vector3.Up) camInfo pos
        
        // Right = Up x ViewDir. ViewDir = camPos - pos = (10, 0, 0)
        // Right = (0, 1, 0) x (10, 0, 0) = (0, 0, -10). Normalized -> (0, 0, -1)
        Expect.floatClose Accuracy.medium (float right.X) 0.0 "Right X should be 0"
        Expect.floatClose Accuracy.medium (float right.Y) 0.0 "Right Y should be 0"
        Expect.floatClose Accuracy.medium (float right.Z) -1.0 "Right Z should be -1"
        
        // Up = ViewDir x Right = (10, 0, 0) x (0, 0, -1) = (0, 10, 0). Normalized -> (0, 1, 0)
        Expect.floatClose Accuracy.medium (float up.X) 0.0 "Up X should be 0"
        Expect.floatClose Accuracy.medium (float up.Y) 1.0 "Up Y should be 1"
        Expect.floatClose Accuracy.medium (float up.Z) 0.0 "Up Z should be 0"
    ]
 
    testList "FrameOrchestration" [
      testCase "runCommandStream partitions opaque and transparent" <| fun _ ->
        let buffer = RenderBuffer<RenderCmd3D>()
        let model = Unchecked.defaultof<Model>
        let transform = Matrix.Identity
        
        buffer.Add((), DrawMesh(Opaque, model, transform, ValueNone, ValueNone, ValueNone))
        buffer.Add((), DrawMesh(Transparent, model, transform, ValueNone, ValueNone, ValueNone))
        
        let opaque = ResizeArray<struct (float32 * RenderCmd3D)>()
        let transparent = ResizeArray<struct (float32 * RenderCmd3D)>()
        let mutable flushes = 0
        
        let state = FrameOrchestration.RenderState()
        let lists : FrameOrchestration.RenderLists = {
          Opaque = opaque
          Transparent = transparent
        }
        let pipeline = {
          new FrameOrchestration.IRenderPipeline with
            member _.ClearLists() = ()
            member _.FlushSegment() = flushes <- flushes + 1
            member _.SortOpaque() = ()
            member _.SortTransparent() = ()
            member _.DrawMesh _ = ()
            member _.DrawSprites _ _ = ()
        }
        let env : FrameOrchestration.RenderEnv = {
          Device = null
          Config = Batch3DConfig.defaults
        }
        
        buffer |> FrameOrchestration.runCommandStream pipeline state env lists (Unchecked.defaultof<GameContext>)
            
        Expect.equal opaque.Count 1 "Should have 1 opaque command"
        Expect.equal transparent.Count 1 "Should have 1 transparent command"
        Expect.equal flushes 1 "Should have flushed once at the end"
 
      testCase "runCommandStream flushes on barriers" <| fun _ ->
        let buffer = RenderBuffer<RenderCmd3D>()
        let model = Unchecked.defaultof<Model>
        
        buffer.Add((), DrawMesh(Opaque, model, Matrix.Identity, ValueNone, ValueNone, ValueNone))
        buffer.Add((), SetCamera { View = Matrix.Identity; Projection = Matrix.Identity })
        buffer.Add((), DrawMesh(Opaque, model, Matrix.Identity, ValueNone, ValueNone, ValueNone))
        
        let opaque = ResizeArray<struct (float32 * RenderCmd3D)>()
        let transparent = ResizeArray<struct (float32 * RenderCmd3D)>()
        let mutable flushes = 0
        
        let state = FrameOrchestration.RenderState()
        let lists : FrameOrchestration.RenderLists = {
          Opaque = opaque
          Transparent = transparent
        }
        let pipeline = {
          new FrameOrchestration.IRenderPipeline with
            member _.ClearLists() = ()
            member _.FlushSegment() = flushes <- flushes + 1
            member _.SortOpaque() = ()
            member _.SortTransparent() = ()
            member _.DrawMesh _ = ()
            member _.DrawSprites _ _ = ()
        }
        let env : FrameOrchestration.RenderEnv = {
          Device = null
          Config = Batch3DConfig.defaults
        }
        
        buffer |> FrameOrchestration.runCommandStream pipeline state env lists (Unchecked.defaultof<GameContext>)
            
        Expect.equal flushes 2 "Should have flushed twice (once at SetCamera, once at end)"
        
      testCase "runCommandStream calculates distance for sorting" <| fun _ ->
        let buffer = RenderBuffer<RenderCmd3D>()
        let model = Unchecked.defaultof<Model>
        
        // Cam at (0,0,10)
        // Mesh 1 at (0,0,0) -> dist 10, distSq 100
        // Mesh 2 at (0,0,5) -> dist 5, distSq 25
        buffer.Add((), DrawMesh(Opaque, model, Matrix.CreateTranslation(0f, 0f, 0f), ValueNone, ValueNone, ValueNone))
        buffer.Add((), DrawMesh(Opaque, model, Matrix.CreateTranslation(0f, 0f, 5f), ValueNone, ValueNone, ValueNone))
        
        let opaque = ResizeArray<struct (float32 * RenderCmd3D)>()
        let transparent = ResizeArray<struct (float32 * RenderCmd3D)>()
        
        let state = FrameOrchestration.RenderState()
        state.View <- Matrix.CreateLookAt(Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.Up)
        let lists : FrameOrchestration.RenderLists = {
          Opaque = opaque
          Transparent = transparent
        }
        let pipeline = {
          new FrameOrchestration.IRenderPipeline with
            member _.ClearLists() = ()
            member _.FlushSegment() = ()
            member _.SortOpaque() = ()
            member _.SortTransparent() = ()
            member _.DrawMesh _ = ()
            member _.DrawSprites _ _ = ()
        }
        let env : FrameOrchestration.RenderEnv = {
          Device = null
          Config = Batch3DConfig.defaults
        }
        
        buffer |> FrameOrchestration.runCommandStream pipeline state env lists (Unchecked.defaultof<GameContext>)
            
        let struct (d1, _) = opaque[0]
        let struct (d2, _) = opaque[1]
        
        Expect.floatClose Accuracy.medium (float d1) 100.0 "First mesh distance squared should be 100"
        Expect.floatClose Accuracy.medium (float d2) 25.0 "Second mesh distance squared should be 25"
    ]
 
    testList "FrameExecution" [
      testCase "flushSegment calls sort and draw functions" <| fun _ ->
        let opaque = ResizeArray<struct (float32 * RenderCmd3D)>()
        let transparent = ResizeArray<struct (float32 * RenderCmd3D)>()
        opaque.Add struct (10f, Unchecked.defaultof<RenderCmd3D>)
        
        let mutable opaqueSorted = false
        let mutable transparentSorted = false
        let mutable drawCalls = 0
        
        let lists : FrameOrchestration.RenderLists = {
          Opaque = opaque
          Transparent = transparent
        }
        let pipeline = {
          new FrameOrchestration.IRenderPipeline with
            member _.ClearLists() = ()
            member _.FlushSegment() = ()
            member _.SortOpaque() = opaqueSorted <- true
            member _.SortTransparent() = transparentSorted <- true
            member _.DrawMesh _ = drawCalls <- drawCalls + 1
            member _.DrawSprites _ _ = ()
        }
        
        pipeline |> FrameExecution.flushSegment lists
            
        Expect.isTrue opaqueSorted "Opaque sort should have been called"
        Expect.isTrue transparentSorted "Transparent sort should have been called"
        Expect.equal drawCalls 1 "Should have called drawMeshCmd once"
        Expect.equal opaque.Count 0 "Opaque list should be cleared"
    ]


    testList "Draw3D Builders" [
      testCase "mesh builder creates opaque command with defaults" <| fun _ ->
        let model = Unchecked.defaultof<Model>
        let transform = Matrix.Identity
        let cmd = Draw3D.mesh model transform
        
        Expect.equal cmd.Model model "Model should match"
        Expect.equal cmd.Transform transform "Transform should match"
        Expect.equal cmd.Pass Opaque "Pass should be Opaque"
        Expect.equal cmd.Color ValueNone "Color should be None"
        Expect.equal cmd.Texture ValueNone "Texture should be None"
        Expect.isTrue (ValueOption.isNone cmd.Setup) "Setup should be None"

      testCase "meshTransparent builder creates transparent command" <| fun _ ->
        let model = Unchecked.defaultof<Model>
        let transform = Matrix.Identity
        let cmd = Draw3D.meshTransparent model transform
        
        Expect.equal cmd.Pass Transparent "Pass should be Transparent"

      testCase "withColor modifies builder" <| fun _ ->
        let model = Unchecked.defaultof<Model>
        let cmd = 
            Draw3D.mesh model Matrix.Identity 
            |> Draw3D.withColor Color.Red
            
        Expect.equal cmd.Color (ValueSome Color.Red) "Color should be updated"

      testCase "quad3D creates correct geometry" <| fun _ ->
        let center = Vector3.Zero
        let right = Vector3.Right
        let up = Vector3.Up
        let q = Draw3D.quad3D center right up
        
        Expect.equal q.Center center "Center should match"
        Expect.equal q.Right right "Right should match"
        Expect.equal q.Up up "Up should match"
        Expect.equal q.Color Color.White "Default color should be White"
        Expect.equal q.Uv UvRect.full "Default UV should be full"
    ]
  ]

