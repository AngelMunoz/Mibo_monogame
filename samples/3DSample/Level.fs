module _3DSample.Level

open System
open Microsoft.Xna.Framework
open Mibo.Layout3D

// ─────────────────────────────────────────────────────────────
// Composable Stamps for 3D Level Building
// ─────────────────────────────────────────────────────────────

/// A stamp is a section transformer - composes via >>
type Stamp = GridSection3D<Cell> -> GridSection3D<Cell>

let none: Stamp = id

// ─────────────────────────────────────────────────────────────
// Cell Constructors with Size Information
// ─────────────────────────────────────────────────────────────

/// Create an anchor cell (will be rendered)
let anchor asset size = {
  AssetName = asset
  Rotation = Quaternion.Identity
  Size = size
  Render = true
}

/// Create a collision marker cell (not rendered, but has collision)
let marker asset size = {
  AssetName = asset
  Rotation = Quaternion.Identity
  Size = size
  Render = false
}

// Pre-defined cell types with their sizes
let floorTile4x4 =
  anchor "Models/Platform/platform_4x4x1_blue" (Vector3(4f, 1f, 4f))

let platform2x2 =
  anchor "Models/Platform/platform_2x2x1_blue" (Vector3(2f, 1f, 2f))

let block = anchor "Models/Platform/platform_1x1x1_blue" (Vector3(1f, 1f, 1f))
let arch = anchor "Models/Platform/arch_blue" (Vector3(2f, 3f, 1f))

/// Rotate a cell around Y axis
let rotateY angle (cell: Cell) = {
  cell with
      Rotation = Quaternion.CreateFromYawPitchRoll(angle, 0f, 0f)
}

// ─────────────────────────────────────────────────────────────
// Footprint-Filling Helper
// ─────────────────────────────────────────────────────────────

/// Place a cell with its full footprint filled
let placeWithFootprint x y z (cell: Cell) : Stamp =
  let sizeX = int cell.Size.X
  let sizeY = int cell.Size.Y
  let sizeZ = int cell.Size.Z
  let markerCell = { cell with Render = false }

  fun s ->
    s
    |> Layout3D.fill x y z sizeX sizeY sizeZ markerCell
    |> Layout3D.set x y z cell

// ─────────────────────────────────────────────────────────────
// Low-Level Stamp Helpers
// ─────────────────────────────────────────────────────────────

/// Place a grid of 4x4 floor tiles
let floorGrid4x4 xStart zStart xCount zCount : Stamp =
  fun s ->
    let mutable section = s

    for ix in 0 .. xCount - 1 do
      for iz in 0 .. zCount - 1 do
        let x = xStart + ix * 4
        let z = zStart + iz * 4
        section <- placeWithFootprint x 0 z floorTile4x4 section

    section

/// Place a grid of 2x2 platforms at given Y level
let floorGrid2x2 xStart yLevel zStart xCount zCount : Stamp =
  fun s ->
    let mutable section = s

    for ix in 0 .. xCount - 1 do
      for iz in 0 .. zCount - 1 do
        let x = xStart + ix * 2
        let z = zStart + iz * 2
        section <- placeWithFootprint x yLevel z platform2x2 section

    section

/// Single 1x1 platform at position
let singleBlock x y z : Stamp = fun s -> Layout3D.set x y z block s

/// Staircase using 1x1 blocks
let staircase x z direction stepsUp stepsWidth : Stamp =
  fun s ->
    let mutable section = s

    for step in 0 .. stepsUp - 1 do
      for w in 0 .. stepsWidth - 1 do
        let xPos, zPos =
          match direction with
          | "X+" -> x + step, z + w
          | "X-" -> x - step, z + w
          | "Z+" -> x + w, z + step
          | _ -> x + w, z - step

        for y in 0..step do
          section <- Layout3D.set xPos y zPos block section

    section

/// Column of 1x1 blocks
let pillarColumn x z height : Stamp =
  fun s ->
    let mutable section = s

    for y in 0 .. height - 1 do
      section <- Layout3D.set x y z block section

    section

/// Floating platform (2x2 at given height)
let floatingPlatform x y z : Stamp = placeWithFootprint x y z platform2x2

/// Jumping challenge - series of floating blocks
let jumpingBlocks x z startY count spacing : Stamp =
  fun s ->
    let mutable section = s

    for i in 0 .. count - 1 do
      section <- Layout3D.set (x + i * spacing) (startY + i) z block section

    section

/// Row of pillars
let pillarRow x z count spacing height : Stamp =
  fun s ->
    let mutable section = s

    for i in 0 .. count - 1 do
      section <- pillarColumn (x + i * spacing) z height section

    section

/// Bridge at given height
let bridge x1 z1 x2 z2 y width : Stamp =
  fun s ->
    let mutable section = s

    let dx =
      if x2 > x1 then 1
      elif x2 < x1 then -1
      else 0

    let dz =
      if z2 > z1 then 1
      elif z2 < z1 then -1
      else 0

    let mutable cx, cz = x1, z1

    while cx <> x2 || cz <> z2 do
      for w in 0 .. width - 1 do
        let wx = if dz <> 0 then cx + w else cx
        let wz = if dx <> 0 then cz + w else cz
        section <- Layout3D.set wx y wz block section

      if cx <> x2 then
        cx <- cx + dx

      if cz <> z2 then
        cz <- cz + dz

    // Final position
    for w in 0 .. width - 1 do
      let wx = if dz <> 0 then x2 + w else x2
      let wz = if dx <> 0 then z2 + w else z2
      section <- Layout3D.set wx y wz block section

    section

