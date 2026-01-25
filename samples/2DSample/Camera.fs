module MiboSample.Camera

open System
open Microsoft.Xna.Framework
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open MiboSample.Domain

/// Update camera position based on player position
let update(model: Model) : Model =
  let viewportWidth = 1280.0f // Default window width
  let targetCameraX = model.PlayerPosition.X - viewportWidth * 0.3f
  let cameraX = Math.Max(0.0f, targetCameraX)
  { model with CameraX = cameraX }

/// Create camera for world space rendering
let createWorldCamera (ctx: GameContext) (model: Model) =
  let viewport = ctx.GraphicsDevice.Viewport
  let viewportSize = Vector2(float32 viewport.Width, float32 viewport.Height)

  // Camera position is top-left of the viewable area in our model
  let cameraX = model.CameraX

  // Calculate world bottom
  let worldBottom = float32 Constants.worldHeight * Constants.tileSize

  // Anchor camera so the bottom of the viewport aligns with the bottom of the world
  // Center Y = WorldBottom - ViewportHeight / 2
  let centerY = worldBottom - viewportSize.Y * 0.5f

  // Camera2D.create expects the CENTER of the view
  let cameraCenter = Vector2(cameraX + viewportSize.X * 0.5f, centerY)

  Camera2D.create cameraCenter 1.0f (Point(viewport.Width, viewport.Height))

/// Create camera for UI (screen space) rendering
let createUICamera(ctx: GameContext) =
  let viewport = ctx.GraphicsDevice.Viewport
  let viewportSize = Vector2(float32 viewport.Width, float32 viewport.Height)

  Camera2D.create
    (viewportSize * 0.5f)
    1.0f
    (Point(viewport.Width, viewport.Height))
