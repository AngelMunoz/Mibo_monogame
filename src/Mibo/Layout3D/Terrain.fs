namespace Mibo.Layout3D

/// <summary>
/// A domain module providing standard layout stamps for outdoor 3D terrain.
/// These functions generate landscapes, paths, and terrain features.
/// They are agnostic to content type, allowing flexible reuse.
/// </summary>
module Terrain =

  /// <summary>
  /// Generates a flat ground plane at Y = 0.
  /// </summary>
  /// <param name="width">Width of the ground (X axis).</param>
  /// <param name="depth">Depth of the ground (Z axis).</param>
  /// <param name="content">Content for the ground tiles.</param>
  let inline ground
    width
    depth
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    section |> Layout3D.floorXZ 0 0 0 width depth content

  /// <summary>
  /// Generates an elevated plateau with a flat top and vertical sides.
  /// </summary>
  /// <param name="width">Width of the plateau (X axis).</param>
  /// <param name="depth">Depth of the plateau (Z axis).</param>
  /// <param name="height">Height of the plateau (Y axis).</param>
  /// <param name="top">Content for the top surface.</param>
  /// <param name="side">Content for the vertical sides.</param>
  let inline plateau
    width
    depth
    height
    top
    side
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Fill the volume with sides
    section |> Layout3D.fill 0 0 0 width height depth side |> ignore
    // Top surface
    section |> Layout3D.floorXZ 0 (height - 1) 0 width depth top

  /// <summary>
  /// Clears a pit/depression below ground level.
  /// Position the section where the pit should be carved.
  /// </summary>
  /// <param name="width">Width of the pit (X axis).</param>
  /// <param name="depth">Depth of the pit (Z axis).</param>
  /// <param name="dropHeight">How deep the pit goes (Y levels to clear).</param>
  let inline pit
    width
    depth
    dropHeight
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    section |> Layout3D.clear 0 0 0 width dropHeight depth

  /// <summary>
  /// Generates a ramp rising along the X axis.
  /// </summary>
  /// <param name="width">Width of the ramp (Z axis, perpendicular to slope).</param>
  /// <param name="depth">Depth of the ramp (X axis, direction of slope).</param>
  /// <param name="rise">Height change from start to end (Y axis).</param>
  /// <param name="content">Content for the ramp surface.</param>
  let inline rampX
    width
    depth
    rise
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if depth <= 0 || rise <= 0 then
      section
    else
      for x in 0 .. depth - 1 do
        let y = (x * rise) / depth

        for z in 0 .. width - 1 do
          // Fill from y=0 to calculated height for solid ramp
          for fy in 0..y do
            setLocal x fy z content section

      section

  /// <summary>
  /// Generates a ramp rising along the Z axis.
  /// </summary>
  /// <param name="width">Width of the ramp (X axis, perpendicular to slope).</param>
  /// <param name="depth">Depth of the ramp (Z axis, direction of slope).</param>
  /// <param name="rise">Height change from start to end (Y axis).</param>
  /// <param name="content">Content for the ramp surface.</param>
  let inline rampZ
    width
    depth
    rise
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if depth <= 0 || rise <= 0 then
      section
    else
      for z in 0 .. depth - 1 do
        let y = (z * rise) / depth

        for x in 0 .. width - 1 do
          for fy in 0..y do
            setLocal x fy z content section

      section

  /// <summary>
  /// Generates a path/road connecting waypoints at ground level (Y = 0).
  /// Uses 3D line rasterization between consecutive points.
  /// </summary>
  /// <param name="points">List of (x, y, z) waypoints.</param>
  /// <param name="width">Width of the path (expands perpendicular to direction).</param>
  /// <param name="content">Content for the path.</param>
  let inline path
    (points: (int * int * int) list)
    width
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    match points with
    | []
    | [ _ ] -> section
    | _ ->
      let rec drawSegments pts =
        match pts with
        | (x1, y1, z1) :: (x2, y2, z2) :: rest ->
          // Draw line and expand width
          if width <= 1 then
            section |> Layout3D.line x1 y1 z1 x2 y2 z2 content |> ignore
          else
            // Draw multiple parallel lines for width
            let hw = width / 2

            for offset in -hw .. hw do
              // Approximate perpendicular expansion (simple approach)
              section
              |> Layout3D.line (x1 + offset) y1 z1 (x2 + offset) y2 z2 content
              |> ignore

              section
              |> Layout3D.line x1 y1 (z1 + offset) x2 y2 (z2 + offset) content
              |> ignore

          drawSegments((x2, y2, z2) :: rest)
        | _ -> ()

      drawSegments points
      section

  /// <summary>
  /// Scatters content randomly on a plane at a specific Y level.
  /// Useful for placing items on plateaus or different floor levels.
  /// </summary>
  let inline scatterAt
    y
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    Layout3D.scatterXZ y count seed content section

  /// <summary>
  /// Scatters content randomly on the ground plane (Y = 0).
  /// </summary>
  let inline scatter
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    scatterAt 0 count seed content section

  /// <summary>
  /// Scatters content randomly across a varying surface defined by a height function.
  /// Perfect for placing trees, rocks, or grass on hills and valleys.
  /// </summary>
  let inline scatterSurface
    ([<InlineIfLambda>] heightFn: int -> int -> int)
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let z = rng.Next(0, section.Depth)
      let y = heightFn x z
      setLocal x y z content section

    section

  /// <summary>
  /// Fills the terrain surface with a checkerboard pattern following the height function.
  /// </summary>
  let inline checkerSurface
    ([<InlineIfLambda>] heightFn: int -> int -> int)
    odd
    even
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for fz in 0 .. section.Depth - 1 do
      for fx in 0 .. section.Width - 1 do
        let y = heightFn fx fz
        let content = if (fx + fz) % 2 = 0 then odd else even
        setLocal fx y fz content section

    section

  /// <summary>
  /// Generates content for the terrain surface using a generator function.
  /// The generator is called for each (x, z) coordinate at the height determined by heightFn.
  /// </summary>
  let inline generateSurface
    ([<InlineIfLambda>] heightFn: int -> int -> int)
    ([<InlineIfLambda>] generator: int -> int -> int -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for fz in 0 .. section.Depth - 1 do
      for fx in 0 .. section.Width - 1 do
        let y = heightFn fx fz
        let content = generator fx y fz
        setLocal fx y fz content section

    section

  /// <summary>
  /// Scatters content randomly along a multi-point path at ground level (Y=0).
  /// </summary>
  let inline scatterPath
    (points: (int * int * int) list)
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    match points with
    | []
    | [ _ ] -> section
    | _ ->
      let rng = System.Random(seed)

      for i in 0 .. points.Length - 2 do
        let (x1, y1, z1) = points.[i]
        let (x2, y2, z2) = points.[i + 1]
        // Partition the count roughly between segments based on distance (or just distribute)
        let segCount = max 1 (count / (points.Length - 1))

        section
        |> Layout3D.scatterLine x1 y1 z1 x2 y2 z2 segCount (rng.Next()) content
        |> ignore

      section

  /// <summary>
  /// Scatters a stamp randomly on an XZ plane at a specific Y level.
  /// </summary>
  let inline scatterStampAt
    y
    count
    seed
    ([<InlineIfLambda>] stamp: GridSection3D<'T> -> GridSection3D<'T>)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let z = rng.Next(0, section.Depth)
      section |> Layout3D.section x y z stamp |> ignore

    section

  /// <summary>
  /// Generates terrain using a height function.
  /// For each (x, z), calls heightFn to get the Y height, then fills from Y=0 to that height.
  /// </summary>
  /// <param name="heightFn">Function that returns height for a given (x, z) coordinate.</param>
  /// <param name="content">Content to fill.</param>
  let inline heightmap
    ([<InlineIfLambda>] heightFn: int -> int -> int)
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for z in 0 .. section.Depth - 1 do
      for x in 0 .. section.Width - 1 do
        let h = heightFn x z

        for y in 0 .. min h (section.Height - 1) do
          setLocal x y z content section

    section

  /// <summary>
  /// Generates terrain with distinct layers (e.g., grass on top, dirt below, stone at bottom).
  /// </summary>
  /// <param name="heightFn">Function that returns total height for (x, z).</param>
  /// <param name="topLayer">Content for top layer (depth 1).</param>
  /// <param name="midLayer">Content for middle layer.</param>
  /// <param name="midDepth">Depth of middle layer from top.</param>
  /// <param name="bottomLayer">Content for everything below.</param>
  let inline layeredHeightmap
    ([<InlineIfLambda>] heightFn: int -> int -> int)
    topLayer
    midLayer
    midDepth
    bottomLayer
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for z in 0 .. section.Depth - 1 do
      for x in 0 .. section.Width - 1 do
        let h = min (heightFn x z) (section.Height - 1)

        for y in 0..h do
          let content =
            if y = h then topLayer
            elif y > h - midDepth then midLayer
            else bottomLayer

          setLocal x y z content section

    section
