namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Pipeline Configuration
// ============================================================================

/// <summary>
/// Configuration for soft shadow rendering (PCF/Poisson).
/// </summary>
[<Struct>]
type SoftShadowConfig = {
  /// <summary>
  /// The physical radius of the light source in world units.
  /// Used by advanced algorithms (like PCSS) to calculate variable penumbra softness.
  /// Larger values create softer shadows that blur more over distance.
  /// </summary>
  Penumbra: float32
}

/// <summary>
/// Configuration for the Shadow Atlas system.
/// </summary>
[<Struct>]
type ShadowConfig = {
  /// <summary>
  /// The resolution for a single shadow map slice (e.g., 2048 or 4096).
  /// A higher resolution reduces aliasing but increases VRAM usage significantly
  /// as it scales with the number of AtlasTiles.
  /// </summary>
  Resolution: int

  /// <summary>
  /// Number of cascades to use for Directional Lights (typically 3 or 4).
  /// Cascades distribute resolution over distance.
  /// 4 cascades provide the best quality for open worlds but require 4 render passes per directional light.
  /// </summary>
  CascadeCount: int

  /// <summary>
  /// Number of samples for Percentage Closer Filtering (PCF).
  /// Ignored if a custom shader uses a different technique (e.g., Poisson Disk).
  /// </summary>
  PCFSamples: int

  /// <summary>
  /// Enables soft shadows if set (ValueSome).
  /// </summary>
  SoftShadows: SoftShadowConfig voption

  /// <summary>
  /// Constant depth bias to prevent shadow acne (self-shadowing artifacts).
  /// Too high: shadows detach from objects (Peter Panning).
  /// Too low: moire patterns appear on surfaces.
  /// </summary>
  Bias: float32

  /// <summary>
  /// Bias applied along the surface normal to prevent acne on curved surfaces.
  /// </summary>
  NormalBias: float32

  /// <summary>
  /// Maximum number of point lights that can cast shadows.
  /// (Note: This logic is largely superseded by the AtlasTiles limit).
  /// </summary>
  MaxPointShadows: int

  /// <summary>
  /// Number of tiles across the shadow atlas (Width/Height).
  /// Total shadow slots = AtlasTiles * AtlasTiles.
  /// Example: 12 tiles = 144 slots.
  /// Crucial for determining how many lights can cast shadows simultaneously.
  /// </summary>
  AtlasTiles: int

  /// <summary>
  /// Hard limit for the Shadow Atlas texture size (e.g., 16384).
  /// If (Resolution * AtlasTiles) exceeds this, the effective resolution per tile will be downscaled.
  /// Watch VRAM usage: a 16k atlas consumes ~2GB of memory.
  /// </summary>
  MaxAtlasSize: int
}

module ShadowConfig =
  /// Default shadow settings: 1024px, 3 Cascades, 4x4 Atlas (16 slots), 8k Max Texture.
  let defaults: ShadowConfig = {
    Resolution = 1024
    CascadeCount = 3
    PCFSamples = 4
    SoftShadows = ValueNone
    Bias = 0.005f
    NormalBias = 0.01f
    MaxPointShadows = 4
    AtlasTiles = 4
    MaxAtlasSize = 8192
  }

  /// <summary>
  /// Sets the resolution of a single shadow map slice (e.g., 2048).
  /// Higher values reduce aliasing (jaggies) but increase VRAM usage and GPU memory bandwidth.
  /// </summary>
  let withResolution (res: int) (cfg: ShadowConfig) = {
    cfg with
        Resolution = res
  }

  /// <summary>
  /// Sets the number of cascades for directional light shadows (typically 3 or 4).
  /// More cascades improve shadow sharpness at various distances but increase the number of draw calls per directional light.
  /// </summary>
  let withCascades (n: int) (cfg: ShadowConfig) = { cfg with CascadeCount = n }

  /// <summary>
  /// Sets the number of PCF samples for soft shadow filtering (e.g., 16).
  /// Higher sample counts create smoother soft shadows but are more expensive to compute in the pixel shader.
  /// </summary>
  let withPCFSamples (n: int) (cfg: ShadowConfig) = { cfg with PCFSamples = n }

  /// <summary>
  /// Enables the soft shadow code path in the shader.
  /// 'penumbra' controls the physical size of the light source, determining how blurry the shadows get with distance.
  /// </summary>
  let withSoftShadows (penumbra: float32) (cfg: ShadowConfig) = {
    cfg with
        SoftShadows = ValueSome { Penumbra = penumbra }
  }

  /// <summary>
  /// Tunable bias values to prevent shadow mapping artifacts.
  /// 'bias': Constant depth offset to prevent acne (self-shadowing).
  /// 'normalBias': Offset along the geometric normal to cure acne on curved surfaces without inducing Peter-Panning (detached shadows).
  /// </summary>
  let withBias (bias: float32) (normalBias: float32) (cfg: ShadowConfig) = {
    cfg with
        Bias = bias
        NormalBias = normalBias
  }

  /// <summary>
  /// Sets the maximum number of point lights that can cast shadows.
  /// Note: This is largely superseded by 'withAtlasTiles', which determines the total slot capacity.
  /// </summary>
  let withMaxPointShadows (n: int) (cfg: ShadowConfig) = {
    cfg with
        MaxPointShadows = n
  }

  /// <summary>
  /// Sets the grid size (N x N) of the shadow atlas texture.
  /// Determines the total capacity for shadow casting lights (Total Slots = N * N).
  /// </summary>
  let withAtlasTiles (n: int) (cfg: ShadowConfig) = { cfg with AtlasTiles = n }

  /// <summary>
  /// Sets a hard limit on the shadow atlas texture size in pixels (e.g., 8192).
  /// Prevents the system from allocating too much VRAM. If (Resolution * Tiles) exceeds this, individual shadow maps will be downscaled.
  /// </summary>
  let withMaxAtlasSize (n: int) (cfg: ShadowConfig) = {
    cfg with
        MaxAtlasSize = n
  }

