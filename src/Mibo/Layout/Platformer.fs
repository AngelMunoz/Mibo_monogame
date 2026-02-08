namespace Mibo.Layout

/// <summary>
/// A domain module providing standard layout stamps for Platformer games.
/// These functions generate geometry using standard Layout primitives.
/// They are agnostic to the layer or content type, allowing flexible reuse.
/// </summary>
module Platformer =

  /// <summary>
  /// Anchor side for ledges or alignment.
  /// </summary>
  [<Struct>]
  type Anchor =
    | Left
    | Right

  /// <summary>
  /// Direction for stairs and slopes construction.
  /// </summary>
  [<Struct>]
  type StairDirection =
    | UpRight
    | UpLeft
    | DownRight
    | DownLeft

  /// <summary>
  /// Generates a rectangular box with a border and filled interior.
  /// </summary>
  /// <param name="width">Total width including border.</param>
  /// <param name="height">Total height including border.</param>
  /// <param name="border">Content for the border.</param>
  /// <param name="fill">Content for the interior.</param>
  let inline box
    width
    height
    border
    fill
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    section |> Layout.rect 0 0 width height border fill

  /// <summary>
  /// Generates a horizontal platform.
  /// </summary>
  /// <param name="width">Width of the platform.</param>
  /// <param name="tile">Content for the platform.</param>
  let inline platform
    width
    tile
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    section |> Layout.repeatX 0 0 width tile

  /// <summary>
  /// Generates a horizontal platform anchored to a specific side of the section.
  /// </summary>
  let inline ledge
    width
    anchor
    tile
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    match anchor with
    | Left -> section |> Layout.repeatX 0 0 width tile
    | Right -> section |> Layout.repeatX (section.Width - width) 0 width tile

  /// <summary>
  /// Generates a vertical wall or pillar.
  /// </summary>
  /// <param name="height">Height of the wall.</param>
  /// <param name="tile">Content for the wall.</param>
  let inline wall height tile (section: GridSection2D<'T>) : GridSection2D<'T> =
    section |> Layout.repeatY 0 0 height tile

  /// <summary>
  /// Generates a 1-tile wide vertical structure with distinct base, middle, and top tiles.
  /// </summary>
  let inline pillar
    height
    baseTile
    middleTile
    topTile
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    if height <= 0 then
      section
    elif height = 1 then
      section |> Layout.set 0 0 middleTile
    else
      section
      |> Layout.set 0 (height - 1) baseTile
      |> Layout.repeatY 0 1 (height - 2) middleTile
      |> Layout.set 0 0 topTile

  /// <summary>
  /// Generates a staircase.
  /// </summary>
  /// <param name="width">Total width of the stairs area.</param>
  /// <param name="tile">Content for the steps.</param>
  /// <param name="direction">Direction of the stairs.</param>
  let inline stairs
    width
    tile
    direction
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    let height = width // Square aspect ratio for simple stairs

    match direction with
    | UpRight ->
      // Steps go up and to the right: (0, H-1), (1, H-2), ...
      for i in 0 .. width - 1 do
        Layout.set i (height - 1 - i) tile section |> ignore
    | DownRight ->
      // Steps go down and to the right: (0, 0), (1, 1), ...
      for i in 0 .. width - 1 do
        Layout.set i i tile section |> ignore
    | UpLeft ->
      // Steps go up and to the left
      for i in 0 .. width - 1 do
        Layout.set (width - 1 - i) (height - 1 - i) tile section |> ignore
    | DownLeft ->
      // Steps go down and to the left
      for i in 0 .. width - 1 do
        Layout.set (width - 1 - i) i tile section |> ignore

    section

  /// <summary>
  /// Generates a smooth diagonal slope using line rasterization.
  /// </summary>
  let inline slope
    width
    height
    tile
    direction
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    match direction with
    | UpRight -> Layout.line 0 (height - 1) (width - 1) 0 tile section
    | DownRight -> Layout.line 0 0 (width - 1) (height - 1) tile section
    | UpLeft -> Layout.line (width - 1) (height - 1) 0 0 tile section
    | DownLeft -> Layout.line (width - 1) 0 0 (height - 1) tile section

  /// <summary>
  /// Creates a horizontal pit (gap in the ground) by clearing a column of cells.
  /// Use this to create fall-through areas or deadly gaps.
  /// </summary>
  /// <param name="width">Width of the pit.</param>
  /// <param name="depth">Depth of the pit (how many cells to clear vertically).</param>
  let inline pit width depth (section: GridSection2D<'T>) : GridSection2D<'T> =
    Layout.clear 0 0 width depth section

  /// <summary>
  /// Creates a rectangular empty area by clearing cells.
  /// Use this to carve out spaces in existing terrain.
  /// </summary>
  /// <param name="width">Width of the gap.</param>
  /// <param name="height">Height of the gap.</param>
  let inline gap width height (section: GridSection2D<'T>) : GridSection2D<'T> =
    Layout.clear 0 0 width height section
