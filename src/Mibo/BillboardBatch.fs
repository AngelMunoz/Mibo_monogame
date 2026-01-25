namespace Mibo.Rendering

open System
open System.Buffers
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

/// <summary>
/// A batcher for drawing camera-facing billboards (particles, sprites in 3D space).
/// </summary>
/// <remarks>
/// Billboards always face the camera using camera right/up vectors.
/// </remarks>
module BillboardBatch =

  type State = {
    mutable Vertices: VertexPositionColorTexture[]
    mutable Indices: int16[]
    mutable VertexBuffer: DynamicVertexBuffer
    mutable IndexBuffer: IndexBuffer
    mutable SpriteCount: int
    GraphicsDevice: GraphicsDevice
  }

  let private ensureBuffers(state: State) =
    if isNull state.VertexBuffer then
      state.VertexBuffer <-
        new DynamicVertexBuffer(
          state.GraphicsDevice,
          typeof<VertexPositionColorTexture>,
          state.Vertices.Length,
          BufferUsage.WriteOnly
        )

      // Pre-calculate indices
      for i = 0 to state.Vertices.Length / 4 - 1 do
        state.Indices[i * 6 + 0] <- int16(i * 4 + 0)
        state.Indices[i * 6 + 1] <- int16(i * 4 + 1)
        state.Indices[i * 6 + 2] <- int16(i * 4 + 2)
        state.Indices[i * 6 + 3] <- int16(i * 4 + 0)
        state.Indices[i * 6 + 4] <- int16(i * 4 + 2)
        state.Indices[i * 6 + 5] <- int16(i * 4 + 3)

      state.IndexBuffer <-
        new IndexBuffer(
          state.GraphicsDevice,
          typeof<int16>,
          state.Indices.Length,
          BufferUsage.WriteOnly
        )

      state.IndexBuffer.SetData(state.Indices)

  let private ensureCapacity (numSprites: int) (state: State) =
    let requiredVerts = (state.SpriteCount + numSprites) * 4

    if requiredVerts > state.Vertices.Length then
      let newSize = Math.Max(state.Vertices.Length * 2, requiredVerts)
      let newVerts = ArrayPool.Shared.Rent(newSize)
      state.Vertices.AsSpan().CopyTo(newVerts.AsSpan())
      ArrayPool.Shared.Return(state.Vertices)
      state.Vertices <- newVerts

      let newIndicesSize = (newSize / 4) * 6
      let newIndices = ArrayPool.Shared.Rent(newIndicesSize)
      state.Indices.AsSpan().CopyTo(newIndices.AsSpan())
      ArrayPool.Shared.Return state.Indices
      state.Indices <- newIndices

      // Re-fill indices
      for i = 0 to state.Vertices.Length / 4 - 1 do
        state.Indices[i * 6 + 0] <- int16(i * 4 + 0)
        state.Indices[i * 6 + 1] <- int16(i * 4 + 1)
        state.Indices[i * 6 + 2] <- int16(i * 4 + 2)
        state.Indices[i * 6 + 3] <- int16(i * 4 + 0)
        state.Indices[i * 6 + 4] <- int16(i * 4 + 2)
        state.Indices[i * 6 + 5] <- int16(i * 4 + 3)

      state.VertexBuffer <-
        new DynamicVertexBuffer(
          state.GraphicsDevice,
          typeof<VertexPositionColorTexture>,
          state.Vertices.Length,
          BufferUsage.WriteOnly
        )

      state.IndexBuffer <-
        new IndexBuffer(
          state.GraphicsDevice,
          typeof<int16>,
          state.Indices.Length,
          BufferUsage.WriteOnly
        )

      state.IndexBuffer.SetData(state.Indices)

  // 512 billboards × 4 vertices = 2048, 512 billboards × 6 indices = 3072
  [<Literal>]
  let private DefaultVertexCapacity = 2048

  [<Literal>]
  let private DefaultIndexCapacity = 3072

  /// <summary>Creates a new billboard batcher.</summary>
  let create(graphicsDevice: GraphicsDevice) = {
    Vertices = ArrayPool.Shared.Rent DefaultVertexCapacity
    Indices = ArrayPool.Shared.Rent DefaultIndexCapacity
    VertexBuffer = null
    IndexBuffer = null
    SpriteCount = 0
    GraphicsDevice = graphicsDevice
  }

  /// <summary>Return pooled arrays. Call when the batch is no longer needed.</summary>
  let dispose(state: State) =
    if not(isNull state.Vertices) then
      ArrayPool.Shared.Return state.Vertices
      state.Vertices <- null

    if not(isNull state.Indices) then
      ArrayPool.Shared.Return state.Indices
      state.Indices <- null

  /// <summary>Begin a batch.</summary>
  let inline begin' (state: State) =
    state.SpriteCount <- 0

  /// <summary>Adds a billboard to the batch with custom UVs (texture atlas).</summary>
  let drawUv
    (position: Vector3)
    (size: Vector2)
    (rotation: float32)
    (color: Color)
    (uv: UvRect)
    (camRight: Vector3)
    (camUp: Vector3)
    (state: State)
    =
    ensureCapacity 1 state

    let halfSize = size * 0.5f

    // Apply rotation
    let cos = MathF.Cos rotation
    let sin = MathF.Sin rotation

    // Rotated basis vectors
    let rotRight = camRight * cos + camUp * sin
    let rotUp = camUp * cos - camRight * sin

    let w = rotRight * halfSize.X
    let h = rotUp * halfSize.Y

    // Quad vertices relative to center position
    let v0 = position - w + h // TopLeft
    let v1 = position + w + h // TopRight
    let v2 = position + w - h // BottomRight
    let v3 = position - w - h // BottomLeft

    let idx = state.SpriteCount * 4

    let u0, v0', u1, v1' = uv.U0, uv.V0, uv.U1, uv.V1

    state.Vertices[idx + 0] <-
      VertexPositionColorTexture(v0, color, Vector2(u0, v0'))

    state.Vertices[idx + 1] <-
      VertexPositionColorTexture(v1, color, Vector2(u1, v0'))

    state.Vertices[idx + 2] <-
      VertexPositionColorTexture(v2, color, Vector2(u1, v1'))

    state.Vertices[idx + 3] <-
      VertexPositionColorTexture(v3, color, Vector2(u0, v1'))

    state.SpriteCount <- state.SpriteCount + 1

  /// <summary>Adds a billboard to the batch.</summary>
  let draw
    (position: Vector3)
    (size: Vector2)
    (rotation: float32)
    (color: Color)
    (camRight: Vector3)
    (camUp: Vector3)
    (state: State)
    =
    drawUv
      position
      size
      rotation
      color
      UvRect.full
      camRight
      camUp
      state

  /// <summary>Adds a screen-aligned 2D billboard to the batch.</summary>
  let draw2D
    (position: Vector2)
    (size: Vector2)
    (rotation: float32)
    (color: Color)
    (uv: UvRect)
    (state: State)
    =
    drawUv
      (Vector3(position, 0f))
      size
      rotation
      color
      uv
      Vector3.UnitX
      (Vector3(0f, -1f, 0f))
      state

  /// <summary>Adds a simple screen-aligned 2D billboard to the batch.</summary>
  let draw2DSimple
    (position: Vector2)
    (size: Vector2)
    (color: Color)
    (state: State)
    =
    draw2D position size 0f color UvRect.full state

  /// <summary>Flushes the current batch to the GPU.</summary>
  let flush (effect: Effect) (state: State) =
    if state.SpriteCount > 0 then
      ensureBuffers state
      state.VertexBuffer.SetData(state.Vertices, 0, state.SpriteCount * 4)
      state.GraphicsDevice.SetVertexBuffer state.VertexBuffer
      state.GraphicsDevice.Indices <- state.IndexBuffer

      for pass in effect.CurrentTechnique.Passes do
        pass.Apply()

        state.GraphicsDevice.DrawIndexedPrimitives(
          PrimitiveType.TriangleList,
          0,
          0,
          state.SpriteCount * 2
        )

  /// <summary>Ends the batch and flushes all draw commands to the GPU.</summary>
  let inline end' (effect: Effect) (state: State) = flush effect state
