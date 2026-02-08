---
title: Terrain Stamps
category: Level Design
categoryindex: 2
index: 26
---

# Terrain Stamps

The `Terrain` module provides stamps for outdoor 3D spaces like landscapes, paths, and landmarks. Import it with:

```fsharp
open Mibo.Layout3D
open Mibo.Layout3D.Terrain
```

## Available Stamps

### ground

Creates a flat ground plane at Y=0:

```fsharp
section |> Terrain.ground 20 20 GrassCell
```

Parameters: `width`, `depth`, `content`

The ground is a single cell thick at Y=0.

### plateau

An elevated flat top with vertical sides:

```fsharp
section |> Terrain.plateau 10 10 5 TopCell SideCell
```

Parameters: `width`, `depth`, `height`, `top`, `side`

- Creates a flat top at the specified height
- Adds vertical sides connecting top to ground (Y=0)
- Uses `top` for the flat surface and `side` for vertical faces

### pit

Carves a depression below ground level:

```fsharp
section |> Terrain.pit 5 5 3
```

Parameters: `width`, `depth`, `dropHeight`

- Clears cells from Y=0 down to Y=-dropHeight
- Useful for craters, basements, or sunken areas

### rampX

A ramp connecting two Y levels along the X axis:

```fsharp
section |> Terrain.rampX 10 5 4 RampCell
```

Parameters: `width`, `depth`, `rise`, `content`

- Ramp rises from Y=0 to Y=rise across the X axis
- Each cell's Y increases as X increases
- Useful for stairs, inclines, or gradual height changes

### rampZ

A ramp connecting two Y levels along the Z axis:

```fsharp
section |> Terrain.rampZ 5 10 4 RampCell
```

Parameters: `width`, `depth`, `rise`, `content`

- Ramp rises from Y=0 to Y=rise across the Z axis
- Each cell's Y increases as Z increases

### path

Creates a path/road at ground level between waypoints:

```fsharp
let waypoints = [(0, 0, 0); (5, 0, 5); (10, 0, 10)]
section |> Terrain.path waypoints 2 PathCell
```

Parameters: `points`, `width`, `content`

- Points are (x, y, z) tuples
- Uses 3D line rasterization to connect points
- Path is at ground level (Y from points)

### scatter

Randomly places content on the ground plane:

```fsharp
section |> Terrain.scatter 10 42 TreeCell
```

Parameters: `count`, `seed`, `content`

- Places `count` items at random X, Z positions at Y=0
- Uses `seed` for deterministic random placement
- Great for trees, rocks, props, or decorative elements

### heightmap

Generates terrain from a height function:

```fsharp
let hillHeight x z =
    let dist = float (sqrt ((x-10)*(x-10) + (z-10)*(z-10)))
    int (5.0 * exp (-dist/10.0))  // Gaussian hill centered at (10,10)

section |> Terrain.heightmap hillHeight GrassCell
```

Parameters: `heightFn`, `content`

- `heightFn` takes (x, z) grid coordinates and returns Y height
- Uses `content` for all cells at specified heights
- Great for hills, valleys, or procedural terrain

### layeredHeightmap

Generates multi-layer terrain with different materials at different depths:

```fsharp
let hillHeight x z =
    let dist = float (sqrt ((x-10)*(x-10) + (z-10)*(z-10)))
    int (8.0 * exp (-dist/10.0))  // Tall hill

section |> Terrain.layeredHeightmap hillHeight GrassCell DirtCell 3 StoneCell
```

Parameters: `heightFn`, `topLayer`, `midLayer`, `midDepth`, `bottomLayer`

- `heightFn` determines the surface height
- `topLayer` is used for the surface (top `midDepth` cells)
- `midLayer` fills from `midDepth` down to near bottom
- `bottomLayer` fills the remaining cells

## Composing Terrain Layouts

### Simple Landscape

```fsharp
let landscape =
    CellGrid3D.create 30 10 30 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Base ground
        |> Terrain.ground 30 30 GrassCell
        // Scattered trees
        |> Terrain.scatter 20 42 TreeCell
        |> Terrain.scatter 15 78 TreeCell
        // A hill
        |> Layout3D.section 10 0 10 (fun inner ->
            let hillHeight x z =
                let dist = float (sqrt ((x-5)*(x-5) + (z-5)*(z-5)))
                int (5.0 * exp (-dist/5.0))
            inner |> Terrain.heightmap hillHeight GrassCell
        )
    )
```

