namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D
open FSharp.UMX

[<Measure>]
type MaterialKey

[<Struct>]
type PBRMaterialData = {
  AlbedoColor: Color
  AlbedoMap: Texture2D voption
  Metallic: float32
  Roughness: float32
  EmissiveColor: Color
  EmissiveIntensity: float32
  NormalMap: Texture2D voption
  MetallicRoughnessMap: Texture2D voption
  AmbientOcclusionMap: Texture2D voption
}

module PBRMaterial =

  let defaults: PBRMaterialData = {
    AlbedoColor = Color.White
    AlbedoMap = ValueNone
    Metallic = 0f
    Roughness = 0.5f
    EmissiveColor = Color.Black
    EmissiveIntensity = 0f
    NormalMap = ValueNone
    MetallicRoughnessMap = ValueNone
    AmbientOcclusionMap = ValueNone
  }

  let defaultsUnlit: PBRMaterialData = {
    defaults with
        Metallic = 0f
        Roughness = 1f
  }

  let inline withAlbedo (color: Color) (mat: PBRMaterialData) = {
    mat with
        AlbedoColor = color
  }

  let inline withAlbedoMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        AlbedoMap = ValueSome tex
  }

  let inline withMetallic (value: float32) (mat: PBRMaterialData) = {
    mat with
        Metallic = value
  }

  let inline withRoughness (value: float32) (mat: PBRMaterialData) = {
    mat with
        Roughness = value
  }

  let inline withEmissive
    (color: Color)
    (intensity: float32)
    (mat: PBRMaterialData)
    =
    {
      mat with
          EmissiveColor = color
          EmissiveIntensity = intensity
    }

  let inline withNormalMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        NormalMap = ValueSome tex
  }

  let inline withMetallicRoughnessMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        MetallicRoughnessMap = ValueSome tex
  }

  let inline withAmbientOcclusionMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        AmbientOcclusionMap = ValueSome tex
  }

[<Struct>]
type RenderContext = {
  Camera: Camera
  LightingState: LightingState
  LightDataTexture: Texture2D voption
  LightCount: int
  TileDataTexture: Texture2D voption
  TileSize: int
  TilesX: int
  TilesY: int
  MaxLightsPerTile: int
  ShadowAtlas: Texture2D voption
  ShadowMatrixTexture: Texture2D voption
  ShadowMatrixCount: int
  ShadowAtlasTilesX: int
  ShadowAtlasSize: float32
  ShadowBias: float32
  ShadowNormalBias: float32
  AmbientColor: Vector3
  AmbientIntensity: float32
}

type EffectBinding = {
  Effect: Effect
  MaterialKey: int<MaterialKey>
  BindGlobal: RenderContext -> unit
  BindPerMaterial: PBRMaterialData voption -> unit
  BindPerInstance: Matrix -> Matrix[] voption -> unit
}

[<Struct>]
type ShadowCasterContext = {
  View: Matrix
  Projection: Matrix
  ShadowBias: float32
  ShadowNormalBias: float32
  ShadowAtlasTilesX: int
  ShadowAtlasSize: float32
}

type ShadowCasterBinding = {
  Effect: Effect
  BindPerFace: ShadowCasterContext -> unit
  BindPerInstance: Matrix -> Matrix[] voption -> unit
}

[<Struct>]
type BloomContext = {
  RenderContext: RenderContext
  SceneTexture: Texture2D
  SceneWidth: int
  SceneHeight: int
  TexelSize: Vector2
}

type BloomBinding = {
  Effect: Effect
  Bind: BloomContext -> unit
}

[<Struct>]
type PostProcessContext = {
  RenderContext: RenderContext
  SceneTexture: Texture2D
  BloomTexture: Texture2D voption
  Time: float32
}

type PostProcessBinding = {
  Effect: Effect
  Bind: PostProcessContext -> unit
}

[<Struct>]
type Drawable = {
  Mesh: Mesh
  Transform: Matrix
  Bones: Matrix[] voption
  BoundingSphere: BoundingSphere
  Pass: RenderPass
  MaterialKey: int<MaterialKey>
  MaterialData: PBRMaterialData voption
  Binding: EffectBinding
}

[<Struct>]
type SortKey = {
  Distance: float32
  Pass: RenderPass
  MaterialKey: int<MaterialKey>
  Effect: Effect
}

module SortKey =

  let inline create
    (distance: float32)
    (pass: RenderPass)
    (materialKey: int<MaterialKey>)
    (effect: Effect)
    : SortKey =
    {
      Distance = distance
      Pass = pass
      MaterialKey = materialKey
      Effect = effect
    }

  let inline opaque (distance: float32) (effect: Effect) : SortKey = {
    Distance = distance
    Pass = RenderPass.Opaque
    MaterialKey = 0<MaterialKey>
    Effect = effect
  }

  let inline transparent (distance: float32) (effect: Effect) : SortKey = {
    Distance = distance
    Pass = RenderPass.Transparent
    MaterialKey = 0<MaterialKey>
    Effect = effect
  }

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
  | DrawCustom of sortKey: SortKey * draw: (GameContext * RenderContext -> unit)
