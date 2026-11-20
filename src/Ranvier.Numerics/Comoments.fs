namespace Ranvier.Numerics

open System

/// <summary>Count, means, spreads and co-moment of a set of (x, y) pairs. Immutable.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carries. Every read is NaN on an empty accumulator.
/// </remarks>
[<Struct; NoComparison>]
type Comoments =
    private
        {
            N: float
            MeanX: Sum
            MeanY: Sum
            M2X: Sum
            M2Y: Sum
            C: Sum
        }

module Comoments =
    let zero: Comoments =
        {
            N = 0.0
            MeanX = Sum.zero
            MeanY = Sum.zero
            M2X = Sum.zero
            M2Y = Sum.zero
            C = Sum.zero
        }

    let add (compensated: bool) (m: Comoments) (x: float) (y: float) : Comoments =
        let n = m.N + 1.0
        let dx = x - Sum.value m.MeanX
        let dy = y - Sum.value m.MeanY
        let meanX = Sum.add compensated m.MeanX (dx / n)
        let meanY = Sum.add compensated m.MeanY (dy / n)

        {
            N = n
            MeanX = meanX
            MeanY = meanY
            M2X = Sum.add compensated m.M2X (dx * (x - Sum.value meanX))
            M2Y = Sum.add compensated m.M2Y (dy * (y - Sum.value meanY))
            C = Sum.add compensated m.C (dx * (y - Sum.value meanY))
        }

    /// <summary>
    /// Raises InvalidOperationException when the count is zero. Returns <c>zero</c> when the count reaches zero. At a
    /// count of one the spreads and the co-moment are exactly zero and the means equal the remaining pair within
    /// rounding.
    /// </summary>
    let remove (compensated: bool) (m: Comoments) (x: float) (y: float) : Comoments =
        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove a pair from an empty set of comoments.")
        elif m.N = 1.0 then
            zero
        else
            let n = m.N - 1.0
            let dx = x - Sum.value m.MeanX
            let dy = y - Sum.value m.MeanY
            let meanX = Sum.add compensated m.MeanX (-(dx / n))
            let meanY = Sum.add compensated m.MeanY (-(dy / n))

            if n = 1.0 then
                { N = n; MeanX = meanX; MeanY = meanY; M2X = Sum.zero; M2Y = Sum.zero; C = Sum.zero }
            else
                {
                    N = n
                    MeanX = meanX
                    MeanY = meanY
                    M2X = Sum.add compensated m.M2X (-(dx * (x - Sum.value meanX)))
                    M2Y = Sum.add compensated m.M2Y (-(dy * (y - Sum.value meanY)))
                    C = Sum.add compensated m.C (-(dx * (y - Sum.value meanY)))
                }

    /// Returns `b` itself when `a` is empty, and `a` itself when `b` is empty.
    let merge (compensated: bool) (a: Comoments) (b: Comoments) : Comoments =
        if a.N = 0.0 then
            b
        elif b.N = 0.0 then
            a
        else
            let n = a.N + b.N
            let dx = Sum.value b.MeanX - Sum.value a.MeanX
            let dy = Sum.value b.MeanY - Sum.value a.MeanY
            let weight = a.N * b.N / n

            {
                N = n
                MeanX = Sum.add compensated a.MeanX (dx * (b.N / n))
                MeanY = Sum.add compensated a.MeanY (dy * (b.N / n))
                M2X = Sum.add compensated (Sum.add compensated a.M2X (Sum.value b.M2X)) (dx * dx * weight)
                M2Y = Sum.add compensated (Sum.add compensated a.M2Y (Sum.value b.M2Y)) (dy * dy * weight)
                C = Sum.add compensated (Sum.add compensated a.C (Sum.value b.C)) (dx * dy * weight)
            }

    /// `unmerge whole part` is the comoments of the pairs of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more pairs than `whole`; returns `zero` when the counts are equal,
    /// and `whole` itself when `part` is empty. At a remaining count of one the spreads and the co-moment are exactly zero.
    let unmerge (compensated: bool) (whole: Comoments) (part: Comoments) : Comoments =
        let n = whole.N - part.N

        if n < 0.0 then
            raise (InvalidOperationException "Cannot unmerge a part with more pairs than the whole.")
        elif n = 0.0 then
            zero
        elif part.N = 0.0 then
            whole
        else
            let share = part.N / n

            let meanX =
                Sum.add compensated whole.MeanX (-(share * (Sum.value part.MeanX - Sum.value whole.MeanX)))

            let meanY =
                Sum.add compensated whole.MeanY (-(share * (Sum.value part.MeanY - Sum.value whole.MeanY)))

            if n = 1.0 then
                { N = n; MeanX = meanX; MeanY = meanY; M2X = Sum.zero; M2Y = Sum.zero; C = Sum.zero }
            else
                let dx = Sum.value part.MeanX - Sum.value meanX
                let dy = Sum.value part.MeanY - Sum.value meanY
                let weight = n * part.N / whole.N

                {
                    N = n
                    MeanX = meanX
                    MeanY = meanY
                    M2X = Sum.add compensated (Sum.add compensated whole.M2X (-(Sum.value part.M2X))) (-(dx * dx * weight))
                    M2Y = Sum.add compensated (Sum.add compensated whole.M2Y (-(Sum.value part.M2Y))) (-(dy * dy * weight))
                    C = Sum.add compensated (Sum.add compensated whole.C (-(Sum.value part.C))) (-(dx * dy * weight))
                }

    /// A whole number, exact up to 2^53 (a float so the count has one representation on both targets).
    let count (m: Comoments) : float = m.N

    /// NaN when the count is zero.
    let meanX (m: Comoments) : float =
        if m.N = 0.0 then nan else Sum.value m.MeanX

    /// NaN when the count is zero.
    let meanY (m: Comoments) : float =
        if m.N = 0.0 then nan else Sum.value m.MeanY

    let private spread (m2: Sum) (n: float) : float =
        if n = 0.0 then
            nan
        else
            let v = Sum.value m2 / n
            if v < 0.0 then 0.0 else v

    /// The population variance of x, never negative; NaN when the count is zero.
    let varianceX (m: Comoments) : float = spread m.M2X m.N

    /// The population variance of y, never negative; NaN when the count is zero.
    let varianceY (m: Comoments) : float = spread m.M2Y m.N

    /// The population covariance, Σ(x − meanX)(y − meanY) / n; NaN when the count is zero.
    let covariance (m: Comoments) : float =
        if m.N = 0.0 then nan else Sum.value m.C / m.N

    /// The sample covariance, Σ(x − meanX)(y − meanY) / (n − 1); NaN when the count is below 2.
    let sampleCovariance (m: Comoments) : float =
        if m.N < 2.0 then nan else Sum.value m.C / (m.N - 1.0)

    /// C / (√M2x · √M2y), where C is the co-moment and M2x, M2y the sums of squares about the means; NaN when the count is
    /// zero or either spread is 0. The result is clamped to [-1, 1]: the computed ratio can exceed 1 in magnitude by a few
    /// ulps on perfectly correlated data, and acos, atanh or sqrt (1 − r²) of such a value would be NaN. The two roots are taken apart so that neither the product of the spreads nor a
    /// square of a tiny one leaves the finite range.
    let correlation (m: Comoments) : float =
        let sx = Sum.value m.M2X
        let sy = Sum.value m.M2Y

        if m.N = 0.0 || not (sx > 0.0) || not (sy > 0.0) then
            nan
        else
            let r = Sum.value m.C / (sqrt sx * sqrt sy)
            if r > 1.0 then 1.0 elif r < -1.0 then -1.0 else r

    /// The least-squares slope of y on x, C / M2x; NaN when the count is zero or M2x is 0.
    let slope (m: Comoments) : float =
        let sx = Sum.value m.M2X

        if m.N = 0.0 || not (sx > 0.0) then nan else Sum.value m.C / sx

    /// The least-squares intercept, meanY − slope · meanX; NaN when the slope is.
    let intercept (m: Comoments) : float =
        meanY m - slope m * meanX m

    /// True when the count is zero or every running total is finite.
    let internal isFinite (m: Comoments) : bool =
        let finite (x: float) = abs x < infinity

        m.N = 0.0
        || (finite (Sum.value m.MeanX)
            && finite (Sum.value m.MeanY)
            && finite (Sum.value m.M2X)
            && finite (Sum.value m.M2Y)
            && finite (Sum.value m.C))
