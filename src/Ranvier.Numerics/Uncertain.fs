namespace Ranvier.Numerics

open System

/// Ascending source ids with their contributions, as `Uncertain` holds them.
module internal UncertainSources =
    /// Each contribution times `d`.
    let scale (d: float) (contributions: float[]) : float[] =
        Array.map (fun c -> d * c) contributions

    /// The union of two ascending id arrays, each id with `dx * ca + dy * cb`, where an id absent from one side
    /// contributes 0 from that side. One pass over both; O(m + n).
    let merge (dx: float) (sa: int[]) (ca: float[]) (dy: float) (sb: int[]) (cb: float[]) : struct (int[] * float[]) =
        if sb.Length = 0 then
            struct (sa, scale dx ca)
        elif sa.Length = 0 then
            struct (sb, scale dy cb)
        elif obj.ReferenceEquals (sa, sb) then
            struct (sa, Array.init sa.Length (fun i -> dx * ca[i] + dy * cb[i]))
        else
            let n = sa.Length + sb.Length
            let ids: int[] = Array.zeroCreate n
            let cs: float[] = Array.zeroCreate n
            let mutable i = 0
            let mutable j = 0
            let mutable k = 0

            while i < sa.Length || j < sb.Length do
                if j = sb.Length || (i < sa.Length && sa[i] < sb[j]) then
                    ids[k] <- sa[i]
                    cs[k] <- dx * ca[i]
                    i <- i + 1
                elif i = sa.Length || sb[j] < sa[i] then
                    ids[k] <- sb[j]
                    cs[k] <- dy * cb[j]
                    j <- j + 1
                else
                    ids[k] <- sa[i]
                    cs[k] <- dx * ca[i] + dy * cb[j]
                    i <- i + 1
                    j <- j + 1

                k <- k + 1

            if k = n then
                struct (ids, cs)
            else
                struct (Array.sub ids 0 k, Array.sub cs 0 k)

