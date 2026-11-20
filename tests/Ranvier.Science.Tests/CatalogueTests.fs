module Ranvier.Science.Tests.CatalogueTests

open System
open Expecto
open Ranvier.Quantities

let private dim l m t i th n j : Dimension =
    { Length = l; Mass = m; Time = t; Current = i; Temperature = th; Amount = n; Luminosity = j }

let private within (relative: float) (expected: float) (actual: float) =
    abs (actual - expected) <= relative * abs expected

let private found (symbol: string) : UnitInfo =
    match UnitCatalogue.tryFind symbol UnitCatalogue.si with
    | ValueSome u -> u
    | ValueNone -> failwith $"expected the catalogue to find {symbol}"

let private isFound (symbol: string) : bool =
    (UnitCatalogue.tryFind symbol UnitCatalogue.si).IsSome

let private micro = "µ"
let private ohm = "Ω"
let private degSign = "°"

let private pa = dim -1 1 -2 0 0 0 0
let private joule = dim 2 1 -2 0 0 0 0
let private watt = dim 2 1 -3 0 0 0 0
let private volt = dim 2 1 -3 -1 0 0 0

/// One row per unit of the catalogue: the unit, its symbol, its dimension and its scale in SI base units.
///
/// Sources of the scales. General knowledge (definitions set by the SI Brochure, 9th edition, 2019, Table 2 base
/// units, Table 4 derived units, Table 7 prefixes and Table 8 min, h, d, L, t and the degree; the Brochure was not
/// read here, its PDF text is font-encoded). au: 149 597 870 700 m exactly, IAU 2012 Resolution B2 (not read:
/// iau.org returned 404); it is cross-checked by the parsec below. pc: 648 000/pi au, IAU 2015 Resolution B2,
/// endnote 4, read at https://arxiv.org/html/1510.06262v2 on 2026-11-20, which gives 3.085 677 581 ... e16 m; the
/// closed form 648 000 * 149 597 870 700 = 96 939 420 213 600 000. eV and Da: CODATA 2022 at NIST, accessed
/// 2026-11-20: e = 1.602 176 634e-19 C exactly, https://physics.nist.gov/cgi-bin/cuu/Value?e, so 1 eV =
/// 1.602 176 634e-19 J; m_u = 1.660 539 068 92e-27 kg, https://physics.nist.gov/cgi-bin/cuu/Value?u. The Julian year
/// is 365.25 d of 86 400 s and the light-year is c = 299 792 458 m/s exactly (CODATA 2022,
/// https://physics.nist.gov/cgi-bin/cuu/Value?c, accessed 2026-11-20) times a Julian year. bar is 1e5 Pa and atm is
/// 101 325 Pa (general knowledge).
let private table: (UnitInfo * string * Dimension * float) list =
    [
        Units.metre.Info, "m", Dimension.length, 1.0
        Units.kilogram.Info, "kg", Dimension.mass, 1.0
        Units.second.Info, "s", Dimension.time, 1.0
        Units.kelvin.Info, "K", Dimension.temperature, 1.0
        Units.ampere.Info, "A", Dimension.current, 1.0
        Units.mole.Info, "mol", Dimension.amount, 1.0
        Units.candela.Info, "cd", Dimension.luminosity, 1.0
        Units.radian.Info, "rad", Dimension.none, 1.0
        Units.steradian.Info, "sr", Dimension.none, 1.0
        Units.hertz.Info, "Hz", dim 0 0 -1 0 0 0 0, 1.0
        Units.newton.Info, "N", dim 1 1 -2 0 0 0 0, 1.0
        Units.pascal.Info, "Pa", pa, 1.0
        Units.joule.Info, "J", joule, 1.0
        Units.watt.Info, "W", watt, 1.0
        Units.coulomb.Info, "C", dim 0 0 1 1 0 0 0, 1.0
        Units.volt.Info, "V", volt, 1.0
        Units.farad.Info, "F", dim -2 -1 4 2 0 0 0, 1.0
        Units.ohm.Info, ohm, dim 2 1 -3 -2 0 0 0, 1.0
        Units.siemens.Info, "S", dim -2 -1 3 2 0 0 0, 1.0
        Units.weber.Info, "Wb", dim 2 1 -2 -1 0 0 0, 1.0
        Units.tesla.Info, "T", dim 0 1 -2 -1 0 0 0, 1.0
        Units.henry.Info, "H", dim 2 1 -2 -2 0 0 0, 1.0
        Units.lumen.Info, "lm", Dimension.luminosity, 1.0
        Units.lux.Info, "lx", dim -2 0 0 0 0 0 1, 1.0
        Units.becquerel.Info, "Bq", dim 0 0 -1 0 0 0 0, 1.0
        Units.gray.Info, "Gy", dim 2 0 -2 0 0 0 0, 1.0
        Units.sievert.Info, "Sv", dim 2 0 -2 0 0 0 0, 1.0
        Units.katal.Info, "kat", dim 0 0 -1 0 0 1 0, 1.0
        Units.kilometre.Info, "km", Dimension.length, 1e3
        Units.centimetre.Info, "cm", Dimension.length, 1e-2
        Units.millimetre.Info, "mm", Dimension.length, 1e-3
        Units.micrometre.Info, micro + "m", Dimension.length, 1e-6
        Units.nanometre.Info, "nm", Dimension.length, 1e-9
        Units.gram.Info, "g", Dimension.mass, 1e-3
        Units.milligram.Info, "mg", Dimension.mass, 1e-6
        Units.millisecond.Info, "ms", Dimension.time, 1e-3
        Units.microsecond.Info, micro + "s", Dimension.time, 1e-6
        Units.nanosecond.Info, "ns", Dimension.time, 1e-9
        Units.millikelvin.Info, "mK", Dimension.temperature, 1e-3
        Units.kilopascal.Info, "kPa", pa, 1e3
        Units.megapascal.Info, "MPa", pa, 1e6
        Units.kilojoule.Info, "kJ", joule, 1e3
        Units.kilowatt.Info, "kW", watt, 1e3
        Units.megawatt.Info, "MW", watt, 1e6
        Units.milliampere.Info, "mA", Dimension.current, 1e-3
        Units.millivolt.Info, "mV", volt, 1e-3
        Units.kilovolt.Info, "kV", volt, 1e3
        Units.minute.Info, "min", Dimension.time, 60.0
        Units.hour.Info, "h", Dimension.time, 3600.0
        Units.day.Info, "d", Dimension.time, 86400.0
        Units.astronomicalUnit.Info, "au", Dimension.length, 149597870700.0
        Units.litre.Info, "L", dim 3 0 0 0 0 0 0, 1e-3
        Units.tonne.Info, "t", Dimension.mass, 1e3
        Units.electronvolt.Info, "eV", joule, 1.602176634e-19
        Units.dalton.Info, "Da", Dimension.mass, 1.66053906892e-27
        Units.degree.Info, degSign, Dimension.none, Math.PI / 180.0
        Units.julianYear.Info, "a", Dimension.time, 365.25 * 86400.0
        Units.lightYear.Info, "ly", Dimension.length, 299792458.0 * (365.25 * 86400.0)
        Units.parsec.Info, "pc", Dimension.length, 96939420213600000.0 / Math.PI
        Units.bar.Info, "bar", pa, 1e5
        Units.atmosphere.Info, "atm", pa, 101325.0
    ]

