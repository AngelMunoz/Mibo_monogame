namespace Mibo.Layout

open Microsoft.Xna.Framework

/// <summary>
/// A dense, flat 2D grid optimized for spatial locality and minimal GC pressure.
/// Uses 'voption' (struct option) to avoid heap allocations for empty cells.
/// Best suited for tilemaps, cellular automata, or dense spatial data.
/// For sparse data over huge coordinates, consider a chunked system instead.
/// </summary>
type CellGrid2D<'T> = {
  /// <summary>
  /// The world-space coordinate corresponding to the top-left (0,0) of the grid index.
  /// Used for translating grid indices to game world positions.
  /// </summary>
  Origin: Vector2
  /// <summary>
  /// The dimensions of a single cell in world units.
  /// Used for hit-testing and rendering calculations.
  /// </summary>
  CellSize: Vector2
  Width: int
  Height: int
  /// <summary>
  /// The raw backing store. direct access is discouraged; use 'get'/'set' for bounds safety.
  /// </summary>
  Cells: 'T voption[]
}

module CellGrid2D =
  /// <summary>
  /// Computes the flat array index from 2D coordinates.
  /// </summary>
  let inline private toIndex x y width = x + y * width

  /// <summary>
  /// Allocates a new grid. This is an O(Width * Height) operation that allocates a single contiguous array.
  /// </summary>
  let create
    width
    height
    (cellSize: Vector2)
    (origin: Vector2)
    : CellGrid2D<'T> =
    {
      Origin = origin
      CellSize = cellSize
      Width = width
      Height = height
      Cells = Array.create (width * height) ValueNone
    }

  /// <summary>
  /// Sets a cell's value. Silently ignores out-of-bounds coordinates to allow
  /// "painting off the edge" without crashing. O(1).
  /// </summary>
  let inline set x y (content: 'T) (grid: CellGrid2D<'T>) : unit =
    if x >= 0 && x < grid.Width && y >= 0 && y < grid.Height then
      let idx = toIndex x y grid.Width
      grid.Cells.[idx] <- ValueSome content

  /// <summary>
  /// Retrieves a cell's value. Returns ValueNone for out-of-bounds coordinates.
  /// O(1).
  /// </summary>
  let inline get x y (grid: CellGrid2D<'T>) : 'T voption =
    if x >= 0 && x < grid.Width && y >= 0 && y < grid.Height then
      let idx = toIndex x y grid.Width
      grid.Cells.[idx]
    else
      ValueNone

  /// <summary>
  /// Projects a grid index (x, y) to its world-space position (top-left of the cell).
  /// Does NOT validate bounds.
  /// </summary>
  let inline getWorldPos x y (grid: CellGrid2D<'T>) : Vector2 =
    Vector2(
      grid.Origin.X + float32 x * grid.CellSize.X,
      grid.Origin.Y + float32 y * grid.CellSize.Y
    )

  /// <summary>
  /// Iterates over every populated cell in the grid.
  /// Skips ValueNone cells cheaply.
  /// </summary>
  let inline iter
    ([<InlineIfLambda>] action: int -> int -> 'T -> unit)
    (grid: CellGrid2D<'T>)
    : unit =
    let w = grid.Width

    for i in 0 .. grid.Cells.Length - 1 do
      match grid.Cells.[i] with
      | ValueSome content ->
        let x = i % w
        let y = i / w
        action x y content
      | ValueNone -> ()

  /// <summary>
  /// Optimization for rendering: only iterates cells that intersect the given world-space bounds.
  /// Use this to avoid drawing off-screen tiles.
  /// </summary>
  let inline iterVisible
    (bounds: Rectangle)
    ([<InlineIfLambda>] action: int -> int -> 'T -> unit)
    (grid: CellGrid2D<'T>)
    : unit =
    let startX = max 0 ((bounds.X - int grid.Origin.X) / int grid.CellSize.X)
    let startY = max 0 ((bounds.Y - int grid.Origin.Y) / int grid.CellSize.Y)

    let endX =
      min
        (grid.Width - 1)
        ((bounds.Right - int grid.Origin.X) / int grid.CellSize.X)

    let endY =
      min
        (grid.Height - 1)
        ((bounds.Bottom - int grid.Origin.Y) / int grid.CellSize.Y)

    let w = grid.Width

    for y in startY..endY do
      let yOffset = y * w

      for x in startX..endX do
        let idx = yOffset + x

        match grid.Cells.[idx] with
        | ValueSome content -> action x y content
        | ValueNone -> ()
