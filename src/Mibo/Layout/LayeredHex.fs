namespace Mibo.Layout

open Microsoft.Xna.Framework
open System.Collections.Generic

/// <summary>
/// A container for multiple HexGrids, managed by layer index.
/// Allows composing content across depth (e.g., Ground, Objects, UI).
/// </summary>
type LayeredHexGrid<'T> = {
  Width: int
  Height: int
  Size: float32
  Origin: Vector2
  Orientation: HexOrientation
  /// <summary>The active layers, keyed by their render order index.</summary>
  Layers: Dictionary<int, HexGrid<'T>>
}

module LayeredHexGrid =
  /// <summary>Creates a new, empty layered hex grid container.</summary>
  let create
    width
    height
    (size: float32)
    (origin: Vector2)
    (orientation: HexOrientation)
    : LayeredHexGrid<'T> =
    {
      Width = width
      Height = height
      Size = size
      Origin = origin
      Orientation = orientation
      Layers = Dictionary()
    }

  /// <summary>Retrieves (or creates) the hex grid for a specific layer.</summary>
  let getOrAddLayer
    index
    (grid: LayeredHexGrid<'T>)
    : HexGrid<'T> * LayeredHexGrid<'T> =
    match grid.Layers.TryGetValue index with
    | true, thing -> thing, grid
    | _ ->
      let newGrid =
        HexGrid.create
          grid.Width
          grid.Height
          grid.Size
          grid.Origin
          grid.Orientation

      grid.Layers.Add(index, newGrid)
      newGrid, grid

module LayeredHexLayout =
  /// <summary>
  /// Executes a layout function on a specific layer.
  /// If the layer does not exist, it is created.
  /// </summary>
  /// <param name="index">The target layer index.</param>
  /// <param name="f">The layout function (HexGridSection -> HexGridSection).</param>
  /// <param name="grid">The layered hex grid container.</param>
  let inline layer
    index
    ([<InlineIfLambda>] f: HexGridSection<'T> -> HexGridSection<'T>)
    (grid: LayeredHexGrid<'T>)
    : LayeredHexGrid<'T> =
    let targetGrid, updatedContainer = LayeredHexGrid.getOrAddLayer index grid

    HexLayout.run f targetGrid |> ignore

    updatedContainer
