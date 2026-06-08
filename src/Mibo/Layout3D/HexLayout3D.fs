namespace Mibo.Layout3D

open HexGrid3D

/// <summary>
/// A lightweight "view" or "cursor" into a backing 3D hex grid.
/// Allows defining content using relative coordinates and nesting layouts without copying data.
/// </summary>
[<Struct>]
type HexGrid3DSection<'T> = {
  BackingGrid: HexGrid3D<'T>
  OffsetCol: int
  OffsetRow: int
  OffsetLayer: int
  Width: int
  Height: int
  Depth: int
}

[<AutoOpen>]
module HexLayout3DHelpers =
  /// <summary>Wraps a raw 3D hex grid in a root-level section.</summary>
  let createHex3DSection(grid: HexGrid3D<'T>) : HexGrid3DSection<'T> = {
    BackingGrid = grid
    OffsetCol = 0
    OffsetRow = 0
    OffsetLayer = 0
    Width = grid.Width
    Height = grid.Height
    Depth = grid.Depth
  }

  /// <summary>Internal helper to set a cell using section-relative coordinates.</summary>
  let inline setHex3DLocal
    (lc: int)
    (lr: int)
    (ll: int)
    (content: 'T)
    (section: HexGrid3DSection<'T>)
    : unit =
    let gc = section.OffsetCol + lc
    let gr = section.OffsetRow + lr
    let gl = section.OffsetLayer + ll

    if
      gc >= 0
      && gc < section.BackingGrid.Width
      && gr >= 0
      && gr < section.BackingGrid.Depth
      && gl >= 0
      && gl < section.BackingGrid.Height
    then
      set gc gr gl content section.BackingGrid

  /// <summary>Internal helper to clear a cell using section-relative coordinates.</summary>
  let inline clearHex3DLocal
    (lc: int)
    (lr: int)
    (ll: int)
    (section: HexGrid3DSection<'T>)
    : unit =
    let gc = section.OffsetCol + lc
    let gr = section.OffsetRow + lr
    let gl = section.OffsetLayer + ll

    if
      gc >= 0
      && gc < section.BackingGrid.Width
      && gr >= 0
      && gr < section.BackingGrid.Depth
      && gl >= 0
      && gl < section.BackingGrid.Height
    then
      clear gc gr gl section.BackingGrid

/// <summary>
/// A fluent DSL for composing 3D hex grid layouts.
/// All operations return the modified section to allow chaining (pipeline style).
/// </summary>
module HexLayout3D =
  /// <summary>The entry point for the 3D hex layout DSL.</summary>
  let inline run
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (grid: HexGrid3D<'T>)
    : HexGrid3D<'T> =
    let section = createHex3DSection grid
    let result = f section
    result.BackingGrid

  /// <summary>Creates a sub-section (nested view) at the specified relative coordinates.</summary>
  let inline section
    col
    row
    layer
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let col = max 0 (min parent.Width col)
    let row = max 0 (min parent.Depth row)
    let layer = max 0 (min parent.Height layer)

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetCol = parent.OffsetCol + col
      OffsetRow = parent.OffsetRow + row
      OffsetLayer = parent.OffsetLayer + layer
      Width = max 0 (parent.Width - col)
      Height = max 0 (parent.Height - layer)
      Depth = max 0 (parent.Depth - row)
    }

    f childSection |> ignore
    parent

  /// <summary>Creates a sub-section that is shrunk by 'n' on all sides.</summary>
  let inline padding
    n
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let n = max 0 n

    if n = 0 then
      f parent |> ignore
      parent
    else
      let childSection = {
        BackingGrid = parent.BackingGrid
        OffsetCol = parent.OffsetCol + n
        OffsetRow = parent.OffsetRow + n
        OffsetLayer = parent.OffsetLayer + n
        Width = max 0 (parent.Width - 2 * n)
        Height = max 0 (parent.Height - 2 * n)
        Depth = max 0 (parent.Depth - 2 * n)
      }

      f childSection |> ignore
      parent

  /// <summary>Creates a sub-section with explicit padding for each side.</summary>
  let inline paddingEx
    left
    bottom
    back
    right
    top
    front
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let left = max 0 left
    let bottom = max 0 bottom
    let back = max 0 back
    let right = max 0 right
    let top = max 0 top
    let front = max 0 front

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetCol = parent.OffsetCol + left
      OffsetRow = parent.OffsetRow + back
      OffsetLayer = parent.OffsetLayer + bottom
      Width = max 0 (parent.Width - left - right)
      Height = max 0 (parent.Height - bottom - top)
      Depth = max 0 (parent.Depth - back - front)
    }

    f childSection |> ignore
    parent

  /// <summary>Centers a block of a specific size within the current section.</summary>
  let inline center
    w
    h
    d
    ([<InlineIfLambda>] f: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let w = max 0 (min parent.Width w)
    let h = max 0 (min parent.Height h)
    let d = max 0 (min parent.Depth d)
    let col = (parent.Width - w) / 2
    let row = (parent.Depth - d) / 2
    let layer = (parent.Height - h) / 2

    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetCol = parent.OffsetCol + col
      OffsetRow = parent.OffsetRow + row
      OffsetLayer = parent.OffsetLayer + layer
      Width = w
      Height = h
      Depth = d
    }

    f childSection |> ignore
    parent

  /// <summary>Sets a single cell at (col, row, layer).</summary>
  let inline set
    col
    row
    layer
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    setHex3DLocal col row layer content section
    section

  /// <summary>Repeats content along the X axis (hex columns).</summary>
  let repeatX
    col
    row
    layer
    count
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if
      row >= 0 && row < section.Depth && layer >= 0 && layer < section.Height
    then
      let c1 = max 0 col
      let c2 = min section.Width (col + count)

      if c2 > c1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let d = grid.Depth
        let startCol = section.OffsetCol + c1
        let gr = section.OffsetRow + row
        let gl = section.OffsetLayer + layer
        let idxBase = gl * gw * d + gr * gw + startCol

        for i in 0 .. c2 - c1 - 1 do
          grid.Cells.[idxBase + i] <- ValueSome content

    section

  /// <summary>Repeats content along the Y axis (vertical layers).</summary>
  let repeatY
    col
    row
    layer
    count
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if col >= 0 && col < section.Width && row >= 0 && row < section.Depth then
      let l1 = max 0 layer
      let l2 = min section.Height (layer + count)

      if l2 > l1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let d = grid.Depth
        let wd = gw * d
        let gc = section.OffsetCol + col
        let gr = section.OffsetRow + row
        let startLayer = section.OffsetLayer + l1
        let idxBase = startLayer * wd + gr * gw + gc

        for i in 0 .. l2 - l1 - 1 do
          grid.Cells.[idxBase + i * wd] <- ValueSome content

    section

  /// <summary>Repeats content along the Z axis (hex rows).</summary>
  let repeatZ
    col
    row
    layer
    count
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if
      col >= 0 && col < section.Width && layer >= 0 && layer < section.Height
    then
      let r1 = max 0 row
      let r2 = min section.Depth (row + count)

      if r2 > r1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let d = grid.Depth
        let gc = section.OffsetCol + col
        let startRow = section.OffsetRow + r1
        let gl = section.OffsetLayer + layer
        let idxBase = gl * gw * d + startRow * gw + gc

        for i in 0 .. r2 - r1 - 1 do
          grid.Cells.[idxBase + i * gw] <- ValueSome content

    section

  /// <summary>Places a vertical column of content. Alias for repeatY.</summary>
  let inline column
    col
    row
    layer
    height
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    repeatY col row layer height content section

  /// <summary>Fills a 3D rectangular volume with content.</summary>
  let fill
    col
    row
    layer
    w
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let c1 = max 0 col
    let r1 = max 0 row
    let l1 = max 0 layer
    let c2 = min section.Width (col + w)
    let r2 = min section.Depth (row + d)
    let l2 = min section.Height (layer + h)

    if c2 > c1 && r2 > r1 && l2 > l1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let startCol = section.OffsetCol + c1
      let startRow = section.OffsetRow + r1
      let startLayer = section.OffsetLayer + l1
      let fillW = c2 - c1
      let fillH = l2 - l1
      let fillD = r2 - r1

      for fl in 0 .. fillH - 1 do
        let layerOffset = (startLayer + fl) * wgd

        for fr in 0 .. fillD - 1 do
          let rowOffset = layerOffset + (startRow + fr) * gw

          for fc in 0 .. fillW - 1 do
            grid.Cells.[rowOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Clears a 3D rectangular volume (sets to ValueNone).</summary>
  let clear
    col
    row
    layer
    w
    h
    d
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let c1 = max 0 col
    let r1 = max 0 row
    let l1 = max 0 layer
    let c2 = min section.Width (col + w)
    let r2 = min section.Depth (row + d)
    let l2 = min section.Height (layer + h)

    if c2 > c1 && r2 > r1 && l2 > l1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let startCol = section.OffsetCol + c1
      let startRow = section.OffsetRow + r1
      let startLayer = section.OffsetLayer + l1
      let fillW = c2 - c1
      let fillH = l2 - l1
      let fillD = r2 - r1

      for fl in 0 .. fillH - 1 do
        let layerOffset = (startLayer + fl) * wgd

        for fr in 0 .. fillD - 1 do
          let rowOffset = layerOffset + (startRow + fr) * gw

          for fc in 0 .. fillW - 1 do
            grid.Cells.[rowOffset + startCol + fc] <- ValueNone

    section

  /// <summary>Fills a horizontal hex floor at a specific layer.</summary>
  let floorHex
    col
    row
    layer
    w
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if layer >= 0 && layer < section.Height then
      let c1 = max 0 col
      let r1 = max 0 row
      let c2 = min section.Width (col + w)
      let r2 = min section.Depth (row + d)

      if c2 > c1 && r2 > r1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gd = grid.Depth
        let wgd = gw * gd
        let startCol = section.OffsetCol + c1
        let startRow = section.OffsetRow + r1
        let gl = section.OffsetLayer + layer
        let fillW = c2 - c1
        let fillD = r2 - r1
        let layerOffset = gl * wgd

        for fr in 0 .. fillD - 1 do
          let rowOffset = layerOffset + (startRow + fr) * gw

          for fc in 0 .. fillW - 1 do
            grid.Cells.[rowOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Fills a vertical wall on the XY plane at a specific Z row.</summary>
  let wallXY
    col
    row
    layer
    w
    h
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if row >= 0 && row < section.Depth then
      let c1 = max 0 col
      let l1 = max 0 layer
      let c2 = min section.Width (col + w)
      let l2 = min section.Height (layer + h)

      if c2 > c1 && l2 > l1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gd = grid.Depth
        let wgd = gw * gd
        let startCol = section.OffsetCol + c1
        let gr = section.OffsetRow + row
        let startLayer = section.OffsetLayer + l1
        let fillW = c2 - c1
        let fillH = l2 - l1

        for fl in 0 .. fillH - 1 do
          let layerOffset = (startLayer + fl) * wgd + gr * gw

          for fc in 0 .. fillW - 1 do
            grid.Cells.[layerOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Fills a vertical wall on the YZ plane at a specific X column.</summary>
  let wallYZ
    col
    row
    layer
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if col >= 0 && col < section.Width then
      let r1 = max 0 row
      let l1 = max 0 layer
      let r2 = min section.Depth (row + d)
      let l2 = min section.Height (layer + h)

      if r2 > r1 && l2 > l1 then
        let grid = section.BackingGrid
        let gw = grid.Width
        let gd = grid.Depth
        let wgd = gw * gd
        let gc = section.OffsetCol + col
        let startRow = section.OffsetRow + r1
        let startLayer = section.OffsetLayer + l1
        let fillH = l2 - l1
        let fillD = r2 - r1

        for fl in 0 .. fillH - 1 do
          let layerOffset = (startLayer + fl) * wgd

          for fr in 0 .. fillD - 1 do
            grid.Cells.[layerOffset + (startRow + fr) * gw + gc] <-
              ValueSome content

    section

  /// <summary>Draws a hollow shell (6 faces of a box).</summary>
  let shell
    col
    row
    layer
    w
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    section
    |> floorHex col row layer w d content
    |> floorHex col row (layer + h - 1) w d content
    |> wallXY col row layer w h content
    |> wallXY col row (layer + d - 1) w h content
    |> wallYZ col row layer h d content
    |> wallYZ (col + w - 1) row layer h d content

  /// <summary>Draws only the 12 edges of a box.</summary>
  let edges
    col
    row
    layer
    w
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    for fc in col .. col + w - 1 do
      setHex3DLocal fc row layer content section
      setHex3DLocal fc (row + h - 1) layer content section
      setHex3DLocal fc row (layer + d - 1) content section
      setHex3DLocal fc (row + h - 1) (layer + d - 1) content section

    for fr in row .. row + h - 1 do
      setHex3DLocal col fr layer content section
      setHex3DLocal (col + w - 1) fr layer content section
      setHex3DLocal col fr (layer + d - 1) content section
      setHex3DLocal (col + w - 1) fr (layer + d - 1) content section

    for fl in layer .. layer + d - 1 do
      setHex3DLocal col row fl content section
      setHex3DLocal (col + w - 1) row fl content section
      setHex3DLocal col (row + h - 1) fl content section
      setHex3DLocal (col + w - 1) (row + h - 1) fl content section

    section

  /// <summary>Scatters content randomly on the 12 edges of a box.</summary>
  let scatterEdges
    col
    row
    layer
    w
    h
    d
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if w > 0 && h > 0 && d > 0 then
      let rng = System.Random(seed)

      for _ in 1..count do
        let edge = rng.Next(0, 12)

        match edge with
        | 0 -> setHex3DLocal (col + rng.Next(0, w)) row layer content section
        | 1 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            (row + h - 1)
            layer
            content
            section
        | 2 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            row
            (layer + d - 1)
            content
            section
        | 3 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            (row + h - 1)
            (layer + d - 1)
            content
            section
        | 4 -> setHex3DLocal col (row + rng.Next(0, h)) layer content section
        | 5 ->
          setHex3DLocal
            (col + w - 1)
            (row + rng.Next(0, h))
            layer
            content
            section
        | 6 ->
          setHex3DLocal
            col
            (row + rng.Next(0, h))
            (layer + d - 1)
            content
            section
        | 7 ->
          setHex3DLocal
            (col + w - 1)
            (row + rng.Next(0, h))
            (layer + d - 1)
            content
            section
        | 8 -> setHex3DLocal col row (layer + rng.Next(0, d)) content section
        | 9 ->
          setHex3DLocal
            (col + w - 1)
            row
            (layer + rng.Next(0, d))
            content
            section
        | 10 ->
          setHex3DLocal
            col
            (row + h - 1)
            (layer + rng.Next(0, d))
            content
            section
        | 11 ->
          setHex3DLocal
            (col + w - 1)
            (row + h - 1)
            (layer + rng.Next(0, d))
            content
            section
        | _ -> ()

    section

  /// <summary>Scatters content randomly along a 3D line.</summary>
  let scatterLine
    c1
    r1
    l1
    c2
    r2
    l2
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let dc = abs(c2 - c1)
    let dr = abs(r2 - r1)
    let dl = abs(l2 - l1)
    let dm = max dc (max dr dl)

    if dm > 0 then
      let rng = System.Random(seed)

      for _ in 1..count do
        let t = rng.NextDouble()
        let lc = c1 + int(float(c2 - c1) * t)
        let lr = r1 + int(float(r2 - r1) * t)
        let ll = l1 + int(float(l2 - l1) * t)
        setHex3DLocal lc lr ll content section

    section

  /// <summary>Draws a 3D line using 3D Bresenham's algorithm.</summary>
  let line
    c1
    r1
    l1
    c2
    r2
    l2
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let dc = abs(c2 - c1)
    let dr = abs(r2 - r1)
    let dl = abs(l2 - l1)
    let sc = if c1 < c2 then 1 else -1
    let sr = if r1 < r2 then 1 else -1
    let sl = if l1 < l2 then 1 else -1

    let dm = max dc (max dr dl)
    let mutable c, r, l = c1, r1, l1
    let mutable ec = dm / 2
    let mutable er = dm / 2
    let mutable el = dm / 2

    for _ in 0..dm do
      setHex3DLocal c r l content section
      ec <- ec - dc

      if ec < 0 then
        ec <- ec + dm
        c <- c + sc

      er <- er - dr

      if er < 0 then
        er <- er + dm
        r <- r + sr

      el <- el - dl

      if el < 0 then
        el <- el + dm
        l <- l + sl

    section

  /// <summary>Draws a sphere using distance checks.</summary>
  let sphere
    cc
    cr
    cl
    radius
    filled
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let r2 = radius * radius

    for l in -radius .. radius do
      for r in -radius .. radius do
        for c in -radius .. radius do
          let d2 = c * c + r * r + l * l

          if filled then
            if d2 <= r2 then
              setHex3DLocal (cc + c) (cr + r) (cl + l) content section
          else if d2 <= r2 && d2 >= (radius - 1) * (radius - 1) then
            setHex3DLocal (cc + c) (cr + r) (cl + l) content section

    section

  /// <summary>Draws a cylinder along the Y axis.</summary>
  let cylinder
    cc
    cr
    layer
    radius
    height
    filled
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let r2 = radius * radius

    for fl in layer .. layer + height - 1 do
      for fr in -radius .. radius do
        for fc in -radius .. radius do
          let d2 = fc * fc + fr * fr

          if filled then
            if d2 <= r2 then
              setHex3DLocal (cc + fc) (cr + fr) fl content section
          else if d2 <= r2 && d2 >= (radius - 1) * (radius - 1) then
            setHex3DLocal (cc + fc) (cr + fr) fl content section

    section

  /// <summary>Fills a 3D volume by calling a generator function for each cell.</summary>
  let inline generate
    col
    row
    layer
    w
    h
    d
    ([<InlineIfLambda>] generator: int -> int -> int -> 'T)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let c1 = max 0 col
    let r1 = max 0 row
    let l1 = max 0 layer
    let c2 = min section.Width (col + w)
    let r2 = min section.Depth (row + d)
    let l2 = min section.Height (layer + h)

    if c2 > c1 && r2 > r1 && l2 > l1 then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let startCol = section.OffsetCol + c1
      let startRow = section.OffsetRow + r1
      let startLayer = section.OffsetLayer + l1
      let fillW = c2 - c1
      let fillH = l2 - l1
      let fillD = r2 - r1

      for fl in 0 .. fillH - 1 do
        let ll = layer + fl
        let layerOffset = (startLayer + fl) * wgd

        for fr in 0 .. fillD - 1 do
          let lr = row + fr
          let rowOffset = layerOffset + (startRow + fr) * gw

          for fc in 0 .. fillW - 1 do
            let lc = col + fc

            grid.Cells.[rowOffset + startCol + fc] <-
              ValueSome(generator lc lr ll)

    section

  /// <summary>Fills an entire hex layer by calling a generator function for each cell.</summary>
  let inline generateHexLayer
    layer
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if layer >= 0 && layer < section.Height then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gl = section.OffsetLayer + layer
      let startCol = section.OffsetCol
      let startRow = section.OffsetRow
      let layerOffset = gl * wgd

      for fr in 0 .. section.Depth - 1 do
        let rowOffset = layerOffset + (startRow + fr) * gw

        for fc in 0 .. section.Width - 1 do
          grid.Cells.[rowOffset + startCol + fc] <- ValueSome(generator fc fr)

    section

  /// <summary>Fills an XY slice by calling a generator function for each cell.</summary>
  let inline generateXY
    row
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if row >= 0 && row < section.Depth then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gr = section.OffsetRow + row
      let startCol = section.OffsetCol
      let startLayer = section.OffsetLayer

      for fl in 0 .. section.Height - 1 do
        let layerOffset = (startLayer + fl) * wgd + gr * gw

        for fc in 0 .. section.Width - 1 do
          grid.Cells.[layerOffset + startCol + fc] <- ValueSome(generator fc fl)

    section

  /// <summary>Fills a YZ slice by calling a generator function for each cell.</summary>
  let inline generateYZ
    col
    ([<InlineIfLambda>] generator: int -> int -> 'T)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if col >= 0 && col < section.Width then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gc = section.OffsetCol + col
      let startRow = section.OffsetRow
      let startLayer = section.OffsetLayer

      for fl in 0 .. section.Height - 1 do
        let layerOffset = (startLayer + fl) * wgd

        for fr in 0 .. section.Depth - 1 do
          grid.Cells.[layerOffset + (startRow + fr) * gw + gc] <-
            ValueSome(generator fr fl)

    section

  /// <summary>Iterates over a 3D volume, providing read access to the cells.</summary>
  let inline iter
    col
    row
    layer
    w
    h
    d
    ([<InlineIfLambda>] action: int -> int -> int -> 'T voption -> unit)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let gw = section.BackingGrid.Width
    let gd = section.BackingGrid.Depth

    for fl in layer .. layer + h - 1 do
      for fr in row .. row + d - 1 do
        for fc in col .. col + w - 1 do
          let gc = section.OffsetCol + fc
          let gr = section.OffsetRow + fr
          let gl = section.OffsetLayer + fl

          if
            gc >= 0
            && gc < section.BackingGrid.Width
            && gr >= 0
            && gr < section.BackingGrid.Depth
            && gl >= 0
            && gl < section.BackingGrid.Height
          then
            let idx = gl * gw * gd + gr * gw + gc
            let cell = section.BackingGrid.Cells.[idx]
            action fc fr fl cell

    section

  /// <summary>Transforms existing content in a 3D volume. Empty cells are skipped.</summary>
  let inline map
    col
    row
    layer
    w
    h
    d
    ([<InlineIfLambda>] mapping: 'T -> 'T)
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let gw = section.BackingGrid.Width
    let gd = section.BackingGrid.Depth

    for fl in layer .. layer + h - 1 do
      for fr in row .. row + d - 1 do
        for fc in col .. col + w - 1 do
          let gc = section.OffsetCol + fc
          let gr = section.OffsetRow + fr
          let gl = section.OffsetLayer + fl

          if
            gc >= 0
            && gc < section.BackingGrid.Width
            && gr >= 0
            && gr < section.BackingGrid.Depth
            && gl >= 0
            && gl < section.BackingGrid.Height
          then
            let idx = gl * gw * gd + gr * gw + gc

            match section.BackingGrid.Cells.[idx] with
            | ValueSome content ->
              section.BackingGrid.Cells.[idx] <- ValueSome(mapping content)
            | ValueNone -> ()

    section

  /// <summary>Replaces all occurrences of 'oldContent' with 'newContent'.</summary>
  let replace
    oldContent
    newContent
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let gw = section.BackingGrid.Width
    let gd = section.BackingGrid.Depth

    for l in 0 .. section.Height - 1 do
      for r in 0 .. section.Depth - 1 do
        for c in 0 .. section.Width - 1 do
          let gc = section.OffsetCol + c
          let gr = section.OffsetRow + r
          let gl = section.OffsetLayer + l

          if
            gc >= 0
            && gc < section.BackingGrid.Width
            && gr >= 0
            && gr < section.BackingGrid.Depth
            && gl >= 0
            && gl < section.BackingGrid.Height
          then
            let idx = gl * gw * gd + gr * gw + gc

            match section.BackingGrid.Cells.[idx] with
            | ValueSome cc when cc = oldContent ->
              section.BackingGrid.Cells.[idx] <- ValueSome newContent
            | _ -> ()

    section

  /// <summary>Probabilistically replaces occurrences of 'oldContent' with 'newContent'.</summary>
  let replaceScatter
    oldContent
    newContent
    (probability: float32)
    seed
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)
    let gw = section.BackingGrid.Width
    let gd = section.BackingGrid.Depth

    for l in 0 .. section.Height - 1 do
      for r in 0 .. section.Depth - 1 do
        for c in 0 .. section.Width - 1 do
          let gc = section.OffsetCol + c
          let gr = section.OffsetRow + r
          let gl = section.OffsetLayer + l

          if
            gc >= 0
            && gc < section.BackingGrid.Width
            && gr >= 0
            && gr < section.BackingGrid.Depth
            && gl >= 0
            && gl < section.BackingGrid.Height
          then
            let idx = gl * gw * gd + gr * gw + gc

            match section.BackingGrid.Cells.[idx] with
            | ValueSome cc when cc = oldContent ->
              if float32(rng.NextDouble()) < probability then
                section.BackingGrid.Cells.[idx] <- ValueSome newContent
            | _ -> ()

    section

  /// <summary>Randomly applies a stamp 'count' times within the section.</summary>
  let inline scatterStamp
    count
    seed
    ([<InlineIfLambda>] stamp: HexGrid3DSection<'T> -> HexGrid3DSection<'T>)
    (section': HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let c = rng.Next(0, section'.Width)
      let r = rng.Next(0, section'.Depth)
      let l = rng.Next(0, section'.Height)
      section' |> section c r l stamp |> ignore

    section'

  /// <summary>Sets a cell only if it is currently empty (ValueNone).</summary>
  let setIfEmpty
    col
    row
    layer
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let gc = section.OffsetCol + col
    let gr = section.OffsetRow + row
    let gl = section.OffsetLayer + layer

    if
      gc >= 0
      && gc < section.BackingGrid.Width
      && gr >= 0
      && gr < section.BackingGrid.Depth
      && gl >= 0
      && gl < section.BackingGrid.Height
    then
      let gw = section.BackingGrid.Width
      let gd = section.BackingGrid.Depth
      let idx = gl * gw * gd + gr * gw + gc
      let cell = &section.BackingGrid.Cells.[idx]

      if cell.IsNone then
        cell <- ValueSome content

    section

  /// <summary>Places a sequence of stamps along the X axis with a fixed step.</summary>
  let inline flowX
    step
    (stamps: (HexGrid3DSection<'T> -> HexGrid3DSection<'T>) seq)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let mutable i = 0

    for stamp in stamps do
      section (i * step) 0 0 stamp parent |> ignore
      i <- i + 1

    parent

  /// <summary>Places a sequence of stamps along the Y axis (vertical) with a fixed step.</summary>
  let inline flowY
    step
    (stamps: (HexGrid3DSection<'T> -> HexGrid3DSection<'T>) seq)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let mutable i = 0

    for stamp in stamps do
      section 0 0 (i * step) stamp parent |> ignore
      i <- i + 1

    parent

  /// <summary>Places a sequence of stamps along the Z axis with a fixed step.</summary>
  let inline flowZ
    step
    (stamps: (HexGrid3DSection<'T> -> HexGrid3DSection<'T>) seq)
    (parent: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let mutable i = 0

    for stamp in stamps do
      section 0 (i * step) 0 stamp parent |> ignore
      i <- i + 1

    parent

  /// <summary>Randomly scatters 'count' items within the 3D volume.</summary>
  let scatter3D
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let c = rng.Next(0, section.Width)
      let r = rng.Next(0, section.Depth)
      let l = rng.Next(0, section.Height)
      setHex3DLocal c r l content section

    section

  /// <summary>Randomly scatters 'count' items on a specific hex layer.</summary>
  let scatterHexLayer
    layer
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let c = rng.Next(0, section.Width)
      let r = rng.Next(0, section.Depth)
      setHex3DLocal c r layer content section

    section

  /// <summary>Randomly scatters 'count' items on an XY slice.</summary>
  let scatterXY
    row
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let c = rng.Next(0, section.Width)
      let l = rng.Next(0, section.Height)
      setHex3DLocal c row l content section

    section

  /// <summary>Randomly scatters 'count' items on a YZ slice.</summary>
  let scatterYZ
    col
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let r = rng.Next(0, section.Depth)
      let l = rng.Next(0, section.Height)
      setHex3DLocal col r l content section

    section

  /// <summary>Randomly scatters 'count' items on the 6 faces of a shell.</summary>
  let scatterShell
    col
    row
    layer
    w
    h
    d
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let face = rng.Next(0, 6)

      match face with
      | 0 ->
        setHex3DLocal
          (col + rng.Next(0, w))
          (row + rng.Next(0, d))
          layer
          content
          section
      | 1 ->
        setHex3DLocal
          (col + rng.Next(0, w))
          (row + rng.Next(0, d))
          (layer + h - 1)
          content
          section
      | 2 ->
        setHex3DLocal
          col
          (row + rng.Next(0, d))
          (layer + rng.Next(0, h))
          content
          section
      | 3 ->
        setHex3DLocal
          (col + w - 1)
          (row + rng.Next(0, d))
          (layer + rng.Next(0, h))
          content
          section
      | 4 ->
        setHex3DLocal
          (col + rng.Next(0, w))
          row
          (layer + rng.Next(0, h))
          content
          section
      | 5 ->
        setHex3DLocal
          (col + rng.Next(0, w))
          (row + d - 1)
          (layer + rng.Next(0, h))
          content
          section
      | _ -> ()

    section

  /// <summary>Fills the entire section with a 3D checkerboard pattern.</summary>
  let checker3D
    odd
    even
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let grid = section.BackingGrid
    let gw = grid.Width
    let gd = grid.Depth
    let wgd = gw * gd
    let startCol = section.OffsetCol
    let startRow = section.OffsetRow
    let startLayer = section.OffsetLayer

    for fl in 0 .. section.Height - 1 do
      let layerOffset = (startLayer + fl) * wgd

      for fr in 0 .. section.Depth - 1 do
        let rowOffset = layerOffset + (startRow + fr) * gw

        for fc in 0 .. section.Width - 1 do
          let content = if (fc + fr + fl) % 2 = 0 then odd else even
          grid.Cells.[rowOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Applies a 2D checkerboard pattern to a specific hex layer.</summary>
  let checkerHexLayer
    layer
    odd
    even
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if layer >= 0 && layer < section.Height then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gl = section.OffsetLayer + layer
      let startCol = section.OffsetCol
      let startRow = section.OffsetRow
      let layerOffset = gl * wgd

      for fr in 0 .. section.Depth - 1 do
        let rowOffset = layerOffset + (startRow + fr) * gw

        for fc in 0 .. section.Width - 1 do
          let content = if (fc + fr) % 2 = 0 then odd else even
          grid.Cells.[rowOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Applies a 2D checkerboard pattern to an XY slice.</summary>
  let checkerXY
    row
    odd
    even
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if row >= 0 && row < section.Depth then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gr = section.OffsetRow + row
      let startCol = section.OffsetCol
      let startLayer = section.OffsetLayer

      for fl in 0 .. section.Height - 1 do
        let layerOffset = (startLayer + fl) * wgd + gr * gw

        for fc in 0 .. section.Width - 1 do
          let content = if (fc + fl) % 2 = 0 then odd else even
          grid.Cells.[layerOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Applies a 2D checkerboard pattern to a YZ slice.</summary>
  let checkerYZ
    col
    odd
    even
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if col >= 0 && col < section.Width then
      let grid = section.BackingGrid
      let gw = grid.Width
      let gd = grid.Depth
      let wgd = gw * gd
      let gc = section.OffsetCol + col
      let startRow = section.OffsetRow
      let startLayer = section.OffsetLayer

      for fl in 0 .. section.Height - 1 do
        let layerOffset = (startLayer + fl) * wgd

        for fr in 0 .. section.Depth - 1 do
          let content = if (fr + fl) % 2 = 0 then odd else even

          grid.Cells.[layerOffset + (startRow + fr) * gw + gc] <-
            ValueSome content

    section

  /// <summary>Applies a checkerboard pattern to the 6 faces of a shell.</summary>
  let checkerShell
    col
    row
    layer
    w
    h
    d
    odd
    even
    (section': HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    section'
    |> section col row layer (fun s ->
      s
      |> checkerHexLayer 0 odd even
      |> checkerHexLayer (h - 1) odd even
      |> checkerXY 0 odd even
      |> checkerXY (d - 1) odd even
      |> checkerYZ 0 odd even
      |> checkerYZ (w - 1) odd even)

  /// <summary>Draws a hollow border (6 faces of a box). Alias for shell.</summary>
  let border
    col
    row
    layer
    w
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if w > 0 && h > 0 && d > 0 then
      section
      |> floorHex col row layer w d content
      |> floorHex col row (layer + h - 1) w d content
      |> wallXY col row layer w h content
      |> wallXY col (row + d - 1) layer w h content
      |> wallYZ col row layer h d content
      |> wallYZ (col + w - 1) row layer h d content
    else
      section

  /// <summary>Draws a filled volume with a border.</summary>
  let rect
    col
    row
    layer
    w
    h
    d
    borderContent
    fillContent
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    section
    |> fill col row layer w h d fillContent
    |> border col row layer w h d borderContent

  /// <summary>Draws only the 8 corners of a box.</summary>
  let corners
    col
    row
    layer
    w
    h
    d
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    section
    |> set col row layer content
    |> set (col + w - 1) row layer content
    |> set col (row + h - 1) layer content
    |> set (col + w - 1) (row + h - 1) layer content
    |> set col row (layer + d - 1) content
    |> set (col + w - 1) row (layer + d - 1) content
    |> set col (row + h - 1) (layer + d - 1) content
    |> set (col + w - 1) (row + h - 1) (layer + d - 1) content

  /// <summary>Scatters content randomly on the 6 faces of a box.</summary>
  let scatterBorder
    col
    row
    layer
    w
    h
    d
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    if w > 0 && h > 0 && d > 0 then
      let rng = System.Random(seed)

      for _ in 1..count do
        let face = rng.Next(0, 6)

        match face with
        | 0 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            (row + rng.Next(0, d))
            layer
            content
            section
        | 1 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            (row + rng.Next(0, d))
            (layer + h - 1)
            content
            section
        | 2 ->
          setHex3DLocal
            col
            (row + rng.Next(0, d))
            (layer + rng.Next(0, h))
            content
            section
        | 3 ->
          setHex3DLocal
            (col + w - 1)
            (row + rng.Next(0, d))
            (layer + rng.Next(0, h))
            content
            section
        | 4 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            row
            (layer + rng.Next(0, h))
            content
            section
        | 5 ->
          setHex3DLocal
            (col + rng.Next(0, w))
            (row + d - 1)
            (layer + rng.Next(0, h))
            content
            section
        | _ -> ()

    section

  /// <summary>Applies a checkerboard pattern to the border of a 3D box.</summary>
  let checkerBorder
    col
    row
    layer
    w
    h
    d
    odd
    even
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    for bc in 0 .. w - 1 do
      for bd in 0 .. d - 1 do
        let top = if (bc + bd) % 2 = 0 then odd else even
        let bottom = if (bc + bd + h - 1) % 2 = 0 then odd else even
        setHex3DLocal (col + bc) row (layer + bd) top section
        setHex3DLocal (col + bc) (row + h - 1) (layer + bd) bottom section

    for br in 1 .. h - 2 do
      for bd in 0 .. d - 1 do
        let left = if (br + bd) % 2 = 0 then odd else even
        let right = if (br + bd + w - 1) % 2 = 0 then odd else even
        setHex3DLocal col (row + br) (layer + bd) left section
        setHex3DLocal (col + w - 1) (row + br) (layer + bd) right section

    for bc in 0 .. w - 1 do
      for br in 0 .. h - 1 do
        let front = if (bc + br) % 2 = 0 then odd else even
        let back = if (bc + br + d - 1) % 2 = 0 then odd else even
        setHex3DLocal (col + bc) (row + br) layer front section
        setHex3DLocal (col + bc) (row + br) (layer + d - 1) back section

    section

  /// <summary>Fills the section with a 3D checkerboard pattern.</summary>
  let checker odd even (section: HexGrid3DSection<'T>) : HexGrid3DSection<'T> =
    let grid = section.BackingGrid
    let gw = grid.Width
    let gd = grid.Depth
    let wgd = gw * gd
    let startCol = section.OffsetCol
    let startRow = section.OffsetRow
    let startLayer = section.OffsetLayer

    for fl in 0 .. section.Height - 1 do
      let layerOffset = (startLayer + fl) * wgd

      for fr in 0 .. section.Depth - 1 do
        let rowOffset = layerOffset + (startRow + fr) * gw

        for fc in 0 .. section.Width - 1 do
          let content = if (fc + fr + fl) % 2 = 0 then odd else even
          grid.Cells.[rowOffset + startCol + fc] <- ValueSome content

    section

  /// <summary>Randomly scatters 'count' items within the section. Alias for scatter3D.</summary>
  let scatter
    count
    seed
    content
    (section: HexGrid3DSection<'T>)
    : HexGrid3DSection<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let c = rng.Next(0, section.Width)
      let r = rng.Next(0, section.Depth)
      let l = rng.Next(0, section.Height)
      setHex3DLocal c r l content section

    section
