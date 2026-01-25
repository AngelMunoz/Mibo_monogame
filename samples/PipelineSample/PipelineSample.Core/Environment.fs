module PipelineSample.Core.Environment

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open PipelineSample.Core

/// Render dynamic environmental lights (moving points)
let viewDynamicLights (state: State) (buffer: PipelineBuffer<RenderCommand>) =
  // Add multiple colorful moving point lights using AddLight
  // This shows how easily modules can contribute to the light aggregate
  for i in 0..5 do
    let angle = (float32 i / 6.0f) * MathHelper.TwoPi + state.Time
    let radius = 10.0f
    let x = cos(angle) * radius
    let z = sin(angle) * radius

    let color =
      match i % 3 with
      | 0 -> Color.Red
      | 1 -> Color.Cyan
      | _ -> Color.LimeGreen

    buffer.AddLight(
      Light.Point {
        Position = Vector3(x, 3f, z)
        Color = color
        Intensity = 1.2f
        Range = 10.0f
        Shadow = ValueSome ShadowSettings.defaults
        SourceRadius = 0.1f
      }
    )
    |> ignore

/// Render the platforms and their static spotlights
let viewPlatforms (state: State) (buffer: PipelineBuffer<RenderCommand>) =
  let spotColors = [| Color.Orange; Color.DeepPink |]

  for i in 0 .. state.Platforms.Length - 1 do
    let plat = state.Platforms.[i]

    // 1. Draw Mesh
    buffer.Draw(
      draw {
        mesh state.Assets.PlatformMesh
        at plat.Position
      }
    )
    |> ignore

    // 2. Add a static spotlight above each platform
    let color = spotColors.[i % spotColors.Length]

    buffer.AddLight(
      Light.Spot {
        Position = plat.Position + Vector3(0f, 8f, 0f)
        Direction = Vector3.Down
        Color = color
        Intensity = 1.5f
        Range = 20.0f
        InnerConeAngle = MathHelper.ToRadians(15f)
        OuterConeAngle = MathHelper.ToRadians(35f)
        Shadow = ValueSome ShadowSettings.defaults
        SourceRadius = 0.2f
      }
    )
    |> ignore
