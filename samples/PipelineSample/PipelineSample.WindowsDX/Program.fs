module PipelineSample.WindowsDX.Program

open System
open PipelineSample.Core
open Mibo.Elmish

[<EntryPoint; STAThread>]
let main _ =
  let program = Game.create()
  use game = new ElmishGame<_, _>(program)
  game.Run()
  0
