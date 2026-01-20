module PipelineSample.Core.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open PipelineSample.Core

// ─────────────────────────────────────────────────────────────
// Player System: Respawn and rendering
// ─────────────────────────────────────────────────────────────

/// Check if player has fallen and respawn if needed
let checkRespawn<'Msg>(state: State) : struct (State * Cmd<'Msg>) =
  if state.PlayerPosition.Y <= Constants.fallLimit then
    {
      state with
          PlayerPosition = Vector3(0f, 2f, 0f)
          Velocity = Vector3.Zero
          IsGrounded = false
    },
    Cmd.none
  else
    state, Cmd.none

/// Render the player and their associated dynamic light
let view (state: State) (buffer: PipelineBuffer<RenderCommand>) =
  // 1. Add a dynamic "Torch" light attached to the player
  // This demonstrates the new AddLight API - it appends to the scene lights
  // without overriding the sun or other environment lights.
  buffer.AddLight(
    Light.Point {
      Position = state.PlayerPosition + Vector3.Up * 1.5f
      Color = Color.LightGoldenrodYellow
      Intensity = 2.0f
      Range = 12.0f
      Shadow = ValueSome ShadowSettings.defaults
      SourceRadius = 0.1f
    }
  ) |> ignore

  // 2. Draw the player mesh
  buffer.Draw(
    draw {
      mesh state.Assets.PlayerMesh
      at state.PlayerPosition
      rotatedBy state.Rotation
      withAlbedo Color.White
      withEmissive Color.Magenta state.EmissivePulse
    }
  ) |> ignore

  // 3. Draw player particles (sparks)
  let sparkCount = 8

  for i in 0 .. sparkCount - 1 do
    let angle =
      float32 i / float32 sparkCount * MathHelper.TwoPi + state.Time * 2.0f

    let offset =
      Vector3(cos angle, sin(state.Time * 5.0f + float32 i), sin angle) * 1.5f

    buffer.Billboard(
      state.Assets.PlatformTexture,
      billboard {
        at(state.PlayerPosition + offset)
        size(Vector2(0.2f, 0.2f))
        color Color.Yellow
      }
    )
    |> ignore

  // 4. Draw Player UI/Debug elements
  buffer
    .QuadTransparent(
      state.Assets.PlatformTexture,
      quad {
        onXZ(Vector2(2.0f, 2.0f))
        relativeTo(Matrix.CreateTranslation(state.PlayerPosition))
        at(Vector3(0f, -0.48f, 0f))
        color(Color.White * 0.3f)
      }
    )
    .Line(
      state.PlayerPosition,
      state.PlayerPosition + state.Velocity * 0.5f,
      Color.Green
    )
    |> ignore
