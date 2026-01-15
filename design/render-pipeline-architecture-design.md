# Render Pipeline Architecture Design

> [!NOTE]
> This is a **design document** structured like F# code: types and interfaces are defined first, then modules implement them, then usage examples show composition.

## Design Goals

1. **Strongly-typed** - No string-based lookups
2. **Complete features** - Shadows, PBR, post-processing built-in
3. **Progressive disclosure** - Simple defaults → advanced customization
4. **Separation of concerns** - CE for view logic, pipeline for execution
5. **Zero-allocation hot paths** - Struct-based, pooled
6. **Modules + object expressions** - No classes, testable via interfaces
7. **Presence = enabled** - `voption` configs, no `bool Enabled` fields

---

## 1. Core Types

> All types defined before any usage.

### Primitives

```fsharp
[<Struct>]
type Mesh = {
    VertexBuffer: VertexBuffer
    IndexBuffer: IndexBuffer
    IndexCount: int
    BoundingBox: BoundingBox
}

[<Struct>]
type Drawable = {
    Mesh: Mesh
    Transform: Matrix
    Material: Material
    BoundingSphere: BoundingSphere
}

[<Struct>]
type RenderTargetSpec = {
    Width: int
    Height: int
    Format: SurfaceFormat
    DepthFormat: DepthFormat
}

[<Struct>]
type ShaderKey = {
    Base: ShaderBase
    Features: ShaderFeatures
}

type ShaderBase = ShadowCaster | GBufferFill | PBRForward | DeferredLighting | Unlit

[<Struct; Flags>]
type ShaderFeatures = None = 0 | Skinned = 1 | NormalMap = 4 | Shadows = 32 | IBL = 64
```

### Configuration Types

```fsharp
type PipelineMode = Forward | ForwardPlus | Deferred

[<Struct>]
type ShadowConfig = {
    Resolution: int
    CascadeCount: int
    PCFSamples: int
    SoftShadows: SoftShadowConfig voption
}

[<Struct>]
type SoftShadowConfig = { Penumbra: float32 }

[<Struct>]
type SSAOConfig = { Radius: float32; Intensity: float32; SampleCount: int }

[<Struct>]
type BloomConfig = { Threshold: float32; Intensity: float32; Scatter: float32 }

type ToneMappingConfig = None | Reinhard | ACES | Filmic | AgX

[<Struct>]
type PostProcessConfig = {
    SSAO: SSAOConfig voption
    Bloom: BloomConfig voption
    ToneMapping: ToneMappingConfig
}

[<Struct>]
type PipelineConfig = {
    Mode: PipelineMode
    Shadows: ShadowConfig voption               // ValueNone = disabled
    PostProcess: PostProcessConfig voption
    DefaultLighting: LightingState voption      // Used if view doesn't override
    ShaderOverrides: Map<ShaderBase, string>    // Asset names, loaded in Initialize
}
```

### Material & Lighting

```fsharp
[<Struct>]
type PBRMaterial = {
    AlbedoColor: Color
    AlbedoMap: Texture2D voption
    NormalMap: Texture2D voption
    Metallic: float32
    Roughness: float32
}

[<Struct; Flags>]
type MaterialFlags = None = 0 | CastsShadow = 1 | ReceivesShadow = 2 | Transparent = 4

[<Struct>]
type Material = {
    PBR: PBRMaterial
    Flags: MaterialFlags
    RenderQueue: int
}

[<Struct>]
type ShadowSettings = { Bias: float32; NormalBias: float32 }

[<Struct>]
type DirectionalLight = {
    Direction: Vector3
    Color: Color
    Intensity: float32
    Shadow: ShadowSettings voption   // voption, not bool
    CascadeCount: int
    CascadeSplits: float32[]
}

[<Struct>]
type LightingState = {
    AmbientColor: Color
    AmbientIntensity: float32
    Lights: Light[]
}
```

---

## 2. Interfaces

> Abstractions for testability. Implementations use object expressions.

```fsharp
type IRenderPipeline =
    abstract member Initialize: GraphicsDevice -> unit
    abstract member Render: GameContext * RenderBuffer<Drawable> * LightingState -> unit

type IShaderCache =
    abstract member Get: ShaderKey -> Effect

type IRenderTargetPool =
    abstract member Acquire: RenderTargetSpec -> RenderTarget2D
    abstract member ReleaseAll: unit -> unit
```

