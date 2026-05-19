namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
type PipelineBuffer<'Cmd> = Mibo.Elmish.RenderBuffer<unit, 'Cmd>

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.RenderPass instead.")>]
type RenderPass = Mibo.Rendering.RenderPass

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.EffectContext instead.")>]
type EffectContext = Mibo.Rendering.EffectContext

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.EffectSetup instead.")>]
type EffectSetup = Mibo.Rendering.EffectSetup

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.BillboardMode instead.")>]
type BillboardMode = Mibo.Rendering.BillboardMode

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.Quad3D instead.")>]
type Quad3D = Mibo.Rendering.Quad3D

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Rendering.Billboard3D instead.")>]
type Billboard3D = Mibo.Rendering.Billboard3D

/// <summary>Camera for 3D rendering (deprecated).</summary>
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Elmish.Camera instead.")>]
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

[<Obsolete("Mibo.Rendering.Graphics3D is deprecated. Use Mibo.Elmish.Camera3D instead.")>]
module Camera =
  let rebuildView(c: Camera) =
    Matrix.CreateLookAt(c.Position, c.Target, c.Up)

  let rebuildProjection(c: Camera) =
    Matrix.CreatePerspectiveFieldOfView(c.Fov, c.Aspect, c.Near, c.Far)

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

  let perspective position target up fov aspect near far : Camera =
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

/// Shader base types for override mapping
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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

/// Mesh geometry reference
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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

// --- Sprite3D / Billboard Command Types ---

/// <summary>Sprite-style quad draw.</summary>
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
type SpriteQuadCmd = {
  Pass: RenderPass
  Texture: Texture2D
  Quad: Quad3D
}

/// <summary>Sprite-style billboard draw.</summary>
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
type SpriteBillboardCmd = {
  Pass: RenderPass
  Texture: Texture2D
  Billboard: Billboard3D
}

/// <summary>Effect-driven quad draw.</summary>
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
type EffectQuadCmd = {
  Pass: RenderPass
  Effect: Effect
  Setup: EffectSetup voption
  Quad: Quad3D
}

/// <summary>Effect-driven billboard draw.</summary>
[<Struct>]
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
[<Obsolete("Mibo.Rendering.Graphics3D is deprecated and will be removed. Use Mibo.Rendering.Graphics3D.V3 instead.")>]
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
