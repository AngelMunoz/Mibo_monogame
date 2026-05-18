namespace Mibo.Rendering.Graphics3D.V2

open System.Runtime.CompilerServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering

[<Struct>]
type DrawState = {
  Mesh: Mibo.Rendering.Graphics3D.Mesh voption
  LocalPosition: Vector3
  LocalRotation: Quaternion
  LocalScale: Vector3
  Binding: EffectBinding voption
  MaterialData: PBRMaterialData voption
  Parent: Matrix voption
  Bones: Matrix[] voption
  Pass: Mibo.Rendering.Graphics3D.RenderPass
}

module DrawState =

  let empty: DrawState = {
    Mesh = ValueNone
    LocalPosition = Vector3.Zero
    LocalRotation = Quaternion.Identity
    LocalScale = Vector3.One
    Binding = ValueNone
    MaterialData = ValueNone
    Parent = ValueNone
    Bones = ValueNone
    Pass = Mibo.Rendering.Graphics3D.Opaque
  }

  let computeTransform(state: DrawState) : Matrix =
    let local =
      Matrix.CreateScale(state.LocalScale)
      * Matrix.CreateFromQuaternion(state.LocalRotation)
      * Matrix.CreateTranslation(state.LocalPosition)

    match state.Parent with
    | ValueSome parent -> local * parent
    | ValueNone -> local

  let toDrawable
    (state: DrawState)
    : Mibo.Rendering.Graphics3D.V2.Drawable voption =
    match state.Mesh, state.Binding with
    | ValueSome mesh, ValueSome binding ->
      let transform = computeTransform state

      ValueSome {
        Mesh = mesh
        Transform = transform
        Bones = state.Bones
        BoundingSphere = mesh.BoundingSphere.Transform(transform)
        Pass = state.Pass
        MaterialKey = binding.MaterialKey
        MaterialData = state.MaterialData
        Binding = binding
      }
    | _ -> ValueNone

[<Sealed>]
type DrawableBuilder() =

  member _.Yield() = DrawState.empty

  member _.Run
    (state: DrawState)
    : Mibo.Rendering.Graphics3D.V2.Drawable voption =
    DrawState.toDrawable state

  [<CustomOperation("mesh")>]
  member _.Mesh(state: DrawState, mesh: Mibo.Rendering.Graphics3D.Mesh) = {
    state with
        Mesh = ValueSome mesh
  }

  [<CustomOperation("at")>]
  member _.At(state: DrawState, position: Vector3) = {
    state with
        LocalPosition = position
  }

  [<CustomOperation("at")>]
  member _.At(state: DrawState, x: float32, y: float32, z: float32) = {
    state with
        LocalPosition = Vector3(x, y, z)
  }

  [<CustomOperation("offset")>]
  member _.Offset(state: DrawState, offset: Vector3) = {
    state with
        LocalPosition = state.LocalPosition + offset
  }

  [<CustomOperation("relativeTo")>]
  member _.RelativeTo(state: DrawState, parentTransform: Matrix) = {
    state with
        Parent = ValueSome parentTransform
  }

  [<CustomOperation("rotatedBy")>]
  member _.RotatedBy(state: DrawState, rotation: Quaternion) = {
    state with
        LocalRotation = state.LocalRotation * rotation
  }

  [<CustomOperation("rotatedByYawPitchRoll")>]
  member _.RotatedByYawPitchRoll
    (state: DrawState, yaw: float32, pitch: float32, roll: float32)
    =
    let rot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll)

    {
      state with
          LocalRotation = state.LocalRotation * rot
    }

  [<CustomOperation("lookAt")>]
  member _.LookAt(state: DrawState, target: Vector3) =
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

  [<CustomOperation("scaledBy")>]
  member _.ScaledBy(state: DrawState, scale: float32) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  [<CustomOperation("scaledByVec")>]
  member _.ScaledByVec(state: DrawState, scale: Vector3) = {
    state with
        LocalScale = state.LocalScale * scale
  }

  [<CustomOperation("withTransform")>]
  member _.WithTransform(state: DrawState, transform: Matrix) = {
    state with
        LocalPosition = Vector3.Zero
        LocalRotation = Quaternion.Identity
        LocalScale = Vector3.One
        Parent = ValueSome transform
  }

  [<CustomOperation("withBinding")>]
  member _.WithBinding(state: DrawState, binding: EffectBinding) = {
    state with
        Binding = ValueSome binding
  }

  [<CustomOperation("withMaterialData")>]
  member _.WithMaterialData(state: DrawState, data: PBRMaterialData) = {
    state with
        MaterialData = ValueSome data
  }

  [<CustomOperation("withEmissive")>]
  member _.WithEmissive(state: DrawState, color: Color, intensity: float32) =
    let data =
      state.MaterialData |> ValueOption.defaultValue PBRMaterial.defaults

    {
      state with
          MaterialData =
            ValueSome {
              data with
                  EmissiveColor = color
                  EmissiveIntensity = intensity
            }
    }

  [<CustomOperation("withAlbedo")>]
  member _.WithAlbedo(state: DrawState, color: Color) =
    let data =
      state.MaterialData |> ValueOption.defaultValue PBRMaterial.defaults

    {
      state with
          MaterialData = ValueSome { data with AlbedoColor = color }
    }

  [<CustomOperation("withMetallic")>]
  member _.WithMetallic(state: DrawState, value: float32) =
    let data =
      state.MaterialData |> ValueOption.defaultValue PBRMaterial.defaults

    {
      state with
          MaterialData = ValueSome { data with Metallic = value }
    }

  [<CustomOperation("withRoughness")>]
  member _.WithRoughness(state: DrawState, value: float32) =
    let data =
      state.MaterialData |> ValueOption.defaultValue PBRMaterial.defaults

    {
      state with
          MaterialData = ValueSome { data with Roughness = value }
    }

  [<CustomOperation("withBones")>]
  member _.WithBones(state: DrawState, bones: Matrix[]) = {
    state with
        Bones = ValueSome bones
  }

  [<CustomOperation("opaque")>]
  member _.Opaque(state: DrawState) = {
    state with
        Pass = Mibo.Rendering.Graphics3D.Opaque
  }

  [<CustomOperation("transparent")>]
  member _.Transparent(state: DrawState) = {
    state with
        Pass = Mibo.Rendering.Graphics3D.Transparent
  }

