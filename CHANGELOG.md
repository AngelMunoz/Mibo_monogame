# Changelog

## [Unreleased]

## [1.3.0] - 2026-01-18

### Added

- Rendering: **Modern Render Pipeline** (Authored in `Mibo.Rendering.Graphics3D`). A high-performance, data-oriented alternative to the legacy 3D system.
- Rendering: **HDR Rendering Path**. Pipeline now utilizes `Vector4` (128-bit) targets to preserve high-intensity light for bloom and tone mapping.
- Rendering: **Advanced Post-Processing**. Integrated Bloom (with multi-tap blur) and ACES Tone Mapping.
- Rendering: **Parity with Legacy System**. Ported optimized batchers for Quads, Billboards, and 3D Lines into the modern pipeline.
- Rendering: **Skinned Animation**. Full support for Bone matrices in both the PBR forward shader and shadow casting pass.
- Rendering: **Unlit Routing**. Automatic material-based routing to high-performance unlit or emissive shaders.
- Rendering: **Flexible Line Rendering**. Added `DrawLinesEffect` for batching 3D lines using custom shaders.
- DSL: Enhanced `draw { ... }` with `withBones` and `withEmissive` operations.
- DSL: Enhanced `render { ... }` with `quad`, `billboard`, `line`, and `linesEffect` operations.
- Assets: Extended `PBR.fx` standard shader with full support for Emissive, Metallic/Roughness, and AO maps.

### Changed

- Rendering: Legacy 3D pipeline is now deprecated in favor of the modern system. It remains available under `Mibo.Rendering.Legacy3D` but it now shows a warning.

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
