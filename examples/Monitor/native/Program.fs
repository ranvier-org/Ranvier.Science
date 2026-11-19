/// The window entry point: the window as the page's platform, and the scripted runs.
module SequentialMonitor.Program

open System
open System.Diagnostics
open System.IO
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Dsl
open Ranvier.View.WebCore.Sdl

[<Literal>]
let private Title = "Sequential monitor"

[<Literal>]
let private Width = 1280

[<Literal>]
let private Height = 900

let private usage = "usage: Monitor.Native [--script <name>]"

/// Where `SavePicture` writes: `examples/Monitor/out`.
let private pictures = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "out"))

/// The window as the page's platform. A variable is the argument after `--<name>`.
type private NativeWindow(window: Window, argv: string[]) =
    let clock = Stopwatch.StartNew()

    interface IPlatform with
        member _.Document = window.Host.Document
        member _.RequestFrame(callback) = window.Host.RequestAnimationFrame callback |> ignore

        member _.Variable(name) =
            match Array.tryFindIndex ((=) ("--" + name)) argv with
            | Some i when i + 1 < argv.Length -> argv[i + 1]
            | _ -> null

        member _.Now() = clock.Elapsed.TotalMilliseconds
        member _.Report(line) = Console.WriteLine line

        member _.SavePicture(name) =
            // The page is laid out and painted again first, so the picture shows the page as it is now.
            for _ in 1..4 do
                window.Step(timeout = 4) |> ignore

            window.Present()
            let struct (w, h, pixels) = window.ReadPixels()
            let path = Path.Combine(pictures, name + ".png")
            Png.PngEncoder.saveRgba8 path w h pixels
            $"saved {path} ({w} x {h} device px)"

let private propsOf (argv: string[]) (window: Window) =
    { Platform = NativeWindow(window, argv)
      Session = Session() }

/// Mounts the page in a hidden window. With a script name it steps the window until a frame has been drawn,
/// runs the script and returns its code; without one it shows the window and runs it until it is closed.
let private run (argv: string[]) (script: string option) =
    use window = Window.Create(Title, Width, Height, hidden = true)
    let graph = window.Graph
    let doc = window.Host.Document
    let container = doc.createElement "div"
    doc.body.appendChild container |> ignore
    let props = propsOf argv window
    props.Session.Auto <- script.IsNone

    let mounted =
        Mounting.mount graph None (fun errs -> raise (AggregateException errs)) container None (fun ctx ->
            Program.run (Component.render App.app props) ctx.Backend)

    try
        match script with
        | Some name ->
            let mutable drawn = false
            props.Platform.RequestFrame(fun _ -> drawn <- true)
            let limit = Stopwatch.StartNew()

            while not drawn && limit.Elapsed.TotalSeconds < 30.0 do
                if not (window.Step(timeout = 4)) then
                    failwith "the window closed during the run"

            if not drawn then
                props.Platform.Report "no frame was drawn in 30 s"
                props.Platform.Report "script done 1"
                1
            else
                window.Present()
                Script.run props name
        | None ->
            window.Show()
            window.Present()

            while window.Step() do
                ()

            0
    finally
        graph.Run(fun () -> mounted.Dispose())
        container.remove ()

let private runApp (argv: string[]) =
    match argv with
    | [||] -> run argv None
    | [| "--script"; name |] -> run argv (Some name)
    | _ ->
        Console.Error.WriteLine usage
        2

[<EntryPoint>]
let main argv =
#if DEBUG
    // dotnet run -- dev: the hot reload host keeps the window open and patches saved edits into it.
    if Array.contains "dev" argv then
        Ranvier.View.WebCore.Hmr.Dev.RunWith(argv, Title, Width, Height, App.app, propsOf argv)
    else
        runApp argv
#else
    runApp argv
#endif