[<Sealed>]
[<Extension>]
type PipelineBufferExtensions =

  [<Extension>]
  static member Camera
    (
      this: RenderBuffer<unit, RenderCommand>,
      camera: Mibo.Rendering.Graphics3D.Camera
    ) =
    this.Add((), SetCamera camera)
    this

  [<Extension>]
  static member Lighting
    (
      this: RenderBuffer<unit, RenderCommand>,
      lighting: Mibo.Rendering.Graphics3D.LightingState
    ) =
    this.Add((), SetLighting lighting)
    this

  [<Extension>]
  static member AddLight
    (
      this: RenderBuffer<unit, RenderCommand>,
      light: Mibo.Rendering.Graphics3D.Light
    ) =
    this.Add((), AddLight light)
    this

  [<Extension>]
  static member Viewport
    (this: RenderBuffer<unit, RenderCommand>, viewport: Viewport)
    =
    this.Add((), SetViewport viewport)
    this

  [<Extension>]
  static member Clear(this: RenderBuffer<unit, RenderCommand>, color: Color) =
    this.Add((), ClearTarget(ValueSome color, true))
    this

  [<Extension>]
  static member ClearTarget
    (this: RenderBuffer<unit, RenderCommand>, color: Color, clearDepth: bool)
    =
    this.Add((), ClearTarget(ValueSome color, clearDepth))
    this

  [<Extension>]
  static member ClearDepth(this: RenderBuffer<unit, RenderCommand>) =
    this.Add((), ClearTarget(ValueNone, true))
    this

  [<Extension>]
  static member Custom
    (
      this: RenderBuffer<unit, RenderCommand>,
      sortKey: SortKey,
      drawFn: GameContext * RenderContext -> unit
    ) =
    this.Add((), DrawCustom(sortKey, drawFn))
    this

  [<Extension>]
  static member Quad
    (
      this: RenderBuffer<unit, RenderCommand>,
      texture: Texture2D,
      quad: Mibo.Rendering.Graphics3D.Quad3D
    ) =
    this.Add(
      (),
      DrawSpriteQuad {
        Pass = Mibo.Rendering.Graphics3D.Opaque
        Texture = texture
        Quad = quad
      }
    )

    this

  [<Extension>]
  static member QuadTransparent
    (
      this: RenderBuffer<unit, RenderCommand>,
      texture: Texture2D,
      quad: Mibo.Rendering.Graphics3D.Quad3D
    ) =
    this.Add(
      (),
      DrawSpriteQuad {
        Pass = Mibo.Rendering.Graphics3D.Transparent
        Texture = texture
        Quad = quad
      }
    )

    this

  [<Extension>]
  static member Billboard
    (
      this: RenderBuffer<unit, RenderCommand>,
      texture: Texture2D,
      billboard: Mibo.Rendering.Graphics3D.Billboard3D
    ) =
    this.Add(
      (),
      DrawSpriteBillboard {
        Pass = Mibo.Rendering.Graphics3D.Transparent
        Texture = texture
        Billboard = billboard
      }
    )

    this

  [<Extension>]
  static member BillboardOpaque
    (
      this: RenderBuffer<unit, RenderCommand>,
      texture: Texture2D,
      billboard: Mibo.Rendering.Graphics3D.Billboard3D
    ) =
    this.Add(
      (),
      DrawSpriteBillboard {
        Pass = Mibo.Rendering.Graphics3D.Opaque
        Texture = texture
        Billboard = billboard
      }
    )

    this

  [<Extension>]
  static member Line
    (
      this: RenderBuffer<unit, RenderCommand>,
      p1: Vector3,
      p2: Vector3,
      color: Color
    ) =
    this.Add((), DrawLine(p1, p2, color, Mibo.Rendering.Graphics3D.Opaque))
    this

  [<Extension>]
  static member Lines
    (
      this: RenderBuffer<unit, RenderCommand>,
      verts: VertexPositionColor[],
      lineCount: int
    ) =
    this.Add((), DrawLines(verts, lineCount, Mibo.Rendering.Graphics3D.Opaque))
    this

  [<Extension>]
  static member LinesEffect
    (
      this: RenderBuffer<unit, RenderCommand>,
      pass: Mibo.Rendering.Graphics3D.RenderPass,
      effect: Effect,
      setup: Mibo.Rendering.Graphics3D.EffectSetup voption,
      verts: VertexPositionColor[],
      lineCount: int
    ) =
    this.Add((), DrawLinesEffect(verts, lineCount, effect, setup, pass))
    this

  [<Extension>]
  static member Draw
    (
      this: RenderBuffer<unit, RenderCommand>,
      drawable: Mibo.Rendering.Graphics3D.V2.Drawable voption
    ) =
    drawable |> ValueOption.iter(fun d -> this.Add((), RenderCommand.Draw d))
    this

  [<Extension>]
  static member DrawMany
    (
      this: RenderBuffer<unit, RenderCommand>,
      drawables: seq<Mibo.Rendering.Graphics3D.V2.Drawable voption>
    ) =
    for d in drawables do
      d |> ValueOption.iter(fun dr -> this.Add((), RenderCommand.Draw dr))

    this

  [<Extension>]
  static member Submit(this: RenderBuffer<unit, RenderCommand>) = ()

