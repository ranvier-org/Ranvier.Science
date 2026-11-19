namespace SequentialMonitor

open System
open System.Threading.Tasks
open Ranvier
open Ranvier.Numerics

/// One run of a feed's source: where its items go, its deviates, and the task that ends it.
[<NoEquality; NoComparison>]
type private FeedStart =
    {
        Sink: StreamSink<float>
        Mean: float
        Sd: float
        mutable Generator: Generator
        Finished: TaskCompletionSource<unit>
        mutable Ended: bool
    }

/// <summary>A simulated instrument: normal deviates from a seed, delivered when asked.</summary>
/// <remarks>
/// <c>EmitAtRate</c> is called on the graph thread; <c>Push</c> on the graph thread or on one other thread at a time.
/// Without a current start, <c>EmitAtRate</c> returns 0 and keeps its fraction, and <c>Push</c> and <c>Fail</c> are
/// ignored. A cancelled start faults with OperationCanceledException.
/// </remarks>
[<Sealed>]
type Feed() =
    let gate = obj ()
    let mutable current: FeedStart option = None
    let mutable starts = 0
    let mutable delivered = 0
    let mutable carry = 0.0

    let hold (body: unit -> 'a) : 'a =
#if FABLE_COMPILER
        ignore gate
        body ()
#else
        lock gate body
#endif

    /// Ends `start` with `error` once; a start that has ended is left as it is.
    let finish (start: FeedStart) (error: exn) =
        let ending =
            hold (fun () ->
                if start.Ended then
                    false
                else
                    start.Ended <- true

                    match current with
                    | Some c when obj.ReferenceEquals(c, start) -> current <- None
                    | _ -> ()

                    true)

        if ending then
            start.Finished.SetException error

    /// A source whose every start begins the sequence again from `seed`.
    member _.Source(mean: float, sd: float, seed: int) : StreamSource<float> =
        StreamSource.create (fun sink token ->
            let start =
                {
                    Sink = sink
                    Mean = mean
                    Sd = sd
                    Generator = Generator.create seed
                    Finished = TaskCompletionSource<unit>()
                    Ended = false
                }

            hold (fun () ->
                starts <- starts + 1
                current <- Some start)

            token.Register(fun () -> finish start (OperationCanceledException "The feed's start was cancelled."))
            |> ignore

            start.Finished.Task :> Task)

    /// Delivers `rate * dt` deviates to the current start, carrying the fraction to the next call. Returns the number delivered.
    /// A `rate * dt` that is negative, infinite or NaN delivers nothing and returns 0, with the fraction and `Delivered` kept.
    member _.EmitAtRate(rate: float, dt: float) : int =
        let amount = rate * dt

        hold (fun () ->
            match current with
            | _ when not (amount >= 0.0 && amount < infinity) -> 0
            | None -> 0
            | Some start ->
                let due = carry + amount
                let count = floor due
                carry <- due - count
                let n = int count

                for _ in 1..n do
                    let struct (z, next) = Generator.normal start.Generator
                    start.Generator <- next
                    start.Sink.Next(start.Mean + start.Sd * z)

                delivered <- delivered + n
                n)

    /// Delivers the given values to the current start.
    member _.Push(values: float[]) : unit =
        hold (fun () ->
            match current with
            | None -> ()
            | Some start ->
                for x in values do
                    start.Sink.Next x

                delivered <- delivered + values.Length)

    /// Fails the current start.
    member _.Fail(error: exn) : unit =
        match hold (fun () -> current) with
        | None -> ()
        | Some start -> finish start error

    /// The number of times a source of this feed has been started.
    member _.Starts = hold (fun () -> starts)

    /// The number of items delivered to every start so far.
    member _.Delivered = hold (fun () -> delivered)
