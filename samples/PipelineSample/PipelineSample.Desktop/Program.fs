module PipelineSample.Desktop.Program

open Mibo.Elmish
open PipelineSample.Core

[<EntryPoint>]
let main _ =
  let program = Game.create()
  use game = new ElmishGame<_, _>(program)
  game.Run()
  0
