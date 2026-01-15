namespace Mibo.Rendering.Graphics3D

open System.Collections.Generic
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Render Pipeline
// ============================================================================

/// Render pipeline interface
type IRenderPipeline =
  abstract member Initialize: GraphicsDevice -> unit

  abstract member Render:
    GameContext * RenderBuffer<unit, RenderCommand> -> unit

/// Pipeline state container
type internal PipelineState = {
  Config: PipelineConfig
  mutable Device: GraphicsDevice
  mutable RtPool: IRenderTargetPool
  mutable BasicEffect: BasicEffect
  CustomShaders: Dictionary<ShaderBase, Effect>
  ShadowMaps: ResizeArray<RenderTarget2D>
  mutable CurrentCamera: Mibo.Rendering.Graphics3D.Camera
  mutable CurrentLighting: LightingState
  mutable CameraWasSet: bool
  // Draw batching for sorting
  OpaqueDrawables: ResizeArray<struct (float32 * Drawable)> // (distance, drawable)
  TransparentDrawables: ResizeArray<struct (float32 * Drawable)>
}

// ============================================================================
// Shared - Utilities used by all render modes
// ============================================================================

module private Shared =

  let createState(config: PipelineConfig) : PipelineState = {
    Config = config
    Device = Unchecked.defaultof<_>
    RtPool = Unchecked.defaultof<_>
    BasicEffect = Unchecked.defaultof<_>
    CustomShaders = Dictionary<ShaderBase, Effect>()
    ShadowMaps = ResizeArray<RenderTarget2D>()
    CurrentCamera = Camera.identity
    CurrentLighting = Lighting.ambient
    CameraWasSet = false
    OpaqueDrawables = ResizeArray<struct (float32 * Drawable)>(256)
    TransparentDrawables = ResizeArray<struct (float32 * Drawable)>(64)
  }

  let resetFrameState(state: PipelineState) =
    state.CurrentCamera <- Camera.identity

    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    state.CameraWasSet <- false
    state.OpaqueDrawables.Clear()
    state.TransparentDrawables.Clear()

  let isVisible
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    : bool =
    let frustum = BoundingFrustum(camera.View * camera.Projection)
    frustum.Contains(drawable.BoundingSphere) <> ContainmentType.Disjoint

  let distanceToCamera
    (camera: Mibo.Rendering.Graphics3D.Camera)
    (drawable: Drawable)
    : float32 =
    Vector3.DistanceSquared(camera.Position, drawable.BoundingSphere.Center)

  let isTransparent(drawable: Drawable) : bool =
    drawable.Material.Flags.HasFlag(MaterialFlags.Transparent)
    || drawable.Material.PBR.AlbedoColor.A < 255uy

  let configureBasicEffectLighting
    (effect: BasicEffect)
    (lighting: LightingState)
    =
    effect.LightingEnabled <- true

    effect.AmbientLightColor <-
      lighting.AmbientColor.ToVector3() * lighting.AmbientIntensity

    // Disable all lights first
    effect.DirectionalLight0.Enabled <- false
    effect.DirectionalLight1.Enabled <- false
    effect.DirectionalLight2.Enabled <- false

    // Configure up to 3 directional lights
    let mutable lightIndex = 0

    for light in lighting.Lights do
      if lightIndex < 3 then
        match light with
        | Directional dl ->
          let beLight =
            match lightIndex with
            | 0 -> effect.DirectionalLight0
            | 1 -> effect.DirectionalLight1
            | _ -> effect.DirectionalLight2

          beLight.Enabled <- true
          beLight.Direction <- dl.Direction
          beLight.DiffuseColor <- dl.Color.ToVector3() * dl.Intensity
          beLight.SpecularColor <- Vector3.Zero
          lightIndex <- lightIndex + 1
        | Point _
        | Spot _ ->
          // BasicEffect doesn't support point/spot lights
          ()

  let configureBasicEffectCamera
    (effect: BasicEffect)
    (camera: Mibo.Rendering.Graphics3D.Camera)
    =
    effect.View <- camera.View
    effect.Projection <- camera.Projection

  let renderDrawableBasicEffect (state: PipelineState) (drawable: Drawable) =
    let mesh = drawable.Mesh
    state.Device.SetVertexBuffer(mesh.VertexBuffer)
    state.Device.Indices <- mesh.IndexBuffer

    state.BasicEffect.World <- drawable.Transform

    state.BasicEffect.DiffuseColor <-
      drawable.Material.PBR.AlbedoColor.ToVector3()

    state.BasicEffect.Alpha <-
      float32 drawable.Material.PBR.AlbedoColor.A / 255f

    match drawable.Material.PBR.AlbedoMap with
    | ValueSome tex ->
      state.BasicEffect.TextureEnabled <- true
      state.BasicEffect.Texture <- tex
    | ValueNone -> state.BasicEffect.TextureEnabled <- false

    for pass in state.BasicEffect.CurrentTechnique.Passes do
      pass.Apply()

      state.Device.DrawIndexedPrimitives(
        PrimitiveType.TriangleList,
        0,
        0,
        mesh.IndexCount / 3
      )

// ============================================================================
// Forward - Forward rendering implementation
// ============================================================================

module internal Forward =

  // --- Core batch operations (no external dependencies) ---

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      () // Nothing to flush
    else
      // Sort opaque: front-to-back (ascending distance = less overdraw)
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      // Sort transparent: back-to-front (descending distance = correct blending)
      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // Render opaque first (with depth write)
      state.Device.DepthStencilState <- DepthStencilState.Default

      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        Shared.renderDrawableBasicEffect state drawable

      // Render transparent (with alpha blending, depth read but no write)
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          Shared.renderDrawableBasicEffect state drawable
        // Restore defaults
        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default

      // Clear batches for next camera pass
      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  let batchDrawable (state: PipelineState) (drawable: Drawable) =
