module Ranvier.Science.Tests.RunTests

open Expecto
open Ranvier
open Ranvier.Quantities

type Sim = { mutable X: float; mutable Steps: int }

/// The signals of a synthetic run, and the run's start and advance.
type private Rig(x0Initial: float, rateInitial: float) =
    let clock = createSignal 0.0
    let x0 = createSignal x0Initial
    let rate = createSignal rateInitial
    let mutable starts = 0
    let mutable advances = 0
    let gaps = ResizeArray<float>()
    let mutable failNext = false

    member _.Clock = clock
    member _.X0 = x0
    member _.Rate = rate
    member _.Starts = starts
    member _.Advances = advances
    member _.Gaps = List.ofSeq gaps
    member _.FailNext() = failNext <- true

    member _.Start() =
        starts <- starts + 1
        { X = x0.Value; Steps = 0 }

    member _.Advance (s: Sim) (dt: float) =
        advances <- advances + 1
        gaps.Add dt

        if failNext then
            failNext <- false
            failwith "advance failed"

        s.X <- s.X + rate.Value * dt
        s.Steps <- s.Steps + 1

    member this.Run() =
        Run.create (fun () -> clock.Value) this.Start this.Advance

    member this.RunWith(maxStep: float) =
        Run.createWith maxStep (fun () -> clock.Value) this.Start this.Advance

[<Tests>]
let tests =
    testList
        "Run"
        [
            test "the first read starts and does not advance" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let s = run.State
                    Expect.equal s.X 4.0 "X is x0"
                    Expect.equal s.Steps 0 "no step has run"
                    Expect.equal rig.Starts 1 "start ran once"
                    Expect.equal rig.Advances 0 "advance did not run")
            }

            test "a later clock value advances once by the gap" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    run.State |> ignore
                    rig.Clock.Value <- 2.5
                    let s = run.State
                    Expect.equal s.Steps 1 "one step"
                    Expect.equal s.X (4.0 + 2.0 * 2.5) "X is x0 + rate * 2.5")
            }

            test "clock writes nobody reads advance nothing" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    run.State |> ignore
                    rig.Clock.Value <- 1.0
                    rig.Clock.Value <- 2.0
                    rig.Clock.Value <- 3.0
                    Expect.equal rig.Advances 0 "unread clock writes call advance 0 times"
                    run.State |> ignore
                    Expect.equal rig.Advances 1 "one read calls advance once"
                    Expect.equal rig.Gaps [ 3.0 ] "the gap is 3")
            }

            test "maxStep limits one advance" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.RunWith 0.1
                    run.State |> ignore
                    rig.Clock.Value <- 2.5
                    Expect.equal run.State.X (4.0 + 2.0 * 0.1) "X is x0 + rate * 0.1")
            }

            test "a quantity read by start restarts the run" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let first = run.State
                    rig.Clock.Value <- 1.0
                    run.State |> ignore
                    rig.X0.Value <- 7.0
                    let s = run.State
                    Expect.isFalse (obj.ReferenceEquals (s, first)) "a new state object"
                    Expect.equal s.Steps 0 "Steps is 0"
                    Expect.equal s.X 7.0 "X is the new x0")
            }

            test "a quantity read only by advance changes the course without a restart" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let first = run.State
                    rig.Clock.Value <- 1.5
                    run.State |> ignore
                    rig.Rate.Value <- 3.0
                    rig.Clock.Value <- 2.0
                    let s = run.State
                    Expect.isTrue (obj.ReferenceEquals (s, first)) "the same state object"
                    Expect.equal s.Steps 2 "Steps is 2"
                    Expect.equal s.X (4.0 + 2.0 * 1.5 + 3.0 * 0.5) "X is x0 + r1 * d1 + r2 * d2"
                    Expect.equal rig.Starts 1 "start ran once")
            }

            test "the clock going back restarts" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let first = run.State
                    rig.Clock.Value <- 5.0
                    run.State |> ignore
                    rig.Clock.Value <- 1.0
                    let s = run.State
                    Expect.isFalse (obj.ReferenceEquals (s, first)) "a new state object"
                    Expect.equal s.Steps 0 "Steps is 0")
            }

            test "an equal or NaN clock does nothing" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let first = run.State
                    rig.Clock.Value <- 1.0
                    run.State |> ignore
                    rig.Clock.Value <- 1.0
                    let again = run.State
                    rig.Clock.Value <- nan
                    let s = run.State
                    Expect.isTrue (obj.ReferenceEquals (again, first)) "the same state object after an equal clock"
                    Expect.isTrue (obj.ReferenceEquals (s, first)) "the same state object after a NaN clock"
                    Expect.equal s.Steps 1 "Steps is unchanged"
                    Expect.equal rig.Starts 1 "no start"
                    rig.Clock.Value <- 3.0
                    run.State |> ignore
                    Expect.equal rig.Gaps [ 1.0; 2.0 ] "a finite clock after NaN advances by the gap from the earlier finite value")
            }

            test "a NaN clock at the start does not stick the run" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    rig.Clock.Value <- nan
                    let run = rig.Run ()
                    let first = run.State
                    Expect.equal first.X 4.0 "X is x0"
                    Expect.equal first.Steps 0 "Steps is 0"
                    rig.Clock.Value <- 2.0
                    let recorded = run.State
                    Expect.isTrue (obj.ReferenceEquals (recorded, first)) "the same state object after the first finite clock"
                    Expect.equal recorded.Steps 0 "the first finite clock is recorded and does not advance"
                    rig.Clock.Value <- 3.0
                    let s = run.State
                    Expect.isTrue (obj.ReferenceEquals (s, first)) "the same state object after the second finite clock"
                    Expect.equal s.Steps 1 "Steps is 1"
                    Expect.equal s.X (4.0 + 2.0 * 1.0) "X is x0 + rate * 1.0")
            }

            test "a reader is notified after each advance" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let seen = ResizeArray<int>()
                    createEffect (fun () -> seen.Add run.State.Steps)
                    rig.Clock.Value <- 1.0
                    rig.Clock.Value <- 2.0
                    Expect.equal (List.ofSeq seen) [ 0; 1; 2 ] "the effect records 0, 1 and 2")
            }

            test "an advance that raises fails the read, and a restart recovers" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    run.State |> ignore
                    rig.FailNext ()
                    rig.Clock.Value <- 1.0
                    let raised =
                        try
                            run.State |> ignore
                            None
                        with error ->
                            Some error.Message

                    Expect.equal raised (Some "advance failed") "the read raises the advance's exception"
                    rig.X0.Value <- 7.0
                    let s = run.State
                    Expect.equal s.X 7.0 "X is the new start's x0"
                    Expect.equal s.Steps 0 "Steps is 0")
            }
        ]
