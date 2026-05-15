# 3D Rendering Pipeline Rewrite — Implementation Plan

> Based on GitHub Issue #15 and subsequent design analysis.

## 1. Motivation

The current 3D rendering pipeline (`src/Mibo/Rendering3D/`) has confirmed architectural problems:

| # | Problem | Evidence |
|---|---------|----------|
| 1 | `EffectOverride` silently dropped when global shader registered | `Pipeline.fs:1229-1237` |
| 2 | ~28 hardcoded `SafeSetParam` calls per drawable, 14 redundant | `Pipeline.fs:819-937` |
| 3 | 29 unique hardcoded parameter name strings | Across `Pipeline.fs` |
| 4 | Tiled culling computed but discarded | `Pipeline.fs:1226` (`let _tileMasks`) |
| 5 | `ShaderBase` DU limits to exactly 5 shader types | `Types.fs:31-41` |
| 6 | No prepare/apply split for lighting (unlike 2D pipeline) | `Graphics2D.fs:660-814` |
| 7 | `DrawCustom` gives bare `GraphicsDevice`, no pipeline data | `Types.fs:604` |
| 8 | `"Intensity"` bound twice (duplicate) | `Pipeline.fs:903,915` |

These problems force users into "fork or fight" situations when their shader needs don't match the hardcoded PBR contract.

## 2. Design Goals

1. **Framework never dictates parameter names** — parameter names live only in user code
2. **Unlimited shader types** — no DU slot limit
3. **~7× reduction in redundant SafeSetParam calls** via three-phase binding
4. **Zero string lookups at draw time** via pre-resolved `EffectParameter` handles
5. **Heterogeneous scenes** — PBR + toon + unlit coexist without type erasure
6. **Align 3D with proven 2D patterns** — prepare/apply split, `RendererBuffers` caching
7. **Incremental migration** — current pipeline continues to exist

## 3. Proposed Architecture

### 3.1 Core Types

```fsharp
/// Framework-computed data, provided to every binding at draw time
[<Struct>]
type RenderContext = {
    Camera: Camera
    LightingState: LightingState
    LightDataTexture: Texture2D voption
    LightCount: int
    ShadowAtlas: Texture2D voption
    ShadowMatrixTexture: Texture2D voption
    ShadowMatrixCount: int
    ShadowAtlasTilesX: int
    ShadowAtlasSize: float32
    ShadowBias: float32
    ShadowNormalBias: float32
    AmbientColor: Vector3
    AmbientIntensity: float32
}

/// Long-lived binding, created ONCE per material/shader combo.
/// Closures capture pre-resolved EffectParameter handles — zero string lookups at draw time.
type EffectBinding = {
    Effect: Effect
    MaterialKey: int
    BindGlobal: RenderContext -> unit
    BindPerMaterial: unit -> unit
    BindPerInstance: Matrix -> Matrix[] voption -> unit
}

/// Lightweight struct, recreated each frame.
/// Only carries data the framework inspects + Bones (consumed by binding, not framework).
[<Struct>]
type Drawable = {
    Mesh: Mesh
    Transform: Matrix                      // framework reads (sorting)
    Bones: Matrix[] voption                // binding consumes (skinning)
    BoundingSphere: BoundingSphere         // framework reads (culling)
    Pass: RenderPass                       // framework reads (pass assignment)
    MaterialKey: int                       // framework reads (batching)
    Binding: EffectBinding                 // framework reads Effect + MaterialKey
}

/// Sort key for DrawCustom — gives framework what it needs for ordering
[<Struct>]
type SortKey = {
    Distance: float32
    Pass: RenderPass
    MaterialKey: int
    Effect: Effect
}
```

### 3.2 Two-Vehicle Model

| Vehicle | Coverage | Purpose |
|---------|----------|---------|
| `Drawable` | ~90% | Common case: mesh + transform + bones + standard material |
| `DrawCustom + SortKey` | ~10% | Escape hatch: morph weights, custom animation, novel data |

**Why not `Drawable<'T>`?** Generic type infects the entire pipeline — `ResizeArray<Drawable<'T>>`, sorting, batching, culling all become generic. Heterogeneous scenes become impossible without `obj` casting.

**Why not SRTP?** Cannot be captured in closures that outlive the call site. `EffectBinding` closures are created at init, used at draw time.

### 3.3 Three-Phase Binding Performance

```
Phase 1 - BindGlobal (once per frame per effect):
  RenderContext → pre-resolved EffectParameter.SetValue(...)
  ~10 calls, no string lookups

Phase 2 - BindPerMaterial (once per unique material per effect):
  Closure-captured material data → pre-resolved EffectParameter.SetValue(...)
  ~10 calls, no string lookups

Phase 3 - BindPerInstance (once per drawable):
  Transform, Bones → pre-resolved EffectParameter.SetValue(...)
  2-3 calls, no string lookups

Opaque sort: (Effect, MaterialKey, Distance front-to-back)
  → minimizes state changes, correct by Z-buffer

Transparent sort: (Distance back-to-front, stable-sort by Effect as tiebreaker)
  → correct blending, accept re-binding cost
```

