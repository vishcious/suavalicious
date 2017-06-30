// include Fake libs

#r "packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FAKE/tools/FakeLib.dll"

open Fake
open System
open System.IO
open Suave
open Suave.Logging
open Suave.Web
open System.Net
open Microsoft.FSharp.Compiler.Interactive.Shell
#load "suavalicious/suavalicious.fsx"
open Suavalicious

// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/"


// Filesets
let appReferences  =
    !! "/**/*.csproj"
    ++ "/**/*.fsproj"

// version info
let version = "0.1"  // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; deployDir]
)

Target "Build" (fun _ ->
    // compile all projects below src/app/
    MSBuildDebug buildDir "Build" appReferences
    |> Log "AppBuild-Output: "
)

Target "Deploy" (fun _ ->
    !! (buildDir + "/**/*.*")
    -- "*.zip"
    |> Zip buildDir (deployDir + "ApplicationName." + version + ".zip")
)

//////////////////////////////////////////////////////////////////////////////
let sbOut = new Text.StringBuilder()
let sbErr = new Text.StringBuilder()

let fsiSession = 
  let inStream = new StringReader("")
  let outStream = new StringWriter(sbOut)
  let errStream = new StringWriter(sbErr)
  let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
  let argv = Array.append [|"/fake/fsi.exe"; "--quiet"; "--noninteractive"; "-d:DO_NOT_START_SERVER"|] [||]
  FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)

let reportFsiError (e:exn) =
  traceError "Reloading suavalicious.fsx script failed."
  traceError (sprintf "Message: %s\nError: %s" e.Message (sbErr.ToString().Trim()))
  sbErr.Clear() |> ignore

let reloadScript () = 
  try
    traceImportant "Reloading suavalicious.fsx script..."
    let appFsx = __SOURCE_DIRECTORY__ @@ "suavalicious/suavalicious.fsx"
    fsiSession.EvalInteraction(sprintf "#load @\"%s\"" appFsx)
    fsiSession.EvalInteraction("open Suavalicious")
    match fsiSession.EvalExpression("app") with
    | Some app -> Some(app.ReflectionValue :?> WebPart)
    | None -> failwith "Couldn't get 'app' value"
  with e -> reportFsiError e; None

// --------------------------------------------------------------------------------------
// Suave server that redirects all request to currently loaded version
// --------------------------------------------------------------------------------------

let currentApp = ref (fun _ -> async { return None })

let serverConfig =
  { defaultConfig with
      homeFolder = Some __SOURCE_DIRECTORY__
      logger = Targets.create Debug [||]
      bindings = [ HttpBinding.create HTTP (IPAddress.Parse("127.0.0.1")) (Sockets.Port.Parse("8083"))] }

let reloadAppServer () =
  reloadScript() |> Option.iter (fun app -> 
    currentApp.Value <- app
    traceImportant "New version of suavalicious.fsx loaded!" )

Target "run" (fun _ ->
  let app ctx = currentApp.Value ctx
  let _, server = startWebServerAsync serverConfig app

  // Start Suave to host it on localhost
  reloadAppServer()
  Async.Start(server)
  // Open web browser with the loaded file
  System.Diagnostics.Process.Start("http://localhost:8083") |> ignore
  
  // Watch for changes & reload when suavalicious.fsx changes
  use watcher = {BaseDirectory = (sprintf "%s/suavalicious" __SOURCE_DIRECTORY__); Includes = ["*.*"]; Excludes = []} |> WatchChanges (fun _ -> reloadAppServer())
  traceImportant "Waiting for suavalicious.fsx edits. Press any key to stop."
  System.Console.ReadLine() |> ignore
)
//////////////////////////////////////////////////////////////////////////////
// Build order
"Clean"
  ==> "Build"
  ==> "Deploy"

// start build
RunTargetOrDefault "Build"
