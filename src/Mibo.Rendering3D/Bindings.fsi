namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

module Bindings =
  val basicEffect:
    materialKey: int<MaterialKey> -> effect: Effect -> EffectBinding

  val pbr:
    material: PBRMaterialData ->
    materialKey: int<MaterialKey> ->
    effect: Effect ->
      EffectBinding