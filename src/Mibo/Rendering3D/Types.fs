namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Core Types for the Rendering Pipeline
// ============================================================================


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
  /// Standard camera type for 3D games with perspective distortion (objects appear smaller with distance).
  /// </summary>
  /// <param name="position">Camera position in world space.</param>
  /// <param name="target">Point the camera should look at.</param>
  /// <param name="up">Up direction vector (typically Vector3.Up).</param>
  /// <param name="fov">Field of view in radians (typically MathHelper.PiOver4 = 45 degrees).</param>
  /// <param name="aspect">Aspect ratio (width / height).</param>
  /// <param name="near">Near clipping plane distance.</param>
  /// <param name="far">Far clipping plane distance.</param>
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
  /// No perspective distortion - objects appear same size regardless of distance.
  /// Useful for UI, isometric games, or technical visualizations.
  /// </summary>
  /// <param name="position">Camera position in world space.</param>
  /// <param name="target">Point camera should look at.</param>
  /// <param name="up">Up direction vector (typically Vector3.Up).</param>
  /// <param name="width">View width in world units.</param>
  /// <param name="height">View height in world units.</param>
  /// <param name="near">Near clipping plane distance.</param>
  /// <param name="far">Far clipping plane distance.</param>
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
  /// Located at origin, looking forward, with no perspective.
  /// Useful for unit tests and debugging when you don't want to set up a real camera.
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
  /// <summary>
  /// Compute bounding box from a ModelMesh by scanning vertex data.
  /// </summary>
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

  /// <summary>
  /// Create a Mesh from a ModelMesh.
  /// Returns a sequence of Mesh objects (one per MeshPart in the ModelMesh).
  /// Each Mesh is shareable and can be drawn with different transforms and materials.
  /// </summary>
  /// <param name="modelMesh">The ModelMesh to extract meshes from.</param>
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

  /// <summary>
  /// Create all meshes from a Model.
  /// Returns a flattened sequence of all Mesh objects across all ModelMeshes.
  /// Convenient for loading entire models at once.
  /// </summary>
  /// <param name="model">The Model to extract all meshes from.</param>
  let fromModel(model: Model) : Mesh seq =
    model.Meshes |> Seq.collect fromModelMesh

  /// <summary>
  /// Create a Mesh with explicit bounds.
  /// Use this when building procedural geometry or when you have pre-computed bounding volumes.
  /// </summary>
  /// <param name="vb">The vertex buffer containing vertex data.</param>
  /// <param name="ib">The index buffer defining triangles.</param>
  /// <param name="indexCount">Number of indices (triangles * 3).</param>
  /// <param name="bounds">Pre-computed bounding box for culling.</param>
  /// <param name="effect">The effect to use when rendering this mesh.</param>
  let create
    (vb: VertexBuffer)
    (ib: IndexBuffer)
    (indexCount: int)
    (bounds: BoundingBox)
    (effect: Effect)
    : Mesh =
    {
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
  /// <summary>
  /// No special rendering behavior (default).
  /// </summary>
  | None = 0
  /// <summary>
  /// This material casts shadows when shadow mapping is enabled.
  /// Opaque objects should typically have this flag.
  /// </summary>
  | CastsShadow =1
  /// <summary>
  /// This material receives shadows from other objects.
  /// Most materials should have this flag enabled for proper lighting.
  /// </summary>
  | ReceivesShadow = 2
  /// <summary>
  /// Material uses alpha blending. Rendered after opaque geometry with depth read-only.
  /// Use for glass, water, foliage, or any semi-transparent object.
  /// </summary>
  | Transparent = 4
  /// <summary>
  /// Material renders both sides of faces (front and back).
  /// Disables backface culling. Useful for thin geometry (leaves, paper, cloth).
  /// </summary>
  | DoubleSided = 8
  /// <summary>
  /// Material ignores all lighting and renders at full brightness.
  /// Useful for UI elements, debug visualization, or self-illuminated objects.
  /// </summary>
  | Unlit = 16
  /// <summary>
  /// Material uses alpha test (discarding pixels below threshold) rather than alpha blending.
  /// Faster than transparency but doesn't support soft edges. Use for foliage, fences, chain-link.
  /// </summary>
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
  /// <summary>
  /// Default PBR material properties with reasonable defaults.
  /// White albedo, no textures, non-metallic, medium roughness.
  /// </summary>
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

  /// <summary>
  /// Default opaque material. Casts and receives shadows, standard render queue.
  /// Good starting point for most solid objects (rocks, walls, floors).
  /// </summary>
  let defaultOpaque: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.CastsShadow ||| MaterialFlags.ReceivesShadow
    AlphaThreshold = 0.5f
    RenderQueue = 2000
  }

  /// <summary>
  /// Unlit material that ignores all lighting.
  /// Useful for UI elements, debug visualization, or emissive objects.
  /// </summary>
  let unlit: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.Unlit
    AlphaThreshold = 0.5f
    RenderQueue = 2000
  }

  /// <summary>
  /// Transparent material with alpha blending enabled.
  /// Receives shadows but does not cast them.
  /// Use for glass, water, or semi-transparent surfaces.
  /// </summary>
  let transparent: Material = {
    PBR = defaultPBR
    Flags = MaterialFlags.Transparent ||| MaterialFlags.ReceivesShadow
    AlphaThreshold = 0.5f
    RenderQueue = 3000
  }

  // Builders

  /// <summary>
  /// Sets the base surface color (diffuse reflection).
  /// In PBR, this represents the raw color of the material free of any lighting information.
  /// </summary>
  let withAlbedo (color: Color) (mat: Material) = {
    mat with
        PBR = { mat.PBR with AlbedoColor = color }
  }

  /// <summary>
  /// Applies a texture to control the base surface color.
  /// Use this for complex surfaces with patterns, text, or variations.
  /// </summary>
  let withAlbedoMap (tex: Texture2D) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              AlbedoMap = ValueSome tex
        }
  }

  /// <summary>
  /// Applies a normal map to simulate fine surface details.
  /// Adds perception of bumps, scratches, and grooves without increasing polygon count.
  /// </summary>
  let withNormalMap (tex: Texture2D) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              NormalMap = ValueSome tex
        }
  }

  /// <summary>
  /// Controls the metallicity of the surface.
  /// 0.0: Dielectric (plastic, wood, stone).
  /// 1.0: Metal (Gold, Silver).
  /// Values between 0 and 1 are rare physically but useful for transitions (e.g., rusty metal).
  /// </summary>
  let withMetallic (value: float32) (mat: Material) = {
    mat with
        PBR = { mat.PBR with Metallic = value }
  }

  /// <summary>
  /// Controls the microscopic roughness of the surface.
  /// 0.0: Smooth (Mirror-like reflections).
  /// 1.0: Rough (Matte/Chalky appearance).
  /// </summary>
  let withRoughness (value: float32) (mat: Material) = {
    mat with
        PBR = { mat.PBR with Roughness = value }
  }

  /// <summary>
  /// Makes the object appear to emit light.
  /// Useful for screens, fire, or magic effects. Note: Does not cast actual light on other objects unless using GI.
  /// </summary>
  let withEmissive (color: Color) (intensity: float32) (mat: Material) = {
    mat with
        PBR = {
          mat.PBR with
              EmissiveColor = color
              EmissiveIntensity = intensity
        }
  }

  /// <summary>
  /// Configures special rendering behaviors.
  /// Use 'Transparent' for glass/liquids or 'DoubleSided' for thin geometry (leaves, paper).
  /// </summary>
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
  EffectOverride: Effect voption
}

