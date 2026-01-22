namespace Mibo.Elmish.Graphics2D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open FSharp.UMX
open Mibo.Elmish

/// <summary>Unit of measure for render layer ordering.</summary>
/// <remarks>Lower values are drawn first (background), higher values drawn last (foreground).</remarks>
[<Measure>]
type RenderLayer

/// <summary>Convenience alias for a render buffer keyed by <see cref="T:Mibo.Elmish.Graphics2D.RenderLayer"/>.</summary>
/// <remarks>This preserves the simple <c>RenderBuffer&lt;'Cmd&gt;</c> API at call sites while the core buffer remains generic (<see cref="T:Mibo.Elmish.RenderBuffer`2"/>).</remarks>
type RenderBuffer<'Cmd> = RenderBuffer<int<RenderLayer>, 'Cmd>

/// <summary>Struct for text draw command parameters.</summary>
[<Struct>]
type TextDrawCmd = {
  Font: SpriteFont
  Text: string
  Position: Vector2
  Color: Color
  Rotation: float32
  Origin: Vector2
  Scale: float32
  Effects: SpriteEffects
  Depth: float32
}

/// <summary>A 2D render command.</summary>
/// <remarks>These commands are queued to a <see cref="T:Mibo.Elmish.RenderBuffer`1"/> and executed by <see cref="T:Mibo.Elmish.Graphics2D.Batch2DRenderer`1"/>.</remarks>
[<Struct>]
type RenderCmd2D =
  /// Set viewport for multi-camera rendering (split-screen, minimaps, etc).
  | SetViewport of viewport: Viewport

  /// Clear the render target. Use between cameras in multi-camera setups.
  | ClearTarget of clearColor: Color voption * clearDepth: bool

  /// Changes the camera transform for subsequent draws.
  | SetCamera of camera: Camera

  /// Set the SpriteBatch effect for subsequent draws.
  ///
  /// ValueNone means "use the renderer's configured default".
  | SetEffect of effect: Effect voption

  /// Set SpriteBatch blend state for subsequent draws.
  | SetBlendState of blendState: BlendState

  /// Set SpriteBatch sampler state for subsequent draws.
  | SetSamplerState of samplerState: SamplerState

  /// Set SpriteBatch depth-stencil state for subsequent draws.
  | SetDepthStencilState of depthStencilState: DepthStencilState

  /// Set SpriteBatch rasterizer state for subsequent draws.
  | SetRasterizerState of rasterizerState: RasterizerState

  /// Escape hatch: run an arbitrary draw function.
  ///
  /// The function is invoked outside of SpriteBatch (SpriteBatch is ended before calling it).
  | DrawCustom of draw: (GameContext -> unit)

  /// Draws a textured quad.
  | DrawTexture of
    texture: Texture2D *
    dest: Rectangle *
    source: Nullable<Rectangle> *
    color: Color *
    rotation: float32 *
    origin: Vector2 *
    effects: SpriteEffects *
    depth: float32

  /// Draws text using a SpriteFont.
  | DrawText of textCmd: TextDrawCmd

/// <summary>Configuration for <see cref="T:Mibo.Elmish.Graphics2D.Batch2DRenderer`1"/>.</summary>
/// <remarks>These settings configure the *rendering pass* (Clear + SpriteBatch.Begin parameters), not individual sprites (those are controlled by <see cref="T:Mibo.Elmish.Graphics2D.RenderCmd2D"/> / <see cref="T:Mibo.Elmish.Graphics2D.Draw2DBuilder"/>).</remarks>
[<Struct>]
type Batch2DConfig = {
  /// Optional color to clear the screen with before drawing.
  ClearColor: Color voption
  /// Whether to sort the command buffer by `RenderLayer` before issuing draws.
  /// Keep this enabled if you rely on `RenderLayer` for deterministic ordering.
  SortCommands: bool
  /// SpriteBatch sort mode (Deferred, Immediate, etc).
  SortMode: SpriteSortMode
  /// Blend state for sprite drawing.
  BlendState: BlendState
  /// Sampler state for texture filtering.
  SamplerState: SamplerState
  /// Depth stencil state.
  DepthStencilState: DepthStencilState
  /// Rasterizer state.
  RasterizerState: RasterizerState
  /// Optional shader effect for all sprites.
  Effect: Effect
  /// Global transform matrix for the batch.
  /// Note: If using `SetCamera` commands, this initial matrix might be overridden during the pass.
  TransformMatrix: Matrix voption
}