### Plateau with Access Ramp

```fsharp
let elevatedArea =
    CellGrid3D.create 20 10 20 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Ground level
        |> Terrain.ground 20 20 GrassCell
        // Elevated plateau
        |> Layout3D.section 5 0 5 (Terrain.plateau 10 10 4 TopCell SideCell)
        // Ramp up to plateau
        |> Layout3D.section 0 0 9 (Terrain.rampX 5 2 4 RampCell)
    )
```

### Valley with Path

```fsharp
let valleyWithPath =
    CellGrid3D.create 40 15 40 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Create a valley (high walls, low center)
        |> Layout3D.section 0 0 0 (fun inner ->
            let valleyHeight x z =
                // Center of valley is at (20, 20)
                let distFromCenter = float (sqrt ((x-20)*(x-20) + (z-20)*(z-20)))
                int (8.0 * exp (-distFromCenter/15.0))  // 8 cells tall at center
            inner |> Terrain.heightmap valleyHeight GrassCell
        )
        // Winding path through valley
        |> Layout3D.section 5 0 5 (Terrain.path [(0,0,0); (10,0,10); (15,0,5); (25,0,15); (30,0,30)] 2 PathCell)
        // Trees on the ridge
        |> Layout3D.section 0 0 0 (fun inner ->
            inner
            |> Terrain.scatter 25 37 TreeCell
            |> Terrain.scatter 20 91 TreeCell
        )
    )
```

### Multi-Layer Terrain

```fsharp
let layeredTerrain =
    CellGrid3D.create 25 20 25 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Create a hill with grass, dirt, and stone layers
        |> Layout3D.section 0 0 0 (fun inner ->
            let hillHeight x z =
                let dist = float (sqrt ((x-12)*(x-12) + (z-12)*(z-12)))
                int (10.0 * exp (-dist/8.0))
            inner |> Terrain.layeredHeightmap hillHeight GrassCell DirtCell 4 StoneCell
        )
        // Add a pit with exposed layers
        |> Layout3D.section 18 0 18 (Terrain.pit 5 5 6)
    )
```

## Building Custom Terrain Stamps

Create game-specific terrain stamps:

```fsharp
module MyTerrain =
    /// A crater with raised rim
    let crater radius rimHeight =
        let craterHeight x z =
            let dist = float (sqrt ((x-radius)*(x-radius) + (z-radius)*(z-radius)))
            if dist < float radius then
                int (-float rimHeight * (1.0 - dist/float radius))  // Depression inside
            elif dist < float radius + 2.0 then
                rimHeight  // Raised rim
            else
                0
        fun section ->
            let (w, d) = (radius * 2 + 4, radius * 2 + 4)
            section
            |> Layout3D.section 0 0 0 (fun inner ->
                inner
                |> Terrain.layeredHeightmap craterHeight CraterDirt CraterDirt 2 CraterRock
            )

    /// A road with barriers on the sides
    let roadWithBarriers length width roadCell barrierCell section =
        section
        |> Terrain.path [(0, 0, 0); (length-1, 0, 0)] width roadCell
        |> Layout3D.repeatX 0 0 1 length BarrierCell  // Left barrier
        |> Layout3D.repeatX (width-1) 0 1 length BarrierCell  // Right barrier

    /// A forest clearing with trees around the edges
    let forestClearing width depth treeCount section =
        section
        |> Terrain.ground width depth GrassCell
        |> Terrain.scatter treeCount 42 TreeCell
        |> Layout3D.clear 2 0 2 (width-4) 1 (depth-4)  // Clear center

    /// A mountain peak with snow on top
    let mountain width depth maxHeight =
        let mountainHeight x z =
            let dist = float (sqrt ((x-float width/2.0)*(x-float width/2.0) + (z-float depth/2.0)*(z-float depth/2.0)))
            let maxDist = float (min width depth) / 2.0
            int (float maxHeight * (1.0 - dist/maxDist))
        fun section ->
            section
            |> Layout3D.section 0 0 0 (fun inner ->
                inner
                |> Terrain.layeredHeightmap mountainHeight SnowCell RockCell 3 StoneCell
            )

    /// A river cutting through terrain
    let river width depth riverWidth section =
        let riverHeight x z =
            // Create a river running down the middle (z axis)
            let distFromCenter = abs (x - width/2)
            if distFromCenter < riverWidth then
                -3  // Riverbed is lower
            elif distFromCenter < riverWidth + 2 then
                1   // Small bank
            else
                2   // Higher ground
        fun section ->
            section
            |> Layout3D.section 0 0 0 (fun inner ->
                inner
                |> Terrain.layeredHeightmap riverHeight WaterCell RiverbedCell 2 GrassCell
            )
```

