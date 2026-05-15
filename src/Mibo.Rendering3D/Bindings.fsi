namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

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

module Bindings =
  val basicEffect:
    materialKey: int<MaterialKey> -> effect: Effect -> EffectBinding

  val pbr:
    material: PBRMaterialData ->
    materialKey: int<MaterialKey> ->
    effect: Effect ->
      EffectBinding
