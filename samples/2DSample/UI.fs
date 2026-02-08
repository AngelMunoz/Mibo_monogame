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
      content $"Chunk: {Helpers.worldXToChunkX model.PlayerPosition.X}"
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
      content $"Tiles: {TerrainStats.totalTiles model.Map.Chunks} | Platforms: {model.Platforms.Length}"
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

  // ─────────────────────────────────────────────────────────────
  // Visual Layer Sorting Test
  // ─────────────────────────────────────────────────────────────
  // We explicitly submit these in REVERSE order of their desired depth.
  // If sorting is working: Red (110) -> Green (111) -> Blue (112).
  // Result: Blue Square is on top.
  // If sorting is broken: Blue (Submitted 1st) -> Green -> Red (Submitted 3rd).
  // Result: Red Square is on top.

  let whiteTex = model.TerrainAssets.WhiteTexture
  let testX = float32 viewport.Width - 100.0f
  let testY = 50.0f
  let size_ = 40.0f

  // 1. Submit Blue (Layer 112 - Top) FIRST
  buffer.Sprite(
    sprite {
      texture whiteTex
      at (testX + 20.0f) (testY + 20.0f)
      size size_ size_
      color Color.Blue
      layer 112<RenderLayer>
    }
  )
  |> ignore

  // 2. Submit Green (Layer 111 - Middle) SECOND
  buffer.Sprite(
    sprite {
      texture whiteTex
      at (testX + 10.0f) (testY + 10.0f)
      size size_ size_
      color Color.Green
      layer 111<RenderLayer>
    }
  )
  |> ignore

  // 3. Submit Red (Layer 110 - Bottom) THIRD
  buffer.Sprite(
    sprite {
      texture whiteTex
      at testX testY
      size size_ size_
      color Color.Red
      layer 110<RenderLayer>
    }
  )
  |> ignore

  // Label
  buffer.Text(
    text {
      font uiFont
      content "Layer Test (Blue on Top)"
      at (testX - 60.0f) (testY + 70.0f)
      color Color.White
      layer 113<RenderLayer>
    }
  )
  |> ignore
