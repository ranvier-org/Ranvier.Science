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
