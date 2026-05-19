module _3DSample.Materials.UnlitBinding

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open _3DSample.Materials.Types

// ============================================================================
// Unlit Material Shader Binding
// ============================================================================
// Binds an UnlitMaterial to its shader effect.
// Call `Apply` before drawing geometry with this material.

/// <summary>Applies unlit material parameters to the effect.</summary>
let apply (effect: Effect) (mp: MaterialParams) (mat: UnlitMaterial) =
  effect.Parameters.["World"].SetValue(mp.World)
  effect.Parameters.["View"].SetValue(mp.View)
  effect.Parameters.["Projection"].SetValue(mp.Projection)
  effect.Parameters.["AlbedoColor"].SetValue(mat.AlbedoColor)
  effect.Parameters.["Intensity"].SetValue(mat.Intensity)

  match mat.AlbedoTexture with
  | Some tex ->
    effect.Parameters.["HasAlbedoMap"].SetValue(1.0f)
    effect.Parameters.["AlbedoMap"].SetValue(tex)
  | None -> effect.Parameters.["HasAlbedoMap"].SetValue(0.0f)

  effect.CurrentTechnique.Passes.[0].Apply()