**Cost comparison (1000 opaque drawables, 10 materials):**

| | Current | Proposed |
|---|---|---|
| SafeSetParam calls | 28,000 (string hash + dict lookup) | 1,110 EffectParameter.SetValue (direct) |
| String lookups | 28,000 | 0 |
| Tiled culling | Computed, discarded | Computed, bound via RenderContext |

### 3.4 RenderCommand Evolution

```fsharp
type RenderCommand =
    | SetCamera of Camera
    | SetLighting of LightingState
    | AddLight of Light
    | SetViewport of Viewport
    | ClearTarget of Color voption * bool
    | Draw of Drawable                                    // REVISED
    | DrawSpriteQuad of SpriteQuadCmd                     // unchanged
    | DrawSpriteBillboard of SpriteBillboardCmd           // unchanged
    | DrawQuadEffect of EffectQuadCmd                     // unchanged
    | DrawBillboardEffect of EffectBillboardCmd           // unchanged
    | DrawLine of ...                                     // unchanged
    | DrawLines of ...                                    // unchanged
    | DrawLinesEffect of ...                              // unchanged
    | DrawCustom of SortKey * (GameContext * RenderContext -> unit)  // REVISED
```

## 4. Package Structure

The new 3D pipeline becomes a separate package (`Mibo.Rendering3D`) that depends on `Mibo` core:

```
src/
  Mibo/                          # Core framework (unchanged)
  Mibo.Rendering3D/              # NEW — 3D rendering pipeline
    Types.fs                     # RenderContext, EffectBinding, Drawable, SortKey
    Bindings.fs                  # Reference implementations (basicEffect, pbr)
    Pipeline.fs                  # Orchestration: passes, sorting, batching
    Drawing.fs                   # Three-phase binding execution
    Lighting.fs                  # Light data packing (reused from current)
    Tiling.fs                    # Tiled culling (now actually used)
    Shadows.fs                   # Shadow pass orchestration
    PostProcess.fs               # CustomPostProcessPass (adopted from 2D)
    Config.fs                    # Pipeline3DConfig
    View.fs                      # draw { ... } computation expression
  Mibo.Tests/                    # Tests
  samples/3DSample/              # Migrated sample
  samples/PipelineSample/        # Migrated sample
```

### 4.1 What Gets Removed

| Current | Replaced By |
|---------|-------------|
| `ShaderBase` DU (5 slots) | `EffectBinding` per drawable — unlimited shader types |
| `drawWithEffect` (119 lines of hardcoded SafeSetParam) | User-provided `BindGlobal`/`BindPerMaterial`/`BindPerInstance` |
| `drawFallback` separate code path | `Bindings.basicEffect` — unified path |
| `EffectOverride` on Drawable (silently dropped) | Obsoleted — every drawable has explicit `EffectBinding` |
| `LightingBinder` global override | `BindGlobal` in EffectBinding |
| `ShaderOverrides` map on PipelineConfig | Per-drawable EffectBinding |
| 29 hardcoded parameter name strings | Live in user code or reference bindings |
| `DrawCustom(GraphicsDevice * Camera)` | `DrawCustom(SortKey * (GameContext * RenderContext -> unit))` |
| `ICustomInstanceData` on Drawable | Removed — 10% path uses `DrawCustom + SortKey` |

### 4.2 What Gets Added

1. `RenderContext` — computed data struct (camera, lighting, shadows)
2. `EffectBinding` — user-provided binding type with three-phase model
3. `SortKey` — lightweight ordering data for DrawCustom
4. `Bindings` module — reference implementations (basicEffect, pbr) as docs/samples
5. `DrawCustom` with SortKey + RenderContext — 10% escape hatch
6. `CustomPostProcessPass` for 3D — adopted from 2D pipeline
7. Adopt 2D's `Pipeline.prepare`/`Pipeline.apply` split
8. Adopt 2D's `RendererBuffers` caching pattern — `LightingPrepared` flag + matrix comparison

### 4.3 What Stays the Same

- MonoGame dependency, SM3 target (vs_3_0 / ps_3_0)
- Tiled forward rendering with CPU culling (now actually used)
- Shadow atlas architecture
- `SafeSetParam` caching in `Render.Shared.fs` (used by reference bindings)
- `RenderBuffer` command pattern
- Sprite/billboard/line command types and batchers

## 5. Migration Path

The current 3D pipeline (`Rendering3D/`) will continue to exist alongside the new package. Users migrate incrementally:

1. Replace `ShaderBase` slot configuration with per-drawable `EffectBinding`
2. Move hardcoded `SafeSetParam` names from framework code to user binding functions (or use `Bindings.basicEffect` / `Bindings.pbr` reference implementations)
3. Replace `EffectOverride` with explicit `EffectBinding` on each drawable
4. Replace `LightingBinder` with `BindGlobal` in the effect binding
5. Replace `DrawCustom(Device * Camera)` with `DrawCustom(SortKey * (GameContext * RenderContext -> unit))`

Reference binding implementations will be provided as sample code, not as framework code.

## 6. Implementation Phases

### Phase 1: Foundation (Types + Bindings)
- [ ] Create `Mibo.Rendering3D` project
- [ ] Define `RenderContext`, `EffectBinding`, `Drawable`, `SortKey` types
- [ ] Implement `Bindings` module with `basicEffect` and `pbr` reference bindings
- [ ] Ensure pre-resolved `EffectParameter` handle capture in closures
- [ ] Write unit tests for binding creation and parameter resolution

### Phase 2: Pipeline Orchestration
- [ ] Implement `Pipeline3DConfig` with prepare/apply split
- [ ] Adopt `RendererBuffers` caching pattern from 2D (`LightingPrepared` flag, matrix comparison)
- [ ] Implement three-phase binding execution in `Drawing.fs`
- [ ] Implement sort + batch: composite key (Effect → MaterialKey → Distance)
- [ ] Wire up tiled culling (no longer discarded)

### Phase 3: Render Commands + View API
- [ ] Define revised `RenderCommand` union
- [ ] Implement `DrawCustom(SortKey * (GameContext * RenderContext -> unit))`
- [ ] Implement `draw { ... }` computation expression for new Drawable
- [ ] Implement shadow pass orchestration
- [ ] Implement `CustomPostProcessPass` for 3D (adopted from 2D)

### Phase 4: Sample Migration + Tests
- [ ] Migrate `samples/3DSample/` to new API
- [ ] Migrate `samples/PipelineSample/` to new API
- [ ] Integration tests: heterogeneous scenes (PBR + toon + unlit)
- [ ] Performance benchmarks: SafeSetParam call count comparison
- [ ] Verify both samples produce identical visual output

### Phase 5: Documentation + Deprecation
- [ ] Document migration guide
- [ ] Add `[<Obsolete>]` attributes to old `ShaderBase`, `EffectOverride`, `LightingBinder`
- [ ] Update FsDocs documentation site
- [ ] Changelog entry under `[Unreleased]`

## 7. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| 90/10 split misjudged — DrawCustom becomes primary path | Medium | Adding fields to Drawable is a minor version bump, not a redesign |
| Reference bindings become de-facto standard, recreating rigidity | Medium | Keep them as samples/docs, not framework code; encourage user-written bindings |
| `MaterialKey` as bare `int` causes silent batching errors | Low | Use newtype wrapper `[<Struct>] type MaterialKey = MaterialKey of int` at zero cost |
| Performance improvement less dramatic than claimed | Low | Direction is correct; actual savings depend on scene composition |
| Migration friction causes users to stay on old pipeline | Low | Old pipeline continues to work; incremental migration path |

## 8. Sample Use Case Coverage

### 3DSample
| Use Case | Coverage |
|----------|----------|
| Grid-based level rendering via `CellGrid3D.iterVolume` | Drawable with DefaultBinding |
| Player ball with rotation | Drawable with transform matrix |
| Custom grid lines with shader | DrawCustom with RenderContext (better than current) |

### PipelineSample
| Use Case | Coverage |
|----------|----------|
| PBR platforms with textures | Drawable with reference PBR binding |
| Dynamic point lights (player torch) | RenderContext.LightingState |
| Moving colored lights in orbit | RenderContext.LightingState |
| Spotlights per platform | RenderContext.LightingState |
| Emissive pulse on player | MaterialData.EmissiveColor/Intensity |
| Particle billboards | Unchanged (sprite commands) |
| Debug quad + velocity line | Unchanged |
| Custom grid with DrawCustom | DrawCustom with RenderContext (strictly better) |
| Bloom + ACES tone mapping | CustomPostProcessPass (adopted from 2D) |
| Shadow mapping with cascades | Shadow pass orchestration |

## 9. Alignment with Mibo Principles

| Principle | Alignment |
|-----------|-----------|
| Zero-cost abstractions | EffectBinding closures capture handles at init; draw-time is direct SetValue |
| Prefer structs over classes | RenderContext, Drawable, SortKey, EffectBinding are all structs |
| Avoid heap allocations in hot paths | Structs avoid heap; closures created once at init |
| Favor arrays and spans over lists | Bones is `Matrix[] voption`; RenderCommand uses ResizeArray internally |
| Favor ArrayPool where possible | RendererBuffers.ensureCapacity pattern adopted from 2D |
| Functional programming with mutable state for performance | Binding functions are pure `-> unit`; pipeline state is mutable internally |
| Well-documented public API | Type definitions and usage examples provided |
| Elmish-friendly patterns | Animation follows 2D AnimatedSprite pattern (update → toDrawable → submit) |
