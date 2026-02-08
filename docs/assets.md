---
title: Assets
category: Amenities
categoryindex: 5
index: 21
---

# Assets (loading + caching)

Mibo provides a simple, functional API for loading and caching game assets through the `Mibo.Elmish.Assets` module. It wraps MonoGame's `ContentManager` with automatic caching so you never load the same texture twice.

## Enabling the service

Add it in your composition root:

```fsharp
Program.mkProgram init update
|> Program.withAssets
```

## Loading Standard Assets

Once enabled, use these functions anywhere you have a `GameContext`:

```fsharp
let init (ctx: GameContext): struct(Model * Cmd<Msg>) =
  // Load and cache content pipeline assets
  let player = Assets.texture "sprites/player" ctx
  let font = Assets.font "fonts/ui" ctx
  let bgm = Assets.sound "audio/background" ctx
  let enemyModel = Assets.model "models/enemy" ctx
  let shader = Assets.effect "Effects/Grid" ctx
  
  { PlayerTex = player
    Font = font
    Bgm = bgm
    Enemy = enemyModel
    Shader = shader }, Cmd.none
```

All these functions cache results automatically:

| Function | Returns | Description |
|----------|---------|-------------|
| `Assets.texture path ctx` | `Texture2D` | 2D image asset |
| `Assets.font path ctx` | `SpriteFont` | Bitmap font |
| `Assets.sound path ctx` | `SoundEffect` | Audio effect |
| `Assets.model path ctx` | `Model` | 3D model |
| `Assets.effect path ctx` | `Effect` | Shader/effect |

## Custom Assets

For assets not loaded through the content pipeline, use `getOrCreate`:

```fsharp
<<<<<<< Updated upstream
// Create a custom shader once, reuse forever
let outlineFx =
  Assets.getOrCreate "OutlineEffect" (fun gd -> new Effect(gd, bytecode)) ctx

// Create a render target
let rt =
  Assets.getOrCreate "MainRenderTarget" 
    (fun gd -> new RenderTarget2D(gd, 1920, 1080)) ctx
```

**Why `getOrCreate`?** It's idempotent - safe to call multiple times, creates only once.

For assets that might not exist yet, use `get`:

```fsharp
match Assets.get<MyConfig> "config" ctx with
| ValueSome cfg -> cfg
| ValueNone -> loadDefaultConfig()
```

To force creation (overwriting any existing), use `create`:

```fsharp
// This replaces any existing "playerData" entry
Assets.create "playerData" (fun _ -> loadPlayerFromDisk()) ctx
```

## Loading Non-Pipeline Files

### JSON with JDeck
=======
// Create once, cache forever - preferred approach
let outlineFx =
  Assets.getOrCreate "OutlineEffect" (fun gd -> new Effect(gd, bytecode)) ctx

// Check if already cached without creating
match Assets.get<Effect> "OutlineEffect" ctx with
| ValueSome fx -> printfn "Already loaded"
| ValueNone -> printfn "Not cached yet"

// Force creation (overwrites existing - use with caution)
let freshData =
  Assets.create "tempData" (fun _ -> calculateExpensive()) ctx
```

**Note:** `Assets.get` only retrieves custom assets created via `create` or `getOrCreate`. Content pipeline assets (textures, models, etc.) are loaded through `Assets.texture`, `Assets.model`, etc.

## JSON helpers
>>>>>>> Stashed changes

Mibo includes JSON helpers via [JDeck](https://github.com/AngelMunoz/JDeck):

```fsharp
// One-off load (no caching)
let config = Assets.fromJson "config.json" myDecoder

// Cached for game lifetime
let data = Assets.fromJsonCache "data/levels.json" levelDecoder ctx
```

For writing decoders, see the JDeck docs:

- Source: https://github.com/AngelMunoz/JDeck
- Guide: https://angelmunoz.github.io/JDeck/

### Custom File Loaders

Load any file type with custom logic:

```fsharp
// One-off load
let raw = Assets.fromCustom "data/save.dat" File.ReadAllBytes ctx

// Cached
let parsed = 
  Assets.fromCustomCache "data/items.csv" 
    (fun path -> parseCsvFile path) ctx
```

## Building Data Stores

For game-specific datasets (skills, items, quests), wrap asset loading in a typed store:

```fsharp
type SkillId = SkillId of string

type Skill = {
  Id: SkillId
  Name: string
  CooldownSeconds: float32
}

module SkillStore =
  let loadAll (path: string) : Skill list =
    // No caching - manage lifetime yourself
    Assets.fromJson path SkillDecoders.list
  
  let loadAllCached (path: string) (ctx: GameContext) : Skill list =
    // Cached for game lifetime
    Assets.fromJsonCache path SkillDecoders.list ctx
  
  let indexById (path: string) (ctx: GameContext) : Map<SkillId, Skill> =
    // Custom cached transformation
    Assets.getOrCreate ("skills/index/" + path)
      (fun _ ->
        let skills = Assets.fromJson path SkillDecoders.list
        skills |> List.map (fun s -> s.Id, s) |> Map.ofList)
      ctx
```

Usage:

```fsharp
let skillsById = SkillStore.indexById "Content/skills.json" ctx
```

## Cache Behavior

**Automatic caching applies to:**
- All standard assets (texture, font, sound, model, effect)
- Anything loaded via `getOrCreate` or `*Cache` variants

**Manual lifetime management:**
- Use `Assets.fromJson` or `Assets.fromCustom` for one-off loads
- Build your own store with LRU/eviction if memory is a concern

**Clearing caches:**

```fsharp
// Get the underlying service (advanced)
let service = Assets.getService ctx

// Clear all custom caches (standard assets remain)
service.Clear()

// Dispose custom assets and clear everything
service.Dispose()
```

## Advanced: Direct IAssets Access

For advanced scenarios, access the underlying `IAssets` service:

```fsharp
// Safe retrieval
match Assets.tryGetService ctx with
| ValueSome svc -> // use service
| ValueNone -> // service not registered

// Throws if not registered
let svc = Assets.getService ctx
```

`IAssets` provides the same operations if you need to pass it around as a value:

```fsharp
let loadStuff (assets: IAssets) =
  let tex = assets.Texture "sprite"
  let fx = assets.GetOrCreate "fx" (fun gd -> ...)
  ...
```

**Prefer the `Assets.*` functions** - they're more ergonomic and require less plumbing.

## Performance Notes

- First load reads from disk; subsequent loads return cached reference
- No built-in eviction - caches grow with unique keys loaded
- GPU resources (textures, effects, render targets) are created once via `getOrCreate`
- `Dispose()` on the service cleans up custom assets implementing `IDisposable`

For large games, consider:

1. Chunked loading (per level/biome) with separate cache scopes
2. Custom stores with LRU eviction for dynamic content
3. Non-cached loads for one-time data
