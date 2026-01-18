namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework

// ============================================================================
// Lighting System
// ============================================================================

/// Shadow settings for a light
[<Struct>]
type ShadowSettings = { Bias: float32; NormalBias: float32 }

module ShadowSettings =
  let defaults: ShadowSettings = { Bias = 0.001f; NormalBias = 0.02f }

/// Directional light (e.g., sun)
[<Struct>]
type DirectionalLight = {
  Direction: Vector3
  Color: Color
  Intensity: float32
  Shadow: ShadowSettings voption
  CascadeCount: int
  CascadeSplits: float32[]
  SourceRadius: float32
}

/// Point light (omni-directional)
[<Struct>]
type PointLight = {
  Position: Vector3
  Color: Color
  Intensity: float32
  Range: float32
  Shadow: ShadowSettings voption
  SourceRadius: float32
}

/// Spot light (cone)
[<Struct>]
type SpotLight = {
  Position: Vector3
  Direction: Vector3
  Color: Color
  Intensity: float32
  Range: float32
  InnerConeAngle: float32
  OuterConeAngle: float32
  Shadow: ShadowSettings voption
  SourceRadius: float32
}

/// Light type union
type Light =
  | Directional of DirectionalLight
  | Point of PointLight
  | Spot of SpotLight

/// Scene lighting state
[<Struct>]
type LightingState = {
  AmbientColor: Color
  AmbientIntensity: float32
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
