namespace Ranvier.Statistics

open System
open Ranvier.Numerics

[<Struct>]
type Interval = { Lower: float; Upper: float }

/// <summary>
/// The settings of a confidence sequence: the error level, the mixture's tuning parameter and the burn-in, a count
/// as <c>Moments.count</c> is a float. Build it with <c>ConfidenceSequence.settings</c> or <c>tunedFor</c>, which
/// validate the fields; <c>interval</c> validates them again.
/// </summary>
type SequenceSettings = { Alpha: float; Rho: float; BurnIn: float }

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
/// interval has zero width at the observation), so a monitor reads the sequence through <c>interval</c>, which
/// applies a burn-in.
/// Coverage, outside the theorem, on 2,000 standard normal streams of 10,000 observations, ρ = <c>rhoTightestAt</c>
/// α 1000, burn-in 30 (<c>standardBurnIn</c>), measured here on 2026-11-20 by the coverage tests "the sequence
/// covers under continuous monitoring and the fixed-sample interval does not" and "the sequence covers at the
/// page's setting" (<c>dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "ConfidenceSequence"</c>
/// and <c>npx mocha dist/tests/Main.fs.js --grep "ConfidenceSequence"</c> after <c>dotnet fickl</c>; the two
/// targets printed the same figures):
/// at α = 0.05, 0.0415 of the streams excluded the mean at some count from 30 on; at α = 0.0125 (0.05 over four
/// channels), 0.009 did. The coverage tests read from t ≥ 30. Read from t = 1, the miss fraction at α = 0.05 was 1.0
/// on the same 2,000 streams (measured here on 2026-11-19, .NET only, by a scratch script that runs the coverage
/// test's loop with the first checked t as a parameter; docs/PROBES.md, "Coverage").
/// </remarks>
module ConfidenceSequence =
    /// <summary>
    /// The half-width sd · sqrt((2(tρ² + 1) / (t²ρ²)) · ln(sqrt(tρ² + 1) / α)) at t observations with standard
    /// deviation sd. NaN when t ≤ 0 or sd is NaN. Raises ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 and
    /// rho is finite and greater than 0, and when sd is negative.
    /// </summary>
    /// <remarks>
    /// The coverage guarantee of the module is asymptotic, with no statement at any finite t; <c>interval</c>
    /// applies the burn-in.
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
    /// counts it is too narrow (at one observation it excludes every other value); <c>interval</c> applies a
    /// burn-in before the bounds are shown or acted on.
    /// </remarks>
    let ofMoments (alpha: float) (rho: float) (m: Moments) : Interval =
        let mean = Moments.mean m
        let r = radius alpha rho (Moments.count m) (sqrt (Moments.variance m))
        { Lower = mean - r; Upper = mean + r }

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

    let private check (alpha: float) (rho: float) (burnIn: float) =
        Arguments.alpha alpha
        Arguments.finitePositive (nameof rho) rho

        if not (burnIn >= 1.0 && burnIn < infinity) then
            raise (ArgumentOutOfRangeException(nameof burnIn, "burnIn must be finite and at least 1."))

    /// <summary>
    /// The settings with the given fields. Raises ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1, rho is
    /// finite and greater than 0, and burnIn is finite and at least 1.
    /// </summary>
    let settings (alpha: float) (rho: float) (burnIn: float) : SequenceSettings =
        check alpha rho burnIn
        { Alpha = alpha; Rho = rho; BurnIn = burnIn }

    /// <summary>
    /// The settings whose Rho is <c>rhoTightestAt</c> alpha at plannedCount. Raises ArgumentOutOfRangeException as
    /// <c>rhoTightestAt</c> and <c>settings</c> do.
    /// </summary>
    let tunedFor (alpha: float) (plannedCount: float) (burnIn: float) : SequenceSettings =
        settings alpha (rhoTightestAt alpha plannedCount) burnIn

    /// <summary>
    /// The burn-in, 30, at which the repository's coverage tests read the sequence: <c>interval</c> shows no bounds
    /// before the 30th observation.
    /// </summary>
    let standardBurnIn: float = 30.0

    /// <summary>
    /// <c>ofMoments</c> at the settings' Alpha and Rho, with NaN bounds while the count is below BurnIn. Checks the
    /// record's fields by the rules of <c>settings</c>, so a record built directly is held to them; raises
    /// ArgumentOutOfRangeException when one fails.
    /// </summary>
    /// <remarks>
    /// The burn-in is a count, as <c>Moments.count</c> is a float. The coverage guarantee is asymptotic, so the
    /// interval carries no statement at any count; see the module's remarks for the coverage measured at the
    /// burn-in <c>standardBurnIn</c>.
    /// </remarks>
    let interval (s: SequenceSettings) (m: Moments) : Interval =
        check s.Alpha s.Rho s.BurnIn

        if Moments.count m < s.BurnIn then
            { Lower = nan; Upper = nan }
        else
            ofMoments s.Alpha s.Rho m

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
