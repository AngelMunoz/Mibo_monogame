---
title: Platformer Layout
category: Level Design
categoryindex: 2
index: 22
---

# Platformer Layout

The `Platformer` module provides stamps for side-scrolling platformer games. Import it with:

```fsharp
open Mibo.Layout
```

## Available Stamps

### box

Creates a filled rectangle with a border:

```fsharp
section |> Platformer.box 10 8 WallTile FloorTile
```

### platform

A horizontal 1-tile surface:

```fsharp
section |> Platformer.platform 5 PlatformTile
```

### ledge

A platform anchored to the left or right edge:

```fsharp
section |> Platformer.ledge 3 Platformer.Left PlatformTile
section |> Platformer.ledge 3 Platformer.Right PlatformTile
```

### wall

A vertical 1-tile column:

```fsharp
section |> Platformer.wall 6 WallTile
```

### pillar

A vertical structure with distinct base, middle, and top tiles:

```fsharp
section |> Platformer.pillar 5 BaseTile MiddleTile TopTile
```

### stairs

Staircase pattern in one of four directions:

```fsharp
section |> Platformer.stairs 4 StepTile Platformer.UpRight
section |> Platformer.stairs 4 StepTile Platformer.DownLeft
```

Directions: `UpRight`, `UpLeft`, `DownRight`, `DownLeft`

### slope

A smooth diagonal line:

```fsharp
section |> Platformer.slope 8 4 SlopeTile Platformer.UpRight
```

### pit

Clears cells to create a fall-through gap:

```fsharp
section |> Platformer.pit 3 5  // 3 wide, 5 deep
```

### gap

Clears a rectangular area:

```fsharp
section |> Platformer.gap 4 3  // 4x3 empty space
```

## Composing Platformer Levels

Combine stamps for complex structures:

```fsharp
let platformChallenge =
    Platformer.box 20 12 WallTile FloorTile
    >> Layout.section 3 9 (Platformer.platform 4 PlatformTile)
    >> Layout.section 10 6 (Platformer.platform 4 PlatformTile)
    >> Layout.section 16 3 (Platformer.platform 3 PlatformTile)

let level =
    CellGrid2D.create 100 20 (Vector2(32f, 32f)) Vector2.Zero
    |> Layout.run (fun section ->
        section
        |> Layout.section 5 5 platformChallenge
        |> Layout.section 30 10 (Platformer.stairs 5 StepTile Platformer.UpRight)
        |> Layout.section 40 5 (Platformer.pit 2 10)
    )
```

## Building Custom Platformer Stamps

Create game-specific stamps:

```fsharp
module MyPlatformer =
    /// A floating platform with supports
    let floatingPlatform width =
        Platformer.platform width PlatformTile
        >> Layout.set 0 1 SupportTile
        >> Layout.set (width - 1) 1 SupportTile

    /// A pit with hazards at the bottom
    let hazardPit width depth =
        Platformer.pit width depth
        >> Layout.section 0 (depth - 1) (
            Layout.repeatX 0 0 width SpikeTile
        )

    /// A checkpoint area
    let checkpoint =
        Platformer.box 4 3 WallTile FloorTile
        >> Layout.set 2 1 FlagTile
```

## Example: Complete Level Section

```fsharp
let levelSection section =
    section
    // Ground
    |> Layout.fill 0 18 100 2 GroundTile

    // Starting platform
    |> Layout.section 5 15 (Platformer.platform 8 PlatformTile)

    // Stair climb
    |> Layout.section 15 10 (Platformer.stairs 5 StepTile Platformer.UpRight)

    // Upper ledge
    |> Layout.section 20 10 (Platformer.ledge 6 Platformer.Left PlatformTile)

    // Pit obstacle
    |> Layout.section 30 15 (Platformer.pit 3 5)

    // Pillars for decoration
    |> Layout.section 40 12 (Platformer.pillar 6 PillarBase PillarMid PillarTop)
    |> Layout.section 50 12 (Platformer.pillar 6 PillarBase PillarMid PillarTop)

    // Box challenge room
    |> Layout.section 60 8 (Platformer.box 15 10 WallTile FloorTile)
```
