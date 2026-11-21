namespace Ranvier.Statistics

open System
open Ranvier.Numerics

module internal IntervalSupport =
    let nanInterval: Interval = { Lower = nan; Upper = nan }

    let around (centre: float) (half: float) : Interval =
        { Lower = centre - half
          Upper = centre + half }

    // The upper α/2 point of the standard normal, as minus the lower point so that no 1 − α/2 is rounded.
    let normalUpper (alpha: float) : float =
        -(Continuous.quantile (Continuous.normal 0.0 1.0) (alpha / 2.0))

    // The upper α/2 point of Student's t with dof degrees of freedom, dof finite and positive.
    let tUpper (alpha: float) (dof: float) : float =
        -(Continuous.quantile (Continuous.studentT dof) (alpha / 2.0))

    // a / q, 0 where a is 0, so that a zero spread stays zero beside a quantile that underflowed to 0.
    let divide (a: float) (q: float) : float = if a = 0.0 then 0.0 else a / q

/// <summary>Confidence intervals for a mean from the moments of a sample.</summary>
/// <remarks>
/// Both raise ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 (NaN raises), before they look at the sample. A
/// count too small for the interval gives NaN bounds; NaN data gives NaN bounds. Each bound's relative error is that
/// of the quantile it uses, <c>Continuous.quantile</c> of the normal and Student t, over the samples the test file
/// pins; the figure is stated on each function. Measured here on .NET (Release), by an F# script that compared each
/// row of those tests with its reference, the largest relative error of a bound was 1.5e-16. The bound is relative to
/// the mean and variance the moments hold; <c>Moments</c> adds its own loss for data with an offset, a relative error
/// up to about ε·|mean|/sd in the variance (ε = 2⁻⁵²; see <c>Moments.variance</c>).
/// </remarks>
module MeanInterval =
    /// <summary>
    /// The interval mean ± t · s / √n with t the Student t quantile at 1 − α/2 for n − 1 degrees of freedom and s the
    /// sample standard deviation (divisor n − 1). It holds the mean with probability 1 − α for independent normal
    /// observations of unknown variance. NaN bounds for a count below 2. A constant sample whose mean is at least
    /// 2⁻⁴⁸⁰ (about 3.2e-145) in magnitude gives [mean, mean]; a spread below about 1.5e-154 or above about 1.3e154,
    /// where the moments' sum of squares leaves the normal doubles, gives NaN bounds, and so does a constant sample
    /// nearer 0 but not exactly 0, where an underflowed spread cannot be told from none (test "a spread the moments
    /// cannot resolve gives NaN bounds, not a zero-width interval"); a sample of zeros is constant and gives [0, 0].
    /// Relative error at most 1e-12 over the samples of the tests "the
    /// Student interval against the reference" (ten observations, α 0.05 and 0.01), which pins it, and "small samples
    /// give NaN bounds" at the edge; the coverage of the 95% interval on 2,000 seeded samples is pinned by "the Student
    /// interval covers at its level".
    /// </summary>
    let student (alpha: float) (m: Moments) : Interval =
        Check.alpha alpha
        let n = Moments.count m

        if n < 2.0 then
            IntervalSupport.nanInterval
        else
            let half =
                IntervalSupport.tUpper alpha (n - 1.0) * sqrt (Check.resolvedVariance Moments.sampleVariance m) / sqrt n
            IntervalSupport.around (Moments.mean m) half

    /// <summary>
    /// The interval mean ± z · σ / √n with z the standard normal quantile at 1 − α/2 and σ the known standard
    /// deviation of the observations. Raises ArgumentOutOfRangeException unless σ is finite and greater than 0.
    /// NaN bounds for a count of 0. Relative error at most 1e-12 over the sample of the test "the known-sigma
    /// interval against the reference" (ten observations, σ 3, α 0.05), which pins it.
    /// </summary>
    let knownSigma (alpha: float) (sigma: float) (m: Moments) : Interval =
        Check.alpha alpha
        Check.finitePositive (nameof sigma) sigma
        let n = Moments.count m

        if n < 1.0 then
            IntervalSupport.nanInterval
        else
            IntervalSupport.around (Moments.mean m) (IntervalSupport.normalUpper alpha * sigma / sqrt n)

