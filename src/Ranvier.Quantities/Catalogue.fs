namespace Ranvier.Quantities

open System

/// The metre: FSharp.Core's `UnitSymbols.m`.
[<Measure>]
type m = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m

/// The kilogram: FSharp.Core's `UnitSymbols.kg`.
[<Measure>]
type kg = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.kg

/// The second: FSharp.Core's `UnitSymbols.s`.
[<Measure>]
type s = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.s

/// The kelvin: FSharp.Core's `UnitSymbols.K`.
[<Measure>]
type K = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.K

/// The ampere: FSharp.Core's `UnitSymbols.A`.
[<Measure>]
type A = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.A

/// The mole: FSharp.Core's `UnitSymbols.mol`.
[<Measure>]
type mol = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.mol

/// The candela: FSharp.Core's `UnitSymbols.cd`.
[<Measure>]
type cd = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.cd

/// The hertz: FSharp.Core's `UnitSymbols.Hz`.
[<Measure>]
type Hz = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Hz

/// The newton: FSharp.Core's `UnitSymbols.N`.
[<Measure>]
type N = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.N

/// The pascal: FSharp.Core's `UnitSymbols.Pa`.
[<Measure>]
type Pa = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Pa

/// The joule: FSharp.Core's `UnitSymbols.J`.
[<Measure>]
type J = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.J

/// The watt: FSharp.Core's `UnitSymbols.W`.
[<Measure>]
type W = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.W

/// The coulomb: FSharp.Core's `UnitSymbols.C`.
[<Measure>]
type C = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.C

/// The volt: FSharp.Core's `UnitSymbols.V`.
[<Measure>]
type V = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.V

/// The farad: FSharp.Core's `UnitSymbols.F`.
[<Measure>]
type F = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.F

/// The ohm: FSharp.Core's `UnitSymbols.ohm`.
[<Measure>]
type ohm = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.ohm

/// The siemens: FSharp.Core's `UnitSymbols.S`.
[<Measure>]
type S = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.S

/// The weber: FSharp.Core's `UnitSymbols.Wb`.
[<Measure>]
type Wb = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Wb

/// The tesla: FSharp.Core's `UnitSymbols.T`.
[<Measure>]
type T = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.T

/// The henry: FSharp.Core's `UnitSymbols.H`.
[<Measure>]
type H = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.H

/// The lumen: FSharp.Core's `UnitSymbols.lm`.
[<Measure>]
type lm = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.lm

/// The lux: FSharp.Core's `UnitSymbols.lx`.
[<Measure>]
type lx = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.lx

/// The becquerel: FSharp.Core's `UnitSymbols.Bq`.
[<Measure>]
type Bq = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Bq

/// The gray: FSharp.Core's `UnitSymbols.Gy`.
[<Measure>]
type Gy = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Gy

/// The sievert: FSharp.Core's `UnitSymbols.Sv`.
[<Measure>]
type Sv = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.Sv

/// The katal: FSharp.Core's `UnitSymbols.kat`.
[<Measure>]
type kat = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.kat

/// The radian.
[<Measure>]
type rad

/// The steradian.
[<Measure>]
type sr

/// The kilometre.
[<Measure>]
type km

/// The centimetre.
[<Measure>]
type cm

/// The millimetre.
[<Measure>]
type mm

/// The micrometre, symbol U+00B5 followed by "m".
[<Measure>]
type um

/// The nanometre.
[<Measure>]
type nm

/// The gram.
[<Measure>]
type g

/// The milligram.
[<Measure>]
type mg

/// The millisecond.
[<Measure>]
type ms

/// The microsecond, symbol U+00B5 followed by "s".
[<Measure>]
type us

/// The nanosecond.
[<Measure>]
type ns

/// The millikelvin.
[<Measure>]
type mK

/// The kilopascal.
[<Measure>]
type kPa

/// The megapascal.
[<Measure>]
type MPa

/// The kilojoule.
[<Measure>]
type kJ

/// The kilowatt.
[<Measure>]
type kW

/// The megawatt.
[<Measure>]
type MW

/// The milliampere.
[<Measure>]
type mA

/// The millivolt.
[<Measure>]
type mV

/// The kilovolt.
[<Measure>]
type kV