let private prefixable =
    set
        [
            "m"; "s"; "K"; "A"; "mol"; "cd"; "rad"; "sr"; "Hz"; "N"; "Pa"; "J"; "W"; "C"; "V"; "F"; ohm; "S"; "Wb"; "T"
            "H"; "lm"; "lx"; "Bq"; "Gy"; "Sv"; "kat"; "g"; "L"; "t"; "eV"; "Da"
        ]

[<Tests>]
let tests =
    testList
        "Catalogue"
        [
            test "every unit of the table" {
                for info, symbol, dimension, scale in table do
                    Expect.equal (UnitInfo.symbol info) symbol $"symbol of {symbol}"
                    Expect.equal info.Dimension dimension $"dimension of {symbol}"
                    Expect.isTrue (within 1e-15 scale info.Scale) $"scale of {symbol}: {info.Scale} against {scale}"
                    let f = found symbol
                    Expect.equal (UnitInfo.symbol f) symbol $"the catalogue's symbol of {symbol}"
                    Expect.isTrue (UnitInfo.sameUnit f info) $"the catalogue's {symbol} is the unit"
            }

            test "the 24 prefixes" {
                // General knowledge: the definitions of SI Brochure, 9th edition, Table 7, with quecto, ronto, ronna and
                // quetta from the 27th CGPM (2022) Resolution 3; not read here.
                let expected =
                    [
                        Prefix.quecto, "q", 1e-30
                        Prefix.ronto, "r", 1e-27
                        Prefix.yocto, "y", 1e-24
                        Prefix.zepto, "z", 1e-21
                        Prefix.atto, "a", 1e-18
                        Prefix.femto, "f", 1e-15
                        Prefix.pico, "p", 1e-12
                        Prefix.nano, "n", 1e-9
                        Prefix.micro, micro, 1e-6
                        Prefix.milli, "m", 1e-3
                        Prefix.centi, "c", 1e-2
                        Prefix.deci, "d", 1e-1
                        Prefix.deca, "da", 1e1
                        Prefix.hecto, "h", 1e2
                        Prefix.kilo, "k", 1e3
                        Prefix.mega, "M", 1e6
                        Prefix.giga, "G", 1e9
                        Prefix.tera, "T", 1e12
                        Prefix.peta, "P", 1e15
                        Prefix.exa, "E", 1e18
                        Prefix.zetta, "Z", 1e21
                        Prefix.yotta, "Y", 1e24
                        Prefix.ronna, "R", 1e27
                        Prefix.quetta, "Q", 1e30
                    ]

                Expect.equal expected.Length 24 "24 prefixes"

                for p, symbol, factor in expected do
                    Expect.equal p.Symbol symbol $"symbol of {symbol}"
                    Expect.equal p.Factor factor $"factor of {symbol}"
            }

            test "prefixed units" {
                let km = UnitInfo.prefixed Prefix.kilo Units.metre.Info
                Expect.equal (UnitInfo.symbol km) "km" "symbol"
                Expect.isTrue (UnitInfo.sameUnit km Units.kilometre.Info) "the kilometre"

                Expect.throwsT<ArgumentException>
                    (fun () -> UnitInfo.prefixed Prefix.kilo (Units.metre / Units.second).Info |> ignore)
                    "two factors"

                Expect.throwsT<ArgumentException>
                    (fun () -> UnitInfo.prefixed Prefix.kilo (UnitOf.squared Units.metre).Info |> ignore)
                    "exponent 2"

                Expect.throwsT<ArithmeticException>
                    (fun () ->
                        UnitInfo.prefixed Prefix.quetta { Factors = [ ("x", 1) ]; Dimension = Dimension.length; Scale = 1e300 }
                        |> ignore)
                    "overflow"

                // kilo times -2 is -2000 (closed form); NaN times 1000 is NaN.
                for scale, shown in [ -2.0, "-2000"; nan, "NaN" ] do
                    Expect.throwsC
                        (fun () ->
                            UnitInfo.prefixed Prefix.kilo { Factors = [ ("x", 1) ]; Dimension = Dimension.length; Scale = scale }
                            |> ignore)
                        (fun e -> Expect.stringContains e.Message ("The scale of kx is " + shown + ";") $"a scale of {shown}")

                let us: UnitOf<us> = UnitOf.prefixed Prefix.micro Units.second
                Expect.isTrue (UnitInfo.sameUnit us.Info Units.microsecond.Info) "the microsecond"
                Expect.equal (UnitInfo.symbol us.Info) (micro + "s") "its symbol"
            }

            test "tryFind takes an exact symbol first" {
                let cases =
                    [
                        "min", Units.minute.Info
                        "Pa", Units.pascal.Info
                        "cd", Units.candela.Info
                        "mm", Units.millimetre.Info
                        "Gy", Units.gray.Info
                        "T", Units.tesla.Info
                        "h", Units.hour.Info
                        "kg", Units.kilogram.Info
                    ]

                for symbol, expected in cases do
                    let f = found symbol
                    Expect.equal (UnitInfo.symbol f) symbol $"symbol of {symbol}"
                    Expect.isTrue (UnitInfo.sameUnit f expected) $"{symbol} is the exact unit"
            }

            test "tryFind \"deg\" is the degree" {
                let closedForm = Math.PI / 180.0
                let deg = found "deg"
                Expect.equal (UnitInfo.symbol deg) "deg" "symbol"
                Expect.equal deg.Dimension Dimension.none "dimensionless"
                Expect.isTrue (within 1e-15 closedForm deg.Scale) "scale pi/180"
                Expect.isTrue (within 1e-15 closedForm (found degSign).Scale) "the sign has the same scale"
                Expect.isFalse (isFound "kdeg") "not prefixable"
                Expect.isFalse (isFound ("k" + degSign)) "the sign is not prefixable"
            }

            test "tryFind then a prefix and a prefixable symbol" {
                let hPa = found "hPa"
                Expect.equal (UnitInfo.symbol hPa) "hPa" "symbol"
                Expect.isTrue (within 1e-15 100.0 hPa.Scale) "hPa"
                Expect.isTrue (within 1e-15 10.0 (found "dam").Scale) "dam: da before d"
                Expect.isTrue (within 1e-15 1e-4 (found "dL").Scale) "dL"
                Expect.isTrue (within 1e-15 (1000.0 * Units.electronvolt.Info.Scale) (found "keV").Scale) "keV"
                Expect.isTrue (within 1e-15 1e9 (found "GHz").Scale) "GHz"
                Expect.isTrue (within 1e-15 1e-9 (found (micro + "g")).Scale) "micro-gram"
            }

            test "look-alike characters read the catalogue's symbols" {
                let g = found ("μ" + "g")
                Expect.equal (UnitInfo.symbol g) (micro + "g") "U+03BC reads as U+00B5"
                Expect.equal g.Scale (found (micro + "g")).Scale "the scale of the micro-gram"
                Expect.equal (UnitInfo.symbol (found ("kΩ"))) ("k" + ohm) "U+2126 reads as U+03A9"
            }

            test "prefixes attach to g, not to kg, and only to prefixable units" {
                for _, symbol, _, _ in table do
                    Expect.equal (isFound ("Q" + symbol)) (prefixable.Contains symbol) $"Q{symbol}"

                for symbol in [ "kkg"; "mkg"; "kmin"; "kau"; "Mbar"; "Mpc"; "kkm" ] do
                    Expect.isFalse (isFound symbol) symbol
            }

            test "add" {
                let widget = { Factors = [ ("widget", 1) ]; Dimension = Dimension.length; Scale = 2.5 }
                let plain = UnitCatalogue.add "widget" widget false UnitCatalogue.si
                Expect.isTrue (UnitCatalogue.tryFind "widget" plain).IsSome "found"
                Expect.isTrue (UnitCatalogue.tryFind "kwidget" plain).IsNone "not prefixable"
                let open' = UnitCatalogue.add "widget" widget true UnitCatalogue.si

                match UnitCatalogue.tryFind "kwidget" open' with
                | ValueSome u -> Expect.isTrue (within 1e-15 2500.0 u.Scale) "kwidget scale"
                | ValueNone -> failwith "kwidget"

                Expect.isFalse (isFound "widget") "si is unchanged"

                for symbol in [ "m"; ""; "a b"; "x/y"; "x2" ] do
                    Expect.throwsT<ArgumentException>
                        (fun () -> UnitCatalogue.add symbol widget false UnitCatalogue.si |> ignore)
                        $"symbol '{symbol}'"

                Expect.throwsT<ArgumentException>
                    (fun () -> UnitCatalogue.add "w" (Units.metre / Units.second).Info true UnitCatalogue.si |> ignore)
                    "prefixable two-factor unit"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> UnitCatalogue.add "w" { widget with Scale = 0.0 } false UnitCatalogue.si |> ignore)
                    "scale 0"
            }

            test "an exact symbol wins over a prefix and a prefixable symbol" {
                // Made up so that both readings exist: "mx" is the exact, non-prefixable unit of scale 7, and also
                // milli + "x" (prefixable, scale 3), which would be 0.003. Only the exact reading gives 7.
                let unitOf name scale = { Factors = [ (name, 1) ]; Dimension = Dimension.length; Scale = scale }

                let catalogue =
                    UnitCatalogue.si
                    |> UnitCatalogue.add "x" (unitOf "x" 3.0) true
                    |> UnitCatalogue.add "mx" (unitOf "mx" 7.0) false

                match UnitCatalogue.tryFind "mx" catalogue with
                | ValueSome u -> Expect.equal u.Scale 7.0 "the exact mx, not milli-x"
                | ValueNone -> failwith "mx"
            }

            test "da is tried before d" {
                // Made up so that both readings exist: "am" is a prefixable unit of scale 5, so "dam" is deca +
                // metre (scale 10) and also deci + am (scale 0.5). Only da first gives 10.
                let catalogue =
                    UnitCatalogue.add
                        "am"
                        { Factors = [ ("am", 1) ]; Dimension = Dimension.length; Scale = 5.0 }
                        true
                        UnitCatalogue.si

                match UnitCatalogue.tryFind "dam" catalogue with
                | ValueSome u -> Expect.isTrue (within 1e-15 10.0 u.Scale) $"the decametre, got scale {u.Scale}"
                | ValueNone -> failwith "dam"
            }

            test "opening the namespace keeps FSharp.Core's min and max" {
                Expect.equal (min 2 3) 2 "min"
                Expect.equal (max 2.0 3.0) 3.0 "max"
            }
        ]