/// A value with its first-order contributions by independent source: per source i, the signed contribution
/// ∂f/∂xᵢ · uᵢ. The combined standard uncertainty is the root of the sum of their squares, and two values that hold a
/// common source are correlated through it (the GUM's law of propagation for uncorrelated inputs, extended through
/// shared sources; general knowledge). The propagation is exact for a function linear in its sources and a first-order
/// approximation otherwise.
///
/// Equality is structural: two values are equal when their values are equal and they hold the same contributions from
/// the same sources. Values from separate calls of `Uncertain.source` differ.
///
/// The operators `+ - * /` combine two values, or a value and a float on either side. Each function member applies the
/// float function to the value and multiplies every contribution by the derivative at the value, so F#'s `sqrt`,
/// `exp`, `log`, `log10`, `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `sinh`, `cosh`, `tanh`, `abs` and `**` apply.
/// An operation on two values costs O(m + n) in their numbers of sources; one on a single value costs at most O(n).
[<NoComparison>]
type Uncertain =
    private
        {
            Value: float
            Sources: int[]
            Contributions: float[]
        }

    /// `value`, with each contribution of `u` times `d`.
    static member internal Lift(value: float, d: float, u: Uncertain) : Uncertain =
        {
            Value = value
            Sources = u.Sources
            Contributions = UncertainSources.scale d u.Contributions
        }

    /// `value`, with the contributions of `a` times `dx` and those of `b` times `dy`, summed per source.
    static member internal Combine(value: float, dx: float, a: Uncertain, dy: float, b: Uncertain) : Uncertain =
        let struct (sources, contributions) =
            UncertainSources.merge dx a.Sources a.Contributions dy b.Sources b.Contributions

        {
            Value = value
            Sources = sources
            Contributions = contributions
        }

    static member (+)(a: Uncertain, b: Uncertain) : Uncertain =
        Uncertain.Combine(a.Value + b.Value, 1.0, a, 1.0, b)

    static member (+)(a: Uncertain, b: float) : Uncertain = { a with Value = a.Value + b }

    static member (+)(a: float, b: Uncertain) : Uncertain = { b with Value = a + b.Value }

    static member (-)(a: Uncertain, b: Uncertain) : Uncertain =
        Uncertain.Combine(a.Value - b.Value, 1.0, a, -1.0, b)

    static member (-)(a: Uncertain, b: float) : Uncertain = { a with Value = a.Value - b }

    static member (-)(a: float, b: Uncertain) : Uncertain = Uncertain.Lift(a - b.Value, -1.0, b)

    static member (*)(a: Uncertain, b: Uncertain) : Uncertain =
        Uncertain.Combine(a.Value * b.Value, b.Value, a, a.Value, b)

    static member (*)(a: Uncertain, b: float) : Uncertain = Uncertain.Lift(a.Value * b, b, a)

    static member (*)(a: float, b: Uncertain) : Uncertain = Uncertain.Lift(a * b.Value, a, b)

    /// The quotient; derivatives 1/y in the dividend and −x/y² in the divisor.
    static member (/)(a: Uncertain, b: Uncertain) : Uncertain =
        let q = a.Value / b.Value
        Uncertain.Combine(q, 1.0 / b.Value, a, -q / b.Value, b)

    /// The quotient by a float; each contribution is divided by it.
    static member (/)(a: Uncertain, b: float) : Uncertain =
        {
            Value = a.Value / b
            Sources = a.Sources
            Contributions = Array.map (fun c -> c / b) a.Contributions
        }

    /// A float over the value; derivative −x/y².
    static member (/)(a: float, b: Uncertain) : Uncertain =
        let q = a / b.Value
        Uncertain.Lift(q, -q / b.Value, b)

    static member (~-)(a: Uncertain) : Uncertain = Uncertain.Lift(-a.Value, -1.0, a)

    /// √x; derivative 1/(2√x).
    static member Sqrt(a: Uncertain) : Uncertain =
        let v = sqrt a.Value
        Uncertain.Lift(v, 0.5 / v, a)

    /// eˣ; derivative eˣ.
    static member Exp(a: Uncertain) : Uncertain =
        let v = exp a.Value
        Uncertain.Lift(v, v, a)

    /// ln x; derivative 1/x.
    static member Log(a: Uncertain) : Uncertain = Uncertain.Lift(log a.Value, 1.0 / a.Value, a)

    /// log₁₀ x; derivative 1/(x ln 10).
    static member Log10(a: Uncertain) : Uncertain =
        Uncertain.Lift(log10 a.Value, 1.0 / (a.Value * log 10.0), a)

    /// sin x; derivative cos x.
    static member Sin(a: Uncertain) : Uncertain = Uncertain.Lift(sin a.Value, cos a.Value, a)

    /// cos x; derivative −sin x.
    static member Cos(a: Uncertain) : Uncertain = Uncertain.Lift(cos a.Value, -(sin a.Value), a)

    /// tan x; derivative 1 + tan² x.
    static member Tan(a: Uncertain) : Uncertain =
        let t = tan a.Value
        Uncertain.Lift(t, 1.0 + t * t, a)

    /// asin x; derivative 1/√(1 − x²), evaluated as 1/√((1 − x)(1 + x)).
    static member Asin(a: Uncertain) : Uncertain =
        let x = a.Value
        Uncertain.Lift(asin x, 1.0 / sqrt ((1.0 - x) * (1.0 + x)), a)

    /// acos x; derivative −1/√(1 − x²), evaluated as −1/√((1 − x)(1 + x)).
    static member Acos(a: Uncertain) : Uncertain =
        let x = a.Value
        Uncertain.Lift(acos x, -1.0 / sqrt ((1.0 - x) * (1.0 + x)), a)

    /// atan x; derivative 1/(1 + x²).
    static member Atan(a: Uncertain) : Uncertain =
        let x = a.Value
        Uncertain.Lift(atan x, 1.0 / (1.0 + x * x), a)

    /// sinh x; derivative cosh x.
    static member Sinh(a: Uncertain) : Uncertain = Uncertain.Lift(sinh a.Value, cosh a.Value, a)

    /// cosh x; derivative sinh x.
    static member Cosh(a: Uncertain) : Uncertain = Uncertain.Lift(cosh a.Value, sinh a.Value, a)

    /// tanh x; derivative 1 − tanh² x, evaluated as 1/cosh² x.
    static member Tanh(a: Uncertain) : Uncertain =
        let c = cosh a.Value
        Uncertain.Lift(tanh a.Value, 1.0 / (c * c), a)

    /// |x|; derivative the sign of x: +1 at 0 and −0, NaN at NaN. First-order propagation does not hold at 0, where
    /// the contribution keeps its magnitude. Under Fable, F#'s `abs` on an `Uncertain` compiles to `Math.abs` and
    /// returns NaN (Fable's `Replacements.fs`, case "Abs", sends a non-numeric argument to `Math.abs`), so code meant
    /// for both targets uses `Uncertain.abs`.
    static member Abs(a: Uncertain) : Uncertain =
        let x = a.Value
        let sign = if x < 0.0 then -1.0 elif x >= 0.0 then 1.0 else nan
        Uncertain.Lift(abs x, sign, a)

    /// x ** y for a float exponent; derivative y·x^(y−1).
    static member Pow(a: Uncertain, y: float) : Uncertain =
        Uncertain.Lift(a.Value ** y, y * a.Value ** (y - 1.0), a)

    /// x ** y for an uncertain exponent; derivatives y·x^(y−1) in the base and x^y·ln x in the exponent. The
    /// exponent's contributions are NaN for a base of 0 or less.
    static member Pow(a: Uncertain, b: Uncertain) : Uncertain =
        let x = a.Value
        let y = b.Value
        let v = x ** y
        Uncertain.Combine(v, y * x ** (y - 1.0), a, v * log x, b)