/// <summary>Confidence intervals for a binomial proportion from a count of successes and of trials.</summary>
/// <remarks>
/// Each function raises ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 (NaN raises), trials is zero or more and
/// successes lies in [0, trials]; the alpha check comes first. Zero trials give NaN bounds. Every bound lies in
/// [0, 1], pinned by the test "proportion intervals at 0 and n successes", which also pins the end points at 0 and
/// n successes. Each bound's relative error is at most 1e-12 over the proportions the test "proportion intervals
/// against the reference" pins ((81, 263) at α 0.05 and 0.01, (0, 20), (20, 20) and (1, 29)), where the quantiles
/// are <c>Continuous.quantile</c> of the normal and the beta. The methods are those compared by Brown, Cai and
/// DasGupta, "Interval estimation for a binomial proportion", Statistical Science 16(2), 2001 (general knowledge).
/// Measured here on .NET (Release), by an F# script that compared each row with its reference, the largest relative
/// error of a bound was 3.7e-16. Two regimes fall outside that bound. A bound read at 1 − α/2 carries the rounding of
/// 1 − α/2, an absolute error up to 2⁻⁵³ in a tail of α/2 (general knowledge): at α = 1e-10 the upper Clopper–Pearson
/// and Jeffreys bounds were off by 2.9e-10 to 3.7e-9 relative. And a beta quantile carries the round trip of
/// <c>Continuous.quantile</c> (the cdf at the bound within 1e-12 of the target), not a relative error of the bound:
/// for n of 1e5 or more and at most 3 successes the upper bounds were off by up to 5.7e-11. Both figures were measured
/// by an mpmath scan in the review of Plan T3 Task 5, not committed (spec §5.2 admits no third-party package).
/// </remarks>
module ProportionInterval =
    let private validate (alpha: float) (successes: int) (trials: int) : unit =
        Check.alpha alpha

        if trials < 0 then
            raise (ArgumentOutOfRangeException(nameof trials, "trials must be zero or more."))

        if successes < 0 || successes > trials then
            raise (ArgumentOutOfRangeException(nameof successes, "successes must lie between 0 and trials."))

    let private clip (x: float) : float = Math.Min(1.0, Math.Max(0.0, x))

    // The beta quantile interval, with 0 at no successes and 1 at all of them, for the shapes the method chooses.
    let private betaQuantiles
        (alpha: float)
        (successes: int)
        (trials: int)
        (lowerShapes: float * float)
        (upperShapes: float * float)
        : Interval =
        let lower =
            if successes = 0 then
                0.0
            else
                Continuous.quantile (Continuous.beta (fst lowerShapes) (snd lowerShapes)) (alpha / 2.0)

        let upper =
            if successes = trials then
                1.0
            else
                Continuous.quantile (Continuous.beta (fst upperShapes) (snd upperShapes)) (1.0 - alpha / 2.0)

        { Lower = lower; Upper = upper }

    /// <summary>
    /// The Wilson score interval: (p̂ + z²/2n)/(1 + z²/n) ± z/(1 + z²/n) · √(p̂(1 − p̂)/n + z²/4n²) with p̂ the
    /// proportion and z the standard normal quantile at 1 − α/2, from the inversion of the score test. The lower
    /// bound is 0 at 0 successes and the upper bound 1 at n, which the formula reaches in real arithmetic; the bounds
    /// are clipped to [0, 1] against rounding. Raises ArgumentOutOfRangeException and gives NaN bounds as the module
    /// states.
    /// </summary>
    let wilson (alpha: float) (successes: int) (trials: int) : Interval =
        validate alpha successes trials

        if trials = 0 then
            IntervalSupport.nanInterval
        else
            let n = float trials
            let p = float successes / n
            let z = IntervalSupport.normalUpper alpha
            let z2n = z * z / n
            let d = 1.0 + z2n
            let centre = (p + z2n / 2.0) / d
            let half = z / d * sqrt (p * (1.0 - p) / n + z * z / (4.0 * n * n))
            let lower = if successes = 0 then 0.0 else clip (centre - half)
            let upper = if successes = trials then 1.0 else clip (centre + half)
            { Lower = lower; Upper = upper }

    /// <summary>
    /// The exact (Clopper–Pearson) interval: the Beta(x, n − x + 1) quantile at α/2 and the Beta(x + 1, n − x)
    /// quantile at 1 − α/2 for x successes in n trials, with 0 at x = 0 and 1 at x = n. Its coverage is at least
    /// 1 − α at every proportion (general knowledge: Brown, Cai and DasGupta 2001, named in the module's remarks; no
    /// test here measures it). At 0 successes the upper bound is 1 − (α/2)^(1/n), pinned by the test
    /// "Clopper-Pearson at one trial and at zero successes has a closed form". Raises ArgumentOutOfRangeException and
    /// gives NaN bounds as the module states.
    /// </summary>
    let clopperPearson (alpha: float) (successes: int) (trials: int) : Interval =
        validate alpha successes trials

        if trials = 0 then
            IntervalSupport.nanInterval
        else
            let x = float successes
            let n = float trials
            betaQuantiles alpha successes trials (x, n - x + 1.0) (x + 1.0, n - x)

    /// <summary>
    /// The Agresti–Coull interval: with ñ = n + z² and p̃ = (x + z²/2)/ñ, p̃ ± z · √(p̃(1 − p̃)/ñ) clipped to [0, 1],
    /// z the standard normal quantile at 1 − α/2. Raises ArgumentOutOfRangeException and gives NaN bounds as the
    /// module states.
    /// </summary>
    let agrestiCoull (alpha: float) (successes: int) (trials: int) : Interval =
        validate alpha successes trials

        if trials = 0 then
            IntervalSupport.nanInterval
        else
            let z = IntervalSupport.normalUpper alpha
            let nTilde = float trials + z * z
            let p = (float successes + z * z / 2.0) / nTilde
            let half = z * sqrt (p * (1.0 - p) / nTilde)
            { Lower = clip (p - half); Upper = clip (p + half) }

    /// <summary>
    /// The Jeffreys interval: the Beta(x + ½, n − x + ½) quantiles at α/2 and 1 − α/2 for x successes in n trials,
    /// with 0 at x = 0 and 1 at x = n. Raises ArgumentOutOfRangeException and gives NaN bounds as the module states.
    /// </summary>
    let jeffreys (alpha: float) (successes: int) (trials: int) : Interval =
        validate alpha successes trials

        if trials = 0 then
            IntervalSupport.nanInterval
        else
            let x = float successes
            let n = float trials
            betaQuantiles alpha successes trials (x + 0.5, n - x + 0.5) (x + 0.5, n - x + 0.5)

