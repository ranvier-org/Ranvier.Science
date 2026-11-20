module Ranvier.Science.Tests.NotationTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Quantities

// Reference values are closed forms written in each test: 1000 m per km (the prefix kilo is 10^3, a definition: SI
// Brochure, 9th edition, Table 7), so 2 km is 2000 m and 0.1 km is 100 m; a literal is one of its unit.

let private parsed (text: string) : Expr =
    match Formula.parse text with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected a tree for \"%s\", got: %s" text message

let private syntaxError (text: string) : string =
    match Formula.parse text with
    | Result.Error message -> message
    | Result.Ok e -> failtestf "expected a syntax error for \"%s\", got %A" text e

let private unitOf (model: Model) (text: string) : Result<UnitInfo, string> = Formula.check model (parsed text)

let private expectUnit (model: Model) (text: string) (expected: UnitInfo) : unit =
    match unitOf model text with
    | Result.Ok u ->
        Expect.isTrue
            (UnitInfo.sameUnit u expected)
            (sprintf "%s has unit %s, expected %s" text (UnitInfo.symbol u) (UnitInfo.symbol expected))
    | Result.Error message -> failtestf "expected a unit for \"%s\", got: %s" text message

let private expectUnitError (model: Model) (text: string) : string =
    match unitOf model text with
    | Result.Error message -> message
    | Result.Ok u -> failtestf "expected an error for \"%s\", got the unit %s" text (UnitInfo.symbol u)

let private valueOf (model: Model) (text: string) : float = (Formula.evaluator model (parsed text)) ()

let private relative (actual: float) (expected: float) = abs (actual - expected) / abs expected

/// A made-up unit of length with scale 2.5.
let private widget: UnitInfo = { Factors = [ ("widget", 1) ]; Dimension = Dimension.length; Scale = 2.5 }

/// A model holding `x` = 2 ± 0.1 km.
let private withKilometres () : Model =
    let model = Model.create "m"
    Quantity.inputWithUncertainty model "x" "X" Units.kilometre 2.0<km> 0.1<km> |> ignore
    model

