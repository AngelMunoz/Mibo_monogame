namespace Mibo.Rendering3D

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
  val empty: DrawState
  val computeTransform: state: DrawState -> Matrix
  val toDrawable: state: DrawState -> Mibo.Rendering3D.Drawable voption

[<Sealed>]
type DrawableBuilder =
  new: unit -> DrawableBuilder
  member Yield: unit -> DrawState
  member Run: state: DrawState -> Mibo.Rendering3D.Drawable voption

  member Mesh:
    state: DrawState * mesh: Mibo.Rendering.Graphics3D.Mesh -> DrawState

  member At:
    state: DrawState * x: float32 * y: float32 * z: float32 -> DrawState

  member At: state: DrawState * position: Vector3 -> DrawState
  member Offset: state: DrawState * offset: Vector3 -> DrawState
  member RelativeTo: state: DrawState * parentTransform: Matrix -> DrawState
  member RotatedBy: state: DrawState * rotation: Quaternion -> DrawState

  member RotatedByYawPitchRoll:
    state: DrawState * yaw: float32 * pitch: float32 * roll: float32 ->
      DrawState

  member LookAt: state: DrawState * target: Vector3 -> DrawState
  member ScaledBy: state: DrawState * scale: float32 -> DrawState
  member ScaledByVec: state: DrawState * scale: Vector3 -> DrawState
  member WithTransform: state: DrawState * transform: Matrix -> DrawState
  member WithBinding: state: DrawState * binding: EffectBinding -> DrawState

  member WithMaterialData:
    state: DrawState * data: PBRMaterialData -> DrawState

  member WithEmissive:
    state: DrawState * color: Color * intensity: float32 -> DrawState

  member WithAlbedo: state: DrawState * color: Color -> DrawState
  member WithMetallic: state: DrawState * value: float32 -> DrawState
  member WithRoughness: state: DrawState * value: float32 -> DrawState
  member WithBones: state: DrawState * bones: Matrix[] -> DrawState
  member Opaque: state: DrawState -> DrawState
  member Transparent: state: DrawState -> DrawState

[<Sealed>]
[<Extension>]
type PipelineBufferExtensions =
  [<Extension>]
  static member Camera:
    this: RenderBuffer<unit, RenderCommand> *
    camera: Mibo.Rendering.Graphics3D.Camera ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Lighting:
    this: RenderBuffer<unit, RenderCommand> *
    lighting: Mibo.Rendering.Graphics3D.LightingState ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member AddLight:
    this: RenderBuffer<unit, RenderCommand> *
    light: Mibo.Rendering.Graphics3D.Light ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Viewport:
    this: RenderBuffer<unit, RenderCommand> * viewport: Viewport ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Clear:
    this: RenderBuffer<unit, RenderCommand> * color: Color ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member ClearTarget:
    this: RenderBuffer<unit, RenderCommand> * color: Color * clearDepth: bool ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member ClearDepth:
    this: RenderBuffer<unit, RenderCommand> -> RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Custom:
    this: RenderBuffer<unit, RenderCommand> *
    sortKey: SortKey *
    drawFn: (GameContext * RenderContext -> unit) ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Quad:
    this: RenderBuffer<unit, RenderCommand> *
    texture: Texture2D *
    quad: Mibo.Rendering.Graphics3D.Quad3D ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member QuadTransparent:
    this: RenderBuffer<unit, RenderCommand> *
    texture: Texture2D *
    quad: Mibo.Rendering.Graphics3D.Quad3D ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Billboard:
    this: RenderBuffer<unit, RenderCommand> *
    texture: Texture2D *
    billboard: Mibo.Rendering.Graphics3D.Billboard3D ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member BillboardOpaque:
    this: RenderBuffer<unit, RenderCommand> *
    texture: Texture2D *
    billboard: Mibo.Rendering.Graphics3D.Billboard3D ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Line:
    this: RenderBuffer<unit, RenderCommand> *
    p1: Vector3 *
    p2: Vector3 *
    color: Color ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Lines:
    this: RenderBuffer<unit, RenderCommand> *
    verts: VertexPositionColor[] *
    lineCount: int ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member LinesEffect:
    this: RenderBuffer<unit, RenderCommand> *
    pass: Mibo.Rendering.Graphics3D.RenderPass *
    effect: Effect *
    setup: Mibo.Rendering.Graphics3D.EffectSetup voption *
    verts: VertexPositionColor[] *
    lineCount: int ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Draw:
    this: RenderBuffer<unit, RenderCommand> *
    drawable: Mibo.Rendering3D.Drawable voption ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member DrawMany:
    this: RenderBuffer<unit, RenderCommand> *
    drawables: seq<Mibo.Rendering3D.Drawable voption> ->
      RenderBuffer<unit, RenderCommand>

  [<Extension>]
  static member Submit: this: RenderBuffer<unit, RenderCommand> -> unit

module Buffer =
  val camera:
    camera: Mibo.Rendering.Graphics3D.Camera ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val lighting:
    lighting: Mibo.Rendering.Graphics3D.LightingState ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val addLight:
    light: Mibo.Rendering.Graphics3D.Light ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val viewport:
    viewport: Viewport ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val clear:
    color: Color ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val clearTarget:
    color: Color ->
    clearDepth: bool ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val clearDepth:
    buffer: RenderBuffer<unit, RenderCommand> -> RenderBuffer<unit, RenderCommand>

  val custom:
    sortKey: SortKey ->
    drawFn: (GameContext * RenderContext -> unit) ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val quad:
    tex: Texture2D ->
    q: Mibo.Rendering.Graphics3D.Quad3D ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val quadTransparent:
    tex: Texture2D ->
    q: Mibo.Rendering.Graphics3D.Quad3D ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val billboard:
    tex: Texture2D ->
    b: Mibo.Rendering.Graphics3D.Billboard3D ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val billboardOpaque:
    tex: Texture2D ->
    b: Mibo.Rendering.Graphics3D.Billboard3D ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val line:
    p1: Vector3 ->
    p2: Vector3 ->
    col: Color ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val lines:
    verts: VertexPositionColor[] ->
    lineCount: int ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val linesEffect:
    pass: Mibo.Rendering.Graphics3D.RenderPass ->
    effect: Effect ->
    setup: Mibo.Rendering.Graphics3D.EffectSetup voption ->
    verts: VertexPositionColor[] ->
    lineCount: int ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val draw:
    d: Mibo.Rendering3D.Drawable voption ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val drawMany:
    ds: seq<Mibo.Rendering3D.Drawable voption> ->
    buffer: RenderBuffer<unit, RenderCommand> ->
      RenderBuffer<unit, RenderCommand>

  val submit: buffer: RenderBuffer<unit, RenderCommand> -> unit

module View =
  val draw: DrawableBuilder