---

## 3. Module Implementations

> Each module contains `State` record + functions, and a `create` function returning an interface.

### RenderTargetPool

```fsharp
module RenderTargetPool =
    type State = {
        Device: GraphicsDevice
        Available: Dictionary<RenderTargetSpec, ResizeArray<RenderTarget2D>>
        InUse: ResizeArray<struct (RenderTarget2D * RenderTargetSpec)>
    }

    let private acquire' (spec: RenderTargetSpec) (state: State) : RenderTarget2D =
        match state.Available.TryGetValue(spec) with
        | true, list when list.Count > 0 ->
            let rt = list.[list.Count - 1]
            list.RemoveAt(list.Count - 1)
            state.InUse.Add(struct (rt, spec))
            rt
        | _ ->
            let rt = new RenderTarget2D(state.Device, spec.Width, spec.Height, false, spec.Format, spec.DepthFormat)
            state.InUse.Add(struct (rt, spec))
            rt

    let private releaseAll' (state: State) =
        for struct (rt, spec) in state.InUse do
            match state.Available.TryGetValue(spec) with
            | true, list -> list.Add(rt)
            | false, _ ->
                let list = ResizeArray()
                list.Add(rt)
                state.Available.[spec] <- list
        state.InUse.Clear()

    let create (device: GraphicsDevice) : IRenderTargetPool =
        let state = { Device = device; Available = Dictionary(); InUse = ResizeArray() }
        { new IRenderTargetPool with
            member _.Acquire(spec) = acquire' spec state
            member _.ReleaseAll() = releaseAll' state }
```

### ShaderCache

```fsharp
module ShaderCache =
    let create (device: GraphicsDevice) : IShaderCache =
        let cache = Dictionary<ShaderKey, Effect>()
        { new IShaderCache with
            member _.Get(key) =
                match cache.TryGetValue(key) with
                | true, effect -> effect
                | false, _ ->
                    let effect = compileShader device key
                    cache.[key] <- effect
                    effect }
```

### RenderPipeline

```fsharp
module RenderPipeline =
    let forward (config: PipelineConfig) (game: Game) : IRenderPipeline =
        // Closure state
        let mutable device: GraphicsDevice = Unchecked.defaultof<_>
        let mutable rtPool: IRenderTargetPool = Unchecked.defaultof<_>
        let mutable basicEffect: BasicEffect = Unchecked.defaultof<_>  // Fallback
        let customShaders = Dictionary<ShaderBase, Effect>()
        let shadowMaps = ResizeArray<RenderTarget2D>()

        { new IRenderPipeline with
            member _.Initialize(gd) =
                device <- gd
                rtPool <- RenderTargetPool.create gd

                // BasicEffect as fallback (always works)
                basicEffect <- new BasicEffect(gd)
                basicEffect.EnableDefaultLighting()

                // Load custom shaders from content (asset names → Effects)
                for KeyValue(shaderBase, assetName) in config.ShaderOverrides do
                    customShaders.[shaderBase] <- game.Content.Load<Effect>(assetName)

                // Pre-allocate shadow maps (only if we have shadow shader)
                if customShaders.ContainsKey(ShaderBase.ShadowCaster) then
                    config.Shadows |> ValueOption.iter (fun cfg ->
                        for _ in 0 .. cfg.CascadeCount - 1 do
                            shadowMaps.Add(new RenderTarget2D(gd, cfg.Resolution, cfg.Resolution, false, SurfaceFormat.Single, DepthFormat.Depth24))
                    )

            member _.Render(ctx, buffer, lighting) =
                let effectiveLighting =
                    match lighting with
                    | Some l -> l
                    | None -> config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

                for drawable in buffer do
                    // Use custom shader if available, otherwise BasicEffect
                    match customShaders.TryGetValue(ShaderBase.PBRForward) with
                    | true, effect -> renderWithEffect effect drawable effectiveLighting
                    | false, _ -> renderWithBasicEffect basicEffect drawable effectiveLighting

                rtPool.ReleaseAll()
        }
```

---

## 4. Config Builders

> Defaults + builder functions for ergonomic configuration.

