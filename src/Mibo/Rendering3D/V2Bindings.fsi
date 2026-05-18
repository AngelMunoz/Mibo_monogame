namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

module Bindings =
  val basicEffect:
    materialKey: int<MaterialKey> -> effect: Effect -> EffectBinding

  val pbr:
    material: PBRMaterialData ->
    materialKey: int<MaterialKey> ->
    effect: Effect ->
      EffectBinding

  val shadowCaster: effect: Effect -> ShadowCasterBinding

  val bloom:
    threshold: float32 -> intensity: float32 -> effect: Effect -> BloomBinding

  val postProcess: toneMappingMode: int -> effect: Effect -> PostProcessBinding
