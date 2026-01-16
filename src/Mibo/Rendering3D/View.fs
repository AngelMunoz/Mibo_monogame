namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// DSL State Types
// ============================================================================

/// Mutable state for building a Drawable
[<Struct>]
type DrawState = {
  mutable Mesh: Mesh voption
  mutable LocalPosition: Vector3
  mutable LocalRotation: Quaternion
  mutable LocalScale: Vector3
  mutable Material: Material
  mutable Parent: Matrix voption
  mutable EffectOverride: Effect voption
}

module DrawState =
  let empty: DrawState = {
    Mesh = ValueNone
    LocalPosition = Vector3.Zero
    LocalRotation = Quaternion.Identity
    LocalScale = Vector3.One
    Material = Material.defaultOpaque
    Parent = ValueNone
    EffectOverride = ValueNone
  }

  /// Compute final world transform from local components + parent
  let computeTransform(state: DrawState) : Matrix =
    let local =
      Matrix.CreateScale(state.LocalScale)
      * Matrix.CreateFromQuaternion(state.LocalRotation)
      * Matrix.CreateTranslation(state.LocalPosition)

    match state.Parent with
    | ValueSome parent -> local * parent
    | ValueNone -> local

  /// Convert state to Drawable (returns ValueNone if no mesh)
  let toDrawable(state: DrawState) : Drawable voption =
    match state.Mesh with
    | ValueSome mesh ->
      let transform = computeTransform state

      ValueSome {
        Mesh = mesh
        Transform = transform
        Material = state.Material
        BoundingSphere = mesh.BoundingSphere.Transform(transform)
        EffectOverride = state.EffectOverride
      }
    | ValueNone -> ValueNone

// ============================================================================
// DrawableBuilder - Standalone CE that returns Drawable voption
// ============================================================================

type DrawableBuilder() =

  member inline _.Yield(_: unit) = DrawState.empty

  /// Run returns Drawable voption - caller handles adding to buffer
  member inline _.Run(state: DrawState) : Drawable voption =
    DrawState.toDrawable state

  // === Mesh ===

  [<CustomOperation("mesh")>]
  member inline _.Mesh(state: DrawState, mesh: Mesh) = {
    state with
        Mesh = ValueSome mesh
  }

  // === Absolute Positioning ===

  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, position: Vector3) = {
    state with
        LocalPosition = position
  }

  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, x: float32, y: float32, z: float32) = {
    state with
        LocalPosition = Vector3(x, y, z)
  }

  // === Relative Positioning ===

  [<CustomOperation("offset")>]
  member inline _.Offset(state: DrawState, offset: Vector3) = {
    state with
        LocalPosition = state.LocalPosition + offset
  }

  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(state: DrawState, parentTransform: Matrix) = {
    state with
        Parent = ValueSome parentTransform
  }

  // === Rotation ===

  [<CustomOperation("rotatedBy")>]
  member inline _.RotatedBy(state: DrawState, rotation: Quaternion) = {
    state with
        LocalRotation = state.LocalRotation * rotation
  }

  [<CustomOperation("rotatedByYawPitchRoll")>]
  member inline _.RotatedByYawPitchRoll
    (state: DrawState, yaw: float32, pitch: float32, roll: float32)
    =
    let rot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll)

    {
      state with
          LocalRotation = state.LocalRotation * rot
    }

  [<CustomOperation("lookAt")>]
  member inline _.LookAt(state: DrawState, target: Vector3) =
    // Guard against zero-distance to prevent NaN from Normalize
    if Vector3.DistanceSquared(target, state.LocalPosition) < 0.0001f then
      state
    else
      let forward = Vector3.Normalize(target - state.LocalPosition)

      let up =
        if abs(Vector3.Dot(forward, Vector3.Up)) > 0.999f then
          Vector3.Forward
        else
          Vector3.Up

      let rot =
        Quaternion.CreateFromRotationMatrix(
          Matrix.CreateWorld(Vector3.Zero, forward, up)
        )

      { state with LocalRotation = rot }

  // === Scale ===

  [<CustomOperation("scaledBy")>]
  member inline _.ScaledBy(state: DrawState, scale: float32) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  [<CustomOperation("scaledByVec")>]
  member inline _.ScaledByVec(state: DrawState, scale: Vector3) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  // === Direct Transform ===

  [<CustomOperation("withTransform")>]
  member inline _.WithTransform(state: DrawState, transform: Matrix) = {
    state with
        LocalPosition = Vector3.Zero
        LocalRotation = Quaternion.Identity
        LocalScale = Vector3.One
        Parent = ValueSome transform
  }

  // === Material ===

  [<CustomOperation("withMaterial")>]
  member inline _.WithMaterial(state: DrawState, material: Material) = {
    state with
        Material = material
  }

  [<CustomOperation("withAlbedo")>]
  member inline _.WithAlbedo(state: DrawState, color: Color) = {
    state with
        Material = state.Material |> Material.withAlbedo color
  }

  [<CustomOperation("withAlbedoMap")>]
  member inline _.WithAlbedoMap(state: DrawState, texture: Texture2D) = {
    state with
        Material = state.Material |> Material.withAlbedoMap texture
  }

  [<CustomOperation("withMetallic")>]
  member inline _.WithMetallic(state: DrawState, metallic: float32) = {
    state with
        Material = state.Material |> Material.withMetallic metallic
  }

  [<CustomOperation("withRoughness")>]
  member inline _.WithRoughness(state: DrawState, roughness: float32) = {
    state with
        Material = state.Material |> Material.withRoughness roughness
  }

  [<CustomOperation("withFlags")>]
  member inline _.WithFlags(state: DrawState, flags: MaterialFlags) = {
    state with
        Material = state.Material |> Material.withFlags flags
  }

  // === Effect Override (Escape Hatch) ===

  [<CustomOperation("withEffect")>]
  member inline _.WithEffect(state: DrawState, effect: Effect) = {
    state with
        EffectOverride = ValueSome effect
  }

