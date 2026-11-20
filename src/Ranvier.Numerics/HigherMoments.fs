namespace Ranvier.Numerics

open System

/// <summary>Count, mean and central moments to the fourth order of a set of observations. Immutable.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carries. Every read is NaN on an empty accumulator.
/// </remarks>
[<Struct; NoComparison>]
type HigherMoments =
    private
        {
            N: float
            Mean: Sum
            M2: Sum
            M3: Sum
            M4: Sum
        }

module HigherMoments =
    let zero: HigherMoments =
        {
            N = 0.0
            Mean = Sum.zero
            M2 = Sum.zero
            M3 = Sum.zero
            M4 = Sum.zero
        }

    // The cross terms of Pebay's merge (SAND2008-6212, equations 2.1 to 2.3) for parts A and B with counts na and nb,
    // n = na + nb and delta = meanB - meanA. Each fraction is formed before it meets a moment, so that no product leaves
    // the finite range while the moments are finite.
    let private cross2 (na: float) (nb: float) (n: float) (delta: float) : float = delta * delta * (na * nb / n)

    let private cross3 (na: float) (nb: float) (n: float) (delta: float) (m2a: float) (m2b: float) : float =
        let d2 = delta * delta
        let share = na * nb * (na - nb) / (n * n)
        delta * d2 * share + 3.0 * delta * ((na / n) * m2b - (nb / n) * m2a)

    let private cross4
        (na: float)
        (nb: float)
        (n: float)
        (delta: float)
        (m2a: float)
        (m2b: float)
        (m3a: float)
        (m3b: float)
        : float =
        let d2 = delta * delta
        let weight = na * nb * (na * na - na * nb + nb * nb) / (n * n * n)
        let fa = na / n
        let fb = nb / n
        d2 * d2 * weight + 6.0 * d2 * (fa * fa * m2b + fb * fb * m2a) + 4.0 * delta * (fa * m3b - fb * m3a)

    let private plus (compensated: bool) (s: Sum) (x: float) : Sum = Sum.add compensated s x

    let private singleton (compensated: bool) (x: float) : HigherMoments =
        {
            N = 1.0
            Mean = Sum.add compensated Sum.zero x
            M2 = Sum.zero
            M3 = Sum.zero
            M4 = Sum.zero
        }

    /// Returns `b` itself when `a` is empty, and `a` itself when `b` is empty.
    let merge (compensated: bool) (a: HigherMoments) (b: HigherMoments) : HigherMoments =
        if a.N = 0.0 then
            b
        elif b.N = 0.0 then
            a
        else
            let n = a.N + b.N
            let delta = Sum.value b.Mean - Sum.value a.Mean
            let m2a = Sum.value a.M2
            let m2b = Sum.value b.M2
            let m3a = Sum.value a.M3
            let m3b = Sum.value b.M3

            {
                N = n
                Mean = plus compensated a.Mean (delta * (b.N / n))
                M2 = plus compensated (plus compensated a.M2 m2b) (cross2 a.N b.N n delta)
                M3 = plus compensated (plus compensated a.M3 m3b) (cross3 a.N b.N n delta m2a m2b)
                M4 = plus compensated (plus compensated a.M4 (Sum.value b.M4)) (cross4 a.N b.N n delta m2a m2b m3a m3b)
            }

    let add (compensated: bool) (m: HigherMoments) (x: float) : HigherMoments =
        merge compensated m (singleton compensated x)

    /// `unmerge whole part` is the moments of the observations of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more observations than `whole`; returns `zero` when the counts are equal,
    /// and `whole` itself when `part` is empty. At a remaining count of one the central moments are exactly zero.
    let unmerge (compensated: bool) (whole: HigherMoments) (part: HigherMoments) : HigherMoments =
        let n = whole.N - part.N

        if n < 0.0 then
            raise (InvalidOperationException "Cannot unmerge a part with more observations than the whole.")
        elif n = 0.0 then
            zero
        elif part.N = 0.0 then
            whole
        else
            let wholeMean = Sum.value whole.Mean
            let mean = plus compensated whole.Mean (-((part.N / n) * (Sum.value part.Mean - wholeMean)))

            if n = 1.0 then
                { N = n; Mean = mean; M2 = Sum.zero; M3 = Sum.zero; M4 = Sum.zero }
            else
                let delta = Sum.value part.Mean - Sum.value mean
                let m2b = Sum.value part.M2
                let m3b = Sum.value part.M3
                let m2 = plus compensated (plus compensated whole.M2 (-m2b)) (-(cross2 n part.N whole.N delta))
                let m2a = Sum.value m2
                let m3 = plus compensated (plus compensated whole.M3 (-m3b)) (-(cross3 n part.N whole.N delta m2a m2b))
                let m3a = Sum.value m3

                let m4 =
                    plus
                        compensated
                        (plus compensated whole.M4 (-(Sum.value part.M4)))
                        (-(cross4 n part.N whole.N delta m2a m2b m3a m3b))

                { N = n; Mean = mean; M2 = m2; M3 = m3; M4 = m4 }

    /// <summary>
    /// Raises InvalidOperationException when the count is zero. Returns <c>zero</c> when the count reaches zero. At a
    /// count of one the central moments are exactly zero and the mean equals the remaining observation within rounding.
    /// </summary>
    let remove (compensated: bool) (m: HigherMoments) (x: float) : HigherMoments =
        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation from an empty set of moments.")
        else
            unmerge compensated m (singleton compensated x)

    /// A whole number, exact up to 2^53 (a float so the count has one representation on both targets).
    let count (m: HigherMoments) : float = m.N

    /// NaN when the count is zero.
    let mean (m: HigherMoments) : float =
        if m.N = 0.0 then nan else Sum.value m.Mean

    /// The population variance, M2 / n, never negative; NaN when the count is zero.
    let variance (m: HigherMoments) : float =
        if m.N = 0.0 then
            nan
        else
            let v = Sum.value m.M2 / m.N
            if v < 0.0 then 0.0 else v

    /// The sample variance, M2 / (n - 1), never negative; NaN when the count is below 2.
    let sampleVariance (m: HigherMoments) : float =
        if m.N < 2.0 then
            nan
        else
            let v = Sum.value m.M2 / (m.N - 1.0)
            if v < 0.0 then 0.0 else v

    // 2^-970 = 2^-1022 * 2^52, written as the decimal that rounds to exactly that double: below it a moment bounded by
    // M2^(3/2) or M2^2 has left the normal range and keeps fewer than 52 bits.
    let private normalFloor: float = 1.0020841800044864e-292

    /// <summary>
    /// The population skewness, sqrt n * (M3 / M2) / sqrt M2; NaN when M2 is 0, when the count is zero, or when
    /// M2 * sqrt M2 is below 2^-970 (deviations below about 1e-97, where M3 may have left the normal range: rescale the
    /// data). The divisions are ordered so that no intermediate overflows or underflows while the moments are finite and
    /// normal.
    /// </summary>
    let skewness (m: HigherMoments) : float =
        let m2 = Sum.value m.M2

        if not (m2 > 0.0) || m2 * sqrt m2 < normalFloor then
            nan
        else
            sqrt m.N * (Sum.value m.M3 / m2) / sqrt m2

    /// <summary>
    /// The population excess kurtosis, n * (M4 / M2) / M2 - 3; NaN when M2 is 0, when the count is zero, or when
    /// M2 * M2 is below 2^-970 (deviations below about 1e-73, where M4 may have underflowed to a plausible -3: rescale
    /// the data). The divisions are ordered so that no intermediate overflows or underflows while the moments are finite
    /// and normal.
    /// </summary>
    let kurtosis (m: HigherMoments) : float =
        let m2 = Sum.value m.M2

        if not (m2 > 0.0) || m2 * m2 < normalFloor then
            nan
        else
            m.N * (Sum.value m.M4 / m2) / m2 - 3.0

    /// The count, mean and M2 as a Moments; `Moments.zero` when the count is zero.
    let toMoments (m: HigherMoments) : Moments =
        if m.N = 0.0 then
            Moments.zero
        else
            Moments.ofState m.N m.Mean m.M2

    /// True when the count is zero or every running total is finite.
    let internal isFinite (m: HigherMoments) : bool =
        m.N = 0.0
        || (Doubles.isFinite (Sum.value m.Mean)
            && Doubles.isFinite (Sum.value m.M2)
            && Doubles.isFinite (Sum.value m.M3)
            && Doubles.isFinite (Sum.value m.M4))
