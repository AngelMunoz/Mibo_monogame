namespace Mibo.Elmish.Graphics3D

open System
open System.Runtime.CompilerServices
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish
open Mibo.Rendering

// --- 3D Rendering Implementation ---

/// <summary>A 3D render command.</summary>
/// <remarks>These commands are queued to a <see cref="T:Mibo.Elmish.RenderBuffer`1"/> and executed by <see cref="T:Mibo.Elmish.Graphics3D.Batch3DRenderer`1"/>.</remarks>
[<Struct>]
type RenderCmd3D =
  /// <summary>Set viewport for multi-camera rendering (split-screen, minimaps, etc).</summary>
  | SetViewport of viewport: Viewport
  /// <summary>Clear the render target. Use between cameras in multi-camera setups.</summary>
  | ClearTarget of clearColor: Color voption * clearDepth: bool
  /// <summary>Sets the camera for subsequent draws.</summary>
  | SetCamera of camera: Camera
  /// <summary>Draws a 3D mesh from a <see cref="T:Microsoft.Xna.Framework.Graphics.Model"/>.</summary>
  | DrawMesh of
    pass: RenderPass *
    model: Model *
    transform: Matrix *
    diffuseColor: Color voption *
    texture: Texture2D voption *
    setup: EffectSetup voption

  /// <summary>Escape hatch: run an arbitrary draw function.</summary>
  /// <remarks>The function is invoked with the current camera View/Projection matrices (as last set by <c>SetCamera</c>) so userland can integrate custom effects without forking the renderer.</remarks>
  | DrawCustom of draw: (GameContext * Matrix * Matrix -> unit)

  /// <summary>Contract for skinned/animated models.</summary>
  /// <remarks>If the underlying model uses <see cref="T:Microsoft.Xna.Framework.Graphics.SkinnedEffect"/>, this renderer will apply <c>bones</c> via <c>SkinnedEffect.SetBoneTransforms</c>.</remarks>
  | DrawSkinned of
    pass: RenderPass *
    model: Model *
    transform: Matrix *
    bones: Matrix[] *
    diffuseColor: Color voption *
    texture: Texture2D voption *
    setup: EffectSetup voption

  /// <summary>Sprite-style quad draw (90% case): unlit textured quad using built-in sprite effect.</summary>
  | DrawSpriteQuad of spriteQuad: SpriteQuadCmd

  /// <summary>Sprite-style billboard draw (90% case): unlit textured billboard using built-in sprite effect.</summary>
  | DrawSpriteBillboard of spriteBillboard: SpriteBillboardCmd

  /// <summary>Effect-driven quad draw: user provides an effect and optional setup callback.</summary>
  | DrawQuadEffect of quadEffect: EffectQuadCmd

  /// <summary>Effect-driven billboard draw: user provides an effect and optional setup callback.</summary>
  | DrawBillboardEffect of billboardEffect: EffectBillboardCmd

  /// <summary>Draw a single line segment using the built-in BasicEffect.</summary>
  | DrawLine of line: struct (Vector3 * Vector3 * Color) * pass: RenderPass

  /// <summary>Draw line segments using the built-in BasicEffect (vertex colors).</summary>
  /// <remarks>Lines are batched together; flushes occur on pass/camera/effect changes.</remarks>
  | DrawLines of
    lines: VertexPositionColor[] *
    lineCount: int *
    pass: RenderPass

  /// <summary>Draw line segments using a custom effect.</summary>
  /// <remarks>Use for glowing lines, dashed lines, or other custom line effects.</remarks>
  | DrawLinesEffect of
    lines: VertexPositionColor[] *
    lineCount: int *
    effect: Effect *
    setup: EffectSetup voption *
    pass: RenderPass


/// <summary>Convenience alias for a render buffer for 3D commands.</summary>
/// <remarks>3D rendering typically does not rely on a 2D-style render-layer ordering. We preserve submission order (do not sort), so the key is <c>unit</c>.</remarks>
type RenderBuffer<'Cmd> = RenderBuffer<unit, 'Cmd>

/// <summary>Configuration for <see cref="T:Mibo.Elmish.Graphics3D.Batch3DRenderer`1"/>.</summary>
/// <remarks>This controls the rendering pass defaults (clears + device state), not per-mesh material setup.</remarks>
[<Struct>]
type Batch3DConfig = {
  /// Optional color buffer clear.
  ClearColor: Color voption
  /// Whether to clear the depth buffer before rendering.
  ClearDepth: bool

  /// <summary>Whether to restore the previous <see cref="T:Microsoft.Xna.Framework.Graphics.GraphicsDevice"/> states after rendering.</summary>
  /// <remarks>This makes composition with other renderers more predictable (at a small cost).</remarks>
  RestoreDeviceStates: bool

  OpaqueBlendState: BlendState
  OpaqueDepthStencilState: DepthStencilState

  TransparentBlendState: BlendState
  TransparentDepthStencilState: DepthStencilState

  RasterizerState: RasterizerState

  /// <summary>Sampler state used for Sprite3D draws (billboards/quads).</summary>
  SpriteSamplerState: SamplerState

  /// <summary>Rasterizer state used for Sprite3D draws (billboards/quads).</summary>
  SpriteRasterizerState: RasterizerState

  SpriteOpaqueBlendState: BlendState
  SpriteOpaqueDepthStencilState: DepthStencilState

  SpriteTransparentBlendState: BlendState
  SpriteTransparentDepthStencilState: DepthStencilState

  /// <summary>If true, opaque draws are sorted front-to-back by distance to camera.</summary>
  /// <remarks>This can reduce overdraw.</remarks>
  SortOpaqueFrontToBack: bool

  /// <summary>Enables caching of <see cref="T:Microsoft.Xna.Framework.Graphics.BasicEffect"/> lighting setup.</summary>
  /// <remarks>If you want to animate lights every frame, disable caching.</remarks>
  CacheBasicEffectLighting: bool
}

