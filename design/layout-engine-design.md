# Mibo Layout Engine - Minimalist 2D Design

## Design Goals

1. **Low-level storage primitives** - Efficient grid-based data storage for level/map data
2. **Code-first** - No designer tools, pure F# code
3. **Integrate with Mibo renderers** - Built-in convenience + raw data access
4. **Performance-first** - In-place operations with zero copies for optimal speed

**Note:** This is Phase 1 of the Layout Engine - low-level storage primitives. Phase 2 will add a high-level declarative DSL for level authoring with shape primitives, pattern operations, and composition helpers.

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
  val set: x:int -> y:int -> content:'T -> grid:CellGrid2D<'T> -> unit
  val get: x:int -> y:int -> grid:CellGrid2D<'T> -> 'T voption
  val getWorldPos: x:int -> y:int -> grid:CellGrid2D<'T> -> Vector2
  val iter: (int -> int -> 'T -> unit) -> grid:CellGrid2D<'T> -> unit
  val iterVisible: bounds:Rectangle -> (int -> int -> 'T -> unit) -> grid:CellGrid2D<'T> -> unit
```

## Composition Layer (The View)

To support relative positioning, nesting, and reusable layout components without data copying, we introduce a lightweight "view" abstraction.

### GridSection2D<'T>

A cursor-like view into a backing grid that handles coordinate translation and bounds.

```fsharp
[<Struct>]
type GridSection2D<'T> = {
  BackingGrid: CellGrid2D<'T>
  OffsetX: int
  OffsetY: int
  Width: int
  Height: int
}
```

## Layout DSL

A fluent API where all operations act on a `GridSection` (Cursor).
**Performance Note:** Functions taking lambdas should be marked `inline` and define the lambda parameter with `[<InlineIfLambda>]` to eliminate closure allocation and runtime overhead.

```fsharp
module Layout =
  // Lifecycle
  // Lifts the Grid into the Section context, executes the chain, and returns the modified Grid.
  val run: f:(GridSection2D<'T> -> GridSection2D<'T>) -> grid:CellGrid2D<'T> -> CellGrid2D<'T>

  // Scoping
  // Creates a sub-section at (x,y), runs 'f' on it, and returns the PARENT section.
  // This allows nesting components indefinitely.
  val section: x:int -> y:int -> f:(GridSection2D<'T> -> GridSection2D<'T>) -> parent:GridSection2D<'T> -> GridSection2D<'T>

  // Structural Decorators (Web-like Flow)
  val padding: n:int -> f:(GridSection2D<'T> -> GridSection2D<'T>) -> parent:GridSection2D<'T> -> GridSection2D<'T>
  val paddingEx: left:int -> top:int -> right:int -> bottom:int -> f:(GridSection2D<'T> -> GridSection2D<'T>) -> parent:GridSection2D<'T> -> GridSection2D<'T>
  val center: w:int -> h:int -> f:(GridSection2D<'T> -> GridSection2D<'T>) -> parent:GridSection2D<'T> -> GridSection2D<'T>
  val flowX: step:int -> stamps:seq<GridSection2D<'T> -> GridSection2D<'T>> -> parent:GridSection2D<'T> -> GridSection2D<'T>
  val flowY: step:int -> stamps:seq<GridSection2D<'T> -> GridSection2D<'T>> -> parent:GridSection2D<'T> -> GridSection2D<'T>

  // Primitives (Section -> Section)
  val set: x:int -> y:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val fill: x:int -> y:int -> width:int -> height:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val border: x:int -> y:int -> width:int -> height:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  
  // Functional / Procedural
  // Fills a region by calling a generator function for each cell.
  val generate: x:int -> y:int -> width:int -> height:int -> generator:(int -> int -> 'T) -> section:GridSection2D<'T> -> GridSection2D<'T>
  // Iterates over a region (read-only).
  val iter: x:int -> y:int -> width:int -> height:int -> action:(int -> int -> 'T voption -> unit) -> section:GridSection2D<'T> -> GridSection2D<'T>
  // Transforms existing content in a region.
  val map: x:int -> y:int -> width:int -> height:int -> mapping:('T -> 'T) -> section:GridSection2D<'T> -> GridSection2D<'T>

  val repeatX: x:int -> y:int -> count:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val repeatY: x:int -> y:int -> count:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>

  // Geometry (Rasterized)
  val line: x1:int -> y1:int -> x2:int -> y2:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val circle: cx:int -> cy:int -> radius:int -> filled:bool -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val polygon: points:struct (int * int)[] -> filled:bool -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>

  // Patterns
  val checker: odd:'T -> even:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val scatter: count:int -> seed:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>

  // Logic / Editing
  val clear: x:int -> y:int -> width:int -> height:int -> section:GridSection2D<'T> -> GridSection2D<'T>
  val replace: oldContent:'T -> newContent:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
  val setIfEmpty: x:int -> y:int -> content:'T -> section:GridSection2D<'T> -> GridSection2D<'T>
```

## Phase 1.5: Layered Composition

To support overlapping content (e.g., decorations behind physical objects), we introduce a layered container.

### LayeredGrid2D<'T>

```fsharp
type LayeredGrid2D<'T> = {
    Width: int
    Height: int
    CellSize: Vector2
    Origin: Vector2
    // Map from RenderLayer index to Grid
    Layers: Map<int, CellGrid2D<'T>>
}
```

### LayeredLayout DSL

Helper functions to select the active layer before applying standard Layout primitives.

```fsharp
module LayeredLayout =
    // Selects a layer and runs a standard Layout function on it.
    // Creates the layer if it doesn't exist.
    val layer: index:int -> f:(GridSection2D<'T> -> GridSection2D<'T>) -> grid:LayeredGrid2D<'T> -> LayeredGrid2D<'T>
```

## Phase 1.5: Domain Modules

Domain modules are standard libraries of **Layout Stamps**. They provide high-level geometry generators that return `LayoutOp` (GridSection2D -> GridSection2D). They do **not** depend on physics or game logic.

### Platformer Module

```fsharp
module Platformer =
    // Generates a rectangular box with border and fill
    val box: width:int -> height:int -> border:'T -> fill:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a platform (guaranteed 1-tile high)
    val platform: width:int -> tile:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a horizontal platform anchored to a side
    val ledge: width:int -> anchor:Anchor -> tile:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a wall or column
    val wall: height:int -> tile:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a vertical pillar with caps
    val pillar: height:int -> baseTile:'T -> middleTile:'T -> topTile:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates stairs
    val stairs: width:int -> tile:'T -> direction:StairDirection -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a smooth diagonal slope
    val slope: width:int -> height:int -> tile:'T -> direction:StairDirection -> (GridSection2D<'T> -> GridSection2D<'T>)
```

### TopDown Module

```fsharp
module TopDown =
    // Direction for corridor construction
    type CorridorDirection =
        | Horizontal
        | Vertical
        | DiagonalDownRight
        | DiagonalDownLeft
        | DiagonalUpRight
        | DiagonalUpLeft

    // Generates a horizontal wall segment
    val wallSegment: length:int -> wall:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a wall with a gap (doorway opening)
    val doorway: length:int -> wall:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a corridor (floor with side walls)
    val corridor: length:int -> width:int -> direction:CorridorDirection -> floor:'T -> wall:'T -> (GridSection2D<'T> -> GridSection2D<'T>)

    // Generates a rectangular room with walls and floor
    val room: width:int -> height:int -> floor:'T -> wall:'T -> (GridSection2D<'T> -> GridSection2D<'T>)
```


## Usage Examples

### Layered Composition (Building a Guard Post)

```fsharp
// 1. Create a layered container
let level = LayeredGrid2D.create 100 100 (Vector2(32f, 32f)) Vector2.Zero

// 2. Compose content using decorators and stamps
let result =
    level
    |> LayeredLayout.layer 0 ( // Physical Layer
         Layout.padding 2 (
             Platformer.box 10 8 WallTile FloorTile
             >> Layout.center 4 1 (Platformer.platform 4 TableTile)
         )
    )
    |> LayeredLayout.layer 1 ( // Decoration Layer
         Layout.padding 2 (
             Layout.flowX 8 [
                 Platformer.pillar 5 Base Mid Top
                 Platformer.pillar 5 Base Mid Top
             ]
         )
    )
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

### Why In-Place Operations?

- Initialization and bulk updates require setting many cells (hundreds or thousands)
- Copy-on-write would be O(n²) - catastrophic for performance
- In-place operations are O(1) per cell - optimal for initialization
- For runtime immutability, users can copy explicitly: `{ grid with Cells = Array2D.copy grid.Cells }`
- This aligns with Mibo's philosophy: performance first, provide primitives, let users compose

## Rendering Integration

### Built-in Renderers (Optional)

```fsharp
module CellGridRenderer2D =
  val render: CellGrid2D<'T> -> renderCell:(Vector2 -> 'T -> unit) -> unit
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
├── Layered.fs      // LayeredGrid2D and LayeredLayout module
├── Platformer.fs   // Platformer domain stamps
├── TopDown.fs     // Top-down domain stamps
├── Renderer2D.fs   // Optional built-in 2D renderer
└── Layout.fs       // Composition Types and DSL (Layout module)
```

## Summary

This minimalist design provides:

✅ **Generic cell grids** - User defines content type
✅ **Low-level storage primitives** - Efficient data structure for level/map data
✅ **Raw access** - Direct cell iteration for custom renderers
✅ **Built-in renderers** - Convenience helpers for 2D
✅ **Built-in iteration** - `iterVisible` with culling support
✅ **Zero-copy operations** - In-place mutations for optimal performance
✅ **Composable DSL** - Relative positioning and reuse via `GridSection`
✅ **Layered Composition** - Support for overlapping content via `LayeredGrid2D`
✅ **Domain Primitives** - Reusable geometry stamps via `Platformer` and `TopDown` modules

What is **NOT** included (Phase 1):

❌ CSS-like layout algorithms (Flexbox, Grid tracks)
❌ Auto-sizing or measure/arrange passes
❌ Built-in content types (Texture2D, Model, etc.)
❌ Constraints (min/max size, etc.)

This aligns with Mibo's philosophy: provide primitives, let users compose.
