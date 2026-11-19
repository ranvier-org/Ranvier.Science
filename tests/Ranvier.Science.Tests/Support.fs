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

/// <summary>
/// The correctly rounded sum of <c>xs</c>, by Shewchuk's non-overlapping partials as in Python's <c>math.fsum</c>.
/// NaN for a non-finite input. Uses only <c>+ - * /</c>, comparisons and <c>abs</c>, which both targets round identically.
/// </summary>
let exactSum (xs: float[]) : float =
    let isFinite (v: float) = abs v < infinity
    if not (Array.forall isFinite xs) then
        nan
    else
        let partials = ResizeArray<float> ()

        for x0 in xs do
            let mutable x = x0
            let mutable kept = 0

            for i in 0 .. partials.Count - 1 do
                let mutable y = partials[i]

                if abs x < abs y then
                    let t = x
                    x <- y
                    y <- t

                let hi = x + y
                let lo = y - (hi - x)

                if lo <> 0.0 then
                    partials[kept] <- lo
                    kept <- kept + 1

                x <- hi

            partials.RemoveRange (kept, partials.Count - kept)
            partials.Add x

        let mutable n = partials.Count
        let mutable hi = 0.0

        if n > 0 then
            n <- n - 1
            hi <- partials[n]
            let mutable lo = 0.0
            let mutable looping = true

            while looping && n > 0 do
                let x = hi
                n <- n - 1
                let y = partials[n]
                hi <- x + y
                let yr = hi - x
                lo <- y - yr

                if lo <> 0.0 then looping <- false

            // Half-way correction: a lower partial of the same sign as `lo` that doubles `lo` into an exact tie rounds the tie the right way.
            if n > 0 && ((lo < 0.0 && partials[n - 1] < 0.0) || (lo > 0.0 && partials[n - 1] > 0.0)) then
                let y = lo * 2.0
                let x = hi + y
                let yr = x - hi

                if y = yr then hi <- x

        hi
