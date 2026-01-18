---
title: Render Pipeline Architecture & Usage
category: Rendering
index: 15
---

# Render Pipeline Architecture & Usage

Mibo 3D uses a modern, data-oriented rendering pipeline designed for flexibility and performance. This guide covers both the high-level architecture and the practical "Complexity Ladder" for using it in your game.

---

## Part 1: Complexity Ladder

Mibo's 3D rendering is designed to scale with your needs. You can start with simple unlit shapes and climb all the way to high-fidelity PBR with shadow casting, without rewriting your game loop.

### Level 1: Basic Rendering

At the simplest level, you just want to get a mesh on the screen.

```fsharp
open Mibo.Rendering.Graphics3D

// In your view function
let view (ctx: GameContext) (state: State) (buffer: RenderBuffer<unit, RenderCommand>) =

    // 1. Define a camera
    let camera = Camera.perspective ...

    // 2. Submit commands to the buffer
    buffer
    |> RenderBuilder.camera camera
    |> RenderBuilder.clear Color.CornflowerBlue
    |> RenderBuilder.draw (
        draw {
            mesh state.MyMesh
            at Vector3(0f, 0f, 0f)
            scaledBy 2.0f
        }
    )
    |> RenderBuilder.submit
```

#### The `draw` Builder

The `draw` computation expression is how you compose a generic `Mesh` into a specific instance in the world.

```fsharp
draw {
    mesh myMesh
    at 10f 0f 5f                        // Position (x, y, z)
    rotatedByYawPitchRoll 0f 1.5f 0f     // Rotation (yaw, pitch, roll in radians)
    scaledBy 1.5f                         // Uniform scale
    withAlbedo Color.Red                     // Basic tint color
}
```

**Result:** Returns a `Drawable voption` which can be submitted to the render buffer. The `voption` allows the builder to fail gracefully (e.g., no mesh set), and `RenderBuilder.draw` automatically handles `ValueNone` by skipping it.

### Level 2: Materials & Textures

Mibo uses a PBR (Physically Based Rendering) material model by default. You can configure materials inline within the `draw` builder.

```fsharp
draw {
    mesh sphereMesh
    at 0f 2f 0f

    // Bind textures
    withAlbedoMap myTexture
    withNormalMap myNormalMap

    // Tune material properties
    withMetallic 0.8f    // 0.0 (Dielectric) to 1.0 (Metal)
    withRoughness 0.2f   // 0.0 (Smooth) to 1.0 (Rough)

    // Rendering flags
    withFlags (MaterialFlags.DoubleSided ||| MaterialFlags.Transparent)
}
```

**Reuse Materials:** Define reusable materials as values and apply them with `withMaterial`.

```fsharp
// Define a 'Gold' material template
let goldMaterial =
    Material.defaultOpaque
    |> Material.withAlbedo Color.Gold
    |> Material.withMetallic 1.0f
    |> Material.withRoughness 0.1f

// Use it later
draw {
    mesh coinMesh
    withMaterial goldMaterial  // Apply the pre-configured material
    at 5f 0f 0f
}
```

### Level 3: Scene Organization

As your scene grows, the `RenderBuilder` fluent DSL organizes frame rendering cleanly.

```fsharp
// Global State
buffer
|> RenderBuilder.clear Color.Black
|> RenderBuilder.clearDepth
|> RenderBuilder.camera mainCamera
|> RenderBuilder.lighting sceneLighting

// Draw all enemies
|> RenderBuilder.drawMany (
    [|
        for enemy in enemies do
            draw {
                mesh enemyMesh
                at enemy.Position
                rotatedByYawPitchRoll enemy.Rotation.Yaw 0f 0f
            }
    |]
)

// Draw player
|> RenderBuilder.draw (
    draw {
        mesh playerMesh
        at player.Pos
    }
)
|> RenderBuilder.submit
```

> **Command Ordering Matters**
>
> RenderBuilder commands accumulate in a buffer and execute at end of frame. The order you specify them determines the rendering sequence:
>
> 1. **Clear commands first** - `clear`/`clearDepth` wipes the previous frame
> 2. **Set camera before drawing** - Without it, uses default identity camera (nothing visible)
> 3. **Set lighting** - Updates light state (no flush triggered)
> 4. **Draw commands** - Batches geometry (renders when frame ends)
>
> "Your screen is empty or showing wrong camera? Check that `RenderBuilder.camera` is called before your draw commands."
>
> "Your mesh drawing overwrites everything? Make sure `clear` comes before your draws, not after."