/// <summary>The confidence interval for a variance from the moments of a sample.</summary>
module VarianceInterval =
    /// <summary>
    /// The interval [(n − 1)s²/χ²₁₋α/₂, (n − 1)s²/χ²α/₂] with χ² the chi-squared quantiles for n − 1 degrees of
    /// freedom and s² the sample variance (divisor n − 1). It holds the variance with probability 1 − α for
    /// independent normal observations; it is not robust to other distributions. Raises ArgumentOutOfRangeException
    /// unless 0 &lt; alpha &lt; 1 (NaN raises), before it looks at the sample. NaN bounds for a count below 2 and for
    /// NaN data, and for a spread the moments cannot resolve, as <c>MeanInterval.student</c> states. Relative error at
    /// most 1e-12 over the sample of the test "the variance interval against the reference" (ten observations, α
    /// 0.05), which pins it; measured here on .NET (Release), by an F# script that compared both bounds with the
    /// reference, the largest was 2.2e-16. The lower bound is read at 1 − α/2, whose rounding costs accuracy at small
    /// α: at α = 1e-10 it was off by up to 3.7e-9 relative (measured by an mpmath scan in the review of Plan T3 Task 5,
    /// not committed; spec §5.2 admits no third-party package). The variance carries the loss of <c>Moments</c> for
    /// data with an offset, up to about ε·|mean|/sd relative (ε = 2⁻⁵²).
    /// </summary>
    let chiSquared (alpha: float) (m: Moments) : Interval =
        Check.alpha alpha
        let n = Moments.count m

        if n < 2.0 then
            IntervalSupport.nanInterval
        else
            let dof = n - 1.0
            let chi = Continuous.chiSquared dof
            let numerator = dof * Check.resolvedVariance Moments.sampleVariance m

            { Lower = IntervalSupport.divide numerator (Continuous.quantile chi (1.0 - alpha / 2.0))
              Upper = IntervalSupport.divide numerator (Continuous.quantile chi (alpha / 2.0)) }

