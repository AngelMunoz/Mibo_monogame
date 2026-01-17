# Multi-Shadow System Design

## Overview

Enable shadows for **arbitrary numbers of Directional, Point, and Spot lights** across all rendering modes (Forward, Forward+, Deferred) with cross-platform support (OpenGL + DirectX).

## Current State

### What Works
- ✅ Arbitrary numbers of Directional, Point, and Spot lights for lighting
- ✅ Point light data binding (up to 32, but extensible)
- ✅ Data structures for ShadowMaps, ShadowViewMatrices, ShadowProjectionMatrices
- ✅ Shadow generation for Directional lights only

### What's Missing
- ❌ Shadow generation for Point lights
- ❌ Shadow generation for Spot lights
- ❌ Only first shadow map bound to shaders (hardcoded `[0]` index)
- ❌ Shaders only apply shadow to first directional light

---

## Design Goals

1. **Arbitrary Shadows**: Support any number of Directional, Point, and Spot light shadows (practically limited by GPU)
2. **Cross-Platform**: Work identically on OpenGL and DirectX via MonoGame
3. **All Rendering Modes**: Forward, Forward+, and Deferred
4. **Shader Flexibility**: Provide shadow data in arrays; let user shaders decide how many to use
5. **Backward Compatible**: Default behavior should not break existing code

---

## Shadow Data Architecture

### Per-Light Shadow Requirements

| Light Type | Shadow Map Format | View Projections | Storage |
|------------|------------------|------------------|---------|
| **Directional** | 2D depth map | 1 per light (or N for cascades) | 2D texture array slice |
| **Spot** | 2D depth map | 1 per light | 2D texture array slice |
| **Point** | Cubemap depth OR 6x 2D depth maps | 1 per light (cubemap) or 6 per light | Cubemap array OR 6x slices in 2D array |

### Simplified Approach (Recommended)

For initial implementation, use **2D texture arrays for all light types**:

- **Directional**: 1 slice per light
- **Spot**: 1 slice per light
- **Point**: 6 slices per light (one per face)

This keeps the implementation simple and uniform across all light types.

---

## Data Structures

### PipelineState Updates

```fsharp
type internal PipelineState = {
  // ... existing fields ...
  
  // Shadow Maps - Changed to Texture2DArray for all light types
  ShadowMapArray: Texture2DArray voption
  
  // Per-light shadow metadata
  ShadowLightTypes: ResizeArray<LightType>  // Directional, Spot, Point
  ShadowLightIndices: ResizeArray<int>     // Index in original lights array
  
  // Matrices (one per shadow map slice)
  ShadowViewMatrices: ResizeArray<Matrix>
  ShadowProjectionMatrices: ResizeArray<Matrix>
  
  // Per-light index range in shadow data arrays
  DirectionalShadowRanges: ResizeArray<struct (int * int)>  // (start, count)
  SpotShadowRanges: ResizeArray<struct (int * int)>
  PointShadowRanges: ResizeArray<struct (int * int)>
}

and LightType = DirectionalLight | SpotLight | PointLight
```

### Shader Bindings

Provide three sets of data arrays for user shaders:

```hlsl
// All shadow maps in one array (directional + spot + point faces)
Texture2DArray ShadowMaps;
sampler ShadowSampler;

// View/Projection matrices for each shadow map slice
float4x4 ShadowViews[256];   // MAX_SHADOWS configurable
float4x4 ShadowProjs[256];

// Light index mapping (which shadow slices belong to which light)
// For point lights: 6 consecutive indices for the 6 faces
uint4 LightShadowRanges[256];  // x=start, y=count, z=lightIndex, w=lightType
```

---

## Shadow Generation

### Unified `renderShadowPass`