### Level 4: Lighting

To light your scene, you construct a `LightingState` and pass it to the renderer. Lights work independently of shadows.

#### Define Lights

Use the `Light` union to define Directional, Point, or Spot lights.

```fsharp
let sun =
    Light.Directional {
        Direction = Vector3.Normalize(Vector3(-1f, -2f, -1f))
        Color = Color.LightYellow
        Intensity = 2.0f
        Shadow = ValueNone  // No shadows initially
        CascadeCount = 0
        CascadeSplits = [||]
        SourceRadius = 0.05f
    }

let lamp =
    Light.Spot {
        Position = Vector3(0f, 5f, 0f)
        Direction = Vector3.Down
        Color = Color.Orange
        Intensity = 5.0f
        Range = 20.0f
        InnerConeAngle = MathHelper.ToRadians(30f)
        OuterConeAngle = MathHelper.ToRadians(45f)
        Shadow = ValueNone  // No shadows initially
        SourceRadius = 0.1f
    }
```

#### Apply to Scene

```fsharp
let lighting = {
    Lighting.ambient with
        Lights = [| sun; lamp |]
        AmbientIntensity = 0.2f
}

buffer
 |> RenderBuilder.lighting lighting
// ... draws ...
```

#### Lighting Scope

You can define lighting in two ways:

**Global Lighting (Program Setup):** Set lighting once in your program configuration. Used as fallback if you don't call `RenderBuilder.lighting` in your view function.

```fsharp
// In your Program.fs
Program.mkProgram init update
 |> Program.withPipeline (
    PipelineConfig.defaults
    |> PipelineConfig.withDefaultLighting sceneLighting
)

// In your view function - lighting is already applied
buffer
 |> RenderBuilder.drawMany drawables
```

**Per-Frame Lighting (View Function):** Override global lighting or set it dynamically in your view function. Changes every frame.

```fsharp
let lighting = {
    Lighting.ambient with
        Lights = [| sun; lamp |]
        AmbientIntensity = 0.2f
}

buffer
 |> RenderBuilder.lighting lighting
// ... draws ...
```

**Per-Drawable Lighting:** Use `withEffect` with custom shaders that implement their own lighting. Useful for emissive objects, force-lit items, or debug visualizations.

```fsharp
// Emissive object with no lighting
let emissiveEffect = Assets.effect "Effects/Unlit" ctx

draw {
    mesh glowMesh
    at 0f 2f 0f
    withEffect emissiveEffect  // Bypasses scene lighting
}
```

**Priority:** Global lighting (via `PipelineConfig`) → Per-frame lighting (via `RenderBuilder.lighting`) → Per-drawable lighting (via `withEffect`). Later settings override earlier ones.

### Level 5: Shadows

Shadows require three things to work together: lights configured with shadow settings, materials that cast shadows, and the pipeline configured to render shadows.

#### 1. Enable Shadows on Lights

```fsharp
let sun =
    Light.Directional {
        Direction = Vector3.Normalize(Vector3(-1f, -2f, -1f))
        Color = Color.LightYellow
        Intensity = 2.0f
        Shadow = ValueSome ShadowSettings.defaults  // Enable shadows
        CascadeCount = 3
        CascadeSplits = [| 0.1f; 0.3f; 1.0f |]
        SourceRadius = 0.05f
    }

let lamp =
    Light.Spot {
        Position = Vector3(0f, 5f, 0f)
        Direction = Vector3.Down
        Color = Color.Orange
        Intensity = 5.0f
        Range = 20.0f
        InnerConeAngle = MathHelper.ToRadians(30f)
        OuterConeAngle = MathHelper.ToRadians(45f)
        Shadow = ValueSome ShadowSettings.defaults  // Enable shadows
        SourceRadius = 0.1f
    }
```

#### 2. Ensure Materials Cast Shadows

Opaque materials cast shadows by default. Transparent materials do not.

```fsharp
let opaqueMaterial = Material.defaultOpaque  // Has CastsShadow flag
let transparentMaterial = Material.transparent   // Does NOT cast shadow
```

#### 3. Configure Pipeline for Shadows

```fsharp
// In your Program.fs
Program.mkProgram init update
|> Program.withPipeline (
    PipelineConfig.defaults
    |> PipelineConfig.withShadows (
        ShadowConfig.defaults
        // High quality (4096px per shadow), soft edges
        |> ShadowConfig.withResolution 4096
        |> ShadowConfig.withCascades 3
        |> ShadowConfig.withSoftShadows 1.0f
        // Ensure Atlas is large enough for all lights (Resolution * Tiles)
        |> ShadowConfig.withAtlasTiles 8
        |> ShadowConfig.withMaxAtlasSize 16384
    )
)
```

