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

  let defaults: Pipeline3DConfig = {
    Shadows = ValueNone
    PostProcess = ValueNone
    DefaultLighting = ValueNone
    TileSize = 32
  }

  let inline withShadows (cfg: ShadowConfig) (pc: Pipeline3DConfig) = {
    pc with
        Shadows = ValueSome cfg
  }

  let inline withPostProcess (cfg: PostProcessConfig) (pc: Pipeline3DConfig) = {
    pc with
        PostProcess = ValueSome cfg
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
