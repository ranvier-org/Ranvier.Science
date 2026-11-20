namespace Ranvier.Numerics

open System

/// <summary>Observation count, total weight, weighted mean and weighted spread of a set of weighted observations. Immutable.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carries. A weight is never negative: a weight of 0 counts the
/// observation and leaves every other total. The number of observations of positive weight is tracked exactly; when it
/// returns to zero every weight total is exactly zero, whatever the rounding of the removals. A NaN weight, or a
/// removal that leaves positive-weight observations with a total weight of 0 or less, makes every weight read NaN
/// until the positive-weight count returns to zero. The ratio reads (mean, the variances, effective count) are NaN on
/// an empty accumulator and where their divisor is not positive.
/// </remarks>
[<Struct; NoComparison>]
type WeightedMoments =
    private
        {
            N: float
            P: float
            W: Sum
            W2: Sum
            Mean: Sum
            S: Sum
        }

module WeightedMoments =
    let zero: WeightedMoments =
        {
            N = 0.0
            P = 0.0
            W = Sum.zero
            W2 = Sum.zero
            Mean = Sum.zero
            S = Sum.zero
        }

    let private checkWeight (w: float) : unit =
        if w < 0.0 then
            raise (ArgumentOutOfRangeException("w", "A weight must not be negative."))

    // n observations, p of positive weight, whose weight totals are NaN.
    let private poisoned (n: float) (p: float) : WeightedMoments =
        let nanSum = Sum.add false Sum.zero nan
        { N = n; P = p; W = nanSum; W2 = nanSum; Mean = nanSum; S = nanSum }

    // True when positive-weight observations remain but the total weight is NaN or not positive.
    let private broken (m: WeightedMoments) : bool = m.P > 0.0 && not (Sum.value m.W > 0.0)

    /// <summary>
    /// Raises ArgumentOutOfRangeException when the weight is negative. A weight of 0 raises the count and leaves the
    /// other totals. A NaN weight poisons the state.
    /// </summary>
    let add (compensated: bool) (m: WeightedMoments) (x: float) (w: float) : WeightedMoments =
        checkWeight w

        if Double.IsNaN w then
            poisoned (m.N + 1.0) (m.P + 1.0)
        elif w = 0.0 then
            { m with N = m.N + 1.0 }
        elif broken m then
            poisoned (m.N + 1.0) (m.P + 1.0)
        else
            let total = Sum.add compensated m.W w
            let delta = x - Sum.value m.Mean
            let mean = Sum.add compensated m.Mean (delta * (w / Sum.value total))

            {
                N = m.N + 1.0
                P = m.P + 1.0
                W = total
                W2 = Sum.add compensated m.W2 (w * w)
                Mean = mean
                S = Sum.add compensated m.S (w * delta * (x - Sum.value mean))
            }

    /// <summary>
    /// Raises InvalidOperationException when the count is zero or when no positive-weight observation remains to be
    /// removed, and ArgumentOutOfRangeException when the weight is negative. Returns <c>zero</c> when the count reaches
    /// zero. When the last positive-weight observation goes, every weight total is exactly zero. At a count of one the
    /// spread is exactly zero. A removal of a weight larger than the total leaves a poisoned state. The weight and the
    /// observation must be a pair that was added; the totals are otherwise not those of any set.
    /// </summary>
    let remove (compensated: bool) (m: WeightedMoments) (x: float) (w: float) : WeightedMoments =
        checkWeight w

        if m.N <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation from an empty set of weighted moments.")
        elif w = 0.0 then
            if m.N = 1.0 then zero else { m with N = m.N - 1.0 }
        elif m.P <= 0.0 then
            raise (InvalidOperationException "Cannot remove an observation of positive weight: none remains.")
        else
            let n = m.N - 1.0
            let p = m.P - 1.0

            if p = 0.0 then
                { zero with N = n }
            elif Double.IsNaN w || broken m then
                poisoned n p
            else
                let remaining = Sum.add compensated m.W (-w)

                if not (Sum.value remaining > 0.0) then
                    poisoned n p
                else
                    let oldMean = Sum.value m.Mean
                    let mean = Sum.add compensated m.Mean (-((x - oldMean) * (w / Sum.value remaining)))

                    {
                        N = n
                        P = p
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
        elif b.P = 0.0 then
            { a with N = a.N + b.N }
        elif a.P = 0.0 then
            { b with N = a.N + b.N }
        else
            let wa = Sum.value a.W
            let wb = Sum.value b.W
            let total = wa + wb

            if broken a || broken b || not (total > 0.0) then
                poisoned (a.N + b.N) (a.P + b.P)
            else
                let delta = Sum.value b.Mean - Sum.value a.Mean

                {
                    N = a.N + b.N
                    P = a.P + b.P
                    W = Sum.add compensated a.W wb
                    W2 = Sum.add compensated a.W2 (Sum.value b.W2)
                    Mean = Sum.add compensated a.Mean (delta * (wb / total))
                    S = Sum.add compensated (Sum.add compensated a.S (Sum.value b.S)) (delta * delta * (wa * (wb / total)))
                }

    /// `unmerge whole part` is the moments of the observations of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more observations, or more positive-weight observations, than `whole`;
    /// returns `zero` when the counts are equal, and `whole` itself when `part` is empty. When no positive-weight observation
    /// remains every weight total is exactly zero. At a remaining count of one the spread is exactly zero.
    let unmerge (compensated: bool) (whole: WeightedMoments) (part: WeightedMoments) : WeightedMoments =
        let n = whole.N - part.N
        let p = whole.P - part.P

        if n < 0.0 || p < 0.0 then
            raise (InvalidOperationException "Cannot unmerge a part with more observations than the whole.")
        elif n = 0.0 then
            zero
        elif part.N = 0.0 then
            whole
        elif part.P = 0.0 then
            { whole with N = n }
        elif p = 0.0 then
            { zero with N = n }
        else
            let wb = Sum.value part.W
            let remaining = Sum.add compensated whole.W (-wb)
            let wa = Sum.value remaining

            if broken whole || broken part || not (wa > 0.0) then
                poisoned n p
            else
                let wholeMean = Sum.value whole.Mean
                let mean = Sum.add compensated whole.Mean (-((wb / wa) * (Sum.value part.Mean - wholeMean)))
                let delta = Sum.value part.Mean - Sum.value mean

                {
                    N = n
                    P = p
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

    /// The total weight, W; 0 when the count is zero, NaN for a poisoned state.
    let totalWeight (m: WeightedMoments) : float = Sum.value m.W

    // True when the weight reads are defined: a positive-weight observation exists and the total weight is positive.
    let private defined (m: WeightedMoments) : bool = m.P > 0.0 && Sum.value m.W > 0.0

    /// The weighted mean, sum of w * x over W; NaN when W is not positive or the state is poisoned.
    let mean (m: WeightedMoments) : float =
        if defined m then Sum.value m.Mean else nan

    let private spread (m: WeightedMoments) (divisor: float) : float =
        if not (defined m) || not (divisor > 0.0) then
            nan
        else
            let v = Sum.value m.S / divisor
            if v < 0.0 then 0.0 else v

    /// The weighted population variance, S / W, never negative; NaN when W is not positive or the state is poisoned.
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
        if defined m && w2 > 0.0 then Sum.value m.W / w2 * Sum.value m.W else nan

    /// True when the count is zero or every running total is finite.
    let internal isFinite (m: WeightedMoments) : bool =
        let finite (x: float) = abs x < infinity

        m.N = 0.0
        || (finite (Sum.value m.W)
            && finite (Sum.value m.W2)
            && finite (Sum.value m.Mean)
            && finite (Sum.value m.S))
