namespace Mibo.Layout3D

open Microsoft.Xna.Framework
open Mibo.Layout

/// <summary>
/// A dense, flat 3D hexagonal grid. Hex cells are arranged on the XZ plane using
/// offset coordinates with a pointy-top or flat-top orientation. The Y axis provides
/// vertical layering with a configurable layer height.
/// Uses 'voption' (struct option) to avoid heap allocations for empty cells.
/// </summary>
/// <remarks>
/// Axis Convention:
/// - X/Z = hex plane (col/row mapped to X/Z)
/// - Y = vertical layers
/// </remarks>
[<Struct>]
type HexGrid3D<'T> = {
  /// <summary>The world-space coordinate corresponding to the center of hex (0,0,0).</summary>
  Origin: Vector3
  /// <summary>The radius of a single hexagon in world units (center to vertex distance).</summary>
  HexSize: float32
  /// <summary>The vertical distance between layers in world units.</summary>
  LayerHeight: float32
  /// <summary>Whether hexes are pointy-top or flat-top on the XZ plane.</summary>
  Orientation: HexOrientation
  Width: int
  Height: int
  Depth: int
  /// <summary>The raw backing store. Direct access is discouraged; use 'get'/'set' for bounds safety.</summary>
  Cells: 'T voption[]
}