```fsharp
module ShadowConfig =
    let defaults = { Resolution = 1024; CascadeCount = 3; PCFSamples = 4; SoftShadows = ValueNone }
    let withResolution res cfg = { cfg with Resolution = res }
    let withCascades n cfg = { cfg with CascadeCount = n }

module SSAOConfig =
    let defaults = { Radius = 0.5f; Intensity = 1.0f; SampleCount = 16 }

module BloomConfig =
    let defaults = { Threshold = 1.0f; Intensity = 0.5f; Scatter = 0.7f }

module PostProcessConfig =
    let defaults = { SSAO = ValueNone; Bloom = ValueNone; ToneMapping = ToneMappingConfig.ACES }
    let withSSAO cfg pp = { pp with SSAO = ValueSome cfg }
    let withBloom cfg pp = { pp with Bloom = ValueSome cfg }

module PipelineConfig =
    let forward = {
        Mode = Forward
        Shadows = ValueNone
        PostProcess = ValueNone
        DefaultLighting = ValueNone  // Uses Lighting.ambient
        ShaderOverrides = Map.empty  // BasicEffect fallback
    }
    let deferred = { forward with Mode = Deferred }
    let withShadows cfg pc = { pc with Shadows = ValueSome cfg }
    let withPostProcess cfg pc = { pc with PostProcess = ValueSome cfg }
    let withDefaultLighting lighting pc = { pc with DefaultLighting = ValueSome lighting }
    let withShader shaderBase assetName pc =
        { pc with ShaderOverrides = pc.ShaderOverrides.Add(shaderBase, assetName) }
```

---

## 5. Program Integration

> Can live in an external library. No changes to core `Program` type needed.

```fsharp
module Program =
    let withPipeline (config: PipelineConfig) (view: GameContext -> 'Model -> RenderBuffer<Drawable> -> unit) (program: Program<_,_>) =
        program |> withRenderer (fun game ->
            let pipeline = RenderPipeline.forward config game
            PipelineRenderer.create pipeline view game
        )
```

---

## 6. Usage Examples

### Tier 1: Simple (Defaults)

```fsharp
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<Drawable>) =
    render buffer {
        for entity in model.Entities do
            entity { mesh entity.Mesh; at entity.Position }
    }

Program.create init update view
|> Program.withPipeline PipelineConfig.forward
|> Program.run game
```

### Tier 2: Configured

```fsharp
Program.create init update view
|> Program.withPipeline (
    PipelineConfig.forward
    |> PipelineConfig.withShadows (ShadowConfig.defaults |> ShadowConfig.withResolution 2048)
    |> PipelineConfig.withPostProcess (
        PostProcessConfig.defaults
        |> PostProcessConfig.withSSAO SSAOConfig.defaults
        |> PostProcessConfig.withBloom BloomConfig.defaults
    )
)
|> Program.run game
```

### Tier 3: Custom Materials

```fsharp
let metalMaterial =
    Material.defaultOpaque
    |> Material.withMetallic 1.0f
    |> Material.withRoughness 0.2f

let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<Drawable>) =
    render buffer {
        entity { mesh cubeMesh; at Vector3.Zero; withMaterial metalMaterial }
    }
```

### Tier 4: Custom Lighting

```fsharp
let lighting = {
    AmbientColor = Color(0.1f, 0.12f, 0.15f)
    AmbientIntensity = 1.0f
    Lights = [|
        Light.Directional {
            Direction = Vector3.Normalize(Vector3(-1f, -1f, -1f))
            Color = Color.White
            Intensity = 2.0f
            Shadow = ValueSome { Bias = 0.001f; NormalBias = 0.02f }
            CascadeCount = 4
            CascadeSplits = [| 0.05f; 0.15f; 0.5f; 1.0f |]
        }
    |]
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER CODE (View)                         │
│  render buffer { entity { mesh; at pos; withMaterial mat } }    │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     RenderBuffer<Drawable>                      │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│               IRenderPipeline.Render(ctx, buffer, lighting)     │
└─────────────────────────────────────────────────────────────────┘
         │              │              │              │
         ▼              ▼              ▼              ▼
   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
   │ Shadow   │  │ Forward/ │  │ Post     │  │ Blit to  │
   │  Pass    │  │ Deferred │  │ Process  │  │ Screen   │
   └──────────┘  └──────────┘  └──────────┘  └──────────┘
```

---

## Verification Plan

1. **Unit tests** - Culling, sorting, resource pooling
2. **Visual tests** - Shadow quality, PBR correctness, post-process effects
3. **Performance** - 1K, 10K, 100K drawables benchmarks
4. **Integration** - Works with existing 3DSample
