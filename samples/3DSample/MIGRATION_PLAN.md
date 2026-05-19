# 3DSample Migration Plan

## Vision

Transform `samples/3DSample/` from a basic demo into an **educational reference** that teaches users how to build a complete 3D rendering pipeline using Mibo's V3 infrastructure-only architecture.

Each feature lives in its own well-named module with clear boundaries, making it easy to:
- Understand what each piece does in isolation
- Copy-paste individual features into a user's own project
- See how features compose together

---

## Current State

| File | Responsibility |
|------|---------------|
| `Domain.fs` | Core types (`State`, `Cell`, `GameAssets`, `GameAction`) |
| `Program.fs` | Entry point, `init`, `update`, `view`, `subscribe` |
| `Rendering.fs` | `SampleCmd` DU, `SampleCommandProcessor` |
| `Grid.fs` | Grid vertex generation + draw call |
| `Player.fs` | Player model rendering + respawn logic |
| `Level.fs` | Level grid construction via Layout3D DSL |
| `Movement.fs`, `Physics.fs`, `Rotation.fs`, `Platform.fs` | Game logic systems |

---

## Target Structure

```
samples/3DSample/
├── Domain/
│   ├── Domain.Types.fs          # State, Cell, GameAction, constants
│   ├── Domain.Assets.fs         # GameAssets type + asset loading helpers
│   └── Domain.Input.fs          # GameAction, InputMap setup
├── Systems/
│   ├── Systems.Movement.fs      # Player movement
│   ├── Systems.Physics.fs       # Gravity, collision, jumping
│   ├── Systems.Rotation.fs      # Ball rolling rotation
│   └── Systems.Respawn.fs       # Fall detection + respawn
├── Level/
│   ├── Level.Cells.fs           # Cell types (anchor, marker, floorTile, etc.)
│   ├── Level.Stamps.fs          # Composable stamps (floorGrid, pillar, bridge, etc.)
│   └── Level.Build.fs           # Level.create() composition
├── Rendering/
│   ├── Rendering.Commands.fs    # RenderCmd DU (the single source of truth)
│   ├── Rendering.Processor.fs   # Command processor (iterates + dispatches)
│   ├── Rendering.Camera.fs      # Camera setup + follow logic
│   ├── Rendering.Clear.fs       # Screen clear command
│   ├── Rendering.Mesh.fs        # Mesh drawing with material support
│   ├── Rendering.Lights.fs      # Light types + light commands
│   ├── Rendering.Shadows.fs     # Shadow pass commands + shadow map management
│   ├── Rendering.PostProcess.fs # Bloom, tone mapping commands
│   ├── Rendering.Grid.fs        # Grid line rendering (custom effect)
│   ├── Rendering.Particles.fs   # Billboard particles
│   └── Rendering.Debug.fs       # Debug lines, quads, velocity vector
├── Shaders/                     # .fx files (content pipeline)
│   ├── Unlit.fx
│   ├── PBR.fx
│   ├── ShadowCaster.fx
│   ├── Grid.fx
│   ├── BloomExtract.fx
│   └── PostProcess.fx
├── Materials/
│   ├── Materials.PBR.fs         # PBR material data + shader binding
│   ├── Materials.Unlit.fs       # Unlit material data + shader binding
│   └── Materials.Grid.fs        # Grid material (distance fade)
├── Program.fs                   # Thin orchestrator: init, update, view, main
└── 3DSample.fsproj
```

---

## Migration Sessions

### Session 1: Restructure Foundation

**Goal:** Split monolithic files into focused modules without changing behavior.

| Task | From | To |
|------|------|-----|
| Split `Domain.fs` | Single file | `Domain.Types.fs`, `Domain.Assets.fs`, `Domain.Input.fs` |
| Split game logic | `Movement.fs`, `Physics.fs`, etc. | `Systems/` folder (same files, new location) |
| Split level building | `Level.fs` | `Level.Cells.fs`, `Level.Stamps.fs`, `Level.Build.fs` |
| Split rendering | `Rendering.fs` | `Rendering.Commands.fs`, `Rendering.Processor.fs` |
| Update `Program.fs` | Monolithic | Thin orchestrator, delegates to modules |

**Deliverable:** Same visual output, cleaner file structure.

---

### Session 2: Material System

**Goal:** Replace `BasicEffect` with a proper material abstraction.

