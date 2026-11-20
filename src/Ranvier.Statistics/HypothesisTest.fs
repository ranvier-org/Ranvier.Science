namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>The side of the null value on which a test looks for evidence.</summary>
type Alternative =
    /// The parameter differs from its null value in either direction.
    | TwoSided
    /// The parameter is below its null value.
    | Less
    /// The parameter is above its null value.
    | Greater

/// <summary>The outcome of a hypothesis test.</summary>
/// <remarks>
/// A field that does not apply to the test, or that is undefined for the data, is NaN: DenominatorDegreesOfFreedom
/// outside the F tests, both degrees of freedom for the z test, and DegreesOfFreedom for Welch's t at a zero
/// standard error.
/// </remarks>
type TestResult =
    {
        /// The test statistic.
        Statistic: float
        /// The degrees of freedom of a t test, or the numerator degrees of freedom of an F test.
        DegreesOfFreedom: float
        /// The denominator degrees of freedom of an F test.
        DenominatorDegreesOfFreedom: float
        /// The probability under the null hypothesis of a statistic at least as extreme, on the alternative's side.
        PValue: float
    }

module internal PValue =
    /// Less: the cdf at the statistic; Greater: the survival; TwoSided: min 1 (2 · min cdf survival). NaN statistic
    /// gives NaN. Each tail is computed directly, so Less and Greater sum to 1 within about 1e-16.
    let continuous (alternative: Alternative) (d: Continuous) (statistic: float) : float =
        if Double.IsNaN statistic then
            nan
        else
            match alternative with
            | Less -> Continuous.cdf d statistic
            | Greater -> Continuous.survival d statistic
            | TwoSided -> Math.Min(1.0, 2.0 * Math.Min(Continuous.cdf d statistic, Continuous.survival d statistic))

    // The p-value at an infinite statistic, which every distribution used here puts wholly on one side.
    let private infinite (alternative: Alternative) (statistic: float) : float =
        match alternative with
        | TwoSided -> 0.0
        | Less -> if statistic > 0.0 then 1.0 else 0.0
        | Greater -> if statistic > 0.0 then 0.0 else 1.0

    // The p-value of a statistic whose distribution is built only once the statistic and the degrees of freedom allow
    // it: a NaN statistic or NaN degrees of freedom gives NaN, an infinite statistic needs no distribution.
    let private settle (alternative: Alternative) (statistic: float) (dof: float) (build: unit -> Continuous) : float =
        if Double.IsNaN statistic then nan
        elif Double.IsInfinity statistic then infinite alternative statistic
        elif Double.IsNaN dof then nan
        else continuous alternative (build ()) statistic

    let student (alternative: Alternative) (dof: float) (statistic: float) : float =
        settle alternative statistic dof (fun () -> Continuous.studentT dof)

    let fisher (alternative: Alternative) (d1: float) (d2: float) (statistic: float) : float =
        settle alternative statistic (d1 + d2) (fun () -> Continuous.fisherF d1 d2)

    let normal (alternative: Alternative) (statistic: float) : float =
        settle alternative statistic 0.0 (fun () -> Continuous.normal 0.0 1.0)

module internal TestSupport =
    let nanResult: TestResult =
        {
            Statistic = nan
            DegreesOfFreedom = nan
            DenominatorDegreesOfFreedom = nan
            PValue = nan
        }

    let nullValue (name: string) (x: float) : unit =
        if Double.IsNaN x || Double.IsInfinity x then
            raise (ArgumentOutOfRangeException(name, name + " must be finite."))

    // The result of a t statistic on dof degrees of freedom.
    let tResult (alternative: Alternative) (statistic: float) (dof: float) : TestResult =
        {
            Statistic = statistic
            DegreesOfFreedom = dof
            DenominatorDegreesOfFreedom = nan
            PValue = PValue.student alternative dof statistic
        }

