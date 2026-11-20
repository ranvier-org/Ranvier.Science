namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>The size, centre, spread and quartiles of a sample. Every field but Count is NaN for an empty sample or
/// one that holds a NaN; StandardDeviation is also NaN for one item.</summary>
type Summary =
    {
        Count: int
        Mean: float
        /// Divisor n − 1.
        StandardDeviation: float
        Minimum: float
        LowerQuartile: float
        Median: float
        UpperQuartile: float
        Maximum: float
    }

/// <summary>How <c>Describe.binEdges</c> chooses the number of equal-width bins, following NumPy's
/// <c>histogram_bin_edges</c>.</summary>
type BinRule =
    /// ⌈log₂ n + 1⌉ bins.
    | Sturges
    /// Width σ·(24√π / n)^(1/3) with σ the population standard deviation.
    | Scott
    /// Width 2·IQR / n^(1/3).
    | FreedmanDiaconis
    /// This many bins, which must be at least 1.
    | Count of int

/// <summary>Order statistics, ranks, autocorrelation, a five-number summary and histogram bin edges of a sample held
/// in a float array.</summary>
/// <remarks>
/// A function that reads values (every one but <c>quantileSorted</c> and <c>ranks</c>) returns NaN, or NaN fields,
/// when the array holds a NaN and raises nothing for it; <c>ranks</c> raises. The bin rules are those of numpy 2.x
/// <c>histogram_bin_edges</c>, from documentation (numpy.org, numpy.histogram_bin_edges), pinned by the test "bin edges
/// follow NumPy's rules". The costs given are general knowledge of the algorithms used.
/// </remarks>
module Describe =
    let private hasNaN (xs: float[]) : bool =
        let mutable found = false
        let mutable i = 0

        while not found && i < xs.Length do
            found <- Double.IsNaN xs[i]
            i <- i + 1

        found

    // The sorted copy, or None when xs holds a NaN. O(n log n).
    let private sortedOrNone (xs: float[]) : float[] option =
        if hasNaN xs then None else Some(Array.sort xs)

    let private mean (xs: float[]) : float =
        let mutable sum = 0.0

        for x in xs do
            sum <- sum + x

        sum / float xs.Length

    /// <summary>The type-7 quantile of an array already sorted ascending, linear between order statistics: with
    /// h = (n − 1)p, (1 − f)·x[⌊h⌋] + f·x[⌊h⌋ + 1] where f = h − ⌊h⌋. This is <c>OrderStatistic.quantileSorted</c>:
    /// NaN for an empty array, ArgumentOutOfRangeException unless 0 ≤ p ≤ 1 (a NaN p raises). The sort order is not
    /// checked and a NaN in the array is not looked for. O(1). Pinned by "quantiles and quantileSorted agree with
    /// quantile".</summary>
    let quantileSorted (p: float) (sorted: float[]) : float = OrderStatistic.quantileSorted p sorted

    /// <summary>The type-7 quantile at the probability p of an unsorted array, which is not changed (a sorted copy is
    /// read). NaN for an empty array, for an array that holds a NaN, and for a NaN p. Raises
    /// ArgumentOutOfRangeException unless 0 ≤ p ≤ 1 or p is NaN. O(n log n). Pinned by "quantile type 7", "quantile of
    /// an empty array is NaN" and "NaN in the data".</summary>
    let quantile (p: float) (xs: float[]) : float =
        if Double.IsNaN p then
            nan
        else
            Check.probability "p" p

            match sortedOrNone xs with
            | None -> nan
            | Some sorted -> OrderStatistic.quantileSorted p sorted

    /// <summary>The type-7 quantile at each probability, from one sort of a copy: element i is
    /// <c>quantile ps[i] xs</c>. Raises ArgumentOutOfRangeException unless every probability is from 0 to 1 or NaN.
    /// O(n log n + m) for m probabilities. Pinned by "quantiles and quantileSorted agree with quantile" and "NaN in the
    /// data".</summary>
    let quantiles (ps: float[]) (xs: float[]) : float[] =
        for p in ps do
            if not (Double.IsNaN p) then
                Check.probability "ps" p

        match sortedOrNone xs with
        | None -> Array.create ps.Length nan
        | Some sorted ->
            ps
            |> Array.map (fun p -> if Double.IsNaN p then nan else OrderStatistic.quantileSorted p sorted)

    /// <summary>The median, <c>quantile 0.5</c>: the middle value, or the mean of the two middle values for an even
    /// count. NaN for an empty array or one that holds a NaN. O(n log n). Pinned by "median, interquartile range and
    /// median absolute deviation".</summary>
    let median (xs: float[]) : float = quantile 0.5 xs

    /// <summary>The third quartile minus the first, both type 7, from one sort. NaN for an empty array or one that
    /// holds a NaN. O(n log n). Pinned by "median, interquartile range and median absolute deviation".</summary>
    let interquartileRange (xs: float[]) : float =
        match sortedOrNone xs with
        | None -> nan
        | Some sorted -> OrderStatistic.quantileSorted 0.75 sorted - OrderStatistic.quantileSorted 0.25 sorted

    /// <summary>The median of the absolute deviations from the median, unscaled; multiply by 1.4826 (general
    /// knowledge: 1/Φ⁻¹(0.75)) for a consistent estimate of the standard deviation of a normal sample. NaN for an
    /// empty array or one that holds a NaN. O(n log n). Pinned by "median, interquartile range and median absolute
    /// deviation".</summary>
    let medianAbsoluteDeviation (xs: float[]) : float =
        let m = median xs

        if Double.IsNaN m then nan else median (Array.map (fun x -> abs (x - m)) xs)

    /// <summary>The mean after removing ⌊n · proportion⌋ values from each end of the sorted array (R's
    /// <c>mean(trim =)</c> and SciPy's <c>trim_mean</c>, general knowledge). Raises ArgumentOutOfRangeException
    /// unless 0 ≤ proportion &lt; 0.5, so NaN raises. NaN for an empty array or one that holds a NaN. O(n log n).
    /// Pinned by "trimmed mean".</summary>
    let trimmedMean (proportion: float) (xs: float[]) : float =
        if not (proportion >= 0.0 && proportion < 0.5) then
            raise (ArgumentOutOfRangeException("proportion", "proportion must be at least 0 and less than 0.5."))

        match sortedOrNone xs with
        | None -> nan
        | Some sorted when sorted.Length = 0 -> nan
        | Some sorted ->
            let cut = int (Math.Floor(float sorted.Length * proportion))
            mean sorted[cut .. sorted.Length - 1 - cut]

    /// <summary>The 1-based rank of each value, tied values sharing the average of the positions they occupy.
    /// Raises ArgumentException when the array holds a NaN. O(n log n). Pinned by "ranks with ties".</summary>
    let ranks (xs: float[]) : float[] =
        if hasNaN xs then
            raise (ArgumentException("xs must not hold NaN.", "xs"))

        let n = xs.Length
        let order = Array.init n id |> Array.sortWith (fun a b -> compare xs[a] xs[b])
        let result = Array.zeroCreate n
        let mutable start = 0

        while start < n do
            let mutable stop = start

            while stop + 1 < n && xs[order[stop + 1]] = xs[order[start]] do
                stop <- stop + 1

            // Positions start + 1 .. stop + 1 average to (start + stop) / 2 + 1.
            let rank = float (start + stop) / 2.0 + 1.0

            for k in start..stop do
                result[order[k]] <- rank

            start <- stop + 1

        result

    /// <summary>The sample autocorrelation at lags 0 to maxLag: r_k = Σ_{t&lt;n−k}(x_t − x̄)(x_{t+k} − x̄) /
    /// Σ(x_t − x̄)², so r_0 = 1. NaN at every lag for a constant series or one that holds a NaN. Raises
    /// ArgumentOutOfRangeException unless 0 ≤ maxLag &lt; n. O(n · maxLag). Pinned by "autocorrelation".</summary>
    let autocorrelation (maxLag: int) (xs: float[]) : float[] =
        let n = xs.Length

        if maxLag < 0 || maxLag >= n then
            raise (ArgumentOutOfRangeException("maxLag", "maxLag must be at least 0 and less than the length of xs."))

        let m = mean xs
        let mutable denominator = 0.0

        for x in xs do
            denominator <- denominator + (x - m) * (x - m)

        Array.init (maxLag + 1) (fun k ->
            let mutable sum = 0.0

            for t in 0 .. n - k - 1 do
                sum <- sum + (xs[t] - m) * (xs[t + k] - m)

            sum / denominator)

    /// <summary>Count, mean, sample standard deviation (divisor n − 1), minimum, quartiles (type 7), median and
    /// maximum. Count is the array's length; for an empty array or one that holds a NaN every other field is NaN, and
    /// StandardDeviation is NaN for one item. Two passes for the moments plus one sort of a copy: O(n log n). Pinned by
    /// "summary", "summary of an empty array and of one item" and "NaN in the data".</summary>
    let summary (xs: float[]) : Summary =
        let n = xs.Length

        match sortedOrNone xs with
        | Some sorted when n > 0 ->
            let m = mean xs
            let mutable squares = 0.0

            for x in xs do
                squares <- squares + (x - m) * (x - m)

            {
                Count = n
                Mean = m
                StandardDeviation = if n < 2 then nan else sqrt (squares / float (n - 1))
                Minimum = sorted[0]
                LowerQuartile = OrderStatistic.quantileSorted 0.25 sorted
                Median = OrderStatistic.quantileSorted 0.5 sorted
                UpperQuartile = OrderStatistic.quantileSorted 0.75 sorted
                Maximum = sorted[n - 1]
            }
        | _ ->
            {
                Count = n
                Mean = nan
                StandardDeviation = nan
                Minimum = nan
                LowerQuartile = nan
                Median = nan
                UpperQuartile = nan
                Maximum = nan
            }

    // A rule that asks for more bins than this raises rather than allocating them.
    let private maxBins = 16777216.0

    /// <summary>The n + 1 edges of equal-width bins over the data's range, by NumPy's rules: <c>Count k</c> gives k
    /// bins; the others give a bin width and ⌈range / width⌉ bins (Sturges directly ⌈log₂ n + 1⌉). The first edge is
    /// the minimum, the last exactly the maximum. Constant data, or a width of 0, gives one bin over
    /// [x − 0.5, x + 0.5] (as NumPy does). Raises ArgumentException for an empty array or a NaN or infinite value,
    /// ArgumentOutOfRangeException for <c>Count k</c> with k &lt; 1, and ArgumentException when a rule asks for more
    /// than 16 777 216 bins (a limit chosen here; NumPy has none). O(n log n) for the quartiles of Freedman–Diaconis,
    /// O(n) for the others. Pinned by "bin edges follow NumPy's rules" and "bin edges of constant data and bad
    /// input".</summary>
    let binEdges (rule: BinRule) (xs: float[]) : float[] =
        if xs.Length = 0 then
            raise (ArgumentException("xs must not be empty.", "xs"))

        for x in xs do
            if Double.IsNaN x || Double.IsInfinity x then
                raise (ArgumentException("xs must hold only finite values.", "xs"))

        match rule with
        | Count k when k < 1 -> raise (ArgumentOutOfRangeException("rule", "Count must be at least 1."))
        | _ -> ()

        let mutable lo = Array.min xs
        let mutable hi = Array.max xs
        let range = hi - lo
        let n = float xs.Length

        let bins =
            let fromWidth (width: float) =
                if width > 0.0 && range > 0.0 then
                    let b = Math.Ceiling(range / width)

                    if b > maxBins then
                        raise (ArgumentException("The rule asks for more than 16777216 bins; use Count.", "rule"))

                    max 1 (int b)
                else
                    1

            match rule with
            | Count k -> k
            | Sturges -> if range > 0.0 then int (Math.Ceiling(Math.Log2 n + 1.0)) else 1
            | Scott ->
                let m = mean xs
                let mutable squares = 0.0

                for x in xs do
                    squares <- squares + (x - m) * (x - m)

                fromWidth (sqrt (squares / n) * Math.Pow(24.0 * Math.Sqrt Math.PI / n, 1.0 / 3.0))
            | FreedmanDiaconis -> fromWidth (2.0 * interquartileRange xs / Math.Pow(n, 1.0 / 3.0))

        if range = 0.0 then
            lo <- lo - 0.5
            hi <- hi + 0.5

        let step = (hi - lo) / float bins
        Array.init (bins + 1) (fun k -> if k = bins then hi else lo + float k * step)
