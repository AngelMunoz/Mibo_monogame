namespace Mibo.Rendering.Graphics3D

open System
open System.Buffers
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

/// <summary>
/// A simple batcher for drawing line primitives in 3D.
/// </summary>
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
module internal LineBatch =

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

  [<Literal>]
  let private DefaultVertexCapacity = 1024

  let create(graphicsDevice: GraphicsDevice) = {
    Vertices = ArrayPool.Shared.Rent DefaultVertexCapacity
    LineCount = 0
    GraphicsDevice = graphicsDevice
  }

  let dispose(state: State) =
    if not(isNull state.Vertices) then
      ArrayPool.Shared.Return state.Vertices
      state.Vertices <- null

  let inline begin'(state: State) = state.LineCount <- 0

  let addLine (p1: Vector3) (p2: Vector3) (color: Color) (state: State) =
    ensureCapacity 1 state
    let idx = state.LineCount * 2
    state.Vertices[idx + 0] <- VertexPositionColor(p1, color)
    state.Vertices[idx + 1] <- VertexPositionColor(p2, color)
    state.LineCount <- state.LineCount + 1

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

  let inline end' (effect: Effect) (state: State) = flush effect state
