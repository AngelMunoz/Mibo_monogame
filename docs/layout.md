---
title: Layout Engine
category: Layout
index: 20
---

# Layout Engine

The Layout engine provides a tile-based level design system for 2D games. It lives in `Mibo.Layout`.

## Core Concepts

The system is built on three primitives:

- `CellGrid2D<'T>` - Storage for tile data
- `GridSection2D<'T>` - A cursor/view into the grid for relative positioning
- Stamps - Functions that transform sections (`GridSection2D<'T> -> GridSection2D<'T>`)

## CellGrid2D - The Storage

A dense 2D array that stores your tile content:

```fsharp
open Mibo.Layout
open Microsoft.Xna.Framework

// Create a 100x50 grid with 32x32 pixel cells
let grid = CellGrid2D.create 100 50 (Vector2(32f, 32f)) Vector2.Zero
```

Each cell holds `'T voption` - either `ValueSome content` or `ValueNone` (empty). This struct-based option type has zero heap allocation per cell.

### Basic Operations

```fsharp
// Set a cell
CellGrid2D.set 5 3 myTile grid

// Get a cell (returns voption)
match CellGrid2D.get 5 3 grid with
| ValueSome tile -> // use tile
| ValueNone -> // empty

// Get world position for a cell
let worldPos = CellGrid2D.getWorldPos 5 3 grid  // Vector2(160f, 96f)
```

### Iteration

```fsharp
// Iterate all populated cells
grid
|> CellGrid2D.iter (fun x y tile ->
    printfn "Tile at (%d, %d)" x y
)

// Iterate only visible cells (culled to viewport)
let viewBounds = Rectangle(cameraX, cameraY, viewportWidth, viewportHeight)
grid
|> CellGrid2D.iterVisible viewBounds (fun x y tile ->
    // render tile at (x, y)
)
```

## GridSection2D - The Cursor

A section is a lightweight view into a grid. It provides:

- **Relative coordinates** - (0, 0) is the section's top-left, not the grid's
- **Bounds clipping** - Drawing outside the section is safely ignored
- **Zero-copy nesting** - Sub-sections reference the same backing grid

You rarely create sections directly - the `Layout.run` function creates the root section for you.

## Layout DSL - Composing Content

The `Layout` module provides a fluent DSL for placing content. All functions return the section, enabling pipeline composition.

### Basic Usage

```fsharp
open Mibo.Layout

let myGrid =
    CellGrid2D.create 20 15 (Vector2(32f, 32f)) Vector2.Zero
    |> Layout.run (fun section ->
        section
        |> Layout.fill 0 0 20 15 FloorTile      // Fill entire area
        |> Layout.border 0 0 20 15 WallTile     // Add border
        |> Layout.set 10 7 ChestTile            // Place item
    )
```

### Scoping with Sections

Create sub-sections for relative positioning:

```fsharp
section
|> Layout.section 5 3 (fun inner ->
    // (0, 0) here maps to (5, 3) in the parent
    inner
    |> Layout.fill 0 0 4 4 FloorTile
)
// Returns to parent section, can continue chaining
|> Layout.section 12 3 (fun inner ->
    inner |> Layout.fill 0 0 4 4 FloorTile
)
```

### Structural Helpers

```fsharp
// Padding - shrink section by N cells on all sides
section |> Layout.padding 2 (fun inner ->
    inner |> Layout.fill 0 0 inner.Width inner.Height content
)

// Center - position a fixed-size block in the center
section |> Layout.center 4 4 (fun inner ->
    inner |> Layout.fill 0 0 4 4 content
)

// Flow - place stamps horizontally or vertically with spacing
section |> Layout.flowX 5 [
    (fun s -> s |> Layout.fill 0 0 3 3 content)
    (fun s -> s |> Layout.fill 0 0 3 3 content)
    (fun s -> s |> Layout.fill 0 0 3 3 content)
]
```

### Primitives

```fsharp
Layout.set x y content section         // Single cell
Layout.fill x y w h content section    // Rectangle
Layout.border x y w h content section  // Hollow rectangle
Layout.repeatX x y count content       // Horizontal line
Layout.repeatY x y count content       // Vertical line
Layout.clear x y w h section           // Clear cells to empty
```

