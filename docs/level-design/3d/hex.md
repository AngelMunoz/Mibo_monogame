---
title: Hexagonal 3D Grid
category: Level Design
categoryindex: 2
index: 27
---

# Hexagonal 3D Grid

The 3D hex grid system extends hexagonal grids into three dimensions. Hex cells are arranged on the XZ plane with vertical layering on the Y axis. It lives in `Mibo.Layout3D`.

## Core Concepts

| Square 3D Grid | Hex 3D Grid | Purpose |
|----------------|-------------|---------|
| `CellGrid3D<'T>` | `HexGrid3D<'T>` | Storage |
| `GridSection3D<'T>` | `HexGrid3DSection<'T>` | Cursor/view |
| `Layout3D` | `HexLayout3D` | DSL module |
| `LayeredGrid3D<'T>` | `LayeredHexGrid3D<'T>` | Multi-layer |

### Axis Convention

- **X/Z** - Hex plane (columns and rows)
- **Y** - Vertical layers

## HexGrid3D - The Storage

```fsharp
open Mibo.Layout3D
open Mibo.Layout
open Microsoft.Xna.Framework

// Create a 10x5x10 hex3D grid
// Width=10 (hex columns), Height=5 (vertical layers), Depth=10 (hex rows)
let grid = HexGrid3D.create 10 5 10 32f 16f Vector3.Zero HexOrientation.PointyTop
```

Parameters:
- `width` - Number of hex columns (X axis)
- `height` - Number of vertical layers (Y axis)
- `depth` - Number of hex rows (Z axis)
- `hexSize` - Hex radius (center to vertex)
- `layerHeight` - Vertical distance between layers
- `origin` - World-space position of hex (0,0,0)
- `orientation` - PointyTop or FlatTop

### Basic Operations

```fsharp
// Set a cell
HexGrid3D.set col row layer myBlock grid

// Get a cell
match HexGrid3D.get col row layer grid with
| ValueSome block -> // use block
| ValueNone -> // empty

// Clear a cell
HexGrid3D.clear col row layer grid

// Get world position
let worldPos = HexGrid3D.getWorldPos col row layer grid  // Vector3
```

### Iteration

```fsharp
// Iterate all populated cells
grid
|> HexGrid3D.iter (fun col row layer block ->
    printfn "Block at (%d, %d, %d)" col row layer
)

// Iterate cells within a bounding box
let bounds = BoundingBox(Vector3(0f, 0f, 0f), Vector3(100f, 50f, 100f))
grid
|> HexGrid3D.iterVolume bounds (fun col row layer block ->
    // render block
)
```

## HexLayout3D DSL

The `HexLayout3D` module provides the full 3D DSL adapted for hex grids.

### Basic Usage

```fsharp
let myGrid =
    HexGrid3D.create 20 10 20 32f 16f Vector3.Zero HexOrientation.PointyTop
    |> HexLayout3D.run (fun section ->
        section
        |> HexLayout3D.fill 0 0 0 20 10 20 FloorBlock
        |> HexLayout3D.shell 0 0 0 20 10 20 WallBlock
        |> HexLayout3D.set 10 5 10 ChestBlock
    )
```

### Volume Primitives

```fsharp
HexLayout3D.set col row layer content section
HexLayout3D.fill col row layer w h d content section
HexLayout3D.clear col row layer w h d section
HexLayout3D.shell col row layer w h d content section  // Hollow box
HexLayout3D.edges col row layer w h d content section  // 12 edges only
HexLayout3D.border col row layer w h d content section // Shell alias
HexLayout3D.rect col row layer w h d borderContent fillContent section
HexLayout3D.corners col row layer w h d content section
```

### Planes

```fsharp
HexLayout3D.floorHex col row layer w d content section  // Horizontal plane
HexLayout3D.wallXY col row layer w h content section    // Vertical XY plane
HexLayout3D.wallYZ col row layer h d content section    // Vertical YZ plane
```

### Repetition

```fsharp
HexLayout3D.repeatX col row layer count content section
HexLayout3D.repeatY col row layer count content section  // Vertical
HexLayout3D.repeatZ col row layer count content section
HexLayout3D.column col row layer height content section  // Alias for repeatY
```

### Geometry

```fsharp
HexLayout3D.line c1 r1 l1 c2 r2 l2 content section
HexLayout3D.sphere cc cr cl radius filled content section
HexLayout3D.cylinder cc cr layer radius height filled content section
```