module Batch2DConfig =

  /// Sensible defaults for a typical 2D game.
  /// Matches the current historical behavior of this renderer (clears CornflowerBlue).
  let defaults: Batch2DConfig = {
    ClearColor = ValueSome Color.CornflowerBlue
    SortCommands = true
    SortMode = SpriteSortMode.Deferred
    BlendState = BlendState.AlphaBlend
    SamplerState = SamplerState.LinearClamp
    DepthStencilState = DepthStencilState.None
    RasterizerState = RasterizerState.CullCounterClockwise
    Effect = null
    TransformMatrix = ValueNone
  }

/// <summary>Standard 2D Renderer using <see cref="T:Microsoft.Xna.Framework.Graphics.SpriteBatch"/>.</summary>
type Batch2DRenderer<'Model>
  (
    game: Game,
    config: Batch2DConfig,
    [<InlineIfLambda>] view:
      GameContext * 'Model * RenderBuffer<RenderCmd2D> -> unit
  ) =
  let mutable spriteBatch: SpriteBatch = null
  let buffer = RenderBuffer<RenderCmd2D>()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, gameTime: GameTime) =
      if isNull spriteBatch then
        spriteBatch <- new SpriteBatch(ctx.GraphicsDevice)

      config.ClearColor |> ValueOption.iter(fun c -> ctx.GraphicsDevice.Clear c)

      buffer.Clear()
      view(ctx, model, buffer)

      if config.SortCommands then
        buffer.Sort()

      // Current SpriteBatch state (mutable via commands)
      let mutable currentSortMode = config.SortMode
      let mutable currentBlend = config.BlendState
      let mutable currentSampler = config.SamplerState
      let mutable currentDepthStencil = config.DepthStencilState
      let mutable currentRasterizer = config.RasterizerState
      let mutable currentEffect = config.Effect

      let mutable currentTransform =
        match config.TransformMatrix with
        | ValueSome m -> Nullable m
        | ValueNone -> Nullable()

      let beginBatch() =
        spriteBatch.Begin(
          currentSortMode,
          currentBlend,
          currentSampler,
          currentDepthStencil,
          currentRasterizer,
          currentEffect,
          currentTransform
        )

      let endBatch() = spriteBatch.End()

      let mutable isBatching = true
      beginBatch()

      for i = 0 to buffer.Count - 1 do
        let struct (_, cmd) = buffer.Item(i)

        match cmd with
        | SetViewport vp ->
          if isBatching then
            endBatch()
            isBatching <- false

          ctx.GraphicsDevice.Viewport <- vp

          beginBatch()
          isBatching <- true

        | ClearTarget(colorOpt, clearDepth) ->
          if isBatching then
            endBatch()
            isBatching <- false

          match colorOpt, clearDepth with
          | ValueSome c, true ->
            ctx.GraphicsDevice.Clear(
              ClearOptions.Target ||| ClearOptions.DepthBuffer,
              c,
              1.0f,
              0
            )
          | ValueSome c, false ->
            ctx.GraphicsDevice.Clear(ClearOptions.Target, c, 1.0f, 0)
          | ValueNone, true ->
            ctx.GraphicsDevice.Clear(
              ClearOptions.DepthBuffer,
              Color.Black,
              1.0f,
              0
            )
          | ValueNone, false -> ()

          beginBatch()
          isBatching <- true

        | SetCamera cam ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentTransform <- Nullable cam.View
          beginBatch()
          isBatching <- true

        | SetEffect effectOpt ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentEffect <-
            match effectOpt with
            | ValueSome e -> e
            | ValueNone -> config.Effect

          beginBatch()
          isBatching <- true

        | SetBlendState bs ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentBlend <- bs
          beginBatch()
          isBatching <- true

        | SetSamplerState ss ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentSampler <- ss
          beginBatch()
          isBatching <- true

        | SetDepthStencilState ds ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentDepthStencil <- ds
          beginBatch()
          isBatching <- true

        | SetRasterizerState rs ->
          if isBatching then
            endBatch()
            isBatching <- false

          currentRasterizer <- rs
          beginBatch()
          isBatching <- true

        | DrawCustom draw ->
          if isBatching then
            endBatch()
            isBatching <- false

          draw ctx

          beginBatch()
          isBatching <- true

        | DrawTexture(tex, dest, src, color, rot, origin, fx, depth) ->
          if not isBatching then
            // Should not happen if logic above is correct, but safe guard
            beginBatch()

            isBatching <- true

          if src.HasValue then
            spriteBatch.Draw(
              tex,
              dest,
              src.Value,
              color,
              rot,
              origin,
              fx,
              depth
            )
          else
            spriteBatch.Draw(tex, dest, color)

        | DrawText cmd ->
          if not isBatching then
            beginBatch()
            isBatching <- true

          spriteBatch.DrawString(
            cmd.Font,
            cmd.Text,
            cmd.Position,
            cmd.Color,
            cmd.Rotation,
            cmd.Origin,
            cmd.Scale,
            cmd.Effects,
            cmd.Depth
          )

      if isBatching then
        endBatch()

