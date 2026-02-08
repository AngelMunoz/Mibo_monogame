namespace Mibo.Layout3D

/// <summary>
/// A domain module providing standard layout stamps for interior 3D spaces.
/// These functions generate enclosed geometry like rooms, corridors, and architectural elements.
/// They are agnostic to content type, allowing flexible reuse.
/// </summary>
module Interior =

  /// <summary>
  /// Side of a room for doorway/window placement.
  /// </summary>
  [<Struct>]
  type DoorSide =
    | North // Front face (max Z)
    | South // Back face (min Z)
    | East // Right face (max X)
    | West // Left face (min X)

  /// <summary>
  /// Generates an enclosed room with floor, walls, and ceiling.
  /// </summary>
  /// <param name="width">Width of the room (X axis).</param>
  /// <param name="height">Height of the room (Y axis).</param>
  /// <param name="depth">Depth of the room (Z axis).</param>
  /// <param name="floor">Content for the floor.</param>
  /// <param name="wall">Content for the walls.</param>
  /// <param name="ceiling">Content for the ceiling.</param>
  let inline room
    width
    height
    depth
    floor
    wall
    ceiling
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Floor (Y = 0)
    section |> Layout3D.floorXZ 0 0 0 width depth floor |> ignore
    // Ceiling (Y = height - 1)
    section |> Layout3D.floorXZ 0 (height - 1) 0 width depth ceiling |> ignore
    // Four walls
    section |> Layout3D.wallXY 0 0 0 width height wall |> ignore // South wall
    section |> Layout3D.wallXY 0 0 (depth - 1) width height wall |> ignore // North wall
    section |> Layout3D.wallYZ 0 0 0 height depth wall |> ignore // West wall
    section |> Layout3D.wallYZ (width - 1) 0 0 height depth wall |> ignore // East wall
    section

  /// <summary>
  /// Generates a room with floor and walls but no ceiling (open-top).
  /// </summary>
  let inline openRoom
    width
    height
    depth
    floor
    wall
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Floor
    section |> Layout3D.floorXZ 0 0 0 width depth floor |> ignore
    // Four walls
    section |> Layout3D.wallXY 0 0 0 width height wall |> ignore
    section |> Layout3D.wallXY 0 0 (depth - 1) width height wall |> ignore
    section |> Layout3D.wallYZ 0 0 0 height depth wall |> ignore
    section |> Layout3D.wallYZ (width - 1) 0 0 height depth wall |> ignore
    section

  /// <summary>
  /// Generates a horizontal corridor along the X axis (open at both X ends).
  /// </summary>
  /// <param name="length">Length of the corridor (X axis).</param>
  /// <param name="width">Width of the corridor (Z axis).</param>
  /// <param name="height">Height of the corridor (Y axis).</param>
  let inline corridorX
    length
    width
    height
    floor
    wall
    ceiling
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Floor and ceiling
    section |> Layout3D.floorXZ 0 0 0 length width floor |> ignore
    section |> Layout3D.floorXZ 0 (height - 1) 0 length width ceiling |> ignore
    // Side walls (along Z edges)
    section |> Layout3D.wallXY 0 0 0 length height wall |> ignore // South wall (Z = 0)
    section |> Layout3D.wallXY 0 0 (width - 1) length height wall |> ignore // North wall (Z = width - 1)
    section

  /// <summary>
  /// Generates a corridor along the Z axis (open at both Z ends).
  /// </summary>
  let inline corridorZ
    length
    width
    height
    floor
    wall
    ceiling
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Floor and ceiling
    section |> Layout3D.floorXZ 0 0 0 width length floor |> ignore
    section |> Layout3D.floorXZ 0 (height - 1) 0 width length ceiling |> ignore
    // Side walls (along X edges)
    section |> Layout3D.wallYZ 0 0 0 height length wall |> ignore // West wall (X = 0)
    section |> Layout3D.wallYZ (width - 1) 0 0 height length wall |> ignore // East wall (X = width - 1)
    section

  /// <summary>
  /// Clears a doorway opening in a wall. Position the section at the doorway location.
  /// </summary>
  /// <param name="side">Which wall to cut the doorway in.</param>
  /// <param name="doorWidth">Width of the door opening.</param>
  /// <param name="doorHeight">Height of the door opening.</param>
  let inline doorway
    side
    doorWidth
    doorHeight
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let roomWidth = section.Width
    let roomDepth = section.Depth
    let startY = 1
    let endY = min (startY + doorHeight - 1) (section.Height - 2)

    match side with
    | North ->
      // Clear on North face (Z = depth - 1)
      let startX = (roomWidth - doorWidth) / 2

      for dy in startY..endY do
        for dx in 0 .. doorWidth - 1 do
          clearLocal (startX + dx) dy (roomDepth - 1) section
    | South ->
      // Clear on South face (Z = 0)
      let startX = (roomWidth - doorWidth) / 2

      for dy in startY..endY do
        for dx in 0 .. doorWidth - 1 do
          clearLocal (startX + dx) dy 0 section
    | East ->
      // Clear on East face (X = width - 1)
      let startZ = (roomDepth - doorWidth) / 2

      for dy in startY..endY do
        for dz in 0 .. doorWidth - 1 do
          clearLocal (roomWidth - 1) dy (startZ + dz) section
    | West ->
      // Clear on West face (X = 0)
      let startZ = (roomDepth - doorWidth) / 2

      for dy in startY..endY do
        for dz in 0 .. doorWidth - 1 do
          clearLocal 0 dy (startZ + dz) section

    section

  /// <summary>
  /// Generates a staircase with individual steps.
  /// </summary>
  /// <param name="width">Width of the stairs (X axis).</param>
  /// <param name="rise">Total height to climb (number of Y levels).</param>
  /// <param name="run">Total run/depth of stairs (Z axis).</param>
  /// <param name="step">Content for each step.</param>
  let inline stairs
    width
    rise
    run
    step
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if rise <= 0 || run <= 0 then
      section
    else
      let stepDepth = max 1 (run / rise)

      for i in 0 .. rise - 1 do
        let y = i
        let zStart = i * stepDepth

        for z in zStart .. zStart + stepDepth - 1 do
          for x in 0 .. width - 1 do
            setLocal x y z step section

      section

  /// <summary>
  /// Generates a vertical shaft (elevator space, ladder well).
  /// Creates walls with hollow interior.
  /// </summary>
  let inline shaft
    width
    depth
    height
    wall
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Create 4 walls around the shaft
    section |> Layout3D.wallXY 0 0 0 width height wall |> ignore
    section |> Layout3D.wallXY 0 0 (depth - 1) width height wall |> ignore
    section |> Layout3D.wallYZ 0 0 0 height depth wall |> ignore
    section |> Layout3D.wallYZ (width - 1) 0 0 height depth wall |> ignore
    section

  /// <summary>
  /// Generates a vertical pillar/column with distinct base, middle, and top.
  /// </summary>
  let inline pillar
    height
    baseTile
    middleTile
    topTile
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if height <= 0 then
      section
    elif height = 1 then
      section |> Layout3D.set 0 0 0 middleTile
    else
      section |> Layout3D.set 0 0 0 baseTile |> ignore

      for y in 1 .. height - 2 do
        setLocal 0 y 0 middleTile section

      section |> Layout3D.set 0 (height - 1) 0 topTile

  /// <summary>
  /// Clears a window opening in a wall.
  /// </summary>
  /// <param name="side">Which wall to cut the window in.</param>
  /// <param name="windowWidth">Width of the window.</param>
  /// <param name="windowHeight">Height of the window.</param>
  /// <param name="sillHeight">Height from floor to bottom of window.</param>
  let inline window
    side
    windowWidth
    windowHeight
    sillHeight
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let roomWidth = section.Width
    let roomDepth = section.Depth
    let startY = max 1 sillHeight
    let endY = min (startY + windowHeight - 1) (section.Height - 2)

    match side with
    | North ->
      let startX = (roomWidth - windowWidth) / 2

      for dy in startY..endY do
        for dx in 0 .. windowWidth - 1 do
          clearLocal (startX + dx) dy (roomDepth - 1) section
    | South ->
      let startX = (roomWidth - windowWidth) / 2

      for dy in startY..endY do
        for dx in 0 .. windowWidth - 1 do
          clearLocal (startX + dx) dy 0 section
    | East ->
      let startZ = (roomDepth - windowWidth) / 2

      for dy in startY..endY do
        for dz in 0 .. windowWidth - 1 do
          clearLocal (roomWidth - 1) dy (startZ + dz) section
    | West ->
      let startZ = (roomDepth - windowWidth) / 2

      for dy in startY..endY do
        for dz in 0 .. windowWidth - 1 do
          clearLocal 0 dy (startZ + dz) section

    section

  /// <summary>
  /// Scatters content randomly on a specific wall/side of the current section.
  /// </summary>
  let inline scatterWall
    side
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    match side with
    | North -> Layout3D.scatterXY (section.Depth - 1) count seed content section
    | South -> Layout3D.scatterXY 0 count seed content section
    | East -> Layout3D.scatterYZ (section.Width - 1) count seed content section
    | West -> Layout3D.scatterYZ 0 count seed content section

  /// <summary>
  /// Scatters content along all room edges/corners.
  /// </summary>
  let inline scatterEdges
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    Layout3D.scatterEdges 0 0 0 section.Width section.Height section.Depth count seed content

  /// <summary>
  /// Non-destructively decorates existing surfaces by replacing a percentage of tiles.
  /// </summary>
  let inline weather
    oldContent
    newContent
    probability
    seed
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    Layout3D.replaceScatter oldContent newContent probability seed section
