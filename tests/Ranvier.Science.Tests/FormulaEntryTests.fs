module Ranvier.Science.Tests.FormulaEntryTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities

let private parsed (text: string) : Expr =
    match Formula.parse text with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected a tree for \"%s\", got: %s" text message

let private relative (actual: float) (expected: float) = abs (actual - expected) / abs expected

let private declared (result: Result<Entry, string>) : Entry =
    match result with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected an entry, got: %s" message

let private errorOf (result: Result<'a, string>) : string =
    match result with
    | Result.Error message -> message
    | Result.Ok _ -> failtest "expected an error"

/// The page's names of `FormulaTests.fs`, with `mean` = 7.3 ± 0.2 mK and `target` = 2.1 ± 0.1 mK.
let private page () : Model * Input<mK> * Input<mK> =
    let model = Model.create "page"
    let mean = Quantity.inputWithUncertainty model "mean" "Mean" Units.millikelvin 7.3<mK> 0.2<mK>
    Quantity.input model "se" "Standard error" Units.millikelvin 0.37<mK> |> ignore
    let target = Quantity.inputWithUncertainty model "target" "Target" Units.millikelvin 2.1<mK> 0.1<mK>
    Quantity.input model "variance" "Variance" (UnitOf.squared Units.millikelvin) 0.5<mK^2> |> ignore
    Quantity.input model "n" "Count" Units.one 12.0 |> ignore
    Quantity.input model "alpha" "Alpha" Units.one 0.05 |> ignore
    Quantity.input model "rate" "Rate" (Units.one / Units.second) 4.0</s> |> ignore
    Quantity.input model "temp_k" "Temperature" Units.kelvin 300.0<K> |> ignore
    model, mean, target

/// The formula entry `id` of `text`, and the count of runs of an effect that reads its value.
let private withReader (model: Model) (id: string) (text: string) : Entry * int ref =
    let e = declared (Quantity.formula model id id text)
    let runs = ref 0

    createEffect (fun () ->
        e.Read () |> ignore
        runs.Value <- runs.Value + 1)

    e, runs

[<Tests>]
let tests =
    testList
        "FormulaEntry"
        [
            test "measuredEvaluator gives the evaluator's value" {
                withGraph (fun () ->
                    let model, _, _ = page ()

                    for text in
                        [
                            "(mean - target) / se"
                            "mean * 2 + target"
                            "sqrt(variance)"
                            "exp(mean / se)"
                            "min(mean, target)"
                            "max(mean, target)"
                            "mean^3"
                            "mean^-2"
                            "-mean"
                            "abs(mean - target)"
                            "ln(n)"
                            "log10(n)"
                        ] do
                        let expr = parsed text

                        Expect.equal
                            (Uncertain.value (Formula.measuredEvaluator model expr ()))
                            (Formula.evaluator model expr ())
                            (text + ": the evaluator's bits"))
            }

            test "measuredEvaluator's min and max keep the evaluator's signed zero" {
                withGraph (fun () ->
                    let model = Model.create "p"
                    Quantity.input model "z" "Z" Units.one 0.0 |> ignore

                    // Math.Min gives −0 and Math.Max +0 for operands +0 and −0 in either order, as IEEE 754-2019's
                    // minimum and maximum do (general knowledge); 1/x is −∞ at −0 and +∞ at +0, so the reciprocals
                    // compare the signs.
                    for text in [ "min(z, -z)"; "min(-z, z)"; "max(z, -z)"; "max(-z, z)" ] do
                        let expr = parsed text

                        Expect.equal
                            (1.0 / Uncertain.value (Formula.measuredEvaluator model expr ()))
                            (1.0 / Formula.evaluator model expr ())
                            (text + ": the sign of the evaluator's zero"))
            }

            test "measuredEvaluator propagates" {
                withGraph (fun () ->
                    let model = Model.create "p"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 10.0 3.0 |> ignore
                    Quantity.inputWithUncertainty model "b" "B" Units.one 20.0 4.0 |> ignore
                    Quantity.inputWithUncertainty model "z" "Z" Units.one 0.0 1.0 |> ignore
                    let u (text: string) = Uncertain.standardUncertainty (Formula.measuredEvaluator model (parsed text) ())
                    // Two independent sources: √(3² + 4²) = 5 (closed form).
                    Expect.equal (u "a + b") 5.0 "a + b"
                    // One source with contributions 3 and −3.
                    Expect.equal (u "a - a") 0.0 "a - a"
                    // d(2a)/da = 2: 2 · 3.
                    Expect.equal (u "2 * a") 6.0 "2 * a"
                    // d(a²)/da = 2a: 2 · 10 · 3.
                    let squared = u "a^2"
                    Expect.isTrue (relative squared 60.0 <= 1e-15) (sprintf "a^2: %.17g within relative 1e-15 of 60" squared)
                    // d|x|/dx = −1 at x = −10, and −a holds a's source with contribution −3: |−1 · −3|.
                    Expect.equal (u "abs(-a)") 3.0 "abs(-a)"
                    // z⁰ ≡ 1, so its derivative is 0 at z = 0 as everywhere (closed form).
                    Expect.equal (u "z^0") 0.0 "z^0 at z = 0")
            }

            test "measuredEvaluator raises for a name the model lacks" {
                withGraph (fun () ->
                    let model, _, _ = page ()

                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.measuredEvaluator model (parsed "mean + nope") |> ignore)
                        "the identifier is missing")
            }

            test "a formula entry" {
                withGraph (fun () ->
                    let model, _, _ = page ()
                    let e = declared (Quantity.formula model "excess" "Excess" "mean - target")
                    Expect.equal e.Formula "mean - target" "Formula"
                    Expect.equal e.Kind QuantityKind.Derived "Kind"
                    Expect.equal e.Id "excess" "Id"
                    Expect.equal e.Name "Excess" "Name"
                    Expect.equal e.Source "" "Source"
                    Expect.isTrue (UnitInfo.sameUnit e.Unit Units.millikelvin.Info) (sprintf "the unit, got %s" (UnitInfo.symbol e.Unit))
                    Expect.equal (e.Read ()) (7.3 - 2.1) "Read"
                    Expect.equal (e.Status ()) QuantityStatus.Ready "Status"
                    Expect.equal (model.TryFind "excess").Value.Id "excess" "the model holds the entry")
            }

            test "a formula entry tracks its reads and cuts off" {
                withGraph (fun () ->
                    let model, mean, target = page ()
                    let excess, runs = withReader model "excess" "mean - target"
                    let lower, lowerRuns = withReader model "lower" "min(mean, target)"
                    Expect.equal runs.Value 1 "the effect's first run"
                    mean.Set 9.9<mK>
                    Expect.equal runs.Value 2 "a write to mean runs the effect once"
                    Expect.equal (excess.Read ()) (9.9 - 2.1) "the value after the write"
                    Expect.equal (lower.Read ()) 2.1 "min(mean, target) after the write"
                    Expect.equal lowerRuns.Value 1 "an unchanged min(mean, target) leaves its reader unrun"
                    target.Set 2.1<mK>
                    Expect.equal runs.Value 2 "a write of target's current value runs nothing")
            }

            test "an uncertainty change leaves a formula entry's value readers unnotified" {
                withGraph (fun () ->
                    let model, mean, _ = page ()
                    let excess, runs = withReader model "excess" "mean - target"
                    // mean − target holds mean's source with contribution u(mean) and target's with −0.1 (closed form).
                    let before = excess.Uncertainty ()
                    let initial = sqrt (0.2 ** 2.0 + 0.1 ** 2.0)
                    Expect.isTrue (relative before initial <= 1e-15) (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" before initial)
                    mean.SetUncertainty 0.4<mK>
                    Expect.equal runs.Value 1 "a new uncertainty of mean leaves the effect unrun"
                    let after = excess.Uncertainty ()
                    let expected = sqrt (0.4 ** 2.0 + 0.1 ** 2.0)
                    Expect.isTrue (relative after expected <= 1e-15) (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" after expected))
            }

            test "a formula can read a formula entry" {
                withGraph (fun () ->
                    let model, mean, _ = page ()
                    declared (Quantity.formula model "excess" "Excess" "mean - target") |> ignore
                    let doubled = declared (Quantity.formula model "doubled" "Doubled" "excess * 2")
                    Expect.equal (doubled.Read ()) ((7.3 - 2.1) * 2.0) "Read"
                    // 2(mean − target) holds mean's source with contribution 2 · 0.2 and target's with −2 · 0.1
                    // (closed form).
                    let uncertainty = doubled.Uncertainty ()
                    let expected = 2.0 * sqrt (0.2 ** 2.0 + 0.1 ** 2.0)

                    Expect.isTrue
                        (relative uncertainty expected <= 1e-15)
                        (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" uncertainty expected)

                    let mutable runs = 0

                    createEffect (fun () ->
                        doubled.Read () |> ignore
                        runs <- runs + 1)

                    mean.SetUncertainty 0.4<mK>
                    Expect.equal runs 1 "a new uncertainty of mean leaves a reader of doubled's value unrun"
                    let after = doubled.Uncertainty ()
                    let expectedAfter = 2.0 * sqrt (0.4 ** 2.0 + 0.1 ** 2.0)

                    Expect.isTrue
                        (relative after expectedAfter <= 1e-15)
                        (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" after expectedAfter))
            }

            test "a formula that does not parse or check declares nothing" {
                withGraph (fun () ->
                    let model, _, _ = page ()
                    let syntax = errorOf (Quantity.formula model "bad" "Bad" "mean +")
                    Expect.stringContains syntax "character" "the parser's message gives the character"
                    errorOf (Quantity.formula model "bad" "Bad" "mean + rate") |> ignore
                    Expect.isTrue (model.TryFind "bad").IsNone "no entry bad"
                    declared (Quantity.formula model "bad" "Bad" "mean") |> ignore)
            }

            test "a formula entry's identifier follows input's rules" {
                withGraph (fun () ->
                    let model, _, _ = page ()
                    declared (Quantity.formula model "excess" "Excess" "mean - target") |> ignore

                    Expect.throwsT<ArgumentException>
                        (fun () -> Quantity.formula model "excess" "Again" "mean" |> ignore)
                        "a second excess"

                    Expect.throwsT<ArgumentException>
                        (fun () -> Quantity.formula model "2x" "Malformed" "mean" |> ignore)
                        "the identifier 2x"

                    Expect.throwsT<ArgumentException>
                        (fun () -> Quantity.formula model "2x" "Malformed" "mean +" |> ignore)
                        "the identifier is checked before the text is parsed")
            }

            test "a pending leaf leaves a formula entry pending" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let feed = ManualStream<float>()
                    let total = createStreamFold 0.0 (+) (fun _ -> feed.Source)
                    Quantity.derive model "total" "Total" Units.metre (fun () -> total.Value * 1.0<m>) |> ignore
                    let twice = declared (Quantity.formula model "twice" "Twice" "total * 2")
                    Expect.equal (twice.Status ()) QuantityStatus.Pending "pending before the first item"
                    feed.Push 2.5
                    Expect.equal (twice.Status ()) QuantityStatus.Ready "ready after an item"
                    Expect.equal (twice.Read ()) 5.0 "twice the fold of the item")
            }

            test "view" {
                withGraph (fun () ->
                    let model, _, _ = page ()
                    let excess = declared (Quantity.formula model "excess" "Excess" "mean - target")

                    match Quantity.view Units.millikelvin excess with
                    | Result.Ok q -> Expect.equal q.Value ((7.3 - 2.1) * 1.0<mK>) "the typed value"
                    | Result.Error message -> failtestf "expected a view in mK, got: %s" message

                    let kelvin = errorOf (Quantity.view Units.kelvin excess)
                    Expect.stringContains kelvin "mK" "the message gives the entry's unit"
                    Expect.stringContains kelvin "not in K" "the message gives the view's unit"
                    errorOf (Quantity.view Units.metre excess) |> ignore
                    let square = declared (Quantity.formula model "square" "Square" "mean^2")

                    match Quantity.view (UnitOf.squared Units.millikelvin) square with
                    | Result.Ok _ -> ()
                    | Result.Error message -> failtestf "expected a view in mK^2, got: %s" message

                    // The scale of mK · mK / mK can differ from mK's in the last place; sameUnit joins the two.
                    let back = declared (Quantity.formula model "back" "Back" "mean * mean / mean")

                    match Quantity.view Units.millikelvin back with
                    | Result.Ok _ -> ()
                    | Result.Error message -> failtestf "expected a view in mK, got: %s" message)
            }
        ]
