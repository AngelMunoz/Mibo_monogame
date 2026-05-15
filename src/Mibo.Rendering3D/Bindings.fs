namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

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

  let defaults: PBRMaterialData = {
    AlbedoColor = Color.White
    AlbedoMap = ValueNone
    Metallic = 0f
    Roughness = 0.5f
    EmissiveColor = Color.Black
    EmissiveIntensity = 0f
    NormalMap = ValueNone
    MetallicRoughnessMap = ValueNone
    AmbientOcclusionMap = ValueNone
  }

  let defaultsUnlit: PBRMaterialData = {
    defaults with
        Metallic = 0f
        Roughness = 1f
  }

  let inline withAlbedo (color: Color) (mat: PBRMaterialData) = {
    mat with
        AlbedoColor = color
  }

  let inline withAlbedoMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        AlbedoMap = ValueSome tex
  }

  let inline withMetallic (value: float32) (mat: PBRMaterialData) = {
    mat with
        Metallic = value
  }

  let inline withRoughness (value: float32) (mat: PBRMaterialData) = {
    mat with
        Roughness = value
  }

  let inline withEmissive
    (color: Color)
    (intensity: float32)
    (mat: PBRMaterialData)
    =
    {
      mat with
          EmissiveColor = color
          EmissiveIntensity = intensity
    }

  let inline withNormalMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        NormalMap = ValueSome tex
  }

  let inline withMetallicRoughnessMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        MetallicRoughnessMap = ValueSome tex
  }

  let inline withAmbientOcclusionMap (tex: Texture2D) (mat: PBRMaterialData) = {
    mat with
        AmbientOcclusionMap = ValueSome tex
  }

