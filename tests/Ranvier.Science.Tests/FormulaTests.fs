module Ranvier.Science.Tests.FormulaTests

open System
open Expecto
open Ranvier
open Ranvier.Quantities

let private parsed (text: string) : Expr =
    match Formula.parse text with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected a tree for \"%s\", got: %s" text message

let private isError (r: Result<'a, string>) : bool =
    match r with
    | Result.Error _ -> true
    | Result.Ok _ -> false

/// A model with the page's names, and the input behind `mean`: `mean` is 7.3, `target` 2.1 and `se` 0.37.
let private pageWithMean () : Model * Input<mK> =
    let model = Model.create "page"
    let mean = Quantity.input model "mean" "Mean" Units.millikelvin 7.3<mK>
    Quantity.input model "se" "Standard error" Units.millikelvin 0.37<mK> |> ignore
    Quantity.input model "target" "Target" Units.millikelvin 2.1<mK> |> ignore
    Quantity.input model "variance" "Variance" (UnitOf.squared Units.millikelvin) 0.5<mK^2> |> ignore
    Quantity.input model "n" "Count" Units.one 12.0 |> ignore
    Quantity.input model "alpha" "Alpha" Units.one 0.05 |> ignore
    Quantity.input model "rate" "Rate" (Units.one / Units.second) 4.0</s> |> ignore
    Quantity.input model "temp_k" "Temperature" Units.kelvin 300.0<K> |> ignore
    model, mean

let private pageModel () : Model = fst (pageWithMean ())

let private unitOf (model: Model) (text: string) : Result<UnitInfo, string> =
    Formula.check model (parsed text)

let private expectUnit (model: Model) (text: string) (expected: UnitInfo) : unit =
    match unitOf model text with
    | Result.Ok u ->
        Expect.isTrue (UnitInfo.equivalent u expected) (sprintf "%s has unit %s, expected %s" text (UnitInfo.symbol u) (UnitInfo.symbol expected))
    | Result.Error message -> failtestf "expected a unit for \"%s\", got: %s" text message

let private expectUnitError (model: Model) (text: string) : string =
    match unitOf model text with
    | Result.Error message -> message
    | Result.Ok u -> failtestf "expected an error for \"%s\", got the unit %s" text (UnitInfo.symbol u)

let private valueOf (model: Model) (text: string) : float = (Formula.evaluator model (parsed text)) ()

let private pageFormula = "(mean - target) / se"

[<Tests>]
let tests =
    testList
        "Formula"
        [
            test "precedence and association" {
                Expect.equal (parsed "1 + 2 * 3") (Add(Number 1.0, Multiply(Number 2.0, Number 3.0))) "multiplication binds tighter"
                Expect.equal (parsed "8 - 2 - 1") (Subtract(Subtract(Number 8.0, Number 2.0), Number 1.0)) "subtraction associates left"
                Expect.equal (parsed "-x^2") (Negate(Power(Name "x", 2))) "a power binds tighter than a minus"
                Expect.equal (parsed "x^-2") (Power(Name "x", -2)) "a signed exponent"
            }

            test "the page's formula" {
                Expect.equal (parsed pageFormula) (Divide(Subtract(Name "mean", Name "target"), Name "se")) "the tree"
            }

            test "calls and numbers" {
                Expect.equal
                    (parsed "min(a, max(b, 1.5e-3))")
                    (Call("min", [ Name "a"; Call("max", [ Name "b"; Number 0.0015 ]) ]))
                    "nested calls"

                Expect.equal (parsed "0.1") (Number 0.1) "0.1 is read exactly"
            }

            test "typographic operators" {
                Expect.equal (parsed "a × b ÷ c − d") (parsed "a * b / c - d") "the same tree"
                Expect.equal (parsed "a · b") (parsed "a * b") "the middle dot multiplies"
            }

            test "text that is not a formula" {
                for text in [ ""; "   "; "1 +"; "(1"; "1 2"; "x^1.5"; "2^3^2"; "foo(1)"; "sqrt(1, 2)"; "a $ b"; ".5" ] do
                    Expect.isTrue (isError (Formula.parse text)) (sprintf "\"%s\" is not a formula" text)

                match Formula.parse "1 +" with
                | Result.Error message -> Expect.stringContains message "4" "reading stopped at character 4"
                | Result.Ok e -> failtestf "expected an error, got %A" e
            }

            test "units of the page's formula" {
                withGraph (fun () ->
                    match unitOf (pageModel ()) pageFormula with
                    | Result.Ok u ->
                        Expect.isTrue (UnitInfo.equivalent u UnitInfo.dimensionless) (sprintf "dimensionless, got %s" (UnitInfo.symbol u))
                    | Result.Error message -> failtestf "expected a unit, got: %s" message)
            }

            test "sums need one unit" {
                withGraph (fun () ->
                    let model = pageModel ()
                    let message = expectUnitError model "mean + temp_k"
                    Expect.stringContains message "mK" "names the first unit"
                    Expect.stringContains message "and K:" "names kelvin on its own as the second unit"
                    Expect.stringContains message "convert" "says the notation does not convert"
                    expectUnitError model "mean + rate" |> ignore
                    expectUnitError model "mean + 1" |> ignore
                    expectUnitError model "min(mean, n)" |> ignore)
            }

            test "products, powers and roots" {
                withGraph (fun () ->
                    let model = pageModel ()
                    expectUnit model "2 * mean" Units.millikelvin.Info
                    expectUnit model "mean^2" (model.TryFind "variance").Value.Unit
                    expectUnit model "sqrt(variance)" Units.millikelvin.Info
                    expectUnitError model "sqrt(mean)" |> ignore
                    expectUnit model "abs(mean)" Units.millikelvin.Info
                    expectUnit model "-mean" Units.millikelvin.Info)
            }

            test "transcendental functions need a pure number" {
                withGraph (fun () ->
                    let model = pageModel ()
                    expectUnitError model "exp(mean)" |> ignore
                    expectUnit model "exp(mean / se)" UnitInfo.dimensionless
                    expectUnit model "ln(n)" UnitInfo.dimensionless)
            }

            test "an unknown name" {
                withGraph (fun () ->
                    let message = expectUnitError (pageModel ()) "mean + nope"
                    Expect.stringContains message "nope" "names the identifier")
            }

            test "evaluation tracks its reads" {
                withGraph (fun () ->
                    let model, mean = pageWithMean ()
                    let eval = Formula.evaluator model (parsed pageFormula)
                    let mutable runs = 0

                    let memo =
                        createMemo (fun _ ->
                            runs <- runs + 1
                            eval ())

                    Expect.equal memo.Value ((7.3 - 2.1) / 0.37) "the bits of the F# expression"
                    Expect.equal runs 1 "the first read computes once"
                    memo.Value |> ignore
                    Expect.equal runs 1 "a second read computes nothing"
                    mean.Set 9.9<mK>
                    Expect.equal memo.Value ((9.9 - 2.1) / 0.37) "the bits of the F# expression after the write"
                    Expect.equal runs 2 "a write to mean recomputes once")
            }

            test "integer powers multiply" {
                withGraph (fun () ->
                    let model = Model.create "p"
                    Quantity.input model "x" "X" Units.one 0.7 |> ignore
                    let x = 0.7
                    Expect.equal (valueOf model "x^3") (x * x * x) "x^3 is x * x * x"
                    Expect.equal (valueOf model "x^-2") (1.0 / (x * x)) "x^-2 is 1 / (x * x)"
                    Expect.equal (valueOf model "x^0") 1.0 "x^0 is 1")
            }

            test "no exception from arithmetic" {
                withGraph (fun () ->
                    let model = pageModel ()
                    Expect.isTrue (Double.IsPositiveInfinity(valueOf model "1 / (n - n)")) "a division by zero is an infinity"
                    Expect.isTrue (Double.IsNaN(valueOf model "ln(0 - 1)")) "the logarithm of a negative number is NaN")
            }

            test "a row's name hides the page's" {
                withGraph (fun () ->
                    let page = pageModel ()
                    let row = Model.child page "row"
                    Quantity.input row "mean" "Row mean" Units.millikelvin 99.0<mK> |> ignore
                    Expect.equal (valueOf row "mean") 99.0 "mean reads the row's entry"
                    Expect.equal (valueOf row "alpha") 0.05 "alpha reads the page's entry"
                    Expect.equal (valueOf page "mean") 7.3 "the page still reads its own")
            }

            test "an exponent has a range" {
                Expect.equal (parsed "x^1000") (Power(Name "x", 1000)) "1000 is the largest exponent"
                Expect.equal (parsed "x^-1000") (Power(Name "x", -1000)) "-1000 is the smallest exponent"
                Expect.isTrue (isError (Formula.parse "x^1001")) "1001 is out of range"
                Expect.isTrue (isError (Formula.parse "x^-1001")) "-1001 is out of range"

                match Formula.parse "x^1001" with
                | Result.Error message -> Expect.stringContains message "1000" "the message names the limit"
                | Result.Ok e -> failtestf "expected an error, got %A" e

                Expect.isTrue (isError (Formula.parse ("x^" + String('9', 40)))) "a 40-digit exponent is an error and raises nothing"
            }

            test "an unknown name raises when the evaluator is built" {
                withGraph (fun () ->
                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.evaluator (pageModel ()) (parsed "mean + nope") |> ignore)
                        "the identifier is missing")
            }

            test "a pending entry leaves the formula pending" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let feed = ManualStream<float>()
                    let total = createStreamFold 0.0 (+) (fun _ -> feed.Source)
                    Quantity.derive model "total" "Total" Units.metre (fun () -> total.Value * 1.0<m>) |> ignore
                    let eval = Formula.evaluator model (parsed "total * 2")
                    let memo = createMemo (fun _ -> eval ())
                    Expect.equal memo.TryValue Pending "pending before the first item"
                    feed.Push 2.5
                    Expect.equal memo.TryValue (Ready 5.0) "ready after an item")
            }
        ]
