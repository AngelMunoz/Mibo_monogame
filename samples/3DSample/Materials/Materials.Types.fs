module _3DSample.Materials.Types

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Material Types
// ============================================================================
// A material owns shader binding logic. The command processor calls
// `Apply` before drawing, and the material sets all shader parameters.

/// <summary>Common parameters shared across all materials.</summary>
[<Struct>]
type MaterialParams = {
  World: Matrix
  View: Matrix
  Projection: Matrix
}

/// <summary>Unlit material: albedo color/texture, no lighting.</summary>
[<Struct>]
type UnlitMaterial = {
  AlbedoColor: Vector4
  AlbedoTexture: Texture2D option
  Intensity: float32
}

/// <summary>PBR material: albedo, metallic/roughness, emissive.</summary>
[<Struct>]
type PBRMaterial = {
  AlbedoColor: Vector4
  Metallic: float32
  Roughness: float32
  EmissiveColor: Vector4
  EmissiveIntensity: float32
  LightDirection: Vector3
  LightColor: Vector3
  LightIntensity: float32
  AmbientColor: Vector3
}

/// <summary>Material union - the single source of truth for shader binding.</summary>
/// <remarks>Add new material types here as your game grows.</remarks>
type Material =
  | Unlit of UnlitMaterial
  | PBR of PBRMaterial
