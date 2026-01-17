# Deferred Rendering Directional Lights Debug Report

## Executive Summary

**Problem**: Directional lights work in Forward/Forward+ modes but fail in Deferred mode, despite identical shader code and confirmed F# parameter binding.

**Working Solution**: Using individual scalar parameters (`LightDirection`, `LightColor`) instead of array parameters (`LightDirections[3]`, `LightColors[3]`).

**Root Cause**: Unknown. This appears to be a MonoGame or HLSL constant buffer issue specific to `float3[]` array parameters in fullscreen quad shaders.

---

## Current Status

| Mode     | Directional Lights | Point Lights | Shadows  |
| -------- | ------------------ | ------------ | -------- |
| Forward  | ✅ Works           | ✅ Works     | ✅ Works |
| Forward+ | ✅ Works           | ✅ Works     | ✅ Works |
| Deferred | ❌ **Fails**       | ✅ Works     | ✅ Works |

**Critical observation**: Point lights use `float4[]` arrays and work. Directional lights use `float3[]` arrays and fail.

---

## Working Solution

The only approach that works is using **individual parameters** instead of arrays:

### Shader (DeferredLighting.fx)

```hlsl
// Instead of arrays:
// float3 LightDirections[3];
// float3 LightColors[3];

// Use individual parameters:
float3 LightDirection;  // Primary directional light
float3 LightColor;

// In pixel shader:
float ndotl = max(dot(normal, -normalize(LightDirection)), 0.0);
diffuse += ndotl * LightColor * shadow;
```

### F# Binding (Pipeline.fs)

```fsharp
let firstDirectional =
  lighting.Lights
  |> Array.tryPick (function
    | Directional dl -> Some dl
    | _ -> None)

match firstDirectional with
| Some dl ->
  let pDir = effect.Parameters.["LightDirection"]
  if not(isNull pDir) then pDir.SetValue(dl.Direction)

  let pCol = effect.Parameters.["LightColor"]
  if not(isNull pCol) then pCol.SetValue(dl.Color.ToVector3() * dl.Intensity)
| None -> ()
```

**Limitation**: This approach only supports 1 directional light. For multiple directional lights, a different approach is needed.

---

## What We Tried (All Failed)

### 1. Array SetValue with Vector3[]

```fsharp
let dirs = [| light.Direction |]
effect.Parameters.["LightDirections"].SetValue(dirs)
```

**Result**: Values stored correctly (verified by `GetValueVector3Array()`), but shader reads zeros.

### 2. Array SetValue with Vector4[] (to match point lights)

```fsharp
let dirs = [| Vector4(light.Direction, 0f) |]
effect.Parameters.["LightDirections"].SetValue(dirs)
```

**Shader**: Changed to `float4 LightDirections[3]`
**Result**: Still fails.

### 3. Individual Element Access via Elements[]

```fsharp
effect.Parameters.["LightDirections"].Elements.[0].SetValue(dirs.[0])
```

**Result**: SetValue succeeds, but shader reads zeros.

### 4. Indexed Name Access "LightDirections[0]"

```fsharp
effect.Parameters.["LightDirections[0]"].SetValue(dirs.[0])
```

**Result**: Parameter is NULL - this naming convention doesn't work in MonoGame.

### 5. Padded Arrays (exactly 3 elements)

```fsharp
let lightDirs = Array.init 3 (fun i -> if i < raw.Length then raw.[i] else Vector3.Zero)
effect.Parameters.["LightDirections"].SetValue(lightDirs)
```

**Result**: Still fails.

### 6. Inline Binding (same location as working point lights)

Moved directional light binding to be inline with point light binding in `renderLighting`.
**Result**: Still fails.

### 7. ReadBack Verification

```fsharp
pDirs.SetValue(lightDirs)
let readBack = pDirs.GetValueVector3Array()
// Output: Set: {-0.19, -0.96, -0.19}, ReadBack: {-0.19, -0.96, -0.19}
```

**Result**: ReadBack confirms values ARE stored in the effect parameter. The issue is between EffectParameter storage and HLSL shader execution.

### 8. Dynamic Loop with Count Variable + Float4 Arrays (Latest Attempt)

We hypothesized that the compiler was unrolling the static loop `for(int i=0; i<3; i++)`, breaking the array binding mapping.
**Action**:

1.  Changed `LightDirections` and `LightColors` to `float4[]` (matching Point Lights).
2.  Added `float DirectionalLightCount`.
3.  Changed loop to `for(int i=0; i<3; i++) { if (i >= DirectionalLightCount) break; ... }`.
    **Result**: ❌ Fails. Still renders black (only point lights visible). This suggests loop unrolling is likely not the primary culprit, or the issue is deeper in the constant buffer mapping for this specific shader profile (`ps_4_0_level_9_1` / `ps_3_0`).