## Example: Complete Outdoor Area

```fsharp
let outdoorArea =
    CellGrid3D.create 50 20 50 (Vector3(2f, 2f, 2f)) Vector3.Zero
    |> Layout3D.run (fun section ->
        section
        // Base terrain with hills
        |> Layout3D.section 0 0 0 (fun inner ->
            let hillHeight x z =
                let h1 = 5.0 * exp (-((x-15.0)*(x-15.0) + (z-15.0)*(z-15.0))/50.0)
                let h2 = 8.0 * exp (-((x-35.0)*(x-35.0) + (z-35.0)*(z-35.0))/80.0)
                int (h1 + h2)
            inner |> Terrain.layeredHeightmap hillHeight GrassCell DirtCell 3 StoneCell
        )

        // Path connecting areas
        |> Layout3D.section 5 0 5 (Terrain.path [(0,0,0); (15,0,15); (25,0,20); (40,0,40)] 2 PathCell)

        // Elevated plateau near first hill
        |> Layout3D.section 10 0 10 (Terrain.plateau 8 8 4 TopCell SideCell)
        |> Layout3D.section 10 0 8 (Terrain.rampZ 8 2 4 RampCell)

        // Forest area
        |> Layout3D.section 20 0 30 (fun inner ->
            inner
            |> Terrain.scatter 40 123 TreeCell
            |> Terrain.scatter 30 456 TreeCell
        )

        // Lake (pit filled with water)
        |> Layout3D.section 35 0 35 (Terrain.pit 8 8 4)
        |> Layout3D.section 36 0 36 (Terrain.ground 6 6 WaterCell)

        // Rocks scattered around
        |> Terrain.scatter 15 789 RockCell
        |> Terrain.scatter 20 234 RockCell

        // Small village on the plateau
        |> Layout3D.section 11 4 11 (Layout3D.fill 0 0 0 3 2 3 HouseFloorCell)
        |> Layout3D.section 15 4 13 (Layout3D.fill 0 0 0 3 2 3 HouseFloorCell)
    )
```

## Design Tips

### Heightmap Functions

For natural-looking terrain:
- Use Gaussian functions for smooth hills: `h * exp(-dist^2 / scale)`
- Combine multiple hills by adding heights together
- Use noise functions for more varied terrain

```fsharp
let naturalTerrain x z =
    let h1 = 5.0 * exp (-((x-10.0)*(x-10.0) + (z-10.0)*(z-10.0))/50.0)
    let h2 = 3.0 * exp (-((x-30.0)*(x-30.0) + (z-25.0)*(z-25.0))/40.0)
    let h3 = 2.0 * sin(x/5.0) * cos(z/5.0)  // Gentle undulation
    int (h1 + h2 + h3)
```

### Path Placement

Paths should follow natural contours:
- Use multiple waypoints for winding paths
- Path width of 2-4 cells is typical
- Consider adding ramps for steep sections

### Layered Terrain

Use `layeredHeightmap` for realistic terrain:
- Top layer: Grass, snow, or sand (1-3 cells)
- Middle layer: Dirt, gravel (3-5 cells)
- Bottom layer: Stone, bedrock (remaining cells)

```fsharp
Terrain.layeredHeightmap heightFn GrassCell DirtCell 3 StoneCell
```

### Scatter Placement

Use different seeds for variety:
- Different seed values give different patterns
- Multiple scatter calls with different contents create variety
- Combine with terrain features (trees on hills, rocks near mountains)

```fsharp
section
|> Terrain.scatter 10 42 TreeCell    // Forest pattern
|> Terrain.scatter 15 789 RockCell   // Different rock arrangement
```
