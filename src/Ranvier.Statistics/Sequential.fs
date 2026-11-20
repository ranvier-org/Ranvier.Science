namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>
/// An always-valid sequential test of the mean of a stream against a null value: its p-value may be read after every
/// observation, and the test stopped the first time it rejects.
/// </summary>
/// <remarks>
/// The p-value is the running minimum, from the burn-in on, of 1 / Λ, where Λ is the Gaussian mixture likelihood
/// ratio whose boundary is the interval of <c>ConfidenceSequence.interval</c> at the same settings. The verdict agrees
/// with whether some interval from the burn-in on excluded the null value (the duality of Johari, Pekelis and
/// Walsh, "Always valid inference", with the confidence sequence of Waudby-Smith et al., arXiv 2103.06476; general
/// knowledge), except where the null value lies within rounding of an interval's endpoint: the verdict is the
/// p-value's, computed from the likelihood ratio, and the interval's from a rounded mean and radius. On streams of 40
/// observations with relative spread about 1e-15 and the null value within 3 ulps of the mean, 33,953 of 140,000
/// comparisons disagreed (measured here by the review probe probe.fsx, in the scratchpad folder
/// agents\T3-task-9-review; no such disagreement arose in the duality tests at ordinary spreads). Its validity is the sequence's: asymptotic, with no statement at any finite count. A paired
/// two-sample test folds the differences. The record publishes a new instance on every <c>add</c> and claims no
/// cut-off under structural equality. Build it with <c>SequentialTest.create</c>.
/// </remarks>
[<NoComparison>]
type SequentialTest =
    private
        { Settings: SequenceSettings
          Theta0: float
          Moments: Moments
          PValue: float }

module SequentialTest =
    /// <summary>
    /// A test of the mean against theta0, with p-value 1 and no observations. Raises ArgumentOutOfRangeException
    /// when the settings break the rules of <c>ConfidenceSequence.settings</c> or theta0 is not finite.
    /// </summary>
    let create (s: SequenceSettings) (theta0: float) : SequentialTest =
        ConfidenceSequence.settings s.Alpha s.Rho s.BurnIn |> ignore
        Check.finite (nameof theta0) theta0

        { Settings = s
          Theta0 = theta0
          Moments = Moments.zero
          PValue = 1.0 }

    /// <summary>
    /// The test after one more observation. From the burn-in on the p-value becomes the smaller of itself and
    /// exp(−ln Λ), with ln Λ = −½ ln(tρ² + 1) + ρ²(t(x̄ − θ₀))² / (2σ²(tρ² + 1)) at the count t, the mean x̄ and the
    /// population standard deviation σ of the observations; the exponent is 0 when x̄ = θ₀, and infinite, giving
    /// p-value 0, when x̄ ≠ θ₀ and σ = 0. A NaN or infinite observation, or an overflow that leaves ln Λ NaN, makes
    /// the p-value NaN for good. The flag is that of <c>Moments.add</c>.
    /// </summary>
    let add (compensated: bool) (t: SequentialTest) (x: float) : SequentialTest =
        let m = Moments.add compensated t.Moments x

        let p =
            if Double.IsNaN t.PValue || Double.IsNaN x || Double.IsInfinity x || Double.IsNaN (Moments.mean m) then
                nan
            elif Moments.count m < t.Settings.BurnIn then
                t.PValue
            else
                let n = Moments.count m
                let rho2 = t.Settings.Rho * t.Settings.Rho
                let spread = n * rho2 + 1.0
                let deviation = Moments.mean m - t.Theta0
                let sd = sqrt (Moments.variance m)

                let exponent =
                    if deviation = 0.0 then
                        0.0
                    elif sd = 0.0 then
                        infinity
                    else
                        let z = deviation / sd
                        z * z * (n * n * rho2 / (2.0 * spread))

                let logRatio = -0.5 * log spread + exponent

                if Double.IsNaN logRatio then nan else min t.PValue (exp (-logRatio))

        { t with Moments = m; PValue = p }

    /// <summary>
    /// The always-valid p-value: 1 before the burn-in, non-increasing after it, NaN after a NaN or infinite
    /// observation.
    /// </summary>
    let pValue (t: SequentialTest) : float = t.PValue

    /// <summary>True when the p-value is at most the settings' Alpha; false when it is NaN.</summary>
    let rejects (t: SequentialTest) : bool = t.PValue <= t.Settings.Alpha

    /// <summary>The moments of the observations added so far.</summary>
    let moments (t: SequentialTest) : Moments = t.Moments

