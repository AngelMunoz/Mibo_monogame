namespace Mibo.Layout3D

open Microsoft.Xna.Framework

/// <summary>
/// Optional renderer helper for CellGrid3D.
/// Provides convenience methods for iterating and rendering grid contents.
/// </summary>
module CellGridRenderer3D =

  /// <summary>
  /// Iterates over all populated cells and invokes renderCell with the world position and content.
  /// </summary>
  /// <param name="grid">The grid to render.</param>
  /// <param name="renderCell">Function to call for each populated cell with (worldPosition, content).</param>
  let inline render
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iter(fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell worldPos content)

  /// <summary>
  /// Iterates over populated cells within a bounding volume and invokes renderCell.
  /// Use this for frustum culling optimization.
  /// </summary>
  /// <param name="bounds">The world-space bounding box to filter by.</param>
  /// <param name="grid">The grid to render.</param>
  /// <param name="renderCell">Function to call for each visible cell.</param>
  let inline renderVolume
    (bounds: BoundingBox)
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iterVolume bounds (fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell worldPos content)

  /// <summary>
  /// Iterates over all populated cells and provides grid coordinates along with world position.
  /// Useful when you need both grid indices and world positions.
  /// </summary>
  let inline renderWithIndices
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: int -> int -> int -> Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iter(fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell x y z worldPos content)
