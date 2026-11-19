/// The browser entry point: the page as the platform, and the start of the page in the element with id `app`.
module SequentialMonitor.Browser

open Fable.Core
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Dsl

[<Global "document">]
let private document: Ranvier.Dom.Document = jsNative

[<Emit("requestAnimationFrame($0)")>]
let private requestFrame (callback: float -> unit) : unit = jsNative

/// The value given under a name in the page's address, or null.
[<Emit("new URLSearchParams(location.search).get($0)")>]
let private query (name: string) : string = jsNative

[<Emit("performance.now()")>]
let private now () : float = jsNative

/// Logs a line and posts it to the server that serves the page, which appends it to probe.log. Each post starts
/// after the previous one has finished, so the log holds the lines in the order they were reported.
[<Emit("(console.log($0), globalThis.monitorPosts = (globalThis.monitorPosts ?? Promise.resolve()).then(() => fetch('/log', { method: 'POST', body: $0 })).catch(() => {}))")>]
let private report (line: string) : unit = jsNative

/// The page as the platform. A variable is a value in the page's address.
type private Page() =
    interface IPlatform with
        member _.Document = document
        member _.RequestFrame(callback) = requestFrame callback
        member _.Variable(name) = query name
        member _.Now() = now ()
        member _.Report(line) = report line
        member _.SavePicture(_) = "a page saves no picture: run.mjs --picture <png> saves a screenshot of it"

/// Mounts the page; with `?script=<name>` runs that script after the first frame.
let private start () =
    let host = document.getElementById "app"
    let script = query "script"

    let props =
        { Platform = Page()
          Session = Session() }

    props.Session.Auto <- isNull script
    let graph = new Graph()

    let failed (errors: exn list) =
        for error in errors do
            report ("mount failure: " + error.Message)

    host.textContent <- ""

    Mounting.mount graph None failed host None (fun context -> Program.run (Component.render App.app props) context.Backend)
    |> ignore

    if not (isNull script) then
        requestFrame (fun _ -> Script.run props script |> ignore)

start ()