### Geometry

```fsharp
Layout.line x1 y1 x2 y2 content section        // Bresenham line
Layout.circle cx cy radius filled content      // Midpoint circle
Layout.polygon points filled content           // Arbitrary polygon
```

### Patterns

```fsharp
Layout.checker oddContent evenContent section  // Checkerboard
Layout.scatter count seed content section      // Random placement
Layout.generate x y w h (fun x y -> ...) section  // Procedural
```

## Layered Composition

For multi-layer content (background, foreground, decorations), use `LayeredGrid2D`:

```fsharp
let level =
    LayeredGrid2D.create 100 50 (Vector2(32f, 32f)) Vector2.Zero
    |> LayeredLayout.layer 0 (fun section ->
        // Physical/collision layer
        section |> Layout.fill 0 45 100 5 GroundTile
    )
    |> LayeredLayout.layer 1 (fun section ->
        // Decoration layer
        section |> Layout.scatter 50 42 GrassDecoration
    )
```

Layers are created on-demand.

## Creating Your Own Stamps

A **stamp** is simply a function `GridSection2D<'T> -> GridSection2D<'T>`. You can create reusable stamps just like HTML custom elements:

### Simple Stamp

```fsharp
/// A treasure chest on a pedestal
let treasureChest (section: GridSection2D<Tile>) =
    section
    |> Layout.fill 0 1 3 1 PedestalTile   // Base
    |> Layout.set 1 0 ChestTile           // Chest on top
```

### Parameterized Stamp

```fsharp
/// A configurable room with walls and floor
let room width height floor wall (section: GridSection2D<Tile>) =
    section
    |> Layout.fill 0 0 width height floor
    |> Layout.border 0 0 width height wall
```

### Composing Stamps

Stamps compose with `>>` (function composition):

```fsharp
let guardPost =
    room 8 6 FloorTile WallTile
    >> Layout.center 2 1 (treasureChest)
    >> Layout.section 6 2 (torchStand)
```

### Building a Component Library

Organize stamps into domain modules:

```fsharp
module Dungeon =
    let cell = room 5 5 StoneFloor StoneWall

    let corridor length =
        Layout.fill 0 0 length 3 StoneFloor
        >> Layout.repeatX 0 0 length StoneWall
        >> Layout.repeatX 0 2 length StoneWall

    let intersection =
        cell >> Layout.clear 2 0 1 1  // North door
            >> Layout.clear 2 4 1 1   // South door
            >> Layout.clear 0 2 1 1   // West door
            >> Layout.clear 4 2 1 1   // East door
```

Use them:

```fsharp
level
|> LayeredLayout.layer 0 (fun section ->
    section
    |> Layout.section 0 0 Dungeon.cell
    |> Layout.section 5 1 (Dungeon.corridor 10)
    |> Layout.section 15 0 Dungeon.intersection
)
```

### The Stamp Pattern

Think of stamps like HTML elements:

| HTML | Layout Stamps |
|------|---------------|
| `<div>` | `Layout.section` |
| `<div style="padding">` | `Layout.padding` |
| CSS Flexbox | `Layout.flowX`, `Layout.flowY` |
| Custom component | Your stamp function |
| Nesting `<div>`s | Nesting `Layout.section` calls |
| Composing components | Using `>>` |

The key insight: **stamps are just functions**. You can store them, pass them around, compose them, and build complex structures from simple pieces.

## Domain Modules

Mibo includes pre-built stamps for common game types:

- **[Platformer](platformer.md)** - Boxes, platforms, ledges, walls, pillars, stairs, slopes, pits
- **[TopDown](topdown.md)** - Rooms, corridors, wall segments, doorways

These serve as examples and starting points. Copy and modify them for your game's needs.

## Performance Notes

- **In-place mutation** - All operations mutate the backing array directly (O(1) per cell)
- **Zero-copy sections** - Sections are lightweight structs pointing to the same grid
- **Struct voption** - No heap allocation for empty cells
- **`[<InlineIfLambda>]`** - Lambda-taking functions are inlined for zero closure allocation

For large grids (1000+ cells), prefer `Layout.generate` over setting cells individually - it's a single pass with no intermediate allocations.
