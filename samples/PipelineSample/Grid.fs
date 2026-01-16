module PipelineSample.Grid

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open PipelineSample

/// <summary>
/// Pre-calculates grid vertices for a set of platform bounds.
/// </summary>
let create (platforms: PlatformData list) (padding: float32) (color: Color) =
  let vertices = ResizeArray<VertexPositionColor>()

  for plat in platforms do
    let min = plat.Bounds.Min
    let max = plat.Bounds.Max
    let y = min.Y - 0.01f

    let startX = floor(min.X - padding)
    let endX = ceil(max.X + padding)
    let startZ = floor(min.Z - padding)
    let endZ = ceil(max.Z + padding)

    // X lines
    let mutable x = startX

    while x <= endX + 0.001f do
      vertices.Add(VertexPositionColor(Vector3(x, y, startZ), color))
      vertices.Add(VertexPositionColor(Vector3(x, y, endZ), color))
      x <- x + 1.0f

    // Z lines
    let mutable z = startZ

    while z <= endZ + 0.001f do
      vertices.Add(VertexPositionColor(Vector3(startX, y, z), color))
      vertices.Add(VertexPositionColor(Vector3(endX, y, z), color))
      z <- z + 1.0f

  let result = vertices.ToArray()
  result, result.Length / 2

/// <summary>
/// Draws the grid using a custom shader effect.
/// </summary>
let draw
  (playerPos: Vector3)
  (maxDist: float32)
  (effect: Effect)
  (vertices: VertexPositionColor[])
  (lineCount: int)
  (buffer: RenderBuffer<unit, RenderCommand>)
  =

  if lineCount > 0 then
    // Use DrawCustom for immediate mode drawing within the pipeline
    // The pipeline handles flushing before calling this
    let drawFn (device: GraphicsDevice) (camera: Camera) =
        // Setup effect parameters
        effect.Parameters.["World"].SetValue(Matrix.Identity)
        effect.Parameters.["View"].SetValue(camera.View)
        effect.Parameters.["Projection"].SetValue(camera.Projection)
        effect.Parameters.["PlayerPosition"].SetValue(playerPos)
        effect.Parameters.["MaxDistance"].SetValue(maxDist)

        device.BlendState <- BlendState.AlphaBlend
        device.DepthStencilState <- DepthStencilState.DepthRead

        for pass in effect.CurrentTechnique.Passes do
            pass.Apply()
            device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, lineCount)
        
        // Restore defaults if needed (Pipeline usually handles this but good practice)
        device.BlendState <- BlendState.Opaque
        device.DepthStencilState <- DepthStencilState.Default

    // Add command to buffer
    buffer.Add((), DrawCustom drawFn)
