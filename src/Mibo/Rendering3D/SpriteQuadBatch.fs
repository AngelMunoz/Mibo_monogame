namespace Mibo.Rendering.Graphics3D

open System
open System.Buffers
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering

/// <summary>
/// A simple batcher for drawing textured, vertex-colored quads in 3D.
/// </summary>
module internal SpriteQuadBatch =

  type State = {
    mutable Vertices: VertexPositionColorTexture[]
    mutable Indices: int16[]
    mutable QuadCount: int
    GraphicsDevice: GraphicsDevice
  }

  let private refillIndices (indices: int16[]) (vertexCount: int) =
    let quadCapacity = vertexCount / 4

    for i in 0 .. quadCapacity - 1 do
      let vBase = i * 4
      let iBase = i * 6
      indices.[iBase + 0] <- int16(vBase + 0)
      indices.[iBase + 1] <- int16(vBase + 1)
      indices.[iBase + 2] <- int16(vBase + 2)
      indices.[iBase + 3] <- int16(vBase + 0)
      indices.[iBase + 4] <- int16(vBase + 2)
      indices.[iBase + 5] <- int16(vBase + 3)

  let private ensureCapacity (numQuads: int) (state: State) =
    let requiredVerts = (state.QuadCount + numQuads) * 4

    if requiredVerts > state.Vertices.Length then
      let newSize = max (state.Vertices.Length * 2) requiredVerts
      let newVerts = ArrayPool.Shared.Rent(newSize)
      state.Vertices.AsSpan().CopyTo(newVerts.AsSpan())
      ArrayPool.Shared.Return(state.Vertices)
      state.Vertices <- newVerts

      let newIndicesSize = (newSize / 4) * 6
      let newIndices = ArrayPool.Shared.Rent(newIndicesSize)
      state.Indices.AsSpan().CopyTo(newIndices.AsSpan())
      ArrayPool.Shared.Return(state.Indices)
      state.Indices <- newIndices

      refillIndices state.Indices state.Vertices.Length

  [<Literal>]
  let private DefaultVertexCapacity = 2048

  [<Literal>]
  let private DefaultIndexCapacity = 3072

  let create(graphicsDevice: GraphicsDevice) =
    let vertices = ArrayPool.Shared.Rent DefaultVertexCapacity
    let indices = ArrayPool.Shared.Rent DefaultIndexCapacity
    refillIndices indices vertices.Length

    {
      Vertices = vertices
      Indices = indices
      QuadCount = 0
      GraphicsDevice = graphicsDevice
    }

  let dispose(state: State) =
    if not(isNull state.Vertices) then
      ArrayPool.Shared.Return state.Vertices
      state.Vertices <- null

    if not(isNull state.Indices) then
      ArrayPool.Shared.Return state.Indices
      state.Indices <- null

  let inline begin'(state: State) = state.QuadCount <- 0

  let flush (effect: Effect) (state: State) =
    if state.QuadCount > 0 then
      let gd = state.GraphicsDevice

      for pass in effect.CurrentTechnique.Passes do
        pass.Apply()

        gd.DrawUserIndexedPrimitives(
          PrimitiveType.TriangleList,
          state.Vertices,
          0,
          state.QuadCount * 4,
          state.Indices,
          0,
          state.QuadCount * 2
        )

  let draw
    (center: Vector3)
    (right: Vector3)
    (up: Vector3)
    (color: Color)
    (uv: UvRect)
    (state: State)
    =
    ensureCapacity 1 state
    let idx = state.QuadCount * 4
    let tl = center - right + up
    let tr = center + right + up
    let br = center + right - up
    let bl = center - right - up
    let u0, v0, u1, v1 = uv.U0, uv.V0, uv.U1, uv.V1

    state.Vertices[idx + 0] <-
      VertexPositionColorTexture(tl, color, Vector2(u0, v0))

    state.Vertices[idx + 1] <-
      VertexPositionColorTexture(tr, color, Vector2(u1, v0))

    state.Vertices[idx + 2] <-
      VertexPositionColorTexture(br, color, Vector2(u1, v1))

    state.Vertices[idx + 3] <-
      VertexPositionColorTexture(bl, color, Vector2(u0, v1))

    state.QuadCount <- state.QuadCount + 1

  let inline end' (effect: Effect) (state: State) = flush effect state
