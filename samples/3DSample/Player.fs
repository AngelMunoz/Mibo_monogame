module _3DSample.Player

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics3D
open _3DSample

// ─────────────────────────────────────────────────────────────
// Player System: Respawn and rendering
// ─────────────────────────────────────────────────────────────

/// Check if player has fallen and respawn if needed
let checkRespawn<'Msg>(state: State) : struct (State * Cmd<'Msg>) =
  if state.PlayerPosition.Y <= Constants.fallLimit then
    {
      state with
          PlayerPosition = Vector3(24f, 2f, 24f)
          Velocity = Vector3.Zero
          IsGrounded = false
    },
    Cmd.none
  else
    state, Cmd.none

/// Calculate the offset needed to align ball model center with physics position
/// Physics treats PlayerPosition as the ball's center point
/// Model bounds might have a different origin
let private getModelOffset(bounds: BoundingBox) : Vector3 =
  // Calculate model center in local space
  let modelCenter = (bounds.Min + bounds.Max) / 2f
  // Negate to offset the model so its center aligns with position
  -modelCenter

/// Render the player ball with rotation
let view
  (_ctx: GameContext)
  (state: State)
  (buffer: RenderBuffer<RenderCmd3D>)
  : unit =
  let rotationMatrix = Matrix.CreateFromQuaternion state.Rotation

  // Calculate offset to align model's visual center with physics position
  let modelOffset = getModelOffset state.Assets.PlayerBounds

  let playerMatrix =
    rotationMatrix
    * Matrix.CreateTranslation(modelOffset)
    * Matrix.CreateTranslation(state.PlayerPosition)

  Draw3D.mesh state.Assets.PlayerModel playerMatrix |> Draw3D.submit buffer