module Batch2DRenderer =
  /// <summary>Creates a standard 2D renderer.</summary>
  let inline create<'Model>
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit)
    (game: Game)
    =
    Batch2DRenderer<'Model>(
      game,
      Batch2DConfig.defaults,
      fun (ctx, model, buffer) -> view ctx model buffer
    )
    :> IRenderer<'Model>

  /// <summary>Creates a 2D renderer with custom configuration.</summary>
  let inline createWithConfig<'Model>
    (config: Batch2DConfig)
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer<RenderCmd2D> -> unit)
    (game: Game)
    =
    Batch2DRenderer<'Model>(
      game,
      config,
      fun (ctx, model, buffer) -> view ctx model buffer
    )
    :> IRenderer<'Model>


/// <summary>Fluent builder for <see cref="T:Mibo.Elmish.Graphics2D.RenderCmd2D"/>.</summary>
[<Struct>]
type Draw2DBuilder = {
  Texture: Texture2D
  Dest: Rectangle
  Source: Nullable<Rectangle>
  Color: Color
  Rotation: float32
  Origin: Vector2
  Effects: SpriteEffects
  Depth: float32
  Layer: int<RenderLayer>
}

/// <summary>Functions for building and submitting 2D draw commands.</summary>
module Draw2D =
  /// <summary>Starts a sprite drawing command.</summary>
  let sprite tex dest = {
    Texture = tex
    Dest = dest
    Source = Nullable()
    Color = Color.White
    Rotation = 0.0f
    Origin = Vector2.Zero
    Effects = SpriteEffects.None
    Depth = 0.0f
    Layer = 0<RenderLayer>
  }

  let withSource src (b: Draw2DBuilder) = { b with Source = Nullable src }
  let withColor col (b: Draw2DBuilder) = { b with Color = col }
  let atLayer layer (b: Draw2DBuilder) = { b with Layer = layer }

  /// <summary>Submits the draw command to the renderer's buffer.</summary>
  let submit (buffer: RenderBuffer<RenderCmd2D>) (b: Draw2DBuilder) =
    buffer.Add(
      b.Layer,
      DrawTexture(
        b.Texture,
        b.Dest,
        b.Source,
        b.Color,
        b.Rotation,
        b.Origin,
        b.Effects,
        b.Depth
      )
    )

  /// <summary>Submits a camera change command to the buffer.</summary>
  let camera
    (cam: Camera)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetCamera cam)

  /// <summary>Submits a viewport change command to the buffer.</summary>
  let viewport
    (vp: Viewport)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetViewport vp)

  /// <summary>Clear color and/or depth buffer. Useful between cameras in multi-camera setups.</summary>
  let clear
    (color: Color voption)
    (clearDepth: bool)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, ClearTarget(color, clearDepth))

  /// <summary>Set the SpriteBatch effect for subsequent draws.</summary>
  /// <remarks>Use ValueNone to revert to the renderer's configured default.</remarks>
  let effect
    (effect: Effect voption)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetEffect effect)

  /// <summary>Set the SpriteBatch blend state for subsequent draws.</summary>
  let blendState
    (blendState: BlendState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetBlendState blendState)

  /// <summary>Set the SpriteBatch sampler state for subsequent draws.</summary>
  let samplerState
    (samplerState: SamplerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetSamplerState samplerState)

  /// <summary>Set the SpriteBatch depth-stencil state for subsequent draws.</summary>
  let depthStencilState
    (depthStencilState: DepthStencilState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetDepthStencilState depthStencilState)

  /// <summary>Set the SpriteBatch rasterizer state for subsequent draws.</summary>
  let rasterizerState
    (rasterizerState: RasterizerState)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, SetRasterizerState rasterizerState)

  /// <summary>Submits a custom drawing command to the buffer.</summary>
  /// <remarks>The SpriteBatch is ended before calling <c>draw</c>, and restarted after.</remarks>
  let custom
    (draw: GameContext -> unit)
    (layer: int<RenderLayer>)
    (buffer: RenderBuffer<RenderCmd2D>)
    =
    buffer.Add(layer, DrawCustom draw)

