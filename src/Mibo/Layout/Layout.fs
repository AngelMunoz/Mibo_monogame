namespace Mibo.Layout

open CellGrid2D

/// <summary>
/// A lightweight "view" or "cursor" into a backing grid.
/// Allows defining content using relative coordinates (0,0 is the section's top-left)
/// and nesting layouts without copying data.
/// </summary>
type GridSection2D<'T> = {
  BackingGrid: CellGrid2D<'T>
  /// <summary>X offset from the backing grid's origin.</summary>
  OffsetX: int
  /// <summary>Y offset from the backing grid's origin.</summary>
  OffsetY: int
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
}

[<AutoOpen>]
module LayoutHelpers =
  /// <summary>
  /// Wraps a raw grid in a root-level section.
  /// </summary>
  let createSection(grid: CellGrid2D<'T>) : GridSection2D<'T> = {
    BackingGrid = grid
    OffsetX = 0
    OffsetY = 0
    Width = grid.Width
    Height = grid.Height
  }

  /// <summary>
  /// Internal helper to set a cell using section-relative coordinates.
  /// Handles translation to absolute grid coordinates and bounds checking.
  /// </summary>
  let inline setLocal
    (lx: int)
    (ly: int)
    (content: 'T)
    (section: GridSection2D<'T>)
    : unit =
    let gx = section.OffsetX + lx
    let gy = section.OffsetY + ly

    if
      gx >= 0
      && gx < section.BackingGrid.Width
      && gy >= 0
      && gy < section.BackingGrid.Height
    then
      set gx gy content section.BackingGrid

/// <summary>
/// A fluent DSL for composing layouts.
/// All operations return the modified section to allow chaining (pipeline style).
/// </summary>
module Layout =
  /// <summary>
  /// The entry point for the layout DSL.
  /// Lifts a Grid into a Section, executes the layout function, and returns the modified Grid.
  /// </summary>
  /// <param name="f">The layout function to execute.</param>
  /// <param name="grid">The grid to modify.</param>
  let inline run
    ([<InlineIfLambda>] f: GridSection2D<'T> -> GridSection2D<'T>)
    (grid: CellGrid2D<'T>)
    : CellGrid2D<'T> =
    let section = createSection grid
    let result = f section
    result.BackingGrid

  /// <summary>
  /// Creates a sub-section (nested view) at the specified relative coordinates.
  /// Useful for defining reusable components (e.g., a "Room" or "Control") that don't know their absolute position.
  /// </summary>
  /// <param name="x">Relative X position.</param>
  /// <param name="y">Relative Y position.</param>
  /// <param name="f">The layout function to run inside the new section.</param>
  let inline section
    x
    y
    ([<InlineIfLambda>] f: GridSection2D<'T> -> GridSection2D<'T>)
    (parent: GridSection2D<'T>)
    : GridSection2D<'T> =
    let childSection = {
      BackingGrid = parent.BackingGrid
      OffsetX = parent.OffsetX + x
      OffsetY = parent.OffsetY + y
      Width = parent.Width - x
      Height = parent.Height - y
    }

    f childSection |> ignore
    parent

  /// <summary>
  /// Sets a single cell at (x, y).
  /// </summary>
  let set x y content (section: GridSection2D<'T>) : GridSection2D<'T> =
    setLocal x y content section
    section

  /// <summary>
  /// Fills a rectangular area with content.
  /// </summary>
  let fill
    x
    y
    width
    height
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    for fx in x .. x + width - 1 do
      for fy in y .. y + height - 1 do
        setLocal fx fy content section

    section

  /// <summary>
  /// Draws a hollow rectangle (border).
  /// </summary>
  let border
    x
    y
    width
    height
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    for bx in x .. x + width - 1 do
      setLocal bx y content section
      setLocal bx (y + height - 1) content section

    for by in y + 1 .. y + height - 2 do
      setLocal x by content section
      setLocal (x + width - 1) by content section

    section

  /// <summary>
  /// Repeats content horizontally 'count' times starting at (x, y).
  /// </summary>
  let repeatX
    x
    y
    count
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    for i in 0 .. count - 1 do
      setLocal (x + i) y content section

    section

  /// <summary>
  /// Repeats content vertically 'count' times starting at (x, y).
  /// </summary>
  let repeatY
    x
    y
    count
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    for i in 0 .. count - 1 do
      setLocal x (y + i) content section

    section

  /// <summary>
  /// Draws a line using Bresenham's line algorithm.
  /// </summary>
  let line
    x1
    y1
    x2
    y2
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    let dx = abs(x2 - x1)
    let dy = -abs(y2 - y1)
    let sx = if x1 < x2 then 1 else -1
    let sy = if y1 < y2 then 1 else -1
    let mutable err = dx + dy
    let mutable cx = x1
    let mutable cy = y1

    while not(cx = x2 && cy = y2) do
      setLocal cx cy content section
      let e2 = 2 * err

      if e2 >= dy then
        err <- err + dy
        cx <- cx + sx

      if e2 <= dx then
        err <- err + dx
        cy <- cy + sy

    setLocal cx cy content section
    section

  /// <summary>
  /// Draws a circle using the integer-only Midpoint Circle Algorithm (Bresenham's).
  /// Significantly faster than distance-based checks.
  /// </summary>
  let circle
    cx
    cy
    radius
    filled
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    let mutable x = radius
    let mutable y = 0
    let mutable err = 1 - x // Decision parameter

    let plot xc yc x y =
      if filled then
        // Scanline fill between symmetry points
        // We draw horizontal lines to take advantage of memory locality
        let drawLine x1 x2 y =
          let startX = min x1 x2
          let endX = max x1 x2

          for i in startX..endX do
            setLocal (xc + i) (yc + y) content section

        drawLine (-x) x y
        drawLine (-x) x (-y)
        drawLine (-y) y x
        drawLine (-y) y (-x)
      else
        setLocal (xc + x) (yc + y) content section
        setLocal (xc - x) (yc + y) content section
        setLocal (xc + x) (yc - y) content section
        setLocal (xc - x) (yc - y) content section
        setLocal (xc + y) (yc + x) content section
        setLocal (xc - y) (yc + x) content section
        setLocal (xc + y) (yc - x) content section
        setLocal (xc - y) (yc - x) content section

    while x >= y do
      plot cx cy x y
      y <- y + 1

      if err < 0 then
        err <- err + 2 * y + 1
      else
        x <- x - 1
        err <- err + 2 * (y - x) + 1

    section

  /// <summary>
  /// Draws a polygon from a set of vertices.
  /// Filled polygons use a scanline algorithm (scan-conversion).
  /// </summary>
  let polygon
    (points: struct (int * int)[])
    filled
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    if points.Length = 0 then
      section
    else
      if filled then
        let mutable minY = System.Int32.MaxValue
        let mutable maxY = System.Int32.MinValue

        for i in 0 .. points.Length - 1 do
          let struct (_, y) = points.[i]

          if y < minY then
            minY <- y

          if y > maxY then
            maxY <- y

        for y in max 0 minY .. min (section.Height - 1) maxY do
          let nodes = System.Collections.Generic.List<int>()
          let mutable j = points.Length - 1

          for i in 0 .. points.Length - 1 do
            let struct (xi, yi) = points.[i]
            let struct (xj, yj) = points.[j]

            if (yi <= y && yj > y) || (yj <= y && yi > y) then
              let x = float xi + float(y - yi) / float(yj - yi) * float(xj - xi)
              nodes.Add(int x)

            j <- i

          nodes.Sort()
          let count = nodes.Count
          let mutable i = 0

          while i < count - 1 do
            for x in nodes.[i] .. nodes.[i + 1] do
              if x >= 0 && x < section.Width then
                setLocal x y content section

            i <- i + 2
      else
        let drawLineSegment (x1, y1) (x2, y2) =
          let dx = abs(x2 - x1)
          let dy = -abs(y2 - y1)
          let sx = if x1 < x2 then 1 else -1
          let sy = if y1 < y2 then 1 else -1
          let mutable err = dx + dy
          let mutable cx = x1
          let mutable cy = y1

          while not(cx = x2 && cy = y2) do
            setLocal cx cy content section
            let e2 = 2 * err

            if e2 >= dy then
              err <- err + dy
              cx <- cx + sx

            if e2 <= dx then
              err <- err + dx
              cy <- cy + sy

          setLocal cx cy content section

        if points.Length > 1 then
          for i in 0 .. points.Length - 2 do
            let struct (x1, y1) = points.[i]
            let struct (x2, y2) = points.[i + 1]
            drawLineSegment (x1, y1) (x2, y2)

          let struct (lastX, lastY) = points.[points.Length - 1]
          let struct (firstX, firstY) = points.[0]
          drawLineSegment (lastX, lastY) (firstX, firstY)

      section

  /// <summary>
  /// Fills the section with a checkerboard pattern.
  /// </summary>
  let checker odd even (section: GridSection2D<'T>) : GridSection2D<'T> =
    for x in 0 .. section.Width - 1 do
      for y in 0 .. section.Height - 1 do
        if (x + y) % 2 = 0 then
          setLocal x y odd section
        else
          setLocal x y even section

    section

  /// <summary>
  /// Randomly places 'count' items within the section.
  /// Uses a deterministic seed for reproducible results.
  /// </summary>
  let scatter
    count
    seed
    content
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    let rng = System.Random(seed)

    for _ in 1..count do
      let x = rng.Next(0, section.Width)
      let y = rng.Next(0, section.Height)
      setLocal x y content section

    section

  /// <summary>
  /// Clears (sets to ValueNone) a rectangular area.
  /// </summary>
  let clear x y width height (section: GridSection2D<'T>) : GridSection2D<'T> =
    for cx in x .. x + width - 1 do
      for cy in y .. y + height - 1 do
        let gx = section.OffsetX + cx
        let gy = section.OffsetY + cy

        if
          gx >= 0
          && gx < section.BackingGrid.Width
          && gy >= 0
          && gy < section.BackingGrid.Height
        then
          section.BackingGrid.Cells.[gx, gy] <- ValueNone

    section

  /// <summary>
  /// Replaces all occurrences of 'oldContent' with 'newContent'.
  /// Uses structural equality (=).
  /// </summary>
  let replace
    oldContent
    newContent
    (section: GridSection2D<'T>)
    : GridSection2D<'T> =
    for x in 0 .. section.Width - 1 do
      for y in 0 .. section.Height - 1 do
        let gx = section.OffsetX + x
        let gy = section.OffsetY + y

        if
          gx >= 0
          && gx < section.BackingGrid.Width
          && gy >= 0
          && gy < section.BackingGrid.Height
        then
          match section.BackingGrid.Cells.[gx, gy] with
          | ValueSome c when c = oldContent ->
            section.BackingGrid.Cells.[gx, gy] <- ValueSome newContent
          | _ -> ()

    section

  /// <summary>
  /// Sets a cell only if it is currently empty (ValueNone).
  /// Useful for non-destructive decoration or filling gaps.
  /// </summary>
  let setIfEmpty x y content (section: GridSection2D<'T>) : GridSection2D<'T> =
    let gx = section.OffsetX + x
    let gy = section.OffsetY + y

    if
      gx >= 0
      && gx < section.BackingGrid.Width
      && gy >= 0
      && gy < section.BackingGrid.Height
    then
      let cell = &section.BackingGrid.Cells.[gx, gy]

      if cell.IsNone then
        cell <- ValueSome content

    section
