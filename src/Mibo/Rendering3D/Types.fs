namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Core Types for the Rendering Pipeline
// ============================================================================

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
  /// Create a perspective camera
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

  /// Create an orthographic camera
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

  /// Identity camera (for testing)
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
}

module Mesh =
  /// Create a Mesh from a ModelMesh (uses ModelMesh.BoundingSphere for correct culling)
  let fromModelMesh(modelMesh: ModelMesh) : Mesh seq =
    modelMesh.MeshParts
    |> Seq.map(fun part ->
      let bounds = modelMesh.BoundingSphere

      {
        VertexBuffer = part.VertexBuffer
        IndexBuffer = part.IndexBuffer
        IndexCount = part.PrimitiveCount * 3
        BoundingBox = BoundingBox.CreateFromSphere(bounds)
        BoundingSphere = bounds
      })

  /// Create all meshes from a Model
  let fromModel(model: Model) : Mesh seq =
    model.Meshes |> Seq.collect fromModelMesh

  /// Create a Mesh with explicit bounds
  let create
    (vb: VertexBuffer)
    (ib: IndexBuffer)
    (indexCount: int)
    (bounds: BoundingBox)
    : Mesh =
    {
      VertexBuffer = vb
      IndexBuffer = ib
      IndexCount = indexCount
      BoundingBox = bounds
      BoundingSphere = BoundingSphere.CreateFromBoundingBox(bounds)
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

  // Builders
  let withAlbedo (color: Color) (mat: Material) = {
    mat with
        PBR = { mat.PBR with AlbedoColor = color }
  }

  let withAlbedoMap (tex: Texture2D) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              AlbedoMap = ValueSome tex
        }
  }

  let withNormalMap (tex: Texture2D) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              NormalMap = ValueSome tex
        }
  }

  let withMetallic (value: float32) (mat: Material) = {
    mat with
        PBR = { mat.PBR with Metallic = value }
  }

  let withRoughness (value: float32) (mat: Material) = {
    mat with
        PBR = { mat.PBR with Roughness = value }
  }

  let withEmissive (color: Color) (intensity: float32) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              EmissiveColor = color
              EmissiveIntensity = intensity
        }
  }

  let withFlags (flags: MaterialFlags) (mat: Material) = {
    mat with
        Flags = flags
  }

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
}

module Drawable =
  /// Create a drawable with pre-computed world-space bounding sphere
  let create (mesh: Mesh) (transform: Matrix) (material: Material) : Drawable = {
    Mesh = mesh
    Transform = transform
    Material = material
    BoundingSphere = mesh.BoundingSphere.Transform(transform)
  }

// ============================================================================
// Render Commands - The pipeline processes these sequentially
// ============================================================================

/// Render commands that the pipeline processes in order
[<Struct>]
type RenderCommand =
  /// Set camera for subsequent draws
  | SetCamera of camera: Camera
  /// Set lighting for subsequent draws (overrides config default)
  | SetLighting of lighting: LightingState
  /// Set viewport region (for split-screen, minimap, etc.)
  | SetViewport of viewport: Viewport
  /// Clear the current render target
  | ClearTarget of clearColor: Color voption * clearDepth: bool
  /// Draw a single drawable
  | Draw of drawable: Drawable
  /// Custom draw escape hatch
  | DrawCustom of draw: (GraphicsDevice -> Camera -> unit)
