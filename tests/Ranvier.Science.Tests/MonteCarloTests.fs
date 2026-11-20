// Reference values: z = 1.9599639845400538, the standard normal quantile at the double 0.975, printed by
//   python -I tests/reference/normal_quantile.py
// The rest are closed forms written in each test.
//
// The tests are statistical with a fixed seed. The generator's uniforms are integer arithmetic and agree bit for bit on
// both targets (Generator.fs); the deviates go through log, sqrt and cos, so they agree within the rounding of those
// functions, and the draws with them. Each tolerance on a statistic is a multiple of its Monte Carlo standard error, the closed form
// written beside it (general knowledge: the standard errors of a normal sample's mean, standard deviation and
// quantile). Exact on each target: Draws, the zero mean and spread of leaves that cancel, equal records for one seed,
// and the NaN fields.
module Ranvier.Science.Tests.MonteCarloTests

open System
open Expecto
open Ranvier
open Ranvier.Quantities

/// The standard normal quantile at 0.975: python -I tests/reference/normal_quantile.py
let private z975 = 1.9599639845400538

let private draws = 20000

let private n = float draws

let private parsed (text: string) : Expr =
    match Formula.parse text with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected a tree for \"%s\", got: %s" text message

let private declared (result: Result<Entry, string>) : Entry =
    match result with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected an entry, got: %s" message

/// The record of `text` over `model`: 20,000 draws, seed 1, coverage 0.95.
let private run (model: Model) (text: string) : MonteCarloResult =
    Formula.monteCarlo draws 1 0.95 model (parsed text) ()

let private within (actual: float) (expected: float) (bound: float) (what: string) : unit =
    Expect.isTrue
        (abs (actual - expected) <= bound)
        (sprintf "%s: %.17g within %.6g of %.17g" what actual bound expected)

/// Expects NaN in every float field and `Draws` = 20,000.
let private expectUndefined (r: MonteCarloResult) : unit =
    Expect.isTrue (Double.IsNaN r.Mean) (sprintf "Mean NaN, was %.17g" r.Mean)
    Expect.isTrue (Double.IsNaN r.StandardDeviation) (sprintf "StandardDeviation NaN, was %.17g" r.StandardDeviation)
    Expect.isTrue (Double.IsNaN r.Lower) (sprintf "Lower NaN, was %.17g" r.Lower)
    Expect.isTrue (Double.IsNaN r.Upper) (sprintf "Upper NaN, was %.17g" r.Upper)
    Expect.equal r.Draws draws "Draws"

