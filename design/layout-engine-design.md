# Mibo Layout Engine - Minimalist Design v3

## Design Goals

1. **Author maps/levels** - Code-first, declarative grid-based layout
2. **Code-first** - No designer tools, pure F# composition
3. **Integrate with Mibo renderers** - Built-in convenience + raw data access
4. **Declarative** - Functional composition, not imperative mutation

## Core Abstraction

The entire system is built on one primitive: **the generic cell grid**.

### CellGrid2D<'T>

```fsharp
[<Struct>]
type CellGrid2D<'T> = {
  Origin: Vector2             // World position of cell (0,0)
  CellSize: Vector2           // Size of each cell
  Width: int
  Height: int
  Cells: Array2D<'T voption>  // User content per cell
}

module CellGrid2D =
  val create: width:int -> height:int -> cellSize:Vector2 -> origin:Vector2 -> CellGrid2D<'T>
  val set: x:int -> y:int -> content:'T -> CellGrid2D<'T> -> CellGrid2D<'T>
  val get: x:int -> y:int -> CellGrid2D<'T> -> 'T voption
  val getWorldPos: x:int -> y:int -> CellGrid2D<'T> -> Vector2
  val iter: (int -> int -> 'T -> unit) -> CellGrid2D<'T> -> unit
  val iterVisible: bounds:Rectangle -> (int -> int -> 'T -> unit) -> CellGrid2D<'T> -> unit
```

### CellGrid3D<'T>

```fsharp
[<Struct>]
type LayoutPlane = XY | XZ | YZ

[<Struct>]
type CellGrid3D<'T> = {
  Origin: Vector3
  CellSize: Vector2
  Plane: LayoutPlane
  Width: int
  Height: int
  Cells: Array2D<'T voption>
}

module CellGrid3D =
  val create: width:int -> height:int -> cellSize:Vector2 -> plane:LayoutPlane -> origin:Vector3 -> CellGrid3D<'T>
  val set: x:int -> y:int -> content:'T -> CellGrid3D<'T> -> CellGrid3D<'T>
  val get: x:int -> y:int -> CellGrid3D<'T> -> 'T voption
  val getWorldPos: x:int -> y:int -> CellGrid3D<'T> -> Vector3
  val iter: (int -> int -> 'T -> unit) -> CellGrid3D<'T> -> unit
  val iterVisible: frustum:BoundingFrustum -> (int -> int -> 'T -> unit) -> CellGrid3D<'T> -> unit
```

## Design Decisions

### Why Generic?

Users define `'T`. Examples:

```fsharp
// Simple tile ID
let grid1 = CellGrid2D<int>.create 10 10 (Vector2(32f, 32f)) Vector2.Zero

// Multiple layers with discriminated union
type Cell = Tile of TileData | Decor of DecorData | Empty
let grid2 = CellGrid2D<Cell>.create 20 20 size origin

// Separate grids for different types (best performance)
let terrain = CellGrid2D<TileData>.create 100 50 size origin
let triggers = CellGrid2D<TriggerData>.create 100 50 size origin
```

### Why Array2D<'T voption>?

- Empty cells are common in maps
- `voption` is a struct type (zero heap allocation per cell)
- Array2D provides contiguous memory layout for cache-friendly iteration
- ValueSome/ValueNone pattern matching is idiomatic F#

## Multi-Grid Composition

For complex levels with multiple cell types, users compose grids:

```fsharp
[<Struct>]
type Level = {
  Terrain: CellGrid2D<TileData>
  Decorations: CellGrid2D<Decoration>
  Triggers: CellGrid2D<Trigger>
}
```

Or use zoned composition:

```fsharp
[<Struct>]
type Zone<'T> = {
  Name: string
  Offset: Vector2
  Grid: CellGrid2D<'T>
}

[<Struct>]
type Level<'T> = {
  Zones: Zone<'T> list
}
```

## Rendering Integration

### Built-in Renderers (Optional)

```fsharp
module CellGridRenderer2D =
  val render: CellGrid2D<'T> -> renderCell:(Vector2 -> 'T -> unit) -> unit

module CellGridRenderer3D =
  val render: CellGrid3D<'T> -> renderCell:(Vector3 -> 'T -> unit) -> unit
```