/// <summary>
/// Multiple-testing adjustments of a family of p-values, after R's <c>p.adjust</c>: each result is at the position
/// of its input and capped at 1.
/// </summary>
/// <remarks>
/// A NaN entry stays NaN and is not counted: m is the number of non-NaN entries, as R's default n is (from
/// documentation: p.adjust.R lines 22 to 33, where <c>n = length(p)</c> is first forced after the NA entries are
/// dropped, and p.adjust.Rd lines 149 to 153 of R's source, whose example sets two of 50 p-values NA). Each method raises
/// ArgumentOutOfRangeException when a non-NaN entry lies outside [0, 1]. Tied p-values give tied results. Cost
/// O(m log m), O(m) for Bonferroni. Holm and Bonferroni control the familywise error rate under any dependence;
/// Hochberg needs independence or non-negative dependence; Benjamini–Hochberg controls the false discovery rate
/// under independence or positive dependence, and Benjamini–Yekutieli under any dependence (general knowledge).
/// </remarks>
module Adjust =
    let private checkAll (p: float[]) : unit =
        for x in p do
            if not (Double.IsNaN x) then
                Check.probability "p" x

    let private count (p: float[]) : int =
        p |> Array.sumBy (fun x -> if Double.IsNaN x then 0 else 1)

    // The indices of the non-NaN entries, ascending by value, then by index.
    let private order (p: float[]) : int[] =
        [| 0 .. p.Length - 1 |]
        |> Array.filter (fun i -> not (Double.IsNaN p[i]))
        |> Array.sortWith (fun a b ->
            let c = compare p[a] p[b]
            if c <> 0 then c else compare a b)

    // `scale m rank x` is the term for the entry of ascending rank `rank` (1-based) among m. A step-up method takes
    // the running minimum from the largest p-value down, a step-down method the running maximum from the smallest up.
    let private adjust (p: float[]) (stepUp: bool) (scale: int -> int -> float -> float) : float[] =
        checkAll p
        let o = order p
        let m = o.Length
        let result = Array.create p.Length nan

        if stepUp then
            let mutable running = 1.0

            for k in m - 1 .. -1 .. 0 do
                running <- min running (scale m (k + 1) p[o[k]])
                result[o[k]] <- running
        else
            let mutable running = 0.0

            for k in 0 .. m - 1 do
                running <- max running (scale m (k + 1) p[o[k]])
                result[o[k]] <- min 1.0 running

        result

    /// <summary>min(1, m · p) for each entry. Raises ArgumentOutOfRangeException for an entry outside [0, 1].</summary>
    let bonferroni (p: float[]) : float[] =
        checkAll p
        let m = float (count p)
        p |> Array.map (fun x -> if Double.IsNaN x then nan else min 1.0 (m * x))

    /// <summary>
    /// Step-down: max over j ≤ i of (m − j + 1) · p₍ⱼ₎ on the ascending order, capped at 1. Raises
    /// ArgumentOutOfRangeException for an entry outside [0, 1].
    /// </summary>
    let holm (p: float[]) : float[] =
        adjust p false (fun m r x -> float (m + 1 - r) * x)

    /// <summary>
    /// Step-up: min over j ≥ i of (m − j + 1) · p₍ⱼ₎ on the ascending order, capped at 1. Raises
    /// ArgumentOutOfRangeException for an entry outside [0, 1].
    /// </summary>
    let hochberg (p: float[]) : float[] =
        adjust p true (fun m r x -> float (m + 1 - r) * x)

    /// <summary>
    /// Step-up: min over j ≥ i of (m / j) · p₍ⱼ₎ on the ascending order, capped at 1. Raises
    /// ArgumentOutOfRangeException for an entry outside [0, 1].
    /// </summary>
    let benjaminiHochberg (p: float[]) : float[] =
        adjust p true (fun m r x -> float m / float r * x)

    /// <summary>
    /// Benjamini–Hochberg with each term multiplied by the sum over i ≤ m of 1 / i, capped at 1. Raises
    /// ArgumentOutOfRangeException for an entry outside [0, 1].
    /// </summary>
    let benjaminiYekutieli (p: float[]) : float[] =
        let mutable q = 0.0

        for i in 1 .. count p do
            q <- q + 1.0 / float i

        adjust p true (fun m r x -> q * float m / float r * x)
