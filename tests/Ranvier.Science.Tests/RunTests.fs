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

let private odeTight: Ranvier.Numerics.OdeOptions =
    { Ranvier.Numerics.OdeOptions.standard with AbsoluteTolerance = 1e-10; RelativeTolerance = 1e-10 }

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
                    Expect.equal run.State.X (4.0 + 2.0 * 0.1) "X is x0 + rate * 0.1"
                    rig.Clock.Value <- 2.5625
                    run.State |> ignore
                    Expect.equal rig.Gaps [ 0.1; 0.0625 ] "the next step is measured from the cut step's clock value, 2.5")
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

            test "a quantity written away and back between reads restarts the run" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let first = run.State
                    rig.X0.Value <- 7.0
                    rig.X0.Value <- 4.0
                    let second = run.State
                    Expect.isFalse (obj.ReferenceEquals (second, first)) "writes away and back: a new state object"
                    Expect.equal second.Steps 0 "writes away and back: Steps is 0"
                    Expect.equal second.X 4.0 "writes away and back: X is x0"
                    Expect.equal rig.Starts 2 "writes away and back: start ran again"

                    batch (fun () ->
                        rig.X0.Value <- 9.0
                        rig.X0.Value <- 4.0)

                    let third = run.State
                    Expect.isFalse (obj.ReferenceEquals (third, second)) "two writes in one batch: a new state object"
                    Expect.equal third.Steps 0 "two writes in one batch: Steps is 0"
                    Expect.equal third.X 4.0 "two writes in one batch: X is x0"
                    Expect.equal rig.Starts 3 "two writes in one batch: start ran again")
            }

            test "a control read through a derived quantity does not restart the run when written away and back" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let x0 = createSignal 4.0
                    let x0Value = createMemo (fun _ -> x0.Value)
                    let model = Model.create "page"
                    let tilt = Quantity.input model "tilt" "Tilt" Units.one 4.0
                    let tiltValue = Quantity.derive model "tilt_value" "Tilt value" Units.one (fun () -> tilt.Value)
                    let mutable memoStarts = 0
                    let mutable derivedStarts = 0

                    let advance (s: Sim) (dt: float) =
                        s.X <- s.X + dt
                        s.Steps <- s.Steps + 1

                    let throughMemo =
                        Run.create
                            (fun () -> clock.Value)
                            (fun () ->
                                memoStarts <- memoStarts + 1
                                { X = x0Value.Value; Steps = 0 })
                            advance

                    let throughDerived =
                        Run.create
                            (fun () -> clock.Value)
                            (fun () ->
                                derivedStarts <- derivedStarts + 1
                                { X = float tiltValue.Value; Steps = 0 })
                            advance

                    let memoFirst = throughMemo.State
                    let derivedFirst = throughDerived.State

                    let expect (label: string) (sameObject: bool) (steps: int) (x: float) (starts: int) =
                        for route, s, first, count in
                            [
                                "memo", throughMemo.State, memoFirst, memoStarts
                                "derived quantity", throughDerived.State, derivedFirst, derivedStarts
                            ] do
                            let at = route + ", " + label
                            Expect.equal (obj.ReferenceEquals (s, first)) sameObject (at + ": the same state object")
                            Expect.equal s.Steps steps (at + ": Steps")
                            Expect.equal s.X x (at + ": X")
                            Expect.equal count starts (at + ": starts")

                    clock.Value <- 1.0
                    expect "a first advance" true 1 5.0 1
                    x0.Value <- 7.0
                    x0.Value <- 4.0
                    tilt.Set 7.0
                    tilt.Set 4.0
                    clock.Value <- 2.0
                    expect "writes away and back between reads" true 2 6.0 1

                    batch (fun () ->
                        x0.Value <- 9.0
                        x0.Value <- 4.0
                        tilt.Set 9.0
                        tilt.Set 4.0)

                    clock.Value <- 3.0
                    expect "two writes in one batch" true 3 7.0 1
                    x0.Value <- 5.0
                    tilt.Set 5.0
                    clock.Value <- 4.0
                    expect "a real change" false 0 5.0 2)
            }

            test "a control read through a memo that something else reads restarts the run when written away and back" {
                let advance (s: Sim) (dt: float) =
                    s.X <- s.X + dt
                    s.Steps <- s.Steps + 1

                let measure (label: string) (setControl: float -> unit) (readControl: unit -> float) (otherReader: unit -> unit) =
                    let clock = createSignal 0.0
                    let mutable starts = 0

                    let run =
                        Run.create
                            (fun () -> clock.Value)
                            (fun () ->
                                starts <- starts + 1
                                { X = readControl (); Steps = 0 })
                            advance

                    createEffect otherReader
                    let first = run.State
                    clock.Value <- 1.0
                    run.State |> ignore
                    setControl 7.0
                    setControl 4.0
                    clock.Value <- 2.0
                    let afterWrites = run.State
                    Expect.isFalse (obj.ReferenceEquals (afterWrites, first)) (label + ", writes away and back: a new state object")
                    Expect.equal afterWrites.Steps 0 (label + ", writes away and back: Steps is 0")
                    Expect.equal afterWrites.X 4.0 (label + ", writes away and back: X is x0")
                    Expect.equal starts 2 (label + ", writes away and back: start ran again")

                    batch (fun () ->
                        setControl 9.0
                        setControl 4.0)

                    clock.Value <- 3.0
                    let afterBatch = run.State
                    Expect.isTrue (obj.ReferenceEquals (afterBatch, afterWrites)) (label + ", two writes in one batch: the same state object")
                    Expect.equal afterBatch.Steps 1 (label + ", two writes in one batch: Steps is 1")
                    Expect.equal afterBatch.X 5.0 (label + ", two writes in one batch: X is x0 + 1")
                    Expect.equal starts 2 (label + ", two writes in one batch: no further start")

                withGraph (fun () ->
                    let x0 = createSignal 4.0
                    let x0Value = createMemo (fun _ -> x0.Value)
                    measure "a memo an effect also reads" (fun v -> x0.Value <- v) (fun () -> x0Value.Value) (fun () -> x0Value.Value |> ignore))

                withGraph (fun () ->
                    let model = Model.create "page"
                    let tilt = Quantity.input model "tilt" "Tilt" Units.one 4.0
                    let tiltValue = Quantity.derive model "tilt_value" "Tilt value" Units.one (fun () -> tilt.Value)

                    measure
                        "a derived quantity an effect also reads through Entry.Read"
                        (fun v -> tilt.Set v)
                        (fun () -> float tiltValue.Value)
                        (fun () -> tiltValue.Entry.Read () |> ignore))

                withGraph (fun () ->
                    let model = Model.create "page"
                    let tilt = Quantity.input model "tilt" "Tilt" Units.one 4.0
                    let tiltValue = Quantity.derive model "tilt_value" "Tilt value" Units.one (fun () -> tilt.Value)

                    measure
                        "a derived quantity an effect also reads through Entry.Status"
                        (fun v -> tilt.Set v)
                        (fun () -> float tiltValue.Value)
                        (fun () -> tiltValue.Entry.Status () |> ignore))
            }

            test "a control read through a memo created for the run alone does not restart the run when written away and back" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let model = Model.create "page"
                    let tilt = Quantity.input model "tilt" "Tilt" Units.one 4.0
                    let tiltValue = Quantity.derive model "tilt_value" "Tilt value" Units.one (fun () -> tilt.Value)
                    let forTheRun = createMemo (fun _ -> float tilt.Value)
                    let mutable starts = 0

                    let run =
                        Run.create
                            (fun () -> clock.Value)
                            (fun () ->
                                starts <- starts + 1
                                { X = forTheRun.Value; Steps = 0 })
                            (fun s dt ->
                                s.X <- s.X + dt
                                s.Steps <- s.Steps + 1)

                    createEffect (fun () -> tilt.Value |> ignore)
                    createEffect (fun () -> tiltValue.Entry.Read () |> ignore)
                    let first = run.State
                    clock.Value <- 1.0
                    run.State |> ignore
                    tilt.Set 7.0
                    tilt.Set 4.0
                    clock.Value <- 2.0
                    let afterWrites = run.State
                    Expect.isTrue (obj.ReferenceEquals (afterWrites, first)) "writes away and back: the same state object"
                    Expect.equal afterWrites.Steps 2 "writes away and back: Steps is 2"
                    Expect.equal afterWrites.X 6.0 "writes away and back: X is x0 + 2"
                    Expect.equal starts 1 "writes away and back: one start"

                    batch (fun () ->
                        tilt.Set 9.0
                        tilt.Set 4.0)

                    clock.Value <- 3.0
                    let afterBatch = run.State
                    Expect.isTrue (obj.ReferenceEquals (afterBatch, first)) "two writes in one batch: the same state object"
                    Expect.equal afterBatch.Steps 3 "two writes in one batch: Steps is 3"
                    Expect.equal afterBatch.X 7.0 "two writes in one batch: X is x0 + 3"
                    Expect.equal starts 1 "two writes in one batch: one start")
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

            test "after a failed advance a clock change advances over the gap from the last recorded clock" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let gaps = ResizeArray<float>()
                    let mutable failAfterMutation = false

                    let run =
                        Run.create
                            (fun () -> clock.Value)
                            (fun () -> { X = 0.0; Steps = 0 })
                            (fun s dt ->
                                gaps.Add dt
                                s.X <- s.X + dt
                                s.Steps <- s.Steps + 1

                                if failAfterMutation then
                                    failAfterMutation <- false
                                    failwith "after mutation")

                    let first = run.State
                    clock.Value <- 1.0
                    run.State |> ignore
                    failAfterMutation <- true
                    clock.Value <- 2.0
                    Expect.throws (fun () -> run.State |> ignore) "the read at clock 2 raises"
                    Expect.throws (fun () -> run.State |> ignore) "a second read at clock 2 raises again"
                    Expect.equal (List.ofSeq gaps) [ 1.0; 1.0 ] "the failed advance ran once"
                    clock.Value <- 4.0
                    let s = run.State
                    Expect.isTrue (obj.ReferenceEquals (s, first)) "the same state object"
                    Expect.equal (List.ofSeq gaps) [ 1.0; 1.0; 3.0 ] "the retry advances over the gap from 1.0, the last recorded clock"
                    Expect.equal s.Steps 3 "the failed call's step is kept"
                    Expect.equal s.X 5.0 "X is 1 + 1, from the failed call, + 3")
            }

            test "a clock function that returns its last value after an input changes leaves the run" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = Run.create (fun () -> floor rig.Clock.Value) rig.Start rig.Advance
                    let first = run.State
                    let mutable runs = 0

                    createEffect (fun () ->
                        run.State |> ignore
                        runs <- runs + 1)

                    rig.Clock.Value <- 1.0
                    run.State |> ignore
                    rig.Clock.Value <- 1.5
                    let s = run.State
                    Expect.isTrue (obj.ReferenceEquals (s, first)) "the same state object"
                    Expect.equal rig.Advances 1 "advance ran once, at clock 1"
                    Expect.equal s.Steps 1 "Steps is 1"
                    Expect.equal runs 2 "the reader ran at creation and after the advance, not after the equal clock")
            }

            test "a reader is notified after a start and after a backward restart" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)
                    let run = rig.Run ()
                    let seen = ResizeArray<float * int>()
                    createEffect (fun () -> seen.Add (run.State.X, run.State.Steps))
                    rig.Clock.Value <- 1.0
                    rig.X0.Value <- 9.0
                    rig.Clock.Value <- 0.5

                    Expect.equal
                        (List.ofSeq seen)
                        [ (4.0, 0); (6.0, 1); (9.0, 0); (9.0, 0) ]
                        "the first start, an advance, a start for x0, and a start for the clock going back"

                    Expect.equal rig.Starts 3 "start ran three times")
            }

            test "maxStep must be greater than 0" {
                withGraph (fun () ->
                    let rig = Rig (4.0, 2.0)

                    for maxStep in [ 0.0; -0.5; nan ] do
                        Expect.throwsT<System.ArgumentOutOfRangeException> (fun () -> rig.RunWith maxStep |> ignore) (sprintf "maxStep %g" maxStep)

                    rig.Clock.Value <- 2.0
                    let run = rig.RunWith infinity
                    run.State |> ignore
                    rig.Clock.Value <- 5.0
                    Expect.equal run.State.X (4.0 + 2.0 * 3.0) "maxStep infinity takes the whole gap")
            }

            test "an ode run starts at time 0 with a copy of the start state" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let y0 = createSignal 1.0
                    let k = createSignal 0.5
                    let rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]
                    let run = Run.ode odeTight (fun () -> clock.Value) (fun () -> [| y0.Value |]) rhs
                    Expect.equal run.State.Time 0.0 "time starts at 0"
                    Expect.equal run.State.State [| 1.0 |] "the start state"

                    let held = [| 1.0 |]
                    let heldRun = Run.ode odeTight (fun () -> clock.Value) (fun () -> held) rhs
                    Expect.equal heldRun.State.State [| 1.0 |] "the held start state"
                    held[0] <- 9.0
                    Expect.equal heldRun.State.State[0] 1.0 "the run's state is a copy of the start array")
            }

            test "a clock gap integrates the state" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let y0 = createSignal 1.0
                    let k = createSignal 0.5
                    let rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]
                    let run = Run.ode odeTight (fun () -> clock.Value) (fun () -> [| y0.Value |]) rhs
                    run.State |> ignore
                    clock.Value <- 2.0
                    // closed form y = exp (-k t) with k = 0.5, t = 2
                    Expect.equal run.State.Time 2.0 "time after the first gap"
                    expectClose 0.0 1e-9 (exp -1.0) run.State.State[0] "y at t = 2"
                    clock.Value <- 3.0
                    Expect.equal run.State.Time 3.0 "time after the second gap"
                    expectClose 0.0 1e-9 (exp -1.5) run.State.State[0] "y at t = 3")
            }

            test "a clock move inside the band leaves the time, state and array unchanged" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let k = createSignal 0.5
                    let rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]
                    let run = Run.ode odeTight (fun () -> clock.Value) (fun () -> [| 1.0 |]) rhs
                    run.State |> ignore
                    clock.Value <- 2.0
                    let time = run.State.Time
                    let state = run.State.State[0]
                    Expect.equal time 2.0 "advanced to the clock"
                    let array = run.State.State
                    // 1e-10 below the last clock is inside the 1e-9 band of Run.create: no advance
                    clock.Value <- 2.0 - 1e-10
                    Expect.equal run.State.Time time "time is unchanged inside the band"
                    Expect.equal run.State.State[0] state "state is unchanged inside the band"
                    Expect.isTrue (obj.ReferenceEquals (run.State.State, array)) "the state array is the same instance")
            }

            test "a value read by start restarts the run" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let y0 = createSignal 1.0
                    let k = createSignal 0.5
                    let rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]
                    let run = Run.ode odeTight (fun () -> clock.Value) (fun () -> [| y0.Value |]) rhs
                    run.State |> ignore
                    clock.Value <- 2.0
                    Expect.equal run.State.Time 2.0 "advanced"
                    y0.Value <- 4.0
                    Expect.equal run.State.Time 0.0 "restarted at time 0"
                    Expect.equal run.State.State [| 4.0 |] "restarted from the new start state")
            }

            test "reads inside rhs do not restart the run" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let y0 = createSignal 1.0
                    let k = createSignal 0.5
                    let rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]
                    let run = Run.ode odeTight (fun () -> clock.Value) (fun () -> [| y0.Value |]) rhs
                    run.State |> ignore
                    clock.Value <- 1.0
                    Expect.equal run.State.Time 1.0 "time after the first gap"
                    k.Value <- 1.0
                    Expect.equal run.State.Time 1.0 "a change of k keeps the time"
                    clock.Value <- 2.0
                    // 0.5 over [0, 1] then 1.0 over [1, 2]
                    expectClose 0.0 1e-9 (exp -0.5 * exp -1.0) run.State.State[0] "y at t = 2")
            }

            test "a failed solve fails the read and a later clock retries" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let k = createSignal 0.5
                    let poisoned = ref true

                    let rhs =
                        fun _ (y: float[]) -> if poisoned.Value then [| nan |] else [| -k.Value * y[0] |]

                    let options = { odeTight with MaximumSteps = 50 }
                    let run = Run.ode options (fun () -> clock.Value) (fun () -> [| 1.0 |]) rhs
                    run.State |> ignore
                    clock.Value <- 1.0
                    Expect.throwsT<System.InvalidOperationException> (fun () -> run.State |> ignore) "the failed solve raises"
                    poisoned.Value <- false
                    clock.Value <- 2.0
                    Expect.equal run.State.Time 2.0 "the retry integrates from the last recorded clock"
                    expectClose 0.0 1e-9 (exp -1.0) run.State.State[0] "y at t = 2")
            }
        ]
