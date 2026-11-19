/// <summary>
/// Probes of the core package and the compiler. Each test asserts the behaviour observed on its target;
/// <c>docs/PROBES.md</c> records the outcomes.
/// </summary>
module Ranvier.Science.Tests.Probes

open System.Collections.Generic
open System.Threading
open Expecto
open Ranvier

[<Measure>]
type m

[<Measure>]
type s

let private twice (x: float<'u>) = x + x

type private Tally = { Total: int }

[<Struct>]
type private StructTally = { Total: int }

[<Tests>]
let tests =
    testList "Probes" [
        test "units of measure compile and erase" {
            Expect.equal (6.0<m> / 2.0<s>) 3.0<m/s> "division of measures"
            Expect.equal (float 3.0<m/s>) 3.0 "float strips the measure"
            Expect.equal (LanguagePrimitives.FloatWithMeasure<m> 2.0) 2.0<m> "FloatWithMeasure attaches the measure"
            Expect.equal (twice 2.0<m>) 4.0<m> "a function generic in the measure"
        }

        test "a stream fold can be created in a projection row factory" {
            withGraph (fun () ->
                let feeds = Dictionary<int, ManualStream<int>>()
                let tokens = Dictionary<int, CancellationToken>()
                let keys = createKeyedCollection id
                keys.AddOrUpdate 1

                let rows =
                    createProjectionWith
                        id
                        (fun item ->
                            let key = item ()
                            let feed = ManualStream<int>()
                            feeds[key] <- feed

                            let total =
                                createStreamFold 0 (+) (fun token ->
                                    tokens[key] <- token
                                    feed.Source)

                            fun () -> total.Value)
                        (fun () -> keys.Rows.Keys :> int seq)

                Expect.throwsT<NotReadyException> (fun () -> rows.Get 1 |> ignore) "the row is pending before the first item"
                Expect.equal feeds[1].Starts 1 "the first read starts the row's stream"
                feeds[1].Push 1
                feeds[1].Push 2
                feeds[1].Push 3
                Expect.equal (rows.Get 1) 6 "the row reads the fold of 1, 2 and 3"
                Expect.isFalse tokens[1].IsCancellationRequested "the token is live while the key is present"
                keys.Remove 1 |> ignore
                Expect.sequenceEqual rows.Keys [] "the key is removed"
                Expect.isTrue tokens[1].IsCancellationRequested "removing the key cancels the start token"
                Expect.isTrue feeds[1].Latest.Token.IsCancellationRequested "removing the key cancels the run token")
        }

        testCaseAsync "a memo reading a pending stream is pending"
        <| async {
            use graph = new Graph ()
            let feed = ManualStream<int>()
            let stream = graph.Run (fun () -> createStream (fun _ -> feed.Source))
            let memo = graph.Run (fun () -> createMemo (fun _ -> stream.Value + 1))
            Expect.equal memo.TryValue Pending "TryValue reports pending without raising"
            Expect.equal memo.Status Status.Pending "Status is Pending while pending"
#if FABLE_COMPILER
            Expect.isTrue (isNull (box memo.Peek)) "under Fable Peek is null before the first value"
#else
            Expect.equal memo.Peek 0 "on .NET Peek is the type's default before the first value"
#endif
            feed.Push 4
            Expect.equal memo.TryValue (Ready 5) "after one item 4 the memo reads 5"
            Expect.equal memo.Status Status.None "Status is None after a value"
            feed.Fail (exn "boom")
            do! settled feed.Latest

            match memo.TryValue with
            | Failed error -> Expect.equal error.Message "boom" "TryValue reports the stream's failure"
            | other -> failtestf "expected Failed, got %A" other

            Expect.equal memo.Status Status.Error "Status is Error after a failure"
            Expect.equal memo.Peek 5 "Peek keeps the last value after a failure"
        }

        test "a fold that returns its previous state notifies no reader" {
            withGraph (fun () ->
                let feed = ManualStream<int>()

                let tally =
                    createStreamFold
                        { Tally.Total = 0 }
                        (fun s x -> if x = 0 then s else { s with Total = s.Total + x })
                        (fun _ -> feed.Source)

                let mutable runs = 0

                createEffect (fun () ->
                    tally.TryValue |> ignore
                    runs <- runs + 1)

                Expect.equal runs 1 "the effect runs once at creation"
                feed.Push 1
                Expect.equal runs 2 "a non-zero item re-runs the effect"
                feed.Push 0
                Expect.equal runs 2 "a zero item returns the previous state and leaves the effect"
                feed.Push 2
                Expect.equal runs 3 "a second non-zero item re-runs the effect"
                Expect.equal tally.Peek.Total 3 "the fold's total")
        }

        test "a fold that returns a new equal record notifies its reader" {
            withGraph (fun () ->
                let feed = ManualStream<int>()

                let tally =
                    createStreamFold { Tally.Total = 0 } (fun s x -> { Tally.Total = s.Total + x }) (fun _ -> feed.Source)

                let mutable runs = 0

                createEffect (fun () ->
                    tally.TryValue |> ignore
                    runs <- runs + 1)

                Expect.equal runs 1 "the effect runs once at creation"
                feed.Push 1
                Expect.equal runs 2 "a non-zero item re-runs the effect"
                feed.Push 0
                Expect.equal runs 3 "an equal rebuilt record compares by reference, so a zero item re-runs the effect"
                feed.Push 2
                Expect.equal runs 4 "a second non-zero item re-runs the effect"
                Expect.equal tally.Peek.Total 3 "the fold's total")
        }

        test "a fold that returns a new equal struct" {
            withGraph (fun () ->
                let feed = ManualStream<int>()

                let tally =
                    createStreamFold
                        { StructTally.Total = 0 }
                        (fun s x -> { StructTally.Total = s.Total + x })
                        (fun _ -> feed.Source)

                let mutable runs = 0

                createEffect (fun () ->
                    tally.TryValue |> ignore
                    runs <- runs + 1)

                Expect.equal runs 1 "the effect runs once at creation"
                feed.Push 1
                Expect.equal runs 2 "a non-zero item re-runs the effect"
                feed.Push 0
#if FABLE_COMPILER
                Expect.equal runs 3 "under Fable an equal struct compares by reference, so a zero item re-runs the effect"
#else
                Expect.equal runs 2 "on .NET an equal struct compares by value, so a zero item leaves the effect"
#endif
                Expect.equal tally.Peek.Total 1 "the fold's total")
        }
    ]
