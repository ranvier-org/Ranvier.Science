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

            test "symbols without a numerator or with several denominator factors" {
                let perSecond = (Units.one / Units.second).Info
                Expect.equal (UnitInfo.symbol perSecond) "1/s" "no numerator"
                let newton = (Units.kilogram * Units.metre / UnitOf.squared Units.second).Info
                Expect.equal (UnitInfo.symbol newton) "kg·m/s^2" "two numerator factors"
                let perMetreSecondSquared = (Units.kilogram / (Units.metre * UnitOf.squared Units.second)).Info
                Expect.equal (UnitInfo.symbol perMetreSecondSquared) "kg/(m·s^2)" "two denominator factors"
                let perSecondSquared = (Units.one / UnitOf.squared Units.second).Info
                Expect.equal (UnitInfo.symbol perSecondSquared) "1/s^2" "no numerator, an exponent"
            }
        ]