module Batch3DConfig =

  let defaults: Batch3DConfig = {
    ClearColor = ValueNone
    ClearDepth = true
    RestoreDeviceStates = false

    OpaqueBlendState = BlendState.Opaque
    OpaqueDepthStencilState = DepthStencilState.Default

    TransparentBlendState = BlendState.AlphaBlend
    // A common default: depth test on, depth writes off.
    TransparentDepthStencilState = DepthStencilState.DepthRead

    RasterizerState = RasterizerState.CullCounterClockwise

    // Sprite3D defaults: unlit textured quads/billboards.
    SpriteSamplerState = SamplerState.LinearClamp
    SpriteRasterizerState = RasterizerState.CullNone

    SpriteOpaqueBlendState = BlendState.Opaque
    SpriteOpaqueDepthStencilState = DepthStencilState.Default

    SpriteTransparentBlendState = BlendState.AlphaBlend
    SpriteTransparentDepthStencilState = DepthStencilState.DepthRead

    SortOpaqueFrontToBack = false
    CacheBasicEffectLighting = true
  }

module internal DeviceState =
  type SavedStates = {
    BlendState: BlendState
    DepthStencilState: DepthStencilState
    RasterizerState: RasterizerState
  }

  let inline save(gd: GraphicsDevice) : SavedStates = {
    BlendState = gd.BlendState
    DepthStencilState = gd.DepthStencilState
    RasterizerState = gd.RasterizerState
  }

  let inline restore (gd: GraphicsDevice) (states: SavedStates) =
    gd.BlendState <- states.BlendState
    gd.DepthStencilState <- states.DepthStencilState
    gd.RasterizerState <- states.RasterizerState

  let inline applyRasterizer
    (rasterizer: RasterizerState)
    (gd: GraphicsDevice)
    =
    gd.RasterizerState <- rasterizer

  let inline applyMeshPass
    (config: Batch3DConfig)
    (pass: RenderPass)
    (gd: GraphicsDevice)
    =
    match pass with
    | Opaque ->
      gd.DepthStencilState <- config.OpaqueDepthStencilState
      gd.BlendState <- config.OpaqueBlendState
    | Transparent ->
      gd.DepthStencilState <- config.TransparentDepthStencilState
      gd.BlendState <- config.TransparentBlendState

    gd.RasterizerState <- config.RasterizerState

  let inline applySpritePass
    (config: Batch3DConfig)
    (pass: RenderPass)
    (gd: GraphicsDevice)
    =
    match pass with
    | Opaque ->
      gd.BlendState <- config.SpriteOpaqueBlendState
      gd.DepthStencilState <- config.SpriteOpaqueDepthStencilState
    | Transparent ->
      gd.BlendState <- config.SpriteTransparentBlendState
      gd.DepthStencilState <- config.SpriteTransparentDepthStencilState

    gd.RasterizerState <- config.SpriteRasterizerState
    gd.SamplerStates[0] <- config.SpriteSamplerState

module StandardEffects =
  let inline defaultLighting(effect: BasicEffect) =
    effect.LightingEnabled <- true
    effect.AmbientLightColor <- Vector3(0.2f, 0.2f, 0.2f)
    effect.DirectionalLight0.Enabled <- true
    effect.DirectionalLight0.DiffuseColor <- Vector3(0.8f, 0.8f, 0.8f)
    effect.DirectionalLight0.Direction <- Vector3(-1.0f, -1.0f, -1.0f)
    effect.DirectionalLight0.SpecularColor <- Vector3.Zero

module internal CameraState =
  type CameraBasis = { Right: Vector3; Up: Vector3 }

  type CameraInfo = {
    Position: Vector3
    Basis: CameraBasis
  }

  let getWorldPosition(view: Matrix) : Vector3 =
    let inv = Matrix.Invert(view)
    inv.Translation

  let private calculateBasis(view: Matrix) : CameraBasis =
    let invView = Matrix.Invert(view)

    {
      Right = Vector3(invView.M11, invView.M21, invView.M31)
      Up = Vector3(invView.M12, invView.M22, invView.M32)
    }

  let createInfo(view: Matrix) : CameraInfo = {
    Position = getWorldPosition view
    Basis = calculateBasis view
  }

  let billboardBasis
    (mode: BillboardMode)
    (camInfo: CameraInfo)
    (position: Vector3)
    : struct (Vector3 * Vector3) =
    match mode with
    | Spherical -> struct (camInfo.Basis.Right, camInfo.Basis.Up)
    | Cylindrical upAxis ->
      let viewDir = camInfo.Position - position
      let mutable right = Vector3.Cross(upAxis, viewDir)

      if right.LengthSquared() < 0.000001f then
        struct (camInfo.Basis.Right, camInfo.Basis.Up)
      else
        right.Normalize()
        let mutable up = Vector3.Cross(viewDir, right)

        if up.LengthSquared() < 0.000001f then
          struct (camInfo.Basis.Right, camInfo.Basis.Up)
        else
          up.Normalize()
          struct (right, up)

