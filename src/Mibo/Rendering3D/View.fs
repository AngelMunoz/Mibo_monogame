namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// DSL State Types
// ============================================================================

/// <summary>
/// Mutable state for building a Drawable.
/// Accumulates transform, material, and mesh data before final creation.
/// </summary>
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
  /// <summary>
  /// Empty draw state with default values (no mesh, origin position, identity rotation, unit scale).
  /// Starting point for building a drawable via the `draw {}` CE.
  /// </summary>
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

/// <summary>
/// Builder for creating individual 'Drawable' objects.
/// Used via the 'draw { ... }' computation expression.
/// </summary>
type DrawableBuilder() =

  member inline _.Yield(_: unit) = DrawState.empty

  /// Run returns Drawable voption - caller handles adding to buffer
  member inline _.Run(state: DrawState) : Drawable voption =
    DrawState.toDrawable state

  // === Mesh ===

  /// <summary>
  /// Sets the mesh to be rendered.
  /// </summary>
  [<CustomOperation("mesh")>]
  member inline _.Mesh(state: DrawState, mesh: Mesh) = {
    state with
        Mesh = ValueSome mesh
  }

  // === Absolute Positioning ===

  /// <summary>
  /// Sets the absolute local position.
  /// </summary>
  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, position: Vector3) = {
    state with
        LocalPosition = position
  }

  /// <summary>
  /// Sets the absolute local position using individual components.
  /// </summary>
  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, x: float32, y: float32, z: float32) = {
    state with
        LocalPosition = Vector3(x, y, z)
  }

  // === Relative Positioning ===

  /// <summary>
  /// Adds an offset to the current local position.
  /// Useful for chaining movements.
  /// </summary>
  [<CustomOperation("offset")>]
  member inline _.Offset(state: DrawState, offset: Vector3) = {
    state with
        LocalPosition = state.LocalPosition + offset
  }

  /// <summary>
  /// Sets a parent transform matrix.
  /// The local transform will be multiplied by this parent.
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(state: DrawState, parentTransform: Matrix) = {
    state with
        Parent = ValueSome parentTransform
  }

  // === Rotation ===

  /// <summary>
  /// Rotates the object by a specific quaternion (accumulative).
  /// </summary>
  [<CustomOperation("rotatedBy")>]
  member inline _.RotatedBy(state: DrawState, rotation: Quaternion) = {
    state with
        LocalRotation = state.LocalRotation * rotation
  }

  /// <summary>
  /// Rotates the object using Yaw (Y), Pitch (X), and Roll (Z) angles in radians.
  /// </summary>
  [<CustomOperation("rotatedByYawPitchRoll")>]
  member inline _.RotatedByYawPitchRoll
    (state: DrawState, yaw: float32, pitch: float32, roll: float32)
    =
    let rot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll)

    {
      state with
          LocalRotation = state.LocalRotation * rot
    }

  /// <summary>
  /// Rotates the object to face a specific target point in world space.
  /// </summary>
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

  /// <summary>
  /// Applies a uniform scale factor to all axes equally.
  /// </summary>
  /// <param name="scale">Scale multiplier (1.0 = no change, 2.0 = double size).</param>
  [<CustomOperation("scaledBy")>]
  member inline _.ScaledBy(state: DrawState, scale: float32) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  /// <summary>
  /// Applies a non-uniform scale vector (X, Y, Z).
  /// Different scaling per axis can stretch or squash geometry.
  /// Useful for creating flattened objects or stylized effects.
  /// </summary>
  /// <param name="scale">Per-axis scale vector.</param>
  [<CustomOperation("scaledByVec")>]
  member inline _.ScaledByVec(state: DrawState, scale: Vector3) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  // === Direct Transform ===

  /// <summary>
  /// Sets the transform directly, overriding position, rotation, and scale.
  /// Useful when you have a pre-computed world matrix.
  /// </summary>
  [<CustomOperation("withTransform")>]
  member inline _.WithTransform(state: DrawState, transform: Matrix) = {
    state with
        LocalPosition = Vector3.Zero
        LocalRotation = Quaternion.Identity
        LocalScale = Vector3.One
        Parent = ValueSome transform
  }

  // === Material ===

  /// <summary>
  /// Sets the complete material for this object.
  /// </summary>
  [<CustomOperation("withMaterial")>]
  member inline _.WithMaterial(state: DrawState, material: Material) = {
    state with
        Material = material
  }

  /// <summary>
  /// Sets the Albedo (Diffuse) color tint.
  /// </summary>
  [<CustomOperation("withAlbedo")>]
  member inline _.WithAlbedo(state: DrawState, color: Color) = {
    state with
        Material = state.Material |> Material.withAlbedo color
  }

  /// <summary>
  /// Sets the Albedo (Diffuse) texture map.
  /// </summary>
  [<CustomOperation("withAlbedoMap")>]
  member inline _.WithAlbedoMap(state: DrawState, texture: Texture2D) = {
    state with
        Material = state.Material |> Material.withAlbedoMap texture
  }

  /// <summary>
  /// Sets the Metallic property (0.0 = Dielectric/Plastic, 1.0 = Metal).
  /// </summary>
  [<CustomOperation("withMetallic")>]
  member inline _.WithMetallic(state: DrawState, metallic: float32) = {
    state with
        Material = state.Material |> Material.withMetallic metallic
  }

  /// <summary>
  /// Sets the Roughness property (0.0 = Smooth/Glossy, 1.0 = Rough/Matte).
  /// </summary>
  [<CustomOperation("withRoughness")>]
  member inline _.WithRoughness(state: DrawState, roughness: float32) = {
    state with
        Material = state.Material |> Material.withRoughness roughness
  }

  /// <summary>
  /// Sets material flags (e.g., Transparent, DoubleSided, CastsShadow).
  /// </summary>
  [<CustomOperation("withFlags")>]
  member inline _.WithFlags(state: DrawState, flags: MaterialFlags) = {
    state with
        Material = state.Material |> Material.withFlags flags
  }

  // === Effect Override (Escape Hatch) ===

  /// <summary>
  /// Overrides the standard pipeline shader with a custom MonoGame Effect.
  /// Use this for special effects like Toon Shading or Dissolve.
  /// </summary>
  [<CustomOperation("withEffect")>]
  member inline _.WithEffect(state: DrawState, effect: Effect) = {
    state with
        EffectOverride = ValueSome effect
  }