/// <summary>Hypothesis tests on the moments of one or more samples, for normally distributed observations.</summary>
/// <remarks>
/// Each test takes the <c>Moments</c> of its samples. A count too small for the test gives a result with every field
/// NaN, and NaN data gives a NaN statistic and p-value; neither raises. A zero spread gives an infinite statistic and
/// a p-value of 0 where the means differ (a non-zero difference over 0), and a NaN statistic where they agree. The
/// p-value is a tail probability of the null distribution with the relative error of <c>Continuous.cdf</c> and
/// <c>Continuous.survival</c>, at most 1e-12 where it is at least 1e-300; each function names the test that pins it
/// against the script <c>tests/reference/special.py</c>, section parametric.
/// </remarks>
module HypothesisTest =
    /// <summary>
    /// The one-sample t test of the mean against the null value: t = (x̄ − μ₀)/(s/√n) with s the sample standard
    /// deviation (divisor n − 1), on n − 1 degrees of freedom. Raises ArgumentOutOfRangeException when the null value
    /// is not finite (NaN raises too). A count below 2 gives every field NaN. A constant sample gives ±∞
    /// (p-value 0 two-sided) at a mean other than the null value and NaN at the null value. Relative error of the
    /// p-value at most 1e-12 over the samples of the test "one-sample t against the reference" (R's
    /// <c>datasets::sleep</c> group 1 and the paired differences), which pins it.
    /// </summary>
    let oneSampleT (alternative: Alternative) (mu0: float) (m: Moments) : TestResult =
        TestSupport.nullValue (nameof mu0) mu0
        let n = Moments.count m

        if n < 2.0 then
            TestSupport.nanResult
        else
            let t = (Moments.mean m - mu0) / (Moments.sampleStandardDeviation m / sqrt n)
            TestSupport.tResult alternative t (n - 1.0)

    /// <summary>
    /// The one-sample z test of the mean for a known standard deviation: z = (x̄ − μ₀)/(σ/√n), standard normal under
    /// the null. Raises ArgumentOutOfRangeException unless σ is finite and greater than 0 and the null value is
    /// finite. A count of 0 gives every field NaN. Both degrees-of-freedom fields are NaN. Relative error of the
    /// p-value at most 1e-12 over the sample of the test "one-sample z against the reference", which pins it.
    /// </summary>
    let oneSampleZ (alternative: Alternative) (mu0: float) (sigma: float) (m: Moments) : TestResult =
        TestSupport.nullValue (nameof mu0) mu0
        Check.finitePositive (nameof sigma) sigma
        let n = Moments.count m

        if n < 1.0 then
            TestSupport.nanResult
        else
            let z = (Moments.mean m - mu0) / (sigma / sqrt n)

            {
                Statistic = z
                DegreesOfFreedom = nan
                DenominatorDegreesOfFreedom = nan
                PValue = PValue.normal alternative z
            }

    /// <summary>
    /// Welch's t test of equal means, the parameter being mean a − mean b: t = d/√(s²ₐ/nₐ + s²ᵦ/nᵦ) on the
    /// Welch–Satterthwaite degrees of freedom ν = (s²ₐ/nₐ + s²ᵦ/nᵦ)² / ((s²ₐ/nₐ)²/(nₐ − 1) + (s²ᵦ/nᵦ)²/(nᵦ − 1)),
    /// which does not assume equal variances. A count below 2 in either sample gives every field NaN. A zero
    /// standard error (both samples constant) makes ν NaN; the statistic is then ±∞ with p-value 0 two-sided when
    /// the means differ, and NaN with p-value NaN when they agree. Relative error of the p-value at most 1e-12 over
    /// the samples of the test "Welch and pooled t against the reference", which pins it.
    /// </summary>
    let welchT (alternative: Alternative) (a: Moments) (b: Moments) : TestResult =
        let na = Moments.count a
        let nb = Moments.count b

        if na < 2.0 || nb < 2.0 then
            TestSupport.nanResult
        else
            let qa = Moments.sampleVariance a / na
            let qb = Moments.sampleVariance b / nb
            let s = qa + qb
            let d = Moments.mean a - Moments.mean b

            let nu =
                if s > 0.0 && s < infinity then
                    let wa = qa / s
                    let wb = qb / s
                    1.0 / (wa * wa / (na - 1.0) + wb * wb / (nb - 1.0))
                else
                    nan

            TestSupport.tResult alternative (d / sqrt s) nu

    /// <summary>
    /// The pooled-variance t test of equal means, the parameter being mean a − mean b: t = d/(s_p·√(1/nₐ + 1/nᵦ)),
    /// s²_p = ((nₐ − 1)s²ₐ + (nᵦ − 1)s²ᵦ)/(nₐ + nᵦ − 2), on nₐ + nᵦ − 2 degrees of freedom; it assumes equal
    /// variances. A sample of one observation adds 0 to the pooled sum of squares, so it is defined when one sample
    /// holds a single observation and the other two or more, as in <c>DifferenceInterval.pooled</c>; counts of (1, 1)
    /// or a count of 0 give every field NaN. Two constant samples give a statistic of
    /// ±∞ (p-value 0 two-sided) when the means differ and NaN when they agree. Relative error of the p-value at most
    /// 1e-12 over the samples of the test "Welch and pooled t against the reference", which pins it.
    /// </summary>
    let pooledT (alternative: Alternative) (a: Moments) (b: Moments) : TestResult =
        let na = Moments.count a
        let nb = Moments.count b

        if na < 1.0 || nb < 1.0 || na + nb < 3.0 then
            TestSupport.nanResult
        else
            let sumOfSquares (n: float) (m: Moments) : float =
                if n < 2.0 then 0.0 else (n - 1.0) * Moments.sampleVariance m

            let dof = na + nb - 2.0
            let variance = (sumOfSquares na a + sumOfSquares nb b) / dof
            let d = Moments.mean a - Moments.mean b
            TestSupport.tResult alternative (d / sqrt (variance * (1.0 / na + 1.0 / nb))) dof

    /// <summary>
    /// The F test of equal variances: F = s²ₐ/s²ᵦ on (nₐ − 1, nᵦ − 1) degrees of freedom, the parameter being
    /// σ²ₐ/σ²ᵦ against 1; Less means the ratio is below 1. It assumes normal observations and is sensitive to
    /// departures from normality. A count below 2 in either sample gives every field NaN. A constant b gives an
    /// infinite statistic (p-value 0 two-sided) where a varies and NaN where a is constant too. Relative error of
    /// the p-value at most 1e-12 over the samples of the test "variance ratio against the reference", which pins it.
    /// </summary>
    let varianceRatio (alternative: Alternative) (a: Moments) (b: Moments) : TestResult =
        let na = Moments.count a
        let nb = Moments.count b

        if na < 2.0 || nb < 2.0 then
            TestSupport.nanResult
        else
            let f = Moments.sampleVariance a / Moments.sampleVariance b

            {
                Statistic = f
                DegreesOfFreedom = na - 1.0
                DenominatorDegreesOfFreedom = nb - 1.0
                PValue = PValue.fisher alternative (na - 1.0) (nb - 1.0) f
            }

    /// <summary>
    /// The one-way analysis of variance of k groups: F = (SSB/(k − 1))/(SSW/(N − k)) with SSB = Σ nᵢ(x̄ᵢ − x̄)²,
    /// SSW = Σ nᵢ·σ²ᵢ (the population variance of each group) and N the total count, on (k − 1, N − k) degrees of
    /// freedom; the p-value is the F survival at the statistic, the test for unequal means. It assumes normal
    /// observations of equal variance. Raises ArgumentException for fewer than two groups. An empty group, or
    /// N = k (no degrees of freedom within groups), gives every field NaN. Constant groups give an infinite
    /// statistic (p-value 0) when their means differ and NaN when they agree. Relative error of the p-value at most
    /// 1e-12 over the groups of the test "one-way ANOVA against the reference", which pins it.
    /// </summary>
    let oneWayAnova (groups: Moments[]) : TestResult =
        if groups.Length < 2 then
            raise (ArgumentException("oneWayAnova needs at least two groups.", nameof groups))

        let k = float groups.Length
        let total = groups |> Array.sumBy Moments.count

        if groups |> Array.exists (fun g -> Moments.count g < 1.0) || total - k < 1.0 then
            TestSupport.nanResult
        else
            let grand = (groups |> Array.sumBy (fun g -> Moments.count g * Moments.mean g)) / total

            let between =
                groups
                |> Array.sumBy (fun g ->
                    let delta = Moments.mean g - grand
                    Moments.count g * delta * delta)

            let within = groups |> Array.sumBy (fun g -> Moments.count g * Moments.variance g)
            let f = (between / (k - 1.0)) / (within / (total - k))

            {
                Statistic = f
                DegreesOfFreedom = k - 1.0
                DenominatorDegreesOfFreedom = total - k
                PValue = PValue.fisher Greater (k - 1.0) (total - k) f
            }