module internal EffectConfig =
  [<Struct>]
  type MeshDrawContext = {
    GraphicsDevice: GraphicsDevice
    Config: Batch3DConfig
    Cache: ConditionalWeakTable<BasicEffect, obj>
    View: Matrix
    Projection: Matrix
    Pass: RenderPass
  }

  [<Struct>]
  type MeshEffectArgs = {
    Transform: Matrix
    DiffuseColor: Color voption
    Texture: Texture2D voption
    Bones: Matrix[] voption
    Setup: EffectSetup voption
  }

  let inline createBasicEffectCache() : ConditionalWeakTable<BasicEffect, obj> =
    ConditionalWeakTable<BasicEffect, obj>()

  let inline applyBasicEffectLighting
    (cache: ConditionalWeakTable<BasicEffect, obj>)
    (enableCache: bool)
    (effect: BasicEffect)
    =
    if enableCache then
      match cache.TryGetValue(effect) with
      | true, _ -> ()
      | false, _ ->
        StandardEffects.defaultLighting effect
        cache.Add(effect, box())
    else
      StandardEffects.defaultLighting effect

  let setupMeshEffect
    (ctx: MeshDrawContext)
    (args: MeshEffectArgs)
    (effect: Effect)
    =
    let effectCtx = {
      World = args.Transform
      View = ctx.View
      Projection = ctx.Projection
    }

    match args.Setup with
    | ValueSome setup -> setup effect effectCtx
    | ValueNone -> ()

    let inline setWvp(e: ^T) =
      if ValueOption.isNone args.Setup then
        (^T: (member set_World: Matrix -> unit) (e, effectCtx.World))
        (^T: (member set_View: Matrix -> unit) (e, effectCtx.View))
        (^T: (member set_Projection: Matrix -> unit) (e, effectCtx.Projection))

    match effect with
    | :? BasicEffect as be ->
      setWvp be
      applyBasicEffectLighting ctx.Cache ctx.Config.CacheBasicEffectLighting be

      args.DiffuseColor
      |> ValueOption.iter(fun c -> be.DiffuseColor <- c.ToVector3())

      args.Texture
      |> ValueOption.iter(fun t ->
        be.TextureEnabled <- true
        be.Texture <- t)

    | :? SkinnedEffect as se ->
      setWvp se
      args.Bones |> ValueOption.iter se.SetBoneTransforms

      args.DiffuseColor
      |> ValueOption.iter(fun c -> se.DiffuseColor <- c.ToVector3())

      args.Texture |> ValueOption.iter(fun t -> se.Texture <- t)

    | _ -> ()


module internal MeshDrawing =
  let inline drawModel
    (ctx: EffectConfig.MeshDrawContext)
    (args: EffectConfig.MeshEffectArgs)
    (model: Model)
    =
    DeviceState.applyMeshPass ctx.Config ctx.Pass ctx.GraphicsDevice

    for mesh in model.Meshes do
      for part in mesh.MeshParts do
        part.Effect |> EffectConfig.setupMeshEffect ctx args

      mesh.Draw()