### Raw Access (For Custom Renderers)

```fsharp
// Access cells directly for custom rendering
for x in 0 .. grid.Width - 1 do
  for y in 0 .. grid.Height - 1 do
    match grid.Cells.[x, y] with
    | ValueSome content ->
      let pos = CellGrid2D.getWorldPos x y grid
      // Custom render logic
    | ValueNone -> ()
```

## Usage Examples

### 2D Platformer Level

```fsharp
// User-defined content type
type TileType = Ground | Wall | Platform

type TileData = {
  Texture: Texture2D
  SourceRect: Rectangle
  Type: TileType
  Collision: bool
}

// Declarative level construction
let level =
  CellGrid2D.create 20 15 (Vector2(32f, 32f)) Vector2.Zero
  |> CellGrid2D.set 0 14 (groundTile())
  |> CellGrid2D.set 1 14 (groundTile())
  |> ...
  |> CellGrid2D.set 10 12 (platformTile())

// Rendering
let view (ctx: GameContext) (grid: CellGrid2D<TileData>) (buffer: RenderBuffer<RenderCmd2D>) =
  let cameraBounds = Camera.getWorldBounds ctx
  grid
  |> CellGrid2D.iterVisible cameraBounds (fun x y tile ->
    let pos = CellGrid2D.getWorldPos x y grid
    buffer.Sprite(sprite {
      texture tile.Texture
      sourceRect tile.SourceRect
      at pos.X pos.Y
      size grid.CellSize.X grid.CellSize.Y
    })
  )
```

### 3D Level

```fsharp
type BuildingData = {
  Model: Model
  Transform: Matrix
}

let town =
  CellGrid3D.create 10 10 (Vector2(100f, 100f)) XZ (Vector3.Zero)
  |> CellGrid3D.set 2 2 { Model = tavernModel; Transform = Matrix.Identity }
  |> CellGrid3D.set 5 2 { Model = shopModel; Transform = Matrix.Identity }

// Rendering
let render (grid: CellGrid3D<BuildingData>) (buffer: RenderBuffer<RenderCmd3D>) =
  let cameraFrustum = getCameraFrustum()
  grid
  |> CellGrid3D.iterVisible cameraFrustum (fun x y building ->
    let pos = CellGrid3D.getWorldPos x y grid
    let matrix = building.Transform * Matrix.CreateTranslation(pos)
    Draw3D.mesh building.Model matrix buffer
  )
```

## Performance Characteristics

### Memory

- **Overhead**: One `voption` per cell (struct, no heap allocation)
- **Cache**: Array2D provides contiguous memory, cache-friendly sequential access
- **Allocation**: Single Array2D allocation at creation time

### Runtime

- **Access**: O(1) array indexing
- **Iteration**: O(n*m) sequential, cache-friendly
- **Visibility**: O(n*m) worst case, O(visible) best case with bounds check

## File Structure

```
src/Mibo/Layout/
├── Grid2D.fs       // CellGrid2D and CellGrid2D module
├── Grid3D.fs       // CellGrid3D and CellGrid3D module
├── Renderer2D.fs   // Optional built-in 2D renderer
├── Renderer3D.fs   // Optional built-in 3D renderer
└── Layout.fs       // Composite types (Zone, Level)
```

## Summary

This minimalist design provides:

✅ **Generic cell grids** - User defines content type  
✅ **Code-first authoring** - Declarative composition with `set` chains  
✅ **Raw access** - Direct cell iteration for custom renderers  
✅ **Built-in renderers** - Convenience helpers for 2D and 3D  
✅ **Built-in iteration** - `iterVisible` with culling support  
✅ **Zero-cost abstractions** - Structs, Array2D, voption for cache-friendly access  
✅ **2D and 3D** - Same pattern, different coordinate mapping  

What is **NOT** included:

❌ CSS-like layout algorithms (Flexbox, Grid tracks)  
❌ Nested layouts  
❌ Auto-sizing or measure/arrange passes  
❌ Built-in content types (Texture2D, Model, etc.)  
❌ Constraints (min/max size, etc.)  

This aligns with Mibo's philosophy: provide primitives, let users compose.
