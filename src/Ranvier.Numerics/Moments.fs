namespace Ranvier.Numerics

open System

/// <summary>Count, mean and spread of a set of observations. Immutable.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carries.
/// </remarks>
[<Struct; NoComparison>]
type Moments =
    private
        {
            N: float
            Mean: Sum
            M2: Sum
        }

module Moments =
    let zero: Moments =
        {
            N = 0.0
            Mean = Sum.zero
            M2 = Sum.zero
        }

    /// The state with the given count, mean total and sum of squares about the mean; the caller keeps them consistent.
    let internal ofState (count: float) (mean: Sum) (m2: Sum) : Moments = { N = count; Mean = mean; M2 = m2 }

    let add (compensated: bool) (m: Moments) (x: float) : Moments =
        let n = m.N + 1.0
        let delta = x - Sum.value m.Mean
        let mean = Sum.add compensated m.Mean (delta / n)
        let m2 = Sum.add compensated m.M2 (delta * (x - Sum.value mean))
        { N = n; Mean = mean; M2 = m2 }

    /// <summary>
    /// Raises InvalidOperationException when the count is zero. Returns <c>zero</c> when the count reaches zero. At a
    /// count of one the spread is exactly zero and the mean equals the remaining observation within rounding.
    /// </summary>
    let remove (compensated: bool) (m: Moments) (x: float) : Moments =
        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation from an empty set of moments.")
        elif m.N = 1.0 then
            zero
        else
            let n = m.N - 1.0
            let oldMean = Sum.value m.Mean
            let mean = Sum.add compensated m.Mean (-((x - oldMean) / n))

            let m2 =
                if n = 1.0 then
                    Sum.zero
                else
                    Sum.add compensated m.M2 (-((x - oldMean) * (x - Sum.value mean)))

            { N = n; Mean = mean; M2 = m2 }

    /// Returns `b` itself when `a` is empty, and `a` itself when `b` is empty.
    let merge (compensated: bool) (a: Moments) (b: Moments) : Moments =
        if a.N = 0.0 then
            b
        elif b.N = 0.0 then
            a
        else
            let n = a.N + b.N
            let delta = Sum.value b.Mean - Sum.value a.Mean
            let mean = Sum.add compensated a.Mean (delta * (b.N / n))
            let m2 = Sum.add compensated (Sum.add compensated a.M2 (Sum.value b.M2)) (delta * delta * (a.N * b.N / n))
            { N = n; Mean = mean; M2 = m2 }

    /// `unmerge whole part` is the moments of the observations of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more observations than `whole`; returns `zero` when the counts are equal,
    /// and `whole` itself when `part` is empty. At a remaining count of one the spread is exactly zero.
    let unmerge (compensated: bool) (whole: Moments) (part: Moments) : Moments =
        let n = whole.N - part.N

        if n < 0.0 then
            raise (InvalidOperationException "Cannot unmerge a part with more observations than the whole.")
        elif n = 0.0 then
            zero
        elif part.N = 0.0 then
            whole
        else
            let wholeMean = Sum.value whole.Mean
            let mean = Sum.add compensated whole.Mean (-((part.N / n) * (Sum.value part.Mean - wholeMean)))
            let delta = Sum.value part.Mean - Sum.value mean
            let between = delta * delta * (n * part.N / whole.N)

            let m2 =
                if n = 1.0 then
                    Sum.zero
                else
                    Sum.add compensated (Sum.add compensated whole.M2 (-(Sum.value part.M2))) (-between)

            { N = n; Mean = mean; M2 = m2 }

    /// A whole number, exact up to 2^53 (a float so the count has one representation on both targets).
    let count (m: Moments) : float = m.N

    /// NaN when the count is zero.
    let mean (m: Moments) : float =
        if m.N = 0.0 then nan else Sum.value m.Mean

    /// <summary>
    /// The population variance, Σ(x − mean)² / n, from the sum of squared deviations that Welford's update keeps;
    /// never negative; NaN when the count is zero. The update subtracts a running mean from each observation, so for
    /// data with an offset the relative error reaches about ε·|mean|/sd (ε = 2⁻⁵², sd the standard deviation; general
    /// knowledge), whichever the flag: the test "an offset of 1e6 costs at most ε · |mean| / sd of the sample variance"
    /// pins that bound at an offset of 1e6 and a spread of about 1 against <c>tests/reference/special.py numerics</c>.
    /// </summary>
    let variance (m: Moments) : float =
        if m.N = 0.0 then
            nan
        else
            let v = Sum.value m.M2 / m.N
            if v < 0.0 then 0.0 else v

    /// The sample variance, Σ(x − mean)² / (n − 1), never negative; NaN when the count is below 2. For data with an
    /// offset its relative error reaches about ε·|mean|/sd, as <c>variance</c> states.
    let sampleVariance (m: Moments) : float =
        if m.N < 2.0 then
            nan
        else
            let v = Sum.value m.M2 / (m.N - 1.0)
            if v < 0.0 then 0.0 else v

    /// sqrt (variance); NaN when the count is zero.
    let standardDeviation (m: Moments) : float = sqrt (variance m)

    /// sqrt (sampleVariance); NaN when the count is below 2.
    let sampleStandardDeviation (m: Moments) : float = sqrt (sampleVariance m)

    /// sqrt (variance / count).
    let standardError (m: Moments) : float = sqrt (variance m / m.N)