module internal FrameOrchestration =
  type RenderState() =
    member val View = Matrix.Identity with get, set
    member val Projection = Matrix.Identity with get, set
    member val CameraInfo = CameraState.createInfo Matrix.Identity with get, set
    member val WarnedMissingCamera = false with get, set

  [<Struct>]
  type RenderLists = {
    Opaque: ResizeArray<struct (float32 * RenderCmd3D)>
    Transparent: ResizeArray<struct (float32 * RenderCmd3D)>
  }

  /// <summary>Environmental dependencies for the render process.</summary>
  [<Struct>]
  type RenderEnv = {
    Device: GraphicsDevice
    Config: Batch3DConfig
  }

  /// <summary>Defines the behaviors required to execute a 3D render frame.</summary>
  type IRenderPipeline =
    abstract member ClearLists: unit -> unit
    abstract member FlushSegment: unit -> unit
    abstract member SortOpaque: unit -> unit
    abstract member SortTransparent: unit -> unit
    abstract member DrawMesh: RenderCmd3D -> unit

    abstract member DrawSprites:
      RenderPass -> ResizeArray<struct (float32 * RenderCmd3D)> -> unit

  let warnIfMissingCamera
    (state: RenderState)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    if not state.WarnedMissingCamera then
      let mutable hasCamera = false

      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item(i)

        match cmd with
        | SetCamera _ -> hasCamera <- true
        | _ -> ()

      if not hasCamera then
        state.WarnedMissingCamera <- true

        Console.WriteLine(
          "[Mibo] Batch3DRenderer: no camera submitted this frame; using Identity view/projection."
        )

  /// <summary>Executes the command stream using the provided pipeline and state.</summary>
  let runCommandStream
    (pipeline: IRenderPipeline)
    (state: RenderState)
    (env: RenderEnv)
    (lists: RenderLists)
    (gameCtx: GameContext)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    // Initial camera info is based on current view
    state.CameraInfo <- CameraState.createInfo state.View

    pipeline.ClearLists()

    for i = 0 to buffer.Count - 1 do
      let struct (_, cmd) = buffer.Item(i)

      match cmd with
      | SetViewport vp ->
        pipeline.FlushSegment()
        env.Device.Viewport <- vp

      | ClearTarget(colorOpt, clearDepth) ->
        pipeline.FlushSegment()

        match colorOpt, clearDepth with
        | ValueSome c, true ->
          env.Device.Clear(
            ClearOptions.Target ||| ClearOptions.DepthBuffer,
            c,
            1.0f,
            0
          )
        | ValueSome c, false ->
          env.Device.Clear(ClearOptions.Target, c, 1.0f, 0)
        | ValueNone, true ->
          env.Device.Clear(ClearOptions.DepthBuffer, Color.Black, 1.0f, 0)
        | ValueNone, false -> ()

      | SetCamera cam ->
        pipeline.FlushSegment()
        state.View <- cam.View
        state.Projection <- cam.Projection
        state.CameraInfo <- CameraState.createInfo state.View

      | DrawCustom draw ->
        pipeline.FlushSegment()
        draw(gameCtx, state.View, state.Projection)
        // Re-apply baseline state for subsequent draws.
        env.Device.RasterizerState <- env.Config.RasterizerState

      | DrawMesh(pass, _m, transform, _colorOpt, _texOpt, _setupOpt) ->
        let pos = transform.Translation
        let distSq = Vector3.DistanceSquared(state.CameraInfo.Position, pos)

        match pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawSkinned(pass, _m, transform, _bones, _colorOpt, _texOpt, _setupOpt) ->
        let pos = transform.Translation
        let distSq = Vector3.DistanceSquared(state.CameraInfo.Position, pos)

        match pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawSpriteQuad s ->
        let distSq =
          Vector3.DistanceSquared(state.CameraInfo.Position, s.Quad.Center)

        match s.Pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawSpriteBillboard s ->
        let distSq =
          Vector3.DistanceSquared(
            state.CameraInfo.Position,
            s.Billboard.Position
          )

        match s.Pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawQuadEffect e ->
        let distSq =
          Vector3.DistanceSquared(state.CameraInfo.Position, e.Quad.Center)

        match e.Pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawBillboardEffect e ->
        let distSq =
          Vector3.DistanceSquared(
            state.CameraInfo.Position,
            e.Billboard.Position
          )

        match e.Pass with
        | Opaque -> lists.Opaque.Add struct (distSq, cmd)
        | Transparent -> lists.Transparent.Add struct (distSq, cmd)

      | DrawLine(_, pass)
      | DrawLines(_, _, pass)
      | DrawLinesEffect(_, _, _, _, pass) ->
        // Lines don't have a single center; use 0 for submission-order within pass
        match pass with
        | Opaque -> lists.Opaque.Add struct (0f, cmd)
        | Transparent -> lists.Transparent.Add struct (0f, cmd)

    pipeline.FlushSegment()



