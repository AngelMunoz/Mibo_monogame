module _3DSample.Materials.PBRBinding

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open _3DSample.Materials.Types

// ============================================================================
// PBR Material Shader Binding
// ============================================================================
// Binds a PBRMaterial to its shader effect.
// Call `Apply` before drawing geometry with this material.

/// <summary>Applies PBR material parameters to the effect.</summary>
let apply (effect: Effect) (mp: MaterialParams) (mat: PBRMaterial) =
  effect.Parameters.["World"].SetValue(mp.World)
  effect.Parameters.["View"].SetValue(mp.View)
  effect.Parameters.["Projection"].SetValue(mp.Projection)

  // Albedo
  effect.Parameters.["AlbedoColor"].SetValue(mat.AlbedoColor)

  // PBR properties
  effect.Parameters.["Metallic"].SetValue(mat.Metallic)
  effect.Parameters.["Roughness"].SetValue(mat.Roughness)

  // Emissive
  effect.Parameters.["EmissiveColor"].SetValue(mat.EmissiveColor)
  effect.Parameters.["EmissiveIntensity"].SetValue(mat.EmissiveIntensity)

  // Lighting
  effect.Parameters.["LightDirection"].SetValue(mat.LightDirection)
  effect.Parameters.["LightColor"].SetValue(mat.LightColor)
  effect.Parameters.["LightIntensity"].SetValue(mat.LightIntensity)
  effect.Parameters.["AmbientColor"].SetValue(mat.AmbientColor)

  effect.CurrentTechnique.Passes.[0].Apply()
