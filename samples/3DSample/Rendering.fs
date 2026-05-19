namespace _3DSample

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

// ============================================================================
// Sample Render Commands
// ============================================================================
// In the new V3 architecture, YOU define your render commands.
// This sample defines the commands it needs: camera, mesh, clear, and lines with custom effect.

/// User-defined render command for this sample
type SampleCmd =
  | SetCamera of camera: Mibo.Elmish.Camera
  | DrawMesh of model: Model * transform: Matrix
  | DrawLinesEffect of
    vertices: VertexPositionColor[] *
    lineCount: int *
    effect: Effect *
    setup: EffectSetup
  | Clear of color: Color * clearDepth: bool

// ============================================================================
// Sample Command Processor
// ============================================================================
// This function processes the render commands and does the actual rendering.
// It's where you integrate with MonoGame's rendering APIs.

module SampleCommandProcessor =

  let private drawMeshWithBasicEffect
    (gd: GraphicsDevice)
    (view: Matrix)
    (projection: Matrix)
    (model: Model)
    (transform: Matrix)
    =
    for mesh in model.Meshes do
      for part in mesh.MeshParts do
        match part.Effect with
        | :? BasicEffect as be ->
          be.World <- transform
          be.View <- view
          be.Projection <- projection
          be.EnableDefaultLighting() |> ignore
        | _ -> ()

      mesh.Draw()

  /// Process all commands in the render buffer.
  /// Returns the final camera state for the next frame.
  let processCommands
    (cameraState: CameraState3D)
    (gd: GraphicsDevice)
    (buffer: RenderBuffer3D<SampleCmd>)
    : CameraState3D =
    let mutable currentCamera = cameraState

    for i = 0 to buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]

      match cmd with
      | SetCamera cam ->
        currentCamera <- {
          View = cam.View
          Projection = cam.Projection
        }

      | DrawMesh(model, transform) ->
        drawMeshWithBasicEffect
          gd
          currentCamera.View
          currentCamera.Projection
          model
          transform

      | DrawLinesEffect(vertices, lineCount, effect, setup) ->
        let effectCtx = {
          World = Matrix.Identity
          View = currentCamera.View
          Projection = currentCamera.Projection
        }

        setup effect effectCtx

        for pass in effect.CurrentTechnique.Passes do
          pass.Apply()
          gd.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, lineCount)

      | Clear(color, clearDepth) ->
        if clearDepth then
          gd.Clear(
            ClearOptions.Target ||| ClearOptions.DepthBuffer,
            color,
            1.0f,
            0
          )
        else
          gd.Clear(ClearOptions.Target, color, 1.0f, 0)

    currentCamera