module internal SpriteRendering =
  [<Struct>]
  type SpriteRenderContext = {
    GraphicsDevice: GraphicsDevice
    Config: Batch3DConfig
    SpriteEffect: BasicEffect
    LineEffect: BasicEffect
    SpriteQuadBatch: SpriteQuadBatch.State
    BillboardBatch: BillboardBatch.State
    LineBatch: LineBatch.State
    ViewMatrix: Matrix
    ProjectionMatrix: Matrix
    CameraInfo: CameraState.CameraInfo
  }

  let inline private flushSpriteQuadBatch
    (effect: Effect)
    (state: SpriteQuadBatch.State)
    =
    SpriteQuadBatch.end' effect state
    SpriteQuadBatch.begin' state

  let drawSpritesInList
    (ctx: SpriteRenderContext)
    (pass: RenderPass)
    (items: ResizeArray<struct (float32 * RenderCmd3D)>)
    =
    let mutable currentSpriteQuadTexture: Texture2D = null
    let mutable currentSpriteBillboardTexture: Texture2D = null

    // Reset batches
    SpriteQuadBatch.begin' ctx.SpriteQuadBatch
    BillboardBatch.begin' ctx.BillboardBatch

    let applySpriteStates(pass: RenderPass) =
      ctx.GraphicsDevice |> DeviceState.applySpritePass ctx.Config pass

    let ensureSpriteQuadEffect(tex: Texture2D) =
      if tex <> currentSpriteQuadTexture then
        // Flush pending sprite quads before switching textures.
        if not(isNull currentSpriteQuadTexture) then
          applySpriteStates pass
          ctx.SpriteEffect.View <- ctx.ViewMatrix
          ctx.SpriteEffect.Projection <- ctx.ProjectionMatrix
          flushSpriteQuadBatch ctx.SpriteEffect ctx.SpriteQuadBatch

        currentSpriteQuadTexture <- tex
        ctx.SpriteEffect.Texture <- tex

    let flushPendingQuads() =
      if
        ctx.SpriteQuadBatch.QuadCount > 0
        && not(isNull currentSpriteQuadTexture)
      then
        applySpriteStates pass
        ctx.SpriteEffect.View <- ctx.ViewMatrix
        ctx.SpriteEffect.Projection <- ctx.ProjectionMatrix
        flushSpriteQuadBatch ctx.SpriteEffect ctx.SpriteQuadBatch

    let flushPendingBillboards() =
      if ctx.BillboardBatch.SpriteCount > 0 then
        BillboardBatch.end' (ctx.SpriteEffect :> Effect) ctx.BillboardBatch
        BillboardBatch.begin' ctx.BillboardBatch

    for i = 0 to items.Count - 1 do
      let struct (_, cmd) = items[i]

      match cmd with
      | DrawSpriteQuad s ->
        // Switching from billboards: flush.
        flushPendingBillboards()

        ensureSpriteQuadEffect s.Texture
        let q = s.Quad

        SpriteQuadBatch.draw
          q.Center
          q.Right
          q.Up
          q.Color
          q.Uv
          ctx.SpriteQuadBatch

      | DrawSpriteBillboard s ->
        // Flush any pending quads before drawing billboards.
        flushPendingQuads()

        if s.Texture <> currentSpriteBillboardTexture then
          flushPendingBillboards()
          currentSpriteBillboardTexture <- s.Texture
          applySpriteStates pass
          ctx.SpriteEffect.View <- ctx.ViewMatrix
          ctx.SpriteEffect.Projection <- ctx.ProjectionMatrix
          ctx.SpriteEffect.Texture <- s.Texture
          BillboardBatch.begin' ctx.BillboardBatch
        else
          applySpriteStates pass

        let b = s.Billboard

        let struct (right, up) =
          CameraState.billboardBasis b.Mode ctx.CameraInfo b.Position

        BillboardBatch.drawUv
          b.Position
          b.Size
          b.Rotation
          b.Color
          b.Uv
          right
          up
          ctx.BillboardBatch

      | DrawQuadEffect e ->
        // Flush sprite batches before custom-effect quads.
        flushPendingBillboards()
        flushPendingQuads()

        let effect = e.Effect

        // Per-command setup; since we can't assume parameters are stable across draws,
        // render this command immediately (one quad per command) for correctness.
        e.Setup
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = ctx.ViewMatrix
            Projection = ctx.ProjectionMatrix
          })

        applySpriteStates pass

        SpriteQuadBatch.begin' ctx.SpriteQuadBatch
        let q = e.Quad

        SpriteQuadBatch.draw
          q.Center
          q.Right
          q.Up
          q.Color
          q.Uv
          ctx.SpriteQuadBatch

        SpriteQuadBatch.end' effect ctx.SpriteQuadBatch

      | DrawBillboardEffect e ->
        // Flush sprite batches before effect billboards.
        flushPendingQuads()
        flushPendingBillboards()

        let effect = e.Effect

        e.Setup
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = ctx.ViewMatrix
            Projection = ctx.ProjectionMatrix
          })

        applySpriteStates pass

        BillboardBatch.begin' ctx.BillboardBatch
        let b = e.Billboard

        let struct (right, up) =
          CameraState.billboardBasis b.Mode ctx.CameraInfo b.Position

        BillboardBatch.drawUv
          b.Position
          b.Size
          b.Rotation
          b.Color
          b.Uv
          right
          up
          ctx.BillboardBatch

        BillboardBatch.end' effect ctx.BillboardBatch

      | DrawLine(struct (p1, p2, color), _) ->
        // Flush sprite batches before drawing lines
        flushPendingQuads()
        flushPendingBillboards()

        applySpriteStates pass
        ctx.LineEffect.View <- ctx.ViewMatrix
        ctx.LineEffect.Projection <- ctx.ProjectionMatrix

        LineBatch.begin' ctx.LineBatch
        LineBatch.addLine p1 p2 color ctx.LineBatch
        LineBatch.end' ctx.LineEffect ctx.LineBatch

      | DrawLines(verts, lineCount, _) ->
        // Flush sprite batches before drawing lines
        flushPendingQuads()
        flushPendingBillboards()

        applySpriteStates pass
        ctx.LineEffect.View <- ctx.ViewMatrix
        ctx.LineEffect.Projection <- ctx.ProjectionMatrix

        LineBatch.begin' ctx.LineBatch
        LineBatch.addLines verts lineCount ctx.LineBatch
        LineBatch.end' ctx.LineEffect ctx.LineBatch

      | DrawLinesEffect(verts, lineCount, effect, setupOpt, _) ->
        // Flush sprite batches before drawing lines with custom effect
        flushPendingQuads()
        flushPendingBillboards()

        setupOpt
        |> ValueOption.iter(fun setup ->
          setup effect {
            World = Matrix.Identity
            View = ctx.ViewMatrix
            Projection = ctx.ProjectionMatrix
          })

        applySpriteStates pass

        LineBatch.begin' ctx.LineBatch
        LineBatch.addLines verts lineCount ctx.LineBatch
        LineBatch.end' effect ctx.LineBatch

      | _ -> ()

    // Final flush
    flushPendingQuads()

    if ctx.BillboardBatch.SpriteCount > 0 then
      BillboardBatch.end' (ctx.SpriteEffect :> Effect) ctx.BillboardBatch


