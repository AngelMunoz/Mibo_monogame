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
}

module Pipeline3DConfig =

  let defaults: Pipeline3DConfig = {
    Shadows = ValueNone
    DefaultLighting = ValueNone
    TileSize = 32
    ShadowCasterBinding = ValueNone
    BloomBinding = ValueNone
    PostProcessBinding = ValueNone
  }

  let inline withShadows (cfg: ShadowConfig) (pc: Pipeline3DConfig) = {
    pc with
        Shadows = ValueSome cfg
  }

  let inline withDefaultLighting
    (lighting: LightingState)
    (pc: Pipeline3DConfig)
    =
    {
      pc with
          DefaultLighting = ValueSome lighting
    }

  let inline withTileSize (size: int) (pc: Pipeline3DConfig) = {
    pc with
        TileSize = size
  }

  let inline withShadowCasterBinding
    (binding: ShadowCasterBinding)
    (pc: Pipeline3DConfig)
    =
    {
      pc with
          ShadowCasterBinding = ValueSome binding
    }

  let inline withBloomBinding (binding: BloomBinding) (pc: Pipeline3DConfig) = {
    pc with
        BloomBinding = ValueSome binding
  }

  let inline withPostProcessBinding
    (binding: PostProcessBinding)
    (pc: Pipeline3DConfig)
    =
    {
      pc with
          PostProcessBinding = ValueSome binding
    }
