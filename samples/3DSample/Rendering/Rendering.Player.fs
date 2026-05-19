module _3DSample.Rendering.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering.Graphics3D
open _3DSample.Domain
open _3DSample.Rendering.Commands

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

/// <summary>Render the player ball with rotation.</summary>
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

  buffer.AddCmd(DrawMesh(state.Assets.PlayerModel, playerMatrix))
