---
title: Hexagonal 2D Grid
category: Level Design
categoryindex: 2
index: 24
---

# Hexagonal 2D Grid

The hex grid system provides a tile-based level design system using hexagonal cells instead of square cells. It lives in `Mibo.Layout` alongside the square grid system.

Hex grids offer advantages for certain game types:

- **Uniform neighbor distance** - All 6 neighbors are equidistant (no diagonal penalty)
- **Natural movement** - 6-way movement feels more organic than 4-way or 8-way
- **Better for strategy games** - Civilization-style games, wargames, and tactical RPGs

## Core Concepts

The hex system mirrors the square grid architecture:

| Square Grid | Hex Grid | Purpose |
|-------------|----------|---------|
| `CellGrid2D<'T>` | `HexGrid<'T>` | Storage |
| `GridSection2D<'T>` | `HexGridSection<'T>` | Cursor/view |
| `Layout` | `HexLayout` | DSL module |
| `LayeredGrid2D<'T>` | `LayeredHexGrid<'T>` | Multi-layer |

## Orientation

Hex grids support two orientations:

```fsharp
open Mibo.Layout

// Pointy-top: vertices at top and bottom
let grid = HexGrid.create 10 10 32f Vector2.Zero HexOrientation.PointyTop

// Flat-top: vertices at left and right
let grid = HexGrid.create 10 10 32f Vector2.Zero HexOrientation.FlatTop
```

The orientation affects:
- Neighbor offset patterns
- World position calculations
- Visual appearance

## HexGrid - The Storage

A dense hex array that stores your tile content:

```fsharp
open Mibo.Layout
open Microsoft.Xna.Framework

// Create a 10x10 hex grid with radius 32, pointy-top orientation
let grid = HexGrid.create 10 10 32f Vector2.Zero HexOrientation.PointyTop
```

Each cell holds `'T voption` - either `ValueSome content` or `ValueNone` (empty).

### Basic Operations

```fsharp
// Set a cell
HexGrid.set 5 3 myTile grid

// Get a cell (returns voption)
match HexGrid.get 5 3 grid with
| ValueSome tile -> // use tile
| ValueNone -> // empty

// Clear a cell
HexGrid.clear 5 3 grid

// Get world position for a cell
let worldPos = HexGrid.getWorldPos 5 3 grid  // Vector2
```

### Iteration

```fsharp
// Iterate all populated cells
grid
|> HexGrid.iter (fun col row tile ->
    printfn "Tile at (%d, %d)" col row
)

// Iterate only visible cells (culled to viewport)
grid
|> HexGrid.iterVisible left top right bottom (fun col row tile ->
    // render tile at (col, row)
)
```

## HexLayout DSL

The `HexLayout` module provides the same fluent DSL as `Layout`, adapted for hex grids.

### Basic Usage

```fsharp
let myGrid =
    HexGrid.create 20 15 32f Vector2.Zero HexOrientation.PointyTop
    |> HexLayout.run (fun section ->
        section
        |> HexLayout.fill 0 0 20 15 FloorTile
        |> HexLayout.border 0 0 20 15 WallTile
        |> HexLayout.set 10 7 ChestTile
    )
```

### Primitives

```fsharp
HexLayout.set col row content section
HexLayout.fill col row w h content section
HexLayout.border col row w h content section
HexLayout.rect col row w h borderContent fillContent section
HexLayout.corners col row w h content section
HexLayout.repeatX col row count content section
HexLayout.repeatY col row count content section
HexLayout.clear col row w h section
```

### Geometry

```fsharp
HexLayout.line c1 r1 c2 r2 content section
HexLayout.circle cc cr radius filled content section
HexLayout.polygon points filled content section
```

### Patterns

```fsharp
HexLayout.checker oddContent evenContent section
HexLayout.checkerBorder col row w h odd even section
HexLayout.scatter count seed content section
HexLayout.scatterBorder col row w h count seed content section
HexLayout.scatterLine c1 r1 c2 r2 count seed content section
HexLayout.generate col row w h (fun c r -> ...) section
```

### Scoping

```fsharp
section
|> HexLayout.section 5 3 (fun inner ->
    inner |> HexLayout.fill 0 0 4 4 FloorTile
)
|> HexLayout.padding 2 (fun inner ->
    inner |> HexLayout.set 0 0 ChestTile
)
|> HexLayout.center 4 4 (fun inner ->
    inner |> HexLayout.fill 0 0 4 4 FloorTile
)
```

