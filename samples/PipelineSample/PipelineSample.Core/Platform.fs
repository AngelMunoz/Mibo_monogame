module PipelineSample.Core.Platform

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering.Graphics3D
open PipelineSample.Core

// ─────────────────────────────────────────────────────────────
// Platform System: Bounds computation and collision detection
// ─────────────────────────────────────────────────────────────

/// Bounds are already computed in Mesh, just return it
let computeBounds(mesh: Mesh) : BoundingBox = mesh.BoundingBox

/// Create platform at position with bounds offset from base bounds
let create (baseBounds: BoundingBox) (pos: Vector3) : PlatformData =
  let min = baseBounds.Min + pos
  let max = baseBounds.Max + pos

  {
    Position = pos
    Bounds = BoundingBox(min, max)
  }

/// Check if player collides with any platform (from above, landing)
/// Returns Some(topY) if landing on a platform, None otherwise
let checkCollision
  (playerRadius: float32)
  (prevPos: Vector3)
  (newPos: Vector3)
  (platforms: PlatformData list)
  : float32 option =
  platforms
  |> List.tryPick(fun plat ->
    let topY = plat.Bounds.Max.Y
    let inX = newPos.X >= plat.Bounds.Min.X && newPos.X <= plat.Bounds.Max.X
    let inZ = newPos.Z >= plat.Bounds.Min.Z && newPos.Z <= plat.Bounds.Max.Z

    if inX && inZ then
      if prevPos.Y >= topY && newPos.Y < topY then
        Some topY
      elif
        newPos.Y >= plat.Bounds.Min.Y && newPos.Y <= topY + playerRadius
      then
        Some topY
      else
        Option.None
    else
      Option.None)

/// Level platform positions
let positions: Vector3 list = [
  Vector3(0f, -1f, 0f)
  Vector3(6f, 0f, 0f)
  Vector3(12f, 1f, 0f)
  Vector3(12f, 2f, -6f)
  Vector3(0f, 1f, -6f)
]
