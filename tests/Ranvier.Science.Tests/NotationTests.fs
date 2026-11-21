module Ranvier.Science.Tests.NotationTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Quantities

// Reference values are closed forms written in each test: 1000 m per km (the prefix kilo is 10^3, a definition: SI
// Brochure, 9th edition, Table 7), so 2 km is 2000 m and 0.1 km is 100 m; a literal is one of its unit; 1° is π/180 rad
// (a definition: SI Brochure, 9th edition, Table 8). The derivatives are the calculus's closed forms, written in the
// tests.

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

/// Expects `UnitInfo.dimensionless` itself: no factors and scale 1.
let private expectPure (model: Model) (text: string) : unit =
    Expect.equal (unitOf model text) (Result.Ok UnitInfo.dimensionless) (text + " is dimensionless at scale 1")

let private valueOf (model: Model) (text: string) : float = (Formula.evaluator model (parsed text)) ()

let private relative (actual: float) (expected: float) = abs (actual - expected) / abs expected

/// The contributions of the measured value of `text`, in source order.
let private contributionsOf (model: Model) (text: string) : float[] =
    Formula.measuredEvaluator model (parsed text) ()
    |> Uncertain.contributions
    |> Array.map (fun struct (_, c) -> c)

let private expectWithin (tolerance: float) (actual: float) (expected: float) (what: string) : unit =
    Expect.isTrue
        (relative actual expected <= tolerance)
        (sprintf "%s: %.17g within relative %g of %.17g" what actual tolerance expected)

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

            test "the new functions parse" {
                for name in [ "sin"; "cos"; "tan"; "asin"; "acos"; "atan"; "sinh"; "cosh"; "tanh" ] do
                    Expect.equal (parsed (name + "(x)")) (Call(name, [ Name "x" ])) (name + " takes one argument")

                Expect.equal (parsed "atan2(y, x)") (Call("atan2", [ Name "y"; Name "x" ])) "atan2 takes two"
                Expect.equal (parsed "hypot(x, y)") (Call("hypot", [ Name "x"; Name "y" ])) "hypot takes two"
                Expect.stringContains (syntaxError "sin(1, 2)") "takes 1 argument" "sin takes one"
                Expect.stringContains (syntaxError "atan2(1)") "takes 2 argument" "atan2 takes two"
            }

            test "dimensionless arguments are taken at scale 1" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    expectPure model "sin(30 [deg])"
                    Expect.equal (valueOf model "sin(30 [deg])") (Math.Sin(30.0 * (Math.PI / 180.0))) "sin(π/6)"
                    Quantity.input model "r" "R" (Units.kilometre / Units.metre) 0.001<km/m> |> ignore
                    expectPure model "exp(r)"
                    Expect.equal (valueOf model "exp(r)") (Math.Exp(0.001 * 1000.0)) "0.001 km/m is the pure number 1"
                    Expect.equal (valueOf model "ln(r)") (Math.Log(0.001 * 1000.0)) "ln of the pure number 1"
                    Quantity.input model "x" "X" Units.metre 2.0<m> |> ignore
                    Expect.stringContains (expectUnitError model "exp(x)") "exp needs a dimensionless operand" "exp"
                    Expect.stringContains (expectUnitError model "sin(x)") "sin needs a dimensionless operand" "sin")
            }

            test "the inverse functions return radians" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    expectPure model "asin(1)"
                    Expect.equal (valueOf model "asin(1)") (Math.Asin 1.0) "asin(1) in radians"

                    Expect.equal
                        (valueOf model "convert(asin(1), [deg])")
                        (Math.Asin 1.0 * (1.0 / (Math.PI / 180.0)))
                        "π/2 rad in degrees")
            }

            test "atan2 and hypot" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.input model "y" "Y" Units.metre 3.0<m> |> ignore
                    Quantity.input model "x" "X" Units.metre 4.0<m> |> ignore
                    Quantity.input model "t" "T" Units.second 1.0<s> |> ignore
                    expectPure model "atan2(y, x)"
                    Expect.equal (valueOf model "atan2(y, x)") (Math.Atan2(3.0, 4.0)) "atan2(3, 4)"
                    expectUnit model "hypot(x, y)" Units.metre.Info
                    Expect.equal (valueOf model "hypot(x, y)") 5.0 "4·sqrt(1 + 0.75²) = 4·1.25"
                    let mixed = expectUnitError model "atan2(y, t)"
                    Expect.stringContains mixed "atan2 needs operands of one unit" "m and s"
                    Expect.stringContains (expectUnitError model "hypot(x, 3 [km])") "does not convert" "m and km")
            }

            test "hypot keeps large and zero arguments" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Expect.equal (valueOf model "hypot(1e200 [m], 1e200 [m])") (1e200 * sqrt 2.0) "1e200·sqrt(1 + 1²)"
                    Expect.equal (valueOf model "hypot(0 [m], 0 [m])") 0.0 "0 at the origin"
                    Expect.equal (valueOf model "hypot(-3 [m], -4 [m])") 5.0 "4·sqrt(1 + 0.75²) for negative operands"
                    Quantity.input model "big" "Big" Units.metre (infinity * 1.0<m>) |> ignore
                    Quantity.input model "unknown" "Unknown" Units.metre (nan * 1.0<m>) |> ignore
                    Expect.equal (valueOf model "hypot(unknown, -big)") infinity "+∞ when either is infinite"
                    let withNaN = valueOf model "hypot(3 [m], unknown)"
                    Expect.isTrue (Double.IsNaN withNaN) "NaN when either is NaN otherwise")
            }

            test "pi" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    expectPure model "pi"
                    Expect.equal (valueOf model "2 * pi") (2.0 * Math.PI) "2π"
                    let declaring = Model.create "d"
                    Quantity.input declaring "pi" "Pi" Units.metre 3.0<m> |> ignore
                    expectUnit declaring "pi" Units.metre.Info
                    Expect.equal (valueOf declaring "pi") 3.0 "the model's entry"
                    Expect.equal (valueOf (Model.child declaring "c") "pi") 3.0 "the parent's entry")
            }

            test "0.1.0's pure-number rule is replaced" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.input model "x" "X" Units.kilometre 2.0<km> |> ignore
                    Quantity.input model "y" "Y" Units.metre 500.0<m> |> ignore
                    expectPure model "exp(x / y)"
                    Expect.equal (valueOf model "exp(x / y)") (Math.Exp((2.0 / 500.0) * 1000.0)) "2 km / 500 m is 4"
                    let page = Model.create "page"
                    Quantity.input page "mean" "Mean" Units.millikelvin 7.3<mK> |> ignore
                    Quantity.input page "se" "Standard error" Units.millikelvin 0.37<mK> |> ignore
                    Expect.equal (valueOf page "exp(mean / se)") (Math.Exp(7.3 / 0.37)) "mK/mK has scale 1")
            }

            test "measured trigonometry" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "a" "A" Units.degree 30.0<deg> 1.0<deg> |> ignore
                    let s = Math.PI / 180.0
                    let measured = Formula.measuredEvaluator model (parsed "sin(a)") ()
                    Expect.equal (Uncertain.value measured) (Math.Sin(30.0 * s)) "sin(30°)"
                    let contributions = contributionsOf model "sin(a)"
                    Expect.equal contributions.Length 1 "a's source alone"
                    expectWithin 1e-14 contributions[0] (Math.Cos(30.0 * s) * s) "cos(30°) · π/180 · 1")
            }

            test "measured functions" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.inputWithUncertainty model "v" "V" Units.one 0.5 0.01 |> ignore
                    let ratio = Units.kilometre / Units.metre
                    Quantity.inputWithUncertainty model "r" "R" ratio 0.001<km/m> 0.0001<km/m> |> ignore
                    Quantity.inputWithUncertainty model "y" "Y" Units.metre 3.0<m> 0.1<m> |> ignore
                    Quantity.inputWithUncertainty model "x" "X" Units.metre 4.0<m> 0.2<m> |> ignore
                    Quantity.inputWithUncertainty model "z" "Z" Units.metre 0.0<m> 1.0<m> |> ignore

                    // The derivative at v = 0.5 times u(v) = 0.01; exp(r) reads r as 1 with u = 0.1, so its
                    // contribution is e¹ · 1000 · 0.0001.
                    for text, expected in
                        [
                            "sin(v)", cos 0.5 * 0.01
                            "cos(v)", -(sin 0.5) * 0.01
                            "tan(v)", 0.01 / (cos 0.5 * cos 0.5)
                            "asin(v)", 0.01 / sqrt (1.0 - 0.25)
                            "acos(v)", -0.01 / sqrt (1.0 - 0.25)
                            "atan(v)", 0.01 / (1.0 + 0.25)
                            "sinh(v)", cosh 0.5 * 0.01
                            "cosh(v)", sinh 0.5 * 0.01
                            "tanh(v)", (1.0 - tanh 0.5 * tanh 0.5) * 0.01
                            "exp(r)", exp 1.0 * 1000.0 * 0.0001
                        ] do
                        let contributions = contributionsOf model text
                        Expect.equal contributions.Length 1 (text + ": one source")
                        expectWithin 1e-14 contributions[0] expected text

                    // y = 3 m and x = 4 m, so x² + y² = 25 and sqrt(x² + y²) = 5; y's source precedes x's.
                    let atan2 = contributionsOf model "atan2(y, x)"
                    expectWithin 1e-14 atan2[0] (4.0 / 25.0 * 0.1) "atan2 in y: x/(x² + y²) · u(y)"
                    expectWithin 1e-14 atan2[1] (-3.0 / 25.0 * 0.2) "atan2 in x: −y/(x² + y²) · u(x)"
                    let hypot = contributionsOf model "hypot(x, y)"
                    expectWithin 1e-14 hypot[0] (3.0 / 5.0 * 0.1) "hypot in y: y/h · u(y)"
                    expectWithin 1e-14 hypot[1] (4.0 / 5.0 * 0.2) "hypot in x: x/h · u(x)"
                    for text in [ "hypot(z, z)"; "atan2(z, z)" ] do
                        let contributions = contributionsOf model text
                        Expect.isTrue (Double.IsNaN contributions[0]) (text + ": NaN derivatives at the origin")

                    for text in
                        [
                            "sin(v)"; "cos(v)"; "tan(v)"; "asin(v)"; "acos(v)"; "atan(v)"
                            "sinh(v)"; "cosh(v)"; "tanh(v)"; "exp(r)"; "ln(r)"; "log10(r)"
                            "sin(30 [deg])"; "atan2(y, x)"; "atan2(z, z)"; "hypot(x, y)"; "hypot(z, z)"
                            "hypot(1e200 [m], 1e200 [m])"; "2 * pi"
                        ] do
                        let expr = parsed text

                        Expect.equal
                            (Uncertain.value (Formula.measuredEvaluator model expr ()))
                            (Formula.evaluator model expr ())
                            (text + ": the evaluator's bits"))
            }
        ]
