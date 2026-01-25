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
            if not (isNull p) then p.SetValue(value)
        member this.SafeSet(name: string, value: float32) =
            let p = this.Parameters.[name]
            if not (isNull p) then p.SetValue(value)
        member this.SafeSet(name: string, value: Vector2) =
            let p = this.Parameters.[name]
            if not (isNull p) then p.SetValue(value)
        member this.SafeSet(name: string, value: Matrix) =
            let p = this.Parameters.[name]
            if not (isNull p) then p.SetValue(value)

let view (ctx: Mibo.Elmish.GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
    let time = model.DayNight.TimeOfDay
    let struct (top, bot, stars) = DayNight.getSkyColors time
    let vp = ctx.GraphicsDevice.Viewport
    let w = float32 vp.Width
    let h = float32 vp.Height
    
    // 1. Draw Sky Gradient & Stars (Layer -100)
    let drawSky (g: Mibo.Elmish.GameContext) =
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
                VertexPositionTexture(Vector3(1.0f, 1.0f, 0.0f), Vector2(1.0f, 0.0f))  // Top-Right
                VertexPositionTexture(Vector3(-1.0f, -1.0f, 0.0f), Vector2(0.0f, 1.0f)) // Bottom-Left
                VertexPositionTexture(Vector3(1.0f, -1.0f, 0.0f), Vector2(1.0f, 1.0f)) // Bottom-Right
            |]
            
            g.GraphicsDevice.BlendState <- BlendState.Opaque
            g.GraphicsDevice.SamplerStates.[0] <- SamplerState.LinearClamp
            
            for pass in fx.CurrentTechnique.Passes do
                pass.Apply()
                g.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2)
        else
            // Fallback clear
            g.GraphicsDevice.Clear(bot)
        
    buffer.Add(-100<RenderLayer>, DrawCustom drawSky)

    // 2. Celestial Bodies Position
    // Use Camera position as center to make them feel infinitely far (parallax)
    // But we want them to move across the sky.
    // Fixed orbit relative to camera center is good.
    let center = Vector2(model.CameraX + w/2.0f, 300.0f) 
    
    let radius = Vector2(w * 0.6f, h * 0.7f)
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
            |> AnimatedSprite.withScale 2.0f // Make it big
            
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
    
    // Sun Light
    if sunColor.A > 0uy then
        buffer.PointLight {
            Position = sunPos
            Color = sunColor
            Intensity = 1.2f
            Radius = 2500.0f // Cover entire screen effectively
            Falloff = 0.5f
            Shadow = ValueSome { Bias = ValueNone }
        } |> ignore
        
    // Moon Light
    if moonColor.A > 0uy then
        buffer.PointLight {
            Position = moonPos
            Color = Color.LightBlue
            Intensity = 0.6f
            Radius = 2500.0f
            Falloff = 0.5f
            Shadow = ValueSome { Bias = ValueNone }
        } |> ignore