#if DEBUG
    if not state.CameraWasSet then
      System.Diagnostics.Debug.WriteLine(
        "[Pipeline] Warning: Draw called without SetCamera"
      )
#endif

    // Frustum culling
    if not(Shared.isVisible state.CurrentCamera drawable) then
      () // Culled, do nothing
    else
      let distance = Shared.distanceToCamera state.CurrentCamera drawable

      if Shared.isTransparent drawable then
        state.TransparentDrawables.Add(struct (distance, drawable))
      else
        state.OpaqueDrawables.Add(struct (distance, drawable))

  // --- State commands (call flushDrawBatch before changing state) ---

  let processSetCamera
    (state: PipelineState)
    (camera: Mibo.Rendering.Graphics3D.Camera)
    =
    flushDrawBatch state
    state.CurrentCamera <- camera
    state.CameraWasSet <- true
    Shared.configureBasicEffectCamera state.BasicEffect camera
    Shared.configureBasicEffectLighting state.BasicEffect state.CurrentLighting

  let processSetLighting (state: PipelineState) (lighting: LightingState) =
    state.CurrentLighting <- lighting
    Shared.configureBasicEffectLighting state.BasicEffect lighting

  let processSetViewport (state: PipelineState) (viewport: Viewport) =
    flushDrawBatch state
    state.Device.Viewport <- viewport

  let processClearTarget
    (state: PipelineState)
    (colorOpt: Color voption)
    (clearDepth: bool)
    =
    flushDrawBatch state

    let flags =
      match colorOpt, clearDepth with
      | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
      | ValueSome _, false -> ClearOptions.Target
      | ValueNone, true -> ClearOptions.DepthBuffer
      | ValueNone, false -> ClearOptions.Target

    let color = colorOpt |> ValueOption.defaultValue Color.Black
    state.Device.Clear(flags, color, 1f, 0)

  let processCustomDraw
    (state: PipelineState)
    (drawFn: GraphicsDevice -> Mibo.Rendering.Graphics3D.Camera -> unit)
    =
    flushDrawBatch state
    drawFn state.Device state.CurrentCamera

  // --- Command dispatch ---

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match cmd with
    | SetCamera camera -> processSetCamera state camera
    | SetLighting lighting -> processSetLighting state lighting
    | SetViewport viewport -> processSetViewport state viewport
    | ClearTarget(colorOpt, clearDepth) ->
      processClearTarget state colorOpt clearDepth
    | Draw drawable -> batchDrawable state drawable
    | DrawCustom drawFn -> processCustomDraw state drawFn

  // --- Main render entry point ---

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    Shared.resetFrameState state

    // Process all commands
    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    // Flush any remaining draws
    flushDrawBatch state

    state.RtPool.ReleaseAll()

// ============================================================================
// ForwardPlus - Forward+ rendering (NOT IMPLEMENTED)
// ============================================================================

module internal ForwardPlus =

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    failwith "ForwardPlus rendering mode is not yet implemented"

// ============================================================================
// Deferred - Deferred rendering (NOT IMPLEMENTED)
// ============================================================================

module internal Deferred =

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    failwith "Deferred rendering mode is not yet implemented"

// ============================================================================
// Orchestrate - Dispatches to the correct render mode
// ============================================================================

module internal Orchestrate =

  let inline render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    match state.Config.Mode with
    | PipelineMode.Forward -> Forward.render state buffer
    | PipelineMode.ForwardPlus -> ForwardPlus.render state buffer
    | PipelineMode.Deferred -> Deferred.render state buffer

  let inline initialize
    (state: PipelineState)
    (game: Game)
    (gd: GraphicsDevice)
    =
    state.Device <- gd
    state.RtPool <- RenderTargetPool.create gd

    // BasicEffect as fallback (all modes can use this)
    state.BasicEffect <- new BasicEffect(gd)
    state.BasicEffect.EnableDefaultLighting()

    // Set default lighting from config
    state.CurrentLighting <-
      state.Config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

    // Load custom shaders from content
    for KeyValue(shaderBase, assetName) in state.Config.ShaderOverrides do
      state.CustomShaders.[shaderBase] <- game.Content.Load<Effect>(assetName)

    // Pre-allocate shadow maps (only if shadow shader available)
    if state.CustomShaders.ContainsKey(ShaderBase.ShadowCaster) then
      state.Config.Shadows
      |> ValueOption.iter(fun cfg ->
        for _ in 0 .. cfg.CascadeCount - 1 do
          state.ShadowMaps.Add(
            new RenderTarget2D(
              gd,
              cfg.Resolution,
              cfg.Resolution,
              false,
              SurfaceFormat.Single,
              DepthFormat.Depth24
            )
          ))

// ============================================================================
// RenderPipeline - Public API
// ============================================================================

module RenderPipeline =

  /// Create a rendering pipeline based on configuration
  let create (config: PipelineConfig) (game: Game) : IRenderPipeline =
    let state = Shared.createState config

    { new IRenderPipeline with
        member _.Initialize(gd) = Orchestrate.initialize state game gd
        member _.Render(_, buffer) = Orchestrate.render state buffer
    }

  /// Create a forward rendering pipeline (convenience)
  let inline forward (config: PipelineConfig) (game: Game) : IRenderPipeline =
    create
      {
        config with
            Mode = PipelineMode.Forward
      }
      game