| Task | Files | Description |
|------|-------|-------------|
| `Materials.Unlit.fs` | New | Unlit material: albedo color/texture, intensity. Shader: `Unlit.fx` |
| `Materials.PBR.fs` | New | PBR material: albedo, normal, metallic/roughness, AO, emissive. Shader: `PBR.fx` |
| `Rendering.Mesh.fs` | New | `DrawMesh` command carries material reference instead of raw `Model` |
| Shader files | `Shaders/Unlit.fx`, `Shaders/PBR.fx` | Ported from PipelineSample |

**Deliverable:** Player and platforms render with PBR shading instead of BasicEffect.

---

### Session 3: Lighting System

**Goal:** Add directional, point, and spot lights.

| Task | Files | Description |
|------|-------|-------------|
| `Rendering.Lights.fs` | New | Light types (`DirectionalLight`, `PointLight`, `SpotLight`), light commands |
| `Rendering.Commands.fs` | Update | Add `AddLight`, `SetAmbient`, `SetLighting` commands |
| `Rendering.Processor.fs` | Update | Process light commands, pass light data to shader |
| `Shaders/PBR.fx` | Update | Multi-light forward rendering loop |
| `Program.fs` | Update | Configure lights in `init`, add to buffer in `view` |

**Deliverable:** Scene lit by sunlight + 6 orbiting point lights + 2 spot lights.

---

### Session 4: Shadow Mapping

**Goal:** Add shadow casting and receiving.

| Task | Files | Description |
|------|-------|-------------|
| `Rendering.Shadows.fs` | New | Shadow pass commands, shadow map render targets, shadow atlas |
| `Shaders/ShadowCaster.fx` | New | Depth-only shadow pass shader |
| `Shaders/PBR.fx` | Update | Shadow sampling (PCF) in lighting loop |
| `Rendering.Processor.fs` | Update | Two-pass rendering: shadow pass → lighting pass |
| `Program.fs` | Update | Configure shadow settings (bias, cascade splits) |

**Deliverable:** Platforms and player cast/receive soft shadows.

---

### Session 5: Post-Processing

**Goal:** Add bloom and tone mapping.

| Task | Files | Description |
|------|-------|-------------|
| `Rendering.PostProcess.fs` | New | Bloom extract, tone mapping, HDR render target management |
| `Shaders/BloomExtract.fx` | New | Luminance threshold + 4x4 sampling |
| `Shaders/PostProcess.fx` | New | ACES + Reinhard tone mapping, bloom compositing |
| `Rendering.Processor.fs` | Update | Post-process pass after main scene render |

**Deliverable:** HDR bloom glow on emissive player, filmic tone mapping.

---

### Session 6: Polish & Debug Features

**Goal:** Add visual polish and debug rendering.

| Task | Files | Description |
|------|-------|-------------|
| `Rendering.Particles.fs` | New | Billboard particle system (sparks around player) |
| `Rendering.Debug.fs` | New | Velocity vector line, ground projection quad |
| `Rendering.Grid.fs` | Update | Distance-based fade (port `Grid.fx`) |
| `Materials.Grid.fs` | New | Grid material with player-relative fade |
| `Domain.Assets.fs` | Update | Load particle texture, grid effect |

**Deliverable:** Full visual parity with PipelineSample.

---

### Session 7: Documentation & Examples

**Goal:** Make it copy-paste friendly.

| Task | Description |
|------|-------------|
| Add XML comments | Every public function, type, and command documented |
| Add region headers | Clear section markers in each file |
| Create "feature snippets" | Standalone code blocks users can extract |
| Update README | Explain architecture, how to add features |

---

## Design Principles

1. **One concern per file** — `Rendering.Lights.fs` only knows about lights, not shadows or post-process.
2. **Commands are the contract** — `Rendering.Commands.fs` is the single source of truth for what the renderer understands.
3. **Materials own shader binding** — `Materials.PBR.fs` knows how to set PBR shader parameters; the processor just calls `material.Apply(effect, ctx)`.
4. **Processor is a dispatcher** — `Rendering.Processor.fs` pattern-matches on commands and delegates to the right subsystem. No business logic here.
5. **Shaders are portable** — Each `.fx` file is self-contained with `#if OPENGL` guards. Users can drop them into their content folder.
6. **Progressive disclosure** — A user can stop after Session 2 and have a working PBR renderer. Sessions 3-6 are additive.

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Session breaks existing functionality | Each session ends with a working build + visual verification |
| File count gets unwieldy | Keep related features together; don't over-split |
| Shader porting issues | Test each shader in isolation before integration |
| Performance regression | Profile after each session; V3 infrastructure is zero-cost by design |
