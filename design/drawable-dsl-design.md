# Drawable DSL Design

> [!NOTE]
> This document redesigns the CE system to work with the render pipeline. The DSL produces `Drawable` output, handles transform composition, and shapes data so the renderer just renders.

## Design Goals

1. **Output `Drawable`** - Not `RenderCmd3D`; pipeline consumes `Drawable`
2. **Transform composition** - Matrix multiplications handled in DSL
3. **Relative positioning** - Parent-child transform hierarchy
4. **Data shaping** - DSL prepares exactly what renderer needs
5. **Zero-cost** - Struct state, inlined methods

---

## 1. Core Types

### Drawable (from render pipeline)

```fsharp
[<Struct>]
type Drawable = {
    Mesh: Mesh
    Transform: Matrix
    Material: Material
    BoundingSphere: BoundingSphere
}
```

### DSL State

```fsharp
[<Struct>]
type DrawState = {
    mutable Mesh: Mesh voption
    mutable Transform: Matrix          // Accumulated world transform
    mutable LocalPosition: Vector3
    mutable LocalRotation: Quaternion
    mutable LocalScale: Vector3
    mutable Material: Material
    mutable Parent: Matrix voption     // Parent transform for relative positioning
}

module DrawState =
    let empty = {
        Mesh = ValueNone
        Transform = Matrix.Identity
        LocalPosition = Vector3.Zero
        LocalRotation = Quaternion.Identity
        LocalScale = Vector3.One
        Material = Material.defaultOpaque
        Parent = ValueNone
    }

    /// Compute final world transform from local components + parent
    let computeTransform (state: DrawState) : Matrix =
        let local =
            Matrix.CreateScale(state.LocalScale) *
            Matrix.CreateFromQuaternion(state.LocalRotation) *
            Matrix.CreateTranslation(state.LocalPosition)
        match state.Parent with
        | ValueSome parent -> local * parent
        | ValueNone -> local

    /// Convert state to Drawable
    let toDrawable (state: DrawState) : Drawable voption =
        match state.Mesh with
        | ValueSome mesh ->
            let transform = computeTransform state
            let bounds = BoundingSphere.Transform(mesh.BoundingSphere, transform)
            ValueSome {
                Mesh = mesh
                Transform = transform
                Material = state.Material
                BoundingSphere = bounds
            }
        | ValueNone -> ValueNone
```

---

## 2. Drawable Builder

```fsharp
type DrawableBuilder(buffer: RenderBuffer<Drawable>) =

    member inline _.Yield(_: unit) = DrawState.empty

    member inline _.Run(state: DrawState) =
        DrawState.toDrawable state
        |> ValueOption.iter (fun d -> buffer.Add((), d))

    // === Mesh ===
    [<CustomOperation("mesh")>]
    member inline _.Mesh(state: DrawState, mesh: Mesh) =
        { state with Mesh = ValueSome mesh }

    // === Absolute Positioning ===
    [<CustomOperation("at")>]
    member inline _.At(state: DrawState, position: Vector3) =
        { state with LocalPosition = position }

    [<CustomOperation("at")>]
    member inline _.At(state: DrawState, x: float32, y: float32, z: float32) =
        { state with LocalPosition = Vector3(x, y, z) }

    // === Relative Positioning (parent-relative) ===
    [<CustomOperation("offset")>]
    member inline _.Offset(state: DrawState, offset: Vector3) =
        { state with LocalPosition = state.LocalPosition + offset }

    [<CustomOperation("relativeTo")>]
    member inline _.RelativeTo(state: DrawState, parentTransform: Matrix) =
        { state with Parent = ValueSome parentTransform }

    [<CustomOperation("relativeTo")>]
    member inline _.RelativeTo(state: DrawState, parent: Drawable) =
        { state with Parent = ValueSome parent.Transform }

    // === Rotation ===
    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedBy(state: DrawState, rotation: Quaternion) =
        { state with LocalRotation = state.LocalRotation * rotation }

    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedByEuler(state: DrawState, yaw: float32, pitch: float32, roll: float32) =
        let rot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll)
        { state with LocalRotation = state.LocalRotation * rot }

    [<CustomOperation("lookAt")>]
    member inline _.LookAt(state: DrawState, target: Vector3) =
        let forward = Vector3.Normalize(target - state.LocalPosition)
        let rot = Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, forward, Vector3.Up))
        { state with LocalRotation = rot }

    // === Scale ===
    [<CustomOperation("scaledBy")>]
    member inline _.ScaledBy(state: DrawState, scale: float32) =
        { state with LocalScale = state.LocalScale * scale }

    [<CustomOperation("scaledBy")>]
    member inline _.ScaledBy(state: DrawState, scale: Vector3) =
        { state with LocalScale = state.LocalScale * scale }

    // === Direct Transform (bypass local components) ===
    [<CustomOperation("withTransform")>]
    member inline _.WithTransform(state: DrawState, transform: Matrix) =
        { state with Transform = transform; LocalPosition = Vector3.Zero; LocalRotation = Quaternion.Identity; LocalScale = Vector3.One }

    // === Material ===
    [<CustomOperation("withMaterial")>]
    member inline _.WithMaterial(state: DrawState, material: Material) =
        { state with Material = material }

    [<CustomOperation("withAlbedo")>]
    member inline _.WithAlbedo(state: DrawState, color: Color) =
        { state with Material = { state.Material with PBR = { state.Material.PBR with AlbedoColor = color } } }

    [<CustomOperation("withMetallic")>]
    member inline _.WithMetallic(state: DrawState, metallic: float32) =
        { state with Material = { state.Material with PBR = { state.Material.PBR with Metallic = metallic } } }

    [<CustomOperation("withRoughness")>]
    member inline _.WithRoughness(state: DrawState, roughness: float32) =
        { state with Material = { state.Material with PBR = { state.Material.PBR with Roughness = roughness } } }
```

