namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Core Types for the Rendering Pipeline
// ============================================================================

/// <summary>Coarse rendering pass selection for 3D.</summary>
type RenderPass =
  | Opaque
  | Transparent

/// <summary>Standard transformation matrices used during effect setup.</summary>
[<Struct>]
type EffectContext = {
  World: Matrix
  View: Matrix
  Projection: Matrix
}

/// <summary>Callback for configuring an effect before a draw operation.</summary>
type EffectSetup = Effect -> EffectContext -> unit

/// <summary>UV rectangle in normalized texture coordinates.</summary>
[<Struct>]
type UvRect = {
  U0: float32
  V0: float32
  U1: float32
  V1: float32
}

module UvRect =
  let full: UvRect = {
    U0 = 0.0f
    V0 = 0.0f
    U1 = 1.0f
    V1 = 1.0f
  }

/// Shader base types for override mapping
type ShaderBase =
  /// The default shadow casting shader (depth only).
  | ShadowCaster
  /// High-fidelity PBR shader (Albedo + Normal + MRA).
  | PBRForward
  /// Unlit shader (full brightness, no shadows).
  | Unlit
  /// Bloom extraction shader.
  | Bloom
  /// Final post-processing and tone mapping shader.
  | PostProcess

/// Camera for 3D rendering
[<Struct>]
type Camera = {
  View: Matrix
  Projection: Matrix
  Position: Vector3
  Forward: Vector3
  Near: float32
  Far: float32
}

module Camera =
  /// <summary>
  /// Create a perspective camera.
  /// </summary>
  let perspective
    (position: Vector3)
    (target: Vector3)
    (up: Vector3)
    (fov: float32)
    (aspect: float32)
    (near: float32)
    (far: float32)
    : Camera =
    let forward = Vector3.Normalize(target - position)

    {
      View = Matrix.CreateLookAt(position, target, up)
      Projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, near, far)
      Position = position
      Forward = forward
      Near = near
      Far = far
    }

  /// <summary>
  /// Create an orthographic camera.
  /// </summary>
  let orthographic
    (position: Vector3)
    (target: Vector3)
    (up: Vector3)
    (width: float32)
    (height: float32)
    (near: float32)
    (far: float32)
    : Camera =
    let forward = Vector3.Normalize(target - position)

    {
      View = Matrix.CreateLookAt(position, target, up)
      Projection = Matrix.CreateOrthographic(width, height, near, far)
      Position = position
      Forward = forward
      Near = near
      Far = far
    }

  /// <summary>
  /// Identity camera (for testing).
  /// </summary>
  let identity: Camera = {
    View = Matrix.Identity
    Projection = Matrix.Identity
    Position = Vector3.Zero
    Forward = Vector3.Forward
    Near = 0.1f
    Far = 1000f
  }

/// Mesh geometry reference
[<Struct>]
type Mesh = {
  VertexBuffer: VertexBuffer
  IndexBuffer: IndexBuffer
  IndexCount: int
  BoundingBox: BoundingBox
  BoundingSphere: BoundingSphere
  Effect: Effect
}

module Mesh =
  let private computeBox(modelMesh: ModelMesh) : BoundingBox =
    let mutable min = Vector3(infinityf, infinityf, infinityf)
    let mutable max = Vector3(-infinityf, -infinityf, -infinityf)

    for part in modelMesh.MeshParts do
      let vertexSize = part.VertexBuffer.VertexDeclaration.VertexStride / 4
      let data = Array.zeroCreate<float32>(part.NumVertices * vertexSize)
      part.VertexBuffer.GetData(data)

      for i in 0 .. part.NumVertices - 1 do
        let idx = i * vertexSize
        let pos = Vector3(data.[idx], data.[idx + 1], data.[idx + 2])
        min <- Vector3.Min(min, pos)
        max <- Vector3.Max(max, pos)

    BoundingBox(min, max)

  let fromModelMesh(modelMesh: ModelMesh) : Mesh seq =
    let box = computeBox modelMesh
    let sphere = modelMesh.BoundingSphere

    modelMesh.MeshParts
    |> Seq.map(fun part -> {
      VertexBuffer = part.VertexBuffer
      IndexBuffer = part.IndexBuffer
      IndexCount = part.PrimitiveCount * 3
      BoundingBox = box
      BoundingSphere = sphere
      Effect = part.Effect
    })

  let fromModel(model: Model) : Mesh seq =
    model.Meshes |> Seq.collect fromModelMesh

  let create (vb: VertexBuffer) (ib: IndexBuffer) (indexCount: int) (bounds: BoundingBox) (effect: Effect) : Mesh = {
    VertexBuffer = vb
    IndexBuffer = ib
    IndexCount = indexCount
    BoundingBox = bounds
    BoundingSphere = BoundingSphere.CreateFromBoundingBox(bounds)
    Effect = effect
  }

// ============================================================================
// Material System
// ============================================================================

/// Material rendering flags
[<Flags>]
type MaterialFlags =
  | None = 0
  | CastsShadow = 1
  | ReceivesShadow = 2
  | Transparent = 4
  | DoubleSided = 8
  | Unlit = 16
  | AlphaTest = 32

