---
title: Level Design Overview
category: Level Design
categoryindex: 2
index: 20
---

# Level Design

Mibo provides grid-based layout engines for designing game levels programmatically. The system is content-agnostic and works with any tile/entity type you define.

## Core Philosophy

- **Code-first design** - Define levels as pure F# functions, not designer tools
- **Grid-based positioning** - Cells define spatial positions for your content
- **Composable primitives** - Build complex structures from reusable "stamps"
- **Position, not rendering** - Grid handles spatial placement; you handle models and collision

## 2D vs 3D Layout

Mibo provides separate layout engines for 2D and 3D games:

| Feature | 2D Layout | 3D Layout |
|---------|-----------|-----------|
| **Module** | `Mibo.Layout` | `Mibo.Layout3D` |
| **Dimensions** | X, Y | X, Y, Z |
| **Storage** | `CellGrid2D<'T>` | `CellGrid3D<'T>` |
| **Cursor** | `GridSection2D<'T>` | `GridSection3D<'T>` |
| **World Space** | `Vector2` | `Vector3` |

## Common Patterns

Both engines share the same design patterns:

### Stamps

A **stamp** is a function that transforms a section:

```fsharp
// 2D stamp
type Stamp2D<'T> = GridSection2D<'T> -> GridSection2D<'T>

// 3D stamp
type Stamp3D<'T> = GridSection3D<'T> -> GridSection3D<'T>
```

Stamps compose with `>>` (function composition):

```fsharp
let myStructure =
    room 10 8 floor wall
    >> center 2 2 (treasureChest)
    >> section 8 4 (torchStand)
```

### Scoping

Create nested sections for relative positioning:

```fsharp
// 2D
section |> Layout.section 5 3 (fun inner ->
    // (0, 0) maps to (5, 3) in parent
    inner |> fill 0 0 4 4 content
)

// 3D
section |> Layout3D.section 5 3 0 (fun inner ->
    // (0, 0, 0) maps to (5, 3, 0) in parent
    inner |> fill 0 0 0 4 4 4 content
)
```

### DSL Pipeline

Operations return the section for fluent chaining:

```fsharp
// 2D
let grid =
    CellGrid2D.create 100 50 cellSize origin
    |> Layout.run (fun section ->
        section
        |> fill 0 0 100 50 floor
        |> border 0 0 100 50 wall
        |> set 50 25 chest
    )

// 3D
let grid =
    CellGrid3D.create 100 50 50 cellSize origin
    |> Layout3D.run (fun section ->
        section
        |> fill 0 0 0 100 50 50 floor
        |> shell 0 0 0 100 50 50 wall
        |> set 50 25 25 chest
    )
```

## Content Types

Grids are generic - you define what each cell contains:

```fsharp
// 2D example
type Tile = 
    | Floor of TileType
    | Wall of WallType
    | Prop of PropType
    | Spawn of EntityType

let myGrid = CellGrid2D.create 100 50 cellSize origin

// 3D example
type Cell =
    | Block of BlockType
    | Entity of EntityType
    | SpawnPoint of SpawnType
    | Trigger of TriggerInfo

let my3DGrid = CellGrid3D.create 100 50 50 cellSize origin
```

## World Position Conversion

Convert grid coordinates to world space for rendering:

```fsharp
// 2D
let worldPos = CellGrid2D.getWorldPos x y grid  // Vector2

// 3D
let worldPos = CellGrid3D.getWorldPos x y z grid  // Vector3
```

## Performance

Both engines use zero-cost abstractions:

- **Flat array storage** - O(1) access via index calculation
- **Struct voption** - No heap allocation for empty cells
- **Inline lambdas** - Zero closure allocation for DSL functions
- **In-place mutation** - All operations mutate the backing array directly
- **Zero-copy sections** - Sections are lightweight views into the backing grid

## Iteration

Iterate over populated cells for rendering:

```fsharp
// 2D
grid |> CellGrid2D.iter (fun x y tile ->
    let worldPos = CellGrid2D.getWorldPos x y grid
    renderTile worldPos tile
)

// 3D
grid |> CellGrid3D.iter (fun x y z content ->
    let worldPos = CellGrid3D.getWorldPos x y z grid
    spawnModel worldPos content
)
```

## Domain Modules

Mibo includes pre-built stamps for common game types:

### 2D Games

- **[Platformer](2d/platformer.html)** - Boxes, platforms, ledges, walls, pillars, stairs, slopes, pits
- **[TopDown](2d/topdown.html)** - Rooms, corridors, wall segments, doorways

### 3D Games

- **[Interior](3d/interior.html)** - Rooms, corridors, doorways, stairs, shafts, pillars, windows
- **[Terrain](3d/terrain.html)** - Ground, plateaus, pits, ramps, paths, heightmaps

## Choosing Between 2D and 3D

Use **2D Layout** for:
- Side-scrolling platformers
- Top-down RPGs and roguelikes
- Isometric games (3D projection, 2D layout)
- Tile-based puzzle games

Use **3D Layout** for:
- First-person shooters
- Dungeon crawlers
- Outdoor exploration games
- Voxel-based games

## Getting Started

- **[2D Layout Engine](2d/core.html)** - Core 2D concepts and DSL
- **[Platformer Stamps](2d/platformer.html)** - 2D platformer examples
- **[TopDown Stamps](2d/topdown.html)** - 2D top-down examples
- **[3D Layout Engine](3d/core.html)** - Core 3D concepts and DSL
- **[Interior Stamps](3d/interior.html)** - 3D interior examples
- **[Terrain Stamps](3d/terrain.html)** - 3D terrain examples