---

## Key Observations

### Point Lights Work (Same Effect, Same Shader)

```fsharp
// F# Side - WORKS
let pData = [| Vector4(pos.X, pos.Y, pos.Z, range) |]  // Vector4[]
effect.Parameters.["PointLightData"].SetValue(pData)
```

```hlsl
// Shader Side - WORKS
float4 PointLightData[32];
float4 PointLightColors[32];
```

### Directional Lights Fail (Same Effect, Same Shader)

```fsharp
// F# Side - FAILS
let dirs = [| direction |]  // Vector3[]
effect.Parameters.["LightDirections"].SetValue(dirs)
```

```hlsl
// Shader Side - FAILS (reads zeros despite correct SetValue)
float3 LightDirections[3];
float3 LightColors[3];
```

### Identical Code in Forward Works

The exact same `float3 LightDirections[3]` and `SetValue(Vector3[])` code works in Forward mode (PBR.fx).

---

## Hypotheses

### 1. HLSL Constant Buffer Packing for float3[]

In HLSL, `float3` arrays are padded to 16-byte boundaries per element. This creates hidden padding:

```
float3[0] = 12 bytes + 4 padding
float3[1] = 12 bytes + 4 padding
float3[2] = 12 bytes + 4 padding
```

MonoGame's `SetValue(Vector3[])` may not account for this padding correctly in all shader contexts. `Vector4[]` naturally aligns to 16 bytes and works.

### 2. Per-Draw vs Once-Per-Frame Binding

- **Forward**: Parameters are set per-drawable, effect is applied, geometry is drawn.
- **Deferred**: Parameters are set once, then a fullscreen quad is drawn.

The difference in binding frequency might affect how constant buffers are updated.

### 3. Effect Technique/Pass Differences

Forward uses standard geometry rendering with vertex/pixel shaders operating on vertices.
Deferred uses a fullscreen quad where the vertex shader is minimal.

The constant buffer layout might differ between these shader configurations.

### 4. MonoGame Bug for float3[] in Post-Process Shaders

Given that:

- `Vector3[]` → `float3[]` fails in deferred
- `Vector4[]` → `float4[]` works in deferred (point lights)
- `Vector3[]` → `float3[]` works in forward

There may be a MonoGame bug specifically with `float3[]` arrays in shaders that use fullscreen quad techniques.

---

## Complete Code Files

### DeferredLighting.fx (Full Shader)

```hlsl
#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// G-Buffer textures
texture AlbedoMap;
sampler AlbedoSampler = sampler_state { Texture = <AlbedoMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture NormalMap;
sampler NormalSampler = sampler_state { Texture = <NormalMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

texture WorldPosMap;
sampler WorldPosSampler = sampler_state { Texture = <WorldPosMap>; MagFilter = Point; MinFilter = Point; AddressU = Clamp; AddressV = Clamp; };

// Lighting
float3 AmbientColor;
// Directional lights
float3 LightDirections[3];
float3 LightColors[3];

// Point Lights
float4 PointLightData[32];   // xyz = position, w = range
float4 PointLightColors[32]; // rgb = color * intensity
float PointLightCount = 0;

// Shadow Mapping
texture ShadowMap;
sampler ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

matrix LightView;
matrix LightProjection;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output;
	output.Position = input.Position;
	output.TexCoord = input.TexCoord;
	return output;
}

float CalculateShadow(float4 shadowCoord)
{
    float3 projCoords = shadowCoord.xyz / shadowCoord.w;
    float2 uv = float2(0.5 * projCoords.x + 0.5, -0.5 * projCoords.y + 0.5);
    float z = projCoords.z;

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 || z < 0.0 || z > 1.0)
        return 1.0;

    float shadow = 0.0;
    float2 texelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
    float bias = 0.002;  // Lower shadow bias

    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = tex2Dlod(ShadowSampler, float4(uv + float2(x, y) * texelSize, 0, 0)).r;
            shadow += (z > pcfDepth + bias) ? 0.1 : 1.0;
        }
    }
    return shadow / 9.0;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    // 1. Sample G-Buffer
	float4 albedo = tex2D(AlbedoSampler, input.TexCoord);

    // Discard if no geometry (Albedo Alpha will be 0 from clear)
    if (albedo.a < 0.001) discard;

	float3 normal = normalize(tex2D(NormalSampler, input.TexCoord).rgb);
    float3 worldPos = tex2D(WorldPosSampler, input.TexCoord).rgb;

    // 2. Calculate shadow coordinates from world position (no normal offset to match Forward mode)
    float4 shadowPos = mul(float4(worldPos, 1.0), LightView);
    shadowPos = mul(shadowPos, LightProjection);
    float shadow = CalculateShadow(shadowPos);

    // DEBUG: Visualize shadow value - WHITE = lit (1.0), BLACK = shadowed (0.0)
    // return float4(shadow, shadow, shadow, 1.0);

    float3 diffuse = AmbientColor;

    // Directional lights - THIS LOOP READS ZEROS FROM LightDirections/LightColors
    for(int i = 0; i < 3; i++)
    {
        float ndotl = max(dot(normal, -normalize(LightDirections[i])), 0.0);
        float atten = (i == 0) ? shadow : 1.0;
        diffuse += ndotl * LightColors[i] * atten;
    }

    // 3. Point Lights - THIS LOOP WORKS CORRECTLY
    for(int j = 0; j < 32; j++)
    {
        if (j >= (int)PointLightCount) break;

        float3 lightDir = PointLightData[j].xyz - worldPos;
        float dist = length(lightDir);
        float range = PointLightData[j].w;

        if (dist < range)
        {
            lightDir /= dist;
            float ndotl = max(dot(normal, lightDir), 0.0);
            float atten = pow(max(1.0 - (dist / range), 0.0), 2.0);
            diffuse += ndotl * PointLightColors[j].rgb * atten;
        }
    }

	return float4(albedo.rgb * diffuse, albedo.a);
}

technique DeferredLighting
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
```