### Patterns

```fsharp
HexLayout3D.checker oddContent evenContent section
HexLayout3D.checkerHexLayer layer odd even section
HexLayout3D.checkerXY row odd even section
HexLayout3D.checkerYZ col odd even section
HexLayout3D.checkerShell col row layer w h d odd even section
HexLayout3D.scatter3D count seed content section
HexLayout3D.scatterHexLayer layer count seed content section
HexLayout3D.scatterShell col row layer w h d count seed content section
HexLayout3D.generate col row layer w h d (fun c r l -> ...) section
```

### Scoping

```fsharp
section
|> HexLayout3D.section 5 3 0 (fun inner ->
    inner |> HexLayout3D.fill 0 0 0 4 4 4 FloorBlock
)
|> HexLayout3D.padding 2 (fun inner ->
    inner |> HexLayout3D.set 0 0 0 ChestBlock
)
|> HexLayout3D.center 4 4 4 (fun inner ->
    inner |> HexLayout3D.fill 0 0 0 4 4 4 FloorBlock
)
```

### Flow

```fsharp
let stamps = [
    fun s -> s |> HexLayout3D.set 0 0 0 Torch
    fun s -> s |> HexLayout3D.set 0 0 0 Torch
]

section |> HexLayout3D.flowX 5 stamps  // Along hex columns
section |> HexLayout3D.flowY 3 stamps  // Vertical
section |> HexLayout3D.flowZ 5 stamps  // Along hex rows
```

## Spatial Algorithms

The `Hex3DSpatial` module provides 3D hex-aware algorithms:

### Neighbors

```fsharp
// 8 neighbors: 6 hex neighbors on same layer + up/down
let nbrs = Hex3DSpatial.neighbors col row layer grid

// 6 hex neighbors on same layer only
let hexNbrs = Hex3DSpatial.neighborsHex col row layer grid
```

### Distance

```fsharp
// Hex distance on plane + vertical distance
let dist = Hex3DSpatial.distance c1 r1 l1 c2 r2 l2 grid
```

### World to Cell

```fsharp
match Hex3DSpatial.worldToCell worldPos grid with
| ValueSome struct (col, row, layer) -> // found
| ValueNone -> // outside grid
```

### Range

```fsharp
let cells = Hex3DSpatial.inRange col row layer range grid
```

### Line of Sight

```fsharp
let isClear = Hex3DSpatial.lineOfSight c1 r1 l1 c2 r2 l2 isBlocked grid
```

### Flood Fill

```fsharp
let reachable = Hex3DSpatial.floodFill col row layer (fun c r l ->
    match HexGrid3D.get c r l grid with
    | ValueNone -> true
    | ValueSome _ -> false
) grid
```

### A* Pathfinding

```fsharp
let passable c r l =
    match HexGrid3D.get c r l grid with
    | ValueNone -> true
    | ValueSome _ -> false

let cost c1 r1 l1 c2 r2 l2 = 1f

match Hex3DSpatial.findPath startCol startRow startLayer goalCol goalRow goalLayer passable cost grid with
| ValueSome path ->
    for struct (c, r, l) in path do
        printfn "Step: (%d, %d, %d)" c r l
| ValueNone ->
    printfn "No path found"
```

## Layered Composition

```fsharp
let level =
    LayeredHexGrid3D.create 10 5 10 32f 16f Vector3.Zero HexOrientation.PointyTop
    |> LayeredHexLayout3D.layer 0 (fun section ->
        section |> HexLayout3D.fill 0 0 0 10 1 10 GroundBlock
    )
    |> LayeredHexLayout3D.layer 1 (fun section ->
        section |> HexLayout3D.scatter3D 20 42 GrassDecoration
    )
```

## Creating Hex3D Stamps

```fsharp
/// A hexagonal tower
let hexTower radius height floor wall (section: HexGrid3DSection<Block>) =
    section
    |> HexLayout3D.floorHex 0 0 0 (radius * 2) (radius * 2) floor
    |> HexLayout3D.shell 0 0 0 (radius * 2) height (radius * 2) wall

/// A vertical shaft
let shaft width depth height wall (section: HexGrid3DSection<Block>) =
    section
    |> HexLayout3D.shell 0 0 0 width height depth wall
    |> HexLayout3D.clear 1 1 0 (width - 2) (height - 2) (depth - 2)
```
