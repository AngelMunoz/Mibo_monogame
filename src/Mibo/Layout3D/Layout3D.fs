namespace Mibo.Layout3D

open CellGrid3D

/// <summary>
/// A lightweight "view" or "cursor" into a backing 3D grid.
/// Allows defining content using relative coordinates (0,0,0 is the section's corner)
/// and nesting layouts without copying data.
/// </summary>
[<Struct>]
type GridSection3D<'T> = {
  BackingGrid: CellGrid3D<'T>
  /// <summary>X offset from the backing grid's origin.</summary>
  OffsetX: int
  /// <summary>Y offset from the backing grid's origin.</summary>
  OffsetY: int
  /// <summary>Z offset from the backing grid's origin.</summary>
  OffsetZ: int
  /// <summary>
  /// The virtual width of this section.
  /// Drawing outside (0..Width-1) is safely ignored (clipped).
  /// </summary>
  Width: int
  /// <summary>
  /// The virtual height of this section.
  /// Drawing outside (0..Height-1) is safely ignored (clipped).
  /// </summary>
  Height: int
  /// <summary>
  /// The virtual depth of this section.
  /// Drawing outside (0..Depth-1) is safely ignored (clipped).
  /// </summary>
  Depth: int
}

[<AutoOpen>]
module Layout3DHelpers =
  /// <summary>
  /// Wraps a raw grid in a root-level section.
  /// </summary>
  let inline createSection(grid: CellGrid3D<'T>) : GridSection3D<'T> = {
    BackingGrid = grid
    OffsetX = 0
    OffsetY = 0
    OffsetZ = 0
    Width = grid.Width
    Height = grid.Height
    Depth = grid.Depth
  }

  /// <summary>
  /// Internal helper to set a cell using section-relative coordinates.
  /// Handles translation to absolute grid coordinates and bounds checking.
  /// </summary>
  let inline setLocal
    (lx: int)
    (ly: int)
    (lz: int)
    (content: 'T)
    (section: GridSection3D<'T>)
    : unit =
    let gx = section.OffsetX + lx
    let gy = section.OffsetY + ly
    let gz = section.OffsetZ + lz

    if
      gx >= 0
      && gx < section.BackingGrid.Width
      && gy >= 0
      && gy < section.BackingGrid.Height
      && gz >= 0
      && gz < section.BackingGrid.Depth
    then
      set gx gy gz content section.BackingGrid

  /// <summary>
  /// Internal helper to clear a cell using section-relative coordinates.
  /// </summary>
  let inline clearLocal
    (lx: int)
    (ly: int)
    (lz: int)
    (section: GridSection3D<'T>)
    : unit =
    let gx = section.OffsetX + lx
    let gy = section.OffsetY + ly
    let gz = section.OffsetZ + lz

    if
      gx >= 0
      && gx < section.BackingGrid.Width
      && gy >= 0
      && gy < section.BackingGrid.Height
      && gz >= 0
      && gz < section.BackingGrid.Depth
    then
      clear gx gy gz section.BackingGrid

/// <summary>
/// A fluent DSL for composing 3D layouts.
/// All operations return the modified section to allow chaining (pipeline style).
/// </summary>
module Layout3D =
  /// <summary>
  /// The entry point for the layout DSL.
  /// Lifts a Grid into a Section, executes the layout function, and returns the modified Grid.
  /// </summary>
  let inline run
    ([<InlineIfLambda>] f: GridSection3D<'T> -> GridSection3D<'T>)
    (grid: CellGrid3D<'T>)
    : CellGrid3D<'T> =
    let section = createSection grid
    let result = f section
    result.BackingGrid

  // ============================================================================
  // Scoping Operations
  // ============================================================================

  /// <summary>
  /// Creates a sub-section (nested view) at the specified relative coordinates.
  /// Useful for defining reusable components that don't know their absolute position.
  /// </summary>
  let inline section
    x
    y
    z
    ([<InlineIfLambda>] f: GridSection3D<'T> -> GridSection3D<'T>)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let x = max 0 (min parent.Width x)
    let y = max 0 (min parent.Height y)
    let z = max 0 (min parent.Depth z)

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetX = parent.OffsetX + x
      OffsetY = parent.OffsetY + y
      OffsetZ = parent.OffsetZ + z
      Width = max 0 (parent.Width - x)
      Height = max 0 (parent.Height - y)
      Depth = max 0 (parent.Depth - z)
    }

    f childSection |> ignore
    parent

  /// <summary>
  /// Creates a sub-section that is shrunk by 'n' on all sides.
  /// </summary>
  let inline padding
    n
    ([<InlineIfLambda>] f: GridSection3D<'T> -> GridSection3D<'T>)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let n = max 0 n

    if n = 0 then
      f parent |> ignore
      parent
    else
      let childSection = {
        BackingGrid = parent.BackingGrid
        OffsetX = parent.OffsetX + n
        OffsetY = parent.OffsetY + n
        OffsetZ = parent.OffsetZ + n
        Width = max 0 (parent.Width - 2 * n)
        Height = max 0 (parent.Height - 2 * n)
        Depth = max 0 (parent.Depth - 2 * n)
      }

      f childSection |> ignore
      parent

  /// <summary>
  /// Creates a sub-section with explicit padding for each side.
  /// Order: left, bottom, back, right, top, front
  /// </summary>
  let inline paddingEx
    left
    bottom
    back
    right
    top
    front
    ([<InlineIfLambda>] f: GridSection3D<'T> -> GridSection3D<'T>)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let left = max 0 left
    let bottom = max 0 bottom
    let back = max 0 back
    let right = max 0 right
    let top = max 0 top
    let front = max 0 front

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetX = parent.OffsetX + left
      OffsetY = parent.OffsetY + bottom
      OffsetZ = parent.OffsetZ + back
      Width = max 0 (parent.Width - left - right)
      Height = max 0 (parent.Height - bottom - top)
      Depth = max 0 (parent.Depth - back - front)
    }

    f childSection |> ignore
    parent

  /// <summary>
  /// Centers a block of a specific size within the current section.
  /// </summary>
  let inline center
    w
    h
    d
    ([<InlineIfLambda>] f: GridSection3D<'T> -> GridSection3D<'T>)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let w = max 0 (min parent.Width w)
    let h = max 0 (min parent.Height h)
    let d = max 0 (min parent.Depth d)
    let x = (parent.Width - w) / 2
    let y = (parent.Height - h) / 2
    let z = (parent.Depth - d) / 2

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetX = parent.OffsetX + x
      OffsetY = parent.OffsetY + y
      OffsetZ = parent.OffsetZ + z
      Width = w
      Height = h
      Depth = d
    }

    f childSection |> ignore
    parent

  // ============================================================================
  // Primitives
  // ============================================================================

  /// <summary>
  /// Sets a single cell at (x, y, z).
  /// </summary>
  let inline set
    x
    y
    z
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    setLocal x y z content section
    section

  /// <summary>
  /// Fills a cuboid volume with content.
  /// </summary>
  let fill
    x
    y
    z
    w
    h
    d
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let x1 = max 0 x
    let y1 = max 0 y
    let z1 = max 0 z
    let x2 = min section.Width (x + w)
    let y2 = min section.Height (y + h)
    let z2 = min section.Depth (z + d)

    if x2 > x1 && y2 > y1 && z2 > z1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gh = grid.Height
      let wh = gw * gh
      let startX = section.OffsetX + x1
      let startY = section.OffsetY + y1
      let startZ = section.OffsetZ + z1
      let fillW = x2 - x1
      let fillH = y2 - y1
      let fillD = z2 - z1

      for fz in 0 .. fillD - 1 do
        let zOffset = (startZ + fz) * wh

        for fy in 0 .. fillH - 1 do
          let yzOffset = zOffset + (startY + fy) * gw

          for fx in 0 .. fillW - 1 do
            grid.Cells.[yzOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Clears (sets to ValueNone) a cuboid volume.
  /// </summary>
  let clear x y z w h d (section: GridSection3D<'T>) : GridSection3D<'T> =
    let x1 = max 0 x
    let y1 = max 0 y
    let z1 = max 0 z
    let x2 = min section.Width (x + w)
    let y2 = min section.Height (y + h)
    let z2 = min section.Depth (z + d)

    if x2 > x1 && y2 > y1 && z2 > z1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gh = grid.Height
      let wh = gw * gh
      let startX = section.OffsetX + x1
      let startY = section.OffsetY + y1
      let startZ = section.OffsetZ + z1
      let fillW = x2 - x1
      let fillH = y2 - y1
      let fillD = z2 - z1

      for fz in 0 .. fillD - 1 do
        let zOffset = (startZ + fz) * wh

        for fy in 0 .. fillH - 1 do
          let yzOffset = zOffset + (startY + fy) * gw

          for fx in 0 .. fillW - 1 do
            grid.Cells.[yzOffset + startX + fx] <- ValueNone

    section

  // ============================================================================
  // Planes (single-cell thickness)
  // ============================================================================

  /// <summary>
  /// Creates a horizontal floor plane (XZ plane) at a specific Y level.
  /// </summary>
  let floorXZ
    x
    y
    z
    w
    d
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if y >= 0 && y < section.Height then
      let x1 = max 0 x
      let z1 = max 0 z
      let x2 = min section.Width (x + w)
      let z2 = min section.Depth (z + d)

      if x2 > x1 && z2 > z1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let startX = section.OffsetX + x1
        let gy = section.OffsetY + y
        let startZ = section.OffsetZ + z1
        let fillW = x2 - x1
        let fillD = z2 - z1

        for fz in 0 .. fillD - 1 do
          let zOffset = (startZ + fz) * wh
          let yzOffset = zOffset + gy * gw

          for fx in 0 .. fillW - 1 do
            grid.Cells.[yzOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Creates a vertical wall plane (XY plane) at a specific Z level.
  /// </summary>
  let wallXY
    x
    y
    z
    w
    h
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if z >= 0 && z < section.Depth then
      let x1 = max 0 x
      let y1 = max 0 y
      let x2 = min section.Width (x + w)
      let y2 = min section.Height (y + h)

      if x2 > x1 && y2 > y1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let startX = section.OffsetX + x1
        let startY = section.OffsetY + y1
        let gz = section.OffsetZ + z
        let fillW = x2 - x1
        let fillH = y2 - y1
        let zOffset = gz * wh

        for fy in 0 .. fillH - 1 do
          let yzOffset = zOffset + (startY + fy) * gw

          for fx in 0 .. fillW - 1 do
            grid.Cells.[yzOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Creates a vertical wall plane (YZ plane) at a specific X level.
  /// </summary>
  let wallYZ
    x
    y
    z
    h
    d
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if x >= 0 && x < section.Width then
      let y1 = max 0 y
      let z1 = max 0 z
      let y2 = min section.Height (y + h)
      let z2 = min section.Depth (z + d)

      if y2 > y1 && z2 > z1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let gx = section.OffsetX + x
        let startY = section.OffsetY + y1
        let startZ = section.OffsetZ + z1
        let fillH = y2 - y1
        let fillD = z2 - z1

        for fz in 0 .. fillD - 1 do
          let zOffset = (startZ + fz) * wh

          for fy in 0 .. fillH - 1 do
            grid.Cells.[zOffset + (startY + fy) * gw + gx] <- ValueSome content

    section

  // ============================================================================
  // 3D Shapes
  // ============================================================================

  /// <summary>
  /// Creates a hollow box (6 faces, empty interior).
  /// </summary>
  let shell
    x
    y
    z
    w
    h
    d
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // Bottom and top faces (XZ planes)
    section |> floorXZ x y z w d content |> ignore
    section |> floorXZ x (y + h - 1) z w d content |> ignore
    // Front and back faces (XY planes)
    section |> wallXY x y z w h content |> ignore
    section |> wallXY x y (z + d - 1) w h content |> ignore
    // Left and right faces (YZ planes)
    section |> wallYZ x y z h d content |> ignore
    section |> wallYZ (x + w - 1) y z h d content |> ignore
    section

  /// <summary>
  /// Creates only the 12 edges of a box (no faces).
  /// </summary>
  let edges
    x
    y
    z
    w
    h
    d
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    // 4 edges along X axis
    for fx in x .. x + w - 1 do
      setLocal fx y z content section
      setLocal fx (y + h - 1) z content section
      setLocal fx y (z + d - 1) content section
      setLocal fx (y + h - 1) (z + d - 1) content section
    // 4 edges along Y axis
    for fy in y .. y + h - 1 do
      setLocal x fy z content section
      setLocal (x + w - 1) fy z content section
      setLocal x fy (z + d - 1) content section
      setLocal (x + w - 1) fy (z + d - 1) content section
    // 4 edges along Z axis
    for fz in z .. z + d - 1 do
      setLocal x y fz content section
      setLocal (x + w - 1) y fz content section
      setLocal x (y + h - 1) fz content section
      setLocal (x + w - 1) (y + h - 1) fz content section

    section

  /// <summary>
  /// Scatters content randomly on the 12 edges of a box.
  /// Ideal for adding localized "noise" (dust, moss, vines) to corners and edges.
  /// </summary>
  let scatterEdges
    x
    y
    z
    w
    h
    d
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let edge = rng.Next(0, 12)

      match edge with
      // 4 edges along X axis
      | 0 -> setLocal (x + rng.Next(0, w)) y z content section
      | 1 -> setLocal (x + rng.Next(0, w)) (y + h - 1) z content section
      | 2 -> setLocal (x + rng.Next(0, w)) y (z + d - 1) content section
      | 3 ->
        setLocal (x + rng.Next(0, w)) (y + h - 1) (z + d - 1) content section
      // 4 edges along Y axis
      | 4 -> setLocal x (y + rng.Next(0, h)) z content section
      | 5 -> setLocal (x + w - 1) (y + rng.Next(0, h)) z content section
      | 6 -> setLocal x (y + rng.Next(0, h)) (z + d - 1) content section
      | 7 ->
        setLocal (x + w - 1) (y + rng.Next(0, h)) (z + d - 1) content section
      // 4 edges along Z axis
      | 8 -> setLocal x y (z + rng.Next(0, d)) content section
      | 9 -> setLocal (x + w - 1) y (z + rng.Next(0, d)) content section
      | 10 -> setLocal x (y + h - 1) (z + rng.Next(0, d)) content section
      | 11 ->
        setLocal (x + w - 1) (y + h - 1) (z + rng.Next(0, d)) content section
      | _ -> ()

    section

  /// <summary>
  /// Scatters content along a 3D line.
  /// Useful for decorating paths, wires, or structural beams.
  /// </summary>
  let scatterLine
    x1
    y1
    z1
    x2
    y2
    z2
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let dx = abs(x2 - x1)
    let dy = abs(y2 - y1)
    let dz = abs(z2 - z1)
    let dm = max dx (max dy dz)

    if dm > 0 then
      let rng = System.Random(seed)

      for _ in 1..count do
        let t = rng.NextDouble()
        let lx = x1 + int(float(x2 - x1) * t)
        let ly = y1 + int(float(y2 - y1) * t)
        let lz = z1 + int(float(z2 - z1) * t)
        setLocal lx ly lz content section

    section

  // ================= ===========================================================
  // Repetition
  // ================= ===========================================================

  /// <summary>
  /// Repeats content along X axis starting at (x, y, z).
  /// </summary>
  let repeatX
    x
    y
    z
    count
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if y >= 0 && y < section.Height && z >= 0 && z < section.Depth then
      let x1 = max 0 x
      let x2 = min section.Width (x + count)

      if x2 > x1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let startX = section.OffsetX + x1
        let gy = section.OffsetY + y
        let gz = section.OffsetZ + z
        let idxBase = gz * wh + gy * gw + startX

        for i in 0 .. x2 - x1 - 1 do
          grid.Cells.[idxBase + i] <- ValueSome content

    section

  /// <summary>
  /// Repeats content along Y axis starting at (x, y, z).
  /// </summary>
  let repeatY
    x
    y
    z
    count
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if x >= 0 && x < section.Width && z >= 0 && z < section.Depth then
      let y1 = max 0 y
      let y2 = min section.Height (y + count)

      if y2 > y1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let gx = section.OffsetX + x
        let startY = section.OffsetY + y1
        let gz = section.OffsetZ + z
        let idxBase = gz * wh + startY * gw + gx

        for i in 0 .. y2 - y1 - 1 do
          grid.Cells.[idxBase + i * gw] <- ValueSome content

    section

  /// <summary>
  /// Repeats content along Z axis starting at (x, y, z).
  /// </summary>
  let repeatZ
    x
    y
    z
    count
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if x >= 0 && x < section.Width && y >= 0 && y < section.Height then
      let z1 = max 0 z
      let z2 = min section.Depth (z + count)

      if z2 > z1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gh = grid.Height
        let wh = gw * gh
        let gx = section.OffsetX + x
        let gy = section.OffsetY + y
        let startZ = section.OffsetZ + z1
        let idxBase = startZ * wh + gy * gw + gx

        for i in 0 .. z2 - z1 - 1 do
          grid.Cells.[idxBase + i * wh] <- ValueSome content

    section

  /// <summary>
  /// Creates a vertical column at (x, z) from y=0 to specified height.
  /// Alias for repeatY starting at y=0.
  /// </summary>
  let inline column
    x
    y
    z
    height
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    repeatY x y z height content section

  // ============================================================================
  // Geometry (3D Rasterization)
  // ============================================================================

  /// <summary>
  /// Draws a 3D line using Bresenham's algorithm extended to 3D.
  /// </summary>
  let line
    x1
    y1
    z1
    x2
    y2
    z2
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let dx = abs(x2 - x1)
    let dy = abs(y2 - y1)
    let dz = abs(z2 - z1)
    let sx = if x1 < x2 then 1 else -1
    let sy = if y1 < y2 then 1 else -1
    let sz = if z1 < z2 then 1 else -1

    let dm = max dx (max dy dz)
    let mutable x, y, z = x1, y1, z1
    let mutable ex = dm / 2
    let mutable ey = dm / 2
    let mutable ez = dm / 2

    for _ in 0..dm do
      setLocal x y z content section
      ex <- ex - dx

      if ex < 0 then
        ex <- ex + dm
        x <- x + sx

      ey <- ey - dy

      if ey < 0 then
        ey <- ey + dm
        y <- y + sy

      ez <- ez - dz

      if ez < 0 then
        ez <- ez + dm
        z <- z + sz

    section

  /// <summary>
  /// Draws a sphere using distance-based rasterization.
  /// </summary>
  let sphere
    cx
    cy
    cz
    radius
    filled
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let r2 = radius * radius

    for z in -radius .. radius do
      for y in -radius .. radius do
        for x in -radius .. radius do
          let d2 = x * x + y * y + z * z

          if filled then
            if d2 <= r2 then
              setLocal (cx + x) (cy + y) (cz + z) content section
          else if
            // Shell only: check if on the surface (within 1 cell of radius)
            d2 <= r2 && d2 >= (radius - 1) * (radius - 1)
          then
            setLocal (cx + x) (cy + y) (cz + z) content section

    section

  /// <summary>
  /// Draws a vertical cylinder (Y-axis aligned).
  /// </summary>
  let cylinder
    cx
    cz
    y
    radius
    height
    filled
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let r2 = radius * radius

    for fy in y .. y + height - 1 do
      for fz in -radius .. radius do
        for fx in -radius .. radius do
          let d2 = fx * fx + fz * fz

          if filled then
            if d2 <= r2 then
              setLocal (cx + fx) fy (cz + fz) content section
          else if
            // Shell only
            d2 <= r2 && d2 >= (radius - 1) * (radius - 1)
          then
            setLocal (cx + fx) fy (cz + fz) content section

    section

  // ============================================================================
  // Procedural
  // ============================================================================

  /// <summary>
  /// Fills a volume by calling a generator function for each cell.
  /// </summary>
  let inline generate
    x
    y
    z
    w
    h
    d
    ([<InlineIfLambda>] generator: int -> int -> int -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let x1 = max 0 x
    let y1 = max 0 y
    let z1 = max 0 z
    let x2 = min section.Width (x + w)
    let y2 = min section.Height (y + h)
    let z2 = min section.Depth (z + d)

    if x2 > x1 && y2 > y1 && z2 > z1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gh = grid.Height
      let wh = gw * gh
      let startX = section.OffsetX + x1
      let startY = section.OffsetY + y1
      let startZ = section.OffsetZ + z1
      let fillW = x2 - x1
      let fillH = y2 - y1
      let fillD = z2 - z1

      for fz in 0 .. fillD - 1 do
        let lz = z1 + fz
        let zOffset = (startZ + fz) * wh

        for fy in 0 .. fillH - 1 do
          let ly = y1 + fy
          let yzOffset = zOffset + (startY + fy) * gw

          for fx in 0 .. fillW - 1 do
            let lx = x1 + fx
            grid.Cells.[yzOffset + startX + fx] <- ValueSome(generator lx ly lz)

    section

  /// <summary>
  /// Randomly places 'count' items within the section volume.
  /// Uses a deterministic seed for reproducible results.
  /// </summary>
  let scatter3D
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let y = rng.Next(0, section.Height)
      let z = rng.Next(0, section.Depth)
      setLocal x y z content section

    section

  /// <summary>
  /// Scatters content on an XZ plane at a specific Y level.
  /// Useful for floors or ceilings at any height.
  /// </summary>
  let scatterXZ
    y
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let z = rng.Next(0, section.Depth)
      setLocal x y z content section

    section

  /// <summary>
  /// Scatters content on an XY plane at a specific Z level.
  /// Useful for decorating front/back walls.
  /// </summary>
  let scatterXY
    z
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let y = rng.Next(0, section.Height)
      setLocal x y z content section

    section

  /// <summary>
  /// Scatters content on a YZ plane at a specific X level.
  /// Useful for decorating side walls.
  /// </summary>
  let scatterYZ
    x
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let y = rng.Next(0, section.Height)
      let z = rng.Next(0, section.Depth)
      setLocal x y z content section

    section

  /// <summary>
  /// Scatters content randomly on the 6 outer faces of a cuboid.
  /// Perfect for adding "noise" (cracks, moss, barnacles) to rooms or boxes.
  /// </summary>
  let scatterShell
    x
    y
    z
    w
    h
    d
    count
    seed
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let face = rng.Next(0, 6)

      match face with
      | 0 ->
        setLocal (x + rng.Next(0, w)) y (z + rng.Next(0, d)) content section // Bottom
      | 1 ->
        setLocal
          (x + rng.Next(0, w))
          (y + h - 1)
          (z + rng.Next(0, d))
          content
          section // Top
      | 2 ->
        setLocal x (y + rng.Next(0, h)) (z + rng.Next(0, d)) content section // West
      | 3 ->
        setLocal
          (x + w - 1)
          (y + rng.Next(0, h))
          (z + rng.Next(0, d))
          content
          section // East
      | 4 ->
        setLocal (x + rng.Next(0, w)) (y + rng.Next(0, h)) z content section // South
      | 5 ->
        setLocal
          (x + rng.Next(0, w))
          (y + rng.Next(0, h))
          (z + d - 1)
          content
          section // North
      | _ -> ()

    section

  /// <summary>
  /// Fills the section with a 3D checkerboard pattern.
  /// </summary>
  let checker3D odd even (section: GridSection3D<'T>) : GridSection3D<'T> =
    let grid = section.BackingGrid
    let gw = grid.Width
    let gh = grid.Height
    let wh = gw * gh
    let startX = section.OffsetX
    let startY = section.OffsetY
    let startZ = section.OffsetZ

    for fz in 0 .. section.Depth - 1 do
      let zOffset = (startZ + fz) * wh

      for fy in 0 .. section.Height - 1 do
        let yzOffset = zOffset + (startY + fy) * gw

        for fx in 0 .. section.Width - 1 do
          let content = if (fx + fy + fz) % 2 = 0 then odd else even
          grid.Cells.[yzOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Fills an XZ plane with a checkerboard pattern at a specific Y level.
  /// Ideal for tiled floors or patterned ceilings.
  /// </summary>
  let checkerXZ y odd even (section: GridSection3D<'T>) : GridSection3D<'T> =
    if y >= 0 && y < section.Height then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gy = section.OffsetY + y
      let startX = section.OffsetX
      let startZ = section.OffsetZ

      for fz in 0 .. section.Depth - 1 do
        let zOffset = (startZ + fz) * wh + gy * gw

        for fx in 0 .. section.Width - 1 do
          let content = if (fx + fz) % 2 = 0 then odd else even
          grid.Cells.[zOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Fills an XY plane with a checkerboard pattern at a specific Z level.
  /// </summary>
  let checkerXY z odd even (section: GridSection3D<'T>) : GridSection3D<'T> =
    if z >= 0 && z < section.Depth then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gz = section.OffsetZ + z
      let startX = section.OffsetX
      let startY = section.OffsetY
      let zOffset = gz * wh

      for fy in 0 .. section.Height - 1 do
        let yzOffset = zOffset + (startY + fy) * gw

        for fx in 0 .. section.Width - 1 do
          let content = if (fx + fy) % 2 = 0 then odd else even
          grid.Cells.[yzOffset + startX + fx] <- ValueSome content

    section

  /// <summary>
  /// Fills a YZ plane with a checkerboard pattern at a specific X level.
  /// </summary>
  let checkerYZ x odd even (section: GridSection3D<'T>) : GridSection3D<'T> =
    if x >= 0 && x < section.Width then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gx = section.OffsetX + x
      let startY = section.OffsetY
      let startZ = section.OffsetZ

      for fz in 0 .. section.Depth - 1 do
        let zOffset = (startZ + fz) * wh

        for fy in 0 .. section.Height - 1 do
          let content = if (fy + fz) % 2 = 0 then odd else even
          grid.Cells.[zOffset + (startY + fy) * gw + gx] <- ValueSome content

    section

  /// <summary>
  /// Applies a checkerboard pattern to the 6 outer faces of a cuboid.
  /// </summary>
  let checkerShell
    x
    y
    z
    w
    h
    d
    odd
    even
    (section': GridSection3D<'T>)
    : GridSection3D<'T> =
    section'
    |> section x y z (fun s ->
      s
      |> checkerXZ 0 odd even
      |> checkerXZ (h - 1) odd even
      |> checkerXY 0 odd even
      |> checkerXY (d - 1) odd even
      |> checkerYZ 0 odd even
      |> checkerYZ (w - 1) odd even)

  /// <summary>
  /// Generates content for an XZ plane using a generator function.
  /// </summary>
  let inline generateXZ
    y
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if y >= 0 && y < section.Height then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gy = section.OffsetY + y
      let startX = section.OffsetX
      let startZ = section.OffsetZ

      for fz in 0 .. section.Depth - 1 do
        let zOffset = (startZ + fz) * wh + gy * gw

        for fx in 0 .. section.Width - 1 do
          grid.Cells.[zOffset + startX + fx] <- ValueSome(generator fx fz)

    section

  /// <summary>
  /// Generates content for an XY plane using a generator function.
  /// </summary>
  let inline generateXY
    z
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if z >= 0 && z < section.Depth then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gz = section.OffsetZ + z
      let startX = section.OffsetX
      let startY = section.OffsetY
      let zOffset = gz * wh

      for fy in 0 .. section.Height - 1 do
        let yzOffset = zOffset + (startY + fy) * gw

        for fx in 0 .. section.Width - 1 do
          grid.Cells.[yzOffset + startX + fx] <- ValueSome(generator fx fy)

    section

  /// <summary>
  /// Generates content for a YZ plane using a generator function.
  /// </summary>
  let inline generateYZ
    x
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    if x >= 0 && x < section.Width then
      let grid = section.BackingGrid
      let gw = grid.Width
      let wh = gw * grid.Height
      let gx = section.OffsetX + x
      let startY = section.OffsetY
      let startZ = section.OffsetZ

      for fz in 0 .. section.Depth - 1 do
        let zOffset = (startZ + fz) * wh

        for fy in 0 .. section.Height - 1 do
          grid.Cells.[zOffset + (startY + fy) * gw + gx] <-
            ValueSome(generator fy fz)

    section

  // ============================================================================
  // Iteration / Transformation
  // ============================================================================

  /// <summary>
  /// Iterates over a volume, providing read access to the cells.
  /// </summary>
  let inline iter
    x
    y
    z
    w
    h
    d
    ([<InlineIfLambda>] action: int -> int -> int -> 'T voption -> unit)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for fz in z .. z + d - 1 do
      for fy in y .. y + h - 1 do
        for fx in x .. x + w - 1 do
          let gx = section.OffsetX + fx
          let gy = section.OffsetY + fy
          let gz = section.OffsetZ + fz

          if
            gx >= 0
            && gx < section.BackingGrid.Width
            && gy >= 0
            && gy < section.BackingGrid.Height
            && gz >= 0
            && gz < section.BackingGrid.Depth
          then
            let cell = CellGrid3D.get gx gy gz section.BackingGrid
            action fx fy fz cell

    section

  /// <summary>
  /// Transforms existing content in a volume. Empty cells are skipped.
  /// </summary>
  let inline map
    x
    y
    z
    w
    h
    d
    ([<InlineIfLambda>] mapping: 'T -> 'T)
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for fz in z .. z + d - 1 do
      for fy in y .. y + h - 1 do
        for fx in x .. x + w - 1 do
          let gx = section.OffsetX + fx
          let gy = section.OffsetY + fy
          let gz = section.OffsetZ + fz

          if
            gx >= 0
            && gx < section.BackingGrid.Width
            && gy >= 0
            && gy < section.BackingGrid.Height
            && gz >= 0
            && gz < section.BackingGrid.Depth
          then
            let idx =
              gx
              + gy * section.BackingGrid.Width
              + gz * section.BackingGrid.Width * section.BackingGrid.Height

            match section.BackingGrid.Cells.[idx] with
            | ValueSome content ->
              section.BackingGrid.Cells.[idx] <- ValueSome(mapping content)
            | ValueNone -> ()

    section

  /// <summary>
  /// Replaces all occurrences of 'oldContent' with 'newContent'.
  /// </summary>
  let replace
    oldContent
    newContent
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    for z in 0 .. section.Depth - 1 do
      for y in 0 .. section.Height - 1 do
        for x in 0 .. section.Width - 1 do
          let gx = section.OffsetX + x
          let gy = section.OffsetY + y
          let gz = section.OffsetZ + z

          if
            gx >= 0
            && gx < section.BackingGrid.Width
            && gy >= 0
            && gy < section.BackingGrid.Height
            && gz >= 0
            && gz < section.BackingGrid.Depth
          then
            let idx =
              gx
              + gy * section.BackingGrid.Width
              + gz * section.BackingGrid.Width * section.BackingGrid.Height

            match section.BackingGrid.Cells.[idx] with
            | ValueSome c when c = oldContent ->
              section.BackingGrid.Cells.[idx] <- ValueSome newContent
            | _ -> ()

    section

  /// <summary>
  /// Probabilistically replaces occurrences of 'oldContent' with 'newContent'.
  /// Perfect for "weathering" or "distressing" large surfaces (e.g. 5% of walls are cracked).
  /// </summary>
  let replaceScatter
    oldContent
    newContent
    (probability: float32)
    seed
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for z in 0 .. section.Depth - 1 do
      for y in 0 .. section.Height - 1 do
        for x in 0 .. section.Width - 1 do
          let gx = section.OffsetX + x
          let gy = section.OffsetY + y
          let gz = section.OffsetZ + z

          if
            gx >= 0
            && gx < section.BackingGrid.Width
            && gy >= 0
            && gy < section.BackingGrid.Height
            && gz >= 0
            && gz < section.BackingGrid.Depth
          then
            let idx =
              gx
              + gy * section.BackingGrid.Width
              + gz * section.BackingGrid.Width * section.BackingGrid.Height

            match section.BackingGrid.Cells.[idx] with
            | ValueSome c when c = oldContent ->
              if float32(rng.NextDouble()) < probability then
                section.BackingGrid.Cells.[idx] <- ValueSome newContent
            | _ -> ()

    section

  /// <summary>
  /// Randomly applies a stamp (a component function) 'count' times within the section.
  /// Use this to populate a volume with complex multi-cell objects (e.g. trees, props).
  /// </summary>
  let inline scatterStamp
    count
    seed
    ([<InlineIfLambda>] stamp: GridSection3D<'T> -> GridSection3D<'T>)
    (section': GridSection3D<'T>)
    : GridSection3D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section'.Width)
      let y = rng.Next(0, section'.Height)
      let z = rng.Next(0, section'.Depth)
      section' |> section x y z stamp |> ignore

    section'

  /// <summary>
  /// Sets a cell only if it is currently empty (ValueNone).
  /// </summary>
  let setIfEmpty
    x
    y
    z
    content
    (section: GridSection3D<'T>)
    : GridSection3D<'T> =
    let gx = section.OffsetX + x
    let gy = section.OffsetY + y
    let gz = section.OffsetZ + z

    if
      gx >= 0
      && gx < section.BackingGrid.Width
      && gy >= 0
      && gy < section.BackingGrid.Height
      && gz >= 0
      && gz < section.BackingGrid.Depth
    then
      let idx =
        gx
        + gy * section.BackingGrid.Width
        + gz * section.BackingGrid.Width * section.BackingGrid.Height

      match section.BackingGrid.Cells.[idx] with
      | ValueNone -> section.BackingGrid.Cells.[idx] <- ValueSome content
      | _ -> ()

    section

  // ============================================================================
  // Flow (arrange stamps along axis)
  // ============================================================================

  /// <summary>
  /// Places a sequence of stamps along X axis with a fixed step.
  /// </summary>
  let inline flowX
    step
    (stamps: (GridSection3D<'T> -> GridSection3D<'T>) seq)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let mutable i = 0

    for stamp in stamps do
      section (i * step) 0 0 stamp parent |> ignore
      i <- i + 1

    parent

  /// <summary>
  /// Places a sequence of stamps along Y axis with a fixed step.
  /// </summary>
  let inline flowY
    step
    (stamps: (GridSection3D<'T> -> GridSection3D<'T>) seq)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let mutable i = 0

    for stamp in stamps do
      section 0 (i * step) 0 stamp parent |> ignore
      i <- i + 1

    parent

  /// <summary>
  /// Places a sequence of stamps along Z axis with a fixed step.
  /// </summary>
  let inline flowZ
    step
    (stamps: (GridSection3D<'T> -> GridSection3D<'T>) seq)
    (parent: GridSection3D<'T>)
    : GridSection3D<'T> =
    let mutable i = 0

    for stamp in stamps do
      section 0 0 (i * step) stamp parent |> ignore
      i <- i + 1

    parent