/// <summary>Standard 3D Renderer using <see cref="T:Microsoft.Xna.Framework.Graphics.BasicEffect"/>.</summary>
type Batch3DRenderer<'Model>
  (
    game: Game,
    config: Batch3DConfig,
    [<InlineIfLambda>] view:
      GameContext * 'Model * RenderBuffer<RenderCmd3D> -> unit
  ) =

  let buffer = RenderBuffer<RenderCmd3D>()

  // Default Camera state
  let renderState = FrameOrchestration.RenderState()

  // Cache of BasicEffect instances that have had lighting applied.
  let basicEffectCache = EffectConfig.createBasicEffectCache()

  // Scratch buffers to avoid per-frame allocations.
  let opaque = ResizeArray<struct (float32 * RenderCmd3D)>(1024)
  let transparent = ResizeArray<struct (float32 * RenderCmd3D)>(1024)

  let lists: FrameOrchestration.RenderLists = {
    Opaque = opaque
    Transparent = transparent
  }

  let clearLists() =
    opaque.Clear()
    transparent.Clear()

  // Sprite batchers
  let mutable spriteQuadBatch = SpriteQuadBatch.create(game.GraphicsDevice)
  let mutable billboardBatch = BillboardBatch.create(game.GraphicsDevice)
  let mutable lineBatch = LineBatch.create(game.GraphicsDevice)

  // Built-in unlit sprite effect (texture + vertex color)
  let spriteEffect =
    let e = new BasicEffect(game.GraphicsDevice)
    e.LightingEnabled <- false
    e.TextureEnabled <- true
    e.VertexColorEnabled <- true
    e

  // Built-in line effect (vertex color, no texture)
  let lineEffect =
    let e = new BasicEffect(game.GraphicsDevice)
    e.LightingEnabled <- false
    e.TextureEnabled <- false
    e.VertexColorEnabled <- true
    e

  // Cached sort comparers to avoid per-frame allocations
  let opaqueComparer =
    { new Collections.Generic.IComparer<struct (float32 * RenderCmd3D)> with
        member _.Compare(struct (da, _), struct (db, _)) = compare da db
    }

  let transparentComparer =
    { new Collections.Generic.IComparer<struct (float32 * RenderCmd3D)> with
        member _.Compare(struct (da, _), struct (db, _)) = compare db da
    }

  // Renderer-lifetime pipeline; Draw just executes.
  let pipeline: FrameOrchestration.IRenderPipeline =
    { new FrameOrchestration.IRenderPipeline with
        member _.ClearLists() = clearLists()

        member self.FlushSegment() =
          if lists.Opaque.Count = 0 && lists.Transparent.Count = 0 then
            ()
          else
            self.SortOpaque()
            self.SortTransparent()

            // Execute opaque then transparent
            for i = 0 to lists.Opaque.Count - 1 do
              let struct (_, cmd) = lists.Opaque[i]

              match cmd with
              | DrawMesh _
              | DrawSkinned _ -> self.DrawMesh cmd
              | _ -> ()

            self.DrawSprites Opaque lists.Opaque

            for i = 0 to lists.Transparent.Count - 1 do
              let struct (_, cmd) = lists.Transparent[i]

              match cmd with
              | DrawMesh _
              | DrawSkinned _ -> self.DrawMesh cmd
              | _ -> ()

            self.DrawSprites Transparent lists.Transparent

            lists.Opaque.Clear()
            lists.Transparent.Clear()

        member _.SortOpaque() =
          if config.SortOpaqueFrontToBack then
            opaque.Sort opaqueComparer

        member _.SortTransparent() = transparent.Sort transparentComparer

        member _.DrawMesh(cmd) =
          // Uses latest View/Projection from renderState.
          match cmd with
          | DrawMesh(pass, m, transform, colorOpt, texOpt, setupOpt) ->
            let meshDrawCtx: EffectConfig.MeshDrawContext = {
              GraphicsDevice = game.GraphicsDevice
              Config = config
              Cache = basicEffectCache
              View = renderState.View
              Projection = renderState.Projection
              Pass = pass
            }

            let args: EffectConfig.MeshEffectArgs = {
              Transform = transform
              DiffuseColor = colorOpt
              Texture = texOpt
              Bones = ValueNone
              Setup = setupOpt
            }

            m |> MeshDrawing.drawModel meshDrawCtx args

          | DrawSkinned(pass, m, transform, bones, colorOpt, texOpt, setupOpt) ->
            let meshDrawCtx: EffectConfig.MeshDrawContext = {
              GraphicsDevice = game.GraphicsDevice
              Config = config
              Cache = basicEffectCache
              View = renderState.View
              Projection = renderState.Projection
              Pass = pass
            }

            let args: EffectConfig.MeshEffectArgs = {
              Transform = transform
              DiffuseColor = colorOpt
              Texture = texOpt
              Bones = ValueSome bones
              Setup = setupOpt
            }

            m |> MeshDrawing.drawModel meshDrawCtx args
          | _ -> ()

        member _.DrawSprites pass items =
          let spriteCtx: SpriteRendering.SpriteRenderContext = {
            GraphicsDevice = game.GraphicsDevice
            Config = config
            SpriteEffect = spriteEffect
            LineEffect = lineEffect
            SpriteQuadBatch = spriteQuadBatch
            BillboardBatch = billboardBatch
            LineBatch = lineBatch
            ViewMatrix = renderState.View
            ProjectionMatrix = renderState.Projection
            CameraInfo = renderState.CameraInfo
          }

          items |> SpriteRendering.drawSpritesInList spriteCtx pass
    }

  interface IDisposable with
    member _.Dispose() =
      SpriteQuadBatch.dispose spriteQuadBatch
      BillboardBatch.dispose billboardBatch
      LineBatch.dispose lineBatch

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, gameTime: GameTime) =
      let prevStates = DeviceState.save ctx.GraphicsDevice

      // Clear
      match config.ClearColor with
      | ValueSome c ->
        if config.ClearDepth then
          ctx.GraphicsDevice.Clear(
            ClearOptions.Target ||| ClearOptions.DepthBuffer,
            c,
            1.0f,
            0
          )
        else
          ctx.GraphicsDevice.Clear(ClearOptions.Target, c, 1.0f, 0)
      | ValueNone ->
        if config.ClearDepth then
          ctx.GraphicsDevice.Clear(
            ClearOptions.DepthBuffer,
            Color.Black,
            1.0f,
            0
          )
        else
          ()

      DeviceState.applyRasterizer config.RasterizerState ctx.GraphicsDevice
      buffer.Clear()
      view(ctx, model, buffer)

      FrameOrchestration.warnIfMissingCamera renderState buffer

      let env: FrameOrchestration.RenderEnv = {
        Device = ctx.GraphicsDevice
        Config = config
      }

      buffer
      |> FrameOrchestration.runCommandStream pipeline renderState env lists ctx


      if config.RestoreDeviceStates then
        DeviceState.restore ctx.GraphicsDevice prevStates

