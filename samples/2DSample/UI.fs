module MiboSample.UI

open Microsoft.Xna.Framework
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open MiboSample.Domain
open MiboSample.Terrain

/// Render game UI and debug information
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let uiFont = Assets.font "Fonts/monogram" ctx
  let viewport = ctx.GraphicsDevice.Viewport

  let debugText =
    $"CamX: {model.CameraX:F0} PlayerX: {model.PlayerPosition.X:F0} Grounded: {model.IsGrounded}"

  // Debug line: basic stats
  buffer.Text(
    text {
      font uiFont
      content debugText
      at 10.0f 10.0f
      color Color.Yellow
      layer 100<RenderLayer>
    }
  )
  |> ignore

  // Position display
  buffer.Text(
    text {
      font uiFont
      content
        $"Position: ({int model.PlayerPosition.X}, {int model.PlayerPosition.Y})"
      at 10.0f 30.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  // Chunk display
  buffer.Text(
    text {
      font uiFont
      content $"Chunk: {Terrain.worldXToChunkX model.PlayerPosition.X}"
      at 10.0f 50.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  // Tile count display
  buffer.Text(
    text {
      font uiFont
      content $"Tiles: {model.Tiles.Length}"
      at 10.0f 70.0f
      color Color.White
      layer 100<RenderLayer>
    }
  )
  |> ignore

  // Controls help
  buffer.Text(
    text {
      font uiFont
      content "Controls: A/D or Arrow Keys to move, Space to jump, R to respawn"
      at 10.0f (float32 viewport.Height - 30.0f)
      color Color.Yellow
      layer 100<RenderLayer>
    }
  )
  |> ignore
