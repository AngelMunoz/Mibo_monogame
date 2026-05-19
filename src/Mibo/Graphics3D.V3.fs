namespace Mibo.Rendering.Graphics3D

open System
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish

// ============================================================================
// Infrastructure-Only 3D Rendering (V3)
// ============================================================================
// This module provides zero-cost infrastructure for 3D rendering.
// All rendering logic (shaders, lighting, shadows, post-processing) is userland.

/// <summary>Convenience alias for a render buffer for 3D commands.</summary>
/// <remarks>3D rendering typically does not rely on a 2D-style render-layer ordering. We preserve submission order (do not sort), so the key is <c>unit</c>.</remarks>
type RenderBuffer3D<'Cmd> = RenderBuffer<unit, 'Cmd>

/// <summary>Runtime camera state tracked during command processing.</summary>
[<Struct>]
type CameraState3D = { View: Matrix; Projection: Matrix }

module CameraState3D =
  /// <summary>Creates camera state from a camera's view/projection.</summary>
  let inline from(cam: Camera) : CameraState3D = {
    View = cam.View
    Projection = cam.Projection
  }

  let defaults: CameraState3D = {
    View = Matrix.Identity
    Projection = Matrix.Identity
  }

/// <summary>
/// Infrastructure-only 3D renderer.
/// Delegates all rendering logic to user-provided functions.
/// </summary>
/// <typeparam name="Model">The game model type.</typeparam>
/// <typeparam name="Cmd">User-defined render command type.</typeparam>
type Batch3DRenderer<'Model, 'Cmd>
  (
    game: Game,
    [<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer3D<'Cmd> -> unit,
    [<InlineIfLambda>] processCommands:
      CameraState3D -> GraphicsDevice -> RenderBuffer3D<'Cmd> -> CameraState3D
  ) =

  let buffer = RenderBuffer3D<'Cmd>()
  let mutable cameraState = CameraState3D.defaults

  interface IDisposable with
    member _.Dispose() = ()

  interface IRenderer<'Model> with
    member _.Draw(ctx: GameContext, model: 'Model, _gameTime: GameTime) =
      buffer.Clear()
      view ctx model buffer
      cameraState <- processCommands cameraState ctx.GraphicsDevice buffer

/// <summary>Functions for creating <see cref="T:Mibo.Rendering.Graphics3D.Batch3DRenderer`2"/>.</summary>
module Batch3DRenderer =
  /// <summary>Creates an infrastructure-only 3D renderer.</summary>
  /// <param name="game">The MonoGame Game instance.</param>
  /// <param name="view">User function that fills the render buffer from the model.</param>
  /// <param name="processCommands">User function that processes render commands and returns final camera state.</param>
  let inline create<'Model, 'Cmd>
    (game: Game)
    ([<InlineIfLambda>] view:
      GameContext -> 'Model -> RenderBuffer3D<'Cmd> -> unit)
    ([<InlineIfLambda>] processCommands:
      CameraState3D -> GraphicsDevice -> RenderBuffer3D<'Cmd> -> CameraState3D)
    : IRenderer<'Model> =
    new Batch3DRenderer<'Model, 'Cmd>(game, view, processCommands)
    :> IRenderer<'Model>

[<AutoOpen>]
module RenderBuffer3DExtensions =
  open System.Runtime.CompilerServices

  /// <summary>Extension methods for <see cref="T:Mibo.Rendering.Graphics3D.RenderBuffer3D`1"/>.</summary>
  [<Extension>]
  type RenderBuffer3DExtensions =
    /// <summary>Adds a command to a 3D render buffer. Since 3D buffers use <c>unit</c> as the key, this overload lets you omit it.</summary>
    [<Extension>]
    static member inline AddCmd(this: RenderBuffer3D<'Cmd>, cmd: 'Cmd) =
      this.Add((), cmd)