---

## 3. Scene Builder

```fsharp
type RenderBuilder(buffer: RenderBuffer<Drawable>) =

    member inline _.Yield(_: unit) = ()
    member inline _.Zero() = ()
    member inline _.Delay([<InlineIfLambda>] f: unit -> unit) = f
    member inline _.Run([<InlineIfLambda>] f: unit -> unit) = f()

    member inline _.Combine((), [<InlineIfLambda>] f: unit -> unit) = f()

    member inline _.For(source: 'T seq, [<InlineIfLambda>] body: 'T -> unit) =
        for item in source do body item

    /// Nested drawable
    [<CustomOperation("draw")>]
    member inline _.Draw((), [<InlineIfLambda>] configure: DrawableBuilder -> unit) =
        let builder = DrawableBuilder(buffer)
        configure builder

/// Scene-level CE instance
let render (buffer: RenderBuffer<Drawable>) = RenderBuilder(buffer)
```

---

## 4. Transform Composition Examples

### Basic Positioning

```fsharp
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<Drawable>) =
    render buffer {
        draw {
            mesh cubeMesh
            at (10f, 0f, 5f)
        }
    }
```

### Rotation + Scale

```fsharp
render buffer {
    draw {
        mesh characterMesh
        at playerPosition
        rotatedBy playerRotation
        scaledBy 1.5f
    }
}
```

### Parent-Relative (Hierarchy)

```fsharp
// Weapon attached to character hand
let characterTransform = Matrix.CreateWorld(charPos, charForward, Vector3.Up)

render buffer {
    // Character
    draw {
        mesh characterMesh
        withTransform characterTransform
    }

    // Weapon relative to character
    draw {
        mesh swordMesh
        relativeTo characterTransform
        offset (Vector3(0.5f, 1.2f, 0f))  // Local offset from hand
        rotatedBy swordRotation
    }
}
```

### Look At Target

```fsharp
render buffer {
    draw {
        mesh turretMesh
        at turretPosition
        lookAt targetPosition  // Computes rotation to face target
    }
}
```

### Transform Multiplication Chain

```fsharp
// Manual transform composition (advanced)
let parentWorld = Matrix.CreateRotationY(parentAngle) * Matrix.CreateTranslation(parentPos)
let childLocal = Matrix.CreateScale(0.5f) * Matrix.CreateTranslation(Vector3(1f, 0f, 0f))
let childWorld = childLocal * parentWorld

render buffer {
    draw {
        mesh childMesh
        withTransform childWorld
    }
}
```

---

## 5. Batch Rendering

```fsharp
render buffer {
    // Render many similar entities
    for entity in model.Entities do
        draw {
            mesh entity.Mesh
            at entity.Position
            rotatedBy entity.Rotation
            withMaterial entity.Material
        }

    // Instanced trees - same mesh, different transforms
    for tree in model.Trees do
        draw {
            mesh treeMesh
            at tree.Position
            scaledBy tree.Scale
            withMaterial foliageMaterial
        }
}
```

---

## 6. What the DSL Computes (Renderer Just Renders)