/// <summary>Confidence intervals for the difference of two means, the mean of the first sample minus that of the
/// second.</summary>
/// <remarks>
/// Both raise ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 (NaN raises), before they look at the samples.
/// NaN data gives NaN bounds, and so does a spread the moments cannot resolve in either sample, as
/// <c>MeanInterval.student</c> states. Relative error at most 1e-12 over the samples of the test "difference intervals
/// against the reference" (R's <c>datasets::sleep</c>, α 0.05), which pins both; measured here on .NET (Release), by
/// an F# script that compared each bound with the reference, the largest relative error was 7.4e-17. The bound is
/// relative to the means and variances the moments hold; <c>Moments</c> adds its own loss for data with an offset, up
/// to about ε·|mean|/sd relative in each variance (ε = 2⁻⁵²).
/// </remarks>
module DifferenceInterval =
    /// <summary>
    /// The Welch interval d ± t · √(s²ₐ/nₐ + s²ᵦ/nᵦ), d the difference of the means and t the Student t quantile at
    /// 1 − α/2 for the Welch–Satterthwaite degrees of freedom ν = (s²ₐ/nₐ + s²ᵦ/nᵦ)² / ((s²ₐ/nₐ)²/(nₐ − 1) +
    /// (s²ᵦ/nᵦ)²/(nᵦ − 1)), which does not assume equal variances. NaN bounds when either count is below 2. A zero
    /// standard error, both samples constant away from 0, gives [d, d]; it is detected before ν, which is then 0/0.
    /// ν is formed from the ratios of the two terms to their sum, so it neither underflows nor overflows.
    /// </summary>
    let welch (alpha: float) (a: Moments) (b: Moments) : Interval =
        Check.alpha alpha
        let na = Moments.count a
        let nb = Moments.count b

        if na < 2.0 || nb < 2.0 then
            IntervalSupport.nanInterval
        else
            let d = Moments.mean a - Moments.mean b
            let qa = Check.resolvedVariance Moments.sampleVariance a / na
            let qb = Check.resolvedVariance Moments.sampleVariance b / nb
            let s = qa + qb

            if s = 0.0 then
                { Lower = d; Upper = d }
            else
                let wa = qa / s
                let wb = qb / s
                let nu = 1.0 / (wa * wa / (na - 1.0) + wb * wb / (nb - 1.0))

                if nu > 0.0 && nu < infinity then
                    IntervalSupport.around d (IntervalSupport.tUpper alpha nu * sqrt s)
                else
                    IntervalSupport.nanInterval

    /// <summary>
    /// The pooled-variance interval d ± t · s_p · √(1/nₐ + 1/nᵦ), d the difference of the means, s²_p =
    /// ((nₐ − 1)s²ₐ + (nᵦ − 1)s²ᵦ)/(nₐ + nᵦ − 2) and t the Student t quantile at 1 − α/2 for nₐ + nᵦ − 2 degrees of
    /// freedom; it assumes equal variances. A sample of one observation adds 0 to the pooled sum of squares, so one
    /// sample may hold a single observation if the other holds two or more. NaN bounds when either count is below 1
    /// or the counts total less than 3. Two constant samples away from 0 give [d, d].
    /// </summary>
    let pooled (alpha: float) (a: Moments) (b: Moments) : Interval =
        Check.alpha alpha
        let na = Moments.count a
        let nb = Moments.count b

        if na < 1.0 || nb < 1.0 || na + nb < 3.0 then
            IntervalSupport.nanInterval
        else
            let sumOfSquares (n: float) (m: Moments) : float =
                if n < 2.0 then 0.0 else (n - 1.0) * Check.resolvedVariance Moments.sampleVariance m

            let dof = na + nb - 2.0
            let variance = (sumOfSquares na a + sumOfSquares nb b) / dof
            let half = IntervalSupport.tUpper alpha dof * sqrt (variance * (1.0 / na + 1.0 / nb))
            IntervalSupport.around (Moments.mean a - Moments.mean b) half
