namespace Mibo.Layout3D

open Microsoft.Xna.Framework

/// <summary>
/// A dense, flat 3D grid optimized for spatial locality and minimal GC pressure.
/// Uses 'voption' (struct option) to avoid heap allocations for empty cells.
/// Best suited for voxel-like layouts, 3D maps, or dense spatial data.
/// For sparse data over huge coordinates, consider a chunked system instead.
/// </summary>
/// <remarks>
/// Axis Convention:
/// - X = width (left/right)
/// - Y = height (up/down)
/// - Z = depth (forward/back)
/// </remarks>
[<Struct>]
type CellGrid3D<'T> = {
  /// <summary>
  /// The world-space coordinate corresponding to (0,0,0) of the grid index.
  /// Used for translating grid indices to game world positions.
  /// </summary>
  Origin: Vector3
  /// <summary>
  /// The dimensions of a single cell in world units.
  /// Used for hit-testing and rendering calculations.
  /// </summary>
  CellSize: Vector3
  Width: int
  Height: int
  Depth: int
  /// <summary>
  /// The raw backing store as a flat array.
  /// Indexed as: x + y * Width + z * Width * Height
  /// Direct access is discouraged; use 'get'/'set' for bounds safety.
  /// </summary>
  Cells: 'T voption[]
}

module CellGrid3D =
  /// <summary>
  /// Computes the flat array index from 3D coordinates.
  /// </summary>
  let inline private toIndex x y z width height =
    x + y * width + z * width * height

  /// <summary>
  /// Allocates a new grid. This is an O(Width * Height * Depth) operation
  /// that allocates a single contiguous array.
  /// </summary>
  let create
    width
    height
    depth
    (cellSize: Vector3)
    (origin: Vector3)
    : CellGrid3D<'T> =
    {
      Origin = origin
      CellSize = cellSize
      Width = width
      Height = height
      Depth = depth
      Cells = Array.create (width * height * depth) ValueNone
    }

  /// <summary>
  /// Sets a cell's value. Silently ignores out-of-bounds coordinates to allow
  /// "painting off the edge" without crashing. O(1).
  /// </summary>
  let inline set x y z (content: 'T) (grid: CellGrid3D<'T>) : unit =
    if
      x >= 0
      && x < grid.Width
      && y >= 0
      && y < grid.Height
      && z >= 0
      && z < grid.Depth
    then
      let idx = toIndex x y z grid.Width grid.Height
      grid.Cells.[idx] <- ValueSome content

  /// <summary>
  /// Retrieves a cell's value. Returns ValueNone for out-of-bounds coordinates.
  /// O(1).
  /// </summary>
  let inline get x y z (grid: CellGrid3D<'T>) : 'T voption =
    if
      x >= 0
      && x < grid.Width
      && y >= 0
      && y < grid.Height
      && z >= 0
      && z < grid.Depth
    then
      let idx = toIndex x y z grid.Width grid.Height
      grid.Cells.[idx]
    else
      ValueNone

  /// <summary>
  /// Clears a cell (sets to ValueNone). Silently ignores out-of-bounds coordinates.
  /// O(1).
  /// </summary>
  let inline clear x y z (grid: CellGrid3D<'T>) : unit =
    if
      x >= 0
      && x < grid.Width
      && y >= 0
      && y < grid.Height
      && z >= 0
      && z < grid.Depth
    then
      let idx = toIndex x y z grid.Width grid.Height
      grid.Cells.[idx] <- ValueNone

  /// <summary>
  /// Projects a grid index (x, y, z) to its world-space position (corner of the cell).
  /// Does NOT validate bounds.
  /// </summary>
  let inline getWorldPos x y z (grid: CellGrid3D<'T>) : Vector3 =
    Vector3(
      grid.Origin.X + float32 x * grid.CellSize.X,
      grid.Origin.Y + float32 y * grid.CellSize.Y,
      grid.Origin.Z + float32 z * grid.CellSize.Z
    )

  /// <summary>
  /// Iterates over every populated cell in the grid.
  /// Uses linear array traversal for optimal cache performance.
  /// Coordinates are derived from index only when content exists.
  /// </summary>
  let inline iter
    ([<InlineIfLambda>] action: int -> int -> int -> 'T -> unit)
    (grid: CellGrid3D<'T>)
    : unit =
    let w = grid.Width
    let wh = w * grid.Height

    for i in 0 .. grid.Cells.Length - 1 do
      match grid.Cells.[i] with
      | ValueSome content ->
        let x = i % w
        let y = (i / w) % grid.Height
        let z = i / wh
        action x y z content
      | ValueNone -> ()

  /// <summary>
  /// Optimization for rendering: only iterates cells that intersect the given world-space bounds.
  /// Uses direct index calculation to avoid redundant bounds checks.
  /// </summary>
  let inline iterVolume
    (bounds: BoundingBox)
    ([<InlineIfLambda>] action: int -> int -> int -> 'T -> unit)
    (grid: CellGrid3D<'T>)
    : unit =
    let startX = max 0 (int((bounds.Min.X - grid.Origin.X) / grid.CellSize.X))
    let startY = max 0 (int((bounds.Min.Y - grid.Origin.Y) / grid.CellSize.Y))
    let startZ = max 0 (int((bounds.Min.Z - grid.Origin.Z) / grid.CellSize.Z))

    let endX =
      min
        (grid.Width - 1)
        (int((bounds.Max.X - grid.Origin.X) / grid.CellSize.X))

    let endY =
      min
        (grid.Height - 1)
        (int((bounds.Max.Y - grid.Origin.Y) / grid.CellSize.Y))

    let endZ =
      min
        (grid.Depth - 1)
        (int((bounds.Max.Z - grid.Origin.Z) / grid.CellSize.Z))

    let w = grid.Width
    let wh = w * grid.Height

    for z in startZ..endZ do
      let zOffset = z * wh

      for y in startY..endY do
        let yzOffset = zOffset + y * w

        for x in startX..endX do
          let idx = yzOffset + x

          match grid.Cells.[idx] with
          | ValueSome content -> action x y z content
          | ValueNone -> ()
