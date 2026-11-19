module Ranvier.Science.Tests.UnitsTests

open Expecto
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

            test "a scale reached by two routes can differ in the last place" {
                let u = (UnitOf.define<m> "u" Dimension.length 0.7).Info
                let inverse = UnitInfo.power u -1

                match UnitInfo.sqrt (UnitInfo.power u -2) with
                | Ok root ->
                    Expect.equal root.Dimension inverse.Dimension "dimension"
                    Expect.isTrue (UnitInfo.sameDimension root inverse) "same dimension"

                    Expect.isTrue
                        (abs (root.Scale - inverse.Scale) <= 1e-15 * abs inverse.Scale)
                        $"scales {root.Scale} and {inverse.Scale} within relative 1e-15"

                    Expect.isFalse (UnitInfo.sameUnit root inverse) $"scales {root.Scale} and {inverse.Scale} differ in the last place"
                | Error e -> failwith $"expected Ok, got Error {e}"
            }

            test "equivalent holds across routes that differ in the last place" {
                let u = (UnitOf.define<m> "u" Dimension.length 0.7).Info
                let inverse = UnitInfo.power u -1

                match UnitInfo.sqrt (UnitInfo.power u -2) with
                | Ok root ->
                    Expect.isFalse (UnitInfo.sameUnit root inverse) "sameUnit separates them"
                    Expect.isTrue (UnitInfo.equivalent root inverse) "equivalent joins them"
                | Error e -> failwith $"expected Ok, got Error {e}"
            }

            test "equivalent separates sizes and dimensions" {
                Expect.isFalse (UnitInfo.equivalent Units.millikelvin.Info Units.kelvin.Info) "millikelvin and kelvin"

                Expect.isFalse
                    (UnitInfo.equivalent (Units.kilometre / Units.metre).Info UnitInfo.dimensionless)
                    "kilometre per metre and dimensionless"

                Expect.isFalse (UnitInfo.equivalent Units.metre.Info Units.second.Info) "metre and second"
                Expect.isTrue (UnitInfo.equivalent Units.metre.Info Units.metre.Info) "a unit and itself"
            }
        ]
