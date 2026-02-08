---
title: Interior Stamps
category: Level Design
categoryindex: 2
index: 25
---

# Interior Stamps

The `Interior` module provides stamps for enclosed 3D spaces like rooms, corridors, dungeons, and building interiors. Import it with:

```fsharp
open Mibo.Layout3D
open Mibo.Layout3D.Interior
```

## DoorSide Direction

Many interior stamps use `DoorSide` to specify which face to operate on:

```fsharp
type DoorSide =
  | North  // Front face (max Z)
  | South  // Back face (min Z)
  | East   // Right face (max X)
  | West   // Left face (min X)
```

## Available Stamps

### room

A fully enclosed room with floor, ceiling, and four walls:

```fsharp
section |> Interior.room 8 4 8 FloorCell WallCell CeilingCell
```

Parameters: `width`, `height`, `depth`, `floor`, `wall`, `ceiling`

### openRoom

A room with floor and walls but no ceiling (for open-top areas):

```fsharp
section |> Interior.openRoom 8 4 8 FloorCell WallCell
```

Parameters: `width`, `height`, `depth`, `floor`, `wall`

### corridorX

A horizontal corridor (along X axis) with floor, walls, and ceiling:

```fsharp
section |> Interior.corridorX 10 2 3 FloorCell WallCell CeilingCell
```

Parameters: `length`, `width`, `height`, `floor`, `wall`, `ceiling`

The corridor is open at both X ends.

### corridorZ

A depth corridor (along Z axis) with floor, walls, and ceiling:

```fsharp
section |> Interior.corridorZ 10 2 3 FloorCell WallCell CeilingCell
```

Parameters: `length`, `width`, `height`, `floor`, `wall`, `ceiling`

The corridor is open at both Z ends.

### doorway

Clears a door-sized opening in a wall plane:

```fsharp
section |> Interior.doorway North 2 3
```

Parameters: `side`, `doorWidth`, `doorHeight`

Used after creating a room to add exits:

```fsharp
let roomWithDoor =
    Interior.room 8 4 8 FloorCell WallCell CeilingCell
    >> Interior.doorway East 2 3
```

### window

Clears a window opening in a wall:

```fsharp
section |> Interior.window North 3 2 1
```

Parameters: `side`, `windowWidth`, `windowHeight`, `sillHeight`

The `sillHeight` specifies how many cells from the bottom the window starts.

### stairs

A staircase connecting two Y levels. Steps rise along the Z axis:

```fsharp
section |> Interior.stairs 2 4 4 StairCell
```

Parameters: `width`, `rise`, `run`, `step`

- `width` - Width of the staircase in X
- `rise` - Total height increase in Y
- `run` - Length of the staircase in Z (number of steps)
- `step` - Cell type for each step

### shaft

A vertical shaft (elevator, ladder space) with walls and hollow interior:

```fsharp
section |> Interior.shaft 2 2 10 WallCell
```

Parameters: `width`, `depth`, `height`, `wall`

The shaft is hollow in the center with walls on all four sides.

### pillar

A vertical column with distinct base, middle, and top tiles:

```fsharp
section |> Interior.pillar 5 BaseCell MiddleCell TopCell
```

Parameters: `height`, `baseTile`, `middleTile`, `topTile`

- Bottom cell uses `baseTile`
- Middle cells use `middleTile`
- Top cell uses `topTile`

## Composing Interior Layouts

### Simple Dungeon

```fsharp
let dungeon =
    CellGrid3D.create 20 5 20 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Main room
        |> Layout3D.section 0 0 0 (Interior.room 8 4 8 FloorCell WallCell CeilingCell)

        // Corridor leading east
        |> Layout3D.section 8 0 3 (Interior.corridorX 6 2 3 FloorCell WallCell CeilingCell)

        // Second room
        |> Layout3D.section 14 0 0 (Interior.room 6 4 6 FloorCell WallCell CeilingCell)
    )
```

### Room with Exits

```fsharp
let centralRoom section =
    section
    |> Interior.room 10 4 10 FloorCell WallCell CeilingCell
    // Add doorways on all four sides
    |> Interior.doorway North 2 3
    |> Interior.doorway South 2 3
    |> Interior.doorway East 2 3
    |> Interior.doorway West 2 3
```

### Multi-Level Room with Stairs

```fsharp
let roomWithUpperLevel section =
    section
    // Lower room
    |> Layout3D.section 0 0 0 (Interior.room 8 4 8 FloorCell WallCell CeilingCell)
    // Stairs going up
    |> Layout3D.section 2 0 3 (Interior.stairs 2 4 4 StairCell)
    // Upper platform
    |> Layout3D.section 0 4 0 (Interior.openRoom 6 2 6 FloorCell WallCell)
```

### Building with Windows

```fsharp
let roomWithWindows section =
    section
    |> Interior.room 10 5 10 FloorCell WallCell CeilingCell
    // Add windows on north wall
    |> Interior.window North 3 2 2
    |> Interior.window North 3 2 2
    // Add windows on east wall
    |> Interior.window East 3 2 2
```

## Building Custom Interior Stamps

Create game-specific stamps:

