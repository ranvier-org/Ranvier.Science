namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>
/// A continuous univariate distribution with fixed parameters. Build one with the constructors of the
/// <c>Continuous</c> module, which validate the parameters. Two values with equal parameters are equal.
/// </summary>
type Continuous =
    private
    | Normal of mean: float * sd: float
    | LogNormal of mu: float * sigma: float
    | Uniform of lower: float * upper: float
    | Exponential of rate: float
    | Cauchy of location: float * scale: float
    | Weibull of shape: float * scale: float
    | Logistic of location: float * scale: float
    | Gamma of shape: float * scale: float
    | ChiSquared of dof: float
    | Beta of a: float * b: float
    | StudentT of dof: float
    | FisherF of d1: float * d2: float

/// <summary>
/// Density, distribution function, survival function, quantile, moments, support and sampling of continuous
/// distributions.
/// </summary>
/// <remarks>
/// <c>cdf</c>, <c>survival</c> and <c>quantile</c> state their error bound over the parameter values the tests
/// cover. The bounds hold on .NET and on JavaScript separately; the two targets can differ in the last digits.
/// A NaN argument gives NaN. <c>density</c> and <c>logDensity</c> carry no bound beyond the accuracy of the
/// <c>exp</c>, <c>log</c> and power functions they call, and for the gamma, chi-squared, beta, Student t and F that of
/// <c>Special.gammaLn</c> or <c>Special.betaLn</c> and the cancellation of terms of the log density as large as
/// shape · |ln x| (general knowledge). Measured here against mpmath 1.3.0, by the scan that <c>cdf</c> cites: at most
/// 2e-13 relative for shapes and degrees of freedom from 0.5 to 100, 1.2e-12 for the gamma at shape 1000 and 1.1e-10
/// for the chi-squared with 1e5 degrees of freedom; the test "the stated domains at their edges against the reference"
/// pins the density at the ends of each range, within 1e-12, 2e-12 at gamma shape 1000 and 2e-10 at chi-squared 1e5.
/// </remarks>
module Continuous =
    let private sqrt2 = sqrt 2.0
    let private sqrtTwoPi = sqrt (2.0 * Math.PI)
    let private halfLnTwoPi = 0.5 * log (2.0 * Math.PI)

    /// <summary>The normal distribution with the given mean and standard deviation. Raises ArgumentOutOfRangeException
    /// unless the mean is finite and the standard deviation is finite and greater than 0.</summary>
    let normal (mean: float) (sd: float) : Continuous =
        Check.finite (nameof mean) mean
        Check.finitePositive (nameof sd) sd
        Normal(mean, sd)

    /// <summary>The distribution of exp(X) for X normal with mean mu and standard deviation sigma. Raises
    /// ArgumentOutOfRangeException unless mu is finite and sigma is finite and greater than 0.</summary>
    let logNormal (mu: float) (sigma: float) : Continuous =
        Check.finite (nameof mu) mu
        Check.finitePositive (nameof sigma) sigma
        LogNormal(mu, sigma)

    /// <summary>The uniform distribution on [lower, upper]. Raises ArgumentOutOfRangeException unless both bounds are
    /// finite, lower is less than upper and upper - lower is finite.</summary>
    let uniform (lower: float) (upper: float) : Continuous =
        Check.finite (nameof lower) lower
        Check.finite (nameof upper) upper

        if not (lower < upper) then
            raise (ArgumentOutOfRangeException(nameof upper, "upper must be greater than lower."))

        Check.finite "upper - lower" (upper - lower)
        Uniform(lower, upper)

    /// <summary>The exponential distribution with the given rate. Raises ArgumentOutOfRangeException unless the rate
    /// is finite and greater than 0.</summary>
    let exponential (rate: float) : Continuous =
        Check.finitePositive (nameof rate) rate
        Exponential rate

    /// <summary>The Cauchy distribution with the given location and scale. Raises ArgumentOutOfRangeException unless
    /// the location is finite and the scale is finite and greater than 0.</summary>
    let cauchy (location: float) (scale: float) : Continuous =
        Check.finite (nameof location) location
        Check.finitePositive (nameof scale) scale
        Cauchy(location, scale)

    /// <summary>The Weibull distribution with the given shape and scale. Raises ArgumentOutOfRangeException unless
    /// both are finite and greater than 0.</summary>
    let weibull (shape: float) (scale: float) : Continuous =
        Check.finitePositive (nameof shape) shape
        Check.finitePositive (nameof scale) scale
        Weibull(shape, scale)

    /// <summary>The logistic distribution with the given location and scale. Raises ArgumentOutOfRangeException unless
    /// the location is finite and the scale is finite and greater than 0.</summary>
    let logistic (location: float) (scale: float) : Continuous =
        Check.finite (nameof location) location
        Check.finitePositive (nameof scale) scale
        Logistic(location, scale)

    /// <summary>The gamma distribution with the given shape and scale, of density
    /// x^(shape − 1) e^(−x/scale) / (Γ(shape) scale^shape). Raises ArgumentOutOfRangeException unless both are finite
    /// and greater than 0.</summary>
    let gamma (shape: float) (scale: float) : Continuous =
        Check.finitePositive (nameof shape) shape
        Check.finitePositive (nameof scale) scale
        Gamma(shape, scale)

    /// <summary>The chi-squared distribution with the given degrees of freedom: the gamma with shape dof/2 and scale 2.
    /// Raises ArgumentOutOfRangeException unless dof is finite and greater than 0.</summary>
    let chiSquared (dof: float) : Continuous =
        Check.finitePositive (nameof dof) dof
        ChiSquared dof

    /// <summary>The beta distribution on [0, 1] with shapes a and b, of density x^(a − 1) (1 − x)^(b − 1) / B(a, b).
    /// Raises ArgumentOutOfRangeException unless both are finite and greater than 0.</summary>
    let beta (a: float) (b: float) : Continuous =
        Check.finitePositive (nameof a) a
        Check.finitePositive (nameof b) b
        Beta(a, b)

    /// <summary>Student's t distribution with the given degrees of freedom, centred at 0 with unit scale. Raises
    /// ArgumentOutOfRangeException unless dof is finite and greater than 0.</summary>
    let studentT (dof: float) : Continuous =
        Check.finitePositive (nameof dof) dof
        StudentT dof

    /// <summary>The F distribution with d1 numerator and d2 denominator degrees of freedom: (V₁/d1)/(V₂/d2) for
    /// independent chi-squared V₁ and V₂. Raises ArgumentOutOfRangeException unless both are finite and greater than
    /// 0.</summary>
    let fisherF (d1: float) (d2: float) : Continuous =
        Check.finitePositive (nameof d1) d1
        Check.finitePositive (nameof d2) d2
        FisherF(d1, d2)

    // The standard normal distribution function and survival function at z.
    let private normalCdf (z: float) : float = 0.5 * Special.erfc (-z / sqrt2)
    let private normalSurvival (z: float) : float = 0.5 * Special.erfc (z / sqrt2)

    // The standard Cauchy distribution function: atan(-1 / z) / pi below 0, so the lower tail keeps its relative accuracy.
    let private cauchyCdf (z: float) : float =
        if Double.IsNaN z then nan
        elif z < 0.0 then atan (-1.0 / z) / Math.PI
        elif z > 0.0 then 1.0 - atan (1.0 / z) / Math.PI
        else 0.5

    // The standard logistic distribution function.
    let private logisticCdf (z: float) : float = 1.0 / (1.0 + exp -z)

    // The standard normal quantile of p in (0, 0.5]: Acklam's approximation, then one Halley step on the lower-tail
    // distribution function, e = cdf(x) - p, u = e sqrt(2 pi) exp(x^2 / 2), x - u / (1 + x u / 2). The step is skipped
    // below -37.5, where exp(x^2 / 2) approaches overflow.
    let private normalLowerQuantile (p: float) : float =
        let x = NormalQuantile.ofProbability p

        if x < -37.5 then
            x
        else
            let e = normalCdf x - p
            let u = e * sqrtTwoPi * exp (x * x / 2.0)
            x - u / (1.0 + x * u / 2.0)

    // The standard normal quantile of p in (0, 1); exact symmetry for p >= 0.5, where 1 - p is exact.
    let private normalQuantile (p: float) : float =
        if p = 0.5 then 0.0
        elif p < 0.5 then normalLowerQuantile p
        else -(normalLowerQuantile (1.0 - p))

    // -ln(1 - p), accurate for small p.
    let private exponentialQuantile (p: float) : float =
        if p <= 0.5 then -Special.log1p (-p) else -(log (1.0 - p))

    let private cauchyQuantile (p: float) : float =
        if p = 0.5 then 0.0
        elif p < 0.5 then -1.0 / tan (Math.PI * p)
        else 1.0 / tan (Math.PI * (1.0 - p))

    let private logisticQuantile (p: float) : float =
        if p = 0.5 then 0.0
        elif p < 0.5 then log p - Special.log1p (-p)
        else -(log (1.0 - p) - Special.log1p (-(1.0 - p)))

    // 2^-52, the spacing of the doubles in [1, 2).
    let private epsilon = 2.220446049250313e-16

    // The least positive normal double, 2^-1022.
    let private leastNormal = 2.2250738585072014e-308

    // The limit at its lower bound of a density that behaves there as atOne · x^(power − 1): +∞ below power 1, atOne at
    // power 1 and 0 above.
    let private boundDensity (power: float) (atOne: float) : float =
        if power < 1.0 then infinity
        elif power = 1.0 then atOne
        else 0.0

    // ln of the gamma(shape, scale) density at x, not NaN: the log of its limit where x / scale is 0, and −∞ outside
    // (0, ∞).
    let private gammaLogDensity (shape: float) (scale: float) (x: float) : float =
        if x < 0.0 || x = infinity then
            -infinity
        else
            let t = x / scale

            if t = 0.0 then log (boundDensity shape (1.0 / scale))
            elif t = infinity then -infinity
            else (shape - 1.0) * log t - t - Special.gammaLn shape - log scale

    // ln of the beta(a, b) density at x, not NaN: the log of its limit at 0 and 1, and −∞ outside [0, 1].
    let private betaLogDensity (a: float) (b: float) (x: float) : float =
        if x < 0.0 || x > 1.0 then -infinity
        elif x = 0.0 then log (boundDensity a b)
        elif x = 1.0 then log (boundDensity b a)
        else (a - 1.0) * log x + (b - 1.0) * Special.log1p (-x) - Special.betaLn a b

    // ln of the t(dof) density at t, not NaN; ln(1 + t²/dof) is 2 ln |t| − ln dof where t²/dof overflows.
    let private studentTLogDensity (dof: float) (t: float) : float =
        if Double.IsInfinity t then
            -infinity
        else
            let r = t * t / dof
            let l = if r < infinity then Special.log1p r else 2.0 * log (abs t) - log dof
            -((dof + 1.0) / 2.0) * l - 0.5 * log dof - Special.betaLn (dof / 2.0) 0.5

    // ln w and ln(1 − w) for w = x / (x + c), x > 0 finite and c > 0: −ln(1 + c/x) and −ln(1 + x/c), or ln x − ln c
    // and ln c − ln x where the ratio overflows.
    let private logRatios (x: float) (c: float) : struct (float * float) =
        let r = c / x
        let lnW = if r < infinity then -(Special.log1p r) else log x - log c
        let s = x / c
        let lnV = if s < infinity then -(Special.log1p s) else log c - log x
        struct (lnW, lnV)

    // ln of the F(d1, d2) density at x, not NaN: (d1/2) ln w + (d2/2) ln(1 − w) − ln x − ln B(d1/2, d2/2) with
    // w = x / (x + d2/d1); the log of its limit at 0 and −∞ outside [0, ∞).
    let private fisherFLogDensity (d1: float) (d2: float) (x: float) : float =
        if x < 0.0 || x = infinity then
            -infinity
        elif x = 0.0 then
            log (boundDensity (d1 / 2.0) 1.0)
        else
            let struct (lnW, lnV) = logRatios x (d2 / d1)
            d1 / 2.0 * lnW + d2 / 2.0 * lnV - log x - Special.betaLn (d1 / 2.0) (d2 / 2.0)

    // I_w(a, b) for w in [0, 1]. Below the least normal double, where w has lost precision or underflowed, the leading
    // term w^a / (a B(a, b)) of its series from ln w, which lnW gives without forming w; the next term is about w times
    // it.
    let private betaLowerTail (a: float) (b: float) (w: float) (lnW: unit -> float) : float =
        if w >= leastNormal then
            Special.betaRegularized a b w
        else
            exp (a * lnW () - log a - Special.betaLn a b)

    // P(T > s) for the t(dof) and s ≥ 1: I_x(dof/2, 1/2) / 2 with x = dof / (dof + s²), or (dof / s) / s where s²
    // overflows.
    let private studentTTail (dof: float) (s: float) : float =
        if s = infinity then
            0.0
        else
            let s2 = s * s
            let x = if s2 < infinity then dof / (dof + s2) else dof / s / s
            0.5 * betaLowerTail (dof / 2.0) 0.5 x (fun () -> log dof - 2.0 * log s)

    // P(T ≤ t) for the t(dof). Below |t| = 1 it is ½ ∓ I_y(1/2, dof/2) / 2 with y = t² / (dof + t²), at least 0.158
    // there for every dof (general knowledge: P(T ≤ −1) falls with dof towards Φ(−1) = 0.1587), so the subtraction
    // loses at most about one bit; from |t| = 1 on, the tail I_x(dof/2, 1/2) / 2.
    let private studentTCdf (dof: float) (t: float) : float =
        let s = abs t

        if s < 1.0 then
            let half = 0.5 * Special.betaRegularized 0.5 (dof / 2.0) (t * t / (dof + t * t))
            if t < 0.0 then 0.5 - half else 0.5 + half
        else
            let tail = studentTTail dof s
            if t < 0.0 then tail else 1.0 - tail

    // The t(dof) quantile of p in (0, 0.5]: from I_y(1/2, dof/2) = 1 − 2p, exact for p ≥ 1/4, where that gives y ≤ ½,
    // and otherwise from I_x(dof/2, 1/2) = 2p, with t = −√(dof · y / (1 − y)) or −√(dof · (1 − x) / x); of y and
    // x = 1 − y the one used is the one at most ½, whose complement is exact. Where x falls below the least normal
    // double, from the leading term of I_x as in betaLowerTail: ln |t| = ½ ln dof − (ln 2p + ln a + ln B(a, ½)) / dof
    // with a = dof/2.
    let private studentTLowerQuantile (dof: float) (p: float) : float =
        let a = dof / 2.0

        let fromX () =
            let x = Special.betaRegularizedInverse a 0.5 (2.0 * p)

            if x >= leastNormal then
                -(sqrt (dof * ((1.0 - x) / x)))
            else
                -(exp (0.5 * log dof - (log (2.0 * p) + log a + Special.betaLn a 0.5) / dof))

        if p = 0.5 then
            0.0
        elif p >= 0.25 then
            let y = Special.betaRegularizedInverse 0.5 a (1.0 - 2.0 * p)
            if y <= 0.5 then -(sqrt (dof * (y / (1.0 - y)))) else fromX ()
        else
            fromX ()

    // The beta(a, b) survival at x in (0, 1): I_(1 − x)(b, a), except below ½ where I_x(a, b) ≤ ½. There 1 − x rounds,
    // which moves I_(1 − x)(b, a) by the density times up to 2^-54, unbounded near 0 for a < 1; 1 − I_x(a, b) is at
    // least ½ and cannot cancel.
    let private betaSurvival (a: float) (b: float) (x: float) : float =
        if x >= 0.5 then
            Special.betaRegularized b a (1.0 - x)
        else
            let lower = Special.betaRegularized a b x
            if lower <= 0.5 then 1.0 - lower else Special.betaRegularized b a (1.0 - x)

    // P(F ≤ x) for the F(d1, d2) and x not NaN: I_w(d1/2, d2/2) with w = x / (x + c), c = d2/d1. Above x = c, where
    // w > ½ rounds near 1 and an I whose second shape is below 1 moves there by its density times up to 2^-54, it is
    // 1 − I_v(d2/2, d1/2) with v = c / (x + c) wherever that tail is at most ½, so that the subtraction cannot cancel.
    let private fisherFCdf (d1: float) (d2: float) (x: float) : float =
        if x <= 0.0 then
            0.0
        elif x = infinity then
            1.0
        else
            let a = d1 / 2.0
            let b = d2 / 2.0
            let c = d2 / d1

            if x <= c then
                betaLowerTail a b (x / (x + c)) (fun () -> log x - log c)
            else
                let upper = betaLowerTail b a (c / (x + c)) (fun () -> log c - log x)
                if upper <= 0.5 then 1.0 - upper else Special.betaRegularized a b (x / (x + c))

    // P(F > x) for the F(d1, d2) and x not NaN: I_v(d2/2, d1/2) with v = c / (x + c), and below x = c, as in
    // fisherFCdf with the roles swapped, 1 − I_w(d1/2, d2/2) wherever that tail is at most ½.
    let private fisherFSurvival (d1: float) (d2: float) (x: float) : float =
        if x <= 0.0 then
            1.0
        elif x = infinity then
            0.0
        else
            let a = d1 / 2.0
            let b = d2 / 2.0
            let c = d2 / d1

            if x >= c then
                betaLowerTail b a (c / (x + c)) (fun () -> log c - log x)
            else
                let lower = betaLowerTail a b (x / (x + c)) (fun () -> log x - log c)
                if lower <= 0.5 then 1.0 - lower else Special.betaRegularized b a (c / (x + c))

    // The F(d1, d2) quantile of p in (0, 1), from the beta inverse at p for p ≤ ½ and at 1 − p in the reflected
    // shapes above, with x = c w / (1 − w) or c (1 − w) / w. Where w falls below the least normal double, from the
    // leading term of I_w: ln x = ln c + (ln p + ln a + ln B(a, b)) / a with a = d1/2, b = d2/2.
    let private fisherFQuantile (d1: float) (d2: float) (p: float) : float =
        let a = d1 / 2.0
        let b = d2 / 2.0
        let c = d2 / d1

        if p <= 0.5 then
            let w = Special.betaRegularizedInverse a b p

            if w >= leastNormal then
                c * (w / (1.0 - w))
            else
                exp (log c + (log p + log a + Special.betaLn a b) / a)
        else
            let w = Special.betaRegularizedInverse b a (1.0 - p)
            c * ((1.0 - w) / w)

    // The gamma(shape, 1) quantile of p in (0, 1). For p ≤ ½ the inverse of P; above, from that start, at most 8 Newton
    // steps on ln Q(shape, x) − ln(1 − p), whose slope is −f/Q with f = x^(shape − 1) e^(−x) / Γ(shape), stopping when a
    // step is below 4 ulps of x or would leave (0, ∞). ln Q is close to linear in the upper tail, where the inverse of
    // P, resolving 1 − p only to the spacing of the doubles near 1, can start several units off.
    let private gammaUnitQuantile (shape: float) (p: float) : float =
        let start = Special.gammaLowerRegularizedInverse shape p

        if p <= 0.5 then
            start
        else
            let lnTarget = log (1.0 - p)
            let lnGamma = Special.gammaLn shape
            let mutable x = start
            let mutable steps = 0
            let mutable finished = false

            while not finished && steps < 8 do
                steps <- steps + 1
                let upper = Special.gammaUpperRegularized shape x
                let density = exp ((shape - 1.0) * log x - x - lnGamma)
                let step = (log upper - lnTarget) * (upper / density)
                let next = x + step

                if next > 0.0 && next < infinity then
                    finished <- abs step <= 4.0 * epsilon * next
                    x <- next
                else
                    finished <- true

            x

    /// <summary>
    /// The probability density at x: 0 outside the support, NaN at NaN, and +∞ at 0 for a Weibull of shape below 1. At
    /// 0 for the gamma, chi-squared and F, and at 0 and 1 for the beta, it is the density's limit there: +∞ where the
    /// density's power of x (or of 1 − x) is negative, 0 where it is positive, and where it is 0 the constant 1/scale
    /// for the gamma of shape 1, ½ for the chi-squared with 2 degrees of freedom, b at 0 for the beta with a = 1 and a
    /// at 1 for the beta with b = 1, and 1 for the F with d1 = 2.
    /// </summary>
    let density (d: Continuous) (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            match d with
            | Normal (mean, sd) ->
                let z = (x - mean) / sd
                exp (-0.5 * z * z) / (sd * sqrtTwoPi)
            | LogNormal (mu, sigma) ->
                if x <= 0.0 then
                    0.0
                else
                    let z = (log x - mu) / sigma
                    exp (-0.5 * z * z) / (x * sigma * sqrtTwoPi)
            | Uniform (lower, upper) -> if x < lower || x > upper then 0.0 else 1.0 / (upper - lower)
            | Exponential rate -> if x < 0.0 then 0.0 else rate * exp (-rate * x)
            | Cauchy (location, scale) ->
                let z = (x - location) / scale
                1.0 / (Math.PI * scale * (1.0 + z * z))
            | Weibull (shape, scale) ->
                if x < 0.0 then
                    0.0
                elif x = 0.0 then
                    if shape < 1.0 then infinity
                    elif shape = 1.0 then 1.0 / scale
                    else 0.0
                else
                    let t = x / scale
                    let value = shape / scale * (t ** (shape - 1.0)) * exp (-(t ** shape))
                    // t ** (shape - 1) overflows only where exp(-(t ** shape)) is 0.
                    if Double.IsNaN value then 0.0 else value
            | Logistic (location, scale) ->
                let e = exp (-(abs ((x - location) / scale)))
                e / (scale * (1.0 + e) * (1.0 + e))
            | Gamma (shape, scale) ->
                if x = 0.0 then boundDensity shape (1.0 / scale) else exp (gammaLogDensity shape scale x)
            | ChiSquared dof -> if x = 0.0 then boundDensity (dof / 2.0) 0.5 else exp (gammaLogDensity (dof / 2.0) 2.0 x)
            | Beta (a, b) ->
                if x = 0.0 then boundDensity a b
                elif x = 1.0 then boundDensity b a
                else exp (betaLogDensity a b x)
            | StudentT dof -> exp (studentTLogDensity dof x)
            | FisherF (d1, d2) -> if x = 0.0 then boundDensity (d1 / 2.0) 1.0 else exp (fisherFLogDensity d1 d2 x)

    /// <summary>
    /// The natural logarithm of the density at x: −∞ outside the support, NaN at NaN, and +∞ at 0 for a Weibull of
    /// shape below 1. At the bounds of the gamma, chi-squared, beta and F it is the logarithm of the limit that
    /// <c>density</c> gives there, never NaN, so a sample exactly at a bound (as <c>Generator.gamma</c> and
    /// <c>Generator.beta</c> give at small shapes) has a log density.
    /// </summary>
    let logDensity (d: Continuous) (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            match d with
            | Normal (mean, sd) ->
                let z = (x - mean) / sd
                -0.5 * z * z - log sd - halfLnTwoPi
            | LogNormal (mu, sigma) ->
                if x <= 0.0 then
                    -infinity
                else
                    let z = (log x - mu) / sigma
                    -0.5 * z * z - log x - log sigma - halfLnTwoPi
            | Uniform (lower, upper) -> if x < lower || x > upper then -infinity else -(log (upper - lower))
            | Exponential rate -> if x < 0.0 then -infinity else log rate - rate * x
            | Cauchy (location, scale) ->
                let z = (x - location) / scale
                -(log (Math.PI * scale)) - Special.log1p (z * z)
            | Weibull (shape, scale) ->
                if x < 0.0 || x = infinity then
                    -infinity
                elif x = 0.0 then
                    if shape < 1.0 then infinity
                    elif shape = 1.0 then -(log scale)
                    else -infinity
                else
                    let t = x / scale
                    log (shape / scale) + (shape - 1.0) * log t - (t ** shape)
            | Logistic (location, scale) ->
                let a = abs ((x - location) / scale)
                -a - log scale - 2.0 * Special.log1p (exp -a)
            | Gamma (shape, scale) -> gammaLogDensity shape scale x
            | ChiSquared dof -> gammaLogDensity (dof / 2.0) 2.0 x
            | Beta (a, b) -> betaLogDensity a b x
            | StudentT dof -> studentTLogDensity dof x
            | FisherF (d1, d2) -> fisherFLogDensity d1 d2 x

    /// <summary>
    /// P(X ≤ x). Relative error at most 1e-12 where the value is at least 1e-300, over the parameter values pinned by
    /// the <c>Continuous</c> tests against <c>tests/reference/special.py</c> and closed forms (measured here: the
    /// tests "normal cdf and survival against the reference" and "logNormal against the reference"). Exactly 0 at
    /// and below the lower end of the support and 1 at and above the upper end; NaN at NaN.
    /// Domain of the bound, measured here by the tests "quantile round trips", "normal cdf and survival against the
    /// reference", "logNormal against the reference", "weibull survival at a large shape and a scale that is not a
    /// power of two" and "log-normal quantile round trip at a large mu" on .NET and on JavaScript: the normal, uniform,
    /// exponential, Cauchy and logistic at the tested parameters; the log-normal for |mu| up to 50 at sigma 0.1 and
    /// above; the Weibull for shape up to 20. Beyond it the error grows with the conditioning of the input:
    /// about shape · (x / scale)^shape · 1.1e-16 for the Weibull at a scale that is not a power of two, which pinned
    /// rows show as at most 1e-9 at shape 1000 (general knowledge: the rounding of x / scale, raised to the shape).
    /// For the gamma, chi-squared, beta, Student t and F the bound holds for shapes and degrees of freedom from 0.5 to
    /// 100, measured here by a dense scan against mpmath 1.3.0 on .NET and on JavaScript (10 792 points with arguments
    /// from 1e-300 to 1.7e308, worst 1.5e-13) that is not kept in the repository. The same scan holds it for the gamma
    /// at shapes from 0.01 to 1000, the chi-squared from 0.05 to 1e5 degrees of freedom, the beta at shapes from 0.2 to
    /// 500, the t from 0.05 to 1000 degrees of freedom and the F with one of d1 and d2 up to 1000 and the other from 0.5
    /// to 100. Pinned against <c>tests/reference/special.py</c>: rows inside the range by the tests "gamma against the
    /// reference", "chi-squared against the reference", "beta against the reference", "Student t against the
    /// reference" and "F against the reference"; rows at both ends of each range above by the test "the stated domains
    /// at their edges against the reference"; and the t at 100 degrees of freedom either side of |t| = 1, the beta at
    /// shape 0.5 near 0 and the F with a degree of freedom of 0.5 far into either tail, where a single form would
    /// cancel or round 1 − x, by the test "where a plain form would cancel or round 1 - x, against the reference".
    /// Beyond those ranges the error grows: for the t as about
    /// dof · 5.5e-17 (general knowledge: the rounding of dof / (dof + t²) near 1, raised to the power dof/2; measured
    /// here 6.6e-13 at 1e4 and 5.7e-12 at 1e5 degrees of freedom), and for the beta and the F with a shape or degrees
    /// of freedom below 0.5 as the density times 2^-54 over the value, from the rounding near 1 of 1 − x or of
    /// x / (x + d2/d1) where the density is unbounded (general knowledge; measured here up to 1.2e-9 for the beta
    /// survival at shape 0.05 near its median and 3.8e-5 for the F at 0.05 degrees of freedom). Cost: one or two
    /// evaluations of the regularised incomplete gamma or beta function for those five families.
    /// </summary>
    let cdf (d: Continuous) (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            match d with
            | Normal (mean, sd) -> normalCdf ((x - mean) / sd)
            | LogNormal (mu, sigma) -> if x <= 0.0 then 0.0 else normalCdf ((log x - mu) / sigma)
            | Uniform (lower, upper) ->
                if x <= lower then 0.0
                elif x >= upper then 1.0
                else (x - lower) / (upper - lower)
            | Exponential rate -> if x <= 0.0 then 0.0 else -(Special.expm1 (-rate * x))
            | Cauchy (location, scale) -> cauchyCdf ((x - location) / scale)
            | Weibull (shape, scale) -> if x <= 0.0 then 0.0 else -(Special.expm1 (-((x / scale) ** shape)))
            | Logistic (location, scale) -> logisticCdf ((x - location) / scale)
            | Gamma (shape, scale) -> if x <= 0.0 then 0.0 else Special.gammaLowerRegularized shape (x / scale)
            | ChiSquared dof -> if x <= 0.0 then 0.0 else Special.gammaLowerRegularized (dof / 2.0) (x / 2.0)
            | Beta (a, b) ->
                if x <= 0.0 then 0.0
                elif x >= 1.0 then 1.0
                else Special.betaRegularized a b x
            | StudentT dof -> studentTCdf dof x
            | FisherF (d1, d2) -> fisherFCdf d1 d2 x

    /// <summary>
    /// P(X &gt; x), accurate in the upper tail. Relative error at most 1e-12 where the value is at least 1e-300, over
    /// the parameter values pinned by the <c>Continuous</c> tests (measured here, as for <c>cdf</c>). 1 at and below
    /// the lower end of the support and exactly 0 at and above the upper end; NaN at NaN.
    /// Domain of the bound, measured here by the tests "quantile round trips", "normal cdf and survival against the
    /// reference", "logNormal against the reference", "weibull survival at a large shape and a scale that is not a
    /// power of two" and "log-normal quantile round trip at a large mu" on .NET and on JavaScript: the normal, uniform,
    /// exponential, Cauchy and logistic at the tested parameters; the log-normal for |mu| up to 50 at sigma 0.1 and
    /// above; the Weibull for shape up to 20. Beyond it the error grows with the conditioning of the input:
    /// about shape · (x / scale)^shape · 1.1e-16 for the Weibull at a scale that is not a power of two, which pinned
    /// rows show as at most 1e-9 at shape 1000 (general knowledge: the rounding of x / scale, raised to the shape).
    /// For the gamma, chi-squared, beta, Student t and F the domain, its edges and the cost are those of <c>cdf</c>
    /// (measured here by the same tests and scan).
    /// </summary>
    let survival (d: Continuous) (x: float) : float =
        if Double.IsNaN x then
            nan
        else
            match d with
            | Normal (mean, sd) -> normalSurvival ((x - mean) / sd)
            | LogNormal (mu, sigma) -> if x <= 0.0 then 1.0 else normalSurvival ((log x - mu) / sigma)
            | Uniform (lower, upper) ->
                if x <= lower then 1.0
                elif x >= upper then 0.0
                else (upper - x) / (upper - lower)
            | Exponential rate -> if x <= 0.0 then 1.0 else exp (-rate * x)
            | Cauchy (location, scale) -> cauchyCdf (-((x - location) / scale))
            | Weibull (shape, scale) -> if x <= 0.0 then 1.0 else exp (-((x / scale) ** shape))
            | Logistic (location, scale) -> logisticCdf (-((x - location) / scale))
            | Gamma (shape, scale) -> if x <= 0.0 then 1.0 else Special.gammaUpperRegularized shape (x / scale)
            | ChiSquared dof -> if x <= 0.0 then 1.0 else Special.gammaUpperRegularized (dof / 2.0) (x / 2.0)
            | Beta (a, b) ->
                if x <= 0.0 then 1.0
                elif x >= 1.0 then 0.0
                else betaSurvival a b x
            | StudentT dof -> studentTCdf dof (-x)
            | FisherF (d1, d2) -> fisherFSurvival d1 d2 x

    /// <summary>The bounds of the support: (−∞, ∞) for the normal, Cauchy, logistic and Student t, (0, ∞) for the
    /// log-normal, exponential, Weibull, gamma, chi-squared and F, (0, 1) for the beta and (lower, upper) for the
    /// uniform.</summary>
    let support (d: Continuous) : struct (float * float) =
        match d with
        | Normal _
        | Cauchy _
        | Logistic _
        | StudentT _ -> struct (-infinity, infinity)
        | LogNormal _
        | Exponential _
        | Weibull _
        | Gamma _
        | ChiSquared _
        | FisherF _ -> struct (0.0, infinity)
        | Beta _ -> struct (0.0, 1.0)
        | Uniform (lower, upper) -> struct (lower, upper)

    /// <summary>
    /// The x with P(X ≤ x) = p. At p = 0 and 1 it is the support's lower and upper bound; NaN at NaN. Raises
    /// ArgumentOutOfRangeException for p outside [0, 1]. The round trip holds: the <c>cdf</c> at the result is within
    /// relative 1e-12 of p for p ≤ 0.5, and the <c>survival</c> at the result is within relative 1e-12 of 1 − p for
    /// p &gt; 0.5, down to p = 1e-300, over the parameter values pinned by the <c>Continuous</c> tests (measured
    /// here: the test "quantile round trips"). The normal quantile is Acklam's approximation (relative error 1.2e-9,
    /// <c>NormalQuantile.ofProbability</c>) refined by one Halley step on the <c>cdf</c>, skipped below −37.5, where
    /// p is under about 5e-308 (general knowledge). The round trip's domain is that of <c>cdf</c>; outside it the
    /// error of the log-normal grows about as |mu| · |z| · 1.1e-16 / sigma, because exp (mu + sigma · z) rounds
    /// its argument to half an ulp of mu (general knowledge), and a pinned row shows at most 2e-11 at mu = 300,
    /// sigma = 0.1.
    /// For the gamma, chi-squared, beta, Student t and F the round trip holds over the domain of <c>cdf</c>, where the
    /// quantile is a normal double and the doubles beside it resolve p, measured here by a dense scan of the quantiles
    /// at 96 values of p from 1e-300 to 1 − 2e-16 on .NET and on JavaScript (worst 9.8e-13 for a beta quantile near 1
    /// that the doubles barely resolve, 2e-13 for the other families). It is pinned inside the range by the test
    /// "quantile round trips for the incomplete-function families", at both ends of each range by "quantile round
    /// trips at the edges of the stated domains", and within 1e-14 of p = 1 for the gamma and chi-squared by "gamma
    /// and chi-squared quantile round trips within 1e-14 of 1". Where the quantile is below the least normal double the result is
    /// 0, and beyond the largest double −∞ for the t at small dof; near 1 for the beta with a small b, where adjacent
    /// doubles move the survival by more than 1e-12 of itself (measured here: from p = 0.95 at b = 0.5 to
    /// p = 1 − 1e-10 at b = 4 for a up to 100, and at b = 12 with a = 500), the result is within one double of the
    /// root. Outside the domain of <c>cdf</c> the round trip carries the errors <c>cdf</c> states there, and for the
    /// beta and F with a shape or dof below 0.5 also the rounding near 1 of the reflected inverse (measured here
    /// 1.5e-9 for the beta at shape 0.05, 3.7e-4 for the F at 0.05 degrees of freedom and 9.7e-12 for the t at 1e5
    /// degrees of freedom). The gamma and chi-squared invert P with <c>Special.gammaLowerRegularizedInverse</c> and
    /// above p = ½ refine that by at most 8 Newton steps on ln Q; at shapes far below the tested ones, where the root
    /// underflows with p within about 1e-15 of 1, the result is that inverse's floor, 5e-324 · scale (measured here at
    /// shape 1e-20). The beta, t and F map from
    /// <c>Special.betaRegularizedInverse</c> at p, or at 1 − p with the shapes reflected. Cost: at most 100
    /// evaluations of the incomplete beta function for the beta, t and F, and at most 100 of P and 8 of Q for the gamma
    /// and chi-squared (from documentation: the inverses in <c>Special</c>).
    /// </summary>
    let quantile (d: Continuous) (p: float) : float =
        if Double.IsNaN p then
            nan
        else
            Check.probability (nameof p) p
            let struct (lower, upper) = support d

            if p = 0.0 then
                lower
            elif p = 1.0 then
                upper
            else
                match d with
                | Normal (mean, sd) -> mean + sd * normalQuantile p
                | LogNormal (mu, sigma) -> exp (mu + sigma * normalQuantile p)
                | Uniform (lower, upper) -> lower + (upper - lower) * p
                | Exponential rate -> exponentialQuantile p / rate
                | Cauchy (location, scale) -> location + scale * cauchyQuantile p
                | Weibull (shape, scale) -> scale * (exponentialQuantile p ** (1.0 / shape))
                | Logistic (location, scale) -> location + scale * logisticQuantile p
                | Gamma (shape, scale) -> scale * gammaUnitQuantile shape p
                | ChiSquared dof -> 2.0 * gammaUnitQuantile (dof / 2.0) p
                | Beta (a, b) ->
                    if p <= 0.5 then
                        Special.betaRegularizedInverse a b p
                    else
                        1.0 - Special.betaRegularizedInverse b a (1.0 - p)
                | StudentT dof -> if p <= 0.5 then studentTLowerQuantile dof p else -(studentTLowerQuantile dof (1.0 - p))
                | FisherF (d1, d2) -> fisherFQuantile d1 d2 p

    /// <summary>
    /// The mean: +∞ where the integral that defines it diverges to +∞ (the F with d2 ≤ 2) or where it overflows, and NaN
    /// where it is undefined (the Cauchy, and the Student t with dof ≤ 1, whose integral diverges both ways).
    /// </summary>
    let mean (d: Continuous) : float =
        match d with
        | Normal (mean, _) -> mean
        | LogNormal (mu, sigma) -> exp (mu + sigma * sigma / 2.0)
        | Uniform (lower, upper) -> lower + (upper - lower) / 2.0
        | Exponential rate -> 1.0 / rate
        | Cauchy _ -> nan
        | Weibull (shape, scale) -> scale * Special.gamma (1.0 + 1.0 / shape)
        | Logistic (location, _) -> location
        | Gamma (shape, scale) -> shape * scale
        | ChiSquared dof -> dof
        | Beta (a, b) -> a / (a + b)
        | StudentT dof -> if dof > 1.0 then 0.0 else nan
        | FisherF (_, d2) -> if d2 > 2.0 then d2 / (d2 - 2.0) else infinity

    // zeta(n) for n = 2..30, printed by `python -I tests/reference/special.py zeta`.
    let private zetaValues: float[] =
        [|
            1.6449340668482264
            1.2020569031595942
            1.0823232337111381
            1.03692775514337
            1.0173430619844492
            1.008349277381923
            1.0040773561979444
            1.0020083928260821
            1.000994575127818
            1.0004941886041194
            1.000246086553308
            1.0001227133475785
            1.0000612481350588
            1.000030588236307
            1.0000152822594086
            1.0000076371976379
            1.000003817293265
            1.0000019082127165
            1.0000009539620338
            1.0000004769329869
            1.0000002384505027
            1.000000119219926
            1.000000059608189
            1.0000000298035034
            1.0000000149015549
            1.0000000074507118
            1.000000003725334
            1.0000000018626598
            1.0000000009313275
        |]

    // The coefficients (-1)^n zeta(n) (2^n - 2) / n of S(e) = ln Gamma(1 + 2e) - 2 ln Gamma(1 + e), n = 2..30.
    let private logRatioCoefficients: float[] =
        Array.mapi
            (fun i z ->
                let n = i + 2
                (if n % 2 = 0 then 1.0 else -1.0) * z * (2.0 ** float n - 2.0) / float n)
            zetaValues

    // S(e) by Horner's rule from the top term: the Euler constant terms of the two log-gamma values cancel, so the
    // series starts at e^2 pi^2 / 6. It converges for e < 1/2.
    let private weibullLogRatio (e: float) : float =
        let mutable s = 0.0

        for i in logRatioCoefficients.Length - 1 .. -1 .. 0 do
            s <- s * e + logRatioCoefficients[i]

        s * e * e

    /// <summary>
    /// The variance: +∞ where the integral that defines it diverges to +∞ (the Student t with 1 &lt; dof ≤ 2, the F with
    /// 2 &lt; d2 ≤ 4) or where it overflows, and NaN where it is undefined (the Cauchy, the Student t with dof ≤ 1 and
    /// the F with d2 ≤ 2, whose means are undefined or infinite). The Weibull's is
    /// scale² (Γ(1 + 2/shape) − Γ(1 + 1/shape)²) for shape below 10, and scale² Γ(1 + ε)² expm1(S) with ε = 1/shape and
    /// S = Σ over n = 2..30 of (−1)ⁿ ζ(n) (2ⁿ − 2) εⁿ / n from shape 10 on, where the difference would cancel
    /// (the Euler constant terms of ln Γ(1 + 2ε) − 2 ln Γ(1 + ε) cancel exactly; the series is from documentation:
    /// the expansion of ln Γ(1 + z) in ζ(n)). Relative error at most 1e-13 over shape from 10 to 1e8, and at shape 2 within 1e-14, pinned by the tests "weibull variance against
    /// the reference at large shape" and "weibull closed forms" against <c>tests/reference/special.py weibull</c>
    /// (measured here). The ζ(n) come from <c>tests/reference/special.py zeta</c>.
    /// </summary>
    let variance (d: Continuous) : float =
        match d with
        | Normal (_, sd) -> sd * sd
        | LogNormal (mu, sigma) -> Special.expm1 (sigma * sigma) * exp (2.0 * mu + sigma * sigma)
        | Uniform (lower, upper) -> (upper - lower) * (upper - lower) / 12.0
        | Exponential rate -> 1.0 / (rate * rate)
        | Cauchy _ -> nan
        | Weibull (shape, scale) ->
            if shape >= 10.0 then
                let e = 1.0 / shape
                let g = Special.gamma (1.0 + e)
                scale * scale * g * g * Special.expm1 (weibullLogRatio e)
            else
                let g1 = Special.gamma (1.0 + 1.0 / shape)
                let g2 = Special.gamma (1.0 + 2.0 / shape)

                if Double.IsInfinity g1 || Double.IsInfinity g2 then infinity else scale * scale * (g2 - g1 * g1)
        | Logistic (_, scale) -> scale * scale * Math.PI * Math.PI / 3.0
        | Gamma (shape, scale) -> shape * scale * scale
        | ChiSquared dof -> 2.0 * dof
        | Beta (a, b) ->
            let s = a + b
            a / s * (b / s) / (s + 1.0)
        | StudentT dof ->
            if dof > 2.0 then dof / (dof - 2.0)
            elif dof > 1.0 then infinity
            else nan
        | FisherF (d1, d2) ->
            if d2 > 4.0 then
                let m = d2 / (d2 - 2.0)
                2.0 * m * m * (d1 + d2 - 2.0) / (d1 * (d2 - 4.0))
            elif d2 > 2.0 then
                infinity
            else
                nan

    /// <summary>The square root of the variance: +∞ where the variance is +∞ and NaN where it is undefined.</summary>
    let standardDeviation (d: Continuous) : float = sqrt (variance d)

    // A uniform value in (0, 1) and the next generator: draws again at 0.
    let rec private openUniform (g: Generator) : struct (float * Generator) =
        let struct (u, next) = Generator.uniform g
        if u > 0.0 then struct (u, next) else openUniform next

    /// <summary>
    /// A value of the distribution and the next generator. The normal, log-normal, uniform, exponential, gamma and beta
    /// use the <c>Generator</c> functions of the same name, and the chi-squared <c>Generator.gamma</c> with shape dof/2
    /// and scale 2; the Cauchy, Weibull and logistic invert a uniform value, drawing again at 0 where the inversion
    /// needs a value in (0, 1). The Student t is Z / √(V/dof) and the F (V₁/d1) / (V₂/d2), with Z from
    /// <c>Generator.normal</c> and V, V₁, V₂ chi-squared draws; all are exact in distribution. Below shape 1 a gamma
    /// draw is exactly 0 for about e^(−745 · shape) of the draws, and at small shapes a beta draw can be exactly 0 or 1
    /// (from documentation: <c>Generator.gamma</c> and <c>Generator.beta</c>); where a chi-squared draw in a denominator is 0
    /// the t draw is ±∞ and the F draw +∞, or NaN where both F draws are 0.
    /// </summary>
    let sample (d: Continuous) (g: Generator) : struct (float * Generator) =
        match d with
        | Normal (mean, sd) ->
            let struct (z, next) = Generator.normal g
            struct (mean + sd * z, next)
        | LogNormal (mu, sigma) ->
            let struct (z, next) = Generator.normal g
            struct (exp (mu + sigma * z), next)
        | Uniform (lower, upper) ->
            let struct (u, next) = Generator.uniform g
            struct (lower + (upper - lower) * u, next)
        | Exponential rate -> Generator.exponential rate g
        | Cauchy (location, scale) ->
            let struct (u, next) = openUniform g
            struct (location + scale * cauchyQuantile u, next)
        | Weibull (shape, scale) ->
            let struct (e, next) = Generator.exponential 1.0 g
            struct (scale * (e ** (1.0 / shape)), next)
        | Logistic (location, scale) ->
            let struct (u, next) = openUniform g
            struct (location + scale * logisticQuantile u, next)
        | Gamma (shape, scale) -> Generator.gamma shape scale g
        | ChiSquared dof -> Generator.gamma (dof / 2.0) 2.0 g
        | Beta (a, b) -> Generator.beta a b g
        | StudentT dof ->
            let struct (z, g1) = Generator.normal g
            let struct (v, g2) = Generator.gamma (dof / 2.0) 2.0 g1
            struct (z / sqrt (v / dof), g2)
        | FisherF (d1, d2) ->
            let struct (v1, g1) = Generator.gamma (d1 / 2.0) 2.0 g
            let struct (v2, g2) = Generator.gamma (d2 / 2.0) 2.0 g1
            struct (v1 / d1 / (v2 / d2), g2)

    /// <summary>
    /// count values, drawn as count successive calls of <c>sample</c> would, and the next generator. Raises
    /// ArgumentOutOfRangeException when count is negative. Cost: O(count).
    /// </summary>
    let samples (d: Continuous) (count: int) (g: Generator) : struct (float[] * Generator) =
        if count < 0 then
            raise (ArgumentOutOfRangeException(nameof count, "count must be zero or more."))

        let out: float[] = Array.zeroCreate count
        let mutable current = g

        for i in 0 .. count - 1 do
            let struct (x, next) = sample d current
            out[i] <- x
            current <- next

        struct (out, current)

    /// <summary>
    /// The family and parameters as <c>Name(parameter = value, ...)</c>, each value by F#'s <c>string</c>, for example
    /// <c>Weibull(shape = 1.5, scale = 2)</c>. The text of a parameter whose magnitude lies outside [1e-5, 1e15) may
    /// differ between .NET and JavaScript.
    /// </summary>
    let describe (d: Continuous) : string =
        match d with
        | Normal (mean, sd) -> "Normal(mean = " + string mean + ", sd = " + string sd + ")"
        | LogNormal (mu, sigma) -> "LogNormal(mu = " + string mu + ", sigma = " + string sigma + ")"
        | Uniform (lower, upper) -> "Uniform(lower = " + string lower + ", upper = " + string upper + ")"
        | Exponential rate -> "Exponential(rate = " + string rate + ")"
        | Cauchy (location, scale) -> "Cauchy(location = " + string location + ", scale = " + string scale + ")"
        | Weibull (shape, scale) -> "Weibull(shape = " + string shape + ", scale = " + string scale + ")"
        | Logistic (location, scale) -> "Logistic(location = " + string location + ", scale = " + string scale + ")"
        | Gamma (shape, scale) -> "Gamma(shape = " + string shape + ", scale = " + string scale + ")"
        | ChiSquared dof -> "ChiSquared(dof = " + string dof + ")"
        | Beta (a, b) -> "Beta(a = " + string a + ", b = " + string b + ")"
        | StudentT dof -> "StudentT(dof = " + string dof + ")"
        | FisherF (d1, d2) -> "FisherF(d1 = " + string d1 + ", d2 = " + string d2 + ")"
