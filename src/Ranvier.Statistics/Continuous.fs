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

/// <summary>
/// Density, distribution function, survival function, quantile, moments, support and sampling of continuous
/// distributions.
/// </summary>
/// <remarks>
/// <c>cdf</c>, <c>survival</c> and <c>quantile</c> state their error bound over the parameter values the tests
/// cover. The bounds hold on .NET and on JavaScript separately; the two targets can differ in the last digits.
/// A NaN argument gives NaN. <c>density</c> and <c>logDensity</c> carry no bound beyond the accuracy of the
/// <c>exp</c>, <c>log</c> and power functions they call.
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

    /// <summary>
    /// The probability density at x: 0 outside the support, NaN at NaN, and +∞ at 0 for a Weibull of shape below 1.
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

    /// <summary>
    /// The natural logarithm of the density at x: −∞ outside the support, NaN at NaN, and +∞ at 0 for a Weibull of
    /// shape below 1.
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

    /// <summary>The bounds of the support: (−∞, ∞) for the normal, Cauchy and logistic, (0, ∞) for the log-normal,
    /// exponential and Weibull, and (lower, upper) for the uniform.</summary>
    let support (d: Continuous) : struct (float * float) =
        match d with
        | Normal _
        | Cauchy _
        | Logistic _ -> struct (-infinity, infinity)
        | LogNormal _
        | Exponential _
        | Weibull _ -> struct (0.0, infinity)
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

    /// <summary>The mean: NaN for the Cauchy, +∞ where it overflows.</summary>
    let mean (d: Continuous) : float =
        match d with
        | Normal (mean, _) -> mean
        | LogNormal (mu, sigma) -> exp (mu + sigma * sigma / 2.0)
        | Uniform (lower, upper) -> lower + (upper - lower) / 2.0
        | Exponential rate -> 1.0 / rate
        | Cauchy _ -> nan
        | Weibull (shape, scale) -> scale * Special.gamma (1.0 + 1.0 / shape)
        | Logistic (location, _) -> location

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
    /// The variance: NaN for the Cauchy, +∞ where it overflows. The Weibull's is
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

    /// <summary>The square root of the variance: NaN for the Cauchy.</summary>
    let standardDeviation (d: Continuous) : float = sqrt (variance d)

    // A uniform value in (0, 1) and the next generator: draws again at 0.
    let rec private openUniform (g: Generator) : struct (float * Generator) =
        let struct (u, next) = Generator.uniform g
        if u > 0.0 then struct (u, next) else openUniform next

    /// <summary>
    /// A value of the distribution and the next generator. The normal, log-normal, uniform and exponential use the
    /// <c>Generator</c> functions of the same name; the Cauchy, Weibull and logistic invert a uniform value, drawing
    /// again at 0 where the inversion needs a value in (0, 1).
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
