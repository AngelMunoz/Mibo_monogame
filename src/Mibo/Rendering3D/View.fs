namespace Mibo.Rendering.Graphics3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open System.Runtime.CompilerServices

// ============================================================================
// DSL State Types
// ============================================================================

/// <summary>
/// Intermediate state used to accumulate properties for a 3D object before it is submitted to the renderer.
/// Usually created via the <see cref="T:Mibo.Rendering.Graphics3D.View.draw"/> computation expression.
/// </summary>
[<Struct>]
type DrawState = {
  /// <summary>The geometric mesh to render.</summary>
  mutable Mesh: Mesh voption
  /// <summary>The world-space or parent-relative position.</summary>
  mutable LocalPosition: Vector3
  /// <summary>The world-space or parent-relative rotation.</summary>
  mutable LocalRotation: Quaternion
  /// <summary>The world-space or parent-relative scale.</summary>
  mutable LocalScale: Vector3
  /// <summary>The PBR material properties and rendering flags.</summary>
  mutable Material: Material
  /// <summary>An optional parent transform matrix to concatenate with the local transform.</summary>
  mutable Parent: Matrix voption
  /// <summary>An optional shader effect to override the default pipeline shader.</summary>
  mutable EffectOverride: Effect voption
  /// <summary>Optional bone matrices for skinned mesh animation.</summary>
  mutable Bones: Matrix[] voption
}

/// <summary>
/// Intermediate state for building a textured quad.
/// Usually created via the <see cref="T:Mibo.Rendering.Graphics3D.View.quad"/> computation expression.
/// </summary>
[<Struct>]
type QuadState = {
  /// <summary>Center position of the quad.</summary>
  Center: Vector3
  /// <summary>The "right" basis vector (half-width).</summary>
  Right: Vector3
  /// <summary>The "up" basis vector (half-height).</summary>
  Up: Vector3
  /// <summary>Vertex color tint (multiplied by texture color).</summary>
  Color: Color
  /// <summary>UV coordinates for the quad.</summary>
  Uv: UvRect
  /// <summary>Optional parent transform matrix.</summary>
  Parent: Matrix voption
}

/// <summary>
/// Intermediate state for building a camera-facing billboard.
/// Usually created via the <see cref="T:Mibo.Rendering.Graphics3D.View.billboard"/> computation expression.
/// </summary>
[<Struct>]
type BillboardState = {
  /// <summary>Position of the billboard center.</summary>
  Position: Vector3
  /// <summary>Width and height of the billboard.</summary>
  Size: Vector2
  /// <summary>Rotation around the facing axis (in radians).</summary>
  Rotation: float32
  /// <summary>Vertex color tint.</summary>
  Color: Color
  /// <summary>UV coordinates for the billboard.</summary>
  Uv: UvRect
  /// <summary>Billboard orientation mode (Spherical or Cylindrical).</summary>
  Mode: BillboardMode
  /// <summary>Optional parent transform matrix.</summary>
  Parent: Matrix voption
}