[<Tests>]
let tests =
    testList
        "MonteCarlo"
        [
            test "a linear formula meets first-order propagation" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore
                    Quantity.inputWithUncertainty model "b" "B" Units.one 2.0 0.2 |> ignore
                    let r = run model "a + b"
                    // a + b is normal at 3 with σ² = 0.1² + 0.2² = 0.05.
                    let sigma = sqrt 0.05
                    Expect.equal r.Draws draws "Draws"
                    // The mean's standard error is σ/√N.
                    within r.Mean 3.0 (4.0 * sigma / sqrt n) "Mean"
                    // The sample standard deviation's standard error is σ/√(2(N − 1)).
                    within r.StandardDeviation sigma (4.0 * sigma / sqrt (2.0 * (n - 1.0))) "StandardDeviation"
                    // The sample quantile at p has standard error σ·√(p(1 − p)/N)/φ(z_p), with φ the standard normal
                    // density: 0.0189σ at p = 0.975 and N = 20,000, so 0.1σ is 5.3 standard errors.
                    within r.Lower (3.0 - z975 * sigma) (0.1 * sigma) "Lower"
                    within r.Upper (3.0 + z975 * sigma) (0.1 * sigma) "Upper")
            }

            test "leaves that share a source move together" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let x = Quantity.inputWithUncertainty model "x" "X" Units.one 1.0 0.1

                    Quantity.deriveMeasured model "y1" "Y1" Units.one (fun () -> 2.0 * x.Entry.Measured ())
                    |> ignore

                    Quantity.deriveMeasured model "y2" "Y2" Units.one (fun () -> -2.0 * x.Entry.Measured ())
                    |> ignore

                    // Each draw sets y1 to 2 + 0.2z and y2 to −2 − 0.2z for one deviate z of x's source: the sum is
                    // exactly 0, since rounding is symmetric in sign.
                    let shared = run model "y1 + y2"
                    Expect.equal shared.Mean 0.0 "the mean of y1 + y2"
                    Expect.equal shared.StandardDeviation 0.0 "the spread of y1 + y2"
                    Quantity.inputWithUncertainty model "p" "P" Units.one 0.0 0.2 |> ignore
                    Quantity.inputWithUncertainty model "q" "Q" Units.one 0.0 0.2 |> ignore
                    // Independent: σ = 0.2√2 = 0.283, which is 58 standard errors σ/√(2(N − 1)) above 0.2.
                    let independent = run model "p + q"
                    Expect.isGreaterThan independent.StandardDeviation 0.2 "the spread of p + q")
            }

            test "formula entries are inlined" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.5 |> ignore
                    declared (Quantity.formula model "f" "F" "a^2") |> ignore
                    let r = run model "f"
                    // E[(1 + 0.5Z)²] = 1 + 0.25 = 1.25, Var = 4 · 0.25 + 2 · 0.0625 = 1.125; standard error √(1.125/N).
                    within r.Mean 1.25 (4.0 * sqrt 1.125 / sqrt n) "Mean"
                    // f as a leaf would hold a's source at first order and centre on 1² = 1.
                    Expect.isGreaterThan (r.Mean - 1.0) 0.1 "the mean of the inlined square")
            }

            test "inlining uses the names a formula entry resolved" {
                withGraph (fun () ->
                    let parent = Model.create "parent"
                    Quantity.inputWithUncertainty parent "a" "A" Units.one 1.0 0.1 |> ignore
                    declared (Quantity.formula parent "f" "F" "a * 2") |> ignore
                    let child = Model.child parent "child"
                    Quantity.inputWithUncertainty child "a" "A" Units.one 100.0 0.1 |> ignore
                    // f reads the parent's a: normal at 2 with σ = 0.2, standard error 0.2/√N = 0.0014, so 0.01 is
                    // 7 standard errors. The child's a would centre f on 200.
                    within (run child "f").Mean 2.0 0.01 "Mean")
            }

            test "a fixed seed gives fixed results" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore
                    let expr = parsed "a * 3"
                    let first = Formula.monteCarlo draws 1 0.95 model expr ()
                    let second = Formula.monteCarlo draws 1 0.95 model expr ()
                    Expect.equal first second "two readers with seed 1"
                    let other = Formula.monteCarlo draws 2 0.95 model expr ()
                    Expect.notEqual other.Mean first.Mean "seeds 1 and 2")
            }

            test "a NaN contribution gives NaN fields" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    // A derive entry's uncertainty is unknown: one contribution, NaN.
                    Quantity.derive model "d" "D" Units.one (fun () -> 3.0) |> ignore
                    expectUndefined (run model "d * 2")
                    // d^0 is 1 at every d, so every draw is 1 and the NaN comes from the contribution alone.
                    expectUndefined (run model "d^0"))
            }

            test "a NaN leaf value gives NaN fields" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1
                    a.Set nan
                    // a^0 is 1 at every a, NaN included (the evaluator's repeated multiplication), so every draw is 1
                    // and the NaN comes from the leaf's value alone.
                    expectUndefined (run model "a^0"))
            }

            test "a draw that evaluates to NaN gives NaN fields" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 0.0 1.0 |> ignore
                    // a < 0, where sqrt(a) is NaN, with probability 1/2 per draw: 20,000 draws all miss it with
                    // probability 2^−20000.
                    expectUndefined (run model "sqrt(a)"))
            }

            test "a draw that overflows gives NaN fields" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    // exp(x) overflows to +∞ above ln(1.797e308) = 709.78 (closed form of the largest double): every
                    // draw of a = 800 ± 1, and about one draw in five of b = 709 ± 1 (P(Z > 0.78) = 0.22), which 20,000
                    // draws all miss with probability 0.78^20000.
                    Quantity.inputWithUncertainty model "a" "A" Units.one 800.0 1.0 |> ignore
                    Quantity.inputWithUncertainty model "b" "B" Units.one 709.0 1.0 |> ignore
                    expectUndefined (run model "exp(a)")
                    expectUndefined (run model "exp(b)"))
            }

            test "a second read gives an equal record" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore
                    let reader = Formula.monteCarlo draws 1 0.95 model (parsed "a * 3")
                    let first = reader ()
                    // Every field is finite, so the record's equality compares each field by value.
                    Expect.equal (reader ()) first "the second read of one reader")
            }

            test "a reader is tracked" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1
                    let reader = Formula.monteCarlo draws 1 0.95 model (parsed "a")
                    let mutable runs = 0

                    let memo =
                        createMemo (fun _ ->
                            runs <- runs + 1
                            reader ())

                    memo.Value |> ignore
                    Expect.equal runs 1 "the first read"
                    a.Set 2.0
                    memo.Value |> ignore
                    Expect.equal runs 2 "a.Set adds one run"
                    a.SetUncertainty 0.2
                    memo.Value |> ignore
                    Expect.equal runs 3 "a.SetUncertainty adds one run")
            }

            test "units and conversions" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.kilometre 2.0<km> 0.1<km> |> ignore
                    // 1 km = 1000 m (the prefix kilo is 10³, SI Brochure, 9th edition, Table 7): normal at 2000 m with
                    // σ = 100 m, standard error 100/√N.
                    within (run model "convert(a, [m])").Mean 2000.0 (4.0 * 100.0 / sqrt n) "Mean")
            }

            test "arguments" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore
                    let expr = parsed "a"

                    for d in [ 1; 0 ] do
                        Expect.throwsT<ArgumentOutOfRangeException>
                            (fun () -> Formula.monteCarlo d 1 0.95 model expr |> ignore)
                            (sprintf "draws = %d" d)

                    for coverage in [ 0.0; 1.0; nan ] do
                        Expect.throwsT<ArgumentOutOfRangeException>
                            (fun () -> Formula.monteCarlo draws 1 coverage model expr |> ignore)
                            (sprintf "coverage = %g" coverage)

                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.monteCarlo draws 1 0.95 model (parsed "nope") |> ignore)
                        "an identifier the model lacks"

                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.monteCarlo draws 1 0.95 model (parsed "a + [s]") |> ignore)
                        "units that do not combine")
            }

            test "a reader keeps the entries it resolved" {
                withGraph (fun () ->
                    let model = Model.create "m"

                    let owner =
                        createRoot (fun o ->
                            Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore
                            o)

                    let reader = Formula.monteCarlo draws 1 0.95 model (parsed "a")
                    owner.Dispose ()
                    Quantity.inputWithUncertainty model "a" "A again" Units.one 10.0 0.1 |> ignore
                    // The first a: normal at 1 with σ = 0.1, standard error 0.1/√N = 0.0007, so 0.01 is 14 standard
                    // errors.
                    within (reader ()).Mean 1.0 0.01 "Mean")
            }

            test "a removed formula entry's record leaves with it" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.one 1.0 0.1 |> ignore

                    let owner =
                        createRoot (fun o ->
                            declared (Quantity.formula model "f" "F" "a * 2") |> ignore
                            o)

                    owner.Dispose ()
                    Quantity.inputWithUncertainty model "f" "F again" Units.one 5.0 0.1 |> ignore
                    // The input f is a leaf: normal at 5 with σ = 0.1, standard error 0.0007. The removed formula
                    // would centre on 2.
                    within (run model "f").Mean 5.0 0.01 "Mean")
            }
        ]
