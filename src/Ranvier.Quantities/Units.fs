namespace Ranvier.Quantities

open System

/// Integer exponents over the seven SI base dimensions.
[<Struct>]
type Dimension =
    {
        Length: int
        Mass: int
        Time: int
        Current: int
        Temperature: int
        Amount: int
        Luminosity: int
    }

/// A unit at run time.
type UnitInfo =
    {
        /// Named factors with their exponents, in the order written: [ "km", 1; "s", -1 ].
        Factors: (string * int) list
        Dimension: Dimension
        /// The size of one of this unit in SI base units.
        Scale: float
    }

/// An SI prefix: its symbol and the factor it multiplies a unit's scale by.
type Prefix = { Symbol: string; Factor: float }

module Dimension =
    let none: Dimension =
        {
            Length = 0
            Mass = 0
            Time = 0
            Current = 0
            Temperature = 0
            Amount = 0
            Luminosity = 0
        }

    let length = { none with Length = 1 }
    let mass = { none with Mass = 1 }
    let time = { none with Time = 1 }
    let current = { none with Current = 1 }
    let temperature = { none with Temperature = 1 }
    let amount = { none with Amount = 1 }
    let luminosity = { none with Luminosity = 1 }

    let internal map2 (f: int -> int -> int) (a: Dimension) (b: Dimension) : Dimension =
        {
            Length = f a.Length b.Length
            Mass = f a.Mass b.Mass
            Time = f a.Time b.Time
            Current = f a.Current b.Current
            Temperature = f a.Temperature b.Temperature
            Amount = f a.Amount b.Amount
            Luminosity = f a.Luminosity b.Luminosity
        }

    let internal map (f: int -> int) (d: Dimension) : Dimension = map2 (fun x _ -> f x) d d

    let internal exponents (d: Dimension) =
        [ d.Length; d.Mass; d.Time; d.Current; d.Temperature; d.Amount; d.Luminosity ]

/// The 24 SI prefixes: general knowledge, definitions set by the SI Brochure, 9th edition, Table 7, and, for quecto,
/// ronto, ronna and quetta, by the 27th CGPM (2022) Resolution 3; not read here.
module Prefix =
    let quecto: Prefix = { Symbol = "q"; Factor = 1e-30 }
    let ronto: Prefix = { Symbol = "r"; Factor = 1e-27 }
    let yocto: Prefix = { Symbol = "y"; Factor = 1e-24 }
    let zepto: Prefix = { Symbol = "z"; Factor = 1e-21 }
    let atto: Prefix = { Symbol = "a"; Factor = 1e-18 }
    let femto: Prefix = { Symbol = "f"; Factor = 1e-15 }
    let pico: Prefix = { Symbol = "p"; Factor = 1e-12 }
    let nano: Prefix = { Symbol = "n"; Factor = 1e-9 }
    /// Symbol U+00B5 (micro sign).
    let micro: Prefix = { Symbol = "\u00B5"; Factor = 1e-6 }
    let milli: Prefix = { Symbol = "m"; Factor = 1e-3 }
    let centi: Prefix = { Symbol = "c"; Factor = 1e-2 }
    let deci: Prefix = { Symbol = "d"; Factor = 1e-1 }
    let deca: Prefix = { Symbol = "da"; Factor = 1e1 }
    let hecto: Prefix = { Symbol = "h"; Factor = 1e2 }
    let kilo: Prefix = { Symbol = "k"; Factor = 1e3 }
    let mega: Prefix = { Symbol = "M"; Factor = 1e6 }
    let giga: Prefix = { Symbol = "G"; Factor = 1e9 }
    let tera: Prefix = { Symbol = "T"; Factor = 1e12 }
    let peta: Prefix = { Symbol = "P"; Factor = 1e15 }
    let exa: Prefix = { Symbol = "E"; Factor = 1e18 }
    let zetta: Prefix = { Symbol = "Z"; Factor = 1e21 }
    let yotta: Prefix = { Symbol = "Y"; Factor = 1e24 }
    let ronna: Prefix = { Symbol = "R"; Factor = 1e27 }
    let quetta: Prefix = { Symbol = "Q"; Factor = 1e30 }

    /// Every prefix, "da" first so that it is tried before "d".
    let internal all: Prefix list =
        [
            deca; quecto; ronto; yocto; zepto; atto; femto; pico; nano; micro; milli; centi; deci
            hecto; kilo; mega; giga; tera; peta; exa; zetta; yotta; ronna; quetta
        ]

    /// The unit with one factor named `p.Symbol + name` and scale `p.Factor * u.Scale`; ValueNone unless `u` is one
    /// factor with exponent 1 or when that scale is not positive and finite.
    let internal tryApply (p: Prefix) (u: UnitInfo) : UnitInfo voption =
        match u.Factors with
        | [ (name, 1) ] ->
            let scale = p.Factor * u.Scale

            if scale > 0.0 && not (Double.IsInfinity scale) then
                ValueSome { Factors = [ (p.Symbol + name, 1) ]; Dimension = u.Dimension; Scale = scale }
            else
                ValueNone
        | _ -> ValueNone

    /// Replaces the Greek mu (U+03BC) by the micro sign (U+00B5) and the ohm sign (U+2126) by the Greek capital
    /// omega (U+03A9), the forms the catalogue uses.
    let internal normalise (symbol: string) : string =
        symbol.Replace("\u03BC", "\u00B5").Replace("\u2126", "\u03A9")

