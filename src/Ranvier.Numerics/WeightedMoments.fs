namespace Ranvier.Numerics

open System

/// <summary>Observation count, total weight, weighted mean and weighted spread of a set of weighted observations. Immutable.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carries. A weight is never negative: a weight of 0 counts the
/// observation and leaves every other total, and a NaN weight makes the reads NaN. Every read other than the count is
/// NaN on an empty accumulator, and where its divisor is not positive.
/// </remarks>
[<Struct; NoComparison>]
type WeightedMoments =
    private
        {
            N: float
            W: Sum
            W2: Sum
            Mean: Sum
            S: Sum
        }

module WeightedMoments =
    let zero: WeightedMoments =
        {
            N = 0.0
            W = Sum.zero
            W2 = Sum.zero
            Mean = Sum.zero
            S = Sum.zero
        }

    let private checkWeight (w: float) : unit =
        if w < 0.0 then
            raise (ArgumentOutOfRangeException("w", "A weight must not be negative."))

    // n observations, none of positive weight.
    let private unweighted (n: float) : WeightedMoments = { zero with N = n }

    /// <summary>
    /// Raises ArgumentOutOfRangeException when the weight is negative. A weight of 0 raises the count and leaves the
    /// other totals.
    /// </summary>
    let add (compensated: bool) (m: WeightedMoments) (x: float) (w: float) : WeightedMoments =
        checkWeight w

        if w = 0.0 then
            { m with N = m.N + 1.0 }
        else
            let total = Sum.add compensated m.W w
            let delta = x - Sum.value m.Mean
            let mean = Sum.add compensated m.Mean (delta * (w / Sum.value total))

            {
                N = m.N + 1.0
                W = total
                W2 = Sum.add compensated m.W2 (w * w)
                Mean = mean
                S = Sum.add compensated m.S (w * delta * (x - Sum.value mean))
            }

    /// <summary>
    /// Raises InvalidOperationException when the count is zero, and ArgumentOutOfRangeException when the weight is
    /// negative. Returns <c>zero</c> when the count reaches zero. At a count of one the spread is exactly zero. The
    /// weight and the observation must be a pair that was added; the totals are otherwise not those of any set.
    /// </summary>
    let remove (compensated: bool) (m: WeightedMoments) (x: float) (w: float) : WeightedMoments =
        checkWeight w

        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation from an empty set of weighted moments.")
        elif m.N = 1.0 then
            zero
        elif w = 0.0 then
            { m with N = m.N - 1.0 }
        else
            let remaining = Sum.add compensated m.W (-w)
            let n = m.N - 1.0

            if not (Sum.value remaining > 0.0) then
                unweighted n
            else
                let oldMean = Sum.value m.Mean
                let mean = Sum.add compensated m.Mean (-((x - oldMean) * (w / Sum.value remaining)))

                {
                    N = n
                    W = remaining
                    W2 = Sum.add compensated m.W2 (-(w * w))
                    Mean = mean
                    S = if n = 1.0 then Sum.zero else Sum.add compensated m.S (-(w * (x - oldMean) * (x - Sum.value mean)))
                }

    /// Returns `b` itself when `a` is empty, and `a` itself when `b` is empty.
    let merge (compensated: bool) (a: WeightedMoments) (b: WeightedMoments) : WeightedMoments =
        if a.N = 0.0 then
            b
        elif b.N = 0.0 then
            a
        elif not (Sum.value b.W > 0.0) then
            { a with N = a.N + b.N }
        elif not (Sum.value a.W > 0.0) then
            { b with N = a.N + b.N }
        else
            let wa = Sum.value a.W
            let wb = Sum.value b.W
            let total = wa + wb
            let delta = Sum.value b.Mean - Sum.value a.Mean

            {
                N = a.N + b.N
                W = Sum.add compensated a.W wb
                W2 = Sum.add compensated a.W2 (Sum.value b.W2)
                Mean = Sum.add compensated a.Mean (delta * (wb / total))
                S = Sum.add compensated (Sum.add compensated a.S (Sum.value b.S)) (delta * delta * (wa * (wb / total)))
            }

    /// `unmerge whole part` is the moments of the observations of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more observations than `whole`; returns `zero` when the counts are equal,
    /// and `whole` itself when `part` is empty. At a remaining count of one the spread is exactly zero.
    let unmerge (compensated: bool) (whole: WeightedMoments) (part: WeightedMoments) : WeightedMoments =
        let n = whole.N - part.N

        if n < 0.0 then
            raise (InvalidOperationException "Cannot unmerge a part with more observations than the whole.")
        elif n = 0.0 then
            zero
        elif part.N = 0.0 then
            whole
        elif not (Sum.value part.W > 0.0) then
            { whole with N = n }
        else
            let remaining = Sum.add compensated whole.W (-(Sum.value part.W))

            if not (Sum.value remaining > 0.0) then
                unweighted n
            else
                let wa = Sum.value remaining
                let wb = Sum.value part.W
                let wholeMean = Sum.value whole.Mean
                let mean = Sum.add compensated whole.Mean (-((wb / wa) * (Sum.value part.Mean - wholeMean)))
                let delta = Sum.value part.Mean - Sum.value mean

                {
                    N = n
                    W = remaining
                    W2 = Sum.add compensated whole.W2 (-(Sum.value part.W2))
                    Mean = mean
                    S =
                        if n = 1.0 then
                            Sum.zero
                        else
                            Sum.add
                                compensated
                                (Sum.add compensated whole.S (-(Sum.value part.S)))
                                (-(delta * delta * (wa * (wb / Sum.value whole.W))))
                }

    /// The number of observations, zero weights included; a whole number, exact up to 2^53.
    let count (m: WeightedMoments) : float = m.N

    /// The total weight, W; NaN when the count is zero.
    let totalWeight (m: WeightedMoments) : float =
        if m.N = 0.0 then nan else Sum.value m.W

    /// The weighted mean, sum of w * x over W; NaN when W is not positive.
    let mean (m: WeightedMoments) : float =
        if Sum.value m.W > 0.0 then Sum.value m.Mean else nan

    let private spread (m: WeightedMoments) (divisor: float) : float =
        if m.N = 0.0 || not (divisor > 0.0) then
            nan
        else
            let v = Sum.value m.S / divisor
            if v < 0.0 then 0.0 else v

    /// The weighted population variance, S / W, never negative; NaN when W is not positive.
    let variance (m: WeightedMoments) : float = spread m (Sum.value m.W)

    /// The variance for frequency weights, S / (W - 1), never negative; NaN when W is at most 1.
    let frequencyVariance (m: WeightedMoments) : float = spread m (Sum.value m.W - 1.0)

    /// <summary>
    /// The variance for reliability weights, S / (W - W2 / W), where W2 is the sum of the squared weights, never
    /// negative; NaN when the divisor is not positive, which includes a set of one observation.
    /// </summary>
    let reliabilityVariance (m: WeightedMoments) : float =
        let w = Sum.value m.W
        spread m (w - Sum.value m.W2 / w)

    /// Kish's effective count, W^2 / W2, written (W / W2) * W so that the squares are not formed; NaN when W2 is not positive.
    let effectiveCount (m: WeightedMoments) : float =
        let w2 = Sum.value m.W2
        if w2 > 0.0 then Sum.value m.W / w2 * Sum.value m.W else nan

    /// True when the count is zero or every running total is finite.
    let internal isFinite (m: WeightedMoments) : bool =
        let finite (x: float) = abs x < infinity

        m.N = 0.0
        || (finite (Sum.value m.W)
            && finite (Sum.value m.W2)
            && finite (Sum.value m.Mean)
            && finite (Sum.value m.S))
