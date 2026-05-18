namespace Mibo.Rendering.Graphics3D.V2

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
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    {
      Mesh = mesh
      Transform = transform
      Bones = ValueNone
      BoundingSphere = mesh.BoundingSphere.Transform(transform)
      Pass = Opaque
      MaterialKey = binding.MaterialKey
      MaterialData = ValueNone
      Binding = binding
    }

  let inline withTransform
    (transform: Matrix)
    (d: Mibo.Rendering.Graphics3D.V2.Drawable)
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    {
      d with
          Transform = transform
          BoundingSphere = d.Mesh.BoundingSphere.Transform(transform)
    }

  let inline withBones
    (bones: Matrix[])
    (d: Mibo.Rendering.Graphics3D.V2.Drawable)
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    { d with Bones = ValueSome bones }

  let inline withPass
    (pass: RenderPass)
    (d: Mibo.Rendering.Graphics3D.V2.Drawable)
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    { d with Pass = pass }

  let inline withMaterialKey
    (key: int<MaterialKey>)
    (d: Mibo.Rendering.Graphics3D.V2.Drawable)
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    { d with MaterialKey = key }

  let inline withMaterialData
    (data: PBRMaterialData)
    (d: Mibo.Rendering.Graphics3D.V2.Drawable)
    : Mibo.Rendering.Graphics3D.V2.Drawable =
    { d with MaterialData = ValueSome data }