**Shadow Requirements Summary:**
| Component | Requirement |
|-----------|-------------|
| Light | `Shadow = ValueSome ShadowSettings.defaults` |
| Material | `CastsShadow` flag (default for opaque) |
| Pipeline | `PipelineConfig.withShadows` configured |

#### Required Shaders for Shadows

> **Mibo does not include any shaders.** You must provide your own shader files (`.fx` or `.mgfxb`) in your project and load them via `PipelineConfig.withShader` or content system.

To render shadows, your shader must implement a shadow casting pass. This is a simple depth-only shader:

```hlsl
// Vertex Shader: Output depth for shadow map
VS_OUTPUT ShadowVS(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = mul(input.Position, WorldViewProjectionMatrix);
    output.Depth = output.Position.z;  // Store depth for shadow comparison
    return output;
}

// Pixel Shader: Output depth to shadow atlas
float4 ShadowPS(VS_OUTPUT input) : COLOR0
{
    return input.Depth;  // Output raw depth value (R = depth)
}
```

The pipeline automatically:

- Binds view and projection matrices for each light
- Clears the shadow atlas to white (far depth)
- Sets viewport to correct atlas tile
- Culls geometry to light frustums

Your shader just renders depth to the atlas. No lighting, no textures - just depth.

> **"My meshes use their built-in effects and I want to keep them. If `ShaderBase.PBRForward` is not provided, the pipeline falls back to `mesh.Effect` (the effect your .xnb model was compiled with). No need to write a custom PBR shader - just provide your shadow caster."**

### Level 6: Custom Effects & Escape Hatches

Sometimes PBR isn't what you need. You might want a custom Toon shader, a special VFX shader, or debug lines.

#### Override Per-Drawable Shader

```fsharp
let toonEffect = Assets.effect "Effects/Toon" ctx

draw {
    mesh myMesh
    at 0f 0f 0f
    withEffect toonEffect  // Uses your custom shader instead of PBR
}
```

#### Raw Graphics Device Access

If you need to draw primitives, change render states manually, or do something completely custom:

```fsharp
buffer |> RenderBuilder.custom (fun device camera ->
    // You have full access to the GraphicsDevice here
    device.BlendState <- BlendState.Additive
    device.DrawUserPrimitives(...)
)
```

#### Custom Light-Receiving Shaders

If you write a custom shader but still want it to participate in Mibo's lighting/shadow system, you must declare the pipeline's texture bindings.

```hlsl
// Mibo Pipeline Bindings
texture LightDataTexture;
sampler LightDataSampler = sampler_state { Texture = <LightDataTexture>; ... };

texture ShadowMatrixTexture;
sampler ShadowMatrixSampler = sampler_state { Texture = <ShadowMatrixTexture>; ... };

texture ShadowAtlas;
sampler ShadowAtlasSampler = sampler_state { Texture = <ShadowAtlas>; ... };

float LightCount;
float ShadowAtlasSize;
float ShadowAtlasTilesX;

// In your Pixel Shader:
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float3 diffuse = AmbientColor;

    // Iterate lights (see "Shader Contract" below for data layout)
    for(int i = 0; i < (int)LightCount; i++) {
        // 1. Fetch Light Data (Pos, Dir, Color, ShadowIndex) from LightDataTexture
        // 2. Calculate NdotL
        // 3. If ShadowIndex >= 0, sample ShadowAtlas using ShadowMatrixTexture
        // 4. Accumulate lighting
    }

    return float4(diffuse, 1.0);
}
```

---

## Part 2: Architecture & Configuration

### Core Concepts

**RenderBuffer:** A command queue where you submit rendering instructions. Commands are processed sequentially by the pipeline each frame.

**RenderCommand:** The types of commands you can submit:

- `SetCamera` - Sets the active camera for subsequent draws
- `SetLighting` - Sets the scene lighting configuration
- `SetViewport` - Changes the rendering viewport (split-screen, minimaps)
- `ClearTarget` - Clears color and/or depth buffers
- `Draw` - Draws a single drawable object
- `DrawCustom` - Executes a custom callback with GraphicsDevice access

**Drawable:** A complete renderable object combining mesh, transform, material, and bounding sphere. Created by the `draw {}` builder.

**View Module:** Provides two building blocks:

