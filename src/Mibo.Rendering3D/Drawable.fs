namespace Mibo.Rendering3D

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering
open Mibo.Rendering.Graphics3D

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Drawable =

  let inline create
    (mesh: Mesh)
    (transform: Matrix)
    (binding: EffectBinding)
    : Mibo.Rendering3D.Drawable =
    {
      Mesh = mesh
      Transform = transform
      Bones = ValueNone
      BoundingSphere = mesh.BoundingSphere.Transform(transform)
      Pass = Opaque
      MaterialKey = binding.MaterialKey
      Binding = binding
    }

  let inline withTransform
    (transform: Matrix)
    (d: Mibo.Rendering3D.Drawable)
    : Mibo.Rendering3D.Drawable =
    {
      d with
          Transform = transform
          BoundingSphere = d.Mesh.BoundingSphere.Transform(transform)
    }

  let inline withBones
    (bones: Matrix[])
    (d: Mibo.Rendering3D.Drawable)
    : Mibo.Rendering3D.Drawable =
    { d with Bones = ValueSome bones }

  let inline withPass
    (pass: RenderPass)
    (d: Mibo.Rendering3D.Drawable)
    : Mibo.Rendering3D.Drawable =
    { d with Pass = pass }

  let inline withMaterialKey
    (key: int<MaterialKey>)
    (d: Mibo.Rendering3D.Drawable)
    : Mibo.Rendering3D.Drawable =
    { d with MaterialKey = key }
