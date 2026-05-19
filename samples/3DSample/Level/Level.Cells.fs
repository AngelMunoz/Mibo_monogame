module _3DSample.Level.Cells

open Microsoft.Xna.Framework
open Mibo.Layout3D
open _3DSample.Domain

// ============================================================================
// Cell Types and Constructors
// ============================================================================
// A Stamp is a composable section transformer. Stamps compose via >>.

/// <summary>A stamp transforms a grid section. Compose multiple stamps with <c>&gt;&gt;</c>.</summary>
type Stamp = GridSection3D<Cell> -> GridSection3D<Cell>

/// <summary>No-op stamp, useful as a starting point for composition.</summary>
let none: Stamp = id

// ─────────────────────────────────────────────────────────────
// Cell Constructors
// ─────────────────────────────────────────────────────────────

/// <summary>Create an anchor cell (will be rendered).</summary>
let anchor asset size = {
  AssetName = asset
  Rotation = Quaternion.Identity
  Size = size
  Render = true
}

/// <summary>Create a collision marker cell (not rendered, but has collision).</summary>
let marker asset size = {
  AssetName = asset
  Rotation = Quaternion.Identity
  Size = size
  Render = false
}

// ─────────────────────────────────────────────────────────────
// Pre-defined Cell Types
// ─────────────────────────────────────────────────────────────

/// <summary>4x4 floor tile.</summary>
let floorTile4x4 =
  anchor "Models/Platform/platform_4x4x1_blue" (Vector3(4f, 1f, 4f))

/// <summary>2x2 platform.</summary>
let platform2x2 =
  anchor "Models/Platform/platform_2x2x1_blue" (Vector3(2f, 1f, 2f))

/// <summary>1x1 block.</summary>
let block = anchor "Models/Platform/platform_1x1x1_blue" (Vector3(1f, 1f, 1f))

/// <summary>Arch piece.</summary>
let arch = anchor "Models/Platform/arch_blue" (Vector3(2f, 3f, 1f))

/// <summary>Rotate a cell around the Y axis.</summary>
let rotateY angle (cell: Cell) = {
  cell with
      Rotation = Quaternion.CreateFromYawPitchRoll(angle, 0f, 0f)
}

// ─────────────────────────────────────────────────────────────
// Footprint Helper
// ─────────────────────────────────────────────────────────────

/// <summary>Place a cell with its full footprint filled with collision markers.</summary>
let placeWithFootprint x y z (cell: Cell) : Stamp =
  let sizeX = int cell.Size.X
  let sizeY = int cell.Size.Y
  let sizeZ = int cell.Size.Z
  let markerCell = { cell with Render = false }

  fun s ->
    s
    |> Layout3D.fill x y z sizeX sizeY sizeZ markerCell
    |> Layout3D.set x y z cell
