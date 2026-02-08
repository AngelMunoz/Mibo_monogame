---
title: Welcome to Mibo
category: Documentation
index: 0
---

# Mibo: A Functional Game Framework for F#

Mibo is a lightweight, Elmish-based game framework built on top of MonoGame. It brings the power of the **Model-View-Update (MVU)** architecture to game development, encouraging pure game logic and predictable state management.

## Getting Started

To get started with Mibo, you need the [dotnet SDK](https://get.dot.net) installed.

Install the templates:

```bash
dotnet new install Mibo.Templates
```

Create a new Mibo project using the provided template:

```bash
dotnet new mibo2d -n MyMiboGame
cd MyMiboGame
dotnet run
```

Or the 3D template:

```bash
dotnet new mibo3d -n MyMibo3DGame
cd MyMibo3DGame
dotnet run
```

If you prefer learning by example, the projects in `src/Sample` and `src/3DSample` show complete, working setups.

You can then start building your game using any of the following

- [VsCode](https://code.visualstudio.com/) with the
  - [Ionide extension](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp) (MS Registry)
  - [Ionide extension](https://open-vsx.org/extension/Ionide/Ionide-fsharp) (Open VSX Registry)
- [JetBrains Rider](https://www.jetbrains.com/rider/)
- [Visual Studio](https://visualstudio.microsoft.com/)

## Why Mibo?

Traditional game engines often rely heavily on mutable state and complex object hierarchies. Mibo offers an alternative:

- **Functional First**: Write your game logic as pure functions that transform state.
- **Predictable State**: The entire game state (the Model) is centralized and immutable.
- **Elmish Architecture**: Leverage the robust MVU pattern for clear separation of concerns.
- **MonoGame Power**: Benefit from the performance and cross-platform capabilities of MonoGame.
- **Deferred Rendering**: Built-in 2D and 3D batchers that handle sorting and culling for you.

## Core Patterns

### The Elmish Loop

Every Mibo game follows a simple loop:

1. **Init**: Define your initial state.
2. **Update**: Purely calculate the next state based on messages (input, timers, etc.).
3. **View**: Describe what should be rendered based on the current state.
4. **Subscribe**: Listen to external events like keyboard or touch input.

### Semantic Input Mapping

Instead of checking for specific keys in your player logic, Mibo encourages mapping keys to **Actions**. This allows for easy input rebinding and multi-device support.

## Getting Started

Run one of the samples, then copy its program setup (composition root) into your own project.

## Documentation

- Architecture
  - [Elmish (MVU) runtime](elmish.html)
  - [Programs & composition](program.html)
  - [System pipeline (phases + snapshot)](system.html)
  - [Service composition](services.html)
  - [Scaling Mibo (Simple → Complex)](scaling.html)
  - [F# For Perf](performance.html)

 - Rendering
   - [Rendering overview + custom renderers](rendering.html)
   - [Rendering 2D](rendering2d.html)

 - Level Design
   - [Level Design Overview](level-design/overview.html)
   - [2D Layout Engine](level-design/2d/core.html)
   - [Platformer Stamps](level-design/2d/platformer.html)
   - [TopDown Stamps](level-design/2d/topdown.html)
   - [3D Layout Engine](level-design/3d/core.html)
   - [Interior Stamps](level-design/3d/interior.html)
   - [Terrain Stamps](level-design/3d/terrain.html)

 - 3D Rendering
  - [Rendering3D Overview](3d-rendering/overview.html)
  - [Rendering3D: Pipeline](3d-rendering/pipeline.html)
  - [Rendering3D: Materials](3d-rendering/materials.html)
  - [Rendering3D: Lighting](3d-rendering/lighting.html)
  - [Rendering3D: Primitives](3d-rendering/primitives.html)
  - [Rendering3D: Post-Processing](3d-rendering/postprocessing.html)
  - [Rendering3D: Custom Shaders](3d-rendering/custom-shaders.html)

- Rendering & Assets
  - [Camera](camera.html)
  - [Culling](culling.html)
  - [Shaders](shaders.html)
  - [Input (raw + mapped)](input.html)
  - [Assets (loading + caching)](assets.html)
  - [Animation (2D sprites)](animation.html)
  - [Commands (async + effects)](commands.html)
  - [Subscriptions (events)](subscriptions.html)
