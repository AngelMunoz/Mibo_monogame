# Mibo Layout Engine 3D - Design Document

## Overview

This document extends the 2D Layout Engine design to 3D space. The 3D engine follows the same philosophy:

- **Grid-based positioning** - Cells define where things are in 3D space
- **Code-first** - Pure F# DSL, no designer tools
- **Content-agnostic** - Grid stores user-defined `'T`, not voxel blocks
- **Position, not rendering** - Grid handles spatial placement; users handle models, rotation, and collision

## Design Philosophy

The 3D Layout Engine is for **map design**, not voxel rendering:

- Users place 3D models, spawn points, items, triggers at grid positions
- Users bring their own models and handle size/rotation at render time
- Grid provides `getWorldPos` → `Vector3` for placement
- No assumptions about voxel blocks or mesh generation

## Core Abstractions

### CellGrid3D<'T>

3D extension of `CellGrid2D`. Dense flat array with 3D indexing.

```fsharp
[<Struct>]
type CellGrid3D<'T> = {
  Origin: Vector3             // World position of cell (0,0,0)
  CellSize: Vector3           // Size of each cell
  Width: int                  // X dimension
  Height: int                 // Y dimension (vertical)
  Depth: int                  // Z dimension
  Cells: 'T voption[]         // Flat array, indexed as x + y*Width + z*Width*Height
}

module CellGrid3D =
  val create: width:int -> height:int -> depth:int -> cellSize:Vector3 -> origin:Vector3 -> CellGrid3D<'T>
  val set: x:int -> y:int -> z:int -> content:'T -> grid:CellGrid3D<'T> -> unit
  val get: x:int -> y:int -> z:int -> grid:CellGrid3D<'T> -> 'T voption
  val clear: x:int -> y:int -> z:int -> grid:CellGrid3D<'T> -> unit
  val getWorldPos: x:int -> y:int -> z:int -> grid:CellGrid3D<'T> -> Vector3
  val iter: (int -> int -> int -> 'T -> unit) -> grid:CellGrid3D<'T> -> unit
  val iterVolume: bounds:BoundingBox -> (int -> int -> int -> 'T -> unit) -> grid:CellGrid3D<'T> -> unit
```

**Axis Convention:**
- X = width (left/right)
- Y = height (up/down)
- Z = depth (forward/back)

### GridSection3D<'T>

Cursor/view into a 3D grid with relative coordinates.

```fsharp
[<Struct>]
type GridSection3D<'T> = {
  BackingGrid: CellGrid3D<'T>
  OffsetX: int
  OffsetY: int
  OffsetZ: int
  Width: int
  Height: int
  Depth: int
}
```

## Helpers Module

```fsharp
[<AutoOpen>]
module Layout3DHelpers =
  /// Wraps a raw grid in a root-level section.
  val createSection: grid:CellGrid3D<'T> -> GridSection3D<'T>
  /// Internal helper to set a cell using section-relative coordinates.
  val inline setLocal: x:int -> y:int -> z:int -> content:'T -> section:GridSection3D<'T> -> unit
  /// Internal helper to clear a cell using section-relative coordinates.
  val inline clearLocal: x:int -> y:int -> z:int -> section:GridSection3D<'T> -> unit
```

## Layout3D DSL

All operations follow the 2D pattern: `section -> section` for pipeline composition.

