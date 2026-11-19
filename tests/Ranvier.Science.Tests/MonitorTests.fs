module Ranvier.Science.Tests.MonitorTests

open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities
open SequentialMonitor

/// Reads every row's cells and band, and the pooled row's cells.
let private readPage (m: Monitor) =
    for r in Monitor.rows m do
        r.Cells () |> ignore
        r.Band.Value |> ignore

    Monitor.pooledCells m |> ignore

let private push (r: Row) (values: float[]) = batch (fun () -> r.Feed.Push values)

let private rowOf (m: Monitor) (key: string) = m.Rows.Get key

/// A monitor with `count` channels whose folds have started.
let private monitorWith (count: int) =
    let m = Monitor.create ()

    for _ in 1..count do
        Monitor.addChannel m |> ignore

    readPage m
    m

let private valuesOf (count: int) (offset: float) =
    Array.init count (fun i -> offset + float ((i * 37) % 11) - 5.0)

let private snapshot (c: RowCounts) = { c with Starts = c.Starts }

let private relative (a: float) (b: float) = abs (a - b) / abs b

let private sameInstance (a: obj) (b: obj) = obj.ReferenceEquals (a, b)

let private line (cells: string[]) = String.concat " | " cells

/// The message of what `body` raises, or None.
let private raisedBy (body: unit -> unit) : string option =
    try
        body ()
        None
    with error ->
        Some error.Message

let private failingStream () =
    async {
        use graph = new Graph ()
        let m = graph.Run (fun () -> monitorWith 2)
        let a, b = graph.Run (fun () -> rowOf m "A", rowOf m "B")

        graph.Run (fun () ->
            Expect.equal
                (Monitor.pooledCells m)
                [| "All"; "waiting for data"; ""; ""; "(pooled, 0 of 2 channels)"; "" |]
                "the pooled cells with no channel showing")

        graph.Run (fun () ->
            push a (valuesOf 40 0.0)
            push b (valuesOf 40 1.0))

        graph.Run (fun () -> readPage m)
        let bCells = graph.Run (fun () -> b.Cells ())
        a.Feed.Fail (exn "boom")
        do! drained ()

        graph.Run (fun () ->
            Expect.equal (raisedBy (fun () -> readPage m)) None "reading the page raises nothing"
            Expect.equal (a.State ()) (RowState.Failed "boom") "A's state"
            Expect.equal (a.Cells ()).[1] "failed: boom" "A's second cell"
            Expect.equal (b.Cells ()) bCells "B's cells"
            let pooled = m.Pooled.Value.Value

            match b.Observations.TryValue with
            | Ready f ->
                Expect.equal (Moments.count pooled) (Moments.count f.Moments) "the pooled count is B's"
                Expect.isLessThanOrEqual (relative (Moments.mean pooled) (Moments.mean f.Moments)) 1e-12 "the pooled mean is B's"
            | other -> failtestf "B has no value: %A" other

            Expect.equal (Monitor.pooledCells m).[4] "(pooled, 1 of 2 channels)" "the pooled note")
    }

#if !FABLE_COMPILER
let private threadTests =
    [
        testCase "items from a second thread fold to the same state"
        <| fun _ ->
            use graph = manualGraph ()
            let m = graph.Run (fun () -> monitorWith 1)
            let a = graph.Run (fun () -> rowOf m "A")
            new Effect (graph, fun () -> a.Observations.TryValue |> ignore) |> ignore
            graph.Pump () |> ignore
            let values = Array.init 10_000 (fun i -> 1.0 + float (i % 977) * 0.25)
            offThread (fun () -> a.Feed.Push values)
            graph.Pump () |> ignore
            let single = values |> Array.fold Folded.add Folded.zero

            match graph.Run (fun () -> a.Observations.TryValue) with
            | Ready f ->
                Expect.equal (Moments.count f.Moments) 10000.0 "every item counted"
                Expect.equal (Moments.mean f.Moments) (Moments.mean single.Moments) "the same-thread fold's mean"
                Expect.equal (Moments.variance f.Moments) (Moments.variance single.Moments) "the same-thread fold's variance"
            | other -> failtestf "expected a value, got %A" other
    ]
