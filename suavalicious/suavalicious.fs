module suavalicious

open System
open System.Threading
open Suave

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig (Successful.OK "Hello World!")
    0 // return an integer exit code