### Pipeline.fs - Deferred.renderLighting (F# Code)

```fsharp
let renderLighting
  (state: PipelineState)
  (gBuffer: (RenderTarget2D * RenderTarget2D * RenderTarget2D) voption)
  =

  match gBuffer with
  | ValueSome(albedo, normal, depth) ->
    let hasDeferredShader =
      state.CustomShaders.ContainsKey(ShaderBase.DeferredLighting)

    match state.CustomShaders.TryGetValue(ShaderBase.DeferredLighting) with
    | true, effect ->

      // Bind G-Buffer textures
      if not(isNull effect.Parameters.["AlbedoMap"]) then
        effect.Parameters.["AlbedoMap"].SetValue(albedo)

      if not(isNull effect.Parameters.["NormalMap"]) then
        effect.Parameters.["NormalMap"].SetValue(normal)

      if not(isNull effect.Parameters.["WorldPosMap"]) then
        effect.Parameters.["WorldPosMap"].SetValue(depth)

      // Bind Point Light Data - THIS WORKS
      let pointLightData =
        state.CurrentLighting.Lights
        |> Array.choose (function
          | Point pl ->
            Some(
              Vector4(pl.Position.X, pl.Position.Y, pl.Position.Z, pl.Range)
            )
          | _ -> None)

      let pointLightColors =
        state.CurrentLighting.Lights
        |> Array.choose (function
          | Point pl ->
            Some(Vector4(pl.Color.ToVector3() * pl.Intensity, 1.0f))
          | _ -> None)

      if pointLightData.Length > 0 then
        let pParams = effect.Parameters.["PointLightData"]
        if not(isNull pParams) then
          pParams.SetValue(pointLightData)

        let pColors = effect.Parameters.["PointLightColors"]
        if not(isNull pColors) then
          pColors.SetValue(pointLightColors)

        let pCount = effect.Parameters.["PointLightCount"]
        if not(isNull pCount) then
          pCount.SetValue(float32 pointLightData.Length)
      else
        let pCount = effect.Parameters.["PointLightCount"]
        if not(isNull pCount) then
          pCount.SetValue(0.0f)

      // Bind Directional Light Data - THIS FAILS (shader reads zeros)
      let lightDirsRaw =
        state.CurrentLighting.Lights
        |> Array.choose (function
          | Directional dl -> Some dl.Direction
          | _ -> None)

      let lightColorsRaw =
        state.CurrentLighting.Lights
        |> Array.choose (function
          | Directional dl -> Some(dl.Color.ToVector3() * dl.Intensity)
          | _ -> None)

      // Pad to exactly 3 elements
      let lightDirs =
        Array.init 3 (fun i ->
          if i < lightDirsRaw.Length then
            lightDirsRaw.[i]
          else
            Vector3.Zero)

      let lightColors =
        Array.init 3 (fun i ->
          if i < lightColorsRaw.Length then
            lightColorsRaw.[i]
          else
            Vector3.Zero)

      if lightDirs.Length > 0 then
        let pDirs = effect.Parameters.["LightDirections"]
        if not(isNull pDirs) then
          pDirs.SetValue(lightDirs)
          // Verify by reading back
          let readBack = pDirs.GetValueVector3Array()
          let rbStr =
            if readBack.Length > 0 then
              readBack.[0].ToString()
            else
              "empty"
          printfn $"[Deferred] Set LightDirections: {lightDirs.[0]}, ReadBack: {rbStr}"
        else
          printfn "[Deferred] LightDirections is NULL"

        let pCols = effect.Parameters.["LightColors"]
        if not(isNull pCols) then
          pCols.SetValue(lightColors)
          let readBack = pCols.GetValueVector3Array()
          let rbStr =
            if readBack.Length > 0 then
              readBack.[0].ToString()
            else
              "empty"
          printfn $"[Deferred] Set LightColors: {lightColors.[0]}, ReadBack: {rbStr}"

      // Bind Ambient Color
      let pAmbient = effect.Parameters.["AmbientColor"]
      if not(isNull pAmbient) then
        pAmbient.SetValue(
          state.CurrentLighting.AmbientColor.ToVector3()
          * state.CurrentLighting.AmbientIntensity
        )

      // Bind Shadow Data
      if state.ShadowMaps.Count > 0 && state.ShadowViewMatrices.Count > 0 then
        let pShadowMap = effect.Parameters.["ShadowMap"]
        if not(isNull pShadowMap) then
          pShadowMap.SetValue(state.ShadowMaps.[0])

        let pLightView = effect.Parameters.["LightView"]
        if not(isNull pLightView) then
          pLightView.SetValue(state.ShadowViewMatrices.[0])

        let pLightProj = effect.Parameters.["LightProjection"]
        if not(isNull pLightProj) then
          pLightProj.SetValue(state.ShadowProjectionMatrices.[0])

      state.Device.DepthStencilState <- DepthStencilState.None
      Shared.renderFullScreenQuad state.Device effect
      state.Device.DepthStencilState <- DepthStencilState.Default
    | false, _ ->
      // Fallback: blit albedo to current target
      // ...
  | ValueNone -> ()
```