module Bindings =

  let inline resolveParam
    (effect: Effect)
    (name: string)
    : EffectParameter voption =
    match effect.Parameters.[name] with
    | null -> ValueNone
    | p -> ValueSome p

  let inline setIfExists
    (param: EffectParameter voption)
    (value: 'T)
    (setter: EffectParameter -> 'T -> unit)
    =
    param |> ValueOption.iter(fun p -> setter p value)

  let inline setMatrix (param: EffectParameter voption) (value: Matrix) =
    setIfExists param value (fun p v -> p.SetValue(v))

  let inline setFloat (param: EffectParameter voption) (value: float32) =
    setIfExists param value (fun p v -> p.SetValue(v))

  let inline setVector3 (param: EffectParameter voption) (value: Vector3) =
    setIfExists param value (fun p v -> p.SetValue(v))

  let inline setTexture (param: EffectParameter voption) (value: Texture) =
    setIfExists param value (fun p v -> p.SetValue(v))

  let inline setMatrixArray (param: EffectParameter voption) (value: Matrix[]) =
    setIfExists param value (fun p v -> p.SetValue(v))

  let inline setColorAsVector3 (param: EffectParameter voption) (value: Color) =
    setIfExists param (value.ToVector3()) (fun p v -> p.SetValue(v))

  let basicEffect
    (materialKey: int<MaterialKey>)
    (effect: Effect)
    : EffectBinding =
    let worldParam = resolveParam effect "World"
    let viewParam = resolveParam effect "View"
    let projParam = resolveParam effect "Projection"
    let ambientColorParam = resolveParam effect "AmbientColor"
    let lightCountParam = resolveParam effect "LightCount"
    let lightDataTexParam = resolveParam effect "LightDataTexture"
    let shadowAtlasParam = resolveParam effect "ShadowAtlas"
    let shadowBiasParam = resolveParam effect "ShadowBias"
    let shadowNormalBiasParam = resolveParam effect "ShadowNormalBias"
    let shadowAtlasTilesXParam = resolveParam effect "ShadowAtlasTilesX"
    let shadowAtlasSizeParam = resolveParam effect "ShadowAtlasSize"
    let shadowMatrixTextureParam = resolveParam effect "ShadowMatrixTexture"
    let shadowMatrixCountParam = resolveParam effect "ShadowMatrixCount"
    let bonesParam = resolveParam effect "Bones"

    let bindGlobal(ctx: RenderContext) =
      setMatrix viewParam ctx.Camera.View
      setMatrix projParam ctx.Camera.Projection
      setVector3 ambientColorParam (ctx.AmbientColor * ctx.AmbientIntensity)

      match ctx.LightDataTexture with
      | ValueSome tex ->
        setTexture lightDataTexParam (tex :> Texture)
        setFloat lightCountParam (float32 ctx.LightCount)
      | ValueNone -> setFloat lightCountParam 0.0f

      match ctx.ShadowAtlas with
      | ValueSome atlas ->
        setTexture shadowAtlasParam (atlas :> Texture)
        setFloat shadowAtlasTilesXParam (float32 ctx.ShadowAtlasTilesX)
        setFloat shadowAtlasSizeParam ctx.ShadowAtlasSize
        setFloat shadowBiasParam ctx.ShadowBias
        setFloat shadowNormalBiasParam ctx.ShadowNormalBias
      | ValueNone -> ()

      match ctx.ShadowMatrixTexture with
      | ValueSome tex ->
        setTexture shadowMatrixTextureParam (tex :> Texture)
        setFloat shadowMatrixCountParam (float32 ctx.ShadowMatrixCount)
      | ValueNone -> ()

    {
      Effect = effect
      MaterialKey = materialKey
      BindGlobal = bindGlobal
      BindPerMaterial = fun () -> ()
      BindPerInstance =
        fun transform bones ->
          setMatrix worldParam transform
          bones |> ValueOption.iter(fun b -> setMatrixArray bonesParam b)
    }

  let pbr
    (material: PBRMaterialData)
    (materialKey: int<MaterialKey>)
    (effect: Effect)
    : EffectBinding =
    let worldParam = resolveParam effect "World"
    let viewParam = resolveParam effect "View"
    let projParam = resolveParam effect "Projection"
    let bonesParam = resolveParam effect "Bones"
    let albedoColorParam = resolveParam effect "AlbedoColor"
    let metallicParam = resolveParam effect "Metallic"
    let roughnessParam = resolveParam effect "Roughness"
    let emissiveColorParam = resolveParam effect "EmissiveColor"
    let emissiveIntensityParam = resolveParam effect "EmissiveIntensity"
    let hasAlbedoMapParam = resolveParam effect "HasAlbedoMap"
    let albedoMapParam = resolveParam effect "AlbedoMap"
    let normalMapParam = resolveParam effect "NormalMap"
    let metallicRoughnessMapParam = resolveParam effect "MetallicRoughnessMap"
    let ambientOcclusionMapParam = resolveParam effect "AmbientOcclusionMap"
    let ambientColorParam = resolveParam effect "AmbientColor"
    let lightCountParam = resolveParam effect "LightCount"
    let lightDataTexParam = resolveParam effect "LightDataTexture"
    let shadowAtlasParam = resolveParam effect "ShadowAtlas"
    let shadowBiasParam = resolveParam effect "ShadowBias"
    let shadowNormalBiasParam = resolveParam effect "ShadowNormalBias"
    let shadowAtlasTilesXParam = resolveParam effect "ShadowAtlasTilesX"
    let shadowAtlasSizeParam = resolveParam effect "ShadowAtlasSize"
    let shadowMatrixTextureParam = resolveParam effect "ShadowMatrixTexture"
    let shadowMatrixCountParam = resolveParam effect "ShadowMatrixCount"

    let bindGlobal(ctx: RenderContext) =
      setMatrix viewParam ctx.Camera.View
      setMatrix projParam ctx.Camera.Projection
      setVector3 ambientColorParam (ctx.AmbientColor * ctx.AmbientIntensity)

      match ctx.LightDataTexture with
      | ValueSome tex ->
        setTexture lightDataTexParam (tex :> Texture)
        setFloat lightCountParam (float32 ctx.LightCount)
      | ValueNone -> setFloat lightCountParam 0.0f

      match ctx.ShadowAtlas with
      | ValueSome atlas ->
        setTexture shadowAtlasParam (atlas :> Texture)
        setFloat shadowAtlasTilesXParam (float32 ctx.ShadowAtlasTilesX)
        setFloat shadowAtlasSizeParam ctx.ShadowAtlasSize
        setFloat shadowBiasParam ctx.ShadowBias
        setFloat shadowNormalBiasParam ctx.ShadowNormalBias
      | ValueNone -> ()

      match ctx.ShadowMatrixTexture with
      | ValueSome tex ->
        setTexture shadowMatrixTextureParam (tex :> Texture)
        setFloat shadowMatrixCountParam (float32 ctx.ShadowMatrixCount)
      | ValueNone -> ()

    let bindPerMaterial() =
      setColorAsVector3 albedoColorParam material.AlbedoColor
      setFloat metallicParam material.Metallic
      setFloat roughnessParam material.Roughness
      setVector3 emissiveColorParam (material.EmissiveColor.ToVector3())
      setFloat emissiveIntensityParam material.EmissiveIntensity

      match material.AlbedoMap with
      | ValueSome tex ->
        setFloat hasAlbedoMapParam 1.0f
        setTexture albedoMapParam (tex :> Texture)
      | ValueNone -> setFloat hasAlbedoMapParam 0.0f

      material.NormalMap
      |> ValueOption.iter(fun tex -> setTexture normalMapParam (tex :> Texture))

      material.MetallicRoughnessMap
      |> ValueOption.iter(fun tex ->
        setTexture metallicRoughnessMapParam (tex :> Texture))

      material.AmbientOcclusionMap
      |> ValueOption.iter(fun tex ->
        setTexture ambientOcclusionMapParam (tex :> Texture))

    let bindPerInstance (transform: Matrix) (bones: Matrix[] voption) =
      setMatrix worldParam transform
      bones |> ValueOption.iter(fun b -> setMatrixArray bonesParam b)

    {
      Effect = effect
      MaterialKey = materialKey
      BindGlobal = bindGlobal
      BindPerMaterial = bindPerMaterial
      BindPerInstance = bindPerInstance
    }
