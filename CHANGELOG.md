# Changelog

## [Unreleased]

### Added

- Rendering: **High-Performance 3D Forward Renderer**. Features CPU-based Tiled Forward culling for efficient management of many lights.
- Rendering: **PBR Material System**. Full support for Physically Based Rendering including Albedo, Normal, Metallic, Roughness, Ambient Occlusion, and Emissive maps.
- Rendering: **Declarative DSL**. Introduced `draw { ... }` computation expression for type-safe, composable creation of 3D drawables (Meshes, Materials, Transforms).
- Rendering: **RenderBuilder API**. Fluent interface for submitting rendering commands (`.camera`, `.lighting`, `.draw`, `.clear`) to the `RenderBuffer`.
- Lighting: Unified **Lighting System** supporting Directional (Sun), Point, and Spot lights with configurable intensity, range, and color.
- Shadows: **Shadow Atlas System**. efficiently packs multiple shadow maps into a single texture. Supports Cascaded Shadow Maps (CSM) for sunlight and localized shadows for point/spot lights.
- Shadows: **Soft Shadows**. Configurable PCF (Percentage Closer Filtering) and penumbra size settings for realistic shadow falloff.
- Pipeline: **Post-Processing Stack**. Built-in configuration for Bloom, SSAO (Screen Space Ambient Occlusion), and high-quality Tone Mapping (ACES, Filmic, Reinhard).
- Pipeline: **Advanced Configuration**. Granular control over the pipeline via `PipelineConfig`, including `TileSize` tuning, Shader overrides, and `PreRenderCallback` hooks.


## [1.2.0] - 2026-01-10

### Added

- Rendering: First-class 3D line rendering support via `DrawLine`, `DrawLines`, and `DrawLinesEffect`.
- Rendering: `LineBatch` for efficient, performance-oriented batching of 3D line primitives using `ArrayPool`.
- Rendering: Optimized `Draw3D.line` and `Draw3D.lines` helpers for submission to the 3D pipeline.
- Assets: Added `Effect` loading support to `IAssets` and `Assets` module for managing custom shaders.
- Assets: Consistent caching for `Effect` assets, parity with textures and models.
- System: Added `dispatch` and `dispatchWith` helpers for scheduling messages into the `System` pipeline.

### Changed

- Program: Refactored `withConfig` to be a cumulative pipeline for additive platform configuration.

- Updated templates to be consistent within them and with the package.

## [1.1.0] - 2026-01-09

### Added

- Animation: `Mibo.Animation` module for format-agnostic 2D sprite handling.
- Animation: `GridAnimationDef` struct for ergonomic, self-documenting grid-based animation definitions.
- Animation: `SpriteSheet` factories for uniform grids, manual frame arrays, and static textures.
- Animation: `AnimatedSprite` runtime for state-driven playback, scaling, and flip control.
- Animation: Optimized index-based playback to eliminate string allocations in update loops.
- Rendering: Direct integration between `AnimatedSprite` and `Graphics2D` rendering primitives.

## [1.0.1] - 2026-01-09

### Fixed

- Templates: fixed broken templates that wouldn't even compile. (my bad)

## [1.0.0] - 2026-01-09

### Added

- Changed something in the package
- Updated the target framework
- Core: `ElmishGame` runtime loop integrating Elmish architecture with MonoGame lifecycle.
- Core: `Program` module for configuring init, update, renderers, and services.
- System: `System` pipeline for organizing frame logic into mutable and read-only phases (`pipeMutable`, `snapshot`, `pipe`).
- Input: Reactive `Input` service for Keyboard, Mouse, Touch, and Gamepad polling.
- Input: `InputMapper` for binding hardware triggers to semantic Game Actions.
- Input: Subscription helpers (`Keyboard`, `Mouse`, `Gamepad`, `Touch`) for Elmish event handling.
- Rendering: `RenderBuffer` for allocation-friendly command sorting and batching.
- Rendering: `Batch2DRenderer` for layer-sorted 2D sprite rendering.
- Rendering: `Camera2D` with viewport bounds and screen-to-world conversion.
- Rendering: `Batch3DRenderer` supporting `BasicEffect`, `SkinnedEffect`, and custom effects.
- Rendering: `Camera3D` with perspective/orbit modes, raycasting, and frustum generation.
- Rendering: Specialized batchers for 3D Sprites (`BillboardBatch`, `SpriteQuadBatch`) and Quads.
- Assets: `IAssets` service for loading and caching Textures, Models, Fonts, and Sounds.
- Time: `FixedStep` configuration for deterministic physics/simulation steps.
- Math: `Culling` module for frustum containment checks.