module Drawable =
  /// <summary>
  /// Create a drawable with pre-computed world-space bounding sphere.
  /// Faster than the `draw {}` builder when you already have a computed transform.
  /// </summary>
  /// <param name="mesh">The mesh geometry to render.</param>
  /// <param name="transform">World transform matrix (includes position, rotation, scale).</param>
  /// <param name="material">Material properties for this instance.</param>
  let create (mesh: Mesh) (transform: Matrix) (material: Material) : Drawable = {
    Mesh = mesh
    Transform = transform
    Material = material
    BoundingSphere = mesh.BoundingSphere.Transform(transform)
    EffectOverride = ValueNone
  }

// ============================================================================
// Render Commands - The pipeline processes these sequentially
// ============================================================================

/// Render commands that the pipeline processes in order
type RenderCommand =
  /// Set camera for subsequent draws
  | SetCamera of camera: Camera
  /// Set lighting for subsequent draws (overrides config default)
  | SetLighting of lighting: LightingState
  /// Sets the rendering viewport
  | SetViewport of Viewport
  /// Standard clear target command
  | ClearTarget of color: Color voption * clearDepth: bool
  /// Draw a single drawable
  | Draw of drawable: Drawable
  /// Custom draw escape hatch
  | DrawCustom of draw: (GraphicsDevice -> Camera -> unit)
