namespace Mibo.Layout

/// <summary>
/// A domain module providing standard layout stamps for Platformer games.
/// These functions generate geometry using standard Layout primitives.
/// They are agnostic to the layer or content type, allowing flexible reuse.
/// </summary>
module Platformer =

  /// <summary>
  /// Generates a rectangular room with a border (walls) and filled interior (floor).
  /// </summary>
  /// <param name="width">Total width including walls.</param>
  /// <param name="height">Total height including walls.</param>
  /// <param name="wall">Content for the border.</param>
  /// <param name="floor">Content for the interior.</param>
  let room
    width
    height
    wall
    floor
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    section
    |> Layout.fill 0 0 width height floor
    |> Layout.border 0 0 width height wall

  /// <summary>
  /// Generates a horizontal platform.
  /// </summary>
  /// <param name="width">Width of the platform.</param>
  /// <param name="tile">Content for the platform.</param>
  let platform width tile (section: GridSection2D<'T>) : GridSection2D<'T> =
    section |> Layout.repeatX 0 0 width tile

  /// <summary>
  /// Generates a vertical wall or pillar.
  /// </summary>
  /// <param name="height">Height of the wall.</param>
  /// <param name="tile">Content for the wall.</param>
  let wall height tile (section: GridSection2D<'T>) : GridSection2D<'T> =
    section |> Layout.repeatY 0 0 height tile

  /// <summary>
  /// Direction for stairs construction.
  /// </summary>
  [<Struct>]
  type StairDirection =
    | UpRight
    | UpLeft
    | DownRight
    | DownLeft

  /// <summary>
  /// Generates a staircase.
  /// </summary>
  /// <param name="width">Total width of the stairs area.</param>
  /// <param name="tile">Content for the steps.</param>
  /// <param name="direction">Direction of the stairs.</param>
  let stairs
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
