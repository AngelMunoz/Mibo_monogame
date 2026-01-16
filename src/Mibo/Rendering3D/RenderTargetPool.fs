namespace Mibo.Rendering.Graphics3D

open System.Collections.Generic
open Microsoft.Xna.Framework.Graphics

// ============================================================================
// Render Target Pooling
// ============================================================================

/// Render target specification for pooling
[<Struct>]
type RenderTargetSpec = {
  Width: int
  Height: int
  Format: SurfaceFormat
  DepthFormat: DepthFormat
}

/// Render target pool interface
type IRenderTargetPool =
  abstract member Acquire: RenderTargetSpec -> RenderTarget2D
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

  /// Create a render target pool
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