#else
let private threadTests = []
#endif

[<Tests>]
let tests =
    testList "Monitor" ([
        test "a channel waits for its first item" {
            withGraph (fun () ->
                let m = monitorWith 1
                let a = rowOf m "A"
                Expect.equal (a.State ()) RowState.Waiting "before any item"
                Expect.equal (a.Cells ()) [| "A"; "waiting for data"; ""; ""; ""; "" |] "the waiting cells"
                push a [| 1.0 |]
                Expect.equal (a.State ()) RowState.Showing "after one item")
        }

        test "the recorded stream gives the pinned table text" {
            withGraph (fun () ->
                let m = monitorWith 2
                push (rowOf m "A") Recorded.stream
                push (rowOf m "B") (Recorded.stream |> Array.map (fun x -> 2.0 - x))
                m.FormulaText.Value <- "(mean - target) / se"
                let lines = [ for r in Monitor.rows m -> line (r.Cells ()) ] @ [ line (Monitor.pooledCells m) ]

                Expect.equal lines Recorded.tableLines "the three lines")
        }

        test "changing alpha folds nothing and recomputes each channel's bounds and band once" {
            withGraph (fun () ->
                let m = monitorWith 2
                let all = Monitor.rows m

                for r in all do
                    push r (valuesOf 2000 0.0)

                readPage m
                let before = all |> Array.map (fun r -> snapshot r.Counts, r.Feed.Delivered, r.Observations.Peek)
                Expect.equal (Monitor.setAlpha m "0.01") (Result.Ok ()) "α set"
                readPage m

                for r, (counts, delivered, peek) in Array.zip all before do
                    Expect.equal r.Counts.Bounds (counts.Bounds + 1) (r.Key + ": bounds")
                    Expect.equal r.Counts.Bands (counts.Bands + 1) (r.Key + ": band")
                    Expect.equal r.Counts.Starts counts.Starts (r.Key + ": starts")
                    Expect.equal r.Counts.Means counts.Means (r.Key + ": means")
                    Expect.equal r.Feed.Delivered delivered (r.Key + ": delivered")
                    Expect.isTrue (sameInstance r.Observations.Peek peek) (r.Key + ": the same folded state"))
        }

        test "adding a channel recomputes the split and re-runs no fold" {
            withGraph (fun () ->
                let m = monitorWith 2
                let existing = Monitor.rows m

                for r in existing do
                    push r (valuesOf 100 0.0)

                readPage m
                let splits = m.AlphaSplits
                let before = existing |> Array.map (fun r -> snapshot r.Counts, r.Observations.Peek)
                Expect.equal (Monitor.addChannel m) "C" "the new key"
                readPage m
                Expect.equal m.AlphaSplits (splits + 1) "splits"

                for r, (counts, peek) in Array.zip existing before do
                    Expect.equal r.Counts.Bounds (counts.Bounds + 1) (r.Key + ": bounds")
                    Expect.equal r.Counts.Starts counts.Starts (r.Key + ": starts")
                    Expect.isTrue (sameInstance r.Observations.Peek peek) (r.Key + ": the same folded state"))
        }

        test "a publication on one channel recomputes that row alone and edits the pooled moments once" {
            withGraph (fun () ->
                let m = monitorWith 2
                let a, b = rowOf m "A", rowOf m "B"
                push a (valuesOf 100 0.0)
                push b (valuesOf 100 1.0)
                readPage m
                let ca, cb, refolds = snapshot a.Counts, snapshot b.Counts, m.Pooled.Refolds ()
                push a [| 0.5 |]
                readPage m
                Expect.equal a.Counts.Means (ca.Means + 1) "A: means"
                Expect.equal a.Counts.Bounds (ca.Bounds + 1) "A: bounds"
                Expect.equal a.Counts.Settled (ca.Settled + 1) "A: settled"
                Expect.equal b.Counts.Means cb.Means "B: means"
                Expect.equal b.Counts.Bounds cb.Bounds "B: bounds"
                Expect.equal b.Counts.Settled cb.Settled "B: settled"
                Expect.equal (m.Pooled.Refolds ()) refolds "re-folds")
        }

        test "a source parameter restarts that channel from its seed" {
            withGraph (fun () ->
                let m = monitorWith 2
                let a, b = rowOf m "A", rowOf m "B"
                push a (valuesOf 100 0.0)
                push b (valuesOf 100 1.0)
                readPage m
                let ca, cb, bCells = snapshot a.Counts, snapshot b.Counts, b.Cells ()
                a.TrueMean.Set 1.0<mK>
                readPage m
                Expect.equal a.Counts.Starts (ca.Starts + 1) "A: starts"
                push a (valuesOf 5 0.0)
                readPage m
                Expect.equal (float a.N.Value) 5.0 "A: the items delivered since"
                Expect.equal (snapshot b.Counts) cb "B: counts"
                Expect.equal (b.Cells ()) bCells "B: cells")
        }

        test "removing a channel cancels its stream" {
            withGraph (fun () ->
                let m = monitorWith 2
                let a, b = rowOf m "A", rowOf m "B"
                push a (valuesOf 300 0.0)
                push b (valuesOf 200 1.0)
                readPage m
                let starts = b.Feed.Starts
                let split = float m.AlphaPerChannel.Value
                let refolds = m.Pooled.Refolds ()
                Monitor.removeChannel m "B"
                readPage m
                Expect.equal (b.Feed.EmitAtRate (1000.0, 0.1)) 0 "the removed feed delivers nothing"
                let pooled = m.Pooled.Value.Value
                let scratch = m.PooledFromScratch.Value
                Expect.equal (Moments.count pooled) (Moments.count scratch) "the pooled count"

                if m.Pooled.Refolds () > refolds then
                    Expect.equal (Moments.mean pooled) (Moments.mean scratch) "the from-scratch mean"
                    Expect.equal (Moments.variance pooled) (Moments.variance scratch) "the from-scratch variance"
                else
                    Expect.isLessThanOrEqual (relative (Moments.mean pooled) (Moments.mean scratch)) 1e-12 "the mean"
                    Expect.isLessThanOrEqual (relative (Moments.variance pooled) (Moments.variance scratch)) 1e-9 "the variance"

                Expect.isGreaterThan (float m.AlphaPerChannel.Value) split "α per channel"
                Monitor.restart m
                readPage m
                Expect.equal b.Feed.Starts starts "no further start of the removed channel")
        }

        test "no channels" {
            withGraph (fun () ->
                let m = monitorWith 1
                Monitor.removeChannel m "A"
                readPage m
                Expect.isEmpty (Monitor.rows m) "rows"
                Expect.equal (Monitor.pooledCells m) [| "All"; "waiting for data"; ""; ""; ""; "" |] "pooled cells"
                Expect.equal (float m.AlphaPerChannel.Value) (float m.Alpha.Value) "α per channel"
                let key = Monitor.addChannel m
                readPage m
                Expect.equal ((rowOf m key).Cells ()) [| key; "waiting for data"; ""; ""; ""; "" |] "a waiting row")
        }

        test "a formula that fails keeps the last valid values" {
            withGraph (fun () ->
                let m = monitorWith 2

                for r in Monitor.rows m do
                    push r (valuesOf 100 (float r.Key.Length))

                m.FormulaText.Value <- "(mean - target) / se"
                readPage m
                let valid = m.Formula.Value.Valid
                let users = Monitor.rows m |> Array.map (fun r -> r.User.Value)
                m.FormulaText.Value <- "mean + rate"
                readPage m
                let failed = m.Formula.Value

                match valid, failed.Valid with
                | ValueSome a, ValueSome b -> Expect.isTrue (sameInstance a b) "the previous Valid instance"
                | _ -> failtest "a Valid formula is expected"

                Expect.isTrue failed.Error.IsSome "an error"
                Expect.equal (Monitor.rows m |> Array.map (fun r -> r.User.Value)) users "the users' values"
                m.FormulaText.Value <- ""
                readPage m
                Expect.equal m.Formula.Value.Valid ValueNone "no Valid formula"
                Expect.equal m.Formula.Value.Error ValueNone "no error"

                for r in Monitor.rows m do
                    Expect.equal (r.Cells ()).[5] "" (r.Key + ": an empty formula cell"))
        }

        test "a control out of range is refused" {
            withGraph (fun () ->
                let m = monitorWith 1
                let a = rowOf m "A"
                push a (valuesOf 100 0.0)
                readPage m
                let counts, splits = snapshot a.Counts, m.AlphaSplits
                let inputs () = float m.Alpha.Value, float m.TightestAt.Value, float m.Target.Value, float m.Rate.Value
                let before = inputs ()

                let refused =
                    [
                        Monitor.setAlpha m "0"
                        Monitor.setAlpha m "1"
                        Monitor.setAlpha m "abc"
                        Monitor.setTightestAt m "0"
                        Monitor.setRate m "-1"
                        Monitor.setRate m "1e12"
                        Monitor.setTarget m "nan"
                    ]

                for result in refused do
                    match result with
                    | Result.Error _ -> ()
                    | Result.Ok () -> failtest "a value out of range was accepted"

                readPage m
                Expect.equal (inputs ()) before "the inputs"
                Expect.equal (snapshot a.Counts) counts "the row's counts"
                Expect.equal m.AlphaSplits splits "the split")
        }

        test "a non-finite observation marks its channel alone" {
            withGraph (fun () ->
                let m = monitorWith 2
                let a, b = rowOf m "A", rowOf m "B"
                push a (valuesOf 100 0.0)
                push b (valuesOf 100 1.0)
                readPage m
                let bCells = b.Cells ()
                push a [| nan |]
                readPage m
                Expect.equal (a.State ()) RowState.NotFinite "A's state"
                Expect.equal (a.Cells ()) [| "A"; "101"; "not finite"; ""; ""; "" |] "A's cells"
                Expect.equal (b.Cells ()) bCells "B's cells"
                let pooled = m.Pooled.Value.Value
                Expect.isTrue (abs (Moments.mean pooled) < infinity) "a finite pooled mean"
                Expect.isTrue (abs (Moments.variance pooled) < infinity) "a finite pooled variance"
                Expect.equal (Monitor.pooledCells m).[4] "(pooled, 1 of 2 channels)" "the pooled note")
        }

        testCaseAsync "a failed stream shows its message" (failingStream ())

        test "a long frame is limited, and a paused page delivers nothing" {
            withGraph (fun () ->
                let m = monitorWith 2
                Expect.equal (Monitor.setRate m "1024") (Result.Ok ()) "rate set"
                let delivered () = Monitor.rows m |> Array.map (fun r -> r.Feed.Delivered)
                let before = delivered ()
                Monitor.frame m 60.0
                Expect.equal (delivered ()) (before |> Array.map (fun d -> d + 102)) "102 items per channel"
                readPage m
                let after = delivered ()
                Monitor.frame m nan
                Monitor.frame m -1.0
                Expect.equal (delivered ()) after "a NaN or negative dt delivers nothing"
                m.Running.Value <- false
                Monitor.frame m 0.05
                Expect.equal (delivered ()) after "a paused page delivers nothing")
        }

        test "the sequence waits for the burn-in" {
            withGraph (fun () ->
                let m = monitorWith 1
                let a = rowOf m "A"
                push a (valuesOf 29 0.0)
                readPage m
                Expect.equal (a.Cells ()).[4] "after 30 observations" "the sequence cell at 29"
                Expect.equal a.Band.Value.Counts.Length 0 "the band at 29"
                push a [| 0.5 |]
                readPage m
                let cell = (a.Cells ()).[4]
                Expect.stringStarts cell "[" "the sequence cell at 30 opens an interval"
                Expect.isTrue (cell.EndsWith "] mK") (sprintf "the sequence cell at 30, %s, closes an interval in mK" cell)
                Expect.isFalse (cell.Contains "NaN") "the sequence cell at 30 shows numbers")
        }

        test "a small change of alpha returns the same band" {
            withGraph (fun () ->
                let m = monitorWith 1
                let a = rowOf m "A"
                push a (valuesOf 2000 0.0)
                readPage m
                let band = a.Band.Value
                Expect.isGreaterThan band.Counts.Length 0 "a band with elements"
                m.Alpha.Set 0.05000001
                readPage m
                Expect.isTrue (sameInstance a.Band.Value band) "the same band"
                m.Alpha.Set 0.01
                readPage m
                Expect.isFalse (sameInstance a.Band.Value band) "a new band")
        }

        test "pooled, maintained and from scratch" {
            withGraph (fun () ->
                let m = monitorWith 4

                for i in 1..600 do
                    Monitor.frame m (1.0 / 60.0)
                    let refolds = m.Pooled.Refolds ()
                    readPage m
                    let pooled = m.Pooled.Value.Value
                    let rose = m.Pooled.Refolds () > refolds

                    if rose then
                        Expect.equal (Monitor.pooledDistance m) 0.0 (sprintf "frame %d: the distance at a re-fold" i)
                    else
                        let scratch = m.PooledFromScratch.Value

                        Expect.isLessThanOrEqual
                            (relative (Moments.mean pooled) (Moments.mean scratch))
                            1e-12
                            (sprintf "frame %d: the mean" i)

                        Expect.isLessThanOrEqual
                            (relative (Moments.variance pooled) (Moments.variance scratch))
                            1e-9
                            (sprintf "frame %d: the variance" i)

                Expect.isLessThanOrEqual (m.Pooled.Refolds ()) 40 "re-folds")
        }

        test "restart begins every channel from its seed" {
            withGraph (fun () ->
                let m = monitorWith 2
                let all = Monitor.rows m
                let a = rowOf m "A"
                batch (fun () -> a.Feed.EmitAtRate (16.0, 1.0)) |> ignore
                readPage m
                let first = a.Observations.Peek
                let before = all |> Array.map (fun r -> r.Counts.Starts)
                Monitor.restart m
                readPage m
                Expect.equal (all |> Array.map (fun r -> r.Counts.Starts)) (before |> Array.map (fun s -> s + 1)) "starts"
                batch (fun () -> a.Feed.EmitAtRate (16.0, 1.0)) |> ignore
                readPage m
                let again = a.Observations.Peek
                Expect.equal (Moments.count again.Moments) 16.0 "16 observations"
                Expect.equal (Moments.mean again.Moments) (Moments.mean first.Moments) "the mean of the first start"
                Expect.equal (Moments.variance again.Moments) (Moments.variance first.Moments) "the variance of the first start"
                Expect.equal (History.samples again.History) (History.samples first.History) "the samples of the first start")
        }

        test "a freed letter is reused and the rows stay in channel order" {
            withGraph (fun () ->
                let m = monitorWith 3
                Monitor.removeChannel m "B"
                Expect.equal (Monitor.addChannel m) "B" "the freed key"
                readPage m
                let b = rowOf m "B"
                Expect.equal (float b.Seed.Value) 2.0 "B's seed"
                Expect.equal b.TrueMean.Value -0.02<mK> "B's true mean"
                Expect.equal (Monitor.rows m |> Array.map (fun r -> r.Key)) [| "A"; "B"; "C" |] "the keys in channel order"

                for _ in 4..26 do
                    Monitor.addChannel m |> ignore

                Expect.equal (Monitor.addChannel m) "AA" "the key after Z"
                let keys = Monitor.rows m |> Array.map (fun r -> r.Key)
                Expect.equal keys[keys.Length - 2 ..] [| "Z"; "AA" |] "the last keys")
        }

        test "the diagnostics report the frame before" {
            withGraph (fun () ->
                let m = monitorWith 2
                Expect.equal (Monitor.setRate m "1024") (Result.Ok ()) "rate set"

                let aFrame () =
                    Monitor.frame m 0.015625
                    readPage m

                aFrame ()
                aFrame ()
                Expect.equal m.Diagnostics.Value { Items = 32; Rows = 2; Restarts = 0 } "after two frames"
                let second = m.Diagnostics.Value
                aFrame ()
                Expect.isTrue (sameInstance m.Diagnostics.Value second) "an equal tally is not written"
                m.Running.Value <- false
                aFrame ()
                aFrame ()
                Expect.equal m.Diagnostics.Value { Items = 0; Rows = 0; Restarts = 0 } "paused"
                m.Running.Value <- true
                (rowOf m "A").TrueMean.Set 1.0<mK>
                aFrame ()
                let first = m.Diagnostics.Value.Restarts
                aFrame ()
                let next = m.Diagnostics.Value.Restarts
                Expect.equal (List.sort [ first; next ]) [ 0; 1 ] "one restart in exactly one reading")
        }

        test "a channel removed and added again between frames restarts nothing it did not restart" {
            withGraph (fun () ->
                let m = monitorWith 2
                let a, b = rowOf m "A", rowOf m "B"
                a.TrueMean.Set 1.0<mK>
                readPage m
                a.TrueMean.Set 2.0<mK>
                readPage m
                Expect.equal a.Counts.Starts 3 "A's starts before the frame"
                Monitor.frame m (1.0 / 60.0)
                readPage m
                Monitor.removeChannel m "A"
                readPage m
                Expect.equal (Monitor.addChannel m) "A" "the key added again"
                readPage m
                Expect.isFalse (sameInstance (rowOf m "A") a) "a new row under the key"
                b.TrueMean.Set 1.0<mK>
                readPage m
                Monitor.frame m (1.0 / 60.0)
                let restarts = m.Diagnostics.Value.Restarts
                Expect.isTrue (restarts >= 0) (sprintf "restarts %d, not negative" restarts)
                Expect.equal restarts 1 "the restart of B, the one row present at both frames")
        }

        test "a formula that divides by zero shows an infinity or NaN in its cell" {
            withGraph (fun () ->
                let m = monitorWith 1
                let a = rowOf m "A"
                push a (valuesOf 40 0.0)
                readPage m
                Expect.equal (a.State ()) RowState.Showing "a showing row"

                for formula, cell in [ "1 / (n - n)", "∞"; "(n - n) / (n - n)", "NaN" ] do
                    m.FormulaText.Value <- formula
                    Expect.equal (raisedBy (fun () -> readPage m)) None (formula + ": reading the page raises nothing")
                    Expect.equal m.Formula.Value.Error ValueNone (formula + ": no error")
                    Expect.equal (a.Cells ()).[5] cell formula)
        }

        test "the schema covers a row" {
            withGraph (fun () ->
                let m = monitorWith 1
                let row = rowOf m "A"
                let entries = row.Model.Entries
                Expect.equal (List.length entries) 9 "the row's entries"

                for entry in entries do
                    m.FormulaText.Value <- entry.Id

                    match m.Formula.Value.Valid with
                    | ValueSome c when m.Formula.Value.Error = ValueNone ->
                        Expect.isTrue (UnitInfo.equivalent c.Unit entry.Unit) (entry.Id + ": an equivalent unit")
                    | _ -> failtestf "%s is not in the schema: %A" entry.Id m.Formula.Value.Error

                // The reverse: a row's model, or the page's model above it, declares every identifier of the schema.
                Expect.isNonEmpty m.Schema.Entries "the schema's entries"

                for entry in m.Schema.Entries do
                    match row.Model.TryFind entry.Id with
                    | ValueSome declared ->
                        Expect.isTrue (UnitInfo.equivalent declared.Unit entry.Unit) (entry.Id + ": declared with an equivalent unit")
                    | ValueNone -> failtestf "%s of the schema is declared by neither the row's model nor the page's" entry.Id)
        }
    ] @ threadTests)
