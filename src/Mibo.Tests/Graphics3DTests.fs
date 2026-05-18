module Mibo.Tests.Graphics3D

open Expecto
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Rendering.Graphics3D

[<Tests>]
let tests =
  testList "Graphics3D" [
    testList "Camera" [
      testCase "Camera.identity has identity matrices"
      <| fun _ ->
        let c = Camera.identity
        Expect.equal c.Position Vector3.Zero "Position should be zero"
    ]

    testList "Camera3D" [
      testCase "perspective creates camera with view matrix"
      <| fun _ ->
        let c =
          Camera.perspective
            (Vector3(0.f, 0.f, 10.f))
            Vector3.Zero
            Vector3.Up
            (MathHelper.ToRadians 45.f)
            (16.f / 9.f)
            0.1f
            1000.f

        Expect.equal
          c.Position
          (Vector3(0.f, 0.f, 10.f))
          "Position should match"

      testCase "at moves camera position"
      <| fun _ ->
        let c = Camera.identity |> Camera.at(Vector3(5.f, 0.f, 0.f))

        Expect.equal
          c.Position
          (Vector3(5.f, 0.f, 0.f))
          "Position should be updated"
    ]
  ]
