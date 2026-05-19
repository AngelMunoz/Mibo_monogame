module _3DSample.Rendering.Commands

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering
open _3DSample.Materials.Types

// ============================================================================
// Render Commands
// ============================================================================
// In the V3 architecture, YOU define your render commands.
// This module defines the commands this sample needs.
// Add new commands here as your game grows.

/// <summary>User-defined render command for this sample.</summary>
type SampleCmd =
  /// <summary>Set the active camera (view + projection matrices).</summary>
  | SetCamera of camera: Mibo.Elmish.Camera
  /// <summary>Draw a 3D model using BasicEffect (simple, no custom shaders).</summary>
  | DrawMesh of model: Model * transform: Matrix
  /// <summary>Draw a 3D model with a custom material and shader effect.</summary>
  | DrawMeshWithMaterial of
    effect: Effect *
    material: Material *
    model: Model *
    transform: Matrix
  /// <summary>Draw lines using a custom shader effect.</summary>
  | DrawLinesEffect of
    vertices: VertexPositionColor[] *
    lineCount: int *
    effect: Effect *
    setup: EffectSetup
  /// <summary>Clear the render target.</summary>
  | Clear of color: Color * clearDepth: bool