[<Tests>]
let tests =
    testList
        "Notation"
        [
            test "unit literals parse" {
                Expect.equal (parsed "9.81 [m/s^2]") (Multiply(Number 9.81, UnitLiteral "m/s^2")) "a number and a literal"
                Expect.equal (parsed "[km]") (UnitLiteral "km") "a literal alone"
                Expect.equal (parsed "2 [m]^2") (Multiply(Number 2.0, Power(UnitLiteral "m", 2))) "the power is the literal's"
                Expect.equal (parsed "[m]^2") (Power(UnitLiteral "m", 2)) "a literal to a power"
                Expect.equal (parsed "-3 [m]") (Negate(Multiply(Number 3.0, UnitLiteral "m"))) "a negated product"
                Expect.equal (parsed "[ m / s ]") (UnitLiteral "m / s") "the text is trimmed of surrounding spaces"
            }

            test "convert parses" {
                Expect.equal (parsed "convert(x, [m])") (Convert(Name "x", "m")) "a name"
                Expect.equal (parsed "convert(x + y, [km/h])") (Convert(Add(Name "x", Name "y"), "km/h")) "a sum"
            }

            test "malformed literals and conversions" {
                Expect.stringContains (syntaxError "[m") "]" "an unclosed literal names the closing bracket"

                for text in [ "convert(x, m)"; "convert(x)"; "convert(x, [m], [s])" ] do
                    Expect.stringContains (syntaxError text) "convert" (sprintf "\"%s\" names convert" text)

                for text in [ "x [m]"; "(x) [m]" ] do
                    syntaxError text |> ignore
            }

            test "a literal has its unit" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    expectUnit model "9.81 [m/s^2]" (Units.metre / UnitOf.squared Units.second).Info
                    let message = expectUnitError model "[furlong]"
                    Expect.stringContains message "furlong" "names the symbol"
                    Expect.stringContains message "The unit literal [furlong]: " "starts with the literal"
                    expectUnitError model "[widget]" |> ignore
                    let catalogue = UnitCatalogue.si |> UnitCatalogue.add "widget" widget false
                    expectUnit (Model.createWith catalogue "w") "[widget]" widget)
            }

            test "the model's catalogue" {
                withGraph (fun () ->
                    Expect.isTrue (UnitCatalogue.tryFind "m" (Model.create "m").Catalogue).IsSome "create uses the SI catalogue"
                    let catalogue = UnitCatalogue.si |> UnitCatalogue.add "widget" widget false
                    Expect.isTrue (obj.ReferenceEquals((Model.createWith catalogue "w").Catalogue, catalogue)) "createWith"
                    let parent = Model.createWith catalogue "p"
                    Expect.isTrue (obj.ReferenceEquals((Model.child parent "c").Catalogue, parent.Catalogue)) "child")
            }

            test "conversion" {
                withGraph (fun () ->
                    let model = withKilometres ()
                    expectUnit model "convert(x, [m])" Units.metre.Info
                    Expect.equal (valueOf model "convert(x, [m])") 2000.0 "2 km is 2000 m"
                    Expect.stringContains (expectUnitError model "convert(x, [s])") "km to s" "names both units"
                    Expect.stringContains (expectUnitError model "x + 3 [m]") "convert" "a sum does not convert"
                    Expect.equal (valueOf model "convert(x, [m]) + 3 [m]") 2003.0 "2000 m + 3 m"
                    Expect.equal (valueOf model "x + 3 [km]") 5.0 "2 km + 3 km")
            }

            test "a literal is one of its unit" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Expect.equal (valueOf model "[km]") 1.0 "one km"
                    Expect.equal (valueOf model "9.81 [m/s^2]") 9.81 "9.81 times one")
            }

            test "measured conversion" {
                withGraph (fun () ->
                    let model = withKilometres ()
                    let converted = Formula.measuredEvaluator model (parsed "convert(x, [m])") ()
                    Expect.equal (Uncertain.value converted) 2000.0 "2 km is 2000 m"
                    let u = Uncertain.standardUncertainty converted
                    Expect.isTrue (relative u 100.0 <= 1e-15) (sprintf "%.17g within relative 1e-15 of 100" u)
                    Expect.equal (Uncertain.contributions converted).Length 1 "the factor adds no source"
                    let literal = Formula.measuredEvaluator model (parsed "9.81 [m/s^2]") ()
                    Expect.equal (Uncertain.contributions literal).Length 0 "a literal is exact"

                    let entry =
                        match Quantity.formula model "x_m" "X in metres" "convert(x, [m])" with
                        | Result.Ok e -> e
                        | Result.Error message -> failtestf "expected an entry, got: %s" message

                    Expect.isTrue (UnitInfo.sameUnit entry.Unit Units.metre.Info) "the entry is in metres"
                    Expect.equal (entry.Read ()) 2000.0 "the entry's value"
                    let entryU = entry.Uncertainty ()
                    Expect.isTrue (relative entryU 100.0 <= 1e-15) (sprintf "%.17g within relative 1e-15 of 100" entryU))
            }

            test "the measured evaluator keeps the evaluator's bits" {
                withGraph (fun () ->
                    let model = withKilometres ()

                    for text in
                        [ "[km]"; "9.81 [m/s^2]"; "2 [m]^2"; "-3 [m]"; "convert(x, [m])"; "convert(x, [m]) + 3 [m]"; "x * 0.7 [km]" ] do
                        let expr = parsed text

                        Expect.equal
                            (Uncertain.value (Formula.measuredEvaluator model expr ()))
                            (Formula.evaluator model expr ())
                            (text + ": the evaluator's bits"))
            }

            test "the evaluators raise for a formula that does not check" {
                withGraph (fun () ->
                    let model = withKilometres ()

                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.evaluator model (parsed "x + [s]") |> ignore)
                        "evaluator"

                    Expect.throwsT<ArgumentException>
                        (fun () -> Formula.measuredEvaluator model (parsed "x + [s]") |> ignore)
                        "measuredEvaluator"

                    Expect.throwsC
                        (fun () -> Formula.evaluator model (parsed "x + [s]") |> ignore)
                        (fun e -> Expect.stringContains e.Message "+ needs operands of one unit" "check's message"))
            }
        ]
