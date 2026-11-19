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

#if !FABLE_COMPILER
/// <summary>Runs <c>work</c> on a thread other than the caller's and waits for it.</summary>
let offThread (work: unit -> unit) =
    let thread = Thread (ThreadStart work)
    thread.Start ()
    thread.Join ()

/// <summary>A graph whose dispatched work runs on the test's <c>Pump</c> calls.</summary>
let manualGraph () =
    new Graph (
        { GraphOptions.Default with
            Dispatcher = Some (ManualDispatcher () :> IGraphDispatcher)
        }
    )

#endif
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
