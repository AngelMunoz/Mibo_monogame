namespace Mibo.Rendering.Graphics3D.V2

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering.Graphics3D

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Drawable =
  val inline create:
    mesh: Mesh ->
    transform: Matrix ->
    binding: EffectBinding ->
      Mibo.Rendering.Graphics3D.V2.Drawable

  val inline withTransform:
    transform: Matrix ->
    d: Mibo.Rendering.Graphics3D.V2.Drawable ->
      Mibo.Rendering.Graphics3D.V2.Drawable

  val inline withBones:
    bones: Matrix[] ->
    d: Mibo.Rendering.Graphics3D.V2.Drawable ->
      Mibo.Rendering.Graphics3D.V2.Drawable

  val inline withPass:
    pass: RenderPass ->
    d: Mibo.Rendering.Graphics3D.V2.Drawable ->
      Mibo.Rendering.Graphics3D.V2.Drawable

  val inline withMaterialKey:
    key: int<MaterialKey> ->
    d: Mibo.Rendering.Graphics3D.V2.Drawable ->
      Mibo.Rendering.Graphics3D.V2.Drawable

  val inline withMaterialData:
    data: PBRMaterialData ->
    d: Mibo.Rendering.Graphics3D.V2.Drawable ->
      Mibo.Rendering.Graphics3D.V2.Drawable
