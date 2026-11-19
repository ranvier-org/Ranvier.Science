namespace SequentialMonitor

open Ranvier.Dom

/// What the page needs from the window or the browser tab it runs in.
type IPlatform =
    abstract Document: Document
    /// Calls back before the next frame is shown.
    abstract RequestFrame: callback: (float -> unit) -> unit
    /// A setting given from outside, or null: `--script frames` for a window, `?script=frames` for a page.
    abstract Variable: name: string -> string
    /// Milliseconds on a monotonic clock.
    abstract Now: unit -> float
    /// Writes one line of a scripted run: standard output for a window, the local server's log for a page.
    abstract Report: line: string -> unit
    /// Saves a picture of the page under `name` and returns a line saying where it went.
    abstract SavePicture: name: string -> string

/// What one mount shares with the code that drives it.
type Session() =
    /// The mounted page, set by its setup.
    member val Page: obj = null with get, set
    /// The graph the page was mounted in, set by its setup. A signal write from outside the page runs in its `Run`.
    member val Graph: Ranvier.Graph = Unchecked.defaultof<Ranvier.Graph> with get, set
    /// False while a scripted run calls the frames itself.
    member val Auto = true with get, set

type Props = { Platform: IPlatform; Session: Session }