// ============================================================================
// RenderBuilder - Scene-level API
// ============================================================================

/// <summary>
/// Internal builder for submitting commands to a 'RenderBuffer'.
/// While this supports a Computation Expression, the preferred usage is via the 'RenderBuilder' module functions.
/// </summary>
type RenderBuilder(_buffer: RenderBuffer<unit, RenderCommand>) =

  member val buffer = _buffer

  member inline _.Yield(_: unit) = ()

  /// Yield for Drawable voption - handles bare `draw { }` expressions
  member inline this.Yield(drawable: Drawable voption) =
    drawable |> ValueOption.iter(fun d -> this.buffer.Add((), Draw d))

  member inline _.Zero() = ()
  member inline _.Delay([<InlineIfLambda>] f: unit -> unit) = f
  member inline _.Run(f: unit -> unit) = f()

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

  /// <summary>
  /// Sets the camera for subsequent draw calls.
  /// </summary>
  [<CustomOperation("withCamera")>]
  member inline this.WithCamera
    (state, camera: Mibo.Rendering.Graphics3D.Camera)
    =
    this.buffer.Add((), SetCamera camera)
    state

  // === Lighting ===

  /// <summary>
  /// Sets the scene lighting configuration.
  /// </summary>
  [<CustomOperation("withLighting")>]
  member inline this.WithLighting(state, lighting: LightingState) =
    this.buffer.Add((), SetLighting lighting)
    state

  // === Viewport ===

  /// <summary>
  /// Sets the viewport for subsequent draw calls.
  /// Useful for split-screen or minimaps.
  /// </summary>
  [<CustomOperation("withViewport")>]
  member inline this.WithViewport(state, viewport: Viewport) =
    this.buffer.Add((), SetViewport viewport)
    state

  // === Clear ===

  /// <summary>
  /// Clears both Color and Depth buffers.
  /// </summary>
  [<CustomOperation("clear")>]
  member inline this.Clear(state, color: Color) =
    this.buffer.Add((), ClearTarget(ValueSome color, true))
    state

  /// <summary>
  /// Clears targets with explicit control over Color and Depth.
  /// </summary>
  [<CustomOperation("clearTarget")>]
  member inline this.ClearTarget(state, color: Color, clearDepth: bool) =
    this.buffer.Add((), ClearTarget(ValueSome color, clearDepth))
    state

  /// <summary>
  /// Clears only the Depth buffer.
  /// </summary>
  [<CustomOperation("clearDepth")>]
  member inline this.ClearDepth(state) =
    this.buffer.Add((), ClearTarget(ValueNone, true))
    state

  // === Custom ===

  /// <summary>
  /// Submits a custom callback for arbitrary GraphicsDevice operations.
  /// Note: Breaks batching; use sparingly.
  /// </summary>
  [<CustomOperation("custom")>]
  member inline this.Custom
    (
      state,
      [<InlineIfLambda>] drawFn:
        GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit
    ) =
    this.buffer.Add((), DrawCustom drawFn)
    state

