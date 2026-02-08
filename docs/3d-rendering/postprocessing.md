---
title: Post-Processing
category: 3D Rendering
categoryindex: 4
index: 15
---

# Post-Processing

Post-processing runs after main scene render. Configure via `PipelineConfig`.

## Bloom

Creates soft glow around bright pixels.

```fsharp
type BloomConfig = {
  Threshold: float32
  Intensity: float32
  Scatter: float32
}
```

**Threshold**: Minimum brightness for bloom contribution. Pixels below this don't glow. Typical: 0.8 - 1.2.

**Intensity**: Overall glow brightness. Higher = more visible.

**Scatter**: Glow spread distance. Higher = wider, softer bloom.

Enable bloom:

```fsharp
let bloomConfig = {
    Threshold = 1.0f
    Intensity = 0.5f
    Scatter = 0.7f
}

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPostProcess {
      PostProcessConfig.defaults with
          Bloom = ValueSome bloomConfig
    }
```

**Use cases:**
- Glowing projectiles/spells
- Neon lights, lasers
- HDR bloom for bright surfaces
- Dreamy/mystical atmosphere

**Performance**: Bloom adds full-screen blur pass. Cost increases with Scatter.

## Tone Mapping

Maps HDR colors to displayable LDR range.

```fsharp
type ToneMappingConfig =
  | NoToneMapping
  | Reinhard
  | ACES
  | Filmic
  | AgX
```

**NoToneMapping**: Values clip at white. Fastest.

**Reinhard**: Simple classic algorithm. Good contrast rolloff.

**ACES**: Industry standard. Excellent color reproduction, cinematic.

**Filmic**: Good highlight rolloff, preserves shadow detail.

**AgX**: Modern film-inspired. Pleasant color grading.

Configure:

```fsharp
let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPostProcess {
      PostProcessConfig.defaults with
          ToneMapping = ToneMappingConfig.ACES
    }
```

**When to use:**
- Use ACES or Filmic for cinematic games
- Use NoToneMapping for stylized/cartoon look (control highlights manually)
- Use Reinhard for older aesthetic or simpler feel

## SSAO (Screen Space Ambient Occlusion)

Darkens corners and crevices where geometry is close together.

```fsharp
type SSAOConfig = {
  Radius: float32
  Intensity: float32
  SampleCount: int
}
```

**Radius**: Sampling radius in world units. Larger = more coverage, may include distant geometry.

**Intensity**: AO strength. Higher = darker, more pronounced.

**SampleCount**: Samples per pixel. More = better quality, higher cost.

Enable SSAO:

```fsharp
let ssaoConfig = {
    Radius = 0.5f
    Intensity = 1.0f
    SampleCount = 16
}

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPostProcess {
      PostProcessConfig.defaults with
          SSAO = ValueSome ssaoConfig
    }
```

**Use cases:**
- Indoor scenes (contact shadows on walls/floors)
- Dense environments (forests, caves, cities)
- Realistic depth perception

**Performance**: SSAO adds full-screen sampling pass. Cost scales with SampleCount.

## Post-Process Config

```fsharp
type PostProcessConfig = {
  SSAO: SSAOConfig voption
  Bloom: BloomConfig voption
  ToneMapping: ToneMappingConfig
}
```

Combine effects:

```fsharp
let fullPostProcess = {
    SSAO = ValueSome {
        Radius = 0.4f
        Intensity = 0.8f
        SampleCount = 12
    }
    Bloom = ValueSome {
        Threshold = 0.9f
        Intensity = 0.4f
        Scatter = 0.6f
    }
    ToneMapping = ToneMappingConfig.Filmic
}

let config =
  PipelineConfig.defaults
  |> PipelineConfig.withPostProcess (ValueSome fullPostProcess)
```

## Render Target Management

Post-processing requires render target allocation:

- Scene renders to offscreen target (HDR format)
- Post-process effects read from target, write to backbuffer
- Render targets pooled and reused

No manual management needed. Pipeline handles automatically.

See also: [Lighting](lighting.html), [Materials](materials.html)
