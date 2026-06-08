namespace Mibo.Layout3D

open Microsoft.Xna.Framework
open System.Collections.Generic
open Mibo.Layout

/// <summary>
/// A container for multiple HexGrid3Ds, managed by layer index.
/// Allows composing content across depth (e.g., Ground, Objects, UI).
/// </summary>
type LayeredHexGrid3D<'T> = {
  Width: int
  Height: int
  Depth: int
  HexSize: float32
  LayerHeight: float32
  Origin: Vector3
  Orientation: HexOrientation
  /// <summary>The active layers, keyed by their render order index.</summary>
  Layers: Dictionary<int, HexGrid3D<'T>>
}

module LayeredHexGrid3D =
  /// <summary>Creates a new, empty layered 3D hex grid container.</summary>
  let create
    width
    height
    depth
    (hexSize: float32)
    (layerHeight: float32)
    (origin: Vector3)
    (orientation: HexOrientation)
    : LayeredHexGrid3D<'T> =
    {
      Width = width
      Height = height
      Depth = depth
      HexSize = hexSize
      LayerHeight = layerHeight
      Origin = origin
      Orientation = orientation
      Layers = Dictionary()
    }

  /// <summary>Retrieves (or creates) the 3D hex grid for a specific layer.</summary>
  let getOrAddLayer
    index
    (grid: LayeredHexGrid3D<'T>)
    : HexGrid3D<'T> * LayeredHexGrid3D<'T> =
    match grid.Layers.TryGetValue index with
    | true, thing -> thing, grid
    | _ ->
      let newGrid =
        HexGrid3D.create
          grid.Width
          grid.Height
          grid.Depth
          grid.HexSize
          grid.LayerHeight
          grid.Origin
          grid.Orientation

      grid.Layers.Add(index, newGrid)
      newGrid, grid

module LayeredHexLayout3D =
  /// <summary>
  /// Executes a layout function on a specific layer.
  /// If the layer does not exist, it is created.
  /// </summary>
  /// <param name="index">The target layer index.</param>
  /// <param name="f">The layout function (HexGrid3DSection -> HexGrid3DSection).</param>
  /// <param name="grid">The layered 3D hex grid container.</param>
  let inline layer
    index
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (grid: LayeredHexGrid3D<'T>)
    : LayeredHexGrid3D<'T> =
    let targetGrid, updatedContainer = LayeredHexGrid3D.getOrAddLayer index grid

    HexLayout3D.run f targetGrid |> ignore

    updatedContainer