```fsharp
module Layout3D =
  // Lifecycle
  val run: f:(GridSection3D<'T> -> GridSection3D<'T>) -> grid:CellGrid3D<'T> -> CellGrid3D<'T>

  // Scoping (creates sub-section, runs f, returns parent)
  val section: x:int -> y:int -> z:int -> f:(GridSection3D<'T> -> GridSection3D<'T>) -> parent:GridSection3D<'T> -> GridSection3D<'T>
  val padding: n:int -> f:(GridSection3D<'T> -> GridSection3D<'T>) -> parent:GridSection3D<'T> -> GridSection3D<'T>
  val paddingEx: left:int -> bottom:int -> back:int -> right:int -> top:int -> front:int
              -> f:(GridSection3D<'T> -> GridSection3D<'T>) -> parent:GridSection3D<'T> -> GridSection3D<'T>
  val center: w:int -> h:int -> d:int -> f:(GridSection3D<'T> -> GridSection3D<'T>) -> parent:GridSection3D<'T> -> GridSection3D<'T>

  // Primitives
  val set: x:int -> y:int -> z:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val fill: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val clear: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Planes (single-cell thickness)
  val floorXZ: x:int -> y:int -> z:int -> w:int -> d:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val wallXY: x:int -> y:int -> z:int -> w:int -> h:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val wallYZ: x:int -> y:int -> z:int -> h:int -> d:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Shell (hollow box - 6 faces)
  val shell: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Edges (12 edges of a box, no faces)
  val edges: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Repetition
  val repeatX: x:int -> y:int -> z:int -> count:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val repeatY: x:int -> y:int -> z:int -> count:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val repeatZ: x:int -> y:int -> z:int -> count:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val column: x:int -> y:int -> z:int -> height:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Geometry (3D rasterization)
  val line: x1:int -> y1:int -> z1:int -> x2:int -> y2:int -> z2:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val sphere: cx:int -> cy:int -> cz:int -> radius:int -> filled:bool -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val cylinder: cx:int -> cz:int -> y:int -> radius:int -> height:int -> filled:bool -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Procedural
  val generate: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> generator:(int -> int -> int -> 'T) -> section:GridSection3D<'T> -> GridSection3D<'T>
  val scatter3D: count:int -> seed:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val checker3D: odd:'T -> even:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Iteration / Transformation
  val iter: x:int -> y:int -> z:int -> w:int -> h:int -> d:int
         -> action:(int -> int -> int -> 'T voption -> unit) -> section:GridSection3D<'T> -> GridSection3D<'T>
  val map: x:int -> y:int -> z:int -> w:int -> h:int -> d:int -> mapping:('T -> 'T)
        -> section:GridSection3D<'T> -> GridSection3D<'T>
  val replace: oldContent:'T -> newContent:'T -> section:GridSection3D<'T> -> GridSection3D<'T>
  val setIfEmpty: x:int -> y:int -> z:int -> content:'T -> section:GridSection3D<'T> -> GridSection3D<'T>

  // Flow (arrange stamps along axis)
  val flowX: step:int -> stamps:seq<GridSection3D<'T> -> GridSection3D<'T>> -> parent:GridSection3D<'T> -> GridSection3D<'T>
  val flowY: step:int -> stamps:seq<GridSection3D<'T> -> GridSection3D<'T>> -> parent:GridSection3D<'T> -> GridSection3D<'T>
  val flowZ: step:int -> stamps:seq<GridSection3D<'T> -> GridSection3D<'T>> -> parent:GridSection3D<'T> -> GridSection3D<'T>
```

## Domain Modules

Just as 2D games distill to **Platformer** and **TopDown** patterns, 3D games distill to:

- **Interior** - Enclosed spaces (FPS, horror, dungeon crawlers, building interiors)
- **Terrain** - Open outdoor spaces (exploration, RTS, open world)

### Interior Module

For enclosed 3D spaces: rooms, corridors, buildings, dungeons.

```fsharp
module Interior =
  type DoorSide = North | East | South | West

  /// Enclosed room with floor, ceiling, and 4 walls
  val room: width:int -> height:int -> depth:int -> floor:'T -> wall:'T -> ceiling:'T
         -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Room with floor and walls but no ceiling (open-top)
  val openRoom: width:int -> height:int -> depth:int -> floor:'T -> wall:'T
             -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Horizontal corridor (floor + walls + ceiling, open ends)
  val corridorX: length:int -> width:int -> height:int -> floor:'T -> wall:'T -> ceiling:'T
              -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Depth corridor (along Z axis)
  val corridorZ: length:int -> width:int -> height:int -> floor:'T -> wall:'T -> ceiling:'T
              -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Doorway - clears a door-sized opening in a wall plane
  val doorway: side:DoorSide -> doorWidth:int -> doorHeight:int
            -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Staircase connecting two Y levels
  val stairs: width:int -> rise:int -> run:int -> step:'T
           -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Vertical shaft (elevator, ladder space)
  val shaft: width:int -> depth:int -> height:int -> wall:'T
          -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Pillar/column
  val pillar: height:int -> base:'T -> middle:'T -> top:'T
           -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Window opening in a wall (clears cells)
  val window: side:DoorSide -> windowWidth:int -> windowHeight:int -> sillHeight:int
           -> (GridSection3D<'T> -> GridSection3D<'T>)
```

### Terrain Module

For outdoor 3D spaces: landscapes, paths, landmarks.

