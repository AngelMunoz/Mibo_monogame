module _3DSample.Rendering.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open _3DSample.Domain
open _3DSample.Rendering.Commands
open _3DSample.Materials.Types

// ============================================================================
// Player Rendering
// ============================================================================
// Renders the player ball model with proper alignment between physics
// position and visual model center.

/// <summary>Calculate the offset needed to align ball model center with physics position.</summary>
/// <remarks>Physics treats PlayerPosition as the ball's center point. Model bounds might have a different origin.</remarks>
let private getModelOffset(bounds: BoundingBox) : Vector3 =
  let modelCenter = (bounds.Min + bounds.Max) / 2f
  -modelCenter

/// <summary>Render the player ball with rotation using PBR material.</summary>
let draw
  (_ctx: GameContext)
  (state: State)
  (buffer: RenderBuffer3D<SampleCmd>)
  : unit =
  let rotationMatrix = Matrix.CreateFromQuaternion state.Rotation

  let modelOffset = getModelOffset state.Assets.PlayerBounds

  let playerMatrix =
    rotationMatrix
    * Matrix.CreateTranslation(modelOffset)
    * Matrix.CreateTranslation(state.PlayerPosition)

  let pbrMat =
    PBR {
      AlbedoColor = Vector4(0.2f, 0.5f, 1.0f, 1.0f)
      Metallic = 0.8f
      Roughness = 0.3f
      EmissiveColor = Vector4.Zero
      EmissiveIntensity = 0.0f
      LightDirection = Vector3.Normalize(Vector3(0.5f, -1.0f, 0.3f))
      LightColor = Vector3.One
      LightIntensity = 1.2f
      AmbientColor = Vector3(0.15f, 0.15f, 0.2f)
    }

  buffer.AddCmd(
    DrawMeshWithMaterial(state.Assets.PbrEffect, pbrMat, state.Assets.PlayerModel, playerMatrix)
  )