---

## Console Output (Proof F# Side Works)

Every frame, the console shows:

```
[Deferred] Set LightDirections: {X:-0.19245009 Y:-0.9622505 Z:-0.19245009}, ReadBack: {X:-0.19245009 Y:-0.9622505 Z:-0.19245009}
[Deferred] Set LightColors: {X:0.8 Y:0.8 Z:0.8}, ReadBack: {X:0.8 Y:0.8 Z:0.8}
```

This proves:

- `SetValue` is called with correct values
- `GetValueVector3Array` returns the same values (stored correctly)
- The issue is between Effect parameter storage and HLSL shader execution

---

## To Reproduce

```bash
cd c:\Users\scyth\repos\Mibo
dotnet run --project samples/PipelineSample
```

1. Press **1** for Forward mode - Observe bright cyan surfaces with shadow ✅
2. Press **2** for Forward+ mode - Same as Forward ✅
3. Press **3** for Deferred mode - Dark surfaces, only point lights visible ❌

---

## Recommended Next Steps

1. **Try StructuredBuffer instead of constant buffer arrays** - This uses a different memory layout.

2. **Investigate MonoGame source code** for how `EffectParameter.SetValue(Vector3[])` maps to D3D constant buffers.

3. **Test with explicit cbuffer layout in HLSL**:

   ```hlsl
   cbuffer LightingData : register(b0)
   {
       float4 LightDirections[3];  // Force float4 alignment
       float4 LightColors[3];
   }
   ```

4. **File a MonoGame issue** with minimal reproduction case.

5. **Use the working individual parameter solution** as a temporary fix while investigating.

---

## Update: Resolution and Cross-Platform Strategy

**Resolution (DirectX)**:
Confirmed that `float3[]` arrays work correctly for multiple directional lights on DirectX. The previous failure in Deferred mode was resolved by harmonizing the shader structure with the working Forward implementation.

**Cross-Platform Strategy (OpenGL & DirectX)**:
To ensure robust cross-platform support (specifically for OpenGL where `float3[]` packing in fullscreen quads can be problematic), we can implement a "Both Bindings" fallback in `Pipeline.fs`:

1.  Check for `LightDirections` (array parameter).
2.  If found, use `SetValue(vector3Array)`.
3.  If NOT found, check for `LightDirection0` (scalar parameter).
4.  If found, use `SetValue(vector3)` for the first light only.

This allows the _shader definition_ to dictate the binding mode, ensuring the F# code remains platform-agnostic while accommodating specific shader compiler quirks.