module Buffer =

  let camera camera (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Camera(camera)

  let lighting lighting (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Lighting(lighting)

  let addLight light (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.AddLight(light)

  let viewport viewport (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Viewport(viewport)

  let clear color (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Clear(color)

  let clearTarget color clearDepth (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.ClearTarget(color, clearDepth)

  let clearDepth(buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.ClearDepth()

  let custom sortKey drawFn (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Custom(sortKey, drawFn)

  let quad tex q (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Quad(tex, q)

  let quadTransparent tex q (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.QuadTransparent(tex, q)

  let billboard tex b (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Billboard(tex, b)

  let billboardOpaque tex b (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.BillboardOpaque(tex, b)

  let line p1 p2 col (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Line(p1, p2, col)

  let lines verts lineCount (buffer: RenderBuffer<unit, RenderCommand>) =
    buffer.Lines(verts, lineCount)

  let linesEffect
    pass
    effect
    setup
    verts
    lineCount
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.LinesEffect(pass, effect, setup, verts, lineCount)

  let draw d (buffer: RenderBuffer<unit, RenderCommand>) = buffer.Draw(d)

  let drawMany
    (ds: seq<Mibo.Rendering.Graphics3D.V2.Drawable voption>)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    buffer.DrawMany(ds)

  let submit(buffer: RenderBuffer<unit, RenderCommand>) = buffer.Submit()

module View =
  let draw = DrawableBuilder()
