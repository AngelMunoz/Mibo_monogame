# Mibo.Rendering.Graphics3D Implementation

## Status: Phase 2 Complete → Phase 3 (Tests)

## Completed Phases

### Phase 1: Pipeline Core ✅ COMPLETE

- [x] `SetLighting` command for per-frame lighting override
- [x] `Mesh.fromModelMesh` / `Mesh.fromModel` with correct `BoundingSphere`
- [x] Frustum culling (skip drawables outside camera view)
- [x] Sorting (opaque front-to-back, transparent back-to-front)
- [x] Debug warnings (no camera before draw)
- [x] Module organization (Shared/Forward/ForwardPlus/Deferred/Orchestrate)
- [x] Draw batching (collects draws, flushes on state change)

### Phase 2: DSL ✅ COMPLETE

**File: `Rendering3D/View.fs`**

- [x] `DrawState` - mutable state for building Drawable
- [x] `DrawableBuilder` - CE with `mesh`, `at`, `rotatedBy`, `scaledBy`, etc.
- [x] `RenderBuilder` - CE with `withCamera`, `withLighting`, `withViewport`, `draw { }`
- [x] Inline accessibility fix with `member val buffer` pattern

---

### Phase 3: Tests (Current)

**File: `Mibo.Tests/Rendering3D/`**

- [ ] Buffer ordering tests (commands emitted in correct order)
- [ ] Sorting tests (opaque/transparent separation)
- [ ] Culling tests (outside frustum = not rendered)
- [ ] Orchestration tests (camera set before draws, etc.)

---

### Phase 4: 3D Sample Validation

**File: `samples/3DSample/`**

#### Complexity Ladder:
1. Basic 3D shapes with existing models
2. Ambient lighting with BasicEffect
3. Directional lighting + shadows with BasicEffect
4. Custom PBR shader
5. Shadow maps with ShadowCaster shader
6. Post-processing effects

---

## Directory Structure

```
src/Mibo/Rendering3D/
├── Lighting.fs        ✅ Complete
├── Types.fs           ✅ Complete
├── Config.fs          ✅ Complete
├── RenderTargetPool.fs✅ Complete
├── Pipeline.fs        ✅ Complete (Forward mode)
├── View.fs            ✅ Complete (DSL)
└── Program.fs         ✅ Complete
```
