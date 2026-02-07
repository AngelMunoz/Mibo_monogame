namespace Mibo.Layout

/// <summary>
/// A domain module providing standard layout stamps for top-down games.
/// These functions generate enclosed spaces, corridors, and wall structures
/// using standard Layout primitives.
/// They are agnostic to layer or content type, allowing flexible reuse.
/// </summary>
module TopDown =

  /// <summary>
  /// Direction for corridor construction.
  /// </summary>
  [<Struct>]
  type CorridorDirection =
    | Horizontal
    | Vertical
    | DiagonalDownRight
    | DiagonalDownLeft
    | DiagonalUpRight
    | DiagonalUpLeft

  /// <summary>
  /// Generates a horizontal wall segment.
  /// </summary>
  /// <param name="length">Length of the wall.</param>
  /// <param name="wall">Content for the wall.</param>
  let inline wallSegment
    length
    wall
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    section |> Layout.repeatX 0 0 length wall

  /// <summary>
  /// Generates a wall segment with a gap (doorway opening).
  /// Position and place this where needed - it's just a wall with a 1-tile gap.
  /// </summary>
  /// <param name="length">Total length of the wall including the gap.</param>
  /// <param name="wall">Content for the wall.</param>
  let inline doorway
    length
    wall
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    // Create a wall with a 1-tile gap in the center
    let leftLength = length / 2
    let rightLength = length - leftLength - 1 // -1 for the gap

    section
    |> Layout.repeatX 0 0 leftLength wall
    |> Layout.repeatX (leftLength + 1) 0 rightLength wall

  /// <summary>
  /// Generates a corridor (floor with side walls).
  /// Use Layout.section to position the corridor where needed.
  /// </summary>
  /// <param name="length">Length of the corridor.</param>
  /// <param name="width">Width of the corridor (floor + walls).</param>
  /// <param name="direction">Direction of the corridor.</param>
  /// <param name="floor">Content for the floor.</param>
  /// <param name="wall">Content for the walls.</param>
  let inline corridor
    length
    width
    direction
    floor
    wall
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    match direction with
    | Horizontal ->
      // Horizontal corridor: floor in middle, walls on top and bottom
      let floorWidth = width - 2 // -2 for top and bottom walls

      if floorWidth <= 0 then
        section
      else
        section
        |> Layout.fill 0 1 length floorWidth floor
        |> Layout.repeatX 0 0 length wall // Top wall
        |> Layout.repeatX 0 (width - 1) length wall // Bottom wall

    | Vertical ->
      // Vertical corridor: floor in middle, walls on left and right
      let floorWidth = width - 2 // -2 for left and right walls

      if floorWidth <= 0 then
        section
      else
        section
        |> Layout.fill 1 0 floorWidth length floor
        |> Layout.repeatY 0 0 length wall // Left wall
        |> Layout.repeatY (width - 1) 0 length wall // Right wall

    | DiagonalDownRight ->
      // Diagonal corridor going down-right
      // Draw floor line, then offset walls
      section
      |> Layout.line 0 0 (length - 1) (length - 1) floor
      |> Layout.line 1 0 length (length - 1) wall // Bottom-right wall
      |> Layout.line 0 1 (length - 1) length wall // Top-left wall

    | DiagonalDownLeft ->
      // Diagonal corridor going down-left
      let startX = length - 1

      section
      |> Layout.line startX 0 1 (length - 1) floor
      |> Layout.line startX 1 (length - 1) length wall // Bottom-left wall
      |> Layout.line (startX - 1) 0 0 (length - 1) wall // Top-right wall

    | DiagonalUpRight ->
      // Diagonal corridor going up-right
      section
      |> Layout.line 0 (length - 1) (length - 1) 0 floor
      |> Layout.line 1 (length - 1) length 0 wall // Top-right wall
      |> Layout.line 0 (length - 2) (length - 1) (length - 1) wall // Bottom-left wall

    | DiagonalUpLeft ->
      // Diagonal corridor going up-left
      let startX = length - 1

      section
      |> Layout.line startX (length - 1) 1 0 floor
      |> Layout.line startX (length - 2) (length - 1) (length - 1) wall // Bottom-left wall
      |> Layout.line (startX - 1) (length - 1) 0 0 wall // Top-right wall

  /// <summary>
  /// Generates a rectangular room with walls and floor.
  /// </summary>
  /// <param name="width">Width of the room (including walls).</param>
  /// <param name="height">Height of the room (including walls).</param>
  /// <param name="floor">Content for the floor.</param>
  /// <param name="wall">Content for the walls.</param>
  let inline room
    width
    height
    floor
    wall
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    section
    |> Layout.fill 0 0 width height floor
    |> Layout.border 0 0 width height wall