module HexGrid3D =
  let inline private toIndex col row layer width depth =
    col + row * width + layer * width * depth

  let inline private hexDimensions
    (size: float32)
    (orientation: HexOrientation)
    =
    match orientation with
    | PointyTop -> struct (size * sqrt 3f, size * 2f)
    | FlatTop -> struct (size * 2f, size * sqrt 3f)

  /// <summary>
  /// Allocates a new 3D hex grid.
  /// </summary>
  /// <param name="width">Number of hex columns (X axis).</param>
  /// <param name="height">Number of vertical layers (Y axis).</param>
  /// <param name="depth">Number of hex rows (Z axis).</param>
  /// <param name="hexSize">Hex radius (center to vertex) in world units.</param>
  /// <param name="layerHeight">Vertical distance between layers.</param>
  /// <param name="origin">World-space position of the center of hex (0,0,0).</param>
  /// <param name="orientation">Pointy-top or flat-top orientation on the XZ plane.</param>
  let create
    width
    height
    depth
    (hexSize: float32)
    (layerHeight: float32)
    (origin: Vector3)
    (orientation: HexOrientation)
    : HexGrid3D<'T> =
    {
      Origin = origin
      HexSize = hexSize
      LayerHeight = layerHeight
      Orientation = orientation
      Width = width
      Height = height
      Depth = depth
      Cells = Array.create (width * depth * height) ValueNone
    }

  /// <summary>
  /// Sets a cell's value. Silently ignores out-of-bounds coordinates. O(1).
  /// </summary>
  let inline set col row layer (content: 'T) (grid: HexGrid3D<'T>) : unit =
    if
      col >= 0
      && col < grid.Width
      && row >= 0
      && row < grid.Depth
      && layer >= 0
      && layer < grid.Height
    then
      let idx = toIndex col row layer grid.Width grid.Depth
      grid.Cells.[idx] <- ValueSome content

  /// <summary>
  /// Retrieves a cell's value. Returns ValueNone for out-of-bounds coordinates. O(1).
  /// </summary>
  let inline get col row layer (grid: HexGrid3D<'T>) : 'T voption =
    if
      col >= 0
      && col < grid.Width
      && row >= 0
      && row < grid.Depth
      && layer >= 0
      && layer < grid.Height
    then
      let idx = toIndex col row layer grid.Width grid.Depth
      grid.Cells.[idx]
    else
      ValueNone

  /// <summary>
  /// Clears a cell (sets to ValueNone). Silently ignores out-of-bounds coordinates. O(1).
  /// </summary>
  let inline clear col row layer (grid: HexGrid3D<'T>) : unit =
    if
      col >= 0
      && col < grid.Width
      && row >= 0
      && row < grid.Depth
      && layer >= 0
      && layer < grid.Height
    then
      let idx = toIndex col row layer grid.Width grid.Depth
      grid.Cells.[idx] <- ValueNone

  /// <summary>
  /// Projects a grid index (col, row, layer) to its world-space center position,
  /// accounting for hex orientation, offset staggering, and layer height.
  /// Does NOT validate bounds.
  /// </summary>
  let inline getWorldPos col row layer (grid: HexGrid3D<'T>) : Vector3 =
    let struct (hexW, hexH) = hexDimensions grid.HexSize grid.Orientation

    let x, z =
      match grid.Orientation with
      | PointyTop ->
        let x = float32 col * hexW + (if row % 2 = 1 then hexW / 2f else 0f)

        let z = float32 row * hexH * 0.75f
        x, z
      | FlatTop ->
        let x = float32 col * hexW * 0.75f

        let z = float32 row * hexH + (if col % 2 = 1 then hexH / 2f else 0f)

        x, z

    Vector3(
      grid.Origin.X + x + hexW / 2f,
      grid.Origin.Y + float32 layer * grid.LayerHeight,
      grid.Origin.Z + z + hexH / 2f
    )

  /// <summary>
  /// Iterates over every populated cell in the 3D hex grid.
  /// Skips ValueNone cells cheaply.
  /// </summary>
  let inline iter
    ([<InlineIfLambda>] action: int -> int -> int -> 'T -> unit)
    (grid: HexGrid3D<'T>)
    : unit =
    let w = grid.Width
    let d = grid.Depth
    let wd = w * d

    for i in 0 .. grid.Cells.Length - 1 do
      match grid.Cells.[i] with
      | ValueSome content ->
        let col = i % w
        let row = (i / w) % d
        let layer = i / wd
        action col row layer content
      | ValueNone -> ()

  /// <summary>
  /// Optimization for rendering: only iterates cells that intersect the given world-space bounding box.
  /// Use this to avoid drawing off-screen hexes.
  /// </summary>
  let inline iterVolume
    (bounds: BoundingBox)
    ([<InlineIfLambda>] action: int -> int -> int -> 'T -> unit)
    (grid: HexGrid3D<'T>)
    : unit =
    let struct (hexW, hexH) = hexDimensions grid.HexSize grid.Orientation

    let startCol, endCol, startRow, endRow =
      match grid.Orientation with
      | PointyTop ->
        let sc = max 0 (int((bounds.Min.X - grid.Origin.X) / hexW) - 1)

        let ec =
          min (grid.Width - 1) (int((bounds.Max.X - grid.Origin.X) / hexW) + 1)

        let sr =
          max 0 (int((bounds.Min.Z - grid.Origin.Z) / (hexH * 0.75f)) - 1)

        let er =
          min
            (grid.Depth - 1)
            (int((bounds.Max.Z - grid.Origin.Z) / (hexH * 0.75f)) + 1)

        sc, ec, sr, er
      | FlatTop ->
        let sc =
          max 0 (int((bounds.Min.X - grid.Origin.X) / (hexW * 0.75f)) - 1)

        let ec =
          min
            (grid.Width - 1)
            (int((bounds.Max.X - grid.Origin.X) / (hexW * 0.75f)) + 1)

        let sr = max 0 (int((bounds.Min.Z - grid.Origin.Z) / hexH) - 1)

        let er =
          min (grid.Depth - 1) (int((bounds.Max.Z - grid.Origin.Z) / hexH) + 1)

        sc, ec, sr, er

    let startLayer =
      max 0 (int((bounds.Min.Y - grid.Origin.Y) / grid.LayerHeight))

    let endLayer =
      min
        (grid.Height - 1)
        (int((bounds.Max.Y - grid.Origin.Y) / grid.LayerHeight))

    let w = grid.Width
    let d = grid.Depth

    for layer in startLayer..endLayer do
      let layerOffset = layer * w * d

      for row in startRow..endRow do
        let rowOffset = row * w

        for col in startCol..endCol do
          let idx = layerOffset + rowOffset + col

          match grid.Cells.[idx] with
          | ValueSome content -> action col row layer content
          | ValueNone -> ()
