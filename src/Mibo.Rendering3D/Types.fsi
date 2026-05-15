namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

[<Measure>]
type MaterialKey

[<Struct>]
type RenderContext = {
  Camera: Camera
  LightingState: LightingState
  LightDataTexture: Texture2D voption
  LightCount: int
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
  BindPerMaterial: unit -> unit
  BindPerInstance: Matrix -> Matrix[] voption -> unit
}

[<Struct>]
type Drawable = {
  Mesh: Mesh
  Transform: Matrix
  Bones: Matrix[] voption
  BoundingSphere: BoundingSphere
  Pass: RenderPass
  MaterialKey: int<MaterialKey>
  Binding: EffectBinding
}

[<Struct>]
type SortKey = {
  Distance: float32
  Pass: RenderPass
  MaterialKey: int<MaterialKey>
  Effect: Effect
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