### Flow

Place stamps horizontally or vertically with spacing:

```fsharp
let stamps = [
    fun s -> s |> HexLayout.set 0 0 Torch
    fun s -> s |> HexLayout.set 0 0 Torch
    fun s -> s |> HexLayout.set 0 0 Torch
]

section |> HexLayout.flowX 5 stamps
section |> HexLayout.flowY 5 stamps
```

## Spatial Algorithms

The `Hex2DSpatial` module provides algorithms that understand hex topology:

### Neighbor Queries

```fsharp
// Get 6 hex neighbors
let neighbors = Hex2DSpatial.neighbors col row grid
// Returns: [(col-1,row-1); (col,row-1); (col-1,row); (col+1,row); (col-1,row+1); (col,row+1)]
// (offsets depend on orientation and row parity)
```

### Distance

```fsharp
// Hex distance (cube coordinate distance)
let dist = Hex2DSpatial.distance c1 r1 c2 r2 grid
```

### Coordinate Conversion

```fsharp
// Offset to cube coordinates
let struct (q, r, s) = Hex2DSpatial.offsetToCube col row orientation

// Cube to offset coordinates
let struct (col, row) = Hex2DSpatial.cubeToOffset q r orientation

// World position to hex cell
let result = Hex2DSpatial.worldToCell worldPos grid
match result with
| ValueSome struct (col, row) -> // found
| ValueNone -> // outside grid
```

### Range and Shapes

```fsharp
// All cells within N hex steps
let cells = Hex2DSpatial.inRange col row range grid

// Ring of cells at exact distance
let ring = Hex2DSpatial.ring col row radius grid

// Spiral from center outward
let spiral = Hex2DSpatial.spiral col row radius grid
```

### Line of Sight

```fsharp
// Check if line is clear
let isClear = Hex2DSpatial.lineOfSight c1 r1 c2 r2 isBlocked grid

// Get visible cells along line
let visible = Hex2DSpatial.lineOfSightCells c1 r1 c2 r2 isBlocked grid
```

### Flood Fill

```fsharp
// BFS flood fill
let reachable = Hex2DSpatial.floodFill col row (fun c r ->
    match HexGrid.get c r grid with
    | ValueNone -> true
    | ValueSome _ -> false
) grid
```

### A* Pathfinding

```fsharp
let passable c r =
    match HexGrid.get c r grid with
    | ValueNone -> true
    | ValueSome _ -> false

let cost c1 r1 c2 r2 = 1f  // uniform cost

match Hex2DSpatial.findPath startCol startRow goalCol goalRow passable cost grid with
| ValueSome path ->
    // path is array of (col, row) coordinates
    for struct (c, r) in path do
        printfn "Step: (%d, %d)" c r
| ValueNone ->
    printfn "No path found"
```

## Layered Composition

For multi-layer content, use `LayeredHexGrid`:

```fsharp
let level =
    LayeredHexGrid.create 10 10 32f Vector2.Zero HexOrientation.PointyTop
    |> LayeredHexLayout.layer 0 (fun section ->
        section |> HexLayout.fill 0 0 10 10 GroundTile
    )
    |> LayeredHexLayout.layer 1 (fun section ->
        section |> HexLayout.scatter 20 42 GrassDecoration
    )
```

## Creating Hex Stamps

Stamps work the same way as with square grids:

```fsharp
/// A hexagonal room
let hexRoom radius floor wall (section: HexGridSection<Tile>) =
    section
    |> HexLayout.fill 0 0 (radius * 2) (radius * 2) floor
    |> HexLayout.border 0 0 (radius * 2) (radius * 2) wall

/// A corridor connecting two points
let corridor length floor wall (section: HexGridSection<Tile>) =
    section
    |> HexLayout.fill 0 0 length 3 floor
    |> HexLayout.repeatX 0 0 length wall
    |> HexLayout.repeatX 0 2 length wall
```

## Choosing Hex vs Square

Use **hex grids** for:
- Strategy and tactics games (Civ-like)
- Games where uniform neighbor distance matters
- Board game adaptations
- Games with 6-directional movement

Use **square grids** for:
- Platformers and side-scrollers
- Games with axis-aligned movement
- Simpler collision detection
- Lower computational overhead
