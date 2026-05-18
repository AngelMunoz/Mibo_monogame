namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

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
  val defaults: PBRMaterialData
  val defaultsUnlit: PBRMaterialData
  val inline withAlbedo: color: Color -> mat: PBRMaterialData -> PBRMaterialData

  val inline withAlbedoMap:
    tex: Texture2D -> mat: PBRMaterialData -> PBRMaterialData

  val inline withMetallic:
    value: float32 -> mat: PBRMaterialData -> PBRMaterialData

  val inline withRoughness:
    value: float32 -> mat: PBRMaterialData -> PBRMaterialData

  val inline withEmissive:
    color: Color ->
    intensity: float32 ->
    mat: PBRMaterialData ->
      PBRMaterialData

  val inline withNormalMap:
    tex: Texture2D -> mat: PBRMaterialData -> PBRMaterialData

  val inline withMetallicRoughnessMap:
    tex: Texture2D -> mat: PBRMaterialData -> PBRMaterialData

  val inline withAmbientOcclusionMap:
    tex: Texture2D -> mat: PBRMaterialData -> PBRMaterialData

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
  val inline create:
    distance: float32 ->
    pass: RenderPass ->
    materialKey: int<MaterialKey> ->
    effect: Effect ->
      SortKey

  val inline opaque: distance: float32 -> effect: Effect -> SortKey
  val inline transparent: distance: float32 -> effect: Effect -> SortKey

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