- `draw {}` - Creates individual drawables (Mesh + Transform + Material)
- `RenderBuilder` - Fluent DSL for submitting commands to RenderBuffer

### Complexity Ladder Overview

```
┌─────────────────────────────────────────────┐
│ Level 1: Basic Rendering                    │
│   • Mesh on screen                          │
│   • Transforms (position, rotation, scale)  │
│   • Clear color buffer                      │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 2: Materials & Textures               │
│   • PBR material system                     │
│   • Albedo, Normal, Metallic, Roughness maps│
│   • Material reuse                          │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 3: Scene Organization                 │
│   • RenderBuilder fluent DSL                │
│   • Batch rendering (drawMany)              │
│   • Command sequencing                      │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 4: Lighting                           │
│   • Directional, Point, Spot lights         │
│   • Ambient lighting                        │
│   • Multiple lights per scene               │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 5: Shadows                            │
│   • Shadow atlas                            │
│   • Cascaded directional shadows            │
│   • Shadow caster shaders                   │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 6: Custom Effects                     │
│   • Custom shader overrides                 │
│   • Raw GraphicsDevice access               │
│   • Custom light-receiving shaders          │
└─────────────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────┐
│ Level 7: Advanced Customization             │
│   • PreRender callbacks                     │
│   • Custom Lighting Binders                 │
│   • Performance tuning (TileSize)           │
└─────────────────────────────────────────────┘
```

### Level 7: Advanced Customization

For complex engines, you may need to hook into the pipeline execution or radically change how data is fed to shaders.

#### PreRender Callback

Executed **before** the main render pass but **after** command processing. Use this to update global effect parameters, dispatch compute shaders, or perform custom setup that depends on the current camera/lighting state.

```fsharp
Program.withPipeline (
    PipelineConfig.defaults
    |> PipelineConfig.withPreRenderCallback (fun device camera lighting ->
        // e.g. Update a global "Time" uniform on all effects
        // or dispatch a Compute Shader for particle updates
        ()
    )
)
```

#### Lighting Binder Override

If you strictly use your own shaders and hate the Mibo "Texture Buffer" approach, you can override how lighting data is applied to your effects.

```fsharp
Program.withPipeline (
    PipelineConfig.defaults
    |> PipelineConfig.withLightingBinder (fun effect camera lighting ->
        // Manual binding - completely replaces Mibo's default parameter setting
        effect.Parameters.["MyLightPos"].SetValue(lighting.Lights.[0].Position)
        effect.Parameters.["MyLightColor"].SetValue(lighting.Lights.[0].Color.ToVector3())
    )
)
```

> **Warning:** Providing a binder completely disables the automatic `LightDataTexture` binding. You are on your own!

### Command Flow

```
┌──────────────┐      ┌─────────────┐      ┌──────────────┐      ┌──────────────┐
│   draw { }   │      │RenderBuilder│      │RenderBuffer  │      │  Pipeline    │
│  (Create     │─────▶│   (Submit   │─────▶│  (Command    │─────▶│  (Execute    │
│  Drawable)   │      │  Commands)  │      │   Queue)     │      │  Commands)   │
└──────────────┘      └─────────────┘      └──────────────┘      └──────────────┘
       │                                         │                   │
       │ Returns                                 │ Stores            │ Processes
       │ Drawable voption                        │ RenderCommand[]   │ - Culling
       │                                         │                   │ - Shadow pass
       └──▶ Skip if ValueNone ───────────────────┘                   │ - Batching
                                                                     │ - Sort opaque/transparent
                                                                     │ - Main pass
                                                                     │ - Post-process
                                                                     ▼
                                                              ┌──────────────┐
                                                              │   Screen     │
                                                              └──────────────┘
```

### Single-Pass Forward Rendering

The default pipeline uses a **Forward Rendering** approach. All active lights are processed in a single shader pass. To support many lights efficiently, we use **Texture Buffers** to feed light data to the GPU, avoiding the instruction limit of classic uniform arrays.

### CPU Tiled Forward Culling

To optimize performance, the CPU calculates a screen-space grid (tiles) and determines which lights intersect each tile.

> **Note:** While the culling logic exists and computes masks, the default PBR shader currently iterates all active lights for simplicity. Implementing a tile-based shader is an optional optimization for scenes with hundreds of lights.

#### Configuration

You can tune the granularity of the culling grid via `PipelineConfig.withTileSize`:

