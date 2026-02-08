namespace Mibo.Layout

open Microsoft.Xna.Framework
open Mibo.Layout
open System.Collections.Generic

/// <summary>
/// A container for multiple Grid2Ds, managed by layer index.
/// Allows composing content across depth (e.g., Background, Foreground, UI).
/// </summary>
type LayeredGrid2D<'T> = {
  Width: int
  Height: int
  CellSize: Vector2
  Origin: Vector2
  /// <summary>
  /// The active layers, keyed by their render order index.
  /// </summary>
  Layers: Dictionary<int, CellGrid2D<'T>>
}

module LayeredGrid2D =
  /// <summary>
  /// Creates a new, empty layered grid container.
  /// No memory is allocated for layers until they are accessed.
  /// </summary>
  let create width height cellSize origin : LayeredGrid2D<'T> = {
    Width = width
    Height = height
    CellSize = cellSize
    Origin = origin
    Layers = Dictionary()
  }

  /// <summary>
  /// Retrieves (or creates) the grid for a specific layer.
  /// Note: This mutates the internal dictionary in-place.
  /// </summary>
  let getOrAddLayer
    index
    (grid: LayeredGrid2D<'T>)
    : CellGrid2D<'T> * LayeredGrid2D<'T> =
    match grid.Layers.TryGetValue index with
    | true, thing -> thing, grid
    | _ ->
      let newGrid =
        CellGrid2D.create grid.Width grid.Height grid.CellSize grid.Origin

      grid.Layers.Add(index, newGrid)
      newGrid, grid

module LayeredLayout =
  /// <summary>
  /// Executes a layout function on a specific layer.
  /// If the layer does not exist, it is created.
  /// </summary>
  /// <param name="index">The target layer index.</param>
  /// <param name="f">The layout function (GridSection2D -> GridSection2D).</param>
  /// <param name="grid">The layered grid container.</param>
  let inline layer
    index
    ([<InlineIfLambda>] f: GridSection2D<'T> -> GridSection2D<'T>)
    (grid: LayeredGrid2D<'T>)
    : LayeredGrid2D<'T> =
    let targetGrid, updatedContainer = LayeredGrid2D.getOrAddLayer index grid

    // Run the layout on the target grid (in-place mutation of the grid's cells)
    Layout.run f targetGrid |> ignore

    // Return the container (which might have a new layer map entry)
    updatedContainer
