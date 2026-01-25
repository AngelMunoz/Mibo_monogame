module MiboSample.Sky

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Animation
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open MiboSample.Domain
open MiboSample.DayNight

[<AutoOpen>]
module EffectExtensions =
  type Effect with
    member this.SafeSet(name: string, value: Vector4) =
      let p = this.Parameters.[name]

      if not(isNull p) then
        p.SetValue(value)

    member this.SafeSet(name: string, value: float32) =
      let p = this.Parameters.[name]

      if not(isNull p) then
        p.SetValue(value)

    member this.SafeSet(name: string, value: Vector2) =
      let p = this.Parameters.[name]

      if not(isNull p) then
        p.SetValue(value)

    member this.SafeSet(name: string, value: Matrix) =
      let p = this.Parameters.[name]

      if not(isNull p) then
        p.SetValue(value)

let view
  (ctx: Mibo.Elmish.GameContext)
  (model: Model)
  (buffer: RenderBuffer<RenderCmd2D>)
  =
  let time = model.DayNight.TimeOfDay
  let struct (top, bot, stars) = DayNight.getSkyColors time
  let vp = ctx.GraphicsDevice.Viewport
  let w = float32 vp.Width
  let h = float32 vp.Height

  // 1. Draw Sky Gradient & Stars (Layer -100)
  let drawSky(g: Mibo.Elmish.GameContext) =
    let fx = model.TerrainAssets.SkyEffect

    if fx <> null then
      fx.SafeSet("TopColor", top.ToVector4())
      fx.SafeSet("BottomColor", bot.ToVector4())
      fx.SafeSet("StarIntensity", stars)
      fx.SafeSet("Resolution", Vector2(w, h))
      fx.SafeSet("World", Matrix.Identity)
      fx.SafeSet("View", Matrix.Identity)
      fx.SafeSet("Projection", Matrix.Identity)

      // Full screen quad in Normalized Device Coordinates (-1 to 1)
      let quad = [|
        VertexPositionTexture(Vector3(-1.0f, 1.0f, 0.0f), Vector2(0.0f, 0.0f)) // Top-Left
        VertexPositionTexture(Vector3(1.0f, 1.0f, 0.0f), Vector2(1.0f, 0.0f)) // Top-Right
        VertexPositionTexture(Vector3(-1.0f, -1.0f, 0.0f), Vector2(0.0f, 1.0f)) // Bottom-Left
        VertexPositionTexture(Vector3(1.0f, -1.0f, 0.0f), Vector2(1.0f, 1.0f)) // Bottom-Right
      |]

      g.GraphicsDevice.BlendState <- BlendState.Opaque
      g.GraphicsDevice.SamplerStates.[0] <- SamplerState.LinearClamp

      for pass in fx.CurrentTechnique.Passes do
        pass.Apply()

        g.GraphicsDevice.DrawUserPrimitives(
          PrimitiveType.TriangleStrip,
          quad,
          0,
          2
        )
    else
      // Fallback clear
      g.GraphicsDevice.Clear(bot)

  buffer.Add(-100<RenderLayer>, DrawCustom drawSky)

  // 2. Celestial Bodies Position
  // Use Camera position X for centering, but fixed Y horizon (ground level approx)
  let groundY = 650.0f
  let center = Vector2(model.CameraX + w / 2.0f, groundY)

  // Large orbit radius to make it feel like it's coming from far away
  let radius = Vector2(w * 0.8f, h * 0.8f)
  let sunPos = DayNight.getSunPosition time center radius
  let moonPos = DayNight.getMoonPosition time center radius

  let sunColor = DayNight.getSunColor time
  let moonColor = DayNight.getMoonColor time

  // 3. Draw Bodies (Layer -90) using standard SpriteBatch via DSL
  // Sun
  if sunColor.A > 0uy then
    let sun =
      model.TerrainAssets.SunSprite
      |> AnimatedSprite.withColor sunColor
      |> AnimatedSprite.withScale 2.0f

    AnimatedSprite.draw sunPos -90<RenderLayer> buffer sun

  // Moon
  if moonColor.A > 0uy then
    let moon =
      model.TerrainAssets.MoonSprite
      |> AnimatedSprite.withColor moonColor
      |> AnimatedSprite.withScale 1.5f

    AnimatedSprite.draw moonPos -90<RenderLayer> buffer moon

  // 4. Global Lighting
  // Ambient based on sky
  let ambient = Color.Lerp(bot, Color.Black, 0.4f)
  buffer.AddLighting { Color = ambient } |> ignore

  // Threshold for fading out lights as they approach the horizon
  let horizonThreshold = 500.0f

  // Sun Light (Directional)
  if sunColor.A > 0uy then
    let sunFade =
      MathHelper.Clamp((groundY - sunPos.Y) / horizonThreshold, 0.0f, 1.0f)

    if sunFade > 0.01f then
      let direction = Vector2.Normalize(center - sunPos)

      buffer.DirectionalLight {
        Direction = direction
        Color = sunColor
        Intensity = 1.2f * sunFade
        Shadow = ValueSome { Bias = ValueNone }
      }
      |> ignore

  // Moon Light (Directional)
  if moonColor.A > 0uy then
    let moonFade =
      MathHelper.Clamp((groundY - moonPos.Y) / horizonThreshold, 0.0f, 1.0f)

    if moonFade > 0.01f then
      let direction = Vector2.Normalize(center - moonPos)

      buffer.DirectionalLight {
        Direction = direction
        Color = Color.LightBlue
        Intensity = 0.6f * moonFade
        Shadow = ValueSome { Bias = ValueNone }
      }
      |> ignore
