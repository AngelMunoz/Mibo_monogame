---
title: TopDown Layout
category: Layout
index: 22
---

# TopDown Layout

The `TopDown` module provides stamps for top-down games like RPGs, roguelikes, and dungeon crawlers. Import it with:

```fsharp
open Mibo.Layout
```

## Available Stamps

### room

A rectangular room with walls and floor:

```fsharp
section |> TopDown.room 8 6 FloorTile WallTile
```

### wallSegment

A horizontal wall line:

```fsharp
section |> TopDown.wallSegment 10 WallTile
```

### doorway

A wall segment with a 1-tile gap in the center:

```fsharp
section |> TopDown.doorway 7 WallTile
```

### corridor

A passageway with floor and side walls. Multiple directions supported:

```fsharp
// Horizontal corridor: 10 long, 3 wide
section |> TopDown.corridor 10 3 TopDown.Horizontal FloorTile WallTile

// Vertical corridor
section |> TopDown.corridor 8 3 TopDown.Vertical FloorTile WallTile

// Diagonal corridors
section |> TopDown.corridor 6 3 TopDown.DiagonalDownRight FloorTile WallTile
section |> TopDown.corridor 6 3 TopDown.DiagonalUpLeft FloorTile WallTile
```

Directions:
- `Horizontal` - Left to right
- `Vertical` - Top to bottom
- `DiagonalDownRight`, `DiagonalDownLeft`
- `DiagonalUpRight`, `DiagonalUpLeft`

## Composing Top-Down Layouts

### Simple Dungeon

```fsharp
let dungeon section =
    section
    // Main room
    |> Layout.section 0 0 (TopDown.room 10 8 FloorTile WallTile)

    // Corridor to the right
    |> Layout.section 10 3 (TopDown.corridor 8 3 TopDown.Horizontal FloorTile WallTile)

    // Second room
    |> Layout.section 18 0 (TopDown.room 8 8 FloorTile WallTile)
```

### Room with Multiple Exits

```fsharp
let intersectionRoom section =
    section
    |> TopDown.room 7 7 FloorTile WallTile
    // Carve doorways
    |> Layout.clear 3 0 1 1        // North exit
    |> Layout.clear 3 6 1 1        // South exit
    |> Layout.clear 0 3 1 1        // West exit
    |> Layout.clear 6 3 1 1        // East exit
```

## Building Custom Top-Down Stamps

Create game-specific stamps:

```fsharp
module MyDungeon =
    /// A room with a treasure in the center
    let treasureRoom width height =
        TopDown.room width height FloorTile WallTile
        >> Layout.center 1 1 (Layout.set 0 0 ChestTile)

    /// A corridor with alcoves for torches (replaces wall tiles with torch tiles)
    /// Note: This replaces the wall - use if TorchTile includes wall visuals
    let litCorridor length =
        TopDown.corridor length 3 TopDown.Horizontal FloorTile WallTile
        >> Layout.set 2 0 TorchWallTile      // Replaces wall at this position
        >> Layout.set (length - 3) 0 TorchWallTile

    /// A guard post with an enemy spawn point
    let guardPost =
        TopDown.room 5 5 FloorTile WallTile
        >> Layout.set 2 2 EnemySpawnTile

    /// An L-shaped corridor
    let lCorridorRight length1 length2 =
        TopDown.corridor length1 3 TopDown.Horizontal FloorTile WallTile
        >> Layout.section (length1 - 1) 0 (
            TopDown.corridor length2 3 TopDown.Vertical FloorTile WallTile
        )
```

> ***TIP:***
> If you need decorations that layer *on top* of walls without replacing them, use `LayeredGrid2D` with separate layers for structure and decorations.

## Example: Roguelike Floor

```fsharp
let roguelikeFloor section =
    section
    // Spawn room (top-left)
    |> Layout.section 0 0 (TopDown.room 8 6 FloorTile WallTile)
    |> Layout.section 3 2 (Layout.set 0 0 SpawnTile)

    // Corridor east
    |> Layout.section 8 2 (TopDown.corridor 6 3 TopDown.Horizontal FloorTile WallTile)

    // Combat room
    |> Layout.section 14 0 (TopDown.room 10 8 FloorTile WallTile)
    |> Layout.section 17 3 (Layout.set 0 0 EnemyTile)
    |> Layout.section 20 3 (Layout.set 0 0 EnemyTile)

    // Corridor south from combat room
    |> Layout.section 18 8 (TopDown.corridor 8 3 TopDown.Vertical FloorTile WallTile)

    // Treasure room (bottom)
    |> Layout.section 14 16 (MyDungeon.treasureRoom 10 6)

    // Exit stairs
    |> Layout.section 18 18 (Layout.set 0 0 StairsTile)

let floor =
    CellGrid2D.create 30 25 (Vector2(32f, 32f)) Vector2.Zero
    |> Layout.run roguelikeFloor
```

## Multi-Layer Top-Down

Use layers for floor, walls, and decorations:

```fsharp
let dungeon =
    LayeredGrid2D.create 50 50 (Vector2(32f, 32f)) Vector2.Zero
    |> LayeredLayout.layer 0 (fun section ->
        // Collision layer
        section
        |> Layout.section 5 5 (TopDown.room 10 10 FloorTile WallTile)
        |> Layout.section 15 7 (TopDown.corridor 10 3 TopDown.Horizontal FloorTile WallTile)
        |> Layout.section 25 5 (TopDown.room 8 10 FloorTile WallTile)
    )
    |> LayeredLayout.layer 1 (fun section ->
        // Decoration layer
        section
        |> Layout.section 7 7 (Layout.set 0 0 RugTile)
        |> Layout.set 27 7 TableTile
        |> Layout.set 29 7 ChairTile
    )
    |> LayeredLayout.layer 2 (fun section ->
        // Entity spawn layer (different type if needed)
        section
        |> Layout.set 10 10 PlayerSpawn
        |> Layout.set 28 10 EnemySpawn
    )
```
