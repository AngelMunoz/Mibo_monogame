module _3DSample.Level.Stamps

open Microsoft.Xna.Framework
open Mibo.Layout3D
open _3DSample.Level.Cells

// ============================================================================
// Low-Level Stamp Helpers
// ============================================================================
// These stamps place individual geometric elements into the grid.

/// <summary>Place a grid of 4x4 floor tiles.</summary>
let floorGrid4x4 xStart zStart xCount zCount : Stamp =
  fun s ->
    let mutable section = s

    for ix in 0 .. xCount - 1 do
      for iz in 0 .. zCount - 1 do
        let x = xStart + ix * 4
        let z = zStart + iz * 4
        section <- placeWithFootprint x 0 z floorTile4x4 section

    section

/// <summary>Place a grid of 2x2 platforms at given Y level.</summary>
let floorGrid2x2 xStart yLevel zStart xCount zCount : Stamp =
  fun s ->
    let mutable section = s

    for ix in 0 .. xCount - 1 do
      for iz in 0 .. zCount - 1 do
        let x = xStart + ix * 2
        let z = zStart + iz * 2
        section <- placeWithFootprint x yLevel z platform2x2 section

    section

/// <summary>Single 1x1 platform at position.</summary>
let singleBlock x y z : Stamp = fun s -> Layout3D.set x y z block s

/// <summary>Staircase using 1x1 blocks.</summary>
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

/// <summary>Column of 1x1 blocks.</summary>
let pillarColumn x z height : Stamp =
  fun s ->
    let mutable section = s

    for y in 0 .. height - 1 do
      section <- Layout3D.set x y z block section

    section

/// <summary>Floating platform (2x2 at given height).</summary>
let floatingPlatform x y z : Stamp = placeWithFootprint x y z platform2x2

/// <summary>Jumping challenge - series of floating blocks.</summary>
let jumpingBlocks x z startY count spacing : Stamp =
  fun s ->
    let mutable section = s

    for i in 0 .. count - 1 do
      section <- Layout3D.set (x + i * spacing) (startY + i) z block section

    section

/// <summary>Row of pillars.</summary>
let pillarRow x z count spacing height : Stamp =
  fun s ->
    let mutable section = s

    for i in 0 .. count - 1 do
      section <- pillarColumn (x + i * spacing) z height section

    section

/// <summary>Bridge at given height.</summary>
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

    for w in 0 .. width - 1 do
      let wx = if dz <> 0 then x2 + w else x2
      let wz = if dx <> 0 then z2 + w else z2
      section <- Layout3D.set wx y wz block section

    section

/// <summary>Stairway tower - spiral stairs going up.</summary>
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

/// <summary>Platform ring at given height.</summary>
let platformRing cx cz y radius : Stamp =
  fun s ->
    let mutable section = s

    for i in -radius .. radius do
      section <- Layout3D.set (cx + i) y (cz - radius) block section
      section <- Layout3D.set (cx + i) y (cz + radius) block section
      section <- Layout3D.set (cx - radius) y (cz + i) block section
      section <- Layout3D.set (cx + radius) y (cz + i) block section

    section

// ============================================================================
// High-Level Composable Stamps
// ============================================================================
// These stamps combine low-level stamps into recognizable level features.

/// <summary>Central plaza with 4x4 floor tiles.</summary>
let centralPlaza w d : Stamp = floorGrid4x4 0 0 w d

/// <summary>Corner tower structure.</summary>
let cornerTower x z h : Stamp =
  fun s ->
    s
    |> pillarColumn x z h
    |> pillarColumn (x + 3) z h
    |> pillarColumn x (z + 3) h
    |> pillarColumn (x + 3) (z + 3) h
    |> floorGrid2x2 x h z 2 2

/// <summary>Platformer challenge section.</summary>
let platformerChallenge x z : Stamp =
  fun s ->
    s
    |> floatingPlatform x 2 z
    |> floatingPlatform (x + 4) 3 (z + 2)
    |> floatingPlatform (x + 8) 4 z
    |> floatingPlatform (x + 4) 5 (z - 2)
    |> floorGrid2x2 (x + 6) 6 (z - 2) 2 2

/// <summary>Jumping pillar challenge.</summary>
let pillarHopping x z : Stamp =
  fun s ->
    s
    |> singleBlock x 1 z
    |> singleBlock (x + 2) 2 (z + 2)
    |> singleBlock (x + 4) 3 z
    |> singleBlock (x + 6) 4 (z + 2)
    |> singleBlock (x + 8) 5 z
    |> floatingPlatform (x + 10) 6 (z + 1)

/// <summary>Vertical climb challenge.</summary>
let climbChallenge x z : Stamp =
  fun s -> s |> spiralTower x z 12 |> floatingPlatform (x - 1) 12 (z - 1)

/// <summary>Multi-level platform area.</summary>
let multiLevelArea x z : Stamp =
  fun s ->
    s
    |> floorGrid2x2 x 0 z 4 4
    |> floorGrid2x2 (x + 2) 2 (z + 2) 2 2
    |> floorGrid2x2 x 4 z 2 2
    |> floorGrid2x2 (x + 4) 4 (z + 4) 2 2
    |> floatingPlatform (x + 2) 6 (z + 2)

/// <summary>Bridge connecting two areas.</summary>
let connectingBridge x1 z1 x2 z2 y : Stamp = bridge x1 z1 x2 z2 y 2
