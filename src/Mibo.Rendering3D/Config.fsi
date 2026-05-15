namespace Mibo.Rendering3D

open Mibo.Rendering.Graphics3D

[<Struct>]
type Pipeline3DConfig = {
  Shadows: ShadowConfig voption
  PostProcess: PostProcessConfig voption
  DefaultLighting: LightingState voption
  TileSize: int
  ShadowCasterAsset: string voption
  BloomEffectAsset: string voption
  PostProcessEffectAsset: string voption
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

  val inline withShadowCasterAsset:
    asset: string -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withBloomEffectAsset:
    asset: string -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withPostProcessEffectAsset:
    asset: string -> pc: Pipeline3DConfig -> Pipeline3DConfig
