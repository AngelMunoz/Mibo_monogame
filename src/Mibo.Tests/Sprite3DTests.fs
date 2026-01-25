module Mibo.Tests.Sprite3D

open Expecto
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Mibo.Elmish.Graphics3D
open Mibo.Rendering
open Mibo.Elmish
open Mibo.Rendering.Graphics3D

[<Tests>]
let spriteDslTests =
  testList "Sprite3D DSL" [
    testCase "quad CE creates valid struct"
    <| fun _ ->
      let q = quad {
        at Vector3.Zero
        onXY(Vector2(2f, 2f))
        color Color.White
      }

      Expect.equal q.Center Vector3.Zero "Center should be zero"
      Expect.equal q.Right Vector3.UnitX "Right should match half-width"
      Expect.equal q.Up Vector3.UnitY "Up should match half-height"
      Expect.equal q.Color Color.White "Default color should be white"
      Expect.equal q.Uv UvRect.full "Default UV should be full"

    testCase "billboard CE creates valid struct"
    <| fun _ ->
      let b = billboard {
        at(Vector3(1f, 2f, 3f))
        size(Vector2(10f, 20f))
        rotate 0.0f
        facing Spherical
      }

      Expect.equal b.Position (Vector3(1f, 2f, 3f)) "Position should match"
      Expect.equal b.Size (Vector2(10f, 20f)) "Size should match"
      Expect.equal b.Rotation 0.0f "Default rotation should be 0"
      Expect.equal b.Mode Spherical "Default mode should be spherical"

    testCase "RenderBuilder quad adds command"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let tex = Unchecked.defaultof<Texture2D>

      let q = quad {
        at Vector3.Zero
        onXZ(Vector2(2f, 2f))
      }

      Buffer.quad tex q buffer |> ignore

      Expect.equal buffer.Count 1 "Should have 1 command"

      match buffer.[0] with
      | _, DrawSpriteQuad cmd ->
        Expect.equal cmd.Pass Opaque "Should be opaque"
        Expect.equal cmd.Quad.Center Vector3.Zero "Center should match"
      | _ -> failtest "Command should be DrawSpriteQuad"

    testCase "billboard adds command to buffer"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      let tex = Unchecked.defaultof<Texture2D>

      let b = billboard {
        at Vector3.Zero
        size Vector2.One
      }

      Buffer.billboard tex b buffer |> ignore

      Expect.equal buffer.Count 1 "Should have 1 command"

      match buffer.[0] with
      | _, DrawSpriteBillboard cmd ->
        Expect.equal
          cmd.Pass
          Transparent
          "Default billboard should be transparent"
      | _ -> failtest "Command should be DrawSpriteBillboard"

    testCase "line adds command to buffer"
    <| fun _ ->
      let buffer = RenderBuffer<unit, RenderCommand>()
      Buffer.line Vector3.Zero Vector3.UnitX Color.Red buffer |> ignore

      Expect.equal buffer.Count 1 "Should have 1 command"

      match buffer.[0] with
      | _, DrawLine(p1, p2, col, pass) ->
        Expect.equal p1 Vector3.Zero "p1 match"
        Expect.equal p2 Vector3.UnitX "p2 match"
        Expect.equal col Color.Red "color match"
        Expect.equal pass Opaque "pass match"
      | _ -> failtest "Command should be DrawLine"
  ]
