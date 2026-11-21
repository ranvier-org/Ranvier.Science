namespace Ranvier.Statistics

open System
open Ranvier.Numerics

module internal Spread =
    /// The sample standard deviation, divisor n − 1, by the corrected two-pass formula, never NaN from rounding: the
    /// deviations from the rounded mean are exact for values within a factor of two of it, and subtracting (Σd)²/n
    /// removes the effect of the mean's rounding, so a small spread about a large offset keeps its digits where a
    /// one-pass update loses them, and identical values give exactly 0. A NaN or infinite value gives NaN.
    let sampleStandardDeviation (xs: float[]) : float =
        let n = float xs.Length
        let mean = Array.sum xs / n
        let mutable squares = 0.0
        let mutable total = 0.0

        for x in xs do
            let d = x - mean
            squares <- squares + d * d
            total <- total + d

        let variance = (squares - total * total / n) / (n - 1.0)
        sqrt (if variance < 0.0 then 0.0 else variance)

/// <summary>The nonparametric bootstrap of a statistic of a sample: a percentile interval and a standard error.</summary>
/// <remarks>
/// A resample draws n indices, each with <c>Generator.integer 0 n</c>, and copies those items into a new array, so
/// a statistic that keeps its argument never sees it overwritten by the next resample. The result depends only on the
/// generator, and is equal on .NET and under Fable because <c>Generator.integer</c> is exact on both. A statistic that
/// returns NaN for some resample gives a NaN interval or standard error and raises nothing. The costs given are general
/// knowledge of the algorithms used.
/// </remarks>
module Bootstrap =
    let private resample (sample: float[]) (g: Generator) : struct (float[] * Generator) =
        let n = sample.Length
        let drawn: float[] = Array.zeroCreate n
        let mutable current = g

        for i in 0 .. n - 1 do
            let struct (k, next) = Generator.integer 0 n current
            drawn[i] <- sample[k]
            current <- next

        struct (drawn, current)

    // The statistic of each of `resamples` fresh resamples, and the generator after the last draw.
    let private statistics
        (resamples: int)
        (statistic: float[] -> float)
        (sample: float[])
        (g: Generator)
        : struct (float[] * Generator) =
        let values: float[] = Array.zeroCreate resamples
        let mutable current = g

        for r in 0 .. resamples - 1 do
            let struct (drawn, next) = resample sample current
            values[r] <- statistic drawn
            current <- next

        struct (values, current)

    let private checkSample (sample: float[]) : unit =
        if sample.Length = 0 then
            raise (ArgumentException("A bootstrap needs a sample of at least one observation.", nameof sample))

    /// <summary>The percentile interval at level 1 − alpha: the type-7 quantiles, at alpha/2 and 1 − alpha/2, of the
    /// statistic over the resamples (from one sort, <c>Describe.quantiles</c>), and the generator after the draws.
    /// The statistic is called once per resample on a fresh array of the sample's length. A NaN from the statistic
    /// on any resample gives a NaN interval. Raises ArgumentOutOfRangeException unless 0 &lt; alpha &lt; 1 (a NaN
    /// alpha raises) and unless the count of resamples is at least 1, and ArgumentException for an empty sample.
    /// The same generator gives the same interval. Cost: one call of the statistic per resample plus
    /// O(resamples · n) to draw and O(resamples · log resamples) to sort. Pinned by "the percentile interval of the
    /// mean", "the same generator gives the same result", "the statistic sees resamples of the sample", "a constant
    /// sample gives a zero-width interval at the constant" and "bootstrap arguments out of range raise".</summary>
    let percentile
        (alpha: float)
        (resamples: int)
        (statistic: float[] -> float)
        (sample: float[])
        (g: Generator)
        : struct (Interval * Generator) =
        Check.alpha alpha

        if resamples < 1 then
            raise (ArgumentOutOfRangeException(nameof resamples, "resamples must be at least 1."))

        checkSample sample
        let struct (values, next) = statistics resamples statistic sample g
        let bounds = Describe.quantiles [| alpha / 2.0; 1.0 - alpha / 2.0 |] values
        struct ({ Lower = bounds[0]; Upper = bounds[1] }, next)

    /// <summary>The bootstrap standard error: the sample standard deviation, divisor n − 1 (n the count of
    /// resamples), of the statistic over the resamples, by the corrected two-pass formula of the bandwidths, so that
    /// identical statistics give exactly 0 and statistics with a large offset keep the digits of their spread, and
    /// the generator after the draws. A NaN from the statistic gives NaN. Raises ArgumentOutOfRangeException unless
    /// the count of resamples is at least 2 (one value has no spread) and ArgumentException for an empty sample.
    /// Cost: one call of the statistic per resample plus O(resamples · n). Relative error within 1e-12 of the
    /// spread of the statistics taken about their offset, for 400 resample means of 50 values at an offset of 1e9
    /// (test "the bootstrap standard error of statistics with a large offset"), where a one-pass fold can lose up to
    /// about ε·offset/sd relative, 1e-4 there (ε = 2⁻⁵², general knowledge). Pinned also by "the bootstrap standard
    /// error of the mean", "a constant sample gives a zero-width interval
    /// at the constant" and "bootstrap arguments out of range raise".</summary>
    let standardError
        (resamples: int)
        (statistic: float[] -> float)
        (sample: float[])
        (g: Generator)
        : struct (float * Generator) =
        if resamples < 2 then
            raise (ArgumentOutOfRangeException(nameof resamples, "resamples must be at least 2."))

        checkSample sample
        let struct (values, next) = statistics resamples statistic sample g
        struct (Spread.sampleStandardDeviation values, next)