/// <summary>
/// Module for building a 3D frame by submitting commands to a RenderBuffer.
/// Recommended usage: 'buffer |> RenderBuilder.camera ... |> RenderBuilder.draw ...'
/// </summary>
module RenderBuilder =

  /// <summary>
  /// Submits a command to set the current camera.
  /// </summary>
  let inline camera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetCamera camera)
    buffer

  /// <summary>
  /// Submits a command to set the scene lighting.
  /// </summary>
  let inline lighting
    (lighting: LightingState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetLighting lighting)
    buffer

  /// <summary>
  /// Submits a command to set the rendering viewport.
  /// </summary>
  let inline viewport
    (viewport: Viewport)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), SetViewport viewport)
    buffer

  /// <summary>
  /// Submits a command to clear the target (Color + Depth).
  /// </summary>
  let inline clear (color: Color) (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Add((), ClearTarget(ValueSome color, true))
    buffer

  /// <summary>
  /// Submits a command to clear the target with explicit options.
  /// </summary>
  let inline clearTarget
    (color: Color)
    (clearDepth: bool)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), ClearTarget(ValueSome color, clearDepth))
    buffer

  /// <summary>
  /// Submits a command to clear only the depth buffer.
  /// </summary>
  let inline clearDepth(buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Add((), ClearTarget(ValueNone, true))
    buffer

  /// <summary>
  /// Submits a custom drawing function.
  /// </summary>
  let inline custom
    ([<InlineIfLambda>] drawFn:
      GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), DrawCustom drawFn)
    buffer

  /// <summary>
  /// Submits a single drawable to the buffer.
  /// </summary>
  let inline draw
    (drawable: Drawable voption)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    drawable |> ValueOption.iter(fun d -> buffer.Add((), Draw d))
    buffer

  /// <summary>
  /// Submits a sequence of drawables to the buffer.
  /// </summary>
  let inline drawMany
    (drawables: seq<Drawable voption>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    for drawable in drawables do
      drawable |> ValueOption.iter(fun d -> buffer.Add((), Draw d))

    buffer

  /// <summary>
  /// Ends the render command sequence. Currently a no-op used for pipeline readability.
  /// </summary>
  let inline submit(buffer: RenderBuffer<unit, RenderCommand>) = ()

// ============================================================================
// Module API
// ============================================================================

[<AutoOpen>]
module View =
  /// <summary>
  /// Builder for creating individual 'Drawable' objects (Mesh + Transform + Material).
  /// Usage: 'draw { mesh m; at p; ... }'
  /// </summary>
  let draw = DrawableBuilder()

  /// <summary>
  /// INTERNAL: CE builder for the RenderBuffer. 
  /// Preferred usage is via the 'RenderBuilder' module functions.
  /// </summary>
  let render(buffer: RenderBuffer<unit, RenderCommand>) = RenderBuilder(buffer)
