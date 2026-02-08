module MiboSample.Particles

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open Mibo.Rendering
open MiboSample.Domain

// ─────────────────────────────────────────────────────────────
// Particle Logic
// ─────────────────────────────────────────────────────────────

/// Spawns a burst of particles at the given position
let spawnJumpParticles (pos: Vector2) (particles: Particle list) =
  let rand = Random.Shared

  let newParticles = [
    for _ in 1..12 do
      // Random upward velocity with horizontal spread
      let vx = (float32(rand.NextDouble()) - 0.5f) * 150.0f
      let vy = float32(rand.NextDouble()) * -100.0f - 50.0f
      let life = 0.4f + float32(rand.NextDouble()) * 0.4f

      {
        Position = pos
        Velocity = Vector2(vx, vy)
        Lifetime = life
        MaxLifetime = life
        Color = Color.Yellow
        Size = Vector2(8.0f, 8.0f)
        Rotation = float32(rand.NextDouble()) * MathHelper.TwoPi
      }
  ]

  newParticles @ particles

/// Updates particle positions and fades them out
let update (dt: float32) (model: Model) : Particle list =
  // 1. Process existing particles
  let updated =
    model.Particles
    |> List.choose(fun p ->
      let newLife = p.Lifetime - dt

      if newLife <= 0.0f then
        None
      else
        Some {
          p with
              Position = p.Position + p.Velocity * dt
              Velocity = p.Velocity + Vector2(0.0f, 400.0f) * dt // Gravity
              Lifetime = newLife
              Rotation = 0f
        })

  // 2. Spawn new particles on jump event
  if not model.IsGrounded then
    // Spawn at player feet
    spawnJumpParticles model.PlayerPosition updated
  else
    updated

// ─────────────────────────────────────────────────────────────
// Particle View
// ─────────────────────────────────────────────────────────────

/// Render active particles using the high-performance batcher
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  if model.Particles.IsEmpty then
    ()
  else
    // Use full UV coordinates for the 1x1 white texture
    let whiteUv = {
      U0 = 0.0f
      V0 = 0.0f
      U1 = 1.0f
      V1 = 1.0f
    }

    let pStates =
      model.Particles
      |> List.map(fun p -> {
        Position = p.Position
        Size = p.Size
        Rotation = p.Rotation
        Color = p.Color * (p.Lifetime / p.MaxLifetime)
        Uv = whiteUv
      })
      |> List.toArray

    // Render the particles using the cached effect
    buffer.Particles(
      model.TerrainAssets.WhiteTexture,
      model.TerrainAssets.ParticleEffect,
      pStates,
      pStates.Length,
      3<RenderLayer>
    )
    |> ignore