```fsharp
module Terrain =
  /// Flat ground plane at a specific Y level
  val ground: width:int -> depth:int -> content:'T
           -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Elevated plateau (ground + vertical sides)
  val plateau: width:int -> depth:int -> height:int -> top:'T -> side:'T
            -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Depression/pit (clears volume below ground level)
  val pit: width:int -> depth:int -> dropHeight:int
        -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Ramp connecting two Y levels along X or Z
  val rampX: width:int -> depth:int -> rise:int -> content:'T
          -> (GridSection3D<'T> -> GridSection3D<'T>)
  val rampZ: width:int -> depth:int -> rise:int -> content:'T
          -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Path/road at ground level
  val path: points:(int * int * int) list -> width:int -> content:'T
         -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Scattered content on a ground plane (trees, rocks, props)
  val scatter: count:int -> seed:int -> content:'T
            -> (GridSection3D<'T> -> GridSection3D<'T>)

  /// Height-based generation (heightmap-style)
  val heightmap: heightFn:(int -> int -> int) -> content:'T
              -> (GridSection3D<'T> -> GridSection3D<'T>)
```

## Design Decisions

### Rotation and Orientation

The grid stores **position only**. Rotation is:
- Determined at render time by the user
- Derived from context (auto-tiling, neighbor checks)
- Stored in user's cell content type if needed: `type Cell = { Kind: EntityType; Facing: Facing }`

### Multi-Cell Models

Models larger than one cell are a **user concern**:
1. Use anchor-cell only and handle size at render time
2. Create stamps that fill all occupied cells for blocking/collision

### Layered Composition

Unlike 2D where layers are useful for depth-sorting, 3D naturally has Y as height. If users need conceptual "layers" (e.g., separate grids for collision vs. decoration), they can use multiple `CellGrid3D` instances.

## Rendering Integration

### Built-in Renderers (Optional)

```fsharp
module CellGridRenderer3D =
  /// Iterates over populated cells and invokes renderCell with world position.
  val render: CellGrid3D<'T> -> renderCell:(Vector3 -> 'T -> unit) -> unit
```

### Raw Access (For Custom Renderers)

```fsharp
// Access cells directly for custom rendering
for x in 0 .. grid.Width - 1 do
  for y in 0 .. grid.Height - 1 do
    for z in 0 .. grid.Depth - 1 do
      match CellGrid3D.get x y z grid with
      | ValueSome content ->
        let pos = CellGrid3D.getWorldPos x y z grid
        // Custom render logic (spawn model, draw instanced mesh, etc.)
      | ValueNone -> ()
```

## File Structure

```
src/Mibo/Layout3D/
├── Grid3D.fs        # CellGrid3D type and core functions
├── Layout3D.fs      # GridSection3D, Layout3DHelpers, and Layout3D DSL
├── Interior.fs      # Interior domain module (rooms, corridors, etc.)
├── Terrain.fs       # Terrain domain module (ground, plateaus, etc.)
└── Renderer3D.fs    # Optional built-in 3D renderer helper
```

## Usage Example

```fsharp
open Mibo.Layout3D

// Create a dungeon section
let dungeonSection =
    CellGrid3D.create 20 10 20 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Main room
        |> Layout3D.section 0 0 0 (Interior.room 8 4 8 Floor Wall Ceiling)
        // Corridor leading east
        |> Layout3D.section 8 0 3 (Interior.corridorX 6 2 3 Floor Wall Ceiling)
        // Second room
        |> Layout3D.section 14 0 0 (Interior.room 6 4 6 Floor Wall Ceiling)
        // Door between main room and corridor
        |> Layout3D.section 0 0 0 (Interior.doorway East 2 3)
        // Stairs in second room going up
        |> Layout3D.section 15 0 1 (Interior.stairs 2 4 4 StairStep)
    )

// Iterate and spawn models
dungeonSection
|> CellGrid3D.iter (fun x y z content ->
    let worldPos = CellGrid3D.getWorldPos x y z dungeonSection
    spawnModel content worldPos
)
```

## Performance Notes

- **Flat array storage** - O(1) access via index calculation
- **Struct voption** - No heap allocation for empty cells
- **`[<InlineIfLambda>]`** - Zero closure allocation for lambda-taking functions
- **In-place mutation** - All operations mutate the backing array directly

For very large worlds, consider chunking strategies (user-implemented).

## Summary

This 3D design provides:

✅ **Generic cell grids** - User defines content type
✅ **Low-level storage primitives** - Efficient flat array for 3D spatial data
✅ **Raw access** - Direct cell iteration for custom renderers
✅ **Built-in renderers** - Convenience helpers via `CellGridRenderer3D`
✅ **Built-in iteration** - `iterVolume` with bounding box culling
✅ **Zero-copy operations** - In-place mutations for optimal performance
✅ **Composable DSL** - Relative positioning and reuse via `GridSection3D`
✅ **Helpers module** - `setLocal`, `clearLocal`, `createSection`
✅ **Content transformation** - `map`, `replace`, `setIfEmpty`
✅ **Domain Primitives** - Reusable geometry stamps via `Interior` and `Terrain` modules
