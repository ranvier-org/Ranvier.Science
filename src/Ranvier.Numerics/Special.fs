namespace Ranvier.Numerics

open System

/// Special functions of a real argument. An argument outside a function's domain raises
/// `ArgumentOutOfRangeException`; a NaN argument gives NaN.
module Special =
    // Constants and series coefficients: python -I tests/reference/special.py gamma
    let private sqrtTwoPi = 2.5066282746310007
    let private halfLnTwoPi = 0.9189385332046728
    let private lnPi = 1.1447298858494002

    // B₂ₖ / (2k(2k − 1)), k = 1..10: the coefficients of Stirling's series for ln Γ.
    let private stirlingCoefficients =
        [|
            1.0 / 12.0
            -1.0 / 360.0
            1.0 / 1260.0
            -1.0 / 1680.0
            1.0 / 1188.0
            -691.0 / 360360.0
            1.0 / 156.0
            -3617.0 / 122400.0
            43867.0 / 244188.0
            -174611.0 / 125400.0
        |]

    // B₂ₖ / (2k), k = 1..10: the coefficients of the asymptotic series for ψ.
    let private digammaCoefficients =
        [|
            1.0 / 12.0
            -1.0 / 120.0
            1.0 / 252.0
            -1.0 / 240.0
            1.0 / 132.0
            -691.0 / 32760.0
            1.0 / 12.0
            -3617.0 / 8160.0
            43867.0 / 14364.0
            -174611.0 / 6600.0
        |]

    /// The integer k nearest x, with x − k in [−½, ½]; x − k is then exact.
    let private nearestInteger (x: float) : float =
        let n = Math.Floor x
        if x - n >= 0.5 then n + 1.0 else n

    /// sin(πx), as (−1)ᵏ sin(πr) with r = x − k reduced exactly to [−½, ½] for an integer k.
    let internal sinPi (x: float) : float =
        let k = nearestInteger x
        let s = Math.Sin (Math.PI * (x - k))
        if k % 2.0 = 0.0 then s else -s

    /// ln Γ(x) − ((x − ½) ln x − x + ½ ln 2π) for x ≥ 8: Stirling's series Σₖ₌₁¹⁰ B₂ₖ / (2k(2k − 1) x^(2k−1)). The
    /// first omitted term, B₂₂ / (22 · 21) = 77683/5796 over x²¹, is below 1.5e-18 at x = 8 (the coefficient printed by
    /// python -I tests/reference/special.py gamma).
    let internal stirlingRemainder (x: float) : float =
        let w = 1.0 / (x * x)
        let mutable sum = stirlingCoefficients[9]

        for k in 8 .. -1 .. 0 do
            sum <- sum * w + stirlingCoefficients[k]

        sum / x

    /// ln Γ(x) for x ≥ 8.
    let private lnGammaStirling (x: float) : float =
        (x - 0.5) * Math.Log x - x + halfLnTwoPi + stirlingRemainder x

    /// ln Γ(x) for x > 0, exactly 0 at 1 and 2.
    let private lnGammaPositive (x: float) : float =
        if x >= 8.0 then
            lnGammaStirling x
        elif x = 1.0 || x = 2.0 then
            0.0
        else
            let mutable product = x
            let mutable k = 1.0

            while x + k < 8.0 do
                product <- product * (x + k)
                k <- k + 1.0

            lnGammaStirling (x + k) - Math.Log product

    /// Raises unless x > 0 or x is a negative non-integer.
    let private checkGammaArgument (name: string) (x: float) : unit =
        if x <= 0.0 && x = Math.Floor x then
            raise (
                ArgumentOutOfRangeException(
                    nameof x,
                    sprintf "%s is undefined at %g: x must be greater than 0, or negative and not an integer." name x
                )
            )

    /// ln |Γ(x)| for x > 0 and for negative x that is not an integer: exactly 0 at 1 and 2, +∞ at +∞ and NaN at NaN.
    /// Raises `ArgumentOutOfRangeException` at 0, at a negative integer and at −∞. Error at most
    /// max(1e-13 · |ln Γ(x)|, 1e-14): relative 1e-13, and absolute 1e-14 where |ln Γ(x)| < 0.1. The floor is absolute
    /// because ln |Γ| has zeros, at 1 and 2 and two between consecutive integers below −2, near which its value is the
    /// difference of much larger terms and keeps their absolute rounding error. Pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here).
    let gammaLn (x: float) : float =
        if Double.IsNaN x then
            nan
        elif x = infinity then
            infinity
        else
            checkGammaArgument "gammaLn" x

            if x > 0.0 then
                lnGammaPositive x
            else
                lnPi - Math.Log (abs (sinPi x)) - lnGammaPositive (1.0 - x)

    /// Γ(x) for 8 ≤ x < 171.63, as √(2π) · (q · e^(−x)) · q · e^(S(x)) with q = x^((x − ½)/2) and S the Stirling series.
    let private gammaLarge (x: float) : float =
        let q = Math.Pow (x, (x - 0.5) / 2.0)
        sqrtTwoPi * (q * Math.Exp (-x)) * q * Math.Exp (stirlingRemainder x)

    /// Γ(x) for x ≥ 0.5.
    let private gammaFromHalf (x: float) : float =
        if x >= 171.63 then
            infinity
        elif x >= 8.0 then
            gammaLarge x
        else
            let mutable product = x
            let mutable k = 1.0

            while x + k < 8.0 do
                product <- product * (x + k)
                k <- k + 1.0

            gammaLarge (x + k) / product

    /// Γ(x) for x > 0 and for negative x that is not an integer: +∞ from 171.63 on, where Γ(x) exceeds the largest
    /// double, and NaN at NaN. Raises `ArgumentOutOfRangeException` at 0, at a negative integer and at −∞. Relative error
    /// at most 1e-13 where |Γ(x)| ≥ 1e-300, pinned by the `Special` tests against `tests/reference/special.py`
    /// (measured here).
    let gamma (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            checkGammaArgument "gamma" x

            if x >= 0.5 then
                gammaFromHalf x
            else
                Math.PI / (sinPi x * gammaFromHalf (1.0 - x))

    /// ψ(x) for x > 0.
    let private digammaPositive (x: float) : float =
        let mutable shift = 0.0
        let mutable k = 0.0

        while x + k < 8.0 do
            shift <- shift + 1.0 / (x + k)
            k <- k + 1.0

        let z = x + k
        let w = 1.0 / (z * z)
        let mutable sum = digammaCoefficients[9]

        for i in 8 .. -1 .. 0 do
            sum <- sum * w + digammaCoefficients[i]

        Math.Log z - 0.5 / z - sum * w - shift

    /// ψ(x) = Γ′(x) / Γ(x) for x > 0 and for negative x that is not an integer: +∞ at +∞ and NaN at NaN. Raises
    /// `ArgumentOutOfRangeException` at 0, at a negative integer and at −∞. Error at most max(1e-13 · |ψ(x)|, 1e-14):
    /// relative 1e-13, and absolute 1e-14 where |ψ(x)| < 0.1. The floor is absolute because ψ has zeros, near 1.4616
    /// and one between each pair of consecutive non-positive integers, near which its value is the difference of much
    /// larger terms and keeps their absolute rounding error. Pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here).
    let digamma (x: float) : float =
        if Double.IsNaN x then
            nan
        elif x = infinity then
            infinity
        else
            checkGammaArgument "digamma" x

            if x > 0.0 then
                digammaPositive x
            else
                let r = x - nearestInteger x
                digammaPositive (1.0 - x) - Math.PI * Math.Cos (Math.PI * r) / Math.Sin (Math.PI * r)

    /// ln(1 + x) for x ≥ −1, accurate near 0: −∞ at −1, +∞ at +∞ and NaN at NaN. Raises `ArgumentOutOfRangeException`
    /// below −1. Kahan's method: with u = 1 + x rounded, x when u = 1, and ln u · x / (u − 1) otherwise (Goldberg 1991,
    /// "What every computer scientist should know about floating-point arithmetic", Theorem 4, from documentation).
    /// Relative error at most 1e-15, pinned by the `Special` tests near 0 against series and by round trips through
    /// `expm1` (measured here).
    let log1p (x: float) : float =
        if Double.IsNaN x then
            nan
        elif x < -1.0 then
            raise (ArgumentOutOfRangeException(nameof x, sprintf "log1p needs x >= -1; x = %g." x))
        elif x = infinity then
            infinity
        else
            let u = 1.0 + x

            if u = 1.0 then x
            elif u = 0.0 then -infinity
            else Math.Log u * (x / (u - 1.0))

    /// eˣ − 1 for every x, accurate near 0: +∞ where eˣ overflows and at +∞, −1 at −∞ and NaN at NaN. Kahan's method, the
    /// counterpart of `log1p`'s (general knowledge): with u = eˣ, x when u = 1, −1 when u − 1 = −1, and
    /// (u − 1) · x / ln u otherwise. Relative error at most 1e-15, pinned by the `Special` tests near 0 against series and
    /// by round trips through `log1p` (measured here).
    let expm1 (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            let u = Math.Exp x

            if u = 1.0 then
                x
            elif u = infinity then
                infinity
            else
                let v = u - 1.0
                if v = -1.0 then -1.0 else v * (x / Math.Log u)

    /// ln(1 + x) − x for x ≥ −1: its Taylor series −x²/2 + x³/3 − x⁴/4 + … for |x| < 0.5, summed until a term leaves the
    /// sum unchanged, and `log1p x − x` otherwise.
    let internal log1pmx (x: float) : float =
        if abs x < 0.5 then
            let mutable power = x * x
            let mutable k = 2.0
            let mutable sum = 0.0
            let mutable next = -power / k

            while next <> sum do
                sum <- next
                power <- -power * x
                k <- k + 1.0
                next <- sum - power / k

            sum
        else
            log1p x - x

    /// Raises unless a > 0 and b > 0.
    let private checkBetaArguments (a: float) (b: float) : unit =
        if not (a > 0.0) then
            raise (ArgumentOutOfRangeException(nameof a, sprintf "B(a, b) needs a > 0; a = %g." a))

        if not (b > 0.0) then
            raise (ArgumentOutOfRangeException(nameof b, sprintf "B(a, b) needs b > 0; b = %g." b))

    /// ln B(a, b) = ln Γ(a) + ln Γ(b) − ln Γ(a + b) for a, b > 0: −∞ when either is +∞, NaN when either is NaN.
    /// `betaLn a b` and `betaLn b a` are equal. Raises `ArgumentOutOfRangeException` when a or b is 0 or less. Error at
    /// most max(1e-13 · |ln B(a, b)|, 1e-14): relative 1e-13, and absolute 1e-14 where |ln B(a, b)| < 0.1. The floor is
    /// absolute because ln B is zero wherever B(a, b) = 1, as at a = b = 1, near which its value is the difference of
    /// much larger terms and keeps their absolute rounding error. Pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here).
    let betaLn (a: float) (b: float) : float =
        if Double.IsNaN a || Double.IsNaN b then
            nan
        else
            checkBetaArguments a b
            let a, b = if a >= b then a, b else b, a

            if a = infinity then
                -infinity
            elif b >= 8.0 then
                let s = a + b

                (a - 0.5) * log1p (-b / s) + (b - 0.5) * Math.Log (b / s) - 0.5 * Math.Log s
                + halfLnTwoPi
                + stirlingRemainder a
                + stirlingRemainder b
                - stirlingRemainder s
            elif a >= 8.0 then
                let s = a + b

                lnGammaPositive b + (a - 0.5) * log1p (-b / s) - b * Math.Log s + b + stirlingRemainder a
                - stirlingRemainder s
            else
                lnGammaPositive a + lnGammaPositive b - lnGammaPositive (a + b)

    /// B(a, b) = Γ(a)Γ(b) / Γ(a + b) for a, b > 0: 0 when either is +∞, NaN when either is NaN. `beta a b` and
    /// `beta b a` are equal. Raises `ArgumentOutOfRangeException` when a or b is 0 or less. Relative error at most 1e-13
    /// where |ln B(a, b)| < 256, and at most 3e-13 where |ln B(a, b)| ≥ 256 and B(a, b) ≥ 1e-300. The second bound is
    /// looser because B is exp(betaLn a b) once a + b ≥ 8, so the absolute error of ln B becomes the relative error of
    /// B. Once |ln B| ≥ 256, one ulp of ln B is at least 2⁻⁴⁴ ≈ 5.7e-14 (general knowledge), so two ulp exceed 1e-13.
    /// Over 1,500 seeded pairs the worst was 2.29e-13 at (285012.33, 54.578), the same on .NET and JavaScript (measured
    /// here, by a scan against `tests/reference/special.py`). Both bounds are pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here).
    let beta (a: float) (b: float) : float =
        if Double.IsNaN a || Double.IsNaN b then
            nan
        else
            checkBetaArguments a b
            let a, b = if a >= b then a, b else b, a

            if a + b < 8.0 then
                gamma a * (gamma b / gamma (a + b))
            else
                Math.Exp (betaLn a b)

    /// ln n! for n ≥ 0, exactly 0 at n = 0 and 1. Raises `ArgumentOutOfRangeException` when n < 0. Error at most
    /// max(1e-13 · ln n!, 1e-14): relative 1e-13, with the absolute floor of `gammaLn`, whose zeros at 1 and 2 are
    /// n = 0 and 1, where the value is exact. Pinned by the `Special` tests against `tests/reference/special.py`
    /// (measured here).
    let logFactorial (n: int) : float =
        if n < 0 then
            raise (ArgumentOutOfRangeException(nameof n, sprintf "n! needs n >= 0; n = %d." n))

        lnGammaPositive (float n + 1.0)

    /// ln C(n, k), the logarithm of the binomial coefficient, for 0 ≤ k ≤ n. `logChoose n k` and `logChoose n (n − k)`
    /// are equal. Raises `ArgumentOutOfRangeException` unless 0 ≤ k ≤ n. Error at most max(1e-13 · ln C(n, k), 1e-14):
    /// relative 1e-13, and absolute 1e-14 where ln C(n, k) < 0.1, that is at k = 0 and k = n, where the value 0 is the
    /// difference of two terms near ln(n + 1) and keeps their absolute rounding error. Pinned by the `Special` tests
    /// against `tests/reference/special.py` (measured here).
    let logChoose (n: int) (k: int) : float =
        if k < 0 || k > n then
            raise (ArgumentOutOfRangeException(nameof k, sprintf "C(n, k) needs 0 <= k <= n; n = %d, k = %d." n k))

        let m = float (min k (n - k))
        -(Math.Log (float n + 1.0)) - betaLn (float n - m + 1.0) (m + 1.0)