```fsharp
module MyInterior =
    /// A room with a door and a torch on each side
    let torchRoom width height depth floor wall ceiling =
        Interior.room width height depth floor wall ceiling
        >> Interior.doorway North 2 3
        >> Interior.doorway South 2 3
        >> Layout3D.set 1 2 1 TorchCell
        >> Layout3D.set (width - 2) 2 1 TorchCell

    /// A corridor with alcoves for torches
    let litCorridor length width height floor wall ceiling =
        Interior.corridorX length width height floor wall ceiling
        >> Layout3D.set 2 2 1 TorchWallCell
        >> Layout3D.set (length - 3) 2 1 TorchWallCell

    /// A room with a central pillar and 4 pillars in corners
    let pillaredRoom width height depth floor wall ceiling pillarBase pillarMiddle pillarTop =
        Interior.room width height depth floor wall ceiling
        >> Layout3D.center 1 1 1 (Interior.pillar height pillarBase pillarMiddle pillarTop)
        >> Layout3D.set 1 1 1 (Interior.pillar height pillarBase pillarMiddle pillarTop)
        >> Layout3D.set (width - 2) 1 1 (Interior.pillar height pillarBase pillarMiddle pillarTop)
        >> Layout3D.set 1 1 (depth - 2) (Interior.pillar height pillarBase pillarMiddle pillarTop)
        >> Layout3D.set (width - 2) 1 (depth - 2) (Interior.pillar height pillarBase pillarMiddle pillarTop)

    /// An L-shaped room combination
    let lRoom room1W room1D room2W room2D height floor wall ceiling =
        Layout3D.section 0 0 0 (Interior.room room1W height room1D floor wall ceiling)
        >> Layout3D.section (room1W - 1) 0 0 (Interior.room room2W height room2D floor wall ceiling)
        >> Layout3D.clear (room1W - 1) 0 1 1 height 1  // Open passage
```

## Example: Complete FPS Level Section

```fsharp
let levelSection section =
    section
    // Spawn room
    |> Layout3D.section 0 0 0 (Interior.room 8 4 8 FloorCell WallCell CeilingCell)
    |> Interior.doorway East 2 3

    // Corridor with turns
    |> Layout3D.section 8 0 3 (Interior.corridorX 6 2 3 FloorCell WallCell CeilingCell)
    |> Layout3D.section 14 0 0 (Interior.room 4 3 6 FloorCell WallCell CeilingCell)
    |> Interior.doorway South 2 3

    // Side corridor with weapons
    |> Layout3D.section 10 0 0 (Interior.corridorZ 4 2 3 FloorCell WallCell CeilingCell)
    |> Layout3D.set 11 0 1 WeaponCell

    // Main hall
    |> Layout3D.section 14 0 6 (Interior.room 12 6 12 FloorCell WallCell CeilingCell)
    |> Interior.doorway West 2 4
    |> Interior.doorway East 2 4
    |> Interior.doorway South 3 4

    // Pillars in main hall
    |> Layout3D.section 16 0 8 (Interior.pillar 4 PillarBase PillarMiddle PillarTop)
    |> Layout3D.section 22 0 8 (Interior.pillar 4 PillarBase PillarMiddle PillarTop)

    // Stairs to upper level
    |> Layout3D.section 20 0 2 (Interior.stairs 2 4 4 StairCell)

    // Upper balcony
    |> Layout3D.section 16 4 0 (Interior.openRoom 10 2 12 FloorCell WallCell)
    |> Interior.window North 3 2 1
    |> Interior.window South 3 2 1

    // Windows in main hall
    |> Interior.window North 4 3 2
    |> Interior.window North 4 3 2
    |> Interior.window South 4 3 2
    |> Interior.window South 4 3 2

    // Exit shaft
    |> Layout3D.section 0 0 0 (Interior.shaft 2 2 10 WallCell)
```

## Design Tips

### Door Placement

When placing doors between rooms, ensure the door aligns with the corridor:

```fsharp
// Good - door aligns with corridor
section
|> Layout3D.section 0 0 0 (Interior.room 8 4 8 FloorCell WallCell CeilingCell)
|> Interior.doorway East 2 3
|> Layout3D.section 8 0 3 (Interior.corridorX 6 2 3 FloorCell WallCell CeilingCell)

// Bad - corridor Y offset doesn't match door Y position
```

### Stair Geometry

For natural-feeling stairs:
- Rise of 4-6 cells for typical stairs
- Run of 2-3 cells per step for comfortable climbing
- Width matches corridor width (usually 2-4 cells)

```fsharp
// Typical stair
Interior.stairs 2 4 4 StairCell  // width=2, rise=4, run=4
```

### Shaft Sizing

For player-sized shafts (ladders, elevators):
- Width/depth of 2 cells fits most player models
- Height matches floor-to-floor distance

```fsharp
Interior.shaft 2 2 10 WallCell  // 2x2 shaft, 10 cells tall
```

### Window Placement

Windows should be placed at eye level:
- Sill height of 1-2 cells from floor
- Window height of 2-3 cells

```fsharp
Interior.window North 3 2 1  // Width=3, Height=2, Sill=1
```
