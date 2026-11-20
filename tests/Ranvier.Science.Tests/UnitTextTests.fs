module Ranvier.Science.Tests.UnitTextTests

open System
open Expecto
open Ranvier.Quantities

// Reference values are closed forms written in each test (a quotient of the catalogue's scales, 1000 m per km and
// 3600 s per h, which are definitions: SI Brochure, 9th edition, Table 8 for the hour); no external source is needed.

let private p (text: string) : Result<UnitInfo, string> = UnitInfo.parse UnitCatalogue.si text

let private ok (text: string) : UnitInfo =
    match p text with
    | Ok u -> u
    | Error e -> failwith $"expected \"{text}\" to parse, got Error {e}"

let private err (text: string) : string =
    match p text with
    | Ok u -> failwith $"expected \"{text}\" to be an Error, got Ok {UnitInfo.symbol u}"
    | Error e -> e

let private okValue (r: Result<float, string>) : float =
    match r with
    | Ok v -> v
    | Error e -> failwith $"expected Ok, got Error {e}"

[<Tests>]
let tests =
    testList
        "UnitText"
        [
            test "a quotient of prefixed units" {
                let u = ok "km/h"
                Expect.equal u.Factors [ "km", 1; "h", -1 ] "factors"
                Expect.equal (UnitInfo.symbol u) "km/h" "symbol"
                Expect.equal u.Dimension { Dimension.none with Length = 1; Time = -1 } "dimension"
                Expect.equal u.Scale (1000.0 / 3600.0) "scale"
            }

            test "separators and spaces" {
                for text in [ "kg·m/s^2"; "kg*m/s^2"; "kg.m/s^2"; "kg m/s^2"; " kg  m / s ^ 2 " ] do
                    Expect.equal (ok text).Factors [ "kg", 1; "m", 1; "s", -2 ] $"factors of \"{text}\""
            }

            test "spaces around parentheses and exponents" {
                for text in [ "( m / s ) ^ 2"; " (m/s)^ 2"; "( m/s )^-0002 " ] do
                    let expected = if text.Contains "-" then [ "m", -2; "s", 2 ] else [ "m", 2; "s", -2 ]
                    Expect.equal (ok text).Factors expected $"factors of \"{text}\""
            }

            test "division associates left" {
                let u = ok "J/kg/K"
                Expect.equal u.Factors [ "J", 1; "kg", -1; "K", -1 ] "factors"
                Expect.equal (UnitInfo.symbol u) "J/(kg·K)" "symbol"
            }

            test "exponents" {
                Expect.equal (ok "m^2") (ok "m²") "m^2 and m²"
                Expect.equal (ok "s^-1") (ok "s^−1") "s^-1 and s^−1"
                Expect.equal (ok "s^-1") (ok "s⁻¹") "s^-1 and s⁻¹"
                Expect.equal (ok "(m/s)^2") (ok "(m/s)²") "(m/s)^2 and (m/s)²"
                Expect.equal (ok "(m/s)^2").Factors [ "m", 2; "s", -2 ] "factors"
                Expect.equal (ok "m²·s⁻²").Factors [ "m", 2; "s", -2 ] "m²·s⁻²"
                Expect.equal (ok "m⁴").Factors [ ("m", 4) ] "a superscript four, U+2074"
                Expect.equal (ok "m^1000").Dimension.Length 1000 "the largest exponent"
            }

            test "one as a numerator" {
                Expect.equal (ok "1/s").Factors [ ("s", -1) ] "1/s"
                Expect.equal (ok "1/(m·s)").Factors [ "m", -1; "s", -1 ] "1/(m·s)"
                Expect.equal (ok "1").Factors [] "1"
            }

            test "prefixes and look-alike characters" {
                Expect.equal (ok "hPa").Scale 100.0 "hPa"
                Expect.equal (ok "μm").Factors [ ("µm", 1) ] "Greek mu"
                Expect.equal (ok "µm").Factors [ ("µm", 1) ] "micro sign"
                Expect.equal (UnitInfo.symbol (ok "kΩ")) "kΩ" "the ohm sign reads as the Greek capital omega"
            }

            test "errors name the position" {
                for text in [ ""; "   "; "m/"; "m^"; "(m"; "2m" ] do
                    err text |> ignore

                Expect.stringContains (err "m^x") "3" "m^x"
                Expect.stringContains (err "m)") "2" "m)"
                Expect.stringContains (err "m/xyz") "xyz" "m/xyz names the symbol"
                Expect.stringContains (err "m/xyz") "3" "m/xyz names the position"
                Expect.stringContains (err "kkg") "kkg" "kkg"
                Expect.stringContains (err "m^1001") "1000" "m^1001"
                Expect.stringContains (err "km^200") "km^200" "km^200 overflows the scale"
            }

            test "conversion factors" {
                Expect.equal (UnitInfo.conversionFactor (ok "km") (ok "m")) (Ok 1000.0) "km to m"
                Expect.equal (UnitInfo.conversionFactor (ok "m") (ok "km")) (Ok(1.0 / 1000.0)) "m to km"

                Expect.equal
                    (UnitInfo.conversionFactor (ok "km/h") (ok "m/s"))
                    (Ok((1000.0 / 3600.0) / 1.0))
                    "km/h to m/s"

                match UnitInfo.conversionFactor (ok "m") (ok "s") with
                | Ok v -> failwith $"expected Error, got Ok {v}"
                | Error e ->
                    Expect.stringContains e "m" "names m"
                    Expect.stringContains e "s" "names s"

                Expect.equal (UnitInfo.convert (ok "km") (ok "m") 2.5) (Ok 2500.0) "2.5 km"

                Expect.equal
                    (UnitInfo.convert (ok "km/h") (ok "m/s") 72.0)
                    (Ok(72.0 * ((1000.0 / 3600.0) / 1.0)))
                    "72 km/h"

                Expect.isTrue (okValue (UnitInfo.conversionFactor (ok "m") (ok "m")) = 1.0) "m to m"
            }

            test "UnitOf.convert" {
                Expect.equal (UnitOf.convert Units.kilometre Units.metre 2.5<km>) 2500.0<m> "km to m"
                Expect.equal (UnitOf.convert Units.hour Units.second 1.5<h>) 5400.0<s> "h to s"
                Expect.throwsT<ArgumentException> (fun () -> UnitOf.convert Units.metre Units.second 1.0<m> |> ignore) "across dimensions"
            }
        ]
