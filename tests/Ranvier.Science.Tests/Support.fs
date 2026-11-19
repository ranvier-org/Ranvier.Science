[<AutoOpen>]
module Ranvier.Science.Tests.Support

open System
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>Runs <c>body</c> with a fresh graph active, and disposes the graph afterwards.</summary>
let withGraph (body: unit -> unit) : unit =
    use g = new Graph ()
    use _ = g.Activate ()
    body ()

/// <summary>
/// The distance between <c>a</c> and <c>b</c> in units in the last place of the larger magnitude: <c>|a - b|</c>
/// over the spacing of doubles at <c>max |a| |b|</c>. Zero for equal values, NaN when either is NaN, and +infinity when the values differ and the larger magnitude is infinite.
/// </summary>
let ulps (a: float) (b: float) : float =
    let largest = max (abs a) (abs b)

    if Double.IsNaN a || Double.IsNaN b then
        nan
    elif a = b then
        0.0
    elif Double.IsInfinity largest then
        infinity
    else
        // The exponent of the binade holding `largest`, corrected for rounding of the logarithm.
        let mutable e = Math.Floor (Math.Log largest / Math.Log 2.0)

        while Math.Pow (2.0, e) > largest do
            e <- e - 1.0

        while Math.Pow (2.0, e + 1.0) <= largest do
            e <- e + 1.0

        abs (a - b) / Math.Pow (2.0, max (e - 52.0) -1074.0)

/// <summary>One start of a <c>ManualStream</c>: its sink, its token and the task the test completes.</summary>
type StreamStart<'T> =
    {
        Sink: StreamSink<'T>
        Token: CancellationToken
        Done: TaskCompletionSource<unit>
    }

/// <summary>A stream source the test drives: every start records its sink, token and completion.</summary>
type ManualStream<'T>() =
    let starts = ResizeArray<StreamStart<'T>>()

    /// <summary>A source whose every <c>Run</c> records a start; the same object serves every restart.</summary>
    member val Source =
        { new StreamSource<'T>() with
            member _.Run(sink, token) =
                let start =
                    {
                        Sink = sink
                        Token = token
                        Done = TaskCompletionSource<unit>()
                    }

                starts.Add start
                start.Done.Task :> Task
        }

    member _.Starts = starts.Count
    member _.Latest = starts[starts.Count - 1]

    member this.Push(item: 'T) =
        this.Latest.Sink.Next item

    member this.Complete() =
        this.Latest.Done.SetResult ()

    member this.Fail(error: exn) =
        this.Latest.Done.SetException error

/// <summary>Completes once a stream memo has observed the start's task settle: at once on .NET, a microtask later under Fable.</summary>
let settled (start: StreamStart<'T>) : Async<unit> =
#if FABLE_COMPILER
    async {
        let! _ = Async.Catch (Async.AwaitTask start.Done.Task)
        return ()
    }
#else
    async.Return ()
#endif

/// <summary>
/// Completes once every settle already delivered to the graph has applied: at once on .NET, after the microtask
/// queue drains under Fable.
/// </summary>
let drained () : Async<unit> =
#if FABLE_COMPILER
    Async.Sleep 0
#else
    async.Return ()
#endif
