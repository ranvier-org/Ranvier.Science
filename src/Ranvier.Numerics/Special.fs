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
            elif x > -1.0 then
                lnGammaPositive (1.0 + x) - Math.Log (-x)
            elif x > -20.0 then
                // ln Γ(x + m) − ln |x (x + 1) … (x + m − 1)| with x + m in (1, 2); every factor is exact.
                let m = Math.Floor (-x) + 2.0
                let mutable product = x
                let mutable j = 1.0

                while j < m do
                    product <- product * (x + j)
                    j <- j + 1.0

                lnGammaPositive (x + m) - Math.Log (abs product)
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
    /// Relative error at most 1e-15 over x ≥ −1, pinned by the `Special` tests near 0 against series, at negative x
    /// against `tests/reference/special.py`, and by round trips through `expm1` (measured here).
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
    /// (u − 1) · x / ln u otherwise. Relative error at most 1e-15 over every finite x, pinned by the `Special` tests near 0
    /// against series, at negative x against `tests/reference/special.py`, and by round trips through `log1p` (measured
    /// here).
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
    /// where |ln B(a, b)| < 256, and at most 5e-13 where |ln B(a, b)| ≥ 256 and B(a, b) ≥ 1e-300. The second bound is
    /// looser because B is exp(betaLn a b) once a + b ≥ 8, so the absolute error of ln B becomes the relative error of
    /// B. Once |ln B| ≥ 256, one ulp of ln B is at least 2⁻⁴⁴ ≈ 5.7e-14 (general knowledge), so two ulp exceed 1e-13.
    /// Both bounds are pinned by the `Special` tests against `tests/reference/special.py` (measured here).
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

    // 2/√π and 1/√π: python -I tests/reference/special.py erf
    let private twoOverSqrtPi = 1.1283791670955126
    let private oneOverSqrtPi = 0.5641895835477563

    /// a + b = s + e exactly, with s the rounded sum (Knuth's TwoSum).
    let private twoSum (a: float) (b: float) : struct (float * float) =
        let s = a + b
        let v = s - a
        struct (s, (a - (s - v)) + (b - v))

    /// Veltkamp's split of a into high + low, each half of the significand.
    let private split (a: float) : struct (float * float) =
        let c = 134217729.0 * a // 2²⁷ + 1
        let high = c - (c - a)
        struct (high, a - high)

    /// a · b = p + e exactly, with p the rounded product (Dekker's product), where no partial product overflows or
    /// underflows.
    let private twoProduct (a: float) (b: float) : struct (float * float) =
        let p = a * b
        let struct (ah, al) = split a
        let struct (bh, bl) = split b
        struct (p, ((ah * bh - p) + ah * bl + al * bh) + al * bl)

    /// erf(x) for 0 ≤ x < 2 as a double-double high + low: (2/√π) e^(−y) Σₙ x (2y)ⁿ / (1 · 3 · … · (2n + 1)) with y the
    /// rounded x², summed until a term leaves the sum unchanged, with the terms, the sum and the products in
    /// double-double. NaN when a term still changes the sum after 500 terms.
    let private erfSeries (x: float) : struct (float * float) =
        // The same rounded y in both factors: the errors its rounding causes in e^(−y) and in the sum largely cancel.
        let y = x * x
        let z = 2.0 * y
        let mutable term = x
        let mutable termLow = 0.0
        let mutable sum = x
        let mutable sumLow = 0.0
        let mutable n = 0
        let mutable converged = false

        while not converged && n < 500 do
            n <- n + 1
            let d = float (2 * n + 1)
            // term · z / d: the product split exactly, then the quotient and its remainder.
            let struct (p, pe) = twoProduct term z
            let q = p / d
            let struct (qd, qde) = twoProduct q d
            termLow <- ((p - qd) - qde + pe + termLow * z) / d
            term <- q
            let struct (s, se) = twoSum sum term
            sumLow <- sumLow + se + termLow
            converged <- s = sum
            sum <- s

        if converged then
            let e = Math.Exp (-y)
            let struct (p, pe) = twoProduct e sum
            let struct (q, qe) = twoProduct twoOverSqrtPi p
            twoSum q (qe + twoOverSqrtPi * (pe + e * sumLow))
        else
            struct (nan, nan)

    /// erfc(x) for 2 ≤ x < 27.3: e^(−x²)/√π · 1/(x + ½/(x + 1/(x + (3/2)/(x + …)))), Laplace's continued fraction by
    /// the modified Lentz method, with e^(−x²) = e^(−h) e^(−l) for x² = h + l exactly. NaN when the continued fraction
    /// has not converged after 500 terms.
    let private erfcContinuedFraction (x: float) : float =
        let mutable f = x
        let mutable c = x
        let mutable d = 0.0
        let mutable n = 0
        let mutable converged = false

        while not converged && n < 500 do
            n <- n + 1
            let a = 0.5 * float n
            d <- 1.0 / (x + a * d)
            c <- x + a / c
            let delta = c * d
            f <- f * delta
            converged <- abs (delta - 1.0) <= 2.220446049250313e-16 // 2⁻⁵²

        if converged then
            let struct (h, l) = twoProduct x x
            Math.Exp (-h) * Math.Exp (-l) * oneOverSqrtPi / f
        else
            nan

    /// erfc(x) for x ≥ 0.
    let private erfcPositive (x: float) : float =
        if x < 2.0 then
            let struct (high, low) = erfSeries x
            (1.0 - high) - low
        elif x < 27.3 then
            erfcContinuedFraction x
        else
            // erfc(x) < e^(−x²) / (x√π) (general knowledge), which is below 2⁻¹⁰⁷⁵, half the least subnormal, from
            // x = 27.3.
            0.0

    /// erf(x) for x ≥ 0.
    let private erfPositive (x: float) : float =
        if x < 2.0 then
            let struct (high, _) = erfSeries x
            high
        else
            1.0 - erfcPositive x

    /// The error function erf(x) = (2/√π) ∫₀ˣ e^(−t²) dt for every x: ±1 at ±∞ and NaN at NaN, with
    /// erf(−x) = −erf(x) exactly. Relative error at most 1e-13 over every x where |erf(x)| ≥ 1e-300, pinned by the
    /// `Special` tests against `tests/reference/special.py` (measured here).
    let erf (x: float) : float =
        if Double.IsNaN x then nan
        elif x < 0.0 then -(erfPositive (-x))
        elif x = 0.0 then x
        else erfPositive x

    /// The complementary error function erfc(x) = 1 − erf(x) for every x, accurate where erf(x) is near 1: 0 from
    /// x = 27.3 on and at +∞, 2 at −∞ and NaN at NaN, with erfc(−x) = 2 − erfc(x) exactly. Relative error at most
    /// 1e-13 over every x where erfc(x) ≥ 1e-300, that is x up to about 26.2, pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here).
    let erfc (x: float) : float =
        if Double.IsNaN x then nan
        elif x < 0.0 then 2.0 - erfcPositive (-x)
        else erfcPositive x

    /// The most series terms or continued-fraction steps the incomplete gamma functions take before giving NaN.
    let private incompleteGammaCap = 10000

    /// Raises unless a > 0 and finite; a NaN passes.
    let private checkShape (a: float) : unit =
        if not (Double.IsNaN a) && not (a > 0.0 && a < infinity) then
            raise (
                ArgumentOutOfRangeException(
                    nameof a,
                    sprintf "The regularised incomplete gamma needs a > 0 and finite; a = %g." a
                )
            )

    /// Raises unless a > 0 and finite and x ≥ 0; a NaN passes.
    let private checkIncompleteGammaArguments (a: float) (x: float) : unit =
        checkShape a

        if x < 0.0 then
            raise (
                ArgumentOutOfRangeException(
                    nameof x,
                    sprintf "The regularised incomplete gamma needs x >= 0; x = %g." x
                )
            )

    /// xᵃe⁻ˣ/Γ(a) for a > 0 and 0 < x < ∞. From a = 8 on it is exp(a(ln(1 + t) − t) − R(a)) √(a/2π), with
    /// t = (x − a)/a and R the remainder `stirlingRemainder`, so that no large ln Γ(a) is subtracted. a(ln(1 + t) − t)
    /// is `a * log1pmx t` for |t| < ½ and a ln(x/a) + (a − x) otherwise, because forming t loses x's precision when x
    /// is far below a. Below 8 it is exp(a ln x − x − ln Γ(a)).
    let private gammaPrefactor (a: float) (x: float) : float =
        if a >= 8.0 then
            let t = (x - a) / a
            let exponent = if abs t < 0.5 then a * log1pmx t else a * Math.Log (x / a) + (a - x)
            Math.Exp (exponent - stirlingRemainder a) * Math.Sqrt (a / (2.0 * Math.PI))
        else
            Math.Exp (a * Math.Log x - x - gammaLn a)

    /// P(a, x) for 0 < x < a + 1: xᵃe⁻ˣ/Γ(a + 1) · Σₙ xⁿ/((a + 1)…(a + n)), summed until a term leaves the sum
    /// unchanged. Below a = 8 the factor is exp(a ln x − x − ln Γ(a + 1)), so a subnormal a never divides an
    /// underflowed xᵃe⁻ˣ/Γ(a). NaN when a term still changes the sum after `incompleteGammaCap` terms.
    let private lowerSeries (a: float) (x: float) : float =
        let mutable term = 1.0
        let mutable sum = 1.0
        let mutable n = 0
        let mutable converged = false

        while not converged && n < incompleteGammaCap do
            n <- n + 1
            term <- term * x / (a + float n)
            let next = sum + term
            converged <- next = sum
            sum <- next

        if not converged then nan
        elif a >= 8.0 then gammaPrefactor a x * sum / a
        else Math.Exp (a * Math.Log x - x - gammaLn (a + 1.0)) * sum

    /// Q(a, x) for x > 0, fastest from x = a + 1 on: xᵃe⁻ˣ/Γ(a) · 1/(x + 1 − a − 1(1 − a)/(x + 3 − a − 2(2 − a)/(x + 5
    /// − a − …))), Legendre's continued fraction by the modified Lentz method. NaN when it has not converged after
    /// `incompleteGammaCap` steps.
    let private upperContinuedFraction (a: float) (x: float) : float =
        let tiny = 1e-300
        let mutable b = x + 1.0 - a
        let mutable c = 1.0 / tiny
        let mutable d = 1.0 / b
        let mutable h = d
        let mutable n = 0
        let mutable converged = false

        while not converged && n < incompleteGammaCap do
            n <- n + 1
            let an = -(float n) * (float n - a)
            b <- b + 2.0
            d <- an * d + b

            if abs d < tiny then
                d <- tiny

            c <- b + an / c

            if abs c < tiny then
                c <- tiny

            d <- 1.0 / d
            let delta = d * c
            h <- h * delta
            converged <- abs (delta - 1.0) <= 2.220446049250313e-16 // 2⁻⁵²

        if converged then gammaPrefactor a x * h else nan

    /// Q(a, x) for a < 1 and 0 < x < a + 1, where 1 − P(a, x) would cancel: Q(a, x₀) + (1/Γ(a)) ∫ₓ^x₀ tᵃ⁻¹e⁻ᵗ dt with
    /// x₀ = a + 1. Q(a, x₀) comes from the continued fraction, and the integral is Σₙ (−1)ⁿ (x₀ᵃ⁺ⁿ − xᵃ⁺ⁿ)/(n!(a + n)),
    /// summed until a term leaves the sum unchanged, its n = 0 term xᵃ(e^(a ln(x₀/x)) − 1)/a by `expm1`. Q(a, x₀) and
    /// the integral are both positive, so their sum does not cancel. NaN when a term still changes the sum after
    /// `incompleteGammaCap` terms.
    let private upperSmallShape (a: float) (x: float) : float =
        let x0 = a + 1.0
        let xa = Math.Exp (a * Math.Log x)
        let ratio = x0 / x
        let logRatio = if ratio < infinity then Math.Log ratio else Math.Log x0 - Math.Log x
        let mutable sum = xa * expm1 (a * logRatio) / a
        let mutable coefficient = 1.0
        let mutable powerX0 = Math.Exp (a * Math.Log x0)
        let mutable powerX = xa
        let mutable n = 0
        let mutable converged = false

        while not converged && n < incompleteGammaCap do
            n <- n + 1
            coefficient <- -coefficient / float n
            powerX0 <- powerX0 * x0
            powerX <- powerX * x
            let next = sum + coefficient * (powerX0 - powerX) / (a + float n)
            converged <- next = sum
            sum <- next

        if converged then
            upperContinuedFraction a x0 + sum * a * Math.Exp (-(gammaLn (a + 1.0)))
        else
            nan

    /// struct (P(a, x), Q(a, x)) for a > 0 finite and x ≥ 0, neither NaN. Below x = a + 1, P by its series; above, Q by
    /// the continued fraction; the other as 1 minus. Below a + 1 with a < 1 and P > ½, Q by `upperSmallShape` and P as
    /// 1 − Q instead.
    let private regularizedGamma (a: float) (x: float) : struct (float * float) =
        if x = 0.0 then
            struct (0.0, 1.0)
        elif x = infinity then
            struct (1.0, 0.0)
        elif x < a + 1.0 then
            let p = lowerSeries a x

            if a < 1.0 && p > 0.5 then
                let q = upperSmallShape a x
                struct (1.0 - q, q)
            else
                struct (p, 1.0 - p)
        else
            let q = upperContinuedFraction a x
            struct (1.0 - q, q)

    /// P(a, x) = (1/Γ(a)) ∫₀ˣ tᵃ⁻¹e⁻ᵗ dt, the regularised lower incomplete gamma function, for a > 0 finite and
    /// x ≥ 0: 0 at x = 0, 1 at x = +∞, NaN when a or x is NaN. Raises `ArgumentOutOfRangeException` when a ≤ 0,
    /// a = +∞ or x < 0, even with the other argument NaN. Relative error at most 1e-12 over 0 < a ≤ 1000 where
    /// P(a, x) ≥ 1e-300, pinned by the `Special` tests against `tests/reference/special.py` (measured here). Cost: a
    /// series or continued fraction of at most 10 000 terms, NaN past them; a dense scan of a ≤ 1000 took at most 270
    /// series terms and 95 continued-fraction steps (measured here).
    let gammaLowerRegularized (a: float) (x: float) : float =
        checkIncompleteGammaArguments a x

        if Double.IsNaN a || Double.IsNaN x then
            nan
        else
            let struct (p, _) = regularizedGamma a x
            p

    /// Q(a, x) = 1 − P(a, x) = (1/Γ(a)) ∫ₓ^∞ tᵃ⁻¹e⁻ᵗ dt, the regularised upper incomplete gamma function, for a > 0
    /// finite and x ≥ 0, accurate where P(a, x) is near 1: 1 at x = 0, 0 at x = +∞, NaN when a or x is NaN. Raises
    /// `ArgumentOutOfRangeException` when a ≤ 0, a = +∞ or x < 0, even with the other argument NaN. Relative error at
    /// most 1e-12 over 0 < a ≤ 1000 where Q(a, x) ≥ 1e-300, pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here). Cost: as `gammaLowerRegularized`.
    let gammaUpperRegularized (a: float) (x: float) : float =
        checkIncompleteGammaArguments a x

        if Double.IsNaN a || Double.IsNaN x then
            nan
        else
            let struct (_, q) = regularizedGamma a x
            q

    /// The starting point of `invgammp`, Numerical Recipes, 3rd edition, §6.2.1, as ported in the Haskell
    /// math-functions package (`invIncompleteGamma` in Numeric/SpecFunctions/Internal.hs, from documentation). For
    /// a > 1, the Wilson–Hilferty form a(1 − 1/(9a) + z/(3√a))³, with z the normal quantile of p by the rational form
    /// of Abramowitz and Stegun, Handbook of Mathematical Functions (NBS Applied Mathematics Series 55, 1964), 26.2.22
    /// (general knowledge; the constants are the port's), held to at least (pΓ(a + 1))^(1/a), a lower bound of the
    /// root that replaces the book's 1e-3. For a ≤ 1, with c = a(0.253 + 0.12a): e^((ln p − ln(1 − c))/a) when
    /// 1 − p > c, and 1 − ln((1 − p)/c) otherwise, which are the port's (p/t)^(1/a) and 1 − ln(1 − (p − t)/(1 − t)) for
    /// t = 1 − c, formed without t. The starting point is at least 5e-324.
    let private inverseGuess (a: float) (p: float) : float =
        let x =
            if a > 1.0 then
                let t = Math.Sqrt (-2.0 * Math.Log (if p < 0.5 then p else 1.0 - p))
                let upper = t - (2.30753 + t * 0.27061) / (1.0 + t * (0.99229 + t * 0.04481))
                let z = if p < 0.5 then -upper else upper
                let c = 1.0 - 1.0 / (9.0 * a) + z / (3.0 * Math.Sqrt a)
                max (Math.Exp ((Math.Log p + gammaLn (a + 1.0)) / a)) (a * (c * c * c))
            else
                let c = a * (0.253 + a * 0.12)

                if 1.0 - p > c then
                    Math.Exp ((Math.Log p - log1p (-c)) / a)
                else
                    1.0 - Math.Log ((1.0 - p) / c)

        if x >= 5e-324 then x else 5e-324

    /// The x ≥ 0 with P(a, x) = p, for a > 0 finite and 0 ≤ p ≤ 1: 0 at p = 0, +∞ at p = 1, NaN when a or p is NaN.
    /// Raises `ArgumentOutOfRangeException` when a ≤ 0, a = +∞, p < 0 or p > 1, even with the other argument NaN. From
    /// the starting point of `invgammp` (Numerical Recipes, 3rd edition, §6.2.1, as ported in the Haskell
    /// math-functions package; from documentation), held to a lower bound of the root and to at least 5e-324, it takes
    /// Halley steps on P(a, x) − p, and halves x where a step would leave it at 0 or below. It stops when a step is
    /// below 1e-15 of x or below 1e-323, when a step below 1e-9 of x is no smaller than the one before, when P(a, x) is
    /// within 1e-14 p of p (keeping that last step only inside (0, ∞)), or after 100 steps. It gives 0 only where
    /// P(a, x) exceeds p at the least positive double. P(a, x) at the result is within relative 1e-12 of p over
    /// 0 < a ≤ 1000 where the root is a normal double and the doubles beside it resolve p, pinned by the `Special`
    /// tests (measured here). Below that floor of 1e-300, at large a, the result is NaN where P(a, x) and xᵃe⁻ˣ/Γ(a)
    /// both underflow at the starting point; the largest such p measured here is 1.9e-301, at a = 546.39 (the
    /// `Special` test "the inverse below 1e-300 at a large shape"). Cost: at most 100 evaluations of
    /// `gammaLowerRegularized`.
    let gammaLowerRegularizedInverse (a: float) (p: float) : float =
        checkShape a

        if p < 0.0 || p > 1.0 then
            raise (ArgumentOutOfRangeException(nameof p, sprintf "The inverse of P(a, x) needs 0 <= p <= 1; p = %g." p))

        if Double.IsNaN a || Double.IsNaN p then
            nan
        elif p = 0.0 then
            0.0
        elif p = 1.0 then
            infinity
        else
            let mutable x = inverseGuess a p
            let mutable steps = 0
            let mutable previous = infinity
            let mutable finished = false

            while not finished && steps < 100 do
                if x = 0.0 || Double.IsNaN x then
                    finished <- true
                else
                    steps <- steps + 1
                    let struct (pAtX, _) = regularizedGamma a x
                    let error = pAtX - p
                    // With P′(a, x) = xᵃ⁻¹e⁻ˣ/Γ(a) and P″/P′ = (a − 1)/x − 1, the Halley step
                    // u/(1 − ½u((a − 1)/x − 1)) for u = (P − p)/P′, written with r = u/x, where x is only a factor.
                    // Where xᵃe⁻ˣ/Γ(a) underflows with P above p, r is +∞ and the step goes down to a halving.
                    let r = error / gammaPrefactor a x
                    let step = r * x / (1.0 - 0.5 * min 1.0 (r * (a - 1.0 - x)))
                    let next = if Double.IsNaN step && r > 0.0 then -infinity else x - step
                    let size = abs step

                    if abs error <= 1e-14 * p then
                        // Within the noise of P: the last step stands only where it stays inside (0, ∞).
                        if next > 0.0 && next < infinity then
                            x <- next

                        finished <- true
                    else
                        x <- if next <= 0.0 then 0.5 * x else next
                        finished <- size < max (1e-15 * x) 1e-323 || (size < 1e-9 * x && size >= previous)
                        previous <- size

            x

    /// The most continued-fraction steps or series terms the incomplete beta function takes before giving NaN.
    let private incompleteBetaCap = 10000

    /// Raises unless a and b are greater than 0 and finite; a NaN passes.
    let private checkBetaShapes (a: float) (b: float) : unit =
        if not (Double.IsNaN a) && not (a > 0.0 && a < infinity) then
            raise (
                ArgumentOutOfRangeException(
                    nameof a,
                    sprintf "The regularised incomplete beta needs a > 0 and finite; a = %g." a
                )
            )

        if not (Double.IsNaN b) && not (b > 0.0 && b < infinity) then
            raise (
                ArgumentOutOfRangeException(
                    nameof b,
                    sprintf "The regularised incomplete beta needs b > 0 and finite; b = %g." b
                )
            )

    /// ln Γ(z) − ln Γ(z + s) for z > 0 and s ≥ 0, accurate in absolute terms for small s, where ln Γ(z) and
    /// ln Γ(z + s) nearly cancel: the sum of ln(1 + s/(z + k)) for z + k < 8, plus, at the first w = z + k ≥ 8,
    /// Stirling's series −((w − ½) ln(1 + s/w) + s ln(w + s) − s + R(w + s) − R(w)), R the remainder
    /// `stirlingRemainder`.
    let private lnGammaDifference (z: float) (s: float) : float =
        let mutable sum = 0.0
        let mutable w = z

        while w < 8.0 do
            sum <- sum + log1p (s / w)
            w <- w + 1.0

        sum - ((w - 0.5) * log1p (s / w) + s * Math.Log (w + s) - s + (stirlingRemainder (w + s) - stirlingRemainder w))

    /// ln Γ(1 + a) + ln Γ(1 + b) − ln Γ(1 + a + b) = ln(B(a, b) ab/(a + b)) for a, b > 0, by `lnGammaDifference` on the
    /// smaller shape: of the order of min(a, b) for a small shape, where ln B(a, b) is near −ln a − ln b.
    let private lnBetaReduced (a: float) (b: float) : float =
        let s, t = if a <= b then a, b else b, a
        lnGammaDifference (1.0 + t) s - lnGammaDifference 1.0 s

    /// Which shape, if either, `betaPowers` divides by.
    type private BetaScale =
        | Unscaled
        | OverA
        | OverB

    /// xᵃ(1 − x)ᵇ/B(a, b), divided by the divisor q that the scale gives (1, a or b), for a, b > 0 and 0 < x < 1, given
    /// ln x and ln(1 − x); the same for (a, b, x) and (b, a, 1 − x) with the scale swapped.
    ///
    /// For a, b ≥ 8 it is √(ab/(2πc)) exp(a(ln(1 + t) − t) + b(ln(1 + u) − u) − R(a) − R(b) + R(c))/q, with c = a + b,
    /// t = x/x₀ − 1, u = (1 − x)/y₀ − 1, x₀ = a/c, y₀ = b/c and R the remainder `stirlingRemainder`, so that no large
    /// ln Γ is subtracted. The rounding of c, x₀ and y₀ is carried exactly by Knuth's and Dekker's error-free sum and
    /// product: with a + b = c + γ, x₀c = a + ρ₁ and y₀c = b + ρ₂, the linear part a t + b u, 0 in exact arithmetic,
    /// is −(ρ₁t + ρ₂u), and (1 − x) − y₀ is −(x − x₀) − (ρ₁ + ρ₂ + γ)/c. a(ln(1 + t) − t) is `a * log1pmx t` for
    /// t > −½ and a(ln(x/x₀) − t) below, where forming 1 + t would lose the precision of x; likewise for u, whose
    /// second form takes 1 − x only for x > ½, where it is exact.
    ///
    /// Where a or b is below 1 it is exp(a ln x + b ln(1 − x) − G) times ab/(a + b), b/(a + b) or a/(a + b), with G
    /// from `lnBetaReduced`: the exponent then holds no large ln a or ln b, and a subnormal shape scales the result.
    /// Otherwise it is exp(a ln x + b ln(1 − x) − ln B(a, b) − ln q).
    let private betaPowers (a: float) (b: float) (x: float) (lnX: float) (lnY: float) (scale: BetaScale) : float =
        if a >= 8.0 && b >= 8.0 then
            let s =
                match scale with
                | Unscaled -> 0.0
                | OverA -> Math.Log a
                | OverB -> Math.Log b

            let struct (c, gamma) = twoSum a b
            let x0 = a / c
            let y0 = b / c
            let struct (p1, e1) = twoProduct x0 c
            let struct (p2, e2) = twoProduct y0 c
            let rho1 = (p1 - a) + e1
            let rho2 = (p2 - b) + e2
            let dx = x - x0
            let dy = -dx - (rho1 + rho2 + gamma) / c
            let t = dx / x0
            let u = dy / y0
            let xTerm = if t > -0.5 then a * log1pmx t else a * (Math.Log (x / x0) - t)
            let yTerm = if u > -0.5 then b * log1pmx u else b * (Math.Log ((1.0 - x) / y0) - u)
            let remainders = stirlingRemainder a + stirlingRemainder b - stirlingRemainder c

            Math.Sqrt (a * b / (2.0 * Math.PI * c))
            * Math.Exp (xTerm + yTerm - (rho1 * t + rho2 * u) - remainders - s)
        elif a < 1.0 || b < 1.0 then
            let shapes =
                match scale with
                | Unscaled -> a * (b / (a + b))
                | OverA -> b / (a + b)
                | OverB -> a / (a + b)

            Math.Exp (a * lnX + b * lnY - lnBetaReduced a b) * shapes
        else
            let s =
                match scale with
                | Unscaled -> 0.0
                | OverA -> Math.Log a
                | OverB -> Math.Log b

            Math.Exp (a * lnX + b * lnY - betaLn a b - s)

    /// The continued fraction of Numerical Recipes, 3rd edition, §6.4 (`betacf`, from documentation),
    /// I_x(a, b) = xᵃ(1 − x)ᵇ/(a B(a, b)) · 1/(1 + d₁/(1 + d₂/(1 + …))), with
    /// d₂ₘ₊₁ = −(a + m)(a + b + m)x/((a + 2m)(a + 2m + 1)) and d₂ₘ = m(b − m)x/((a + 2m − 1)(a + 2m)), by the modified
    /// Lentz method; it converges fast for x < (a + 1)/(a + b + 2). The first denominator,
    /// 1 + d₁ = ((a + 1) − (a + b)x)/(a + 1), is near 2/(a + b + 2) at that switch, and its numerator is formed from
    /// the error-free sums and product of its terms. NaN when it has not converged after `incompleteBetaCap` steps of
    /// two terms.
    let private betaContinuedFraction (a: float) (b: float) (x: float) : float =
        let tiny = 1e-300
        let struct (qab, qabLow) = twoSum a b
        let struct (qap, qapLow) = twoSum a 1.0
        let qam = a - 1.0
        let struct (p, pLow) = twoProduct qab x
        let mutable c = 1.0
        let mutable d = ((qap - p) + qapLow - (pLow + qabLow * x)) / qap

        if abs d < tiny then
            d <- tiny

        d <- 1.0 / d
        let mutable h = d
        let mutable m = 0
        let mutable converged = false

        while not converged && m < incompleteBetaCap do
            m <- m + 1
            let fm = float m
            let m2 = 2.0 * fm
            let even = fm * (b - fm) * x / ((qam + m2) * (a + m2))
            d <- 1.0 + even * d

            if abs d < tiny then
                d <- tiny

            c <- 1.0 + even / c

            if abs c < tiny then
                c <- tiny

            d <- 1.0 / d
            h <- h * (d * c)
            let odd = -(a + fm) * (qab + fm) * x / ((a + m2) * (qap + m2))
            d <- 1.0 + odd * d

            if abs d < tiny then
                d <- tiny

            c <- 1.0 + odd / c

            if abs c < tiny then
                c <- tiny

            d <- 1.0 / d
            let delta = d * c
            h <- h * delta
            converged <- abs (delta - 1.0) <= 2.220446049250313e-16 // 2⁻⁵²

        if converged then h else nan

    /// I_x(a, b) for b < 1 and b ≤ a, at or above s = (a + 1)/(a + b + 2), where 1 − I_(1 − x)(b, a) would cancel:
    /// I_s(a, b) + (1/B(a, b)) ∫ₛˣ tᵃ⁻¹(1 − t)ᵇ⁻¹ dt, a sum of two positive terms. With y = 1 − x and yₛ = 1 − s, both
    /// exact because s ≥ ½, the integral is Σₖ cₖ (yₛᵇ⁺ᵏ − yᵇ⁺ᵏ)/(b + k), with cₖ = (1 − a)(2 − a)…(k − a)/k! from the
    /// binomial series of (1 − u)ᵃ⁻¹ at u = 1 − t. I_s comes from the continued fraction, and the k = 0 term is
    /// yᵇ(e^(b ln(yₛ/y)) − 1) by `expm1`. The sum runs until a term leaves it unchanged; its terms fall from k = 2 on,
    /// because a·yₛ < b + 1 < 2. NaN when a term still changes the sum after `incompleteBetaCap` terms.
    let private betaAboveSmallB (a: float) (b: float) (x: float) : float =
        let s = (a + 1.0) / (a + b + 2.0)
        let ys = 1.0 - s
        let y = 1.0 - x
        let atS = betaPowers a b s (Math.Log s) (log1p (-s)) OverA * betaContinuedFraction a b s
        let ysb = Math.Exp (b * Math.Log ys)
        let yb = Math.Exp (b * Math.Log y)
        let first = yb * expm1 (b * Math.Log (ys / y))
        let mutable tail = 0.0
        let mutable coefficient = 1.0
        let mutable powerS = ysb
        let mutable powerX = yb
        let mutable k = 0
        let mutable converged = false

        while not converged && k < incompleteBetaCap do
            k <- k + 1
            coefficient <- coefficient * (float k - a) / float k
            powerS <- powerS * ys
            powerX <- powerX * y
            let next = tail + coefficient * (powerS - powerX) / (b + float k)
            converged <- next = tail
            tail <- next

        if converged then
            // 1/(b B(a, b)) = (a/(a + b)) e^(−G), with G from `lnBetaReduced`.
            atS + (first + b * tail) * (a / (a + b)) * Math.Exp (-(lnBetaReduced a b))
        else
            nan

    /// I_x(a, b) for a, b > 0 finite and 0 < x < 1: ½ at x = ½ when a = b, by symmetry. Below s = (a + 1)/(a + b + 2)
    /// it is the continued fraction; at or above s, 1 − I_(1 − x)(b, a) by the continued fraction, or
    /// `betaAboveSmallB` where b < 1, b ≤ a and I_(1 − x)(b, a) > ½, where the subtraction would cancel.
    let private regularizedBeta (a: float) (b: float) (x: float) : float =
        let lnX = Math.Log x
        let lnY = log1p (-x)

        if a = b && x = 0.5 then
            0.5
        elif x < (a + 1.0) / (a + b + 2.0) then
            betaPowers a b x lnX lnY OverA * betaContinuedFraction a b x
        else
            let complement = betaPowers a b x lnX lnY OverB * betaContinuedFraction b a (1.0 - x)

            if complement > 0.5 && b < 1.0 && b <= a then
                betaAboveSmallB a b x
            else
                1.0 - complement

    /// I_x(a, b) = (1/B(a, b)) ∫₀ˣ tᵃ⁻¹(1 − t)ᵇ⁻¹ dt, the regularised incomplete beta function, for a, b > 0 finite
    /// and 0 ≤ x ≤ 1: 0 at x = 0, 1 at x = 1, NaN when a, b or x is NaN. Raises `ArgumentOutOfRangeException` when
    /// a ≤ 0, b ≤ 0, a or b is +∞, or x is outside [0, 1], even with another argument NaN. Relative error at most 1e-12
    /// over 0 < a, b ≤ 1000 where I_x(a, b) ≥ 1e-300, pinned by the `Special` tests against
    /// `tests/reference/special.py` (measured here). Cost: a continued fraction of at most 10 000 steps, and for b < 1
    /// a series of as many terms, NaN past them; a dense scan of a, b ≤ 1000 took at most 92 steps and 54 terms
    /// (measured here).
    let betaRegularized (a: float) (b: float) (x: float) : float =
        checkBetaShapes a b

        if x < 0.0 || x > 1.0 then
            raise (
                ArgumentOutOfRangeException(
                    nameof x,
                    sprintf "The regularised incomplete beta needs 0 <= x <= 1; x = %g." x
                )
            )

        if Double.IsNaN a || Double.IsNaN b || Double.IsNaN x then nan
        elif x = 0.0 then 0.0
        elif x = 1.0 then 1.0
        else regularizedBeta a b x

    /// The starting point of Numerical Recipes, 3rd edition, §6.14.1 (`invbetai`, from documentation). For a, b ≥ 1 it
    /// is a/(a + b e^(2w)), with w from the normal quantile of p (Abramowitz and Stegun 26.5.22, the quantile by
    /// 26.2.22), held to bounds of the root: for p < ½ to at least (p a B(a, b))^(1/a), since
    /// I_x(a, b) ≤ xᵃ/(a B(a, b)) when b ≥ 1, and for p ≥ ½ to at most 1 − ((1 − p) b B(a, b))^(1/b), since
    /// 1 − I_x(a, b) ≤ (1 − x)ᵇ/(b B(a, b)) when a ≥ 1. Otherwise, with w = t + u for t = (a/(a + b))ᵃ/a and
    /// u = (b/(a + b))ᵇ/b, it is (a w p)^(1/a) when p < t/w, and 1 − (b w (1 − p))^(1/b) above, both formed in
    /// logarithms: ln(a w) = a ln(a/(a + b)) + ln(1 + u/t), ln(b w) = b ln(b/(a + b)) + ln(1 + t/u), and the second as
    /// −(e^((ln(b w) + ln(1 − p))/b) − 1) by `expm1`, so that a tiny shape neither cancels the start to 0 nor pushes
    /// it below 0. The starting point is held to [5e-324, 1 − 2⁻⁵³].
    let private betaInverseGuess (a: float) (b: float) (p: float) : float =
        let x =
            if a >= 1.0 && b >= 1.0 then
                let tail = if p < 0.5 then p else 1.0 - p
                let t = Math.Sqrt (-2.0 * Math.Log tail)
                let upper = t - (2.30753 + t * 0.27061) / (1.0 + t * (0.99229 + t * 0.04481))
                let z = if p < 0.5 then upper else -upper
                let lambda = (z * z - 3.0) / 6.0
                let h = 2.0 / (1.0 / (2.0 * a - 1.0) + 1.0 / (2.0 * b - 1.0))

                let w =
                    z * Math.Sqrt (lambda + h) / h
                    - (1.0 / (2.0 * b - 1.0) - 1.0 / (2.0 * a - 1.0)) * (lambda + 5.0 / 6.0 - 2.0 / (3.0 * h))

                let normal = a / (a + b * Math.Exp (2.0 * w))
                let lnB = betaLn a b

                if p < 0.5 then
                    max normal (Math.Exp ((Math.Log p + Math.Log a + lnB) / a))
                else
                    min normal (1.0 - Math.Exp ((Math.Log (1.0 - p) + Math.Log b + lnB) / b))
            else
                let lnA = Math.Log a
                let lnB = Math.Log b
                let lnC = Math.Log (a + b)
                let atLn = a * (lnA - lnC) // ln(a t)
                let buLn = b * (lnB - lnC) // ln(b u)
                let ratioLn = (atLn - lnA) - (buLn - lnB) // ln(t/u)
                // ln(1 + e^z), without overflow.
                let softplus (z: float) = if z > 0.0 then z + log1p (Math.Exp (-z)) else log1p (Math.Exp z)

                if p < 1.0 / (1.0 + Math.Exp (-ratioLn)) then
                    Math.Exp ((atLn + softplus (-ratioLn) + Math.Log p) / a)
                else
                    -(expm1 ((buLn + softplus ratioLn + log1p (-p)) / b))

        if x > 0.9999999999999999 then 0.9999999999999999 // 1 − 2⁻⁵³
        elif x >= 5e-324 then x
        else 5e-324

    /// The x in [0, 1] with I_x(a, b) = p, for a, b > 0 finite and 0 ≤ p ≤ 1: 0 at p = 0, 1 at p = 1, NaN when a, b or
    /// p is NaN. Raises `ArgumentOutOfRangeException` when a ≤ 0, b ≤ 0, a or b is +∞, or p is outside [0, 1], even
    /// with another argument NaN. From the starting point of Numerical Recipes, 3rd edition, §6.14.1 (`invbetai`, from
    /// documentation), held to bounds of the root in the tails and to [5e-324, 1 − 2⁻⁵³], it takes Halley steps on
    /// I_x(a, b) − p, or, for p < ½, Newton steps on ln I_x(a, b) against ln x where I_x(a, b) is off p by more than a
    /// factor e or the Halley step would leave (0, 1), and halves the distance to 0 or 1 where a step would still leave
    /// (0, 1). It stops when a step is below 1e-15 of the distance from x to the nearer end of (0, 1) or below 2⁻⁵² x,
    /// when a step below 1e-9 of that distance is no smaller than the one before, when I_x(a, b) is within 1e-14 p of
    /// p (keeping that last step only inside (0, 1)), or after 100 steps. It gives 0 only where I_x(a, b) exceeds p at
    /// the least positive double, 1 only where I_x(a, b) falls short of p at the largest double below 1, and NaN only
    /// where `betaRegularized` gives NaN at a step. I_x(a, b) at the result is within relative 1e-12 of p over
    /// 0 < a, b ≤ 1000 where the root is a normal double and the doubles beside it resolve p; near 1 for b < 1, where
    /// I_x(a, b) can step by more than 1e-12 p between adjacent doubles, the root lies within one double of the
    /// result. Both are pinned by the `Special` tests and a dense scan with shapes from 5e-324 to 1000 (measured here).
    /// Cost: at most 100 evaluations of `betaRegularized`; the scan took at most 15 (measured here).
    let betaRegularizedInverse (a: float) (b: float) (p: float) : float =
        checkBetaShapes a b

        if p < 0.0 || p > 1.0 then
            raise (
                ArgumentOutOfRangeException(nameof p, sprintf "The inverse of I_x(a, b) needs 0 <= p <= 1; p = %g." p)
            )

        if Double.IsNaN a || Double.IsNaN b || Double.IsNaN p then
            nan
        elif p = 0.0 then
            0.0
        elif p = 1.0 then
            1.0
        else
            let mutable x = betaInverseGuess a b p
            let mutable steps = 0
            let mutable previous = infinity
            let mutable finished = false

            while not finished && steps < 100 do
                if x = 0.0 || x = 1.0 || Double.IsNaN x then
                    finished <- true
                else
                    steps <- steps + 1
                    let y = 1.0 - x
                    let value = regularizedBeta a b x
                    let factor = betaPowers a b x (Math.Log x) (log1p (-x)) Unscaled
                    let error = value - p
                    let logRatio = Math.Log (value / p)

                    // With F = xᵃ(1 − x)ᵇ/B(a, b), I′ = F/(x(1 − x)) and I″/I′ = (a − 1)/x − (b − 1)/(1 − x), the
                    // Halley step u/(1 − ½uI″/I′) for u = (I − p)/I′, written with r = (I − p)/F so that x and 1 − x
                    // are only factors. Where F underflows, r is infinite and the step takes the sign of r.
                    let r = error / factor
                    let halley = r * x * y / (1.0 - 0.5 * min 1.0 (r * ((a - 1.0) * y - (b - 1.0) * x)))

                    let halleyNext =
                        if Double.IsNaN halley && not (Double.IsNaN r) then
                            (if r > 0.0 then -infinity else infinity)
                        else
                            x - halley

                    let next =
                        if
                            p < 0.5
                            && value > 0.0
                            && factor > 0.0
                            && (abs logRatio > 1.0 || not (halleyNext > 0.0 && halleyNext < 1.0))
                        then
                            // d ln I/d ln x = xI′/I = F/((1 − x)I).
                            x * Math.Exp (-logRatio * y * (value / factor))
                        else
                            halleyNext

                    let size = abs (x - next)

                    if abs error <= 1e-14 * p then
                        // Within the noise of I: the last step stands only where it stays inside (0, 1).
                        if next > 0.0 && next < 1.0 then
                            x <- next

                        finished <- true
                    else
                        x <- if next <= 0.0 then 0.5 * x elif next >= 1.0 then 0.5 * (x + 1.0) else next
                        let nearer = min x (1.0 - x)

                        finished <-
                            size < max (1e-15 * nearer) (max (2.220446049250313e-16 * x) 1e-323) // 2⁻⁵² x
                            || (size < 1e-9 * nearer && size >= previous)

                        previous <- size

            x
