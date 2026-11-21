module Ranvier.Science.Tests.UnitsTests

open System
open Expecto
open Microsoft.FSharp.Data.UnitSystems.SI
open Ranvier.Quantities

[<Tests>]
let tests =
    testList
        "Units"
        [
            test "kilometres per second" {
                let u = (Units.kilometre / Units.second).Info
                Expect.equal (UnitInfo.symbol u) "km/s" "symbol"
                Expect.equal u.Dimension { Dimension.none with Length = 1; Time = -1 } "dimension"
                Expect.equal u.Scale 1000.0 "scale"
            }

            test "millikelvin squared and back" {
                let u2 = (UnitOf.squared Units.millikelvin).Info
                Expect.equal (UnitInfo.symbol u2) "mK^2" "symbol"
                Expect.equal u2.Dimension.Temperature 2 "temperature exponent"
                Expect.isTrue (abs (u2.Scale - 1e-6) <= 1e-15 * 1e-6) $"scale {u2.Scale} within relative 1e-15 of 1e-6"

                match UnitInfo.sqrt u2 with
                | Ok u ->
                    Expect.equal (UnitInfo.symbol u) "mK" "symbol of the root"
                    Expect.isTrue (UnitInfo.sameUnit u Units.millikelvin.Info) $"the root is millikelvin (scale {u.Scale})"
                | Error e -> failwith $"expected Ok, got Error {e}"
            }

            test "the square root of an odd power is an error" {
                match UnitInfo.sqrt Units.metre.Info with
                | Ok u -> failwith $"expected Error, got Ok {UnitInfo.symbol u}"
                | Error e -> Expect.stringContains e "m" "the message names the unit"
            }

            test "metres and kilometres share a dimension and not a unit" {
                Expect.isTrue (UnitInfo.sameDimension Units.metre.Info Units.kilometre.Info) "same dimension"
                Expect.isFalse (UnitInfo.sameUnit Units.metre.Info Units.kilometre.Info) "different unit"
            }

            test "a product cancels to dimensionless" {
                let u = (Units.second * (Units.one / Units.second)).Info
                Expect.equal u.Dimension Dimension.none "dimension"
                Expect.equal (UnitInfo.symbol u) "" "symbol"
            }

            test "a unit needs a positive finite scale" {
                for scale in [ 0.0; -1.0; nan; infinity ] do
                    Expect.throwsT<System.ArgumentOutOfRangeException>
                        (fun () -> UnitOf.define<m> "m" Dimension.length scale |> ignore)
                        $"scale {scale}"
            }

            test "a unit needs a symbol" {
                for symbol in [ null; "" ] do
                    Expect.throwsT<System.ArgumentException>
                        (fun () -> UnitOf.define<m> symbol Dimension.length 1.0 |> ignore)
                        (sprintf "symbol %A" symbol)
            }

            test "symbols without a numerator or with several denominator factors" {
                let perSecond = (Units.one / Units.second).Info
                Expect.equal (UnitInfo.symbol perSecond) "1/s" "no numerator"
                let newton = (Units.kilogram * Units.metre / UnitOf.squared Units.second).Info
                Expect.equal (UnitInfo.symbol newton) "kg·m/s^2" "two numerator factors"
                let perMetreSecondSquared = (Units.kilogram / (Units.metre * UnitOf.squared Units.second)).Info
                Expect.equal (UnitInfo.symbol perMetreSecondSquared) "kg/(m·s^2)" "two denominator factors"
                let perSecondSquared = (Units.one / UnitOf.squared Units.second).Info
                Expect.equal (UnitInfo.symbol perSecondSquared) "1/s^2" "no numerator, an exponent"
                let perMetreSecond = (Units.one / (Units.metre * Units.second)).Info
                Expect.equal (UnitInfo.symbol perMetreSecond) "1/(m·s)" "no numerator, two denominator factors"
            }

            test "sameUnit joins two routes that differ in the last place" {
                let u = UnitOf.define<m> "u" Dimension.length 0.7
                let inverse = UnitInfo.power u.Info -1

                match UnitInfo.sqrt (UnitInfo.power u.Info -2) with
                | Ok root ->
                    Expect.notEqual root.Scale inverse.Scale "the two routes differ in the last place"
                    Expect.isTrue (UnitInfo.sameUnit root inverse) $"sameUnit joins {root.Scale} and {inverse.Scale}"
                    Expect.isTrue (UnitInfo.sameUnit inverse root) $"sameUnit joins {inverse.Scale} and {root.Scale}"
                | Error e -> failwith $"expected Ok, got Error {e}"
            }

            test "sameUnit separates sizes and dimensions" {
                Expect.isFalse (UnitInfo.sameUnit Units.millikelvin.Info Units.kelvin.Info) "millikelvin and kelvin"

                Expect.isFalse
                    (UnitInfo.sameUnit (Units.kilometre / Units.metre).Info UnitInfo.dimensionless)
                    "kilometre per metre and dimensionless"

                Expect.isFalse (UnitInfo.sameUnit Units.metre.Info Units.second.Info) "metre and second"
                Expect.isTrue (UnitInfo.sameUnit Units.metre.Info Units.metre.Info) "a unit and itself"
            }

            test "sameUnit at the relative bound, in both orders" {
                // 2^-52, the spacing of doubles at 1. 1e-12 / 2^-52 = 4503.6 (closed form), so 4503 spacings lie
                // within the relative bound of 1e-12 and 4504 lie beyond it.
                let spacing = 1.0 / 4503599627370496.0
                let at (x: float) = { Units.metre.Info with Scale = x }
                let within = at (1.0 + 4503.0 * spacing)
                let beyond = at (1.0 + 4504.0 * spacing)
                Expect.isTrue (UnitInfo.sameUnit (at 1.0) within) "1 and 1 + 4503 spacings"
                Expect.isTrue (UnitInfo.sameUnit within (at 1.0)) "1 + 4503 spacings and 1"
                Expect.isFalse (UnitInfo.sameUnit (at 1.0) beyond) "1 and 1 + 4504 spacings"
                Expect.isFalse (UnitInfo.sameUnit beyond (at 1.0)) "1 + 4504 spacings and 1"
            }

            test "the SI measures are FSharp.Core's" {
                let x: float<UnitSymbols.m> = 2.0<m>
                let mass: float<UnitSymbols.kg> = 2.0<kg>
                let time: float<UnitSymbols.s> = 2.0<s>
                let temperature: float<UnitSymbols.K> = 2.0<K>
                let metre: UnitOf<UnitSymbols.m> = Units.metre
                Expect.equal (float x) 2.0 "a float<m> is a float<UnitSymbols.m>"
                Expect.equal (float mass) 2.0 "a float<kg> is a float<UnitSymbols.kg>"
                Expect.equal (float time) 2.0 "a float<s> is a float<UnitSymbols.s>"
                Expect.equal (float temperature) 2.0 "a float<K> is a float<UnitSymbols.K>"
                Expect.equal (UnitInfo.symbol metre.Info) "m" "a UnitOf<m> is a UnitOf<UnitSymbols.m>"
            }

            test "a scale that overflows or vanishes raises" {
                // Closed forms: 1000^103 = 1e309 > 1.797e308, the largest double, and 1000^102 = 1e306 is finite;
                // 1000^-108 = 1e-324 is below half of 4.9e-324, the smallest subnormal, so it rounds to 0.
                Expect.throwsT<ArithmeticException>
                    (fun () -> UnitInfo.power Units.kilometre.Info 103 |> ignore)
                    "km^103 overflows"

                Expect.throwsC
                    (fun () -> UnitInfo.power Units.kilometre.Info 103 |> ignore)
                    (fun e -> Expect.stringContains e.Message "km^103" "the message names the result")

                Expect.throwsT<ArithmeticException>
                    (fun () -> UnitInfo.power Units.kilometre.Info -108 |> ignore)
                    "km^-108 has scale 0"

                let big = UnitOf.define<m> "big" Dimension.length 1e200
                let tiny = UnitOf.define<m> "tiny" Dimension.length 1e-200

                Expect.throwsT<ArithmeticException>
                    (fun () -> UnitInfo.multiply big.Info big.Info |> ignore)
                    "1e200 · 1e200 = 1e400 overflows"

                Expect.throwsT<ArithmeticException>
                    (fun () -> UnitInfo.divide tiny.Info big.Info |> ignore)
                    "1e-200 / 1e200 = 1e-400 rounds to 0"

                let square = { Factors = [ ("z", 2) ]; Dimension = { Dimension.none with Length = 2 }; Scale = 0.0 }

                Expect.throwsT<ArithmeticException> (fun () -> UnitInfo.sqrt square |> ignore) "the root of scale 0"

                Expect.throwsT<ArithmeticException>
                    (fun () -> UnitInfo.sqrt { square with Scale = infinity } |> ignore)
                    "the root of scale ∞"

                Expect.equal (UnitInfo.power Units.kilometre.Info 102).Dimension.Length 102 "km^102 raises nothing"
            }
        ]
