module _3DSample.Rendering.Processor

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering
open Mibo.Rendering.Graphics3D
open _3DSample.Rendering.Commands
open _3DSample.Materials.Types
open _3DSample.Materials.UnlitBinding
open _3DSample.Materials.PBRBinding

// ============================================================================
// Command Processor
// ============================================================================
// Processes render commands and does the actual rendering.
// This is where you integrate with MonoGame's rendering APIs.
// Keep this module focused on dispatching - no game logic here.

/// <summary>Draws a model using MonoGame's BasicEffect.</summary>
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

/// <summary>Draws a model with a custom material, applying shader parameters first.</summary>
let private drawMeshWithMaterial
  (gd: GraphicsDevice)
  (mp: MaterialParams)
  (effect: Effect)
  (material: Material)
  (model: Model)
  (transform: Matrix)
  =
  match material with
  | Unlit mat -> _3DSample.Materials.UnlitBinding.apply effect mp mat
  | PBR mat -> _3DSample.Materials.PBRBinding.apply effect mp mat

  for mesh in Mesh.fromModel model do
    gd.SetVertexBuffer(mesh.VertexBuffer)
    gd.Indices <- mesh.IndexBuffer

    for pass in effect.CurrentTechnique.Passes do
      pass.Apply()
      gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.IndexCount / 3)

/// <summary>Process all commands in the render buffer.</summary>
/// <returns>The final camera state for the next frame.</returns>
let processCommands
  (cameraState: CameraState3D)
  (gd: GraphicsDevice)
  (buffer: RenderBuffer3D<SampleCmd>)
  : CameraState3D =
  let mutable currentCamera = cameraState

  // Use AsSpan() for bounds-check-elided iteration in the hot path
  for struct (_, cmd) in buffer.AsSpan() do
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

    | DrawMeshWithMaterial(effect, material, model, transform) ->
      let matParams = {
        World = transform
        View = currentCamera.View
        Projection = currentCamera.Projection
      }

      drawMeshWithMaterial gd matParams effect material model transform

    | DrawLinesEffect(vertices, lineCount, effect, setup) ->
      let effectCtx: Mibo.Rendering.EffectContext = {
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
