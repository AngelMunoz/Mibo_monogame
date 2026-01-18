namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework

// ============================================================================
// Lighting System
// ============================================================================

/// <summary>
/// Shadow settings for a light.
/// </summary>
[<Struct>]
type ShadowSettings = { 
    /// <summary>
    /// Constant depth bias to prevent shadow acne.
    /// </summary>
    Bias: float32
    /// <summary>
    /// Bias applied along the surface normal to prevent acne on curved surfaces.
    /// </summary>
    NormalBias: float32 
}

module ShadowSettings =
  /// <summary>
  /// Default shadow settings: 0.001 bias, 0.02 normal bias.
  /// Good starting point for most scenes, adjust if you see acne or detached shadows.
  /// </summary>
  let defaults: ShadowSettings = { Bias = 0.001f; NormalBias = 0.02f }

/// <summary>
/// Directional light (e.g., sun).
/// Infinite distance, parallel rays. Covers the entire view frustum via cascades.
/// </summary>
[<Struct>]
type DirectionalLight = {
  /// <summary>
  /// Direction light is pointing (should be normalized). Points toward objects.
  /// Example: Vector3(-1f, -1f, -1f) points diagonally down and away.
  /// </summary>
  Direction: Vector3
  /// <summary>
  /// Light color. Multiplied with intensity for final contribution.
  /// Use Color.White for neutral light, or tints for artistic effects.
  /// </summary>
  Color: Color
  /// <summary>
  /// Light intensity (typically 1.0 - 5.0 for sunlight).
  /// Higher values create brighter light; 1.0 = base brightness.
  /// </summary>
  Intensity: float32
  /// <summary>
  /// Shadow settings (ValueNone to disable shadows).
  /// </summary>
  Shadow: ShadowSettings voption
  /// <summary>
  /// Number of shadow cascades (1-4).
  /// </summary>
  CascadeCount: int
  /// <summary>
  /// Manual split distances for cascades (0.0 to 1.0 relative to ViewFrustum far plane).
  /// Example: [| 0.05f; 0.15f; 0.5f; 1.0f |]
  /// Adjust these to concentrate resolution near the camera.
  /// </summary>
  CascadeSplits: float32[]
  /// <summary>
  /// Physical radius of the light source (angular diameter in radians approx).
  /// Used by PCSS algorithms to calculate penumbra softness.
  /// </summary>
  SourceRadius: float32
}

/// <summary>
/// Point light (omni-directional).
/// Falls off with distance.
/// </summary>
[<Struct>]
type PointLight = {
  /// <summary>
  /// World position of the light center.
  /// Light attenuation is calculated from this point.
  /// </summary>
  Position: Vector3
  /// <summary>
  /// Light color. Multiplied with intensity for final contribution.
  /// Use warm colors (orange, yellow) for firelight, cool colors for moonlight.
  /// </summary>
  Color: Color
  /// <summary>
  /// Peak intensity at the source. Light attenuates toward zero at Range distance.
  /// Higher values = brighter light closer to source.
  /// </summary>
  Intensity: float32
  /// <summary>
  /// Maximum range of influence. Light attenuation falls to zero at this distance.
  /// Objects beyond this distance receive no contribution from this light.
  /// </summary>
  Range: float32
  /// <summary>
  /// Shadow settings. Point lights use CubeMap shadows (6 faces).
  /// Expensive! Use sparingly or with low atlas resolution.
  /// </summary>
  Shadow: ShadowSettings voption
  /// <summary>
  /// Physical radius of the light bulb/sphere.
  /// Used for soft shadow calculations.
  /// </summary>
  SourceRadius: float32
}

