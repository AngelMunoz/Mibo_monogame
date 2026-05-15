namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Mibo.Rendering.Graphics3D

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Drawable =
  val inline create:
    mesh: Mesh ->
    transform: Matrix ->
    binding: EffectBinding ->
      Mibo.Rendering3D.Drawable

  val inline withTransform:
    transform: Matrix ->
    d: Mibo.Rendering3D.Drawable ->
      Mibo.Rendering3D.Drawable

  val inline withBones:
    bones: Matrix[] -> d: Mibo.Rendering3D.Drawable -> Mibo.Rendering3D.Drawable

  val inline withPass:
    pass: RenderPass ->
    d: Mibo.Rendering3D.Drawable ->
      Mibo.Rendering3D.Drawable

  val inline withMaterialKey:
    key: int<MaterialKey> ->
    d: Mibo.Rendering3D.Drawable ->
      Mibo.Rendering3D.Drawable
