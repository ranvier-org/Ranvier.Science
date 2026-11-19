module Ranvier.Science.Tests.AggregateTests

open Expecto
open Ranvier
open Ranvier.Numerics

let private keyOf (struct (k: int, _: 'V)) = k
let private valueOf (struct (_: int, v: 'V)) = v

/// The rows `0 .. values.Length - 1`, in key order.
let private rowsOf (values: 'V[]) =
    let rows = createKeyedCollection keyOf

    for k in 0 .. values.Length - 1 do
        rows.AddOrUpdate (struct (k, values[k]))

    rows

let private plainFold compensated (values: float[]) =
    values |> Array.fold (Sum.add compensated) Sum.zero |> Sum.value

let private momentsOf (seed: int) =
    Array.init 50 (fun j -> 100.0 + float ((seed * 31 + j * 17) % 1000) / 7.0)
    |> Array.fold (Moments.add true) Moments.zero

let private relative (a: float) (b: float) = abs (a - b) / abs b

#if !FABLE_COMPILER
let private moreThreadTests =
    [
        testCase ".NET: moments folded from a second thread count every item"
        <| fun _ ->
            use graph = manualGraph ()
            let feed = ManualStream<float>()
            let items = Array.init 10_000 (fun i -> 1.0 + float (i % 977) * 0.25)

            let memo =
                graph.Run (fun () -> createStreamFold Moments.zero (Moments.add true) (fun _ -> feed.Source))

            new Effect (graph, fun () -> memo.TryValue |> ignore) |> ignore
            graph.Pump () |> ignore

            offThread (fun () ->
                for x in items do
                    feed.Push x)

            graph.Pump () |> ignore
            let single = items |> Array.fold (Moments.add true) Moments.zero

            match memo.TryValue with
            | Ready m ->
                Expect.equal (Moments.count m) 10000.0 "every item counted"
                Expect.equal (Moments.mean m) (Moments.mean single) "the single-thread fold's mean"
            | other -> failtestf "expected a value, got %A" other
    ]
#else
let private moreThreadTests = []
#endif

[<Tests>]
let tests =
    testList "Aggregate" ([
        test "1e20 then 1, then 1e20 removed" {
            for policy, expected in [ FloatPolicy.standard, 1.0; FloatPolicy.fast, 0.0 ] do
                withGraph (fun () ->
                    let rows = rowsOf [| 1e20; 1.0 |]
                    let sum = rows.Rows |> Aggregate.sumBy policy valueOf
                    Expect.equal sum.Value.Value (1e20 + 1.0) "both rows"
                    rows.Remove 0 |> ignore
                    Expect.equal sum.Value.Value expected (sprintf "Compensated = %b" policy.Compensated))
        }

        test "a row edit makes one subtract and one add" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 1000 float)
                let mutable calls = 0

                let counting (struct (_: int, v: float)) =
                    calls <- calls + 1
                    v

                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard counting
                sum.Value.Value |> ignore
                calls <- 0
                rows.AddOrUpdate (struct (500, 12.5))
                Expect.equal sum.Value.Value (exactSum (Array.init 1000 (fun k -> if k = 500 then 12.5 else float k))) "value after the edit"
                Expect.equal calls 1 "projection calls for one edit"
                Expect.equal (sum.Refolds ()) 0 "re-folds")
        }

        test "standard re-folds after as many removals as rows" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 float)
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                sum.Value.Value |> ignore

                for i in 1..9 do
                    rows.AddOrUpdate (struct (i % 10, float (100 + i)))
                    sum.Value.Value |> ignore

                Expect.equal (sum.Refolds ()) 0 "after 9 edits"
                rows.AddOrUpdate (struct (0, 110.0))
                sum.Value.Value |> ignore
                Expect.equal (sum.Refolds ()) 1 "after the 10th edit")
        }

        test "fast never re-folds on finite values" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 float)
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.fast valueOf
                sum.Value.Value |> ignore

                for i in 1..1000 do
                    rows.AddOrUpdate (struct (i % 10, float i * 0.5))
                    sum.Value.Value |> ignore

                Expect.equal (sum.Refolds ()) 0 "re-folds")
        }

        test "after a re-fold the maintained sum has the from-scratch bits" {
            withGraph (fun () ->
                let values = Array.init 10 (fun k -> 0.1 * float (k + 1))
                let rows = rowsOf values
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                sum.Value.Value |> ignore
                let mutable i = 0

                while sum.Refolds () = 0 do
                    i <- i + 1
                    values[i % 10] <- 1e15 / float i + 0.3
                    rows.AddOrUpdate (struct (i % 10, values[i % 10]))
                    let v = sum.Value.Value

                    if sum.Refolds () = 1 then
                        Expect.equal v (plainFold true values) "the fold in key order")
        }

        test "between re-folds the sum stays within 2 ulp" {
            withGraph (fun () ->
#if FABLE_COMPILER
                let edits = 100_000
#else
                let edits = 1_000_000
#endif
                let valueAt (i: int) = 1.0 + (float i * 3.14159) % 999_999.0
                let values = Array.init 1000 valueAt
                let rows = rowsOf values
                let plainRows = rowsOf (Array.copy values)
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                let plain = plainRows.Rows |> Aggregate.sumBy FloatPolicy.fast valueOf
                sum.Value.Value |> ignore
                plain.Value.Value |> ignore
                // The largest distance at the edit before each re-fold, at every 997th edit, and of the fast sum.
                let mutable beforeRefold = 0.0
                let mutable everyPhase = 0.0
                let mutable fast = 0.0

                for e in 1..edits do
                    let k = e % 1000
                    values[k] <- valueAt (1000 + e)
                    rows.AddOrUpdate (struct (k, values[k]))
                    plainRows.AddOrUpdate (struct (k, values[k]))
                    let maintained = sum.Value.Value
                    let unmaintained = plain.Value.Value

                    if e % 1000 = 999 || e % 997 = 0 then
                        let exact = exactSum values
                        let distance = ulps maintained exact
                        fast <- max fast (ulps unmaintained exact)

                        if e % 1000 = 999 then beforeRefold <- max beforeRefold distance
                        if e % 997 = 0 then everyPhase <- max everyPhase distance
                        Expect.isLessThanOrEqual distance 2.0 (sprintf "edit %d" e)

                printfn
                    "Aggregate drift: largest distance %g ulp before a re-fold, %g ulp at every 997th edit, %g ulp under fast, over %d edits, %d re-folds"
                    beforeRefold
                    everyPhase
                    fast
                    edits
                    (sum.Refolds ()))
        }

        test "maintained moments follow from-scratch moments" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 100 momentsOf)
                let maintained = rows.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                let scratch = rows.Rows |> Aggregate.momentsFromScratchBy FloatPolicy.standard valueOf
                maintained.Value.Value |> ignore
                // Sampled at the edit before each re-fold and at every 97th edit.
                let mutable meanDistance = 0.0
                let mutable varianceDistance = 0.0

                for e in 1..10_000 do
                    rows.AddOrUpdate (struct (e % 100, momentsOf (100 + e)))
                    let m = maintained.Value.Value

                    if e % 100 = 99 || e % 97 = 0 then
                        let s = scratch.Value
                        meanDistance <- max meanDistance (relative (Moments.mean m) (Moments.mean s))
                        varianceDistance <- max varianceDistance (relative (Moments.variance m) (Moments.variance s))

                printfn
                    "Aggregate moments drift: largest relative difference %g in the mean, %g in the variance, %d re-folds"
                    meanDistance
                    varianceDistance
                    (maintained.Refolds ())

                Expect.isLessThanOrEqual meanDistance 1e-12 "mean"
                Expect.isLessThanOrEqual varianceDistance 1e-9 "variance")
        }

        test "after a re-fold the maintained moments have the from-scratch bits" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 momentsOf)
                let maintained = rows.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                let scratch = rows.Rows |> Aggregate.momentsFromScratchBy FloatPolicy.standard valueOf
                maintained.Value.Value |> ignore
                let mutable i = 0

                while maintained.Refolds () = 0 do
                    i <- i + 1
                    rows.AddOrUpdate (struct (i % 10, momentsOf (1000 * i)))
                    let m = maintained.Value.Value

                    if maintained.Refolds () = 1 then
                        Expect.equal m scratch.Value "the from-scratch moments")
        }

        test "a NaN row poisons the sum until it is removed" {
            for policy in [ FloatPolicy.standard; FloatPolicy.fast ] do
                withGraph (fun () ->
                    let values = [| 0.1; 0.2; 0.3; 0.4 |]
                    let rows = rowsOf values
                    let sum = rows.Rows |> Aggregate.sumBy policy valueOf
                    sum.Value.Value |> ignore
                    rows.AddOrUpdate (struct (4, nan))
                    Expect.isTrue (System.Double.IsNaN sum.Value.Value) (sprintf "NaN with Compensated = %b" policy.Compensated)
                    rows.Remove 4 |> ignore
                    Expect.equal sum.Value.Value (plainFold policy.Compensated values) (sprintf "removed with Compensated = %b" policy.Compensated))
        }

        test "a NaN row poisons the moments until it is removed" {
            for policy in [ FloatPolicy.standard; FloatPolicy.fast ] do
                withGraph (fun () ->
                    let rows = rowsOf (Array.init 4 momentsOf)
                    let maintained = rows.Rows |> Aggregate.momentsBy policy valueOf
                    let scratch = rows.Rows |> Aggregate.momentsFromScratchBy policy valueOf
                    maintained.Value.Value |> ignore
                    rows.AddOrUpdate (struct (4, Moments.add policy.Compensated (momentsOf 4) nan))
                    let label = sprintf "Compensated = %b" policy.Compensated
                    Expect.isTrue (System.Double.IsNaN (Moments.mean maintained.Value.Value)) ("NaN mean with " + label)
                    rows.Remove 4 |> ignore
                    Expect.equal maintained.Value.Value scratch.Value ("removed with " + label))
        }

        test "removing the last row returns zero" {
            withGraph (fun () ->
                let sums = rowsOf [| 2.5 |]
                let sum = sums.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                Expect.equal sum.Value.Value 2.5 "one row"
                sums.Remove 0 |> ignore
                Expect.equal sum.Value.Value 0.0 "sum"
                let moments = rowsOf [| momentsOf 1 |]
                let m = moments.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                Expect.equal (Moments.count m.Value.Value) 50.0 "one row of moments"
                moments.Remove 0 |> ignore
                Expect.equal m.Value.Value Moments.zero "moments")
        }

    ]
    @ moreThreadTests)
