module _3DSample.Level.Build

open Microsoft.Xna.Framework
open Mibo.Layout3D
open _3DSample.Domain
open _3DSample.Level.Stamps

// ============================================================================
// Level Construction
// ============================================================================
// Composes stamps into a complete level grid.

/// <summary>Create a large showcase level demonstrating the Layout3D DSL.</summary>
let create() : CellGrid3D<Cell> =
  let gridWidth = 64
  let gridHeight = 20
  let gridDepth = 64
  let cellSize = Vector3.One
  let origin = Vector3.Zero

  CellGrid3D.create gridWidth gridHeight gridDepth cellSize origin
  |> Layout3D.run(
    // ═══════════════════════════════════════════════════════════
    // Zone 1: Central Plaza (ground floor)
    // ═══════════════════════════════════════════════════════════
    centralPlaza 12 12

    // ═══════════════════════════════════════════════════════════
    // Zone 2: Corner Towers
    // ═══════════════════════════════════════════════════════════
    >> cornerTower 0 0 8
    >> cornerTower 44 0 8
    >> cornerTower 0 44 8
    >> cornerTower 44 44 8

    // ═══════════════════════════════════════════════════════════
    // Zone 3: Elevated Walkways connecting towers
    // ═══════════════════════════════════════════════════════════
    >> connectingBridge 4 2 44 2 8
    >> connectingBridge 2 4 2 44 8
    >> connectingBridge 46 4 46 44 8
    >> connectingBridge 4 46 44 46 8

    // ═══════════════════════════════════════════════════════════
    // Zone 4: Platformer Challenge Areas
    // ═══════════════════════════════════════════════════════════
    >> platformerChallenge 50 8
    >> climbChallenge 8 50
    >> pillarHopping 50 50
    >> platformRing 24 24 10 3
    >> platformRing 24 24 14 5

    // ═══════════════════════════════════════════════════════════
    // Zone 5: Multi-level Structures
    // ═══════════════════════════════════════════════════════════
    >> multiLevelArea 0 16
    >> multiLevelArea 48 16

    // ═══════════════════════════════════════════════════════════
    // Zone 6: Scattered Jump Challenges
    // ═══════════════════════════════════════════════════════════
    >> floatingPlatform 16 3 8
    >> floatingPlatform 20 4 12
    >> floatingPlatform 24 5 8
    >> floatingPlatform 28 6 12
    >> floatingPlatform 32 7 8
    >> floatingPlatform 8 3 24
    >> floatingPlatform 12 4 28
    >> floatingPlatform 8 5 32
    >> floatingPlatform 12 6 36
    >> floatingPlatform 36 3 24
    >> floatingPlatform 40 4 28
    >> floatingPlatform 36 5 32
    >> floatingPlatform 40 6 36

    // ═══════════════════════════════════════════════════════════
    // Zone 7: Stair Access Points
    // ═══════════════════════════════════════════════════════════
    >> staircase 12 4 "X+" 4 2
    >> staircase 36 4 "X-" 4 2
    >> staircase 4 12 "Z+" 4 2
    >> staircase 4 36 "Z-" 4 2
    >> staircase 20 20 "X+" 10 2
    >> staircase 28 20 "X-" 10 2

    // ═══════════════════════════════════════════════════════════
    // Zone 8: Decorative Pillars
    // ═══════════════════════════════════════════════════════════
    >> pillarRow 8 8 4 8 6
    >> pillarRow 8 40 4 8 6
  )