/// <summary>
/// Internal helpers for managing <see cref="T:Mibo.Rendering.Graphics3D.DrawState"/>.
/// </summary>
module DrawState =
  /// <summary>
  /// The default starting state for a drawable: identity transform, default opaque material, and no mesh.
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

  /// <summary>
  /// Calculates the final world matrix by combining local Scale, Rotation, Position, and the Parent matrix.
  /// </summary>
  /// <param name="state">The draw state to compute the transform for.</param>
  /// <returns>A <see cref="T:Microsoft.Xna.Framework.Matrix"/> representing the full transformation.</returns>
  let computeTransform(state: DrawState) : Matrix =
    let local =
      Matrix.CreateScale(state.LocalScale)
      * Matrix.CreateFromQuaternion(state.LocalRotation)
      * Matrix.CreateTranslation(state.LocalPosition)

    match state.Parent with
    | ValueSome parent -> local * parent
    | ValueNone -> local

  /// <summary>
  /// Converts the accumulation state into a final <see cref="T:Mibo.Rendering.Graphics3D.Drawable"/>.
  /// Returns <see cref="F:Microsoft.FSharp.Core.ValueOption`1.ValueNone"/> if no mesh has been assigned.
  /// </summary>
  /// <param name="state">The draw state to convert.</param>
  /// <returns>A configured Drawable instance or ValueNone.</returns>
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
/// Builder for creating individual <see cref="T:Mibo.Rendering.Graphics3D.Drawable"/> objects.
/// Used via the <see cref="T:Mibo.Rendering.Graphics3D.View.draw"/> computation expression.
/// </summary>
/// <example>
/// <code>
/// draw {
///     mesh myMesh
///     at 0f 2f 0f
///     withAlbedo Color.Gold
///     withMetallic 1.0f
/// }
/// </code>
/// </example>
type DrawableBuilder() =

  member inline _.Yield(_: unit) = DrawState.empty

  /// <summary>Finalizes the accumulation state into an optional Drawable.</summary>
  member inline _.Run(state: DrawState) : Drawable voption =
    DrawState.toDrawable state

  // === Mesh ===

  /// <summary>
  /// Assigns the geometric mesh to the object. Required for rendering.
  /// </summary>
  [<CustomOperation("mesh")>]
  member inline _.Mesh(state: DrawState, mesh: Mesh) = {
    state with
        Mesh = ValueSome mesh
  }

  // === Absolute Positioning ===

  /// <summary>
  /// Sets the local position of the object.
  /// </summary>
  /// <param name="state">Current builder state.</param>
  /// <param name="position">The target position vector.</param>
  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, position: Vector3) = {
    state with
        LocalPosition = position
  }

  /// <summary>
  /// Sets the local position using individual coordinates.
  /// </summary>
  [<CustomOperation("at")>]
  member inline _.At(state: DrawState, x: float32, y: float32, z: float32) = {
    state with
        LocalPosition = Vector3(x, y, z)
  }

  // === Relative Positioning ===

  /// <summary>
  /// Shifts the current local position by an offset vector.
  /// </summary>
  [<CustomOperation("offset")>]
  member inline _.Offset(state: DrawState, offset: Vector3) = {
    state with
        LocalPosition = state.LocalPosition + offset
  }

  /// <summary>
  /// Sets a parent world matrix. The object's local transform will be concatenated with this matrix.
  /// Useful for hierarchical rendering (e.g., equipment attached to a character).
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(state: DrawState, parentTransform: Matrix) = {
    state with
        Parent = ValueSome parentTransform
  }

  // === Rotation ===

  /// <summary>
  /// Multiplies the current local rotation by a quaternion.
  /// </summary>
  [<CustomOperation("rotatedBy")>]
  member inline _.RotatedBy(state: DrawState, rotation: Quaternion) = {
    state with
        LocalRotation = state.LocalRotation * rotation
  }

  /// <summary>
  /// Multiplies the current local rotation using Euler angles (Yaw, Pitch, Roll).
  /// </summary>
  /// <param name="state">Current builder state.</param>
  /// <param name="yaw">Rotation around the Y-axis (radians).</param>
  /// <param name="pitch">Rotation around the X-axis (radians).</param>
  /// <param name="roll">Rotation around the Z-axis (radians).</param>
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
  /// Orientates the object to face a target point in world space.
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
  /// Multiplies the current local scale uniformly on all axes.
  /// </summary>
  [<CustomOperation("scaledBy")>]
  member inline _.ScaledBy(state: DrawState, scale: float32) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  /// <summary>
  /// Multiplies the current local scale using a vector for non-uniform scaling.
  /// </summary>
  [<CustomOperation("scaledByVec")>]
  member inline _.ScaledByVec(state: DrawState, scale: Vector3) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  // === Direct Transform ===

  /// <summary>
  /// Sets the complete world transform, overriding any previous position, rotation, or scale calls.
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
  /// Replaces the entire material with a pre-configured template.
  /// </summary>
  [<CustomOperation("withMaterial")>]
  member inline _.WithMaterial(state: DrawState, material: Material) = {
    state with
        Material = material
  }

  /// <summary>
  /// Sets the Albedo (base diffuse) color tint.
  /// </summary>
  [<CustomOperation("withAlbedo")>]
  member inline _.WithAlbedo(state: DrawState, color: Color) = {
    state with
        Material = state.Material |> Material.withAlbedo color
  }

  /// <summary>
  /// Sets the Albedo (diffuse) texture.
  /// </summary>
  [<CustomOperation("withAlbedoMap")>]
  member inline _.WithAlbedoMap(state: DrawState, texture: Texture2D) = {
    state with
        Material = state.Material |> Material.withAlbedoMap texture
  }

  /// <summary>
  /// Sets the metallic factor (0.0 = Dielectric, 1.0 = Pure Metal).
  /// </summary>
  [<CustomOperation("withMetallic")>]
  member inline _.WithMetallic(state: DrawState, metallic: float32) = {
    state with
        Material = state.Material |> Material.withMetallic metallic
  }

  /// <summary>
  /// Sets the roughness factor (0.0 = Perfectly Smooth, 1.0 = Maximally Rough).
  /// </summary>
  [<CustomOperation("withRoughness")>]
  member inline _.WithRoughness(state: DrawState, roughness: float32) = {
    state with
        Material = state.Material |> Material.withRoughness roughness
  }

  /// <summary>
  /// Sets material rendering flags (e.g. Transparent, DoubleSided, Unlit).
  /// </summary>
  [<CustomOperation("withFlags")>]
  member inline _.WithFlags(state: DrawState, flags: MaterialFlags) = {
    state with
        Material = state.Material |> Material.withFlags flags
  }

  // === Effect Override (Escape Hatch) ===

  /// <summary>
  /// Overrides the automatic PBR shader selection with a specific MonoGame Effect.
  /// Used for custom shaders like Cel-shading, Dissolve, etc.
  /// </summary>
  [<CustomOperation("withEffect")>]
  member inline _.WithEffect(state: DrawState, effect: Effect) = {
    state with
        EffectOverride = ValueSome effect
  }

  /// <summary>
  /// Configures emissive (glow) properties. Required for objects to contribute to the Bloom pass.
  /// </summary>
  /// <param name="state">Current builder state.</param>
  /// <param name="color">The glow color.</param>
  /// <param name="intensity">The brightness multiplier. Values > 1.0 will trigger bloom glow.</param>
  [<CustomOperation("withEmissive")>]
  member inline _.WithEmissive
    (state: DrawState, color: Color, intensity: float32)
    =
    {
      state with
          Material = state.Material |> Material.withEmissive color intensity
    }

  /// <summary>
  /// Sets the bone transformation matrices for skinned mesh animation.
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
/// Builder for creating individual <see cref="T:Mibo.Rendering.Graphics3D.Quad3D"/> objects.
/// Used via the <see cref="T:Mibo.Rendering.Graphics3D.View.quad"/> computation expression.
/// </summary>
/// <example>
/// <code>
/// quad {
///     at (Vector3(0f, 0f, 0f))
///     onXZ (Vector2(2f, 2f))
///     color Color.White
/// }
/// </code>
/// </example>
type QuadBuilder() =

  member inline _.Yield(_: unit) : QuadState = {
    Center = Vector3.Zero
    Right = Vector3.UnitX
    Up = Vector3.UnitY
    Color = Color.White
    Uv = UvRect.full
    Parent = ValueNone
  }

  /// <summary>Sets the center position of the quad.</summary>
  [<CustomOperation("at")>]
  member inline _.At(s: QuadState, pos) = { s with Center = pos }

  /// <summary>Configures the quad geometry to lie on the XZ plane (typical for ground decals).</summary>
  [<CustomOperation("onXZ")>]
  member inline _.OnXZ(s: QuadState, size: Vector2) = {
    s with
        Right = Vector3.UnitX * (size.X * 0.5f)
        Up = Vector3.UnitZ * (size.Y * 0.5f)
  }

  /// <summary>Configures the quad geometry to lie on the XY plane (typical for in-world UI).</summary>
  [<CustomOperation("onXY")>]
  member inline _.OnXY(s: QuadState, size: Vector2) = {
    s with
        Right = Vector3.UnitX * (size.X * 0.5f)
        Up = Vector3.UnitY * (size.Y * 0.5f)
  }

  /// <summary>Sets the vertex color tint.</summary>
  [<CustomOperation("color")>]
  member inline _.Color(s: QuadState, c) = { s with Color = c }

  /// <summary>Sets specific UV coordinates.</summary>
  [<CustomOperation("uv")>]
  member inline _.Uv(s: QuadState, u) = { s with Uv = u }

  /// <summary>
  /// Sets a parent transform matrix. The quad's position and orientation will be transformed by this matrix.
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(s: QuadState, parent: Matrix) = {
    s with
        Parent = ValueSome parent
  }

  /// <summary>Finalizes the quad into a Quad3D object.</summary>
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
/// Builder for creating individual <see cref="T:Mibo.Rendering.Graphics3D.Billboard3D"/> objects.
/// Used via the <see cref="T:Mibo.Rendering.Graphics3D.View.billboard"/> computation expression.
/// </summary>
/// <example>
/// <code>
/// billboard {
///     at (Vector3(0f, 1.5f, 0f))
///     size (Vector2(0.5f, 0.5f))
///     color Color.Yellow
/// }
/// </code>
/// </example>
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

  /// <summary>Sets the world-space or parent-relative position of the billboard.</summary>
  [<CustomOperation("at")>]
  member inline _.At(s: BillboardState, pos) = { s with Position = pos }

  /// <summary>Sets the width and height of the billboard.</summary>
  [<CustomOperation("size")>]
  member inline _.Size(s: BillboardState, size) = { s with Size = size }

  /// <summary>Sets the rotation around the facing axis (radians).</summary>
  [<CustomOperation("rotate")>]
  member inline _.Rotate(s: BillboardState, rot) = { s with Rotation = rot }

  /// <summary>Sets the orientation mode (Spherical for full-facing, Cylindrical for axis-locked).</summary>
  [<CustomOperation("facing")>]
  member inline _.Facing(s: BillboardState, mode) = { s with Mode = mode }

  /// <summary>Sets the vertex color tint.</summary>
  [<CustomOperation("color")>]
  member inline _.Color(s: BillboardState, c) = { s with Color = c }

  /// <summary>Sets specific UV coordinates.</summary>
  [<CustomOperation("uv")>]
  member inline _.Uv(s: BillboardState, u) = { s with Uv = u }

  /// <summary>
  /// Sets a parent transform matrix. The billboard position will be transformed by this matrix.
  /// </summary>
  [<CustomOperation("relativeTo")>]
  member inline _.RelativeTo(s: BillboardState, parent: Matrix) = {
    s with
        Parent = ValueSome parent
  }

  /// <summary>Finalizes the billboard into a Billboard3D object.</summary>
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

