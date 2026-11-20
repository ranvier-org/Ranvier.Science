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
    /// gives NaN. Each tail is computed directly, so Less + Greater differs from 1 by the two tails' rounding: 0 measured here
    /// for the t and normal distributions over the seeded samples of the test "Less + Greater is 1 on seeded samples",
    /// and 2.2e-15 for the F distribution (measured by the reviewer over 20000 random pairs on .NET).
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

    // The relative slack of the two-sided exact rule: R's binom.test and fisher.test accept a mass up to the observed
    // one times 1 + 1e-7.
    let private slack = 1.0 + 1e-7

    // The p-value that sums every mass at most the observed one times the slack, over the support of d, capped at 1.
    let private twoSidedByMass (d: Discrete) (x: int) : float =
        let struct (lower, upper) = Discrete.support d
        let limit = Discrete.mass d x * slack
        let mutable total = 0.0

        for k in lower..upper do
            let m = Discrete.mass d k

            if m <= limit then
                total <- total + m

        Math.Min(1.0, total)

    // R's two-sided binom.test: the cdf up to x (or the survival from x) plus the far tail, whose outcomes are the y
    // most distant ones of mass at most the observed one times the slack.
    let private twoSidedBinomial (d: Discrete) (x: int) (trials: int) (p0: float) : float =
        let m = float trials * p0
        let limit = Discrete.mass d x * slack

        if float x = m then
            1.0
        elif float x < m then
            let mutable y = 0

            for i in int (Math.Ceiling m) .. trials do
                if Discrete.mass d i <= limit then
                    y <- y + 1

            Math.Min(1.0, Discrete.cdf d x + Discrete.survival d (trials - y))
        else
            let mutable y = 0

            for i in 0 .. int (Math.Floor m) do
                if Discrete.mass d i <= limit then
                    y <- y + 1

            Math.Min(1.0, Discrete.cdf d (y - 1) + Discrete.survival d (x - 1))

    let private countResult (statistic: float) (p: float) : TestResult =
        {
            Statistic = statistic
            DegreesOfFreedom = nan
            DenominatorDegreesOfFreedom = nan
            PValue = p
        }

    // A chi-squared result of the statistic on dof degrees of freedom: the survival at the statistic.
    let private chiSquaredResult (statistic: float) (dof: float) : TestResult =
        {
            Statistic = statistic
            DegreesOfFreedom = dof
            DenominatorDegreesOfFreedom = nan
            PValue = PValue.continuous Greater (Continuous.chiSquared dof) statistic
        }

    let private finiteNotNegative (name: string) (x: float) : unit =
        if not (x >= 0.0 && x < infinity) then
            raise (ArgumentOutOfRangeException(name, name + " must be finite and not negative."))

    /// <summary>
    /// The exact binomial test of the success probability against p0, for the given successes in the given trials:
    /// Less is P(X ≤ x), Greater is P(X ≥ x) for X ~ Binomial(trials, p0), and TwoSided follows R's binom.test, the
    /// rule of its default "minlike" method: x equal to trials · p0 gives 1; otherwise the tail on x's side up to x
    /// plus the far tail made of the outcomes, counted from the end, whose mass is at most the mass at x times
    /// 1 + 1e-7 (the slack that lets tied outcomes count, as 7 and 13 of 20 at ½), each tail by <c>Discrete.cdf</c>
    /// or <c>Discrete.survival</c>, capped at 1 (read from R's binom.test.R at
    /// https://raw.githubusercontent.com/wch/r-source/trunk/src/library/stats/R/binom.test.R, from documentation;
    /// R compares logarithms of the masses where this compares the masses). Every mass comes from
    /// <c>Discrete.mass</c>, the observed one included, so equal masses compare equal. At p0 of 0 or 1 the p-value is
    /// 1 at the only possible count and 0 elsewhere. Statistic is the successes; both degrees-of-freedom fields are
    /// NaN. Raises ArgumentOutOfRangeException unless 0 ≤ successes ≤ trials and 0 ≤ p0 ≤ 1 (NaN raises). Cost:
    /// O(trials) masses for TwoSided, else one cdf. Relative error of the p-value at most 1e-12 over the rows of the
    /// test "exact binomial against exact fractions", which pins it against the script
    /// <c>tests/reference/special.py</c>, section counts.
    /// </summary>
    let binomial (alternative: Alternative) (successes: int) (trials: int) (p0: float) : TestResult =
        if trials < 0 then
            raise (ArgumentOutOfRangeException(nameof trials, "trials must not be negative."))

        if successes < 0 || successes > trials then
            raise (ArgumentOutOfRangeException(nameof successes, "successes must lie between 0 and trials."))

        Check.probability (nameof p0) p0
        let d = Discrete.binomial trials p0

        let p =
            match alternative with
            | Less -> Discrete.cdf d successes
            | Greater -> Discrete.survival d (successes - 1)
            | TwoSided -> twoSidedBinomial d successes trials p0

        countResult (float successes) p

    /// <summary>
    /// Pearson's chi-squared goodness-of-fit test of the observed counts against the expected ones: the expected
    /// values are rescaled to the observed total, χ² = Σ (o − e)²/e, on k − 1 degrees of freedom for k cells, and the
    /// p-value is the χ² survival at the statistic (there is no one-sided form). Raises ArgumentException unless the
    /// two arrays have the same length, at least 2; raises ArgumentOutOfRangeException for an observed count that is
    /// negative or not finite and an expected value that is not finite and greater than 0. An observed total of 0
    /// gives every field NaN. The χ² approximation is poor where an expected count is below about 5 (general
    /// knowledge). Relative error of the p-value at most 1e-12 over the counts of the test "goodness of fit against
    /// Mendel's counts", which pins it, with the statistic, against the script <c>tests/reference/special.py</c>,
    /// section counts.
    /// </summary>
    let chiSquaredGoodnessOfFit (observed: float[]) (expected: float[]) : TestResult =
        if observed.Length <> expected.Length then
            raise (ArgumentException("observed and expected must have the same length.", nameof expected))

        if observed.Length < 2 then
            raise (ArgumentException("A goodness-of-fit test needs at least two cells.", nameof observed))

        for o in observed do
            finiteNotNegative (nameof observed) o

        for e in expected do
            Check.finitePositive (nameof expected) e

        let totalObserved = Array.sum observed

        if totalObserved <= 0.0 then
            TestSupport.nanResult
        else
            let scale = totalObserved / Array.sum expected
            let mutable statistic = 0.0

            for i in 0 .. observed.Length - 1 do
                let e = expected[i] * scale
                statistic <- statistic + (observed[i] - e) * (observed[i] - e) / e

            chiSquaredResult statistic (float (observed.Length - 1))

    /// <summary>
    /// Pearson's chi-squared test of independence on a table of counts, one array per row: the expected count is
    /// row total · column total / grand total, χ² = Σ (o − e)²/e without a continuity correction, on (r − 1)(c − 1)
    /// degrees of freedom, and the p-value is the χ² survival at the statistic. Raises ArgumentException for fewer
    /// than 2 rows or 2 columns or rows of unequal length; raises ArgumentOutOfRangeException for an entry that is
    /// negative or not finite. A zero row or column total gives every field NaN. The χ² approximation is poor where
    /// an expected count is below about 5 (general knowledge). Relative error of the p-value at most 1e-12 over the
    /// tables of the test "independence against the reference", which pins it against the script
    /// <c>tests/reference/special.py</c>, section counts.
    /// </summary>
    let chiSquaredIndependence (table: float[][]) : TestResult =
        if table.Length < 2 then
            raise (ArgumentException("A table needs at least two rows.", nameof table))

        let columns = table[0].Length

        if columns < 2 then
            raise (ArgumentException("A table needs at least two columns.", nameof table))

        for row in table do
            if row.Length <> columns then
                raise (ArgumentException("Every row of the table must have the same length.", nameof table))

            for o in row do
                finiteNotNegative (nameof table) o

        let rowTotals = table |> Array.map Array.sum
        let columnTotals = Array.init columns (fun j -> table |> Array.sumBy (fun row -> row[j]))
        let total = Array.sum rowTotals

        if total <= 0.0 || Array.exists (fun t -> t <= 0.0) rowTotals || Array.exists (fun t -> t <= 0.0) columnTotals then
            TestSupport.nanResult
        else
            let mutable statistic = 0.0

            for i in 0 .. table.Length - 1 do
                for j in 0 .. columns - 1 do
                    let e = rowTotals[i] * columnTotals[j] / total
                    statistic <- statistic + (table[i][j] - e) * (table[i][j] - e) / e

            chiSquaredResult statistic (float ((table.Length - 1) * (columns - 1)))

    /// <summary>
    /// Fisher's exact test on the 2 × 2 table [[a, b], [c, d]] of counts: under independence, given the margins,
    /// X = a is Hypergeometric(a + b + c + d, a + b, a + c). Less is P(X ≤ a), Greater is P(X ≥ a), and TwoSided
    /// sums the masses of every outcome whose mass is at most the mass at a times 1 + 1e-7, capped at 1, the rule of
    /// R's fisher.test (read from fisher.test.R at
    /// https://raw.githubusercontent.com/wch/r-source/trunk/src/library/stats/R/fisher.test.R, from documentation;
    /// R computes the masses by a different route, so its sum can differ in the last digits). Every mass comes from
    /// <c>Discrete.mass</c>, the observed one included. Statistic is the sample odds ratio ad/(bc): +∞ where
    /// bc = 0 &lt; ad, NaN where ad and bc are both 0 (R reports the conditional maximum likelihood estimate
    /// instead). Both degrees-of-freedom fields are NaN. Raises ArgumentOutOfRangeException for a negative cell or a
    /// total above Int32.MaxValue. Cost: O(support) masses for TwoSided, else one cdf. Relative error of the p-value
    /// at most 1e-12 over the tables of the tests "Fisher's tea-tasting table" and "Fisher against exact fractions",
    /// which pin it against the closed form of the tea table and the script <c>tests/reference/special.py</c>,
    /// section counts.
    /// </summary>
    let fisherExact (alternative: Alternative) (a: int) (b: int) (c: int) (d: int) : TestResult =
        if a < 0 then
            raise (ArgumentOutOfRangeException(nameof a, "a must not be negative."))

        if b < 0 then
            raise (ArgumentOutOfRangeException(nameof b, "b must not be negative."))

        if c < 0 then
            raise (ArgumentOutOfRangeException(nameof c, "c must not be negative."))

        if d < 0 then
            raise (ArgumentOutOfRangeException(nameof d, "d must not be negative."))

        if float a + float b + float c + float d > float Int32.MaxValue then
            raise (ArgumentOutOfRangeException(nameof a, "The total of the table must not exceed Int32.MaxValue."))

        let dist = Discrete.hypergeometric (a + b + c + d) (a + b) (a + c)

        let p =
            match alternative with
            | Less -> Discrete.cdf dist a
            | Greater -> Discrete.survival dist (a - 1)
            | TwoSided -> twoSidedByMass dist a

        let ad = float a * float d
        let bc = float b * float c

        let oddsRatio =
            if bc > 0.0 then ad / bc
            elif ad > 0.0 then infinity
            else nan

        countResult oddsRatio p
