namespace Ranvier.Statistics

open System
open Ranvier.Numerics

[<Struct>]
type Interval = { Lower: float; Upper: float }

module private Arguments =
    let alpha (alpha: float) =
        if not (alpha > 0.0 && alpha < 1.0) then
            raise (ArgumentOutOfRangeException(nameof alpha, "alpha must lie strictly between 0 and 1."))

    let finitePositive (name: string) (x: float) =
        if not (x > 0.0 && x < infinity) then
            raise (ArgumentOutOfRangeException(name, name + " must be finite and greater than 0."))

    let notNegative (name: string) (x: float) =
        if x < 0.0 then
            raise (ArgumentOutOfRangeException(name, name + " must be zero or more."))

/// <summary>
/// The Gaussian-mixture asymptotic confidence sequence for a mean (Waudby-Smith, Arbour, Sinha, Kennedy and
/// Ramdas, "Time-uniform central limit theory and asymptotic confidence sequences", arXiv 2103.06476,
/// Theorem 2.2, equation 13).
/// </summary>
/// <remarks>
/// For independent, identically distributed observations with finite variance, the intervals form a (1 − α)
/// asymptotic confidence sequence: as t grows, their bounds approach, almost surely, those of a sequence of
/// intervals that holds the mean at every t simultaneously with probability at least 1 − α. The guarantee is
/// asymptotic: it states no coverage at any finite t. Intervals at small t fall outside it (at one observation the
/// interval has zero width at the observation), so a monitor reads the sequence only after a burn-in.
/// Measured by this repository's coverage test, outside the theorem: on standard normal streams of 10,000
/// observations, α = 0.05, 0.0415 of 2,000 streams excluded the mean at some t ≥ 30, and every stream did at t = 1.
/// </remarks>
module ConfidenceSequence =
    /// <summary>
    /// The half-width sd · sqrt((2(tρ² + 1) / (t²ρ²)) · ln(sqrt(tρ² + 1) / α)) at t observations with standard
    /// deviation sd. NaN when t ≤ 0 or sd is NaN. Raises ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 and
    /// rho is finite and greater than 0, and when sd is negative.
    /// </summary>
    /// <remarks>
    /// The coverage guarantee of the module is asymptotic, with no statement at any finite t; a monitor applies a
    /// burn-in before reading it.
    /// </remarks>
    let radius (alpha: float) (rho: float) (t: float) (sd: float) : float =
        Arguments.alpha alpha
        Arguments.finitePositive (nameof rho) rho
        Arguments.notNegative (nameof sd) sd

        if not (t > 0.0) then
            nan
        else
            let rho2 = rho * rho
            let spread = t * rho2 + 1.0
            sd * sqrt (2.0 * spread / (t * t * rho2) * log (sqrt spread / alpha))

    /// <summary>
    /// mean ± radius at the moments' count and population standard deviation. NaN bounds for an empty sample;
    /// a zero-width interval at the observation for a sample of one. Raises ArgumentOutOfRangeException unless
    /// 0 &lt; alpha &lt; 1 and rho is finite and greater than 0.
    /// </summary>
    /// <remarks>
    /// The interval holds the mean only asymptotically, with no coverage statement at any finite count. At small
    /// counts it is too narrow (at one observation it excludes every other value), so a monitor applies a burn-in
    /// before showing or acting on it; the repository's coverage test reads from t ≥ 30.
    /// </remarks>
    let ofMoments (alpha: float) (rho: float) (m: Moments) : Interval =
        let mean = Moments.mean m
        let r = radius alpha rho (Moments.count m) (sqrt (Moments.variance m))
        { Lower = mean - r; Upper = mean + r }

    /// <summary>
    /// <c>ofMoments</c> with NaN bounds while the count is below <c>burnIn</c>. Raises ArgumentOutOfRangeException
    /// unless <c>burnIn</c> is finite and at least 1, and as <c>ofMoments</c> does.
    /// </summary>
    let ofMomentsAfter (burnIn: float) (alpha: float) (rho: float) (m: Moments) : Interval =
        if not (burnIn >= 1.0 && burnIn < infinity) then
            raise (ArgumentOutOfRangeException(nameof burnIn, "burnIn must be finite and at least 1."))

        let interval = ofMoments alpha rho m

        if Moments.count m < burnIn then
            { Lower = nan; Upper = nan }
        else
            interval

    /// <summary>
    /// The rho for which <c>radius</c> at t = n is smallest: sqrt(u / n), where u &gt; 0 solves
    /// α² · exp u = u + 1 (arXiv 2103.06476, appendix B.2, equation 302). Raises ArgumentOutOfRangeException unless
    /// 0 &lt; alpha &lt; 1 and n is finite and greater than 0.
    /// </summary>
    let rhoTightestAt (alpha: float) (n: float) : float =
        Arguments.alpha alpha
        Arguments.finitePositive (nameof n) n
        // Newton's method on h(u) = u − ln(u + 1) + 2 ln α, which is convex and increasing for u > 0, from the
        // closed-form approximation of equation 311.
        let twoLogAlpha = 2.0 * log alpha
        let mutable u = -twoLogAlpha + log (1.0 - twoLogAlpha)
        let mutable step = infinity
        let mutable iterations = 0

        while abs step > 1e-15 * u && iterations < 100 do
            step <- (u - log (u + 1.0) + twoLogAlpha) * (u + 1.0) / u
            u <- u - step
            iterations <- iterations + 1

        sqrt (u / n)

/// <summary>The fixed-sample normal-approximation interval for a mean.</summary>
/// <remarks>
/// Its coverage of 1 − α holds at one sample size planned before the data are seen. Read after every
/// observation, the chance that it excludes the mean at some time grows towards 1.
/// </remarks>
module FixedSample =
    /// <summary>
    /// mean ± z(1 − α/2) · sd / sqrt t, with sd the population standard deviation and t the count. NaN bounds for
    /// an empty sample; a zero-width interval at the observation for a sample of one. Raises
    /// ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1.
    /// </summary>
    let ofMoments (alpha: float) (m: Moments) : Interval =
        Arguments.alpha alpha
        let mean = Moments.mean m
        let r = -(NormalQuantile.ofProbability (alpha / 2.0)) * Moments.standardError m
        { Lower = mean - r; Upper = mean + r }