/// The minute, symbol "min". Named `minute` because a measure type `min` would hide FSharp.Core's `min`.
[<Measure>]
type minute

/// The hour.
[<Measure>]
type h

/// The day.
[<Measure>]
type d

/// The astronomical unit.
[<Measure>]
type au

/// The litre.
[<Measure>]
type L

/// The tonne.
[<Measure>]
type t

/// The electronvolt.
[<Measure>]
type eV

/// The dalton.
[<Measure>]
type Da

/// The degree, symbol U+00B0 (also found as "deg").
[<Measure>]
type deg

/// The Julian year.
[<Measure>]
type a

/// The light-year.
[<Measure>]
type ly

/// The parsec.
[<Measure>]
type pc

/// The bar.
[<Measure>]
type bar

/// The standard atmosphere.
[<Measure>]
type atm

module Units =
    let private dim l m t i th n j : Dimension =
        { Length = l; Mass = m; Time = t; Current = i; Temperature = th; Amount = n; Luminosity = j }

    let private dimPa = dim -1 1 -2 0 0 0 0
    let private dimJ = dim 2 1 -2 0 0 0 0
    let private dimW = dim 2 1 -3 0 0 0 0
    let private dimV = dim 2 1 -3 -1 0 0 0

    let one: UnitOf<1> = UnitOf<1>(UnitInfo.dimensionless)
    let metre: UnitOf<m> = UnitOf.define "m" Dimension.length 1.0
    let kilogram: UnitOf<kg> = UnitOf.define "kg" Dimension.mass 1.0
    let second: UnitOf<s> = UnitOf.define "s" Dimension.time 1.0
    let kelvin: UnitOf<K> = UnitOf.define "K" Dimension.temperature 1.0
    let ampere: UnitOf<A> = UnitOf.define "A" Dimension.current 1.0
    let mole: UnitOf<mol> = UnitOf.define "mol" Dimension.amount 1.0
    let candela: UnitOf<cd> = UnitOf.define "cd" Dimension.luminosity 1.0
    let radian: UnitOf<rad> = UnitOf.define "rad" Dimension.none 1.0
    let steradian: UnitOf<sr> = UnitOf.define "sr" Dimension.none 1.0
    let hertz: UnitOf<Hz> = UnitOf.define "Hz" (dim 0 0 -1 0 0 0 0) 1.0
    let newton: UnitOf<N> = UnitOf.define "N" (dim 1 1 -2 0 0 0 0) 1.0
    let pascal: UnitOf<Pa> = UnitOf.define "Pa" dimPa 1.0
    let joule: UnitOf<J> = UnitOf.define "J" dimJ 1.0
    let watt: UnitOf<W> = UnitOf.define "W" dimW 1.0
    let coulomb: UnitOf<C> = UnitOf.define "C" (dim 0 0 1 1 0 0 0) 1.0
    let volt: UnitOf<V> = UnitOf.define "V" dimV 1.0
    let farad: UnitOf<F> = UnitOf.define "F" (dim -2 -1 4 2 0 0 0) 1.0
    /// The ohm, symbol U+03A9 (Greek capital omega).
    let ohm: UnitOf<ohm> = UnitOf.define "\u03A9" (dim 2 1 -3 -2 0 0 0) 1.0
    let siemens: UnitOf<S> = UnitOf.define "S" (dim -2 -1 3 2 0 0 0) 1.0
    let weber: UnitOf<Wb> = UnitOf.define "Wb" (dim 2 1 -2 -1 0 0 0) 1.0
    let tesla: UnitOf<T> = UnitOf.define "T" (dim 0 1 -2 -1 0 0 0) 1.0
    let henry: UnitOf<H> = UnitOf.define "H" (dim 2 1 -2 -2 0 0 0) 1.0
    let lumen: UnitOf<lm> = UnitOf.define "lm" Dimension.luminosity 1.0
    let lux: UnitOf<lx> = UnitOf.define "lx" (dim -2 0 0 0 0 0 1) 1.0
    let becquerel: UnitOf<Bq> = UnitOf.define "Bq" (dim 0 0 -1 0 0 0 0) 1.0
    let gray: UnitOf<Gy> = UnitOf.define "Gy" (dim 2 0 -2 0 0 0 0) 1.0
    let sievert: UnitOf<Sv> = UnitOf.define "Sv" (dim 2 0 -2 0 0 0 0) 1.0
    let katal: UnitOf<kat> = UnitOf.define "kat" (dim 0 0 -1 0 0 1 0) 1.0
    let kilometre: UnitOf<km> = UnitOf.define "km" Dimension.length 1000.0
    let centimetre: UnitOf<cm> = UnitOf.define "cm" Dimension.length 1e-2
    let millimetre: UnitOf<mm> = UnitOf.define "mm" Dimension.length 1e-3
    /// The micrometre, symbol U+00B5 followed by "m".
    let micrometre: UnitOf<um> = UnitOf.define "\u00B5m" Dimension.length 1e-6
    let nanometre: UnitOf<nm> = UnitOf.define "nm" Dimension.length 1e-9
    let gram: UnitOf<g> = UnitOf.define "g" Dimension.mass 1e-3
    let milligram: UnitOf<mg> = UnitOf.define "mg" Dimension.mass 1e-6
    let millisecond: UnitOf<ms> = UnitOf.define "ms" Dimension.time 1e-3
    /// The microsecond, symbol U+00B5 followed by "s".
    let microsecond: UnitOf<us> = UnitOf.define "\u00B5s" Dimension.time 1e-6
    let nanosecond: UnitOf<ns> = UnitOf.define "ns" Dimension.time 1e-9
    let millikelvin: UnitOf<mK> = UnitOf.define "mK" Dimension.temperature 0.001
    let kilopascal: UnitOf<kPa> = UnitOf.define "kPa" dimPa 1e3
    let megapascal: UnitOf<MPa> = UnitOf.define "MPa" dimPa 1e6
    let kilojoule: UnitOf<kJ> = UnitOf.define "kJ" dimJ 1e3
    let kilowatt: UnitOf<kW> = UnitOf.define "kW" dimW 1e3
    let megawatt: UnitOf<MW> = UnitOf.define "MW" dimW 1e6
    let milliampere: UnitOf<mA> = UnitOf.define "mA" Dimension.current 1e-3
    let millivolt: UnitOf<mV> = UnitOf.define "mV" dimV 1e-3
    let kilovolt: UnitOf<kV> = UnitOf.define "kV" dimV 1e3
    /// The minute, symbol "min": 60 s (from documentation: SI Brochure, 9th edition, 2019, Table 8).
    let minute: UnitOf<minute> = UnitOf.define "min" Dimension.time 60.0
    /// The hour: 3600 s (from documentation: SI Brochure, 9th edition, 2019, Table 8).
    let hour: UnitOf<h> = UnitOf.define "h" Dimension.time 3600.0
    /// The day: 86 400 s (from documentation: SI Brochure, 9th edition, 2019, Table 8).
    let day: UnitOf<d> = UnitOf.define "d" Dimension.time 86400.0
    /// The astronomical unit: exactly 149 597 870 700 m (from documentation: IAU 2012 Resolution B2, XXVIII General Assembly, Beijing, August 2012; SI Brochure, 9th edition, 2019, Table 8). The IAU PDF could not be fetched when this was written: the value was read from a page that quotes the resolution, https://en.wikipedia.org/wiki/Astronomical_unit, accessed 2026-11-20.
    let astronomicalUnit: UnitOf<au> = UnitOf.define "au" Dimension.length 149597870700.0
    /// The litre: 10^-3 m^3 (from documentation: SI Brochure, 9th edition, 2019, Table 8).
    let litre: UnitOf<L> = UnitOf.define "L" (dim 3 0 0 0 0 0 0) 1e-3
    /// The tonne: 1000 kg (from documentation: SI Brochure, 9th edition, 2019, Table 8).
    let tonne: UnitOf<t> = UnitOf.define "t" Dimension.mass 1e3
    /// The electronvolt: e times 1 V, with e = 1.602 176 634e-19 C exactly (from documentation: CODATA 2022, https://physics.nist.gov/cgi-bin/cuu/Value?e, accessed 2026-11-20; SI Brochure, 9th edition, 2019, Table 8).
    let electronvolt: UnitOf<eV> = UnitOf.define "eV" dimJ 1.602176634e-19
    /// The dalton: the atomic mass constant m_u, 1.660 539 068 92e-27 kg with standard uncertainty 5.2e-37 kg, so the scale is not exact (from documentation: CODATA 2022, https://physics.nist.gov/cgi-bin/cuu/Value?u, accessed 2026-11-20; the dalton is listed in SI Brochure, 9th edition, 2019, Table 8).
    let dalton: UnitOf<Da> = UnitOf.define "Da" Dimension.mass 1.66053906892e-27
    /// The degree, symbol U+00B0: pi/180 rad (from documentation: SI Brochure, 9th edition, 2019, Table 8). `UnitCatalogue.si` also finds it as "deg".
    let degree: UnitOf<deg> = UnitOf.define "\u00B0" Dimension.none (Math.PI / 180.0)
    /// The Julian year: 365.25 days of 86 400 s, 31 557 600 s (general knowledge: the IAU convention; the IAU text could not be fetched).
    let julianYear: UnitOf<a> = UnitOf.define "a" Dimension.time 31557600.0
    /// The light-year: the distance light travels in one Julian year, c = 299 792 458 m/s exactly (from documentation: CODATA 2022, https://physics.nist.gov/cgi-bin/cuu/Value?c, accessed 2026-11-20) times 31 557 600 s. The IAU definition of the light-year could not be fetched; that definition is general knowledge.
    let lightYear: UnitOf<ly> = UnitOf.define "ly" Dimension.length (299792458.0 * 31557600.0)
    /// The parsec: exactly 648 000/pi au (from documentation: IAU 2015 Resolution B2, Honolulu, August 2015; the IAU PDF could not be fetched, the formula was read from https://en.wikipedia.org/wiki/Parsec, accessed 2026-11-20).
    let parsec: UnitOf<pc> = UnitOf.define "pc" Dimension.length (648000.0 / Math.PI * 149597870700.0)
    /// The bar: 10^5 Pa (general knowledge: not checked against a source).
    let bar: UnitOf<bar> = UnitOf.define "bar" dimPa 1e5
    /// The standard atmosphere: 101 325 Pa exactly (general knowledge: the 10th CGPM, 1954; not checked against a source).
    let atmosphere: UnitOf<atm> = UnitOf.define "atm" dimPa 101325.0

