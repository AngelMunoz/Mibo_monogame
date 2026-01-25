namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering

// ============================================================================
// Core Types for the Rendering Pipeline
// ============================================================================

type PipelineBuffer<'Cmd> = Mibo.Elmish.RenderBuffer<unit, 'Cmd>

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
  Target: Vector3
  Up: Vector3
  Fov: float32
  Aspect: float32
  Near: float32
  Far: float32
} with

  member this.Forward = Vector3.Normalize(this.Target - this.Position)

module Camera =
  /// <summary>
  /// Recomputes the view matrix based on Position, Target, and Up.
  /// </summary>
  let rebuildView(c: Camera) =
    Matrix.CreateLookAt(c.Position, c.Target, c.Up)

  /// <summary>
  /// Recomputes the projection matrix based on Fov, Aspect, Near, and Far.
  /// </summary>
  let rebuildProjection(c: Camera) =
    Matrix.CreatePerspectiveFieldOfView(c.Fov, c.Aspect, c.Near, c.Far)

  /// <summary>
  /// Standard perspective camera with sensible defaults.
  /// </summary>
  let perspectiveDefaults: Camera =
    let c = {
      Position = Vector3(0f, 0f, 10f)
      Target = Vector3.Zero
      Up = Vector3.Up
      Fov = MathHelper.ToRadians 45f
      Aspect = 16f / 9f
      Near = 0.1f
      Far = 1000f
      View = Matrix.Identity
      Projection = Matrix.Identity
    }

    {
      c with
          View = rebuildView c
          Projection = rebuildProjection c
    }

  /// <summary>
  /// Create a perspective camera. (Backward compatibility)
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
    let c = {
      Position = position
      Target = target
      Up = up
      Fov = fov
      Aspect = aspect
      Near = near
      Far = far
      View = Matrix.Identity
      Projection = Matrix.Identity
    }

    {
      c with
          View = rebuildView c
          Projection = rebuildProjection c
    }

  /// <summary>
  /// Create an orthographic camera. (Backward compatibility)
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
    let c = {
      Position = position
      Target = target
      Up = up
      Fov = 0f // Not used for orthographic but keeping struct consistent
      Aspect = width / height
      Near = near
      Far = far
      View = Matrix.Identity
      Projection = Matrix.CreateOrthographic(width, height, near, far)
    }

    { c with View = rebuildView c }

  /// <summary>Sets the Field of View in radians.</summary>
  let inline withFov fov (c: Camera) =
    let next = { c with Fov = fov }

    {
      next with
          Projection = rebuildProjection next
    }

  /// <summary>Sets the aspect ratio (Width / Height).</summary>
  let inline withAspect aspect (c: Camera) =
    let next = { c with Aspect = aspect }

    {
      next with
          Projection = rebuildProjection next
    }

  /// <summary>Sets the near and far clipping planes.</summary>
  let inline withRange (near: float32) (far: float32) (c: Camera) =
    let next = { c with Near = near; Far = far }

    {
      next with
          Projection = rebuildProjection next
    }

  /// <summary>Sets the world-space position of the camera.</summary>
  let inline at pos (c: Camera) =
    let next = { c with Position = pos }
    { next with View = rebuildView next }

  /// <summary>Sets the target point the camera is looking at.</summary>
  let inline lookingAt target (c: Camera) =
    let next = { c with Target = target }
    { next with View = rebuildView next }

  /// <summary>Sets the world-space "up" vector (typically Vector3.Up).</summary>
  let inline withUp up (c: Camera) =
    let next = { c with Up = up }
    { next with View = rebuildView next }

  /// <summary>
  /// Positions the camera to look at a target from a specific position.
  /// </summary>
  let lookAt position target (c: Camera) =
    let next = {
      c with
          Position = position
          Target = target
    }

    { next with View = rebuildView next }

  /// <summary>
  /// Offsets the camera position along its current forward axis by a distance from the target.
  /// Useful for zooming or maintaining distance in an orbit.
  /// </summary>
  let withDistance distance (c: Camera) =
    let dir = Vector3.Normalize(c.Position - c.Target)

    let next = {
      c with
          Position = c.Target + dir * distance
    }

    { next with View = rebuildView next }

  /// <summary>
  /// Orbits the camera around its current target using spherical angles.
  /// </summary>
  /// <param name="yaw">Horizontal rotation in radians.</param>
  /// <param name="pitch">Vertical rotation in radians.</param>
  let withAngles (yaw: float32) (pitch: float32) (c: Camera) =
    let distance = Vector3.Distance(c.Position, c.Target)

    let pos =
      Vector3(
        distance * float32(Math.Sin(float yaw)) * float32(Math.Cos(float pitch)),
        distance * float32(Math.Sin(float pitch)),
        distance * float32(Math.Cos(float yaw)) * float32(Math.Cos(float pitch))
      )
      + c.Target

    let next = { c with Position = pos }
    { next with View = rebuildView next }

  /// <summary>
  /// Full orbit configuration around a target.
  /// </summary>
  let orbit target (yaw: float32) (pitch: float32) distance (c: Camera) =
    let pos =
      Vector3(
        distance * float32(Math.Sin(float yaw)) * float32(Math.Cos(float pitch)),
        distance * float32(Math.Sin(float pitch)),
        distance * float32(Math.Cos(float yaw)) * float32(Math.Cos(float pitch))
      )
      + target

    let next = {
      c with
          Position = pos
          Target = target
    }

    { next with View = rebuildView next }

  /// <summary>
  /// Creates a ray from screen coordinates for mouse/touch picking.
  /// </summary>
  let screenPointToRay
    (camera: Camera)
    (screenPos: Vector2)
    (viewport: Viewport)
    : Ray =
    let nearPoint = Vector3(screenPos.X, screenPos.Y, 0.0f)
    let farPoint = Vector3(screenPos.X, screenPos.Y, 1.0f)

    let nearSource =
      viewport.Unproject(
        nearPoint,
        camera.Projection,
        camera.View,
        Matrix.Identity
      )

    let farSource =
      viewport.Unproject(
        farPoint,
        camera.Projection,
        camera.View,
        Matrix.Identity
      )

    let direction = farSource - nearSource
    direction.Normalize()

    Ray(nearSource, direction)

  /// <summary>
  /// Calculates the BoundingFrustum for the camera.
  /// </summary>
  let boundingFrustum(camera: Camera) : BoundingFrustum =
    BoundingFrustum(camera.View * camera.Projection)

  /// <summary>
  /// Identity camera (for testing).
  /// </summary>
  let identity: Camera =
    let c = {
      Position = Vector3.Zero
      Target = Vector3.Forward
      Up = Vector3.Up
      Fov = MathHelper.ToRadians 45f
      Aspect = 1f
      Near = 0.1f
      Far = 1000f
      View = Matrix.Identity
      Projection = Matrix.Identity
    }

    {
      c with
          View = rebuildView c
          Projection = rebuildProjection c
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
    BoundingBox.CreateFromSphere(modelMesh.BoundingSphere)

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
    Pass =
      if material.Flags.HasFlag(MaterialFlags.Transparent) then
        Transparent
      else
        Opaque
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
[<Struct>]
type RenderCommand =
  | SetCamera of camera: Camera
  | SetLighting of lighting: LightingState
  | AddLight of light: Light
  | SetViewport of Viewport
  | ClearTarget of ctColor: Color voption * clearDepth: bool
  | Draw of drawable: Drawable
  | DrawSpriteQuad of spriteQuad: SpriteQuadCmd
  | DrawSpriteBillboard of spriteBillboard: SpriteBillboardCmd
  | DrawQuadEffect of quadEffect: EffectQuadCmd
  | DrawBillboardEffect of billboardEffect: EffectBillboardCmd
  | DrawLine of p1: Vector3 * p2: Vector3 * color: Color * pass: RenderPass
  | DrawLines of
    vertices: VertexPositionColor[] *
    lineCount: int *
    pass: RenderPass
  | DrawLinesEffect of
    vertices: VertexPositionColor[] *
    lineCount: int *
    effect: Effect *
    setup: EffectSetup voption *
    pass: RenderPass
  | DrawCustom of draw: (GraphicsDevice -> Camera -> unit)