```fsharp
let renderShadowPass(state: PipelineState) =
  match state.CustomShaders.TryGetValue(ShaderBase.ShadowCaster) with
  | true, shadowEffect ->
    
    // Allocate shadow map array if not exists
    let maxShadows = calculateRequiredShadows state.CurrentLighting.Lights
    ensureShadowMapArray state maxShadows
    
    let mutable sliceIndex = 0
    
    for i, light in enumerate state.CurrentLighting.Lights do
      match light with
      | Directional dl when ValueOption.isSome dl.Shadow ->
        let startIdx = sliceIndex
        // Single slice for directional light
        let view, proj = computeDirectionalShadowMatrices dl state
        renderToSlice state shadowEffect sliceIndex view proj state.OpaqueDrawables
        recordShadowData state DirectionalLight i startIdx 1
        sliceIndex <- sliceIndex + 1
        
      | Spot sl when ValueOption.isSome sl.Shadow ->
        let startIdx = sliceIndex
        // Single slice for spot light (perspective projection)
        let view, proj = computeSpotShadowMatrices sl
        renderToSlice state shadowEffect sliceIndex view proj state.OpaqueDrawables
        recordShadowData state SpotLight i startIdx 1
        sliceIndex <- sliceIndex + 1
        
      | Point pl when ValueOption.isSome pl.Shadow ->
        let startIdx = sliceIndex
        // 6 slices for cubemap faces
        let views, proj = computePointShadowMatrices pl
        for faceIdx = 0 to 5 do
          renderToSlice state shadowEffect (sliceIndex + faceIdx) 
                       views.[faceIdx] proj state.OpaqueDrawables
        recordShadowData state PointLight i startIdx 6
        sliceIndex <- sliceIndex + 6
        
      | _ -> () // Light has no shadow
    
    // Store total shadow count
    state.ShadowCount <- sliceIndex
    
  | false, _ ->
    // No shadow caster shader - skip shadow pass
    ()
```

### Shadow Matrix Computation

```fsharp
let computeDirectionalShadowMatrices 
  (dl: DirectionalLight) 
  (state: PipelineState)
  : Matrix * Matrix =
  // Existing implementation from lines 478-530
  let frustum = BoundingFrustum(state.CurrentCamera.View * state.CurrentCamera.Projection)
  let corners = frustum.GetCorners()
  
  let center = 
    corners |> Array.fold (fun acc v -> acc + v) Vector3.Zero 
    |> fun v -> v / float32 corners.Length
  
  let radius = 
    corners |> Array.maxBy (fun v -> Vector3.Distance(center, v))
    |> fun v -> Vector3.Distance(center, v)
  
  let lightPos = center - dl.Direction * (radius + 100f)
  let lightView = Matrix.CreateLookAt(lightPos, center, Vector3.Up)
  
  // Compute tight bounds in light space
  let mutable minX, minY = infinityf, infinityf
  let mutable maxX, maxY = -infinityf, -infinityf
  
  for corner in corners do
    let lp = Vector3.Transform(corner, lightView)
    minX <- min minX lp.X
    maxX <- max maxX lp.X
    minY <- min minY lp.Y
    maxY <- max maxY lp.Y
  
  let lightProj = Matrix.CreateOrthographicOffCenter(
    minX - 10f, maxX + 10f,
    minY - 10f, maxY + 10f,
    0.1f, (radius + 100f) * 2f
  )
  
  lightView, lightProj

let computeSpotShadowMatrices (sl: SpotLight) : Matrix * Matrix =
  // Perspective projection for spotlight
  let lightView = Matrix.CreateLookAt(sl.Position, sl.Position + sl.Direction, Vector3.Up)
  
  // Calculate FOV from cone angles
  let fov = max sl.InnerConeAngle sl.OuterConeAngle * 2.0f
  let aspect = 1.0f  // Square shadow map
  
  let lightProj = Matrix.CreatePerspectiveFieldOfView(
    fov, aspect, 0.1f, sl.Range
  )
  
  lightView, lightProj

let computePointShadowMatrices (pl: PointLight) : Matrix[] * Matrix =
  // 6 views for cubemap faces (X+, X-, Y+, Y-, Z+, Z-)
  let views = [|
    Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.UnitX, Vector3.UnitY)
    Matrix.CreateLookAt(pl.Position, pl.Position - Vector3.UnitX, Vector3.UnitY)
    Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.UnitY, -Vector3.UnitZ)
    Matrix.CreateLookAt(pl.Position, pl.Position - Vector3.UnitY, Vector3.UnitZ)
    Matrix.CreateLookAt(pl.Position, pl.Position + Vector3.UnitZ, Vector3.UnitY)
    Matrix.CreateLookAt(pl.Position, pl.Position - Vector3.UnitZ, Vector3.UnitY)
  |]
  
  let proj = Matrix.CreatePerspectiveFieldOfView(
    System.Math.PI / 2.0f,  // 90-degree FOV for each face
    1.0f,                    // Square aspect
    0.1f, 
    pl.Range
  )
  
  views, proj
```

---

## Shader API

### Updated PBR.fx