/// Stairway tower - spiral stairs going up
let spiralTower x z height : Stamp =
  fun s ->
    let mutable section = s

    for h in 0 .. height - 1 do
      let dir = h % 4

      let ox, oz =
        match dir with
        | 0 -> 0, 0
        | 1 -> 2, 0
        | 2 -> 2, 2
        | _ -> 0, 2

      section <- Layout3D.set (x + ox) h (z + oz) block section

    section

/// Platform ring at given height
let platformRing cx cz y radius : Stamp =
  fun s ->
    let mutable section = s
    // Place blocks in a square ring pattern
    for i in -radius .. radius do
      section <- Layout3D.set (cx + i) y (cz - radius) block section
      section <- Layout3D.set (cx + i) y (cz + radius) block section
      section <- Layout3D.set (cx - radius) y (cz + i) block section
      section <- Layout3D.set (cx + radius) y (cz + i) block section

    section

// ─────────────────────────────────────────────────────────────
// High-Level Composable Stamps
// ─────────────────────────────────────────────────────────────

/// Central plaza with 4x4 floor tiles
let centralPlaza w d : Stamp = floorGrid4x4 0 0 w d

/// Corner tower structure
let cornerTower x z h : Stamp =
  fun s ->
    s
    |> pillarColumn x z h
    |> pillarColumn (x + 3) z h
    |> pillarColumn x (z + 3) h
    |> pillarColumn (x + 3) (z + 3) h
    |> floorGrid2x2 x h z 2 2

/// Platformer challenge section
let platformerChallenge x z : Stamp =
  fun s ->
    s
    // Floating platforms at increasing heights
    |> floatingPlatform x 2 z
    |> floatingPlatform (x + 4) 3 (z + 2)
    |> floatingPlatform (x + 8) 4 z
    |> floatingPlatform (x + 4) 5 (z - 2)
    // Goal platform
    |> floorGrid2x2 (x + 6) 6 (z - 2) 2 2

/// Jumping pillar challenge
let pillarHopping x z : Stamp =
  fun s ->
    s
    |> singleBlock x 1 z
    |> singleBlock (x + 2) 2 (z + 2)
    |> singleBlock (x + 4) 3 z
    |> singleBlock (x + 6) 4 (z + 2)
    |> singleBlock (x + 8) 5 z
    |> floatingPlatform (x + 10) 6 (z + 1)

/// Vertical climb challenge
let climbChallenge x z : Stamp =
  fun s -> s |> spiralTower x z 12 |> floatingPlatform (x - 1) 12 (z - 1)

/// Multi-level platform area
let multiLevelArea x z : Stamp =
  fun s ->
    s
    // Ground level 2x2 platforms
    |> floorGrid2x2 x 0 z 4 4
    // Raised platforms at different heights
    |> floorGrid2x2 (x + 2) 2 (z + 2) 2 2
    |> floorGrid2x2 x 4 z 2 2
    |> floorGrid2x2 (x + 4) 4 (z + 4) 2 2
    // Top platform
    |> floatingPlatform (x + 2) 6 (z + 2)

/// Bridge connecting two areas
let connectingBridge x1 z1 x2 z2 y : Stamp = bridge x1 z1 x2 z2 y 2

// ─────────────────────────────────────────────────────────────
// Main Level Creation
// ─────────────────────────────────────────────────────────────

/// Create a large showcase level demonstrating the Layout3D DSL
let create() : CellGrid3D<Cell> =
  // Large grid: 64x20x64 cells
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
    centralPlaza 12 12 // 48x48 world units

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

    // Northeast platforming section
    >> platformerChallenge 50 8

    // Northwest climbing challenge
    >> climbChallenge 8 50

    // Southeast pillar hopping
    >> pillarHopping 50 50

    // Central elevated rings
    >> platformRing 24 24 10 3
    >> platformRing 24 24 14 5

    // ═══════════════════════════════════════════════════════════
    // Zone 5: Multi-level Structures
    // ═══════════════════════════════════════════════════════════

    // West multi-level platform
    >> multiLevelArea 0 16

    // East multi-level platform
    >> multiLevelArea 48 16

    // ═══════════════════════════════════════════════════════════
    // Zone 6: Scattered Jump Challenges
    // ═══════════════════════════════════════════════════════════

    // Floating platforms scattered around
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

    // Stairs to elevated areas
    >> staircase 12 4 "X+" 4 2
    >> staircase 36 4 "X-" 4 2
    >> staircase 4 12 "Z+" 4 2
    >> staircase 4 36 "Z-" 4 2

    // Central stairs to first ring
    >> staircase 20 20 "X+" 10 2
    >> staircase 28 20 "X-" 10 2

    // ═══════════════════════════════════════════════════════════
    // Zone 8: Decorative Pillars
    // ═══════════════════════════════════════════════════════════
    >> pillarRow 8 8 4 8 6
    >> pillarRow 8 40 4 8 6
  )