/// An immutable set of named units, each marked prefixable or not. Build one with `UnitCatalogue.add`, starting from
/// `UnitCatalogue.si`.
[<Sealed>]
type UnitCatalogue internal (units: Map<string, struct (UnitInfo * bool)>) =
    member internal _.Units = units

    /// The unit registered under the exact symbol, else a prefix followed by a prefixable symbol ("da" before "d"),
    /// whose factor is the prefix's symbol followed by the unit's and whose scale is the prefix's factor times the
    /// unit's. The look-alikes U+03BC and U+2126 are read as U+00B5 and U+03A9 first.
    member internal _.TryFind(symbol: string) : UnitInfo voption =
        if isNull symbol then
            ValueNone
        else
            let symbol = Prefix.normalise symbol

            match Map.tryFind symbol units with
            | Some(struct (info, _)) -> ValueSome info
            | None ->
                let mutable found = ValueNone

                for p in Prefix.all do
                    let n = p.Symbol.Length

                    if found.IsNone && symbol.Length > n && symbol.Substring(0, n) = p.Symbol then
                        match Map.tryFind (symbol.Substring n) units with
                        | Some(struct (info, true)) -> found <- Prefix.tryApply p info
                        | _ -> ()

                found

module UnitInfo =
    let dimensionless: UnitInfo = { Factors = []; Dimension = Dimension.none; Scale = 1.0 }

    /// Merges factors of the same name by adding exponents, in order of first appearance, and drops zero exponents.
    let private merge (factors: (string * int) list) : (string * int) list =
        let names = factors |> List.map fst |> List.distinct

        names
        |> List.map (fun name -> name, factors |> List.sumBy (fun (n, e) -> if n = name then e else 0))
        |> List.filter (fun (_, e) -> e <> 0)

    /// `x` to the integer power `n`, by repeated multiplication so both targets round alike.
    let private scalePower (x: float) (n: int) : float =
        let mutable product = 1.0

        for _ in 1 .. abs n do
            product <- product * x

        if n < 0 then 1.0 / product else product

    let private format (name: string, e: int) = if e = 1 then name else name + "^" + string e

    /// The numerator factors joined by `·`, then `/` and the denominator factors with positive exponents:
    /// "km/s", "mK^2", "kg·m/s^2". A unit with no numerator factor has numerator "1" ("1/s"); several
    /// denominator factors are parenthesised ("kg/(m·s^2)", "1/(m·s)"). "" for a unit with no factors; a
    /// dimensionless unit with factors keeps them ("km/m").
    let symbol (u: UnitInfo) : string =
        let numerator = u.Factors |> List.filter (fun (_, e) -> e > 0) |> List.map format

        let denominator =
            u.Factors |> List.filter (fun (_, e) -> e < 0) |> List.map (fun (n, e) -> format (n, -e))

        let top = String.Join("·", numerator)

        match numerator, denominator with
        | _, [] -> top
        | [], _ :: _ -> "1/" + (if denominator.Length = 1 then denominator.Head else "(" + String.Join("·", denominator) + ")")
        | _, [ single ] -> top + "/" + single
        | _, _ -> top + "/(" + String.Join("·", denominator) + ")"

    /// The unit itself when its scale is finite and not 0. Raises ArithmeticException otherwise, with a message
    /// stating the unit's symbol ("1" for a unit without factors) and its scale.
    let private checkedScale (u: UnitInfo) : UnitInfo =
        let scale = u.Scale

        if Double.IsNaN scale || Double.IsInfinity scale || scale = 0.0 then
            let name =
                match symbol u with
                | "" -> "1"
                | text -> text

            let value =
                if Double.IsNaN scale then "NaN"
                elif scale = 0.0 then "0"
                elif scale > 0.0 then "∞"
                else "−∞"

            let message = "The scale of " + name + " is " + value + "; a unit's scale must be positive and finite."
            raise (ArithmeticException message)

        u

    /// The product of two units. Raises ArithmeticException when the product of the scales is not finite or is 0;
    /// the message states the product's symbol.
    let multiply (a: UnitInfo) (b: UnitInfo) : UnitInfo =
        checkedScale
            {
                Factors = merge (a.Factors @ b.Factors)
                Dimension = Dimension.map2 (+) a.Dimension b.Dimension
                Scale = a.Scale * b.Scale
            }

    /// The quotient of two units. Raises ArithmeticException when the quotient of the scales is not finite or is 0;
    /// the message states the quotient's symbol.
    let divide (a: UnitInfo) (b: UnitInfo) : UnitInfo =
        checkedScale
            {
                Factors = merge (a.Factors @ (b.Factors |> List.map (fun (n, e) -> n, -e)))
                Dimension = Dimension.map2 (-) a.Dimension b.Dimension
                Scale = a.Scale / b.Scale
            }

    /// `u` to the power `n`; any integer, and `power u 0` is dimensionless with scale 1. The scale is the
    /// floating-point product of `n` factors (its reciprocal for negative `n`), O(|n|). Raises ArithmeticException
    /// when that scale is not finite or is 0; the message states the result's symbol, as "km^103".
    let power (u: UnitInfo) (n: int) : UnitInfo =
        checkedScale
            {
                Factors = merge (u.Factors |> List.map (fun (name, e) -> name, e * n))
                Dimension = Dimension.map (fun e -> e * n) u.Dimension
                Scale = scalePower u.Scale n
            }

    /// `Ok` with every exponent halved and the correctly rounded square root of the scale. Error names the unit when
    /// an exponent is odd. Raises ArithmeticException when every exponent is even and the root of the scale is not
    /// finite or is 0; the message states the root's symbol. The root of a unit reached through `power`, `divide` or
    /// `multiply` can differ in the last place from the same unit reached by another route: `sqrt (power u -2)` and
    /// `power u -1` for a scale of 0.7; `sameUnit` joins the two.
    let sqrt (u: UnitInfo) : Result<UnitInfo, string> =
        let odd e = e % 2 <> 0

        if u.Factors |> List.exists (fun (_, e) -> odd e) || Dimension.exponents u.Dimension |> List.exists odd then
            Error("The square root of " + symbol u + " needs every exponent to be even.")
        else
            Ok(
                checkedScale
                    {
                        Factors = u.Factors |> List.map (fun (n, e) -> n, e / 2)
                        Dimension = Dimension.map (fun e -> e / 2) u.Dimension
                        Scale = Math.Sqrt u.Scale
                    }
            )

    let sameDimension (a: UnitInfo) (b: UnitInfo) : bool = a.Dimension = b.Dimension

    /// True when the dimensions are equal and |a.Scale − b.Scale| ≤ 1e-12 · max(|a.Scale|, |b.Scale|); symmetric. Two
    /// routes to one unit through `power`, `divide`, `multiply` or `sqrt` whose scales differ in the last place are
    /// one unit.
    let sameUnit (a: UnitInfo) (b: UnitInfo) : bool =
        sameDimension a b && abs (a.Scale - b.Scale) <= 1e-12 * max (abs a.Scale) (abs b.Scale)

    /// `u` with its one factor prefixed: the factor's name gains the prefix's symbol and the scale is multiplied by
    /// the prefix's factor, so `prefixed Prefix.kilo` of the metre is "km" of scale 1000. Raises ArgumentException
    /// unless `u` has exactly one factor with exponent 1, and ArithmeticException when the new scale is not finite or
    /// is 0; the message states the symbol.
    let prefixed (prefix: Prefix) (u: UnitInfo) : UnitInfo =
        match u.Factors with
        | [ (_, 1) ] ->
            match Prefix.tryApply prefix u with
            | ValueSome result -> result
            | ValueNone ->
                let name = prefix.Symbol + fst u.Factors.Head
                let value = if prefix.Factor * u.Scale = 0.0 then "0" else "\u221E"

                raise (
                    ArithmeticException("The scale of " + name + " is " + value + "; a unit's scale must be positive and finite.")
                )
        | _ ->
            raise (
                ArgumentException(
                    "Only a unit with one factor of exponent 1 takes a prefix, not " + symbol u + ".",
                    nameof u
                )
            )

