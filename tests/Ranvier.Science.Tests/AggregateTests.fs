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

let private momentsOf (compensated: bool) (seed: int) =
    Array.init 50 (fun j -> 100.0 + float ((seed * 31 + j * 17) % 1000) / 7.0)
    |> Array.fold (Moments.add compensated) Moments.zero

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

        test "with no floor, re-folds after as many removals as rows" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 float)
                let sum = rows.Rows |> Aggregate.sumBy (FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0) valueOf
                sum.Value.Value |> ignore

                for i in 1..9 do
                    rows.AddOrUpdate (struct (i % 10, float (100 + i)))
                    sum.Value.Value |> ignore

                Expect.equal (sum.Refolds ()) 0 "after 9 edits"
                rows.AddOrUpdate (struct (0, 110.0))
                sum.Value.Value |> ignore
                Expect.equal (sum.Refolds ()) 1 "after the 10th edit")
        }

        test "standard carries a floor of 64" {
            Expect.equal FloatPolicy.standard.RefoldFloor 64 "RefoldFloor"
        }

        test "standard waits for 64 removals on a small aggregate" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 float)
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                sum.Value.Value |> ignore

                for i in 1..63 do
                    rows.AddOrUpdate (struct (i % 10, float (100 + i)))
                    sum.Value.Value |> ignore

                Expect.equal (sum.Refolds ()) 0 "after 63 edits"
                rows.AddOrUpdate (struct (64 % 10, float (100 + 64)))
                sum.Value.Value |> ignore
                Expect.equal (sum.Refolds ()) 1 "after the 64th edit")
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

        test "between re-folds under the standard floor a small sum stays within 2 ulp of the from-scratch fold" {
            withGraph (fun () ->
#if FABLE_COMPILER
                let edits = 100_000
#else
                let edits = 1_000_000
#endif
                let valueAt (i: int) = 1.0 + (float i * 3.14159) % 999_999.0
                let values = Array.init 8 valueAt
                let rows = rowsOf values
                let sum = rows.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                sum.Value.Value |> ignore
                let mutable largest = 0.0

                for e in 1..edits do
                    let k = e % 8
                    values[k] <- valueAt (8 + e)
                    rows.AddOrUpdate (struct (k, values[k]))
                    let distance = ulps sum.Value.Value (plainFold true values)
                    largest <- max largest distance

                    if not (distance <= 2.0) then
                        failtestf "edit %d: %g ulp from the from-scratch fold" e distance

                printfn
                    "Re-fold floor drift: largest distance %g ulp over %d edits of 8 rows, %d re-folds"
                    largest
                    edits
                    (sum.Refolds ())

                Expect.equal (sum.Refolds ()) (edits / 64) "one re-fold per 64 removals")
        }

        test "maintained moments follow from-scratch moments" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 100 (momentsOf true))
                let maintained = rows.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                let scratch = rows.Rows |> Aggregate.momentsFromScratchBy FloatPolicy.standard valueOf
                maintained.Value.Value |> ignore
                // Sampled at the edit before each re-fold and at every 97th edit.
                let mutable meanDistance = 0.0
                let mutable varianceDistance = 0.0

                for e in 1..10_000 do
                    rows.AddOrUpdate (struct (e % 100, momentsOf true (100 + e)))
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
                Expect.isLessThanOrEqual varianceDistance 1e-9 "variance"
                Expect.equal (maintained.Refolds ()) (10_000 / 100) "one re-fold per 100 removals from 100 rows, and no other")
        }

        test "a momentsBy row republished with an equal Moments" {
            withGraph (fun () ->
                let instances = Array.init 4 (momentsOf true)
                let rows = rowsOf instances
                let maintained = rows.Rows |> Aggregate.momentsBy (FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0) valueOf
                let first = maintained.Value.Value

                for e in 1..8 do
                    rows.AddOrUpdate (struct (e % 4, instances[e % 4]))
                    maintained.Value.Value |> ignore

                Expect.equal (maintained.Refolds ()) 0 "a republished instance is no edit"

                for e in 1..8 do
                    rows.AddOrUpdate (struct (e % 4, momentsOf true (e % 4)))
                    maintained.Value.Value |> ignore

#if FABLE_COMPILER
                Expect.equal
                    (maintained.Refolds ())
                    2
                    "under Fable an equal rebuilt Moments compares by reference, so each of the 8 republishes of 4 rows is an edit"
#else
                Expect.equal (maintained.Refolds ()) 0 "on .NET an equal rebuilt Moments compares by value, so no republish is an edit"
                Expect.equal maintained.Value.Value first "the state keeps its bits"
#endif
                Expect.equal (Moments.mean maintained.Value.Value) (Moments.mean first) "the mean")
        }

        test "an infinite row reads NaN under standard and the infinity under fast until it is removed" {
            for policy in [ FloatPolicy.standard; FloatPolicy.fast ] do
                for bad in [ infinity; -infinity ] do
                    withGraph (fun () ->
                        let values = [| 0.1; 0.2; 0.3; 0.4 |]
                        let rows = rowsOf values
                        let sum = rows.Rows |> Aggregate.sumBy policy valueOf
                        sum.Value.Value |> ignore
                        let label = sprintf "row %g with Compensated = %b" bad policy.Compensated
                        rows.AddOrUpdate (struct (4, bad))

                        if policy.Compensated then
                            Expect.isTrue (System.Double.IsNaN sum.Value.Value) ("NaN: " + label)
                        else
                            Expect.equal sum.Value.Value bad ("the infinity: " + label)

                        let before = sum.Refolds ()
                        rows.AddOrUpdate (struct (0, 0.15))
                        sum.Value.Value |> ignore
                        rows.AddOrUpdate (struct (0, 0.1))
                        sum.Value.Value |> ignore
                        Expect.equal (sum.Refolds () - before) 2 ("one re-fold per edit while the row is present: " + label)
                        rows.Remove 4 |> ignore
                        Expect.equal sum.Value.Value (plainFold policy.Compensated values) ("removed: " + label))
        }

        test "two rows of 1e308 read NaN under standard and +infinity under fast until one is removed" {
            for policy in [ FloatPolicy.standard; FloatPolicy.fast ] do
                withGraph (fun () ->
                    let rows = rowsOf [| 1e308; 1e308 |]
                    let sum = rows.Rows |> Aggregate.sumBy policy valueOf
                    let label = sprintf "Compensated = %b" policy.Compensated

                    if policy.Compensated then
                        Expect.isTrue (System.Double.IsNaN sum.Value.Value) ("NaN with " + label)
                    else
                        Expect.equal sum.Value.Value infinity ("+infinity with " + label)

                    rows.Remove 1 |> ignore
                    Expect.equal sum.Value.Value (plainFold policy.Compensated [| 1e308 |]) ("removed with " + label))
        }

        test "a moments row ending in +infinity reads mean NaN under standard and mean +infinity under fast until it is removed" {
            for policy in [ FloatPolicy.standard; FloatPolicy.fast ] do
                withGraph (fun () ->
                    let c = policy.Compensated
                    let rows = rowsOf (Array.init 4 (momentsOf c))
                    let maintained = rows.Rows |> Aggregate.momentsBy policy valueOf
                    let scratch = rows.Rows |> Aggregate.momentsFromScratchBy policy valueOf
                    maintained.Value.Value |> ignore
                    rows.AddOrUpdate (struct (4, Moments.add c (momentsOf c 4) infinity))
                    let label = sprintf "Compensated = %b" c
                    let m = maintained.Value.Value

                    if c then
                        Expect.isTrue (System.Double.IsNaN (Moments.mean m)) ("NaN mean with " + label)
                    else
                        Expect.equal (Moments.mean m) infinity ("+infinity mean with " + label)

                    Expect.isTrue (System.Double.IsNaN (Moments.variance m)) ("NaN variance with " + label)
                    rows.Remove 4 |> ignore
                    Expect.equal maintained.Value.Value scratch.Value ("removed with " + label))
        }

        test "after a re-fold the maintained moments have the from-scratch bits" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 10 (momentsOf true))
                let maintained = rows.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                let scratch = rows.Rows |> Aggregate.momentsFromScratchBy FloatPolicy.standard valueOf
                maintained.Value.Value |> ignore
                let mutable i = 0

                while maintained.Refolds () = 0 do
                    i <- i + 1
                    rows.AddOrUpdate (struct (i % 10, momentsOf true (1000 * i)))
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
                    let rows = rowsOf (Array.init 4 (momentsOf policy.Compensated))
                    let maintained = rows.Rows |> Aggregate.momentsBy policy valueOf
                    let scratch = rows.Rows |> Aggregate.momentsFromScratchBy policy valueOf
                    maintained.Value.Value |> ignore
                    rows.AddOrUpdate (struct (4, Moments.add policy.Compensated (momentsOf policy.Compensated 4) nan))
                    let label = sprintf "Compensated = %b" policy.Compensated
                    Expect.isTrue (System.Double.IsNaN (Moments.mean maintained.Value.Value)) ("NaN mean with " + label)
                    rows.Remove 4 |> ignore
                    Expect.equal maintained.Value.Value scratch.Value ("removed with " + label))
        }

        test "a floor delays the re-fold of a small aggregate" {
            let refoldsUnder (policy: FloatPolicy) =
                let mutable result = -1

                withGraph (fun () ->
                    let rows = rowsOf [| 1.0; 2.0; 3.0; 4.0 |]
                    let sum = rows.Rows |> Aggregate.sumBy policy valueOf
                    sum.Value.Value |> ignore

                    for e in 1..256 do
                        rows.AddOrUpdate (struct (e % 4, 10.0 + float e))
                        sum.Value.Value |> ignore

                    result <- sum.Refolds ())

                result

            Expect.equal (refoldsUnder (FloatPolicy.standard |> FloatPolicy.withRefoldFloor 64)) 4 "floor 64"
            Expect.equal (refoldsUnder (FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0)) 64 "no floor"
            Expect.equal (refoldsUnder FloatPolicy.standard) 4 "standard"
        }

        test "a floored aggregate equals the from-scratch one after each re-fold" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 64
                let rows = rowsOf (Array.init 4 (momentsOf true))
                let maintained = rows.Rows |> Aggregate.momentsBy policy valueOf
                let scratch = rows.Rows |> Aggregate.momentsFromScratchBy policy valueOf
                maintained.Value.Value |> ignore
                let mutable refolds = maintained.Refolds ()
                let mutable refoldReads = 0

                for e in 1..256 do
                    rows.AddOrUpdate (struct (e % 4, momentsOf true (100 + e)))
                    let m = maintained.Value.Value
                    let s = scratch.Value

                    if maintained.Refolds () > refolds then
                        refolds <- maintained.Refolds ()
                        refoldReads <- refoldReads + 1
                        Expect.equal m s (sprintf "bits after the re-fold at edit %d" e)
                    else
                        Expect.isLessThanOrEqual (relative (Moments.mean m) (Moments.mean s)) 1e-12 (sprintf "mean at edit %d" e)
                        Expect.isLessThanOrEqual (relative (Moments.variance m) (Moments.variance s)) 1e-9 (sprintf "variance at edit %d" e)

                Expect.equal refoldReads 4 "four re-folds")
        }

        test "a negative floor is rejected" {
            Expect.throwsT<System.ArgumentOutOfRangeException>
                (fun () -> FloatPolicy.standard |> FloatPolicy.withRefoldFloor -1 |> ignore)
                "floor -1"
        }

        test "removing the last row returns zero" {
            withGraph (fun () ->
                let sums = rowsOf [| 2.5 |]
                let sum = sums.Rows |> Aggregate.sumBy FloatPolicy.standard valueOf
                Expect.equal sum.Value.Value 2.5 "one row"
                sums.Remove 0 |> ignore
                Expect.equal sum.Value.Value 0.0 "sum"
                let moments = rowsOf [| momentsOf true 1 |]
                let m = moments.Rows |> Aggregate.momentsBy FloatPolicy.standard valueOf
                Expect.equal (Moments.count m.Value.Value) 50.0 "one row of moments"
                moments.Remove 0 |> ignore
                Expect.equal m.Value.Value Moments.zero "moments")
        }

        test "maintainedBy keeps a custom accumulator" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0
                let rows = rowsOf (Array.init 100 float)

                let pair =
                    rows.Rows
                    |> Aggregate.maintainedBy
                        policy
                        (fun _ -> true)
                        (fun (n: int, s: float) u -> (n + 1, s + u))
                        (fun (n: int, s: float) u -> (n - 1, s - u))
                        (0, 0.0)
                        id
                        valueOf

                let mutable current = Map.ofList [ for k in 0..99 -> k, float k ]
                pair.Value.Value |> ignore

                for e in 0..49 do
                    current <- Map.add e (float (1000 + e)) current
                    rows.AddOrUpdate (struct (e, float (1000 + e)))
                    pair.Value.Value |> ignore

                for k in 90..99 do
                    current <- Map.remove k current
                    rows.Remove k |> ignore
                    pair.Value.Value |> ignore

                let expected = (current.Count, current |> Map.fold (fun s _ v -> s + v) 0.0)
                Expect.equal pair.Value.Value expected "the from-scratch (count, sum)"

                for e in 0..99 do
                    rows.AddOrUpdate (struct (e % 90, float (5000 + e)))
                    pair.Value.Value |> ignore

                Expect.isTrue (pair.Refolds () >= 1) "re-folds after 100 edits under a floor of 0")
        }

        test "comomentsBy follows row edits" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0

                let pointOf (i: int) =
                    (float ((i * 37 + 11) % 101) / 7.0, float ((i * 53 + 5) % 89) / 3.0 + float i / 10.0)

                let rows = rowsOf (Array.init 40 pointOf)
                let project (x: float, y: float) = Comoments.add true Comoments.zero x y
                let maintained = rows.Rows |> Aggregate.comomentsBy policy (valueOf >> project)
                let mutable current = Map.ofList [ for k in 0..39 -> k, pointOf k ]
                maintained.Value.Value |> ignore

                let check (message: string) =
                    let expected =
                        current |> Map.fold (fun s _ (x, y) -> Comoments.add true s x y) Comoments.zero

                    expectClose 1e-12 0.0 (Comoments.covariance expected) (Comoments.covariance maintained.Value.Value) (message + " covariance")
                    expectClose 1e-12 0.0 (Comoments.slope expected) (Comoments.slope maintained.Value.Value) (message + " slope")

                check "initially"

                for e in 0..29 do
                    let p = pointOf (e + 200)
                    current <- Map.add (e % 40) p current
                    rows.AddOrUpdate (struct (e % 40, p))
                    check (sprintf "edit %d" e)

                for k in 30..39 do
                    current <- Map.remove k current
                    rows.Remove k |> ignore
                    check (sprintf "removal %d" k))
        }

        test "comomentsBy recovers from a NaN row once it is removed" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0
                let pointOf (i: int) = (float ((i * 37 + 11) % 101) / 7.0, float ((i * 53 + 5) % 89) / 3.0)
                let rows = rowsOf (Array.init 10 pointOf)
                let project (x: float, y: float) = Comoments.add true Comoments.zero x y
                let maintained = rows.Rows |> Aggregate.comomentsBy policy (valueOf >> project)
                Expect.isFalse (System.Double.IsNaN (Comoments.covariance maintained.Value.Value)) "clean at first"
                rows.AddOrUpdate (struct (3, (nan, 2.0)))
                Expect.isTrue (System.Double.IsNaN (Comoments.covariance maintained.Value.Value)) "poisoned while the NaN row is present"
                rows.AddOrUpdate (struct (4, pointOf 44))
                Expect.isTrue (System.Double.IsNaN (Comoments.covariance maintained.Value.Value)) "still poisoned after another edit"
                rows.Remove 3 |> ignore

                let expected =
                    [ for k in 0..9 do
                          if k <> 3 then
                              if k = 4 then pointOf 44 else pointOf k ]
                    |> List.fold (fun s (x, y) -> Comoments.add true s x y) Comoments.zero

                expectClose 1e-12 0.0 (Comoments.covariance expected) (Comoments.covariance maintained.Value.Value) "covariance"
                expectClose 1e-12 0.0 (Comoments.slope expected) (Comoments.slope maintained.Value.Value) "slope"
                Expect.isTrue (maintained.Refolds () >= 1) "the removal re-folded")
        }

        test "higherMomentsBy and weightedMomentsBy follow row edits" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0
                let xOf (i: int) = float ((i * 37 + 11) % 101) / 7.0
                let wOf (i: int) = 0.5 + float ((i * 29 + 3) % 17) / 4.0
                let rows = rowsOf (Array.init 40 (fun i -> (xOf i, wOf i)))

                let higher =
                    rows.Rows
                    |> Aggregate.higherMomentsBy policy (fun row -> let x, _ = valueOf row in HigherMoments.add true HigherMoments.zero x)

                let weighted =
                    rows.Rows
                    |> Aggregate.weightedMomentsBy policy (fun row -> let x, w = valueOf row in WeightedMoments.add true WeightedMoments.zero x w)

                let mutable current = Map.ofList [ for k in 0..39 -> k, (xOf k, wOf k) ]
                higher.Value.Value |> ignore
                weighted.Value.Value |> ignore

                let check (message: string) =
                    let h = current |> Map.fold (fun s _ (x, _) -> HigherMoments.add true s x) HigherMoments.zero
                    let w = current |> Map.fold (fun s _ (x, wt) -> WeightedMoments.add true s x wt) WeightedMoments.zero
                    Expect.isLessThanOrEqual (abs (HigherMoments.skewness higher.Value.Value - HigherMoments.skewness h)) 1e-10 (message + " skewness")
                    expectClose 1e-12 0.0 (WeightedMoments.mean w) (WeightedMoments.mean weighted.Value.Value) (message + " weighted mean")
                    expectClose 1e-12 0.0 (WeightedMoments.variance w) (WeightedMoments.variance weighted.Value.Value) (message + " weighted variance")

                check "initially"

                for e in 0..29 do
                    let v = (xOf (e + 300), wOf (e + 300))
                    current <- Map.add (e % 40) v current
                    rows.AddOrUpdate (struct (e % 40, v))
                    check (sprintf "edit %d" e)

                for k in 30..39 do
                    current <- Map.remove k current
                    rows.Remove k |> ignore
                    check (sprintf "removal %d" k))
        }

        test "weightedMomentsBy recovers from a NaN weight once its row is removed" {
            withGraph (fun () ->
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0
                let xOf (i: int) = float ((i * 37 + 11) % 101) / 7.0
                let wOf (i: int) = 0.5 + float ((i * 29 + 3) % 17) / 4.0
                let rows = rowsOf (Array.init 10 (fun i -> (xOf i, wOf i)))

                let weighted =
                    rows.Rows
                    |> Aggregate.weightedMomentsBy policy (fun row -> let x, w = valueOf row in WeightedMoments.add true WeightedMoments.zero x w)

                Expect.isFalse (System.Double.IsNaN (WeightedMoments.mean weighted.Value.Value)) "clean at first"
                rows.AddOrUpdate (struct (3, (1.0, nan)))
                Expect.isTrue (System.Double.IsNaN (WeightedMoments.mean weighted.Value.Value)) "poisoned while the NaN row is present"
                rows.AddOrUpdate (struct (4, (2.5, 1.5)))
                Expect.isTrue (System.Double.IsNaN (WeightedMoments.mean weighted.Value.Value)) "still poisoned after another edit"
                rows.Remove 3 |> ignore

                let expected =
                    [ for k in 0..9 do
                          if k <> 3 then
                              if k = 4 then (2.5, 1.5) else (xOf k, wOf k) ]
                    |> List.fold (fun s (x, w) -> WeightedMoments.add true s x w) WeightedMoments.zero

                expectClose 1e-12 0.0 (WeightedMoments.mean expected) (WeightedMoments.mean weighted.Value.Value) "weighted mean"
                expectClose 1e-12 0.0 (WeightedMoments.variance expected) (WeightedMoments.variance weighted.Value.Value) "weighted variance"
                Expect.isTrue (weighted.Refolds () >= 1) "the removal re-folded")
        }

        test "histogramBy counts exactly" {
            withGraph (fun () ->
                let edges = [| 0.0; 1.0; 2.0; 3.0 |]
                let xOf (i: int) = float ((i * 37 + 11) % 53) / 10.0 - 1.0
                let rows = rowsOf (Array.init 20 xOf)
                let histogram = rows.Rows |> Aggregate.histogramBy edges valueOf
                let mutable current = Map.ofList [ for k in 0..19 -> k, xOf k ]
                histogram.Value |> ignore

                let check (message: string) =
                    let expected = current |> Map.fold (fun h _ x -> Histogram.add h x) (Histogram.create edges)
                    Expect.equal histogram.Value expected message

                check "initially"

                for e in 0..14 do
                    let x = if e = 5 then nan else xOf (e + 400)
                    current <- Map.add (e % 20) x current
                    rows.AddOrUpdate (struct (e % 20, x))
                    check (sprintf "edit %d" e)

                for k in 15..19 do
                    current <- Map.remove k current
                    rows.Remove k |> ignore
                    check (sprintf "removal %d" k))
        }

        test "quantileBy is the type-7 quantile of the current rows" {
            withGraph (fun () ->
                let xOf (i: int) = float ((i * 37 + 11) % 1009) / 7.0
                let rows = rowsOf (Array.init 101 xOf)
                let ps = [ 0.25; 0.5; 0.9 ]
                let quantiles = ps |> List.map (fun p -> rows.Rows |> Aggregate.quantileBy p valueOf)
                let mutable current = Map.ofList [ for k in 0..100 -> k, xOf k ]

                let check (message: string) =
                    let sorted = current |> Map.toArray |> Array.map snd |> Array.sort

                    for p, q in List.zip ps quantiles do
                        Expect.equal q.Value (OrderStatistic.quantileSorted p sorted) (sprintf "%s, p = %g" message p)

                check "initially"

                for e in 0..29 do
                    let x = xOf (e + 500)
                    current <- Map.add (e * 3 % 101) x current
                    rows.AddOrUpdate (struct (e * 3 % 101, x))
                    check (sprintf "edit %d" e)

                for e in 0..9 do
                    let x = xOf (e + 700)
                    current <- Map.add (101 + e) x current
                    rows.AddOrUpdate (struct (101 + e, x))
                    check (sprintf "insertion %d" e)

                for e in 0..9 do
                    current <- Map.remove (e * 7) current
                    rows.Remove (e * 7) |> ignore
                    check (sprintf "removal %d" e))
        }

        test "quantileBy at the edges" {
            withGraph (fun () ->
                let empty = (rowsOf ([||]: float[])).Rows |> Aggregate.quantileBy 0.5 valueOf
                Expect.isTrue (System.Double.IsNaN empty.Value) "no rows"
                let withNan = (rowsOf [| 1.0; nan; 2.0 |]).Rows |> Aggregate.quantileBy 0.5 valueOf
                Expect.isTrue (System.Double.IsNaN withNan.Value) "a NaN row"
                let one = (rowsOf [| 4.0 |]).Rows
                Expect.equal (Aggregate.quantileBy 1.0 valueOf one).Value 4.0 "p = 1 over one row"
                let two = (rowsOf [| 4.0; 2.0 |]).Rows
                Expect.equal (Aggregate.quantileBy 1.0 valueOf two).Value 4.0 "p = 1 reads only the last row"
                Expect.equal (Aggregate.quantileBy 0.0 valueOf two).Value 2.0 "p = 0"
                Expect.throwsT<System.ArgumentOutOfRangeException> (fun () -> Aggregate.quantileBy -0.1 valueOf one |> ignore) "p = -0.1"
                Expect.throwsT<System.ArgumentOutOfRangeException> (fun () -> Aggregate.quantileBy 1.1 valueOf one |> ignore) "p = 1.1")
        }

        test "quantileBy cuts off an unchanged value" {
            withGraph (fun () ->
                let rows = rowsOf (Array.init 11 float)
                let median = rows.Rows |> Aggregate.quantileBy 0.5 valueOf
                let runs = ref 0

                createEffect (fun () ->
                    median.Value |> ignore
                    runs.Value <- runs.Value + 1)

                Expect.equal runs.Value 1 "the first run"
                rows.AddOrUpdate (struct (3, 100.0))
                Expect.equal runs.Value 2 "an edit that moves the median"
                rows.AddOrUpdate (struct (3, 200.0))
                Expect.equal runs.Value 2 "an edit of the largest row to a larger value")
        }

        test "a projection from createProjectionOnce is refused" {
            withGraph (fun () ->
                let keys = createKeyedCollection id
                keys.AddOrUpdate 1
                keys.AddOrUpdate 2

                let once =
                    createProjectionOnce ProjectionOptions.None id (fun item -> float (item ())) (fun () -> keys.Rows.Keys :> int seq)

                Expect.throwsT<System.NotSupportedException> (fun () -> Aggregate.quantileBy 0.5 id once |> ignore) "quantileBy"

                Expect.throwsT<System.NotSupportedException>
                    (fun () -> Aggregate.momentsBy FloatPolicy.fast (fun x -> Moments.add true Moments.zero x) once |> ignore)
                    "momentsBy")
        }

        test "quantileBy raises the exception its projection raises" {
            withGraph (fun () ->
                let rows = rowsOf [| 1.0; 2.0; 3.0; 4.0; 50.0 |]
                let boom (row: struct (int * float)) = if valueOf row = 50.0 then failwith "boom" else valueOf row

                for p in [ 0.5; 1.0 ] do
                    let q = rows.Rows |> Aggregate.quantileBy p boom

                    let message =
                        try
                            q.Value |> ignore
                            "no exception"
                        with e ->
                            e.Message

                    Expect.equal message "boom" (sprintf "the message at p = %g" p))
        }

        test "removing a dominant row leaves an error within the stated bound" {
            withGraph (fun () ->
                let rows = rowsOf [| 1e16; 1.0; 2.0; 3.0 |]
                let project row = Moments.add true Moments.zero (valueOf row)
                let m = rows.Rows |> Aggregate.momentsBy FloatPolicy.standard project
                m.Value.Value |> ignore
                rows.Remove 0 |> ignore
                // 2^-52, Double machine epsilon
                let epsilon = 2.220446049250313e-16
                let bound = 4.0 * epsilon * 1e16
                // 1.75: the value both targets produce, quoted in the momentsBy doc; the rows' mean is 2.
                Expect.equal (Moments.mean m.Value.Value) 1.75 "the mean straight after the removal"
                Expect.isLessThanOrEqual (abs (Moments.mean m.Value.Value - 2.0)) bound "the mean is within n * epsilon * the largest magnitude")
        }

        test "removing a dominant row from a sum leaves an error within n · ε times the largest magnitude" {
            // The bound of maintainedBy with the constant 1: four rows held, the largest 1e16, removed again before
            // any re-fold. Without compensation 1e16 + 1 + 2 + 3 rounds at the spacing 2 of the doubles near 1e16.
            withGraph (fun () ->
                let epsilon = 2.220446049250313e-16

                for policy in [ FloatPolicy.fast; FloatPolicy.standard ] do
                    let rows = rowsOf [| 1e16; 1.0; 2.0; 3.0 |]
                    let s = rows.Rows |> Aggregate.sumBy policy valueOf
                    s.Value.Value |> ignore
                    rows.Remove 0 |> ignore
                    let label = sprintf "compensated %b" policy.Compensated
                    Expect.equal (s.Refolds ()) 0 (label + ": no re-fold yet")
                    Expect.isLessThanOrEqual (abs (s.Value.Value - 6.0)) (4.0 * epsilon * 1e16) (label + ": within the bound"))
        }

        test "removing a dominant row matches the fold once the floor-0 re-fold has run" {
            withGraph (fun () ->
                let rows = rowsOf [| 1e16; 1.0; 2.0; 3.0 |]
                let policy = FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0
                let project row = Moments.add true Moments.zero (valueOf row)
                let m = rows.Rows |> Aggregate.momentsBy policy project
                m.Value.Value |> ignore
                rows.Remove 0 |> ignore
                m.Value.Value |> ignore
                rows.AddOrUpdate (struct (1, 4.0))
                m.Value.Value |> ignore
                rows.AddOrUpdate (struct (2, 5.0))
                let expected = [ 4.0; 5.0; 3.0 ] |> List.fold (Moments.add true) Moments.zero
                expectClose 1e-12 0.0 (Moments.mean expected) (Moments.mean m.Value.Value) "mean"
                expectClose 1e-12 0.0 (Moments.variance expected) (Moments.variance m.Value.Value) "variance"
                Expect.isTrue (m.Refolds () >= 1) "re-folded")
        }

        test "a removal that leaves a finite state non-finite re-folds under the fast policy" {
            withGraph (fun () ->
                let rows = rowsOf [| (1.0, 1e20); (2.0, 1.0); (4.0, 1.0) |]
                let project row = let x, w = valueOf row in WeightedMoments.add false WeightedMoments.zero x w
                let m = rows.Rows |> Aggregate.weightedMomentsBy FloatPolicy.fast project
                m.Value.Value |> ignore
                rows.Remove 0 |> ignore
                rows.AddOrUpdate (struct (3, (6.0, 1.0)))
                let expected = [ (2.0, 1.0); (4.0, 1.0); (6.0, 1.0) ] |> List.fold (fun s (x, w) -> WeightedMoments.add false s x w) WeightedMoments.zero
                expectClose 1e-12 0.0 (WeightedMoments.mean expected) (WeightedMoments.mean m.Value.Value) "mean after the insert")
        }

        test "removing a dominant weight leaves the weighted mean wrong" {
            withGraph (fun () ->
                let rows = rowsOf [| (1.0, 1e20); (1.0, 1.0); (2.0, 1.0); (3.0, 1.0) |]
                let project row = let x, w = valueOf row in WeightedMoments.add true WeightedMoments.zero x w
                let m = rows.Rows |> Aggregate.weightedMomentsBy FloatPolicy.standard project
                m.Value.Value |> ignore
                rows.Remove 0 |> ignore
                let scratch =
                    rows.Rows
                    |> Projection.fold (fun s row -> let x, w = valueOf row in WeightedMoments.add true s x w) WeightedMoments.zero

                expectClose 1e-12 0.0 2.0 (WeightedMoments.mean scratch.Value) "the from-scratch fold of the remaining rows"
                // 1: the value both targets produce, quoted in the weightedMomentsBy doc; the rows' mean is 2.
                Expect.equal (WeightedMoments.mean m.Value.Value) 1.0 "the maintained mean straight after the removal")
        }

    ]
    @ moreThreadTests)
