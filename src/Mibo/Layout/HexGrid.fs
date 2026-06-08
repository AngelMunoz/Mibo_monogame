namespace Mibo.Layout

open Microsoft.Xna.Framework

/// <summary>
/// Hexagon orientation: pointy-top (vertices at top/bottom) or flat-top (vertices at left/right).
/// Affects neighbor offsets, world position calculations, and spatial algorithms.
/// </summary>
[<Struct>]
type HexOrientation =
  | PointyTop
  | FlatTop

/// <summary>
/// A dense, flat hexagonal grid using offset coordinates and a flat array backing store.
/// Supports both pointy-top and flat-top orientations.
/// Uses 'voption' (struct option) to avoid heap allocations for empty cells.
/// </summary>
[<Struct>]
type HexGrid<'T> = {
  /// <summary>The world-space coordinate corresponding to the center of hex (0,0).</summary>
  Origin: Vector2
  /// <summary>The radius of a single hexagon in world units (center to vertex distance).</summary>
  Size: float32
  /// <summary>Whether hexes are pointy-top or flat-top.</summary>
  Orientation: HexOrientation
  Width: int
  Height: int
  /// <summary>The raw backing store. Direct access is discouraged; use 'get'/'set' for bounds safety.</summary>
  Cells: 'T voption[]
}

module HexGrid =
  let inline private toIndex col row width = col + row * width

  let inline private hexDimensions
    (size: float32)
    (orientation: HexOrientation)
    =
    match orientation with
    | PointyTop -> struct (size * sqrt 3f, size * 2f)
    | FlatTop -> struct (size * 2f, size * sqrt 3f)

  /// <summary>
  /// Allocates a new hex grid. This is an O(Width * Height) operation that allocates a single contiguous array.
  /// </summary>
  /// <param name="width">Number of columns.</param>
  /// <param name="height">Number of rows.</param>
  /// <param name="size">Hex radius (center to vertex) in world units.</param>
  /// <param name="origin">World-space position of the center of hex (0,0).</param>
  /// <param name="orientation">Pointy-top or flat-top orientation.</param>
  let create
    width
    height
    (size: float32)
    (origin: Vector2)
    (orientation: HexOrientation)
    : HexGrid<'T> =
    {
      Origin = origin
      Size = size
      Orientation = orientation
      Width = width
      Height = height
      Cells = Array.create (width * height) ValueNone
    }

  /// <summary>
  /// Sets a cell's value. Silently ignores out-of-bounds coordinates to allow
  /// "painting off the edge" without crashing. O(1).
  /// </summary>
  let inline set col row (content: 'T) (grid: HexGrid<'T>) : unit =
    if col >= 0 && col < grid.Width && row >= 0 && row < grid.Height then
      let idx = toIndex col row grid.Width
      grid.Cells.[idx] <- ValueSome content

  /// <summary>
  /// Retrieves a cell's value. Returns ValueNone for out-of-bounds coordinates. O(1).
  /// </summary>
  let inline get col row (grid: HexGrid<'T>) : 'T voption =
    if col >= 0 && col < grid.Width && row >= 0 && row < grid.Height then
      let idx = toIndex col row grid.Width
      grid.Cells.[idx]
    else
      ValueNone

  /// <summary>
  /// Clears a cell (sets to ValueNone). Silently ignores out-of-bounds coordinates. O(1).
  /// </summary>
  let inline clear col row (grid: HexGrid<'T>) : unit =
    if col >= 0 && col < grid.Width && row >= 0 && row < grid.Height then
      let idx = toIndex col row grid.Width
      grid.Cells.[idx] <- ValueNone

  /// <summary>
  /// Projects a grid index (col, row) to its world-space center position,
  /// accounting for hex orientation and offset staggering. Does NOT validate bounds.
  /// </summary>
  let inline getWorldPos col row (grid: HexGrid<'T>) : Vector2 =
    let struct (hexW, hexH) = hexDimensions grid.Size grid.Orientation

    match grid.Orientation with
    | PointyTop ->
      let x =
        grid.Origin.X
        + float32 col * hexW
        + (if row % 2 = 1 then hexW / 2f else 0f)

      let y = grid.Origin.Y + float32 row * hexH * 0.75f
      Vector2(x + hexW / 2f, y + hexH / 2f)
    | FlatTop ->
      let x = grid.Origin.X + float32 col * hexW * 0.75f

      let y =
        grid.Origin.Y
        + float32 row * hexH
        + (if col % 2 = 1 then hexH / 2f else 0f)

      Vector2(x + hexW / 2f, y + hexH / 2f)

  /// <summary>
  /// Iterates over every populated cell in the grid.
  /// Skips ValueNone cells cheaply.
  /// </summary>
  let inline iter
    ([<InlineIfLambda>] action: int -> int -> 'T -> unit)
    (grid: HexGrid<'T>)
    : unit =
    let w = grid.Width

    for i in 0 .. grid.Cells.Length - 1 do
      match grid.Cells.[i] with
      | ValueSome content ->
        let col = i % w
        let row = i / w
        action col row content
      | ValueNone -> ()

  /// <summary>
  /// Optimization for rendering: only iterates cells that intersect the given world-space bounds.
  /// Use this to avoid drawing off-screen hexes.
  /// </summary>
  let inline iterVisible
    (left: float32)
    (top: float32)
    (right: float32)
    (bottom: float32)
    ([<InlineIfLambda>] action: int -> int -> 'T -> unit)
    (grid: HexGrid<'T>)
    : unit =
    let struct (hexW, hexH) = hexDimensions grid.Size grid.Orientation

    let startCol, endCol, startRow, endRow =
      match grid.Orientation with
      | PointyTop ->
        let sc = max 0 (int((left - grid.Origin.X) / hexW) - 1)
        let ec = min (grid.Width - 1) (int((right - grid.Origin.X) / hexW) + 1)
        let sr = max 0 (int((top - grid.Origin.Y) / (hexH * 0.75f)) - 1)

        let er =
          min
            (grid.Height - 1)
            (int((bottom - grid.Origin.Y) / (hexH * 0.75f)) + 1)

        sc, ec, sr, er
      | FlatTop ->
        let sc = max 0 (int((left - grid.Origin.X) / (hexW * 0.75f)) - 1)

        let ec =
          min
            (grid.Width - 1)
            (int((right - grid.Origin.X) / (hexW * 0.75f)) + 1)

        let sr = max 0 (int((top - grid.Origin.Y) / hexH) - 1)

        let er =
          min (grid.Height - 1) (int((bottom - grid.Origin.Y) / hexH) + 1)

        sc, ec, sr, er

    let w = grid.Width

    for row in startRow..endRow do
      let rowOffset = row * w

      for col in startCol..endCol do
        let idx = rowOffset + col

        match grid.Cells.[idx] with
        | ValueSome content -> action col row content
        | ValueNone -> ()
