namespace Mibo.Rendering.Graphics3D.V2

open Mibo.Rendering.Graphics3D

[<Struct>]
type Pipeline3DConfig = {
  Shadows: ShadowConfig voption
  DefaultLighting: LightingState voption
  TileSize: int
  ShadowCasterBinding: ShadowCasterBinding voption
  BloomBinding: BloomBinding voption
  PostProcessBinding: PostProcessBinding voption
  CustomPasses: CustomPass[]
  LightDataProvider: ILightDataProvider voption
  TileCullingProvider: ITileCullingProvider voption
}

module Pipeline3DConfig =
  val defaults: Pipeline3DConfig

  val inline withShadows:
    cfg: ShadowConfig -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withDefaultLighting:
    lighting: LightingState -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withTileSize: size: int -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withShadowCasterBinding:
    binding: ShadowCasterBinding -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withBloomBinding:
    binding: BloomBinding -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withPostProcessBinding:
    binding: PostProcessBinding -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withCustomPasses:
    passes: CustomPass[] -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withLightDataProvider:
    provider: ILightDataProvider -> pc: Pipeline3DConfig -> Pipeline3DConfig

  val inline withTileCullingProvider:
    provider: ITileCullingProvider -> pc: Pipeline3DConfig -> Pipeline3DConfig
