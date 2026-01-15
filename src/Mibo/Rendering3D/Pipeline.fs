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

module RenderPipeline =

  // Type alias for our camera (to avoid conflict with Mibo.Elmish.Camera)
  type private RenderCamera = Mibo.Rendering.Graphics3D.Camera

  /// Create a forward rendering pipeline
  let forward (config: PipelineConfig) (game: Game) : IRenderPipeline =
    // Closure state
    let mutable device: GraphicsDevice = Unchecked.defaultof<_>
    let mutable rtPool: IRenderTargetPool = Unchecked.defaultof<_>
    let mutable basicEffect: BasicEffect = Unchecked.defaultof<_>
    let customShaders = Dictionary<ShaderBase, Effect>()
    let shadowMaps = ResizeArray<RenderTarget2D>()

    // Mutable render state
    let mutable currentCamera: RenderCamera = Camera.identity
    let mutable currentLighting: LightingState = Lighting.ambient

    // Helpers for BasicEffect rendering
    let configureBasicEffect() =
      basicEffect.View <- currentCamera.View
      basicEffect.Projection <- currentCamera.Projection
      basicEffect.LightingEnabled <- true

      basicEffect.AmbientLightColor <-
        currentLighting.AmbientColor.ToVector3()
        * currentLighting.AmbientIntensity

      // Configure up to 3 directional lights
      let mutable lightIndex = 0

      for light in currentLighting.Lights do
        if lightIndex < 3 then
          match light with
          | Directional dl ->
            let beLight =
              match lightIndex with
              | 0 -> basicEffect.DirectionalLight0
              | 1 -> basicEffect.DirectionalLight1
              | _ -> basicEffect.DirectionalLight2

            beLight.Enabled <- true
            beLight.Direction <- dl.Direction
            beLight.DiffuseColor <- dl.Color.ToVector3() * dl.Intensity
            beLight.SpecularColor <- Vector3.Zero
            lightIndex <- lightIndex + 1
          | Point _
          | Spot _ ->
            // BasicEffect doesn't support point/spot lights
            ()

    let renderDrawable(drawable: Drawable) =
      let mesh = drawable.Mesh
      device.SetVertexBuffer(mesh.VertexBuffer)
      device.Indices <- mesh.IndexBuffer

      basicEffect.World <- drawable.Transform
      basicEffect.DiffuseColor <- drawable.Material.PBR.AlbedoColor.ToVector3()
      basicEffect.Alpha <- float32 drawable.Material.PBR.AlbedoColor.A / 255f

      match drawable.Material.PBR.AlbedoMap with
      | ValueSome tex ->
        basicEffect.TextureEnabled <- true
        basicEffect.Texture <- tex
      | ValueNone -> basicEffect.TextureEnabled <- false

      for pass in basicEffect.CurrentTechnique.Passes do
        pass.Apply()

        device.DrawIndexedPrimitives(
          PrimitiveType.TriangleList,
          0,
          0,
          mesh.IndexCount / 3
        )

    let processCommand(cmd: RenderCommand) =
      match cmd with
      | SetCamera camera ->
        currentCamera <- camera
        configureBasicEffect()

      | SetViewport viewport -> device.Viewport <- viewport

      | ClearTarget(colorOpt, clearDepth) ->
        let flags =
          match colorOpt, clearDepth with
          | ValueSome _, true ->
            ClearOptions.Target ||| ClearOptions.DepthBuffer
          | ValueSome _, false -> ClearOptions.Target
          | ValueNone, true -> ClearOptions.DepthBuffer
          | ValueNone, false -> ClearOptions.Target // No-op but need something

        let color = colorOpt |> ValueOption.defaultValue Color.Black
        device.Clear(flags, color, 1f, 0)

      | Draw drawable -> renderDrawable drawable

      | DrawCustom drawFn -> drawFn device currentCamera

    { new IRenderPipeline with
        member _.Initialize(gd) =
          device <- gd
          rtPool <- RenderTargetPool.create gd

          // BasicEffect as fallback
          basicEffect <- new BasicEffect(gd)
          basicEffect.EnableDefaultLighting()

          // Set default lighting from config
          currentLighting <-
            config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

          // Load custom shaders from content
          for KeyValue(shaderBase, assetName) in config.ShaderOverrides do
            customShaders.[shaderBase] <- game.Content.Load<Effect>(assetName)

          // Pre-allocate shadow maps (only if shadow shader available)
          if customShaders.ContainsKey(ShaderBase.ShadowCaster) then
            config.Shadows
            |> ValueOption.iter(fun cfg ->
              for _ in 0 .. cfg.CascadeCount - 1 do
                shadowMaps.Add(
                  new RenderTarget2D(
                    gd,
                    cfg.Resolution,
                    cfg.Resolution,
                    false,
                    SurfaceFormat.Single,
                    DepthFormat.Depth24
                  )
                ))

        member _.Render(ctx, buffer) =
          // Reset to defaults for this frame
          currentCamera <- Camera.identity

          currentLighting <-
            config.DefaultLighting |> ValueOption.defaultValue Lighting.ambient

          // Process all commands in order
          for i in 0 .. buffer.Count - 1 do
            let struct (_, cmd) = buffer.[i]
            processCommand cmd

          rtPool.ReleaseAll()
    }