/// <summary>
/// Spot light (conical).
/// Directional point light with inner/outer cone falloff.
/// </summary>
[<Struct>]
type SpotLight = {
  /// <summary>
  /// World position of light source (tip of the cone).
  /// Light direction and attenuation calculated from this point.
  /// </summary>
  Position: Vector3
  /// <summary>
  /// Direction the spot is pointing (should be normalized).
  /// Points along the cone's central axis.
  /// </summary>
  Direction: Vector3
  /// <summary>
  /// Light color. Multiplied with intensity for final contribution.
  /// Tinting can create mood effects (e.g., red for emergency lights).
  /// </summary>
  Color: Color
  /// <summary>
  /// Peak intensity at the light source.
  /// Light attenuates toward zero at Range distance.
  /// Higher values = brighter, more concentrated light.
  /// </summary>
  Intensity: float32
  /// <summary>
  /// Maximum range of influence. Light attenuation falls to zero at this distance.
  /// Defines how far the spotlight cone extends.
  /// </summary>
  Range: float32
  /// <summary>
  /// Inner cone angle (radians). Full intensity within this angle.
  /// Creates a bright, unblurred center region (hotspot).
  /// Use MathHelper.ToRadians(degrees) for degree values.
  /// </summary>
  InnerConeAngle: float32
  /// <summary>
  /// Outer cone angle (radians). Intensity fades to zero between Inner and Outer angles.
  /// Defines the full extent of the cone (penumbra).
  /// Should be larger than InnerConeAngle.
  /// </summary>
  OuterConeAngle: float32
  /// <summary>
  /// Shadow settings. Spot lights use standard 2D shadow maps (perspective projection).
  /// </summary>
  Shadow: ShadowSettings voption
  /// <summary>
  /// Physical radius of the light source.
  /// </summary>
  SourceRadius: float32
}

/// <summary>
/// Discriminated union of all supported light types.
/// </summary>
type Light =
  | Directional of DirectionalLight
  | Point of PointLight
  | Spot of SpotLight

/// <summary>
/// The complete lighting state for a scene.
/// Passed to the renderer via 'SetLighting' command.
/// </summary>
[<Struct>]
type LightingState = {
  /// <summary>
  /// Global ambient color added to all surfaces regardless of light sources.
  /// Use low, desaturated colors (e.g., RGB(30,30,35)) for realistic ambient.
  /// </summary>
  AmbientColor: Color
  /// <summary>
  /// Intensity multiplier for ambient color.
  /// Higher values make ambient light stronger (can wash out scenes).
  /// Typical range: 0.1f - 1.0f
  /// </summary>
  AmbientIntensity: float32
  /// <summary>
  /// Array of active lights in scene.
  /// The renderer will cull lights outside camera frustum and sort for tiled forward rendering.
  /// Maximum 31 lights supported per frame (due to tiled culling bitmask limitations).
  /// </summary>
  Lights: Light[]
}

module Lighting =
  /// Minimal ambient-only lighting
  let ambient: LightingState = {
    AmbientColor = Color(50, 50, 55)
    AmbientIntensity = 1f
    Lights = [||]
  }

  /// Default sunlight preset
  let defaultSunlight: LightingState = {
    AmbientColor = Color(50, 50, 55)
    AmbientIntensity = 1f
    Lights = [|
      Light.Directional {
        Direction = Vector3.Normalize(Vector3(-0.2f, -1f, -0.2f))
        Color = Color.White
        Intensity = 0.8f
        Shadow = ValueSome ShadowSettings.defaults
        CascadeCount = 3
        CascadeSplits = [| 0.1f; 0.3f; 1f |]
        SourceRadius = 0.05f
      }
    |]
  }

  /// Create lighting with single directional light
  let withDirectional
    (dir: Vector3)
    (color: Color)
    (intensity: float32)
    (ambient: Color)
    : LightingState =
    {
      AmbientColor = ambient
      AmbientIntensity = 1f
      Lights = [|
        Light.Directional {
          Direction = Vector3.Normalize(dir)
          Color = color
          Intensity = intensity
          Shadow = ValueNone
          CascadeCount = 0
          CascadeSplits = [||]
          SourceRadius = 0.0f
        }
      |]
    }