/// PBR material properties
[<Struct>]
type PBRMaterial = {
  AlbedoColor: Color
  AlbedoMap: Texture2D voption
  NormalMap: Texture2D voption
  MetallicRoughnessMap: Texture2D voption
  Metallic: float32
  Roughness: float32
  AmbientOcclusionMap: Texture2D voption
  EmissiveColor: Color
  EmissiveIntensity: float32
}

/// Complete material definition
[<Struct>]
type Material = {
  PBR: PBRMaterial
  Flags: MaterialFlags
  AlphaThreshold: float32
  RenderQueue: int
}

module Material =
  let defaultPBR: PBRMaterial = {
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

  let defaultOpaque: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.CastsShadow ||| MaterialFlags.ReceivesShadow
    AlphaThreshold = 0.5f
    RenderQueue = 2000
  }

  let unlit: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.Unlit
    AlphaThreshold = 0.5f
    RenderQueue = 2000
  }

  let transparent: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.Transparent ||| MaterialFlags.ReceivesShadow
    AlphaThreshold = 0.5f
    RenderQueue = 3000
  }

  let withAlbedo (color: Color) (mat: Material) = { mat with PBR = { mat.PBR with AlbedoColor = color } }
  let withAlbedoMap (tex: Texture2D) (mat: Material) = { mat with PBR = { mat.PBR with AlbedoMap = ValueSome tex } }
  let withNormalMap (tex: Texture2D) (mat: Material) = { mat with PBR = { mat.PBR with NormalMap = ValueSome tex } }
  let withMetallic (value: float32) (mat: Material) = { mat with PBR = { mat.PBR with Metallic = value } }
  let withRoughness (value: float32) (mat: Material) = { mat with PBR = { mat.PBR with Roughness = value } }
  let withEmissive (color: Color) (intensity: float32) (mat: Material) = { mat with PBR = { mat.PBR with EmissiveColor = color; EmissiveIntensity = intensity } }
  let withFlags (flags: MaterialFlags) (mat: Material) = { mat with Flags = flags }

// ============================================================================
// Drawable - The unit of rendering
// ============================================================================

/// A single drawable object ready for the pipeline
[<Struct>]
type Drawable = {
  Mesh: Mesh
  Transform: Matrix
  Material: Material
  BoundingSphere: BoundingSphere
  EffectOverride: Effect voption
  Pass: RenderPass
  Bones: Matrix[] voption
}

module Drawable =
  let create (mesh: Mesh) (transform: Matrix) (material: Material) : Drawable = {
    Mesh = mesh
    Transform = transform
    Material = material
    BoundingSphere = mesh.BoundingSphere.Transform(transform)
    EffectOverride = ValueNone
    Pass = if material.Flags.HasFlag(MaterialFlags.Transparent) then Transparent else Opaque
    Bones = ValueNone
  }

// --- Sprite3D / Billboard Types ---

/// <summary>Billboard facing mode.</summary>
[<Struct>]
type BillboardMode =
  | Spherical
  | Cylindrical of upAxis: Vector3

/// <summary>A textured quad in 3D space, represented as center + basis half-extents.</summary>
[<Struct>]
type Quad3D = {
  Center: Vector3
  Right: Vector3
  Up: Vector3
  Color: Color
  Uv: UvRect
}

/// <summary>A billboard (camera-facing quad) in 3D space.</summary>
[<Struct>]
type Billboard3D = {
  Position: Vector3
  Size: Vector2
  Rotation: float32
  Color: Color
  Uv: UvRect
  Mode: BillboardMode
}

/// <summary>Sprite-style quad draw.</summary>
[<Struct>]
type SpriteQuadCmd = {
  Pass: RenderPass
  Texture: Texture2D
  Quad: Quad3D
}

/// <summary>Sprite-style billboard draw.</summary>
[<Struct>]
type SpriteBillboardCmd = {
  Pass: RenderPass
  Texture: Texture2D
  Billboard: Billboard3D
}

/// <summary>Effect-driven quad draw.</summary>
[<Struct>]
type EffectQuadCmd = {
  Pass: RenderPass
  Effect: Effect
  Setup: EffectSetup voption
  Quad: Quad3D
}

/// <summary>Effect-driven billboard draw.</summary>
[<Struct>]
type EffectBillboardCmd = {
  Pass: RenderPass
  Effect: Effect
  Setup: EffectSetup voption
  Billboard: Billboard3D
}

// ============================================================================
// Render Commands - The pipeline processes these sequentially
// ============================================================================

/// Render commands that the pipeline processes in order
type RenderCommand =
  | SetCamera of camera: Camera
  | SetLighting of lighting: LightingState
  | SetViewport of Viewport
  | ClearTarget of color: Color voption * clearDepth: bool
  | Draw of drawable: Drawable
  | DrawSpriteQuad of spriteQuad: SpriteQuadCmd
  | DrawSpriteBillboard of spriteBillboard: SpriteBillboardCmd
  | DrawQuadEffect of quadEffect: EffectQuadCmd
  | DrawBillboardEffect of billboardEffect: EffectBillboardCmd
  | DrawLine of p1: Vector3 * p2: Vector3 * color: Color * pass: RenderPass
  | DrawLines of vertices: VertexPositionColor[] * lineCount: int * pass: RenderPass
  | DrawLinesEffect of
    vertices: VertexPositionColor[] *
    lineCount: int *
    effect: Effect *
    setup: EffectSetup voption *
    pass: RenderPass
  | DrawCustom of draw: (GraphicsDevice -> Camera -> unit)