/// <summary>
/// Configuration for Screen Space Ambient Occlusion (SSAO).
/// Adds depth perception by darkening corners and crevices where geometry is close together.
/// </summary>
[<Struct>]
type SSAOConfig = {
  /// <summary>
  /// Sampling radius in world units. Larger values cover more area but may include distant geometry.
  /// Typical values: 0.3f - 1.0f
  /// </summary>
  Radius: float32
  /// <summary>
  /// Strength of the ambient occlusion effect. Higher values create darker, more pronounced shadows.
  /// Typical values: 0.5f - 2.0f
  /// </summary>
  Intensity: float32
  /// <summary>
  /// Number of samples to take per pixel for occlusion calculation. More samples = better quality but higher GPU cost.
  /// Typical values: 8 - 32
  /// </summary>
  SampleCount: int
}

module SSAOConfig =
  /// <summary>
  /// Default SSAO settings: 0.5 radius, 1.0 intensity, 16 samples.
  /// </summary>
  let defaults: SSAOConfig = {
    Radius = 0.5f
    Intensity = 1f
    SampleCount = 16
  }

/// <summary>
/// Configuration for Bloom post-process effect.
/// Creates a soft glow around pixels that exceed the threshold brightness.
/// </summary>
[<Struct>]
type BloomConfig = {
  /// <summary>
  /// Minimum brightness level for pixels to contribute to bloom. Pixels below this threshold are not blurred.
  /// Typical values: 0.8f - 1.2f (in linear HDR space)
  /// </summary>
  Threshold: float32
  /// <summary>
  /// Overall brightness of the bloom effect. Higher values make the glow more visible.
  /// Typical values: 0.1f - 1.5f
  /// </summary>
  Intensity: float32
  /// <summary>
  /// How far the bloom effect spreads from bright pixels. Higher values create wider, softer glow.
  /// Typical values: 0.5f - 1.0f
  /// </summary>
  Scatter: float32
}

module BloomConfig =
  /// <summary>
  /// Default bloom settings: 1.0 threshold, 0.5 intensity, 0.7 scatter.
  /// </summary>
  let defaults: BloomConfig = {
    Threshold = 1f
    Intensity = 0.5f
    Scatter = 0.7f
  }

/// <summary>
/// Tone mapping algorithms for HDR -> LDR conversion.
/// Controls how high dynamic range values are compressed to fit within displayable range.
/// </summary>
type ToneMappingConfig =
  /// <summary>
  /// No tone mapping applied. Values may clip at white.
  /// </summary>
  | NoToneMapping
  /// <summary>
  /// Reinhard tone mapping. Simple, classic algorithm with good contrast rolloff.
  /// </summary>
  | Reinhard
  /// <summary>
  /// Academy Color Encoding System (ACES). Industry standard for film and games, excellent color reproduction.
  /// </summary>
  | ACES
  /// <summary>
  /// Filmic tone mapping. Cinematic look with good highlight rolloff and shadow detail.
  /// </summary>
  | Filmic
  /// <summary>
  /// AgX tone mapping. Modern film-inspired transform with pleasing color grading.
  /// </summary>
  | AgX

/// <summary>
/// Configuration for the Post-Processing pipeline phase.
/// </summary>
[<Struct>]
type PostProcessConfig = {
  SSAO: SSAOConfig voption
  Bloom: BloomConfig voption
  ToneMapping: ToneMappingConfig
}

module PostProcessConfig =
  /// <summary>
  /// Default post-process configuration: No SSAO, No Bloom, ACES tone mapping.
  /// </summary>
  let defaults: PostProcessConfig = {
    SSAO = ValueNone
    Bloom = ValueNone
    ToneMapping = ToneMappingConfig.ACES
  }

  /// <summary>
  /// Enables Screen Space Ambient Occlusion (SSAO).
  /// Adds depth perception by darkening corners and crevices, at the cost of a full-screen sampling pass.
  /// </summary>
  let withSSAO (cfg: SSAOConfig) (pp: PostProcessConfig) = {
    pp with
        SSAO = ValueSome cfg
  }

  /// <summary>
  /// Enables Bloom for glowing high-light areas.
  /// Creates a soft glow around pixels that exceed the threshold brightness.
  /// </summary>
  let withBloom (cfg: BloomConfig) (pp: PostProcessConfig) = {
    pp with
        Bloom = ValueSome cfg
  }

  /// <summary>
  /// Sets the tone mapping algorithm used to map HDR color values to the screen's limited range.
  /// Different algorithms produce different visual styles (e.g., Filmic is cinematic, ACES is standard for games).
  /// </summary>
  let withToneMapping (tm: ToneMappingConfig) (pp: PostProcessConfig) = {
    pp with
        ToneMapping = tm
  }