module Uncertain =
#if FABLE_COMPILER
    let private lastSource = ref 0
#else
    // 64-bit so that calls after exhaustion never wrap the counter back to issuable ids.
    let private lastSource = ref 0L
#endif

    let private exhausted () : 'a =
        raise (InvalidOperationException "The source counter is exhausted: every id up to 2^31 - 2 has been issued.")

    let private nextSource () : int =
#if FABLE_COMPILER
        if lastSource.Value >= Int32.MaxValue - 1 then
            exhausted ()

        lastSource.Value <- lastSource.Value + 1
        lastSource.Value
#else
        let id = Threading.Interlocked.Increment &lastSource.contents

        if id >= int64 Int32.MaxValue then
            exhausted ()

        int id
#endif

    /// A value with standard uncertainty 0 and an empty list of contributions.
    let exact (x: float) : Uncertain =
        {
            Value = x
            Sources = [||]
            Contributions = [||]
        }

    /// The value with a new source, independent of every other, whose one contribution is the standard uncertainty.
    /// Source ids ascend from 1 in the order of creation; on .NET, calls from several threads get distinct ids.
    /// Raises `ArgumentOutOfRangeException` for a negative standard uncertainty. NaN and +∞ are accepted and propagate.
    /// Ids run from 1 to 2³¹ − 2 per process: every call after the last of them raises `InvalidOperationException`
    /// (the source counter is exhausted).
    let source (standardUncertainty: float) (value: float) : Uncertain =
        if standardUncertainty < 0.0 then
            raise (
                ArgumentOutOfRangeException(
                    "standardUncertainty",
                    "A standard uncertainty must be zero or more, or NaN."
                )
            )

        {
            Value = value
            Sources = [| nextSource () |]
            Contributions = [| standardUncertainty |]
        }

    let value (u: Uncertain) : float = u.Value

    /// The root of the sum of the squared contributions, summed in id order: 0 for an exact value, NaN when a
    /// contribution is NaN. It overflows to +∞ when a contribution exceeds about 1e154 in magnitude (general
    /// knowledge). O(n) in the number of sources.
    let standardUncertainty (u: Uncertain) : float =
        let c = u.Contributions
        let mutable s = 0.0

        for i in 0 .. c.Length - 1 do
            s <- s + c[i] * c[i]

        sqrt s

    /// The sum of the products of the two values' contributions over the sources both hold: 0 for disjoint sources.
    /// O(m + n).
    let covariance (a: Uncertain) (b: Uncertain) : float =
        let sa = a.Sources
        let sb = b.Sources
        let mutable s = 0.0
        let mutable i = 0
        let mutable j = 0

        while i < sa.Length && j < sb.Length do
            if sa[i] < sb[j] then
                i <- i + 1
            elif sb[j] < sa[i] then
                j <- j + 1
            else
                s <- s + a.Contributions[i] * b.Contributions[j]
                i <- i + 1
                j <- j + 1

        s

    /// The covariance over the product of the standard uncertainties, in [−1, 1] up to rounding; NaN when either
    /// standard uncertainty is 0. O(m + n).
    let correlation (a: Uncertain) (b: Uncertain) : float =
        let ua = standardUncertainty a
        let ub = standardUncertainty b

        if ua = 0.0 || ub = 0.0 then
            nan
        else
            covariance a b / (ua * ub)

    /// Each source id with its signed contribution, ids ascending; a contribution that cancels to 0 stays listed.
    /// O(n), into a new array.
    let contributions (u: Uncertain) : struct (int * float)[] =
        Array.init u.Sources.Length (fun i -> struct (u.Sources[i], u.Contributions[i]))

    /// `f` at the value, with each contribution times `df` at the value: first-order propagation through a function
    /// whose derivative is `df`. O(n).
    let map (f: float -> float) (df: float -> float) (u: Uncertain) : Uncertain =
        Uncertain.Lift(f u.Value, df u.Value, u)

    /// `f` at the two values, with the first value's contributions times `dfx` and the second's times `dfy`, both
    /// partial derivatives at the two values, summed per source. O(m + n).
    let map2
        (f: float -> float -> float)
        (dfx: float -> float -> float)
        (dfy: float -> float -> float)
        (a: Uncertain)
        (b: Uncertain)
        : Uncertain =
        Uncertain.Combine(f a.Value b.Value, dfx a.Value b.Value, a, dfy a.Value b.Value, b)

    /// |x|, as `Uncertain.Abs`: derivative the sign of x, +1 at 0 and −0, NaN at NaN. Under Fable, F#'s `abs` on an
    /// `Uncertain` compiles to `Math.abs` and returns NaN (Fable's `Replacements.fs`, case "Abs", sends a non-numeric
    /// argument to `Math.abs`), so code meant for both targets uses this function.
    let abs (u: Uncertain) : Uncertain = Uncertain.Abs u
