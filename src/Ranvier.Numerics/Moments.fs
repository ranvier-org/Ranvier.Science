namespace Ranvier.Numerics

open System

/// Count, mean and spread of a set of observations. Immutable.
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

    let add (compensated: bool) (m: Moments) (x: float) : Moments =
        let n = m.N + 1.0
        let delta = x - Sum.value m.Mean
        let mean = Sum.add compensated m.Mean (delta / n)
        let m2 = Sum.add compensated m.M2 (delta * (x - Sum.value mean))
        { N = n; Mean = mean; M2 = m2 }

    /// Raises InvalidOperationException when the count is zero. Returns `zero` when the count reaches zero.
    let remove (compensated: bool) (m: Moments) (x: float) : Moments =
        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation from an empty set of moments.")
        elif m.N = 1.0 then
            zero
        else
            let n = m.N - 1.0
            let oldMean = Sum.value m.Mean
            let mean = Sum.add compensated m.Mean (-((x - oldMean) / n))
            let m2 = Sum.add compensated m.M2 (-((x - oldMean) * (x - Sum.value mean)))
            { N = n; Mean = mean; M2 = m2 }

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
    /// Raises InvalidOperationException when `part` has more observations than `whole`; returns `zero` when the counts are equal.
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
            let m2 = Sum.add compensated (Sum.add compensated whole.M2 (-(Sum.value part.M2))) (-between)
            { N = n; Mean = mean; M2 = m2 }

    /// A whole number, exact up to 2^53 (a float so the count has one representation on both targets).
    let count (m: Moments) : float = m.N

    /// NaN when the count is zero.
    let mean (m: Moments) : float =
        if m.N = 0.0 then nan else Sum.value m.Mean

    /// The population variance, (1/n)·Σy² − mean², never negative; NaN when the count is zero.
    let variance (m: Moments) : float =
        if m.N = 0.0 then
            nan
        else
            let v = Sum.value m.M2 / m.N
            if v < 0.0 then 0.0 else v

    /// sqrt (variance / count).
    let standardError (m: Moments) : float = sqrt (variance m / m.N)
