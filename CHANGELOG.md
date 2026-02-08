# Changelog

## [Unreleased]

## [1.6.0]

### Added

- Level Design: **2D Layout Engine**. Introduced a code-first, grid-based level design system in `Mibo.Layout`, featuring a composable DSL, optimized geometric primitives (`fill`, `rect`, `circle`, `line`), and domain-specific stamps for Platformers and Top-Down games.
- Level Design: **3D Layout Engine**. Introduced a code-first, grid-based level design system in `Mibo.Layout3D`, featuring a composable DSL, 3D rasterization primitives (`sphere`, `cylinder`, `line`), and domain-specific stamps for Interiors and Terrain.
- Documentation: **Comprehensive Level Design Guides**. Added guidance-focused documentation for both 2D and 3D engines, including the "Stamp" pattern for reusable components and idiomatic rendering using `RenderLayer`.

## [1.5.1] - 2026-01-29

### Fixed

- Rendering: Corrected `RenderBuffer.Sort` logic to use in-place array sorting. Previously, it sorted a temporary copy, causing `RenderLayer` depth keys to be ignored during draw call submission.

## [1.5.0] - 2026-01-25

### Added

- Rendering: **Enhanced 2D Render Pipeline**. Integrated a multi-pass post-processing system in `Batch2DRenderer` supporting global effects: **Vignette**, **Bloom**, and **Color Grading (3D LUT)**.
- Rendering: **Selective 2D Effects**. Added support for per-batch shader effects using the `SetEffect` command, enabling targeted effects like grayscale on specific entity groups.
- Rendering: **Render Target Pooling**. Introduced `IRenderTargetPool` to efficiently manage intermediate buffers during complex 2D post-processing passes, reducing allocation overhead.
- Rendering: **Layered Compositing**. Added `FinalBlendState` to `Batch2DConfig` to allow 2D layers (including post-processed ones) to composite cleanly over previous 3D or 2D scenes.
- Rendering: **Tiled 2D Lighting System**. Added GPU-accelerated point lighting with screen-space tile binning for efficient multi-light support.
- Rendering: **2D Dynamic Shadows**. Integrated a polar/orthographic shadow atlas system for point and directional lights, supporting soft shadows and occluder segments.
- Rendering: **High-Performance 2D Particles**. Repurposed `BillboardBatch` for 2D space, enabling GPU-accelerated particle systems with rotation and custom UVs.
- Rendering: **2D Primitive Batching**. Added `DrawLine2D`, `DrawRect2D`, and `DrawCircle2D` commands using `LineBatch` for efficient shape rendering.
- Rendering: `PointLight2D` struct with position, color, intensity, radius, and falloff properties.
- Rendering: `DirectionalLight2D` struct with direction, color, and intensity properties for global lighting (e.g., sun/moon).
- Rendering: `Lighting2DConfig` for enabling/disabling 2D lighting with configurable ambient color, tile size, and max lights per tile.
- Rendering: `buffer.PointLight`, `buffer.DirectionalLight`, and `buffer.Occluder` fluent APIs for submitting lighting data to the 2D render pipeline.
- Rendering: Screen-space light transformation for correct world-to-screen light binning when using cameras.
- Shaders: `lighting.fx` and `grayscale.fx` implement the Tiled Lighting Contract with normal map support and light clamping.
- DSL: Updated `sprite` and `text` computation expressions for Improved performance via direct struct-based command submission.

### Fixed

- Rendering: Corrected rendering state management in `Batch2DRenderer` to fix "accumulation trails" by ensuring render targets are set before clearing.
- Rendering: Improved `Batch2DRenderer` to always clear internal pooled targets to `Transparent`, preventing garbage data from persisting across frames while still allowing background preservation via `ClearColor: ValueNone`.
- Rendering: Fixed winding order in `BillboardBatch.draw2D` to prevent culling in 2D (Y-down) coordinate systems.
- Rendering: Improved `DrawParticles` robustness in `Batch2DRenderer` by explicitly handling `BasicEffect` property mapping and camera matrix propagation.

## [1.4.0] - 2026-01-19

### Added

- Rendering: **Property-Driven Camera API**. Refactored `Camera` struct to preserve projection and view parameters, enabling a self-documenting DSL for camera configuration.
- Rendering: **Composable Camera DSL**. Introduced pipeline-friendly helpers including `withFov`, `withAspect`, `withRange`, `at`, `lookingAt`, `lookAt`, `withDistance`, `withAngles`, and `orbit` for ergonomic camera manipulation.
- Rendering: **Aggregate Dynamic Lighting**. Introduced `AddLight` command and `withDefaultLighting` configuration to support modular, additive lighting contributions. Modules can now independently contribute lights (point, spot, directional) to the scene aggregate without destructive overrides of global lighting state.

### Changed

- Rendering: **Performance Optimization**. Consolidated `PreRenderCallback` and lighting updates into `Drawing.flush` to eliminate redundant array allocations every frame.
- Templates: **Modern 3D Template**. Migrated the `Mibo3D` project template to utilize the high-performance `Mibo.Rendering.Graphics3D` engine and the new composable Camera API.

### Fixed

- Rendering: Removed CPU-side vertex data scanning for mesh bounding box computation. This prevents crashes on Android and other platforms where GPU vertex buffers are often allocated as write-only. Bounding boxes are now derived safely from bounding spheres.
- Rendering: Eliminated redundant null checks and boxing in `configureBasicEffectLighting` for better error visibility and performance.

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