// ============================================================================
// Phase 1 DSL Module
// ============================================================================

/// <summary>
/// DSL for building 2D sprites and text with computation expressions and pipeline-style functions.
/// </summary>
/// <remarks>
/// This module contains all types, builders, and utilities for the declarative 2D rendering DSL.
/// Use the View2D module for global access to the computation expression builders.
/// </remarks>
module DSL =

  open System.Runtime.CompilerServices

  // --------------------------------------------------------------------------
  // Sprite Types and Builder
  // --------------------------------------------------------------------------

  /// <summary>Intermediate state for building a 2D sprite.</summary>
  [<Struct>]
  type SpriteState = {
    Texture: Texture2D voption
    DestX: int
    DestY: int
    Width: int
    Height: int
    SourceRect: Rectangle voption
    Color: Color
    Rotation: float32
    Origin: Vector2
    Effects: SpriteEffects
    Depth: float32
    Layer: int<RenderLayer>
  }

  /// <summary>Helper functions for SpriteState.</summary>
  module Sprite =
    /// <summary>Creates a default empty sprite state.</summary>
    let empty: SpriteState = {
      Texture = ValueNone
      DestX = 0
      DestY = 0
      Width = 0
      Height = 0
      SourceRect = ValueNone
      Color = Color.White
      Rotation = 0f
      Origin = Vector2.Zero
      Effects = SpriteEffects.None
      Depth = 0f
      Layer = 0<RenderLayer>
    }

    /// <summary>Creates a sprite state from a texture.</summary>
    let inline fromTexture(tex: Texture2D) : SpriteState = {
      empty with
          Texture = ValueSome tex
          Width = tex.Width
          Height = tex.Height
    }

    let inline at x y (s: SpriteState) = { s with DestX = x; DestY = y }
    let inline size w h (s: SpriteState) = { s with Width = w; Height = h }
    let inline color c (s: SpriteState) = { s with Color = c }
    let inline layer l (s: SpriteState) = { s with Layer = l }

    let inline sourceRect r (s: SpriteState) = {
      s with
          SourceRect = ValueSome r
    }

    let inline rotatedBy r (s: SpriteState) = { s with Rotation = r }
    let inline depth d (s: SpriteState) = { s with Depth = d }
    let inline origin o (s: SpriteState) = { s with Origin = o }

    let inline centered(s: SpriteState) = {
      s with
          Origin = Vector2(float32 s.Width / 2f, float32 s.Height / 2f)
    }

    let inline flippedH(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipHorizontally
    }

    let inline flippedV(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipVertically
    }

  /// <summary>Computation expression builder for sprites.</summary>
  type SpriteBuilder() =
    member inline _.Yield(_: unit) : SpriteState = Sprite.empty

    [<CustomOperation("texture")>]
    member inline _.Texture(s: SpriteState, tex: Texture2D) = {
      s with
          Texture = ValueSome tex
          Width = tex.Width
          Height = tex.Height
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, x: int, y: int) = {
      s with
          DestX = x
          DestY = y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, v: Vector2) = {
      s with
          DestX = int v.X
          DestY = int v.Y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: SpriteState, x: float32, y: float32) = {
      s with
          DestX = int x
          DestY = int y
    }

    [<CustomOperation("size")>]
    member inline _.Size(s: SpriteState, w: int, h: int) = {
      s with
          Width = w
          Height = h
    }

    [<CustomOperation("sourceRect")>]
    member inline _.SourceRect(s: SpriteState, r: Rectangle) = {
      s with
          SourceRect = ValueSome r
    }

    [<CustomOperation("color")>]
    member inline _.Color(s: SpriteState, c: Color) = { s with Color = c }

    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedBy(s: SpriteState, radians: float32) = {
      s with
          Rotation = radians
    }

    [<CustomOperation("centered")>]
    member inline _.Centered(s: SpriteState) = {
      s with
          Origin = Vector2(float32 s.Width / 2f, float32 s.Height / 2f)
    }

    [<CustomOperation("origin")>]
    member inline _.Origin(s: SpriteState, o: Vector2) = { s with Origin = o }

    [<CustomOperation("flippedH")>]
    member inline _.FlippedH(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipHorizontally
    }

    [<CustomOperation("flippedV")>]
    member inline _.FlippedV(s: SpriteState) = {
      s with
          Effects = s.Effects ||| SpriteEffects.FlipVertically
    }

    [<CustomOperation("depth")>]
    member inline _.Depth(s: SpriteState, d: float32) = { s with Depth = d }

    [<CustomOperation("layer")>]
    member inline _.Layer(s: SpriteState, l: int<RenderLayer>) = {
      s with
          Layer = l
    }

    member inline _.Run(s: SpriteState) : SpriteState = s

  // --------------------------------------------------------------------------
  // Text Types and Builder
  // --------------------------------------------------------------------------

  /// <summary>Intermediate state for building text.</summary>
  [<Struct>]
  type TextState = {
    Font: SpriteFont voption
    Text: string
    DestX: int
    DestY: int
    Color: Color
    Scale: float32
    Rotation: float32
    Origin: Vector2
    Effects: SpriteEffects
    Layer: int<RenderLayer>
  }

  /// <summary>Helper functions for TextState.</summary>
  module Text =
    /// <summary>Creates a default empty text state.</summary>
    let empty: TextState = {
      Font = ValueNone
      Text = ""
      DestX = 0
      DestY = 0
      Color = Color.White
      Scale = 1f
      Rotation = 0f
      Origin = Vector2.Zero
      Effects = SpriteEffects.None
      Layer = 0<RenderLayer>
    }

    let inline at x y (s: TextState) = { s with DestX = x; DestY = y }
    let inline color c (s: TextState) = { s with Color = c }
    let inline scale sc (s: TextState) = { s with Scale = sc }
    let inline layer l (s: TextState) = { s with Layer = l }

  /// <summary>Computation expression builder for text.</summary>
  type TextBuilder() =
    member inline _.Yield(_: unit) : TextState = Text.empty

    [<CustomOperation("font")>]
    member inline _.Font(s: TextState, f: SpriteFont) = {
      s with
          Font = ValueSome f
    }

    [<CustomOperation("content")>]
    member inline _.Content(s: TextState, t: string) = { s with Text = t }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, x: float32, y: float32) = {
      s with
          DestX = int x
          DestY = int y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, x: int, y: int) = {
      s with
          DestX = x
          DestY = y
    }

    [<CustomOperation("at")>]
    member inline _.At(s: TextState, v: Vector2) = {
      s with
          DestX = int v.X
          DestY = int v.Y
    }

    [<CustomOperation("color")>]
    member inline _.Color(s: TextState, c: Color) = { s with Color = c }

    [<CustomOperation("scale")>]
    member inline _.Scale(s: TextState, sc: float32) = { s with Scale = sc }

    [<CustomOperation("rotatedBy")>]
    member inline _.RotatedBy(s: TextState, radians: float32) = {
      s with
          Rotation = radians
    }

    [<CustomOperation("origin")>]
    member inline _.Origin(s: TextState, o: Vector2) = { s with Origin = o }

    [<CustomOperation("layer")>]
    member inline _.Layer(s: TextState, l: int<RenderLayer>) = {
      s with
          Layer = l
    }

    member inline _.Run(s: TextState) : TextState = s

  // --------------------------------------------------------------------------
  // Buffer Extensions
  // --------------------------------------------------------------------------

  /// <summary>Fluent extension methods for RenderBuffer.</summary>
  [<Extension>]
  type RenderBuffer2DExtensions =

    [<Extension>]
    static member inline Sprite
      (this: RenderBuffer<RenderCmd2D>, s: SpriteState)
      =
      s.Texture
      |> ValueOption.iter(fun tex ->
        let dest = Rectangle(s.DestX, s.DestY, s.Width, s.Height)

        let source = s.SourceRect |> ValueOption.toNullable

        this.Add(
          s.Layer,
          DrawTexture(
            tex,
            dest,
            source,
            s.Color,
            s.Rotation,
            s.Origin,
            s.Effects,
            s.Depth
          )
        ))

      this

    [<Extension>]
    static member inline Sprite
      (this: RenderBuffer<RenderCmd2D>, tex: Texture2D, x: int, y: int)
      =
      let w, h =
        (if isNull tex then 0 else tex.Width),
        (if isNull tex then 0 else tex.Height)

      this.Add(
        0<RenderLayer>,
        DrawTexture(
          tex,
          Rectangle(x, y, w, h),
          Nullable(),
          Color.White,
          0f,
          Vector2.Zero,
          SpriteEffects.None,
          0f
        )
      )

      this

    [<Extension>]
    static member inline Text(this: RenderBuffer<RenderCmd2D>, t: TextState) =
      t.Font
      |> ValueOption.iter(fun font ->
        this.Add(
          t.Layer,
          DrawText {
            Font = font
            Text = t.Text
            Position = Vector2(float32 t.DestX, float32 t.DestY)
            Color = t.Color
            Rotation = t.Rotation
            Origin = t.Origin
            Scale = t.Scale
            Effects = t.Effects
            Depth = 0f
          }
        ))

      this

    [<Extension>]
    static member inline Text
      (
        this: RenderBuffer<RenderCmd2D>,
        font: SpriteFont,
        text: string,
        x: int,
        y: int
      ) =
      this.Add(
        0<RenderLayer>,
        DrawText {
          Font = font
          Text = text
          Position = Vector2(float32 x, float32 y)
          Color = Color.White
          Rotation = 0f
          Origin = Vector2.Zero
          Scale = 1f
          Effects = SpriteEffects.None
          Depth = 0f
        }
      )

      this

    [<Extension>]
    static member inline Camera
      (this: RenderBuffer<RenderCmd2D>, cam: Camera, ?layer: int<RenderLayer>)
      =
      this.Add(defaultArg layer 0<RenderLayer>, SetCamera cam)
      this

    [<Extension>]
    static member inline Clear(this: RenderBuffer<RenderCmd2D>, color: Color) =
      this.Add(0<RenderLayer>, ClearTarget(ValueSome color, false))
      this

    [<Extension>]
    static member inline BlendState
      (this: RenderBuffer<RenderCmd2D>, bs: BlendState, ?layer: int<RenderLayer>) =
      this.Add(defaultArg layer 0<RenderLayer>, SetBlendState bs)
      this

    [<Extension>]
    static member inline Effect
      (
        this: RenderBuffer<RenderCmd2D>,
        effect: Effect voption,
        ?layer: int<RenderLayer>
      ) =
      this.Add(defaultArg layer 0<RenderLayer>, SetEffect effect)
      this

    [<Extension>]
    static member inline Submit(this: RenderBuffer<RenderCmd2D>) = ()

  // --------------------------------------------------------------------------
  // Pipeline-Style Functions
  // --------------------------------------------------------------------------

  /// <summary>Pipeline-style functions for buffer operations.</summary>
  module Buffer2D =
    let inline sprite s (buffer: RenderBuffer<RenderCmd2D>) = buffer.Sprite(s)
    let inline text t (buffer: RenderBuffer<RenderCmd2D>) = buffer.Text(t)

    let inline camera cam (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Camera(cam)

    let inline clear color (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.Clear(color)

    let inline blendState bs (buffer: RenderBuffer<RenderCmd2D>) =
      buffer.BlendState(bs)

    let inline effect fx (buffer: RenderBuffer<RenderCmd2D>) = buffer.Effect(fx)
    let inline submit(buffer: RenderBuffer<RenderCmd2D>) = buffer.Submit()

  // --------------------------------------------------------------------------
  // Global Builder Instances
  // --------------------------------------------------------------------------

  /// <summary>Global computation expression builders.</summary>
  [<AutoOpen>]
  module View2D =
    /// <summary>Builder for sprites. Usage: sprite { texture tex; at x y; ... }</summary>
    let sprite = SpriteBuilder()

    /// <summary>Builder for text. Usage: text { font f; content "Hello"; at x y; ... }</summary>
    let text = TextBuilder()