/// <summary>Kernel density estimation with a Gaussian kernel, and the two rule-of-thumb bandwidths of R.</summary>
/// <remarks>
/// The bandwidths are R's <c>bw.nrd0</c> (Silverman) and <c>bw.nrd</c> (Scott), from documentation
/// (<c>bandwidths.R</c> in wch/r-source trunk, <c>src/library/stats/R</c>, lines 23 to 37, read on 2026-11-21). Both
/// are a multiple of min(s, IQR/1.34) · n^(−1/5), with s the sample standard deviation (divisor n − 1, by a corrected
/// two-pass formula), the IQR the type-7 interquartile range and n the count. Both need at least two observations, as
/// R does (line 25: "need at least 2 data points"): one point has no spread, its s is NaN, and raising names the
/// cause. Both are NaN for data that holds a NaN or an infinity, and raise nothing for it. The spread is computed on
/// the data multiplied by an exact power of two near its 2-norm and scaled back at the end, so no value is rounded by
/// the scaling and a sample near 1e300 or 1e-300 gives the bandwidth that scaling the sample gives (measured here by
/// <c>dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter-test-list "Resampling"</c>, whose test
/// "bandwidths scale with the data without overflow or underflow" runs at scales 1e300, 1e-300 and 1e-150 and at
/// ±1.5e308, and whose test "bandwidths of offset data match exact rational arithmetic" has four rows
/// (offsets 1 to 1e9, spreads 1e-9 to 1e-3) agree with exact rational arithmetic to 1e-13 relative, from
/// tests/reference/special.py resampling), where a squared deviation or the IQR would otherwise underflow to 0 or
/// overflow to infinity.
/// </remarks>
module Density =
    let private minimumObservations (sample: float[]) : unit =
        if sample.Length < 2 then
            raise (ArgumentException("A bandwidth needs at least 2 observations.", nameof sample))

    let private isFinite (x: float) : bool = not (Double.IsNaN x || Double.IsInfinity x)

    // factor · min(s, IQR/1.34) · n^(−1/5) for `factor`; when the minimum is 0 and `fallbacks` is set, s, then |x₁|,
    // then 1 take its place, as in bw.nrd0.
    let private bandwidth (factor: float) (fallbacks: bool) (sample: float[]) : float =
        minimumObservations sample

        if not (Array.forall isFinite sample) then
            nan
        else
            let weight = factor * Math.Pow(float sample.Length, -0.2)
            let scale = Array.fold (fun m x -> max m (abs x)) 0.0 sample

            if scale = 0.0 then
                if fallbacks then weight else 0.0
            else
                // Scaling by an exact power of two near the 2-norm changes no digit of any value, so the spread is
                // that of the sample itself, even for a small spread about a large offset.
                let exponent = RegressionSupport.exponentNear sample
                let scaled = Array.map (fun x -> RegressionSupport.scaleByPowerOfTwo x (-exponent)) sample
                let s = Spread.sampleStandardDeviation scaled
                let lo = min s (Describe.interquartileRange scaled / 1.34)

                if lo > 0.0 then RegressionSupport.scaleByPowerOfTwo (weight * lo) exponent
                elif not fallbacks then 0.0
                elif s > 0.0 then RegressionSupport.scaleByPowerOfTwo (weight * s) exponent
                else weight * abs sample[0]

    /// <summary>Silverman's rule of thumb, R's <c>bw.nrd0</c>: 0.9 · min(s, IQR/1.34) · n^(−1/5), where, if that
    /// minimum is 0, s takes its place, then |x₁|, then 1. A constant sample therefore gets 0.9 · |x₁| · n^(−1/5), or
    /// 0.9 · n^(−1/5) when it is all zeros, and a sample with IQR 0 but s &gt; 0 gets 0.9 · s · n^(−1/5). Raises
    /// ArgumentException for fewer than two observations; NaN for data that holds a NaN or an infinity. O(n log n).
    /// Pinned by "Silverman and Scott bandwidths", "Silverman's fallbacks at zero spread", "bandwidths of fewer than
    /// two observations raise", "a NaN or infinity in the data gives NaN bandwidths" and "bandwidths scale with the
    /// data without overflow or underflow".</summary>
    let silvermanBandwidth (sample: float[]) : float = bandwidth 0.9 true sample

    /// <summary>Scott's rule of thumb, R's <c>bw.nrd</c>: 1.06 · min(s, IQR/1.34) · n^(−1/5), with no fallback, so 0
    /// when the IQR or s is 0 (a constant sample). Raises ArgumentException for fewer than two observations; NaN for
    /// data that holds a NaN or an infinity. O(n log n). Pinned by "Silverman and Scott bandwidths", "Silverman's
    /// fallbacks at zero spread", "bandwidths of fewer than two observations raise", "a NaN or infinity in the data
    /// gives NaN bandwidths" and "bandwidths scale with the data without overflow or underflow".</summary>
    let scottBandwidth (sample: float[]) : float = bandwidth 1.06 false sample

    /// <summary>The estimate f(x) = (1/(n·h)) · Σ φ((x − xᵢ)/h) with φ the standard normal density, as a function of
    /// x. The sample is copied, so later changes to the array do not affect f. Raises
    /// ArgumentOutOfRangeException unless the bandwidth is finite and greater than 0 (NaN raises) and
    /// ArgumentException for an empty sample. A NaN in the sample, or a NaN x, gives NaN, and an infinite xᵢ
    /// contributes 0 to the sum at a finite x. The terms are added in order, so the rounding grows with n. The sum is
    /// divided by n, by √(2π) and by the bandwidth in turn, so no intermediate overflows or underflows needlessly: a
    /// peak above the largest double is infinity, and where x − xᵢ overflows the true value is below 1e-307, so the
    /// 0 returned is within the smallest normal double of it (general knowledge: with u = |x − xᵢ|/h the term is
    /// φ(u)/h = u·φ(u)/|x − xᵢ|, and u·φ(u) is at most φ(1) ≈ 0.242, so the term is below 0.242/1.8e308). Cost: O(n)
    /// per evaluation. Pinned by "the estimate of
    /// one point is a normal density", "the estimate averages the kernels of its points", "the estimate integrates
    /// to 1", "the estimate copies its sample", "density arguments out of range raise" and "the estimate at extreme
    /// scales is exact or honestly zero".</summary>
    let gaussianKernel (bandwidth: float) (sample: float[]) : (float -> float) =
        Check.finitePositive (nameof bandwidth) bandwidth

        if sample.Length = 0 then
            raise (ArgumentException("A kernel estimate needs at least one observation.", nameof sample))

        let points = Array.copy sample
        let count = float points.Length
        let normaliser = sqrt (2.0 * Math.PI)

        fun x ->
            let mutable sum = 0.0

            for xi in points do
                let u = (x - xi) / bandwidth
                sum <- sum + exp (-0.5 * u * u)

            sum / count / normaliser / bandwidth
