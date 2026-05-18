namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

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

  let inline setVector2 (param: EffectParameter voption) (value: Vector2) =
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
      BindPerMaterial = fun _ -> ()
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
    let tileDataTexParam = resolveParam effect "TileDataTexture"
    let tileSizeParam = resolveParam effect "TileSize"
    let tilesXParam = resolveParam effect "TilesX"
    let tilesYParam = resolveParam effect "TilesY"
    let maxLightsPerTileParam = resolveParam effect "MaxLightsPerTile"
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

      match ctx.TileDataTexture with
      | ValueSome tex ->
        setTexture tileDataTexParam (tex :> Texture)
        setFloat tileSizeParam (float32 ctx.TileSize)
        setFloat tilesXParam (float32 ctx.TilesX)
        setFloat tilesYParam (float32 ctx.TilesY)
        setFloat maxLightsPerTileParam (float32 ctx.MaxLightsPerTile)
      | ValueNone -> ()

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

    let bindPerMaterial(overrideData: PBRMaterialData voption) =
      let data = overrideData |> ValueOption.defaultValue material
      setColorAsVector3 albedoColorParam data.AlbedoColor
      setFloat metallicParam data.Metallic
      setFloat roughnessParam data.Roughness
      setVector3 emissiveColorParam (data.EmissiveColor.ToVector3())
      setFloat emissiveIntensityParam data.EmissiveIntensity

      match data.AlbedoMap with
      | ValueSome tex ->
        setFloat hasAlbedoMapParam 1.0f
        setTexture albedoMapParam (tex :> Texture)
      | ValueNone -> setFloat hasAlbedoMapParam 0.0f

      data.NormalMap
      |> ValueOption.iter(fun tex -> setTexture normalMapParam (tex :> Texture))

      data.MetallicRoughnessMap
      |> ValueOption.iter(fun tex ->
        setTexture metallicRoughnessMapParam (tex :> Texture))

      data.AmbientOcclusionMap
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

  let shadowCaster(effect: Effect) : ShadowCasterBinding =
    let viewParam = resolveParam effect "View"
    let projParam = resolveParam effect "Projection"
    let worldParam = resolveParam effect "World"
    let bonesParam = resolveParam effect "Bones"
    let shadowBiasParam = resolveParam effect "ShadowBias"
    let shadowNormalBiasParam = resolveParam effect "ShadowNormalBias"
    let shadowAtlasTilesXParam = resolveParam effect "ShadowAtlasTilesX"
    let shadowAtlasSizeParam = resolveParam effect "ShadowAtlasSize"

    {
      Effect = effect
      BindPerFace =
        fun (ctx: ShadowCasterContext) ->
          setMatrix viewParam ctx.View
          setMatrix projParam ctx.Projection
          setFloat shadowBiasParam ctx.ShadowBias
          setFloat shadowNormalBiasParam ctx.ShadowNormalBias
          setFloat shadowAtlasTilesXParam (float32 ctx.ShadowAtlasTilesX)
          setFloat shadowAtlasSizeParam ctx.ShadowAtlasSize
      BindPerInstance =
        fun transform bones ->
          setMatrix worldParam transform
          bones |> ValueOption.iter(fun b -> setMatrixArray bonesParam b)
    }

  let bloom
    (threshold: float32)
    (intensity: float32)
    (effect: Effect)
    : BloomBinding =
    let thresholdParam = resolveParam effect "Threshold"
    let intensityParam = resolveParam effect "Intensity"
    let sceneTexParam = resolveParam effect "SceneTexture"
    let texelSizeParam = resolveParam effect "TexelSize"

    {
      Effect = effect
      Bind =
        fun (ctx: BloomContext) ->
          setFloat thresholdParam threshold
          setFloat intensityParam intensity
          setTexture sceneTexParam (ctx.SceneTexture :> Texture)
          setVector2 texelSizeParam ctx.TexelSize
    }

  let postProcess (toneMappingMode: int) (effect: Effect) : PostProcessBinding =
    let sceneTexParam = resolveParam effect "SceneTexture"
    let bloomTexParam = resolveParam effect "BloomTexture"
    let toneMappingParam = resolveParam effect "ToneMapping"
    let timeParam = resolveParam effect "Time"

    {
      Effect = effect
      Bind =
        fun (ctx: PostProcessContext) ->
          setTexture sceneTexParam (ctx.SceneTexture :> Texture)

          ctx.BloomTexture
          |> ValueOption.iter(fun bt ->
            setTexture bloomTexParam (bt :> Texture))

          setFloat toneMappingParam (float32 toneMappingMode)
          setFloat timeParam ctx.Time
    }
