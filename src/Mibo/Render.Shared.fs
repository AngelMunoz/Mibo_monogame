namespace Mibo.Rendering

open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Render Target Pooling
// ============================================================================

/// <summary>
/// Render target specification for pooling.
/// Defines the size and format of render targets to be pooled.
/// </summary>
[<Struct>]
type RenderTargetSpec = {
  /// <summary>
  /// Width of the render target in pixels.
  /// </summary>
  Width: int
  /// <summary>
  /// Height of the render target in pixels.
  /// </summary>
  Height: int
  /// <summary>
  /// Color format of the render target.
  /// </summary>
  Format: SurfaceFormat
  /// <summary>
  /// Depth buffer format (if depth is needed).
  /// </summary>
  DepthFormat: DepthFormat
}

/// <summary>
/// Render target pool interface for efficient render target reuse.
/// </summary>
type IRenderTargetPool =
  /// <summary>
  /// Acquire a render target matching the specification.
  /// Returns an existing pooled RT if available, or creates a new one.
  /// </summary>
  /// <param name="spec">Specification describing desired render target.</param>
  /// <returns>A RenderTarget2D matching the specification.</returns>
  abstract member Acquire: RenderTargetSpec -> RenderTarget2D
  /// <summary>
  /// Release all currently-acquired render targets back to the pool.
  /// Should be called once per frame to recycle targets for reuse.
  /// </summary>
  abstract member ReleaseAll: unit -> unit

module RenderTargetPool =
  /// Internal pool state
  type State = {
    Device: GraphicsDevice
    Available: Dictionary<RenderTargetSpec, ResizeArray<RenderTarget2D>>
    InUse: ResizeArray<struct (RenderTarget2D * RenderTargetSpec)>
  }

  let private acquire'
    (spec: RenderTargetSpec)
    (state: State)
    : RenderTarget2D =
    match state.Available.TryGetValue(spec) with
    | true, list when list.Count > 0 ->
      let rt = list.[list.Count - 1]
      list.RemoveAt(list.Count - 1)
      state.InUse.Add(struct (rt, spec))
      rt
    | _ ->
      let rt =
        new RenderTarget2D(
          state.Device,
          spec.Width,
          spec.Height,
          false,
          spec.Format,
          spec.DepthFormat,
          0,
          RenderTargetUsage.PreserveContents
        )

      state.InUse.Add(struct (rt, spec))
      rt

  let private releaseAll'(state: State) =
    for struct (rt, spec) in state.InUse do
      match state.Available.TryGetValue(spec) with
      | true, list -> list.Add(rt)
      | false, _ ->
        let list = ResizeArray()
        list.Add(rt)
        state.Available.[spec] <- list

    state.InUse.Clear()

  /// <summary>
  /// Create a render target pool for the specified device.
  /// The pool will create and reuse render targets across frames to avoid allocation overhead.
  /// </summary>
  /// <param name="device">The GraphicsDevice to create render targets with.</param>
  /// <returns>A new IRenderTargetPool implementation.</returns>
  let create(device: GraphicsDevice) : IRenderTargetPool =
    let state = {
      Device = device
      Available = Dictionary()
      InUse = ResizeArray()
    }

    { new IRenderTargetPool with
        member _.Acquire(spec) = acquire' spec state
        member _.ReleaseAll() = releaseAll' state
    }

// ============================================================================
// Effect Extensions
// ============================================================================

[<AutoOpen>]
module EffectHelpers =
  let private paramCache =
    ConditionalWeakTable<Effect, Dictionary<string, EffectParameter>>()

  let findParam (name: string) (effect: Effect) =
    let dict =
      match paramCache.TryGetValue(effect) with
      | true, d -> d
      | false, _ ->
        let d = Dictionary<string, EffectParameter>()

        for i = 0 to effect.Parameters.Count - 1 do
          let p = effect.Parameters.[i]
          d.[p.Name] <- p

        paramCache.Add(effect, d)
        d

    match dict.TryGetValue(name) with
    | true, p -> ValueSome p
    | false, _ -> ValueNone

  type Effect with
    member inline this.SafeSetParam(name: string, value: bool) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: int) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Matrix) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Matrix[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Quaternion) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: float32) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: float32[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Texture) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Texture[]) =
      findParam name this
      |> ValueOption.iter(fun p ->
        for i = 0 to min (value.Length - 1) (p.Elements.Count - 1) do
          p.Elements.[i].SetValue(value.[i]))

    member inline this.SafeSetParam(name: string, value: Vector2) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector2[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector3) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector3[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector4) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Vector4[]) =
      findParam name this |> ValueOption.iter(fun p -> p.SetValue(value))

    member inline this.SafeSetParam(name: string, value: Color) =
      findParam name this
      |> ValueOption.iter(fun p -> p.SetValue(value.ToVector4()))

// ============================================================================
// Shared Types
// ============================================================================

/// <summary>UV rectangle in normalized texture coordinates.</summary>
[<Struct>]
type UvRect = {
  U0: float32
  V0: float32
  U1: float32
  V1: float32
}

module UvRect =
  let full: UvRect = {
    U0 = 0.0f
    V0 = 0.0f
    U1 = 1.0f
    V1 = 1.0f
  }
