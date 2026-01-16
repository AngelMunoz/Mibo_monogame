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

module internal Shared =

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

  /// Batch a drawable into the appropriate list (opaque or transparent)
  /// Performs frustum culling.
  let batchDrawable (state: PipelineState) (drawable: Drawable) =
#if DEBUG
    if not state.CameraWasSet then
      System.Diagnostics.Debug.WriteLine(
        "[Pipeline] Warning: Draw called without SetCamera"
      )
#endif

    // Frustum culling
    if not(isVisible state.CurrentCamera drawable) then
      () // Culled, do nothing
    else
      let distance = distanceToCamera state.CurrentCamera drawable

      if isTransparent drawable then
        state.TransparentDrawables.Add(struct (distance, drawable))
      else
        state.OpaqueDrawables.Add(struct (distance, drawable))

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
    | Draw drawable -> Shared.batchDrawable state drawable
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

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()

// ============================================================================
// ForwardPlus - Forward+ rendering
// ============================================================================

module internal ForwardPlus =
  // Forward+ is similar to Forward but handles many lights via light culling.
  // In this implementation, we'll reuse the structure of Forward but prepare
  // for when we have the specific effects.
  // For now, it falls back to BasicEffect if no PBR/ForwardPlus shader is present.

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      ()
    else
      // 1. Sort
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))

      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // 2. Light Culling (Conceptually here)
      // In a real implementation: Compute shader to cull lights into frustums/tiles.
      // state.LightGrid <- CullLights(state.CurrentLighting, state.CurrentCamera)

      // 3. Depth Pre-pass (Optional optimization)
      // state.Device.DepthStencilState <- DepthStencilState.Default
      // for opaque in state.OpaqueDrawables: RenderDepthOnly(opaque)

      // 4. Render Opaque
      state.Device.DepthStencilState <- DepthStencilState.Default
      // Use equal depth if we did a pre-pass

      // Try to find a Forward+ capable shader
      match state.CustomShaders.TryGetValue(ShaderBase.PBRForward) with
      | true, effect ->
        // Bind frame-global data (Camera, Lights, LightGrid)
        // effect.Parameters.["View"].SetValue(state.CurrentCamera.View)
        // ...
        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]
          // Render with PBR effect
          // Bind per-object data (World, Material)
          Shared.renderDrawableBasicEffect state drawable // Fallback for now
      | false, _ ->
        // Fallback to BasicEffect
        for i in 0 .. state.OpaqueDrawables.Count - 1 do
          let struct (_, drawable) = state.OpaqueDrawables.[i]
          Shared.renderDrawableBasicEffect state drawable

      // 5. Render Transparent
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend
        state.Device.DepthStencilState <- DepthStencilState.DepthRead

        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          Shared.renderDrawableBasicEffect state drawable

        state.Device.BlendState <- BlendState.Opaque
        state.Device.DepthStencilState <- DepthStencilState.Default

      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    // Reusing Forward's logic for command processing, but calling our flush
    match cmd with
    | SetCamera camera ->
      flushDrawBatch state
      state.CurrentCamera <- camera
      state.CameraWasSet <- true
      Shared.configureBasicEffectCamera state.BasicEffect camera

      Shared.configureBasicEffectLighting
        state.BasicEffect
        state.CurrentLighting
    | SetLighting lighting ->
      state.CurrentLighting <- lighting
      Shared.configureBasicEffectLighting state.BasicEffect lighting
    | SetViewport viewport ->
      flushDrawBatch state
      state.Device.Viewport <- viewport
    | ClearTarget(colorOpt, clearDepth) ->
      flushDrawBatch state
      // Same clear logic
      let flags =
        match colorOpt, clearDepth with
        | ValueSome _, true -> ClearOptions.Target ||| ClearOptions.DepthBuffer
        | ValueSome _, false -> ClearOptions.Target
        | ValueNone, true -> ClearOptions.DepthBuffer
        | ValueNone, false -> ClearOptions.Target

      let color = colorOpt |> ValueOption.defaultValue Color.Black
      state.Device.Clear(flags, color, 1f, 0)
    | Draw drawable -> Shared.batchDrawable state drawable
    | DrawCustom drawFn ->
      flushDrawBatch state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    Shared.resetFrameState state

    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    flushDrawBatch state

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()

// ============================================================================
// Deferred - Deferred rendering implementation
// ============================================================================