```hlsl
// Shadow Mapping
#define MAX_SHADOWS 256

Texture2DArray ShadowMaps;
sampler ShadowSampler = sampler_state {
    Texture = <ShadowMaps>;
    AddressU = Clamp;
    AddressV = Clamp;
    AddressW = Clamp;  // Important for texture arrays
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

// Matrix arrays for each shadow map slice
float4x4 ShadowViews[MAX_SHADOWS];
float4x4 ShadowProjs[MAX_SHADOWS];

// Light metadata
// x: start shadow index, y: shadow count, z: light index, w: light type
// light type: 0=Directional, 1=Spot, 2=Point
uint4 LightShadowData[256];  // One entry per light

// Total number of shadow map slices
uint ShadowCount;

// Light data (existing)
float3 LightDirections[3];
float3 LightColors[3];
float4 PointLightData[32];
float4 PointLightColors[32];
float PointLightCount = 0;

// Point light data extended for shadows
struct PointLightWithShadow {
    float4 positionRange;  // xyz=position, w=range
    float4 colorIntensity; // rgb=color, a=intensity
    uint shadowStart;     // First shadow slice index
    uint shadowCount;      // Number of shadow slices (0 or 6)
};

// Spot light data (add to shader)
struct SpotLightWithShadow {
    float3 position;
    float range;
    float3 direction;
    float innerConeAngle;
    float outerConeAngle;
    float4 colorIntensity;
    uint shadowStart;
    uint shadowCount;  // 0 or 1
};

// User can define these as needed based on their scene
// For now, we use the existing arrays

// Shadow calculation
float CalculateShadow(float3 worldPos, uint shadowIdx) {
    float4 shadowPos = mul(float4(worldPos, 1.0), ShadowViews[shadowIdx]);
    shadowPos = mul(shadowPos, ShadowProjs[shadowIdx]);
    
    float3 projCoords = shadowPos.xyz / shadowPos.w;
    
    // Transform to [0,1] range
    float2 uv = float2(0.5 * projCoords.x + 0.5, -0.5 * projCoords.y + 0.5);
    float z = projCoords.z;
    
    // Check bounds
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || z < 0.0 || z > 1.0)
        return 1.0;
    
    // Sample from texture array at correct slice
    float depth = ShadowMaps.Sample(ShadowSampler, float3(uv, shadowIdx)).r;
    
    // PCF
    float shadow = 0.0;
    float2 texelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
    float bias = 0.002;
    
    for(int x = -1; x <= 1; ++x) {
        for(int y = -1; y <= 1; ++y) {
            float pcfDepth = ShadowMaps.Sample(
                ShadowSampler, 
                float3(uv + float2(x, y) * texelSize, shadowIdx)
            ).r;
            shadow += (z > pcfDepth + bias) ? 0.1 : 1.0;
        }
    }
    
    return shadow / 9.0;
}

// Point light shadow calculation (samples all 6 faces)
float CalculatePointLightShadow(float3 worldPos, float3 lightPos, uint shadowStart) {
    // Determine which face to sample from
    float3 dir = worldPos - lightPos;
    float3 absDir = abs(dir);
    
    uint faceIdx = 0;
    float maxVal = max(absDir.x, max(absDir.y, absDir.z));
    
    if (maxVal == absDir.x) {
        faceIdx = (dir.x > 0.0) ? 0 : 1;
    } else if (maxVal == absDir.y) {
        faceIdx = (dir.y > 0.0) ? 2 : 3;
    } else {
        faceIdx = (dir.z > 0.0) ? 4 : 5;
    }
    
    uint shadowIdx = shadowStart + faceIdx;
    return CalculateShadow(worldPos, shadowIdx);
}

// Updated pixel shader
float4 MainPS(VertexShaderOutput input) : COLOR0 {
    // ... existing albedo sampling ...
    
    float3 normal = normalize(input.Normal);
    float3 diffuse = AmbientColor;
    
    // Directional Lights
    for(int i = 0; i < 3; i++) {
        float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
        
        // Get shadow data for this light
        uint shadowStart = LightShadowData[i].x;
        uint shadowCount = LightShadowData[i].y;
        
        float shadow = 1.0;
        if (shadowCount > 0 && shadowStart < MAX_SHADOWS) {
            shadow = CalculateShadow(input.WorldPos, shadowStart);
        }
        
        diffuse += ndotl * LightColors[i] * shadow;
    }
    
    // Point Lights
    for(int j = 0; j < 32; j++) {
        if (j >= (int)PointLightCount) break;
        
        float3 lightDir = PointLightData[j].xyz - input.WorldPos;
        float dist = length(lightDir);
        float range = PointLightData[j].w;
        
        if (dist < range) {
            lightDir /= dist;
            float ndotl = max(dot(normal, lightDir), 0.0);
            
            // Get shadow data for point light
            // Need to add shadow metadata to PointLightData or use separate array
            uint shadowStart = /* lookup from shadow data array */;
            uint shadowCount = /* lookup from shadow data array */;
            
            float shadow = 1.0;
            if (shadowCount > 0 && shadowStart < MAX_SHADOWS) {
                float3 lightPos = PointLightData[j].xyz;
                shadow = CalculatePointLightShadow(input.WorldPos, lightPos, shadowStart);
            }
            
            float atten = pow(max(1.0 - (dist / range), 0.0), 2.0);
            diffuse += ndotl * PointLightColors[j].rgb * atten * shadow;
        }
    }
    
    return float4(albedo.rgb * diffuse, albedo.a);
}
```

