# Mibo.Rendering.Graphics3D Skeleton

## Status: Skeleton Only (Types Compile, No Actual Rendering Yet)

## Directory Structure

```
src/Mibo/
├── Mibo.fsproj                     (Updated)
├── Graphics3D.fs                   (Existing - mark Obsolete later)
└── Rendering3D/                    (Created)
    ├── Types.fs                    ✅ Skeleton
    ├── Lighting.fs                 ✅ Skeleton
    ├── Config.fs                   ✅ Skeleton
    ├── RenderTargetPool.fs         ✅ Skeleton
    ├── Pipeline.fs                 ✅ Skeleton
    └── Program.fs                  ✅ Skeleton
```

## What Exists (Skeleton)

### Phase 1: Directory + Project Setup
- [x] Create `src/Mibo/Rendering3D/` directory
- [x] Add files to `Mibo.fsproj`

### Phase 2: Core Types (`Types.fs`)
- [x] `Camera` struct + module (perspective, orthographic, identity)
- [x] `Mesh` struct + module
- [x] `MaterialFlags` enum
- [x] `PBRMaterial`, `Material` structs + builder module
- [x] `Drawable` struct + module
- [x] `RenderCommand` DU (SetCamera, SetViewport, ClearTarget, Draw, DrawCustom)

### Phase 3: Lighting (`Lighting.fs`)
- [x] `ShadowSettings` struct
- [x] `DirectionalLight`, `PointLight`, `SpotLight` structs
- [x] `Light` DU
- [x] `LightingState` struct
- [x] `Lighting` module with presets

### Phase 4: Config (`Config.fs`)
- [x] `PipelineMode`, `ShadowConfig`, `PostProcessConfig`, `PipelineConfig`
- [x] Builder modules

### Phase 5: RenderTargetPool (`RenderTargetPool.fs`)
- [x] `RenderTargetSpec`, `IRenderTargetPool`, module

### Phase 6: Pipeline (`Pipeline.fs`)
- [x] `IRenderPipeline` interface
- [x] `RenderPipeline.forward` - processes `RenderCommand` stream
- [x] BasicEffect fallback (code exists but untested)
- [x] Camera/Lighting state tracking

### Phase 7: Program Integration
- [x] `PipelineRenderer` in `Rendering3D/Program.fs`
- [x] `Program.withPipeline` extension in main `Program.fs`

### Phase 8: Mark Legacy Obsolete
- [ ] Add `[<Obsolete>]` to `Mibo.Elmish.Graphics3D` (deferred)

## What Does NOT Work Yet

| Feature | Status |
|---------|--------|
| Actually rendering to screen | ❌ Untested |
| Mesh loading from Model | ❌ Only `fromModelMeshPart` |
| Culling | ❌ TODO |
| Sorting | ❌ TODO |
| Skinned mesh | ❌ Not implemented |
| Lines/debug | ❌ Not implemented |
| Sprites/billboards | ❌ Not implemented |
| Shadow maps | ❌ Code stub only |
| Post-processing | ❌ Code stub only |

## Next Steps

1. Create test/sample to verify rendering works
2. DSL (DrawableBuilder, RenderBuilder) - separate task
3. Fill in actual rendering implementations