| DSL Operation | Result Computed |
|---------------|-----------------|
| `at pos` | Sets `LocalPosition` |
| `rotatedBy rot` | Multiplies into `LocalRotation` |
| `scaledBy s` | Multiplies into `LocalScale` |
| `relativeTo parent` | Sets `Parent` transform |
| `offset o` | Adds to `LocalPosition` |
| `lookAt target` | Computes rotation quaternion |
| `Run` | Computes final `Matrix` from S * R * T * Parent |
| `Run` | Computes `BoundingSphere` from mesh + transform |
| `Run` | Emits fully-formed `Drawable` to buffer |

**The renderer receives:**
- Pre-computed world transform matrix
- Pre-computed bounding sphere
- Material ready to bind
- Mesh ready to draw

**The renderer does NOT:**
- Compute any transforms
- Multiply matrices
- Calculate bounds
- Resolve parent-child relationships

---

## 7. Integration with View Signature

```fsharp
// View signature from render pipeline design
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<Drawable>) =
    render buffer {
        for entity in model.Entities do
            draw {
                mesh entity.Mesh
                at entity.Position
                rotatedBy entity.Rotation
                scaledBy entity.Scale
                withMaterial entity.Material
            }
    }
```

---

## 8. Key Differences from Original CE Design

| Original Design | New Design |
|-----------------|------------|
| Outputs `RenderCmd3D` | Outputs `Drawable` |
| Transform computed in renderer | Transform computed in DSL |
| Generic scene state | Simple draw state |
| Complex batching in DSL | Batching in pipeline |
| SIMD in DSL | SIMD in pipeline (if needed) |
| Plugin architecture | Focused on data shaping |

---

## 9. Lighting Integration

### Hierarchy

```
PipelineConfig.defaultLighting     → Used if view doesn't specify
    ↓
Scene-level withLighting           → Overrides pipeline default for this frame
```

### Pipeline Defaults (configured once)

```fsharp
Program.withPipeline (
    PipelineConfig.forward
    |> PipelineConfig.withDefaultLighting Lighting.defaultSunlight
    |> PipelineConfig.withShadows ShadowConfig.defaults
)
```

### Scene-Level Override (per frame)

```fsharp
type RenderBuilder(buffer: RenderBuffer<Drawable>) =
    let mutable lighting: LightingState voption = ValueNone

    [<CustomOperation("withLighting")>]
    member _.WithLighting((), state: LightingState) =
        lighting <- ValueSome state

    [<CustomOperation("withLighting")>]
    member _.WithLighting((), preset: LightingPreset) =
        lighting <- ValueSome (Lighting.fromPreset preset)

    member _.GetLighting() = lighting
```

### Usage by Complexity

```fsharp
// Tier 1: Nothing - pipeline default
render buffer { draw { mesh cube; at origin } }

// Tier 2: Preset
render buffer {
    withLighting Lighting.defaultSunlight
    draw { mesh cube; at origin }
}

// Tier 3: Custom
render buffer {
    withLighting {
        AmbientColor = Color(0.1f, 0.1f, 0.15f)
        Lights = [| Light.Directional sunConfig |]
    }
    draw { mesh cube; at origin }
}
```

---

## 10. Shadow Configuration

Shadows are **pipeline-level infrastructure**, not DSL content:

| Level | What | DSL Involvement |
|-------|------|-----------------|
| Pipeline | Resolution, cascades, PCF | None |
| Light | Which lights cast shadows | Via `LightingState` |
| Material | CastsShadow / ReceivesShadow | `withMaterialFlags` |

```fsharp
// Per-drawable shadow flags
draw {
    mesh treeMesh
    at treePosition
    withMaterialFlags (CastsShadow ||| ReceivesShadow)
}

// Non-shadow-casting object (e.g., grass)
draw {
    mesh grassMesh
    at grassPosition
    withMaterialFlags ReceivesShadow  // Only receives, doesn't cast
}
```

---

## 11. Escape Hatches

### Custom Effect (per-drawable shader override)

```fsharp
draw {
    mesh waterMesh
    at waterPosition
    withEffect (fun effect ctx ->
        effect.Parameters.["Time"].SetValue(ctx.TotalTime)
        effect.Parameters.["WaveHeight"].SetValue(0.5f)
    )
}
```

### Raw GPU Access

```fsharp
render buffer {
    draw { mesh terrain; at origin }

    // Direct GraphicsDevice access
    custom (fun device camera ->
        device.BlendState <- BlendState.Additive
        myCustomRenderer.Draw(device, camera)
        device.BlendState <- BlendState.Opaque
    )
}
```

### RenderCmd3D Fallback (backwards compatibility)

