# Rendering Pipeline Debug & Validation Plan

## Current Status
- ✅ **Tier 0**: BasicEffect fallback works (platforms/ball visible)
- ⚠️ DSL commands work (camera, clear), but lighting NOT yet verified
- Mode switching (1/2/3 keys) has no visible effect yet (expected without shaders)

---

## Phase 1: Forward Mode - Lighting Validation

### Step 1.1: Verify DSL Lighting is Applied
**Test**: Change `withLighting Lighting.defaultSunlight` to `withLighting Lighting.ambient`
**Expected**: Scene becomes flat-lit (no directional highlights/shadows)
**If No Change**: The DSL `SetLighting` command isn't being applied to BasicEffect
**Status**: ⏳ NOT YET TESTED

### Step 1.2: Test Lighting Override
**Test**: Change `withLighting Lighting.defaultSunlight` to `withLighting Lighting.ambient`
**Expected**: Flat lighting, no directional shadows
**Verify**: Run sample, observe lighting difference

### Step 1.3: Test Multiple Lights
**Test**: Create custom `LightingState` with 3 directional lights
**Expected**: All 3 lights affect the scene (BasicEffect supports 3 directional)
**Verify**: Visual inspection

---

## Phase 2: Forward Mode - Shadows

### Step 2.1: Enable Shadow Config (No Shader)
**Config**:
```fsharp
PipelineConfig.forward
|> PipelineConfig.withShadows ShadowConfig.defaults
```
**Expected**: No crash, renders same as before (shadow pass skipped without shader)
**Verify**: Run sample, check for errors

### Step 2.2: Add ShadowCaster Shader
**Config**:
```fsharp
PipelineConfig.forward
|> PipelineConfig.withShadows ShadowConfig.defaults
|> PipelineConfig.withShader ShaderBase.ShadowCaster "Effects/ShadowCaster"
```
**Expected**: Shadow maps rendered, but NOT applied (no receiving shader)
**Debug**: Add debug output in `renderShadowPass` to verify it runs
**Verify**: Check shadow map count, verify no crash

### Step 2.3: Create Debug Shadow Visualization
**Action**: Temporarily blit shadow map to corner of screen
**Purpose**: Verify shadow map contains depth data (not all white/black)
**Code Location**: After `renderShadowPass` in `flushDrawBatch`

---

## Phase 3: Forward Mode - PBR Shader

### Step 3.1: Add PBR Shader (Forward)
**Config**:
```fsharp
PipelineConfig.forward
|> PipelineConfig.withShader ShaderBase.PBRForward "Effects/PBR"
```
**Expected**: Custom shader renders scene
**Debug Points**:
- Verify shader loads (`state.CustomShaders.ContainsKey`)
- Verify `renderDrawableWithEffect` is called
- Check shader technique exists

### Step 3.2: PBR + Shadows Combined
**Config**:
```fsharp
PipelineConfig.forward
|> PipelineConfig.withShadows ShadowConfig.defaults
|> PipelineConfig.withShader ShaderBase.ShadowCaster "Effects/ShadowCaster"
|> PipelineConfig.withShader ShaderBase.PBRForward "Effects/PBR"
```
**Expected**: Shadows visible in PBR rendering
**Debug**: Ensure shadow map is bound to PBR shader

---

## Phase 4: ForwardPlus Mode

### Step 4.1: ForwardPlus Without Shaders
**Config**: Set initial mode to `ForwardPlus`
**Expected**: Falls back to Forward mode (no PBR shader)
**Verify**: Confirm fallback logic in `ForwardPlus.render`

### Step 4.2: ForwardPlus With PBR
**Config**: Add PBR shader, set mode to ForwardPlus
**Expected**: Light culling runs, passes tile data to shader
**Debug**: Log tile grid dimensions, verify cullLights produces valid data

### Step 4.3: ForwardPlus Multi-Light Test
**Test**: Add 10+ point lights to scene
**Expected**: Performance similar to Forward for visible lights
**Verify**: FPS counter, visual light contribution

---

## Phase 5: Deferred Mode

### Step 5.1: Deferred Without Shaders
**Config**: Set mode to `Deferred`
**Expected**: Falls back to Forward mode
**Verify**: Confirm fallback in `Deferred.render`

### Step 5.2: Deferred With GBuffer Shader Only
**Config**:
```fsharp
|> PipelineConfig.withShader ShaderBase.GBufferFill "Effects/GBuffer"
```
**Expected**: Still falls back (needs both GBuffer + DeferredLighting)
**Verify**: Check condition in `Deferred.render`

### Step 5.3: Deferred Full Pipeline
**Config**:
```fsharp
|> PipelineConfig.withShader ShaderBase.GBufferFill "Effects/GBuffer"
|> PipelineConfig.withShader ShaderBase.DeferredLighting "Effects/DeferredLighting"
```
**Expected**: G-Buffer pass + Lighting pass + Transparent forward
**Debug**: Visualize G-Buffer albedo/normal as debug output

---

## Phase 6: Post-Processing

### Step 6.1: Enable Post-Process (No Shader)
**Config**:
```fsharp
|> PipelineConfig.withPostProcess PostProcessConfig.defaults
```
**Expected**: Scene renders to RT, blits to backbuffer
**Verify**: No visual difference (passthrough)

### Step 6.2: Add Post-Process Shader
**Config**: Add `ShaderBase.PostProcess` shader
**Expected**: Tone mapping applied
**Verify**: Visual difference (ACES curve)

---

## Debug Utilities Needed

1. **Shader Load Logging**: Print shader names as they load in `Orchestrate.initialize`
2. **Command Count Logging**: Print buffer.Count in each render mode
3. **RT Visualization**: Helper to blit any RT to screen corner
4. **FPS Counter**: Time each frame for perf testing

---

## Current Next Step

**Start with Phase 2, Step 2.1**: Enable shadow config, verify no crash.