- **16 (Pixels):** Tighter culling (fewer lights per pixel) but higher CPU overhead to bin lights. Good for scenes with many small, local lights.
- **32 (Default):** Balanced performance.
- **64+:** Lower CPU overhead, but more "false positives" (lights included in a tile where they don't affect pixels). Good for scenes with fewer, large lights.

```fsharp
PipelineConfig.defaults |> PipelineConfig.withTileSize 16
```

### The Shadow Atlas

Shadows are handled via a single, massive **Shadow Atlas** texture. Instead of allocating a separate render target for every light (which causes expensive context switches), we pack all shadow maps into one texture.

> **Architectural Note:** The current Shadow Atlas approach is a design choice necessitated by limitations in current MonoGame versions regarding **Texture Arrays** (which would allow for consistent resolution across many slots). We expect to transition to more modern techniques, such as Texture Arrays, when **Vulkan and DX12** support is released in MonoGame 3.5+.

#### Configuration

You control the atlas via `PipelineConfig`:

```fsharp
Program.withPipeline
  (PipelineConfig.defaults
   |> PipelineConfig.withShadows(
     ShadowConfig.defaults
     |> ShadowConfig.withResolution 2048      // Target size per shadow
     |> ShadowConfig.withAtlasTiles 8         // 8x8 grid = 64 total slots
     |> ShadowConfig.withMaxAtlasSize 16384   // Hard VRAM limit (16k)
     |> ShadowConfig.withSoftShadows 1.0f     // Enable soft shadows (penumbra size)
   ))
```

#### Capacity Planning

| Light Type  | Slots Consumed                       |
| ----------- | ------------------------------------ |
| Directional | `CascadeCount` slots (typically 3-4) |
| Spot        | 1 slot                               |
| Point       | 6 slots (CubeMap unrolled)           |

#### VRAM Usage

The atlas is an `R32_Float` texture:

- **8k Atlas:** ~256 MB VRAM
- **16k Atlas:** ~1 GB VRAM
- **Resolution Scaling:** If `Resolution * TilesAcross > MaxAtlasSize`, the pipeline silently downscales individual shadow maps to fit the maximum texture size.

### Shader Contract (Custom Shaders)

If you implement a custom `ShaderBase.PBRForward` override, Mibo provides lighting data via standard texture bindings.

#### 1. `LightDataTexture` (Sampler: `LightDataSampler`)

A `4 x LightCount` texture containing all light parameters. Each light is one **Row** (4 pixels).

| Pixel (X) | Component | Description                                                     |
| :-------- | :-------- | :-------------------------------------------------------------- |
| **0**     | `.x`      | **Light Type**: 0.0 (Directional), 1.0 (Point), 2.0 (Spot)      |
|           | `.y`      | **Intensity**                                                   |
|           | `.z`      | **Range** (0.0 for Directional Lights)                          |
|           | `.w`      | **Shadow Index**: Base index in atlas (-1.0 if no shadow)       |
| **1**     | `.xyz`    | **Position** (World Space)                                      |
|           | `.w`      | **Spot Outer Angle**: `cos(OuterConeAngle)`                     |
| **2**     | `.xyz`    | **Direction** (Normalized)                                      |
|           | `.w`      | **Spot Inner Angle**: `cos(InnerConeAngle)`                     |
| **3**     | `.xyz`    | **Color** (RGB)                                                 |
|           | `.w`      | **SourceRadius**: Physical size of light source (used for PCSS) |

#### 2. `ShadowMatrixTexture` (Sampler: `ShadowMatrixSampler`)

A `4 x (ShadowCount * 2)` texture. Each shadow map (or cascade/face) provides two matrices:

- **Row `index * 2`**: View Matrix (4 pixels)
- **Row `index * 2 + 1`**: Projection Matrix (4 pixels)

#### 3. `ShadowAtlas` (Sampler: `ShadowAtlasSampler`)

The raw depth texture:

- **Uniforms:** `ShadowAtlasSize` (float), `ShadowAtlasTilesX` (float)
- **Sampling:** To sample a shadow map `i`, calculate UVs based on `i / TilesX` (Row) and `i % TilesX` (Col)

### Soft Shadows (PCF & Poisson)

> "Mibo does not include any shader files - you must provide your own.\*\* Soft shadow filtering (like Rotated Poisson Disk Sampling) is implemented in your custom PBR shader. The pipeline just provides `ShadowConfig.withSoftShadows` configuration value to your shader via `ShadowAtlasSize` and other uniforms.

Configure soft shadows in your pipeline setup. The float parameter (`penumbra`) represents the base physical softness of shadows and is passed to your shader.
