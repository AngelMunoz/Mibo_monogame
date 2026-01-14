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
        
        FrameOrchestration.runCommandStream
            null
            Batch3DConfig.defaults
            (Unchecked.defaultof<GameContext>)
            buffer
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> { Position = Vector3.Zero; Basis = { Right = Vector3.Right; Up = Vector3.Up } })
            (fun _ -> ())
            (fun () -> ())
            opaque
            transparent
            (fun () -> flushes <- flushes + 1)
            
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
        
        FrameOrchestration.runCommandStream
            null
            Batch3DConfig.defaults
            (Unchecked.defaultof<GameContext>)
            buffer
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> { Position = Vector3.Zero; Basis = { Right = Vector3.Right; Up = Vector3.Up } })
            (fun _ -> ())
            (fun () -> ())
            opaque
            transparent
            (fun () -> flushes <- flushes + 1)
            
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
        
        FrameOrchestration.runCommandStream
            null
            Batch3DConfig.defaults
            (Unchecked.defaultof<GameContext>)
            buffer
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> Matrix.Identity)
            (fun _ -> ())
            (fun () -> { Position = Vector3(0f, 0f, 10f); Basis = { Right = Vector3.Right; Up = Vector3.Up } })
            (fun _ -> ())
            (fun () -> ())
            opaque
            transparent
            (fun () -> ())
            
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
        
        FrameExecution.flushSegment
            (fun () -> opaqueSorted <- true)
            (fun () -> transparentSorted <- true)
            (fun _ _ -> ()) // drawSpritesInList
            opaque
            transparent
            (fun _ -> drawCalls <- drawCalls + 1)
            
        Expect.isTrue opaqueSorted "Opaque sort should have been called"
        Expect.isTrue transparentSorted "Transparent sort should have been called"
        Expect.equal drawCalls 1 "Should have called drawMeshCmd once"
        Expect.equal opaque.Count 0 "Opaque list should be cleared"
    ]
  ]