---

## Binding Updates

### Forward/Forward+ Modes (`renderDrawableWithEffect`)

```fsharp
// Lines 236-251 - Update shadow binding
if state.ShadowMapArray.IsSome then
  let shadowMaps = state.ShadowMapArray.Value
  
  let pShadowMaps = effect.Parameters.["ShadowMaps"]
  if not(isNull pShadowMaps) then
    pShadowMaps.SetValue(shadowMaps)
  
  let pShadowViews = effect.Parameters.["ShadowViews"]
  if not(isNull pShadowViews) then
    let viewArray = state.ShadowViewMatrices.ToArray()
    if viewArray.Length > 0 then
      pShadowViews.SetValue(viewArray)
  
  let pShadowProjs = effect.Parameters.["ShadowProjs"]
  if not(isNull pShadowProjs) then
    let projArray = state.ShadowProjectionMatrices.ToArray()
    if projArray.Length > 0 then
      pShadowProjs.SetValue(projArray)
  
  let pShadowCount = effect.Parameters.["ShadowCount"]
  if not(isNull pShadowCount) then
    pShadowCount.SetValue(float32 state.ShadowViewMatrices.Count)
  
  // Bind light shadow metadata
  let pLightShadowData = effect.Parameters.["LightShadowData"]
  if not(isNull pLightShadowData) then
    // Convert light shadow ranges to uint4 array
    let lightData = buildLightShadowData state
    pLightShadowData.SetValue(lightData)
```

### Deferred Mode (`renderLighting`)

```fsharp
// Lines 1235-1252 - Same binding updates as Forward
// Deferred.renderLighting uses same shadow data structure
```

---

## Implementation Phases

### Phase 1: Infrastructure (Foundation)
1. Update `PipelineState` data structures
2. Implement `ensureShadowMapArray` function
3. Implement `renderToSlice` helper function
4. Implement `recordShadowData` function
5. Add `buildLightShadowData` function

### Phase 2: Shadow Generation (Core)
1. Implement `computeSpotShadowMatrices`
2. Implement `computePointShadowMatrices`
3. Refactor `computeDirectionalShadowMatrices` to extract from existing code
4. Update `renderShadowPass` to handle all light types
5. Remove hardcoded `[0]` bindings

### Phase 3: Shader Updates
1. Update PBR.fx with Texture2DArray support
2. Add `CalculatePointLightShadow` function
3. Update light loops to use shadow metadata
4. Update DeferredLighting.fx similarly

### Phase 4: Testing & Validation
1. Test with single directional light (backward compatibility)
2. Test with multiple directional lights
3. Test with spot lights
4. Test with point lights
5. Test across Forward, Forward+, and Deferred modes
6. Test on both OpenGL and DirectX backends

### Phase 5: Optimization (Optional)
1. CSM (Cascaded Shadow Maps) for directional lights
2. Soft shadows (VSM, ESM, PCSS)
3. Shadow atlas for efficiency
4. Lazy shadow updates

---

## Configuration Options

### ShadowConfig Updates

```fsharp
type ShadowConfig = {
  Resolution: int
  CascadeCount: int  // For directional lights (0 = single shadow)
  PCFSamples: int
  SoftShadows: SoftShadowConfig voption
  MaxShadows: int  // Maximum total shadow map slices
}

module ShadowConfig =
  let defaults: ShadowConfig = {
    Resolution = 1024
    CascadeCount = 0  // 0 = single shadow per directional light
    PCFSamples = 4
    SoftShadows = ValueNone
    MaxShadows = 64
  }
```