/// <summary>
/// Provides low-level functional helpers for creating Sprite3D primitives without using computation expressions.
/// </summary>
module SpriteHelpers =
  /// <summary>
  /// Creates a raw <see cref="T:Mibo.Rendering.Graphics3D.Quad3D"/> definition from basis vectors.
  /// </summary>
  /// <param name="center">World-space center of the quad.</param>
  /// <param name="right">Vector representing half-width orientation.</param>
  /// <param name="up">Vector representing half-height orientation.</param>
  let inline quad3D (center: Vector3) (right: Vector3) (up: Vector3) : Quad3D = {
    Center = center
    Right = right
    Up = up
    Color = Color.White
    Uv = UvRect.full
  }

  /// <summary>
  /// Creates a <see cref="T:Mibo.Rendering.Graphics3D.Quad3D"/> lying flat on the XZ plane.
  /// Ideal for ground decals, floor markers, or flat environmental details.
  /// </summary>
  let inline quadOnXZ (center: Vector3) (size: Vector2) : Quad3D =
    let right = Vector3(size.X * 0.5f, 0.0f, 0.0f)
    let up = Vector3(0.0f, 0.0f, size.Y * 0.5f)
    quad3D center right up

  /// <summary>
  /// Creates a <see cref="T:Mibo.Rendering.Graphics3D.Quad3D"/> standing on the XY plane.
  /// Ideal for in-world signs, nameplates, or simple wall-mounted elements.
  /// </summary>
  let inline quadOnXY (center: Vector3) (size: Vector2) : Quad3D =
    let right = Vector3(size.X * 0.5f, 0.0f, 0.0f)
    let up = Vector3(0.0f, size.Y * 0.5f, 0.0f)
    quad3D center right up

  /// <summary>Applies a multiplicative color tint to the quad's vertices.</summary>
  let inline withQuadColor (color: Color) (q: Quad3D) = { q with Color = color }
  /// <summary>Sets specific UV coordinates for the quad, allowing the use of texture atlases.</summary>
  let inline withQuadUv (uv: UvRect) (q: Quad3D) = { q with Uv = uv }

  /// <summary>
  /// Creates a <see cref="T:Mibo.Rendering.Graphics3D.Billboard3D"/> that will automatically face the active camera.
  /// Common uses include particle effects, glow maps, and distance-scaled icons.
  /// </summary>
  let inline billboard3D (position: Vector3) (size: Vector2) : Billboard3D = {
    Position = position
    Size = size
    Rotation = 0.0f
    Color = Color.White
    Uv = UvRect.full
    Mode = Spherical
  }

  /// <summary>Sets the roll rotation of the billboard around its forward-facing axis.</summary>
  let inline withBillboardRotation (rotation: float32) (b: Billboard3D) = {
    b with
        Rotation = rotation
  }

  /// <summary>Applies a multiplicative color tint to the billboard.</summary>
  let inline withBillboardColor (color: Color) (b: Billboard3D) = {
    b with
        Color = color
  }

  /// <summary>Assigns specific UV coordinates to the billboard.</summary>
  let inline withBillboardUv (uv: UvRect) (b: Billboard3D) = { b with Uv = uv }

  /// <summary>
  /// Configures the billboard to only rotate around a fixed world-space up axis.
  /// Essential for "cylindrical" objects like trees or grass that should face the camera
  /// but remain planted on the ground.
  /// </summary>
  let inline cylindrical (upAxis: Vector3) (b: Billboard3D) = {
    b with
        Mode = Cylindrical upAxis
  }