/// A unit of measure with the description its values carry at run time.
[<Sealed>]
type UnitOf<[<Measure>] 'u> internal (info: UnitInfo) =
    member _.Info: UnitInfo = info

    /// The product, as `UnitInfo.multiply`. Raises ArithmeticException when the product's scale is not finite or is 0.
    static member (*)(a: UnitOf<'u>, b: UnitOf<'v>) : UnitOf<'u 'v> =
        UnitOf<'u 'v>(UnitInfo.multiply a.Info b.Info)

    /// The quotient, as `UnitInfo.divide`. Raises ArithmeticException when the quotient's scale is not finite or is 0.
    static member (/)(a: UnitOf<'u>, b: UnitOf<'v>) : UnitOf<'u / 'v> =
        UnitOf<'u / 'v>(UnitInfo.divide a.Info b.Info)

module UnitOf =
    /// A unit of one factor, `symbol`. The caller's annotation fixes 'u. Factors are identified by their symbol, so
    /// two units defined with one symbol are one factor in a product or quotient. Raises ArgumentException for a null
    /// or empty symbol, and ArgumentOutOfRangeException unless scale is positive and finite.
    let define<[<Measure>] 'u> (symbol: string) (dimension: Dimension) (scale: float) : UnitOf<'u> =
        if String.IsNullOrEmpty symbol then
            raise (ArgumentException("A unit's symbol must have at least one character.", nameof symbol))

        if not (scale > 0.0) || Double.IsInfinity scale then
            raise (ArgumentOutOfRangeException(nameof scale, "A unit's scale must be positive and finite."))

        UnitOf<'u>({ Factors = [ (symbol, 1) ]; Dimension = dimension; Scale = scale })

    /// The square, as `UnitInfo.power` with exponent 2. Raises ArithmeticException when the square's scale is not
    /// finite or is 0.
    let squared (u: UnitOf<'u>) : UnitOf<'u ^ 2> = UnitOf<'u ^ 2>(UnitInfo.power u.Info 2)

    /// The unit with a prefix, as `UnitInfo.prefixed`. The caller's annotation fixes 'v. Raises ArgumentException
    /// unless `u` has one factor with exponent 1, and ArithmeticException when the new scale is not finite or is 0.
    let prefixed<[<Measure>] 'u, [<Measure>] 'v> (prefix: Prefix) (u: UnitOf<'u>) : UnitOf<'v> =
        UnitOf<'v>(UnitInfo.prefixed prefix u.Info)
