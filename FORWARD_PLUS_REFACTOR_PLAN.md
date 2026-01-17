# Tiled Forward Refactor & Multi-Shadow Integration Plan

## Executive Summary
**Goal:** Consolidate the rendering engine into a single, unified **Tiled Forward** pipeline. This involves a **Major Feature Implementation** (Multi-Shadows) alongside the removal of legacy Deferred/Simple Forward paths.

**Key Outcome:** A simplified, robust F# API (`draw { ... }`) where users add lights and meshes, and the engine automatically scales from "Restricted/Discrete" to "Modern/Array" architectures based on the platform.

---

## Phase 1: Cleanup & Removal (The "Great Purge")
**Objective:** Remove redundant and complex legacy code to clear the path for the unified pipeline.

1.  **Delete Deferred Pipeline:**
    *   Remove `Deferred.fs` module (~450 lines).
    *   Remove `DeferredLighting.fx` and `GBuffer.fx` shaders.
    *   Remove G-Buffer RenderTarget allocation logic in `RenderTargetPool`.
2.  **Remove "Simple" Forward Mode:**
    *   Remove the distinct `Forward.fs` module (~175 lines). The logic for "Simple Forward" will be absorbed into the Tiled Forward "Restricted" mode.
3.  **Refactor Pipeline Config:**
    *   Remove `PipelineMode` enum.
    *   Add `ShadowPath` configuration: `Auto` (default), `ForceDiscrete` (GL behavior), `ForceArray` (DX behavior).

---

## Phase 2: The Unified Tiled Forward Core
**Objective:** Promote the existing `ForwardPlus.fs` module to be the single source of truth (`TiledForward.fs`).

1.  **Promote Existing Culling Logic (`LightCuller`):**
    *   **Status**: CPU-based Tiled Culling *already exists* and is functional. Preserve this logic.
    *   **Enhancement**: Add a "Sort & Select" fallback for the Restricted Path (OpenGL). Instead of tiles, select the *Top N* lights globally to fit strict `ps_3_0` uniform limits.
2.  **Shader Harmonization:**
    *   Update the **Sample's Reference Shader** (`PBR.fx`) to serve as the canonical example.
    *   **Targeting**: Demonstrate explicit targeting of `ps_3_0` for OpenGL and `ps_5_0` for DirectX via macros.

---

## Phase 3: Multi-Shadow Integration (New Feature Implementation)
**Objective:** Build a complete Multi-Shadow system from scratch to replace the current single-directional-shadow placeholder.

**Current State**: Code only supports 1 Directional Light shadow (hardcoded to index `[0]`).
**New State**: Support N Directional, Point, and Spot shadows via Hybrid architecture.

1.  **Data Structures (New Infrastructure):**
    *   Update `PipelineState` to hold `ShadowMapArray` (Texture2DArray for DX) AND `DiscreteShadowMaps` (List<RenderTarget2D> for GL).
    *   Update `ShadowConfig` to include: `Bias`, `NormalBias`, `MaxPointShadows`.
2.  **Shadow Generation (`ShadowPass` - New Logic):**
    *   **Spot Lights**: Implement shadow matrix computation (currently nonexistent).
    *   **Point Lights**: Implement 6-face shadow matrix computation (currently nonexistent).
    *   **Culling**: Add frustum culling per light-view (currently nonexistent).
    *   **Performance**: Allow users to configure `MaxPointShadows` to control the cost of 6-pass rendering on the Restricted path.
3.  **Binding Strategy (The "Bridge" - New Logic):**
    *   **DirectX**: Implement `Texture2DArray` binding logic.
    *   **OpenGL**: Implement discrete slot binding loop (`ShadowMap0`, `ShadowMap1`...).
    *   **Fix**: Remove the hardcoded `[0]` index binding and replace with dynamic loop binding.

---

## Phase 4: User Experience & Documentation
**Objective:** Maintain a strict separation between engine logic and shader content.

1.  **Agnostic API Surface:**
    *   Mibo provides a standardized **API Surface** (Effect Parameters) that users implement in their own HLSL.
    *   The engine logic remains purely about data processing and draw call orchestration.
2.  **Documentation:**
    *   Update `rendering.md` to explain the unified pipeline.
    *   **Platform Matrix**: Explicitly document the differences:

| Feature | DirectX (ps_5_0) | OpenGL (ps_3_0) |
|---------|-------------------|------------------|
| Shader Model | ps_5_0 | ps_3_0 |
| Max Uniforms | 65536 | ~256 |
| Dynamic Loops | ✅ Full support | ⚠️ Limited |
| Texture2DArray | ✅ Yes | ❌ No |
| Shadow Strategy | Texture2DArray | Discrete slots |
| Max Shadows | Configurable (e.g., 64-256) | Fixed (e.g., 4-8) |
| Point Light Shadows | 6 faces per light (efficient) | 6 passes per light (expensive) |
| Culling | CPU tiled (bitmasks) | CPU tiled (sorted selection) |

---

## Phase 5: Verification
**Tests:**
1.  **Sample 1 (Restricted Path)**: Force `ShadowPath = ForceDiscrete`. Verify behavior matches OpenGL constraints (limited lights/shadows).
2.  **Sample 2 (Modern Path)**: Force `ShadowPath = ForceArray`. Verify "Unlimited" lights/shadows on DirectX.
3.  **Shadow Quality**: Verify Bias settings eliminate acne without creating "peter-panning".
4.  **Culling**: Verify objects outside light range do not cast shadows.

---

## Phase 6: Extensibility Hooks
**Objective:** Empower advanced users to implement "True Forward+" (GPU Compute) or other custom lighting techniques without forking the engine.

1.  **Pipeline Hooks:**
    *   Add `PreRenderCallback` to `PipelineConfig`: `(GraphicsDevice -> Camera -> LightingState -> unit)`.
        *   **Usage**: Allows users to run Compute Shaders, update StructuredBuffers, or perform custom passes (e.g., Depth Pre-Pass) before the main render.
    *   Add `LightingBinder` to `PipelineConfig`: `(Effect -> Camera -> LightingState -> unit)`.
        *   **Usage**: If provided, overrides Mibo's default `LightDirections`/`SpotLightData` binding logic. Users can bind their own Compute Shader results (Light Grids, Index Lists) directly to their custom shaders.

**Outcome:** Mibo handles the "Boring" stuff (Scene Graph, Culling, Sorting, Draw Calls), while the user can completely replace the "Brain" of the lighting engine (Light Culling & Data Transfer) if the default CPU Tiled Forward implementation isn't sufficient.

---

## Risk Assessment
*   **Shader Complexity**: Maintaining dual paths (Array vs Discrete) in user shaders can be verbose. *Mitigation*: The documentation will provide clear examples of `#if` blocks to handle this specific difference cleanly.
*   **Uniform Limits on GL**: `ps_3_0` has tight constant limits. *Mitigation*: The "Restricted Path" culler must strictly respect these limits (e.g., max 8 active lights per draw) to prevent crashes.