// ============================================================================
// RenderBuilder - Scene-level CE
// ============================================================================

type RenderBuilder(_buffer: RenderBuffer<unit, RenderCommand>) =

  member val buffer = _buffer

  member inline _.Yield(_: unit) = ()

  /// Yield for Drawable voption - handles bare `draw { }` expressions
  member inline this.Yield(drawable: Drawable voption) =
    drawable |> ValueOption.iter(fun d -> this.buffer.Add((), Draw d))

  member inline _.Zero() = ()
  member inline _.Delay([<InlineIfLambda>] f: unit -> unit) = f
  member inline _.Run(_) = ()

  member inline _.For(source: 'T seq, [<InlineIfLambda>] body: 'T -> unit) =
    for item in source do
      body item

  member inline this.For
    (source: 'T seq, [<InlineIfLambda>] body: 'T -> Drawable voption)
    =
    for item in source do
      body item |> ValueOption.iter(fun d -> this.buffer.Add((), Draw d))

  member inline this.Combine(state, draw: Drawable voption) =
    draw |> ValueOption.iter(fun d -> this.buffer.Add((), Draw d))

  member inline this.Combine(state, draw: unit -> unit) = draw()

  // === Camera ===

  [<CustomOperation("withCamera")>]
  member inline this.WithCamera
    (state, camera: Mibo.Rendering.Graphics3D.Camera)
    =
    this.buffer.Add((), SetCamera camera)
    state

  // === Lighting ===

  [<CustomOperation("withLighting")>]
  member inline this.WithLighting(state, lighting: LightingState) =
    this.buffer.Add((), SetLighting lighting)
    state

  // === Viewport ===

  [<CustomOperation("withViewport")>]
  member inline this.WithViewport(state, viewport: Viewport) =
    this.buffer.Add((), SetViewport viewport)
    state

  // === Mode ===

  [<CustomOperation("withMode")>]
  member inline this.WithMode(state, mode: PipelineMode) =
    this.buffer.Add((), SetMode mode)
    state

  // === Clear ===

  [<CustomOperation("clear")>]
  member inline this.Clear(state, color: Color) =
    this.buffer.Add((), ClearTarget(ValueSome color, true))
    state

  [<CustomOperation("clearTarget")>]
  member inline this.ClearTarget(state, color: Color, clearDepth: bool) =
    this.buffer.Add((), ClearTarget(ValueSome color, clearDepth))
    state

  [<CustomOperation("clearDepth")>]
  member inline this.ClearDepth(state) =
    this.buffer.Add((), ClearTarget(ValueNone, true))
    state

  // === Custom ===

  [<CustomOperation("custom")>]
  member inline this.Custom
    (
      state,
      [<InlineIfLambda>] drawFn:
        GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit
    ) =
    this.buffer.Add((), DrawCustom drawFn)
    state

module RenderBuilder =

  let inline camera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetCamera camera)
    buffer

  let inline lighting
    (lighting: LightingState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetLighting lighting)
    buffer

  let inline viewport
    (viewport: Viewport)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetViewport viewport)
    buffer

  let inline mode
    (mode: PipelineMode)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetMode mode)
    buffer

  let inline clear (color: Color) (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Add((), ClearTarget(ValueSome color, true))
    buffer

  let inline clearTarget
    (color: Color)
    (clearDepth: bool)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), ClearTarget(ValueSome color, clearDepth))
    buffer

  let inline clearDepth(buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Add((), ClearTarget(ValueNone, true))
    buffer

  let inline custom
    ([<InlineIfLambda>] drawFn:
      GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), DrawCustom drawFn)
    buffer

  let inline draw
    (drawable: Drawable voption)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    drawable |> ValueOption.iter(fun d -> buffer.Add((), Draw d))
    buffer

  let inline drawMany
    (drawables: seq<Drawable voption>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    for drawable in drawables do
      drawable |> ValueOption.iter(fun d -> buffer.Add((), Draw d))

    buffer

  let inline submit(buffer: RenderBuffer<unit, RenderCommand>) = ()

// ============================================================================
// Module API
// ============================================================================

[<AutoOpen>]
module View =
  /// Standalone draw builder - returns Drawable voption
  let draw = DrawableBuilder()

  /// Create a RenderBuilder for the given buffer
  let render(buffer: RenderBuffer<unit, RenderCommand>) = RenderBuilder(buffer)