module internal Deferred =

  let renderGBuffer(state: PipelineState) =
    // Setup G-Buffer RenderTargets
    // RT0: Albedo (RGB) + Roughness (A)
    // RT1: Normal (RGB) + Metallic (A)
    // RT2: Depth (Linear) or use Hardware Depth

    // For now, mock implementation since we don't have the GBufferFill shader
    // In a real impl, we would acquire RTs from pool

    // let rtAlbedo = state.RtPool.Acquire { Width = ...; Format = Color ... }
    // let rtNormal = state.RtPool.Acquire { ... }
    // state.Device.SetRenderTargets(rtAlbedo, rtNormal)

    // Just clear for now to simulating pass
    state.Device.Clear(Color.Transparent)

    match state.CustomShaders.TryGetValue(ShaderBase.GBufferFill) with
    | true, effect ->
      // Render all opaque objects to G-Buffer
      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        // effect.Parameters["World"].SetValue(drawable.Transform)
        // Draw...
        ()
    | false, _ ->
      // Fallback: If we don't have G-Buffer shader, we can't do deferred.
      // We might just render Forward as fallback or do nothing.
      // For this exercise, let's just render using BasicEffect to the backbuffer
      // effectively falling back to forward so something shows up.
      for i in 0 .. state.OpaqueDrawables.Count - 1 do
        let struct (_, drawable) = state.OpaqueDrawables.[i]
        Shared.renderDrawableBasicEffect state drawable

  let renderLighting(state: PipelineState) =
    // 1. Resolve G-Buffer (if needed)
    // 2. Bind G-Buffer textures to Lighting shader
    // 3. Draw full screen quad for directional/ambient
    // 4. Draw light volumes for point/spot lights

    match state.CustomShaders.TryGetValue(ShaderBase.DeferredLighting) with
    | true, effect ->
      // Draw Quad
      ()
    | false, _ -> ()

  let flushDrawBatch(state: PipelineState) =
    if
      state.OpaqueDrawables.Count = 0 && state.TransparentDrawables.Count = 0
    then
      ()
    else
      // Sort
      state.OpaqueDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d1.CompareTo(d2))
      // Deferred only handles Opaque usually. Transparent is done Forward after.
      state.TransparentDrawables.Sort(fun struct (d1, _) struct (d2, _) ->
        d2.CompareTo(d1))

      // Pass 1: G-Buffer (Opaque)
      renderGBuffer state

      // Pass 2: Lighting (Opaque)
      // If we had a GBuffer, we would switch to main target and render lighting
      renderLighting state

      // Pass 3: Transparent (Forward)
      // Copy depth from G-Buffer if needed
      if state.TransparentDrawables.Count > 0 then
        state.Device.BlendState <- BlendState.AlphaBlend
        // BasicEffect or Forward Transparent Shader
        for i in 0 .. state.TransparentDrawables.Count - 1 do
          let struct (_, drawable) = state.TransparentDrawables.[i]
          Shared.renderDrawableBasicEffect state drawable

        state.Device.BlendState <- BlendState.Opaque

      state.OpaqueDrawables.Clear()
      state.TransparentDrawables.Clear()

  let processCommand (state: PipelineState) (cmd: RenderCommand) =
    match cmd with
    | SetCamera camera ->
      flushDrawBatch state
      state.CurrentCamera <- camera
      state.CameraWasSet <- true
      Shared.configureBasicEffectCamera state.BasicEffect camera
    | SetLighting lighting -> state.CurrentLighting <- lighting
    | SetViewport viewport ->
      flushDrawBatch state
      state.Device.Viewport <- viewport
    | ClearTarget(colorOpt, clearDepth) ->
      flushDrawBatch state
      let color = colorOpt |> ValueOption.defaultValue Color.Black

      state.Device.Clear(
        ClearOptions.Target ||| ClearOptions.DepthBuffer,
        color,
        1f,
        0
      )
    | Draw drawable -> Shared.batchDrawable state drawable
    | DrawCustom drawFn ->
      flushDrawBatch state
      drawFn state.Device state.CurrentCamera

  let render
    (state: PipelineState)
    (buffer: RenderBuffer<unit, RenderCommand>)
    =
    Shared.resetFrameState state

    for i in 0 .. buffer.Count - 1 do
      let struct (_, cmd) = buffer.[i]
      processCommand state cmd

    flushDrawBatch state

    if not(isNull(box state.RtPool)) then
      state.RtPool.ReleaseAll()


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