[<Obsolete("This renderer is deprecated and will be removed in future versions. Please use Mibo.Rendering.Graphics3D.PipelineRenderer instead.")>]
module Batch3DRenderer =
  /// <summary>Creates a standard 3D renderer.</summary>
  let inline create<'Model>
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd3D> -> unit)
    (game: Game)
    =
    new Batch3DRenderer<'Model>(
      game,
      Batch3DConfig.defaults,
      fun (ctx, model, buffer) -> view ctx model buffer
    )
    :> IRenderer<'Model>

  /// <summary>Creates a 3D renderer with custom configuration.</summary>
  let inline createWithConfig<'Model>
    (config: Batch3DConfig)
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd3D> -> unit)
    (game: Game)
    =
    new Batch3DRenderer<'Model>(
      game,
      config,
      fun (ctx, model, buffer) -> view ctx model buffer
    )
    :> IRenderer<'Model>

/// <summary>Fluent builder for <see cref="T:Mibo.Elmish.Graphics3D.RenderCmd3D"/>.</summary>
[<Struct>]
type Draw3DBuilder = {
  Model: Model
  Transform: Matrix
  Color: Color voption
  Texture: Texture2D voption
  Pass: RenderPass
  Setup: EffectSetup voption
}

/// <summary>Functions for building and submitting 3D draw commands.</summary>
[<Obsolete("This renderer is deprecated and will be removed in future versions. Please use Mibo.Rendering.Graphics3D.PipelineRenderer instead.")>]
module Draw3D =
  /// <summary>Starts a mesh drawing command.</summary>
  let inline mesh model transform = {
    Model = model
    Transform = transform
    Color = ValueNone
    Texture = ValueNone
    Pass = Opaque
    Setup = ValueNone
  }

  let inline meshTransparent model transform = {
    mesh model transform with
        Pass = Transparent
  }

  let inline inPass pass (b: Draw3DBuilder) = { b with Pass = pass }

  let inline withColor col (b: Draw3DBuilder) = { b with Color = ValueSome col }

  let inline withTexture tex (b: Draw3DBuilder) = {
    b with
        Texture = ValueSome tex
  }

  /// <summary>Configure the effect for this draw command.</summary>
  let inline withEffect (setup: EffectSetup) (b: Draw3DBuilder) = {
    b with
        Setup = ValueSome setup
  }

  /// <summary>Helper: configure a standard <see cref="T:Microsoft.Xna.Framework.Graphics.BasicEffect"/> with typical parameters (World/View/Proj).</summary>
  /// <remarks>This restores the default behavior of previous versions.</remarks>
  let inline withBasicEffect(b: Draw3DBuilder) =
    b
    |> withEffect(fun effect ctx ->
      match effect with
      | :? BasicEffect as be ->
        be.World <- ctx.World
        be.View <- ctx.View
        be.Projection <- ctx.Projection
        StandardEffects.defaultLighting be
      | _ -> ())

  /// <summary>Submits the draw command to the renderer's buffer.</summary>
  let inline submit (buffer: RenderBuffer<RenderCmd3D>) (b: Draw3DBuilder) =
    buffer.Add(
      (),
      DrawMesh(b.Pass, b.Model, b.Transform, b.Color, b.Texture, b.Setup)
    )

  /// <summary>Submits a camera change command to the buffer.</summary>
  let inline camera (cam: Camera) (buffer: RenderBuffer<RenderCmd3D>) =
    buffer.Add((), SetCamera cam)

  /// <summary>Set viewport for multi-camera rendering (split-screen, minimaps, etc).</summary>
  let inline viewport (vp: Viewport) (buffer: RenderBuffer<RenderCmd3D>) =
    buffer.Add((), SetViewport vp)

  /// <summary>Clear color and/or depth buffer. Use between cameras in multi-camera setups.</summary>
  let inline clear
    (color: Color voption)
    (clearDepth: bool)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), ClearTarget(color, clearDepth))

  /// <summary>Submits a custom drawing command to the buffer.</summary>
  let inline custom
    (draw: GameContext * Matrix * Matrix -> unit)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawCustom draw)

  /// <summary>Submits a skinned model draw command to the buffer.</summary>
  let inline skinned
    (pass: RenderPass)
    (model: Model)
    (transform: Matrix)
    (bones: Matrix[])
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSkinned(
        pass,
        model,
        transform,
        bones,
        ValueNone,
        ValueNone,
        ValueNone
      )
    )

  let inline skinnedWithColor
    (pass: RenderPass)
    (color: Color)
    (model: Model)
    (transform: Matrix)
    (bones: Matrix[])
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSkinned(
        pass,
        model,
        transform,
        bones,
        ValueSome color,
        ValueNone,
        ValueNone
      )
    )

  // --- Sprite3D helpers (90% path) ---

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

  /// <summary>Draw a textured quad using the built-in unlit Sprite3D pipeline.</summary>
  let inline quad
    (texture: Texture2D)
    (quad: Quad3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSpriteQuad {
        Pass = Opaque
        Texture = texture
        Quad = quad
      }
    )

  /// <summary>Draw a textured quad (transparent pass) using the built-in unlit Sprite3D pipeline.</summary>
  let inline quadTransparent
    (texture: Texture2D)
    (quad: Quad3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSpriteQuad {
        Pass = Transparent
        Texture = texture
        Quad = quad
      }
    )

  /// <summary>Draw a camera-facing billboard using the built-in unlit Sprite3D pipeline.</summary>
  let inline billboard
    (texture: Texture2D)
    (billboard: Billboard3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSpriteBillboard {
        Pass = Transparent
        Texture = texture
        Billboard = billboard
      }
    )

  /// <summary>Draw a billboard in the opaque pass using the built-in unlit Sprite3D pipeline.</summary>
  let inline billboardOpaque
    (texture: Texture2D)
    (billboard: Billboard3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawSpriteBillboard {
        Pass = Opaque
        Texture = texture
        Billboard = billboard
      }
    )

  // --- Effect-driven helpers (advanced path) ---

  /// <summary>Draw a quad using a custom effect. Setup is invoked for this command (View/Proj provided).</summary>
  let inline quadEffect
    (pass: RenderPass)
    (effect: Effect)
    (setup: EffectSetup voption)
    (quad: Quad3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawQuadEffect {
        Pass = pass
        Effect = effect
        Setup = setup
        Quad = quad
      }
    )

  /// <summary>Draw a billboard using a custom effect. Setup is invoked for this command (View/Proj provided).</summary>
  let inline billboardEffect
    (pass: RenderPass)
    (effect: Effect)
    (setup: EffectSetup voption)
    (billboard: Billboard3D)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add(
      (),
      DrawBillboardEffect {
        Pass = pass
        Effect = effect
        Setup = setup
        Billboard = billboard
      }
    )

  // --- Line helpers ---

  /// <summary>Draw a single line segment using the built-in unlit line pipeline.</summary>
  let inline line
    (p1: Vector3)
    (p2: Vector3)
    (color: Color)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawLine(struct (p1, p2, color), Opaque))

  /// <summary>Draw a single line segment (transparent pass) using the built-in unlit line pipeline.</summary>
  let inline lineTransparent
    (p1: Vector3)
    (p2: Vector3)
    (color: Color)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawLine(struct (p1, p2, color), Transparent))

  /// <summary>Draw multiple line segments using the built-in unlit line pipeline.</summary>
  let inline lines
    (verts: VertexPositionColor[])
    (lineCount: int)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawLines(verts, lineCount, Opaque))

  /// <summary>Draw multiple line segments (transparent pass) using the built-in unlit line pipeline.</summary>
  let inline linesTransparent
    (verts: VertexPositionColor[])
    (lineCount: int)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawLines(verts, lineCount, Transparent))

  /// <summary>Draw multiple line segments using a custom effect.</summary>
  let inline linesEffect
    (pass: RenderPass)
    (effect: Effect)
    (setup: EffectSetup voption)
    (verts: VertexPositionColor[])
    (lineCount: int)
    (buffer: RenderBuffer<RenderCmd3D>)
    =
    buffer.Add((), DrawLinesEffect(verts, lineCount, effect, setup, pass))
