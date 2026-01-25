namespace Mibo.Rendering

open System
open System.Buffers
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

/// <summary>
/// A simple batcher for drawing line primitives in 2D and 3D.
/// </summary>
/// <remarks>
/// Lines use <see cref="T:Microsoft.Xna.Framework.Graphics.VertexPositionColor"/> and are drawn
/// with <see cref="F:Microsoft.Xna.Framework.Graphics.PrimitiveType.LineList"/>.
/// </remarks>
module LineBatch =

  type State = {
    mutable Vertices: VertexPositionColor[]
    mutable LineCount: int
    GraphicsDevice: GraphicsDevice
  }

  let private ensureCapacity (numLines: int) (state: State) =
    let requiredVerts = (state.LineCount + numLines) * 2

    if requiredVerts > state.Vertices.Length then
      let newSize = max (state.Vertices.Length * 2) requiredVerts
      let newVerts = ArrayPool.Shared.Rent(newSize)
      state.Vertices.AsSpan().CopyTo(newVerts.AsSpan())
      ArrayPool.Shared.Return(state.Vertices)
      state.Vertices <- newVerts

  // 512 lines × 2 vertices = 1024
  [<Literal>]
  let private DefaultVertexCapacity = 1024

  /// <summary>Creates a new line batcher.</summary>
  let create(graphicsDevice: GraphicsDevice) = {
    Vertices = ArrayPool.Shared.Rent DefaultVertexCapacity
    LineCount = 0
    GraphicsDevice = graphicsDevice
  }

  /// <summary>Return pooled arrays. Call when the batch is no longer needed.</summary>
  let dispose(state: State) =
    if not(isNull state.Vertices) then
      ArrayPool.Shared.Return state.Vertices
      state.Vertices <- null

  /// <summary>Begin a batch.</summary>
  let inline begin'(state: State) = state.LineCount <- 0

  /// <summary>Adds a single line segment to the batch.</summary>
  let addLine (p1: Vector3) (p2: Vector3) (color: Color) (state: State) =
    ensureCapacity 1 state

    let idx = state.LineCount * 2
    state.Vertices[idx + 0] <- VertexPositionColor(p1, color)
    state.Vertices[idx + 1] <- VertexPositionColor(p2, color)

    state.LineCount <- state.LineCount + 1

  /// <summary>Adds a 2D line segment to the batch.</summary>
  let addLine2D (p1: Vector2) (p2: Vector2) (color: Color) (state: State) =
    addLine (Vector3(p1, 0f)) (Vector3(p2, 0f)) color state

  /// <summary>Adds a 2D rectangle outline to the batch.</summary>
  let addRect2D (rect: Rectangle) (color: Color) (state: State) =
    let p1 = Vector2(float32 rect.Left, float32 rect.Top)
    let p2 = Vector2(float32 rect.Right, float32 rect.Top)
    let p3 = Vector2(float32 rect.Right, float32 rect.Bottom)
    let p4 = Vector2(float32 rect.Left, float32 rect.Bottom)

    addLine2D p1 p2 color state
    addLine2D p2 p3 color state
    addLine2D p3 p4 color state
    addLine2D p4 p1 color state

  /// <summary>Adds a 2D circle outline to the batch.</summary>
  let addCircle2D
    (center: Vector2)
    (radius: float32)
    (segments: int)
    (color: Color)
    (state: State)
    =
    let segments = max 3 segments
    let step = (MathF.PI * 2.0f) / float32 segments

    for i = 0 to segments - 1 do
      let a1 = float32 i * step
      let a2 = float32(i + 1) * step
      let p1 = center + Vector2(MathF.Cos a1, MathF.Sin a1) * radius
      let p2 = center + Vector2(MathF.Cos a2, MathF.Sin a2) * radius
      addLine2D p1 p2 color state

  /// <summary>Adds multiple line segments from a pre-built vertex array.</summary>
  /// <param name="vertices">Array of vertices (2 per line segment).</param>
  /// <param name="lineCount">Number of line segments to add.</param>
  let addLines
    (vertices: VertexPositionColor[])
    (lineCount: int)
    (state: State)
    =
    ensureCapacity lineCount state

    let idx = state.LineCount * 2
    let vertsToCopy = lineCount * 2
    vertices.AsSpan(0, vertsToCopy).CopyTo(state.Vertices.AsSpan(idx))

    state.LineCount <- state.LineCount + lineCount

  /// <summary>Flushes the current batch to the GPU.</summary>
  /// <remarks>Effect passes are applied by the caller; this only issues the draw call.</remarks>
  let flush (effect: Effect) (state: State) =
    if state.LineCount > 0 then
      let gd = state.GraphicsDevice

      for pass in effect.CurrentTechnique.Passes do
        pass.Apply()

        gd.DrawUserPrimitives(
          PrimitiveType.LineList,
          state.Vertices,
          0,
          state.LineCount
        )

  /// <summary>Ends the batch and flushes all draw commands to the GPU.</summary>
  let inline end' (effect: Effect) (state: State) = flush effect state
