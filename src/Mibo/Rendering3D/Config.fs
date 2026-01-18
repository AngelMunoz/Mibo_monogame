namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Pipeline Configuration
// ============================================================================

/// Soft shadow configuration
[<Struct>]
type SoftShadowConfig = { Penumbra: float32 }

/// Shadow configuration
[<Struct>]
type ShadowConfig = {
  Resolution: int
  CascadeCount: int
  PCFSamples: int
  SoftShadows: SoftShadowConfig voption
  Bias: float32
  NormalBias: float32
  MaxPointShadows: int
  AtlasTiles: int
}

module ShadowConfig =
  let defaults: ShadowConfig = {
    Resolution = 1024
    CascadeCount = 3
    PCFSamples = 4
    SoftShadows = ValueNone
    Bias = 0.005f
    NormalBias = 0.01f
    MaxPointShadows = 4
    AtlasTiles = 4
  }

  let withResolution (res: int) (cfg: ShadowConfig) = {
    cfg with
        Resolution = res
  }

  let withCascades (n: int) (cfg: ShadowConfig) = { cfg with CascadeCount = n }
  let withPCFSamples (n: int) (cfg: ShadowConfig) = { cfg with PCFSamples = n }

  let withSoftShadows (penumbra: float32) (cfg: ShadowConfig) = {
    cfg with
        SoftShadows = ValueSome { Penumbra = penumbra }
  }

  let withBias (bias: float32) (normalBias: float32) (cfg: ShadowConfig) = {
    cfg with
        Bias = bias
        NormalBias = normalBias
  }

  let withMaxPointShadows (n: int) (cfg: ShadowConfig) = {
    cfg with
        MaxPointShadows = n
  }

  let withAtlasTiles (n: int) (cfg: ShadowConfig) = {
    cfg with
        AtlasTiles = n
  }

/// SSAO configuration
[<Struct>]
type SSAOConfig = {
  Radius: float32
  Intensity: float32
  SampleCount: int
}

module SSAOConfig =
  let defaults: SSAOConfig = {
    Radius = 0.5f
    Intensity = 1f
    SampleCount = 16
  }

/// Bloom configuration
[<Struct>]
type BloomConfig = {
  Threshold: float32
  Intensity: float32
  Scatter: float32
}

module BloomConfig =
  let defaults: BloomConfig = {
    Threshold = 1f
    Intensity = 0.5f
    Scatter = 0.7f
  }

/// Tone mapping options
type ToneMappingConfig =
  | NoToneMapping
  | Reinhard
  | ACES
  | Filmic
  | AgX

/// Post-processing configuration
[<Struct>]
type PostProcessConfig = {
  SSAO: SSAOConfig voption
  Bloom: BloomConfig voption
  ToneMapping: ToneMappingConfig
}

module PostProcessConfig =
  let defaults: PostProcessConfig = {
    SSAO = ValueNone
    Bloom = ValueNone
    ToneMapping = ToneMappingConfig.ACES
  }

  let withSSAO (cfg: SSAOConfig) (pp: PostProcessConfig) = {
    pp with
        SSAO = ValueSome cfg
  }

  let withBloom (cfg: BloomConfig) (pp: PostProcessConfig) = {
    pp with
        Bloom = ValueSome cfg
  }

  let withToneMapping (tm: ToneMappingConfig) (pp: PostProcessConfig) = {
    pp with
        ToneMapping = tm
  }

/// Main pipeline configuration
[<Struct>]
type PipelineConfig = {
  ShadowPath: ShadowPath
  Shadows: ShadowConfig voption
  PostProcess: PostProcessConfig voption
  DefaultLighting: LightingState voption
  ShaderOverrides: Map<ShaderBase, string>
  /// Optional callback to run custom logic before the main render pass (e.g. Depth Pre-Pass, Compute Shaders)
  PreRenderCallback: (GraphicsDevice -> Camera -> LightingState -> unit) voption
  /// Optional override for binding light data to shaders
  LightingBinder: (Effect -> Camera -> LightingState -> unit) voption
}

module PipelineConfig =
  let defaults: PipelineConfig = {
    ShadowPath = Auto
    Shadows = ValueNone
    PostProcess = ValueNone
    DefaultLighting = ValueNone
    ShaderOverrides = Map.empty
    PreRenderCallback = ValueNone
    LightingBinder = ValueNone
  }

  let withShadowPath (path: ShadowPath) (pc: PipelineConfig) = {
    pc with
        ShadowPath = path
  }

  let withShadows (cfg: ShadowConfig) (pc: PipelineConfig) = {
    pc with
        Shadows = ValueSome cfg
  }

  let withPostProcess (cfg: PostProcessConfig) (pc: PipelineConfig) = {
    pc with
        PostProcess = ValueSome cfg
  }

  let withDefaultLighting (lighting: LightingState) (pc: PipelineConfig) = {
    pc with
        DefaultLighting = ValueSome lighting
  }

  let withShader
    (shaderBase: ShaderBase)
    (assetName: string)
    (pc: PipelineConfig)
    =
    {
      pc with
          ShaderOverrides = pc.ShaderOverrides.Add(shaderBase, assetName)
    }
