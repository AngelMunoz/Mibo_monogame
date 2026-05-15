namespace Mibo.Rendering3D

open Mibo.Rendering.Graphics3D

[<Struct>]
type Pipeline3DConfig = {
  Shadows: ShadowConfig voption
  PostProcess: PostProcessConfig voption
  DefaultLighting: LightingState voption
  TileSize: int
}

module Pipeline3DConfig =
  val defaults: Pipeline3DConfig

  val inline withShadows:
    cfg: ShadowConfig -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withPostProcess:
    cfg: PostProcessConfig -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withDefaultLighting:
    lighting: LightingState -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withTileSize: size: int -> pc: Pipeline3DConfig -> Pipeline3DConfig