### Per-Light Configuration

Lights already have `Shadow: ShadowSettings voption` field for opt-in shadow support.

---

## Backward Compatibility

### Default Behavior
- Existing shaders without Texture2DArray support still work (fallback to single shadow)
- Existing scenes without shadow configuration render without shadows
- Performance penalty only when shadows are enabled

### Migration Path
1. Update existing shaders to use Texture2DArray (or use provided defaults)
2. No changes required to scene setup
3. New features are opt-in via light `Shadow` field

---

## Performance Considerations

### Memory Usage
- **Directional**: 1 × resolution² × bytesPerDepth per light
- **Spot**: 1 × resolution² × bytesPerDepth per light
- **Point**: 6 × resolution² × bytesPerDepth per light

Example with 1024² depth (4 bytes):
- 4 directional lights: 4 × 4MB = 16MB
- 4 spot lights: 4 × 4MB = 16MB
- 2 point lights: 2 × 6 × 4MB = 48MB
- **Total**: 80MB for 10 shadow-casting lights

### CPU Overhead
- Matrix computation per light per frame
- Multiple render passes (one per shadow slice)
- Frustum culling per shadow pass

### GPU Overhead
- One additional pass per shadow slice
- Shadow map sampling during lighting
- PCF sampling adds additional texture lookups

### Optimization Strategies
1. **Lazy Updates**: Only re-render shadows for moving lights/objects
2. **Distance Culling**: Disable shadows for distant lights
3. **LOD Shadows**: Use lower resolution for distant shadows
4. **Shadow Atlas**: Pack multiple small shadows into single texture

---

## API Summary

### User-Facing API (No Changes Required)

```fsharp
// Light configuration already supports shadows
let directionalLight = {
  Direction = Vector3(0, -1, 0)
  Color = Color.White
  Intensity = 1.0f
  Shadow = ValueSome ShadowSettings.defaults  // Enable shadows
  CascadeCount = 0
  CascadeSplits = [||]
}

let spotLight = {
  Position = Vector3(0, 10, 0)
  Direction = Vector3(0, -1, 0)
  Color = Color.White
  Intensity = 1.0f
  Range = 50.0f
  InnerConeAngle = 0.3f
  OuterConeAngle = 0.5f
  Shadow = ValueSome ShadowSettings.defaults  // Enable shadows
}

let pointLight = {
  Position = Vector3(0, 5, 0)
  Color = Color.White
  Intensity = 1.0f
  Range = 20.0f
  Shadow = ValueSome ShadowSettings.defaults  // Enable shadows
}

let lighting = {
  AmbientColor = Color(50, 50, 55)
  AmbientIntensity = 1.0f
  Lights = [|
    Light.Directional directionalLight
    Light.Spot spotLight
    Light.Point pointLight
  |]
}
```

### Shader API

User shaders receive:
- `ShadowMaps` - Texture2DArray of all shadow maps
- `ShadowViews[]` - View matrices for each slice
- `ShadowProjs[]` - Projection matrices for each slice
- `LightShadowData[]` - Per-light shadow metadata
- `ShadowCount` - Total number of shadow map slices

Shaders can:
- Loop through all lights and apply shadows where available
- Skip shadows for lights without shadow data
- Customize shadow sampling behavior
- Implement advanced shadow techniques

---

## Open Questions

1. **Point Light Cubemaps**: Should we use MonoGame's `CubeMapTexture` or 6 slices in 2D array?
   - Recommendation: 2D array slices (simpler, more uniform)

2. **Maximum Shadows**: What's a reasonable default for `MaxShadows`?
   - Recommendation: 64 (enough for most scenes, configurable)

3. **Shadow Resolution**: Should all shadows use same resolution, or per-light?
   - Recommendation: Per-light via light configuration (future enhancement)

4. **CSM Support**: Should cascaded shadows be Phase 1 or Phase 5?
   - Recommendation: Phase 5 (optimize after basic multi-shadow works)

5. **Soft Shadows**: Implement now or defer to Phase 5?
   - Recommendation: Defer (get basic shadows working first)

---

## References

- MonoGame Texture2DArray support: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2DArray.html
- CSM: Cascaded Shadow Maps
- VSM: Variance Shadow Maps
- ESM: Exponential Shadow Maps
- PCSS: Percentage-Closer Soft Shadows