```fsharp
render buffer {
    draw { mesh cube; at origin }

    // Existing RenderCmd3D for sprites, lines, etc.
    raw (DrawLine(p1, p2, Color.Red, Opaque))
    raw (DrawSpriteQuad spriteQuadCmd)
}
```

---

## 12. Shader Fallback Strategy

### BasicEffect by Default

Without custom shaders, the pipeline uses `BasicEffect` (always available):

| Tier | User provides | Result |
|------|---------------|--------|
| 1 | Nothing | BasicEffect + simple lighting works |
| 2 | PBR.fx | Full PBR materials |
| 3 | ShadowCaster.fx | Shadows enabled |
| 4 | PostProcess.fx | Bloom, SSAO, tone mapping |

### Custom Shaders via Asset Names

Shaders are specified by **asset name** (not loaded Effect), loaded in `Initialize` when `ContentManager` is available:

```fsharp
// Tier 1: No shaders - BasicEffect fallback
Program.withPipeline PipelineConfig.forward

// Tier 2+: Add shaders as you progress
Program.withPipeline (
    PipelineConfig.forward
    |> PipelineConfig.withShader ShaderBase.PBRForward "Shaders/PBR"
    |> PipelineConfig.withShader ShaderBase.ShadowCaster "Shaders/ShadowCaster"
)
```

### What Works at Each Level

| Feature | BasicEffect | PBR Shader | Shadow Shader |
|---------|-------------|------------|---------------|
| Diffuse color | ✅ | ✅ | |
| Textures | ✅ | ✅ | |
| 3 directional lights | ✅ | ✅ | |
| Normal maps | ❌ | ✅ | |
| Metallic/roughness | ❌ | ✅ | |
| Shadows | ❌ | ❌ | ✅ |

---

## 13. Library Structure

**Recommendation: Separate library**

```
Mibo/                           (Core - no changes)
├── Elmish.fs
├── Input.fs
├── Camera.fs
├── Graphics2D.fs
├── Graphics3D.fs               (Batch3DRenderer - kept for simple use)
└── Assets.fs

Mibo.Pipeline/                  (New - opt-in advanced rendering)
├── Types/
│   ├── Drawable.fs
│   ├── Material.fs
│   ├── Lighting.fs
│   └── Config.fs
├── DSL/
│   ├── DrawableBuilder.fs
│   └── RenderBuilder.fs
├── Pipeline/
│   ├── RenderPipeline.fs
│   └── RenderTargetPool.fs
└── Passes/
    ├── ShadowPass.fs
    ├── ForwardPass.fs
    └── PostProcess.fs
```

> [!NOTE]
> No bundled shaders. Users add their own shaders to Content as they progress from Tier 1 to Tier 4.

---

## 14. What Else is Needed

| Component | Status | Notes |
|-----------|--------|-------|
| Drawable types | ✅ Designed | In render-pipeline-architecture-design.md |
| Pipeline config | ✅ Designed | Config builders, voption pattern |
| Material system | ✅ Designed | PBR, flags, builders |
| Lighting types | ✅ Designed | Directional, Point, Spot + Shadow voption |
| DSL | ✅ Designed | This document |
| Shader fallback | ✅ Designed | BasicEffect → custom shaders |
| **Mesh loading** | ❌ Needed | Convert MonoGame `Model` → our `Mesh` |
| **Culling** | ❌ Needed | Frustum culling, occlusion (optional) |
| **Sorting** | ❌ Needed | Front-to-back opaque, back-to-front transparent |
| **Post-process** | ❌ Needed | SSAO, Bloom, ToneMapping implementations |
| **Debug viz** | ❌ Needed | Shadow map, wireframe, bounds |

---

## 15. Implementation Roadmap

### Phase 1: Core Types + DSL (no GPU)
1. `Drawable`, `Material`, `Mesh` types
2. `DrawState`, `DrawableBuilder`
3. `RenderBuilder` with lighting override
4. Unit tests for transform composition

### Phase 2: Pipeline Structure (BasicEffect)
1. `IRenderPipeline` interface
2. `RenderPipeline.forward` with BasicEffect fallback
3. `RenderTargetPool` module
4. Integration with `Program.withPipeline`

### Phase 3: Custom Shaders (opt-in)
1. PBR shader (MGFX) - user provides
2. Forward pass renders with PBR when available
3. Shadow caster shader - user provides
4. Shadow pass for directional lights

### Phase 4: Post-Processing (opt-in)
1. HDR render target setup
2. Bloom shader + pass
3. Tone mapping shader + pass
4. SSAO (optional)

### Phase 5: Polish
1. Debug visualization
2. Performance optimization
3. Documentation
4. Sample project
