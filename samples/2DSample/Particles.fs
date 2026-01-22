module MiboSample.Particles

open System
open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open MiboSample.Domain

// Particles System: MutableSystem<Model, 'Msg>

/// Age particle and return updated or None if expired
let private ageParticle (dt: float32) (p: Particle) : Particle voption =
  let newLife = p.Life - dt

  if newLife <= 0.0f then
    ValueNone
  else
    ValueSome {
      p with
          Position = p.Position + p.Velocity * dt
          Life = newLife
          Color =
            Color.Lerp(Color.Transparent, Color.Yellow, newLife / p.MaxLife)
    }

/// Emit particles at position (imperative for performance)
let emit (pos: Vector2) (count: int) (model: Model) : unit =
  for _ = 0 to count - 1 do
    model.Particles.Add(ParticleFactory.createAt pos)

/// MutableSystem: ages particles, removes expired
let update<'Msg> (dt: float32) (model: Model) : struct (Model * Cmd<'Msg>) =
  let particles = model.Particles
  let mutable i = particles.Count - 1

  while i >= 0 do
    match ageParticle dt particles[i] with
    | ValueNone -> particles.RemoveAt(i)
    | ValueSome updated -> particles[i] <- updated

    i <- i - 1

  struct (model, Cmd.none)

// ─────────────────────────────────────────────────────────────
// View: Render particles using the new DSL
// ─────────────────────────────────────────────────────────────

let view
  (ctx: GameContext)
  (particles: ResizeArray<Particle>)
  (buffer: RenderBuffer<RenderCmd2D>)
  =
  let tex =
    ctx
    |> Assets.getOrCreate<Texture2D> "pixel" (fun gd ->
      let t = new Texture2D(gd, 1, 1)
      t.SetData([| Color.White |])
      t)

  for i = 0 to particles.Count - 1 do
    let p = particles[i]
    // Using the new sprite computation expression
    // Submit using the fluent extension
    buffer
      .Sprite(
        sprite {
          texture tex
          at p.Position
          size 2 2
          color p.Color
          layer 5<RenderLayer>
        }
      )
      .Submit()