/// The catalogues of units.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UnitCatalogue =
    let private forbidden = " \u00B7*./()^[],-\u2212\u00D7\u00F7\u207B\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079"

    /// The catalogue with `unit` registered under `symbol`, prefixable or not. Raises ArgumentException for a null
    /// or empty symbol, one the catalogue already holds, one containing a space, any of `\u00B7*./()^[],-\u2212\u00D7\u00F7`,
    /// an ASCII digit, a superscript digit or `\u207B`, and for `prefixable` on a unit that is not one factor with
    /// exponent 1; raises ArgumentOutOfRangeException unless the unit's scale is positive and finite. The look-alikes
    /// U+03BC and U+2126 are replaced by U+00B5 and U+03A9 before the symbol is checked and stored. The catalogue
    /// given is not changed.
    let add (symbol: string) (unit: UnitInfo) (prefixable: bool) (catalogue: UnitCatalogue) : UnitCatalogue =
        if String.IsNullOrEmpty symbol then
            raise (ArgumentException("A unit's symbol must have at least one character.", nameof symbol))

        let symbol = Prefix.normalise symbol

        if symbol |> Seq.exists (fun c -> forbidden.IndexOf c >= 0 || (c >= '0' && c <= '9')) then
            raise (
                ArgumentException(
                    "The symbol " + symbol + " contains a space, an operator, a bracket or a digit.",
                    nameof symbol
                )
            )

        if Map.containsKey symbol catalogue.Units then
            raise (ArgumentException("The catalogue already holds " + symbol + ".", nameof symbol))

        if not (unit.Scale > 0.0) || Double.IsInfinity unit.Scale then
            raise (ArgumentOutOfRangeException(nameof unit, "A unit's scale must be positive and finite."))

        let oneFactor =
            match unit.Factors with
            | [ (_, 1) ] -> true
            | _ -> false

        if prefixable && not oneFactor then
            raise (ArgumentException("Only a unit with one factor of exponent 1 can take prefixes.", nameof prefixable))

        UnitCatalogue(Map.add symbol (struct (unit, prefixable)) catalogue.Units)

    /// The unit registered under the symbol, or that of a prefix and a prefixable symbol ("da" tried before "d");
    /// a prefixed unit's factor is the prefix's symbol followed by the unit's. ValueNone when there is none.
    let tryFind (symbol: string) (catalogue: UnitCatalogue) : UnitInfo voption = catalogue.TryFind symbol

    /// The SI units: base units, derived units with special names, common prefixed units, units accepted for use
    /// with the SI, and a few others (every `Units` value but `one`), with the symbol "deg" for the degree beside
    /// "\u00B0". Prefixable: m, s, K, A, mol, cd, rad, sr, Hz, N, Pa, J, W, C, V, F, \u03A9, S, Wb, T, H, lm, lx, Bq,
    /// Gy, Sv, kat, g, L, t, eV and Da; not kg, and none of the other prefixed or non-SI units. Each unit's scale is
    /// documented on its `Units` value.
    let si: UnitCatalogue =
        let entries: (string * UnitInfo * bool) list =
            [
                "m", Units.metre.Info, true
                "kg", Units.kilogram.Info, false
                "s", Units.second.Info, true
                "K", Units.kelvin.Info, true
                "A", Units.ampere.Info, true
                "mol", Units.mole.Info, true
                "cd", Units.candela.Info, true
                "rad", Units.radian.Info, true
                "sr", Units.steradian.Info, true
                "Hz", Units.hertz.Info, true
                "N", Units.newton.Info, true
                "Pa", Units.pascal.Info, true
                "J", Units.joule.Info, true
                "W", Units.watt.Info, true
                "C", Units.coulomb.Info, true
                "V", Units.volt.Info, true
                "F", Units.farad.Info, true
                "\u03A9", Units.ohm.Info, true
                "S", Units.siemens.Info, true
                "Wb", Units.weber.Info, true
                "T", Units.tesla.Info, true
                "H", Units.henry.Info, true
                "lm", Units.lumen.Info, true
                "lx", Units.lux.Info, true
                "Bq", Units.becquerel.Info, true
                "Gy", Units.gray.Info, true
                "Sv", Units.sievert.Info, true
                "kat", Units.katal.Info, true
                "km", Units.kilometre.Info, false
                "cm", Units.centimetre.Info, false
                "mm", Units.millimetre.Info, false
                "\u00B5m", Units.micrometre.Info, false
                "nm", Units.nanometre.Info, false
                "g", Units.gram.Info, true
                "mg", Units.milligram.Info, false
                "ms", Units.millisecond.Info, false
                "\u00B5s", Units.microsecond.Info, false
                "ns", Units.nanosecond.Info, false
                "mK", Units.millikelvin.Info, false
                "kPa", Units.kilopascal.Info, false
                "MPa", Units.megapascal.Info, false
                "kJ", Units.kilojoule.Info, false
                "kW", Units.kilowatt.Info, false
                "MW", Units.megawatt.Info, false
                "mA", Units.milliampere.Info, false
                "mV", Units.millivolt.Info, false
                "kV", Units.kilovolt.Info, false
                "min", Units.minute.Info, false
                "h", Units.hour.Info, false
                "d", Units.day.Info, false
                "au", Units.astronomicalUnit.Info, false
                "L", Units.litre.Info, true
                "t", Units.tonne.Info, true
                "eV", Units.electronvolt.Info, true
                "Da", Units.dalton.Info, true
                "\u00B0", Units.degree.Info, false
                "a", Units.julianYear.Info, false
                "ly", Units.lightYear.Info, false
                "pc", Units.parsec.Info, false
                "bar", Units.bar.Info, false
                "atm", Units.atmosphere.Info, false
                "deg", { Units.degree.Info with Factors = [ ("deg", 1) ] }, false
            ]

        entries
        |> List.fold
            (fun catalogue (symbol, unit, prefixable) -> add symbol unit prefixable catalogue)
            (UnitCatalogue Map.empty)
