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
  mutable Bones: Matrix[] voption
}

/// <summary>
/// State for building a Quad3D.
/// </summary>
[<Struct>]
type QuadState = {
  Center: Vector3
  Right: Vector3
  Up: Vector3
  Color: Color
  Uv: UvRect
  Parent: Matrix voption
}

/// <summary>
/// State for building a Billboard3D.
/// </summary>
[<Struct>]
type BillboardState = {
  Position: Vector3
  Size: Vector2
  Rotation: float32
  Color: Color
  Uv: UvRect
  Mode: BillboardMode
  Parent: Matrix voption
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
    Bones = ValueNone
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
        Pass =
          if state.Material.Flags.HasFlag(MaterialFlags.Transparent) then
            Transparent
          else
            Opaque
        Bones = state.Bones
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

  /// <summary>
  /// Sets the Emissive color and intensity.
  /// Useful for glowing objects (used by Bloom).
  /// </summary>
  [<CustomOperation("withEmissive")>]
  member inline _.WithEmissive
    (state: DrawState, color: Color, intensity: float32)
    =
    {
      state with
          Material = state.Material |> Material.withEmissive color intensity
    }

  /// <summary>
  /// Sets the bone matrices for skinned mesh animation.
  /// </summary>
  [<CustomOperation("withBones")>]
  member inline _.WithBones(state: DrawState, bones: Matrix[]) = {
    state with
        Bones = ValueSome bones
  }

// ============================================================================
// QuadBuilder
// ============================================================================

/// <summary>
/// Builder for creating individual 'Quad3D' objects.
/// Used via the 'quad { ... }' computation expression.
/// </summary>
type QuadBuilder() =

  member inline _.Yield(_: unit) : QuadState = {
    Center = Vector3.Zero
    Right = Vector3.UnitX
    Up = Vector3.UnitY
    Color = Color.White
    Uv = UvRect.full
    Parent = ValueNone
  }

  [<CustomOperation("at")>]
  member inline _.At(s: QuadState, pos) = { s with Center = pos }

  [<CustomOperation("onXZ")>]
  member inline _.OnXZ(s: QuadState, size: Vector2) = {
    s with
        Right = Vector3.UnitX * (size.X * 0.5f)
        Up = Vector3.UnitZ * (size.Y * 0.5f)
  }

  [<CustomOperation("onXY")>]
  member inline _.OnXY(s: QuadState, size: Vector2) = {
    s with
        Right = Vector3.UnitX * (size.X * 0.5f)
        Up = Vector3.UnitY * (size.Y * 0.5f)
  }

  [<CustomOperation("color")>]
  member inline _.Color(s: QuadState, c) = { s with Color = c }

  [<CustomOperation("uv")>]
  member inline _.Uv(s: QuadState, u) = { s with Uv = u }

  /// <summary>
  /// Sets a parent transform matrix.
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(s: QuadState, parent: Matrix) = {
    s with
        Parent = ValueSome parent
  }

  member inline _.Run(s: QuadState) : Quad3D =
    match s.Parent with
    | ValueSome p -> {
        Center = Vector3.Transform(s.Center, p)
        Right = Vector3.TransformNormal(s.Right, p)
        Up = Vector3.TransformNormal(s.Up, p)
        Color = s.Color
        Uv = s.Uv
      }
    | ValueNone ->
        {
          Center = s.Center
          Right = s.Right
          Up = s.Up
          Color = s.Color
          Uv = s.Uv
        }

// ============================================================================
// BillboardBuilder
// ============================================================================

/// <summary>
/// Builder for creating individual 'Billboard3D' objects.
/// Used via the 'billboard { ... }' computation expression.
/// </summary>
type BillboardBuilder() =

  member inline _.Yield(_: unit) : BillboardState = {
    Position = Vector3.Zero
    Size = Vector2.One
    Rotation = 0f
    Color = Color.White
    Uv = UvRect.full
    Mode = Spherical
    Parent = ValueNone
  }

  [<CustomOperation("at")>]
  member inline _.At(s: BillboardState, pos) = { s with Position = pos }

  [<CustomOperation("size")>]
  member inline _.Size(s: BillboardState, size) = { s with Size = size }

  [<CustomOperation("rotate")>]
  member inline _.Rotate(s: BillboardState, rot) = { s with Rotation = rot }

  [<CustomOperation("facing")>]
  member inline _.Facing(s: BillboardState, mode) = { s with Mode = mode }

  [<CustomOperation("color")>]
  member inline _.Color(s: BillboardState, c) = { s with Color = c }

  [<CustomOperation("uv")>]
  member inline _.Uv(s: BillboardState, u) = { s with Uv = u }

  /// <summary>
  /// Sets a parent transform matrix.
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(s: BillboardState, parent: Matrix) = {
    s with
        Parent = ValueSome parent
  }

  member inline _.Run(s: BillboardState) : Billboard3D =
    match s.Parent with
    | ValueSome p -> {
        Position = Vector3.Transform(s.Position, p)
        Size = s.Size
        Rotation = s.Rotation
        Color = s.Color
        Uv = s.Uv
        Mode = s.Mode
      }
    | ValueNone ->
        {
          Position = s.Position
          Size = s.Size
          Rotation = s.Rotation
          Color = s.Color
          Uv = s.Uv
          Mode = s.Mode
        }

// ============================================================================
// Sprite3D & Line DSL Helpers
// ============================================================================

module SpriteHelpers =
  /// <summary>Create a quad with sensible defaults (white tint, full UVs).</summary>
  let inline quad3D (center: Vector3) (right: Vector3) (up: Vector3) : Quad3D = {
    Center = center
    Right = right
    Up = up
    Color = Color.White
    Uv = UvRect.full
  }

  /// <summary>Create a quad on the XZ plane (useful for ground decals).</summary>
  let inline quadOnXZ (center: Vector3) (size: Vector2) : Quad3D =
    let right = Vector3(size.X * 0.5f, 0.0f, 0.0f)
    let up = Vector3(0.0f, 0.0f, size.Y * 0.5f)
    quad3D center right up

  /// <summary>Create a quad on the XY plane (useful for in-world UI).</summary>
  let inline quadOnXY (center: Vector3) (size: Vector2) : Quad3D =
    let right = Vector3(size.X * 0.5f, 0.0f, 0.0f)
    let up = Vector3(0.0f, size.Y * 0.5f, 0.0f)
    quad3D center right up

  let inline withQuadColor (color: Color) (q: Quad3D) = { q with Color = color }
  let inline withQuadUv (uv: UvRect) (q: Quad3D) = { q with Uv = uv }

  /// <summary>Create a billboard with sensible defaults (white tint, full UVs, spherical).</summary>
  let inline billboard3D (position: Vector3) (size: Vector2) : Billboard3D = {
    Position = position
    Size = size
    Rotation = 0.0f
    Color = Color.White
    Uv = UvRect.full
    Mode = Spherical
  }

  let inline withBillboardRotation (rotation: float32) (b: Billboard3D) = {
    b with
        Rotation = rotation
  }

  let inline withBillboardColor (color: Color) (b: Billboard3D) = {
    b with
        Color = color
  }

  let inline withBillboardUv (uv: UvRect) (b: Billboard3D) = { b with Uv = uv }

  let inline cylindrical (upAxis: Vector3) (b: Billboard3D) = {
    b with
        Mode = Cylindrical upAxis
  }

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

  // --- Sprite3D / Line Commands ---

  /// <summary>Draw a textured quad using the built-in unlit Sprite3D pipeline.</summary>
  let inline quad
    (texture: Texture2D)
    (quad: Quad3D)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add(
      (),
      DrawSpriteQuad {
        Pass = Opaque
        Texture = texture
        Quad = quad
      }
    )

    buffer

  /// <summary>Draw a textured quad (transparent) using the built-in unlit Sprite3D pipeline.</summary>
  let inline quadTransparent
    (texture: Texture2D)
    (quad: Quad3D)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add(
      (),
      DrawSpriteQuad {
        Pass = Transparent
        Texture = texture
        Quad = quad
      }
    )

    buffer

  /// <summary>Draw a camera-facing billboard using the built-in unlit Sprite3D pipeline.</summary>
  let inline billboard
    (texture: Texture2D)
    (billboard: Billboard3D)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add(
      (),
      DrawSpriteBillboard {
        Pass = Transparent
        Texture = texture
        Billboard = billboard
      }
    )

    buffer

  /// <summary>Draw an opaque billboard using the built-in unlit Sprite3D pipeline.</summary>
  let inline billboardOpaque
    (texture: Texture2D)
    (billboard: Billboard3D)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add(
      (),
      DrawSpriteBillboard {
        Pass = Opaque
        Texture = texture
        Billboard = billboard
      }
    )

    buffer

  /// <summary>Draw a single line segment using the built-in unlit line pipeline.</summary>
  let inline line
    (p1: Vector3)
    (p2: Vector3)
    (color: Color)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), DrawLine(p1, p2, color, Opaque))
    buffer

  /// <summary>Draw multiple line segments using the built-in unlit line pipeline.</summary>
  let inline lines
    (verts: VertexPositionColor[])
    (lineCount: int)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), DrawLines(verts, lineCount, Opaque))
    buffer

  /// <summary>Draw line segments using a custom effect.</summary>
  let inline linesEffect
    (pass: RenderPass)
    (effect: Effect)
    (setup: (Effect -> EffectContext -> unit) voption)
    (verts: VertexPositionColor[])
    (lineCount: int)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.Add((), DrawLinesEffect(verts, lineCount, effect, setup, pass))
    buffer

  /// <summary>Draw multiple billboards using the same texture.</summary>
  let inline billboards
    (texture: Texture2D)
    (billboards: #seq<Billboard3D>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    for b in billboards do
      buffer.Add(
        (),
        DrawSpriteBillboard {
          Pass = Transparent
          Texture = texture
          Billboard = b
        }
      )

    buffer

  /// <summary>Draw multiple textured quads using the same texture.</summary>
  let inline quads
    (texture: Texture2D)
    (quads: #seq<Quad3D>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    for q in quads do
      buffer.Add(
        (),
        DrawSpriteQuad {
          Pass = Opaque
          Texture = texture
          Quad = q
        }
      )

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
    (drawables: #seq<Drawable voption>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    for drawable in drawables do
      drawable |> ValueOption.iter(fun d -> buffer.Add((), Draw d))

    buffer

  /// <summary>
  /// Ends the render command sequence. Currently a no-op used for pipeline readability.
  /// </summary>
  let inline submit(_: RenderBuffer<unit, RenderCommand>) = ()

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
  /// Builder for creating individual 'Quad3D' objects.
  /// Usage: 'quad { at p; onXZ size; ... }'
  /// </summary>
  let quad = QuadBuilder()

  /// <summary>
  /// Builder for creating individual 'Billboard3D' objects.
  /// Usage: 'billboard { at p; size s; ... }'
  /// </summary>
  let billboard = BillboardBuilder()