/// <summary>
/// Main configuration for the 3D Render Pipeline.
/// </summary>
[<System.Obsolete("Use Mibo.Rendering.Graphics3D.V2.Pipeline3DConfig instead.")>]
[<Struct>]
type PipelineConfig = {
  /// Shadow subsystem configuration (ValueNone to disable shadows).
  Shadows: ShadowConfig voption
  /// Post-processing configuration.
  PostProcess: PostProcessConfig voption
  /// Fallback lighting state if no 'SetLighting' command is issued.
  DefaultLighting: LightingState voption
  /// Map of shader overrides (e.g., replacing the default PBR shader).
  ShaderOverrides: Map<ShaderBase, string>
  /// Optional callback to run custom logic before the main render pass (e.g. Depth Pre-Pass, Compute Shaders)
  PreRenderCallback: (GraphicsDevice -> Camera -> LightingState -> unit) voption
  /// Optional override for binding light data to shaders
  LightingBinder: (Effect -> Camera -> LightingState -> unit) voption
  /// <summary>
  /// Screen-space tile size for CPU Tiled Forward culling (default: 32).
  /// Controls the granularity of the light binning grid.
  /// Smaller tiles (16) mean fewer lights per tile but more CPU overhead to calculate bins.
  /// Larger tiles (32-64) reduce CPU cost but may pass more lights to the pixel shader for non-uniform scenes.
  /// </summary>
  TileSize: int
}

[<System.Obsolete("Use Mibo.Rendering.Graphics3D.V2.Pipeline3DConfig instead.")>]
module PipelineConfig =
  /// <summary>
  /// Default pipeline configuration: No shadows, no post-process, no default lighting.
  /// </summary>
  let defaults: PipelineConfig = {
    Shadows = ValueNone
    PostProcess = ValueNone
    DefaultLighting = ValueNone
    ShaderOverrides = Map.empty
    PreRenderCallback = ValueNone
    LightingBinder = ValueNone
    TileSize = 32
  }


  /// <summary>
  /// Enables and configures the shadow rendering subsystem.
  /// Without this, no shadows will be rendered regardless of light settings.
  /// </summary>
  let withShadows (cfg: ShadowConfig) (pc: PipelineConfig) = {
    pc with
        Shadows = ValueSome cfg
  }

  /// <summary>
  /// Enables and configures the post-processing pipeline phase.
  /// </summary>
  let withPostProcess (cfg: PostProcessConfig) (pc: PipelineConfig) = {
    pc with
        PostProcess = ValueSome cfg
  }

  /// <summary>
  /// Provides a fallback lighting configuration.
  /// This lighting state is used for any frame where the view function does not explicitly submit a 'SetLighting' command.
  /// </summary>
  let withDefaultLighting (lighting: LightingState) (pc: PipelineConfig) = {
    pc with
        DefaultLighting = ValueSome lighting
  }

  /// <summary>
  /// Registers a custom shader asset to override a default internal shader (e.g., replacing 'PBRForward' with your own implementation).
  /// </summary>
  let withShader
    (shaderBase: ShaderBase)
    (assetName: string)
    (pc: PipelineConfig)
    =
    {
      pc with
          ShaderOverrides = pc.ShaderOverrides.Add(shaderBase, assetName)
    }

  /// <summary>
  /// [Advanced] Overrides the internal logic for binding lighting data to shaders.
  /// Use this only if you are using completely custom shaders and need to manually map Mibo's lighting structures to your effect parameters.
  /// </summary>
  let withLightingBinder
    (binder: Effect -> Camera -> LightingState -> unit)
    (pc: PipelineConfig)
    =
    {
      pc with
          LightingBinder = ValueSome binder
    }

  /// <summary>
  /// [Advanced] Registers a callback to execute before the main render pass.
  /// Useful for global effect updates, compute shader dispatch, or custom depth pre-passes.
  /// </summary>
  let withPreRenderCallback
    (cb: GraphicsDevice -> Camera -> LightingState -> unit)
    (pc: PipelineConfig)
    =
    {
      pc with
          PreRenderCallback = ValueSome cb
    }

  /// <summary>
  /// Tunable performance setting for CPU Tiled Forward culling.
  /// 16: Tighter culling, higher CPU cost. Good for many small local lights.
  /// 32: Balanced default.
  /// 64: Faster CPU, looser culling. Good for fewer, larger lights.
  /// </summary>
  let withTileSize (size: int) (pc: PipelineConfig) = {
    pc with
        TileSize = size
  }