// ============================================================================
// Render Buffer Fluent Extensions
// ============================================================================

/// <summary>
/// Provides a fluent, discoverable API for submitting 3D rendering commands to a <see cref="T:Mibo.Elmish.RenderBuffer`2"/>.
/// </summary>
/// <remarks>
/// Most methods return the buffer instance to allow for method chaining.
/// These extensions are optimized for the 3D pipeline and handle the transition
/// between opaque and transparent rendering passes automatically.
/// </remarks>
[<Extension>]
type PipelineBufferExtensions =

  /// <summary>
  /// Configures the view and projection matrices for all subsequent draw calls in the current buffer.
  /// </summary>
  /// <remarks>
  /// This command triggers an internal state change. If multiple cameras are needed
  /// (e.g., for a split-screen or a minimap), you should submit a new Camera command
  /// between the respective draw calls.
  /// </remarks>
  /// <example>
  /// <code>
  /// buffer.Camera(myCamera).Draw(player)
  /// </code>
  /// </example>
  [<Extension>]
  static member inline Camera
    (
      this: PipelineBuffer<RenderCommand>,
      camera: Mibo.Rendering.Graphics3D.Camera
    ) =
    this.Add((), SetCamera camera)
    this

  /// <summary>
  /// Sets the global lighting environment, including ambient light and the dynamic light collection.
  /// </summary>
  /// <remarks>
  /// The 3D pipeline uses this state to pack light data into textures for the PBR shader.
  /// Only one lighting state can be active at a time; submitting a new one replaces the previous.
  /// </remarks>
  [<Extension>]
  static member inline Lighting
    (this: PipelineBuffer<RenderCommand>, lighting: LightingState)
    =
    this.Add((), SetLighting lighting)
    this

  /// <summary>
  /// Adds a single dynamic light to the current lighting environment.
  /// </summary>
  /// <remarks>
  /// Unlike 'Lighting', this does not replace existing lights. It appends the new light
  /// to the collection for the current frame. Useful for lights attached to dynamic objects.
  /// </remarks>
  [<Extension>]
  static member inline AddLight
    (this: PipelineBuffer<RenderCommand>, light: Light)
    =
    this.Add((), AddLight light)
    this

  /// <summary>
  /// Restricts rendering to a specific sub-region of the screen.
  /// </summary>
  /// <remarks>
  /// Use this for UI overlays, minimaps, or local viewport effects.
  /// The viewport stays active until another Viewport command is submitted.
  /// </remarks>
  [<Extension>]
  static member inline Viewport
    (this: PipelineBuffer<RenderCommand>, viewport: Viewport)
    =
    this.Add((), SetViewport viewport)
    this

  /// <summary>
  /// Clears the color and depth buffers of the current render target.
  /// </summary>
  /// <param name="this">The render buffer.</param>
  /// <param name="color">The background color to fill the target with.</param>
  [<Extension>]
  static member inline Clear
    (this: PipelineBuffer<RenderCommand>, color: Color)
    =
    this.Add((), ClearTarget(ValueSome color, true))
    this

  /// <summary>
  /// Performs a targeted clear operation on the current render target.
  /// </summary>
  /// <param name="this">The render buffer.</param>
  /// <param name="color">The background color. If black is desired, pass <c>Color.Black</c>.</param>
  /// <param name="clearDepth">If true, the Z-buffer is reset to 1.0, allowing new depth tests to pass.</param>
  [<Extension>]
  static member inline ClearTarget
    (this: PipelineBuffer<RenderCommand>, color: Color, clearDepth: bool)
    =
    this.Add((), ClearTarget(ValueSome color, clearDepth))
    this

  /// <summary>
  /// Resets the depth buffer without affecting the color data.
  /// </summary>
  /// <remarks>
  /// Essential when drawing transparent overlays or "always-on-top" objects (like silhouettes)
  /// that should not be occluded by world geometry.
  /// </remarks>
  [<Extension>]
  static member inline ClearDepth(this: PipelineBuffer<RenderCommand>) =
    this.Add((), ClearTarget(ValueNone, true))
    this

  /// <summary>
  /// Injects a custom rendering callback for low-level GraphicsDevice access.
  /// </summary>
  /// <remarks>
  /// This is an "escape hatch" for operations not supported by the DSL,
  /// such as dispatching compute shaders or drawing custom vertex buffers.
  /// </remarks>
  [<Extension>]
  static member inline Custom
    (
      this: PipelineBuffer<RenderCommand>,
      drawFn: GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit
    ) =
    this.Add((), DrawCustom drawFn)
    this

  /// <summary>
  /// Submits a textured quad to the Opaque rendering pass.
  /// </summary>
  /// <remarks>
  /// Opaque quads are rendered with depth-write enabled and do not support semi-transparency.
  /// Ideal for floors, walls, and solid world geometry.
  /// </remarks>
  [<Extension>]
  static member inline Quad
    (this: PipelineBuffer<RenderCommand>, texture: Texture2D, quad: Quad3D)
    =
    this.Add(
      (),
      DrawSpriteQuad {
        Pass = Opaque
        Texture = texture
        Quad = quad
      }
    )

    this

  /// <summary>
  /// Submits a textured quad to the Transparent rendering pass.
  /// </summary>
  /// <remarks>
  /// Transparent quads are automatically sorted back-to-front by the pipeline
  /// to ensure correct alpha blending. Use this for decals, markers, and UI elements.
  /// </remarks>
  [<Extension>]
  static member inline QuadTransparent
    (this: PipelineBuffer<RenderCommand>, texture: Texture2D, quad: Quad3D)
    =
    this.Add(
      (),
      DrawSpriteQuad {
        Pass = Transparent
        Texture = texture
        Quad = quad
      }
    )

    this

  /// <summary>
  /// Submits a camera-facing billboard to the Transparent pass.
  /// </summary>
  /// <remarks>
  /// Useful for particles, lens flares, and foliage. The pipeline handles
  /// the orientation math based on the active camera at render time.
  /// </remarks>
  [<Extension>]
  static member inline Billboard
    (
      this: PipelineBuffer<RenderCommand>,
      texture: Texture2D,
      billboard: Billboard3D
    ) =
    this.Add(
      (),
      DrawSpriteBillboard {
        Pass = Transparent
        Texture = texture
        Billboard = billboard
      }
    )

    this

  /// <summary>
  /// Submits a camera-facing billboard to the Opaque pass.
  /// </summary>
  /// <remarks>
  /// Rarely used, but effective for high-performance impostors that do not require alpha blending.
  /// </remarks>
  [<Extension>]
  static member inline BillboardOpaque
    (
      this: PipelineBuffer<RenderCommand>,
      texture: Texture2D,
      billboard: Billboard3D
    ) =
    this.Add(
      (),
      DrawSpriteBillboard {
        Pass = Opaque
        Texture = texture
        Billboard = billboard
      }
    )

    this

  /// <summary>
  /// Submits a single unlit line segment.
  /// </summary>
  /// <param name="this">The render buffer.</param>
  /// <param name="p1">Starting point in world space.</param>
  /// <param name="p2">Ending point in world space.</param>
  /// <param name="color">The color of the line.</param>
  [<Extension>]
  static member inline Line
    (this: PipelineBuffer<RenderCommand>, p1: Vector3, p2: Vector3, color: Color) =
    this.Add((), DrawLine(p1, p2, color, Opaque))
    this

  /// <summary>
  /// Submits a batch of line segments for efficient rendering.
  /// </summary>
  /// <param name="this">The render buffer.</param>
  /// <param name="verts">Interleaved start/end vertices.</param>
  /// <param name="lineCount">Number of segments (verts.Length / 2).</param>
  [<Extension>]
  static member inline Lines
    (
      this: PipelineBuffer<RenderCommand>,
      verts: VertexPositionColor[],
      lineCount: int
    ) =
    this.Add((), DrawLines(verts, lineCount, Opaque))
    this

  /// <summary>
  /// Submits a batch of lines to be rendered with a custom shader effect.
  /// </summary>
  /// <remarks>
  /// Used for advanced effects like distance-faded grids or glowing neon paths.
  /// </remarks>
  [<Extension>]
  static member inline LinesEffect
    (
      this: PipelineBuffer<RenderCommand>,
      pass: RenderPass,
      effect: Effect,
      setup: (Effect -> EffectContext -> unit) voption,
      verts: VertexPositionColor[],
      lineCount: int
    ) =
    this.Add((), DrawLinesEffect(verts, lineCount, effect, setup, pass))
    this

  /// <summary>
  /// Adds a pre-configured drawable to the pipeline.
  /// </summary>
  /// <remarks>
  /// This is the standard way to submit meshes. The pipeline will automatically
  /// assign the drawable to the Opaque or Transparent pass based on its material flags.
  /// </remarks>
  /// <param name="this">The render buffer.</param>
  /// <param name="drawable">The object to render. Passing <c>ValueNone</c> is a safe no-op.</param>
  [<Extension>]
  static member inline Draw
    (this: PipelineBuffer<RenderCommand>, drawable: Drawable voption)
    =
    drawable |> ValueOption.iter(fun d -> this.Add((), Draw d))
    this

  /// <summary>
  /// Efficiently submits a collection of drawables.
  /// </summary>
  /// <remarks>
  /// Ideal for rendering lists of entities or particles. Skips all <c>ValueNone</c> entries.
  /// </remarks>
  [<Extension>]
  static member inline DrawMany
    (this: PipelineBuffer<RenderCommand>, drawables: #seq<Drawable voption>)
    =
    for drawable in drawables do
      drawable |> ValueOption.iter(fun d -> this.Add((), Draw d))

    this

  /// <summary>
  /// Terminates the fluent command chain.
  /// </summary>
  /// <remarks>
  /// While this method is currently a no-op, using it is recommended for
  /// visual clarity and to signal the completion of a frame's command submission.
  /// </remarks>
  [<Extension>]
  static member inline Submit(this: PipelineBuffer<RenderCommand>) = ()

/// <summary>
/// Module for building a 3D frame by submitting commands to a RenderBuffer.
/// Recommended usage: 'buffer |> Buffer.camera ... |> Buffer.draw ...'
/// </summary>
module Buffer =

  let inline camera camera (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Camera(camera)

  let inline lighting lighting (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Lighting(lighting)

  let inline addLight light (buffer: PipelineBuffer<RenderCommand>) =
    buffer.AddLight(light)

  let inline viewport viewport (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Viewport(viewport)

  let inline clear color (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Clear(color)

  let inline clearTarget
    color
    clearDepth
    (buffer: PipelineBuffer<RenderCommand>)
    =
    buffer.ClearTarget(color, clearDepth)

  let inline clearDepth(buffer: PipelineBuffer<RenderCommand>) =
    buffer.ClearDepth()

  let inline custom drawFn (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Custom(drawFn)

  let inline quad tex q (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Quad(tex, q)

  let inline quadTransparent tex q (buffer: PipelineBuffer<RenderCommand>) =
    buffer.QuadTransparent(tex, q)

  let inline billboard tex b (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Billboard(tex, b)

  let inline billboardOpaque tex b (buffer: PipelineBuffer<RenderCommand>) =
    buffer.BillboardOpaque(tex, b)

  let inline line p1 p2 col (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Line(p1, p2, col)

  let inline lines verts count (buffer: PipelineBuffer<RenderCommand>) =
    buffer.Lines(verts, count)

  let inline linesEffect
    pass
    effect
    setup
    verts
    count
    (buffer: PipelineBuffer<RenderCommand>)
    =
    buffer.LinesEffect(pass, effect, setup, verts, count)

  let inline billboards tex bs (buffer: PipelineBuffer<RenderCommand>) =
    for b in bs do
      buffer.Billboard(tex, b) |> ignore

    buffer

  let inline quads tex qs (buffer: PipelineBuffer<RenderCommand>) =
    for q in qs do
      buffer.Quad(tex, q) |> ignore

    buffer

  let inline draw d (buffer: PipelineBuffer<RenderCommand>) = buffer.Draw(d)

  let inline drawMany ds (buffer: PipelineBuffer<RenderCommand>) =
    buffer.DrawMany(ds)

  let inline submit(buffer: PipelineBuffer<RenderCommand>) = buffer.Submit()

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
