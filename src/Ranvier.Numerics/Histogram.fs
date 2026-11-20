namespace Ranvier.Numerics

open System

/// Counts over fixed edges, with the under-range, over-range and NaN observations kept apart. `=` compares the edges
/// and every count.
type Histogram =
    private
        {
            Edges: float[]
            Counts: int[]
            Underflow: int
            Overflow: int
            Missing: int
        }

module Histogram =
    let private checkEdges (edges: float[]) =
        if edges.Length < 2 then
            raise (ArgumentException($"A histogram needs at least two edges, got {edges.Length}.", "edges"))

        for i in 0 .. edges.Length - 1 do
            if Double.IsNaN edges[i] || Double.IsInfinity edges[i] then
                raise (ArgumentException("Histogram edges must be finite.", "edges"))

            if i > 0 && not (edges[i] > edges[i - 1]) then
                raise (ArgumentException("Histogram edges must be strictly increasing.", "edges"))

    /// An empty histogram over the edges, which are copied. The bins are [e0, e1), [e1, e2), ... and the last, closed
    /// on both sides, [e(n-2), e(n-1)]. Raises `ArgumentException` unless there are at least two edges, all finite and
    /// strictly increasing.
    let create (edges: float[]) : Histogram =
        checkEdges edges

        {
            Edges = Array.copy edges
            Counts = Array.zeroCreate (edges.Length - 1)
            Underflow = 0
            Overflow = 0
            Missing = 0
        }

    /// An empty histogram of equal-width bins from lower to upper: edge i is lower + (upper - lower) * (i / bins), or
    /// 2 * (lower / 2 + (upper / 2 - lower / 2) * (i / bins)) where upper - lower overflows, and the last edge is
    /// exactly upper. Raises `ArgumentOutOfRangeException` unless lower and upper are finite with lower < upper and
    /// bins is at least 1, and when the interval holds too few doubles for the edges to be strictly increasing.
    let uniform (lower: float) (upper: float) (bins: int) : Histogram =
        if Double.IsNaN lower || Double.IsInfinity lower || Double.IsNaN upper || Double.IsInfinity upper || not (lower < upper) then
            raise (ArgumentOutOfRangeException("lower", "The bounds must be finite with lower below upper."))

        if bins < 1 then
            raise (ArgumentOutOfRangeException("bins", "A histogram needs at least one bin."))

        let width = upper - lower

        let edge i =
            if i = bins then upper
            elif Double.IsInfinity width then 2.0 * (lower / 2.0 + (upper / 2.0 - lower / 2.0) * (float i / float bins))
            else lower + width * (float i / float bins)

        let edges = Array.init (bins + 1) edge

        for i in 1..bins do
            if not (edges[i] > edges[i - 1]) then
                raise (ArgumentOutOfRangeException("bins", "The interval is too narrow for that many distinct edges."))

        create edges

    // The bin of a non-NaN x: -1 below the first edge, Counts.Length above the last, else the index i with
    // e(i) <= x < e(i+1), the last bin including its upper edge. Binary search.
    let private binOf (edges: float[]) (x: float) : int =
        let last = edges.Length - 1

        if x < edges[0] then
            -1
        elif x > edges[last] then
            last
        elif x = edges[last] then
            last - 1
        else
            let mutable lo = 0
            let mutable hi = last

            // Invariant: edges[lo] <= x < edges[hi].
            while hi - lo > 1 do
                let mid = (lo + hi) / 2
                if edges[mid] <= x then lo <- mid else hi <- mid

            lo

    let private change (h: Histogram) (x: float) (delta: int) : Histogram =
        if Double.IsNaN x then
            { h with Missing = h.Missing + delta }
        else
            let b = binOf h.Edges x

            if b < 0 then
                { h with Underflow = h.Underflow + delta }
            elif b >= h.Counts.Length then
                { h with Overflow = h.Overflow + delta }
            else
                let counts = Array.copy h.Counts
                counts[b] <- counts[b] + delta
                { h with Counts = counts }

    /// The histogram with one more observation: a NaN counts as missing, a value below the first edge as underflow, one
    /// above the last edge as overflow, otherwise in its bin. Copies the counts, O(bins), plus a binary search.
    let add (h: Histogram) (x: float) : Histogram = change h x 1

    /// The histogram without one observation of the value, the inverse of `add`. Raises `InvalidOperationException`
    /// when the count the value falls in (its bin, underflow, overflow or missing) is 0. Copies the counts, O(bins).
    let remove (h: Histogram) (x: float) : Histogram =
        let count =
            if Double.IsNaN x then
                h.Missing
            else
                let b = binOf h.Edges x
                if b < 0 then h.Underflow elif b >= h.Counts.Length then h.Overflow else h.Counts[b]

        if count = 0 then
            raise (InvalidOperationException($"Cannot remove {x}: its count is already 0."))

        change h x -1

    let private sameEdges (name: string) (a: Histogram) (b: Histogram) =
        if a.Edges <> b.Edges then
            raise (
                ArgumentException(
                    $"The histograms have different edges; they have {a.Edges.Length} and {b.Edges.Length} edges.",
                    name
                )
            )

    /// The histogram whose every count is the sum of the two. Raises `ArgumentException` naming both edge counts unless
    /// the edges are equal. O(bins).
    let merge (a: Histogram) (b: Histogram) : Histogram =
        sameEdges "b" a b

        { a with
            Counts = Array.map2 (+) a.Counts b.Counts
            Underflow = a.Underflow + b.Underflow
            Overflow = a.Overflow + b.Overflow
            Missing = a.Missing + b.Missing }

    /// The histogram of whole without part, the inverse of `merge`. Raises `ArgumentException` naming both edge counts
    /// unless the edges are equal, and `InvalidOperationException` when any count of part exceeds the same count of
    /// whole. O(bins).
    let unmerge (whole: Histogram) (part: Histogram) : Histogram =
        sameEdges "part" whole part
        let counts = Array.map2 (-) whole.Counts part.Counts

        if Array.exists (fun c -> c < 0) counts
           || whole.Underflow < part.Underflow
           || whole.Overflow < part.Overflow
           || whole.Missing < part.Missing then
            raise (InvalidOperationException("The part holds more observations than the whole."))

        { whole with
            Counts = counts
            Underflow = whole.Underflow - part.Underflow
            Overflow = whole.Overflow - part.Overflow
            Missing = whole.Missing - part.Missing }

    /// A copy of the edges.
    let edges (h: Histogram) : float[] = Array.copy h.Edges

    /// A copy of the bin counts, one fewer than the edges.
    let counts (h: Histogram) : int[] = Array.copy h.Counts

    /// The observations below the first edge.
    let underflow (h: Histogram) : int = h.Underflow

    /// The observations above the last edge.
    let overflow (h: Histogram) : int = h.Overflow

    /// The NaN observations.
    let missing (h: Histogram) : int = h.Missing

    /// Every observation: in range, under, over and missing.
    let total (h: Histogram) : int =
        Array.sum h.Counts + h.Underflow + h.Overflow + h.Missing

/// The P-squared streaming estimate of one quantile (Jain and Chlamtac, Comm. ACM 28(10), 1985): five markers, no
/// stored sample. Add only. A NaN or infinite observation, at any position, makes `value` NaN from then on, since an
/// infinite marker height turns the parabolic step into infinity minus infinity.
type QuantileEstimate =
    private
        {
            P: float
            N: int
            // Marker heights; before the fifth observation, the observations so far, unsorted.
            Heights: float[]
            // Marker positions.
            Positions: float[]
            // Desired marker positions.
            Desired: float[]
            Poisoned: bool
        }

module QuantileEstimate =
    /// An empty estimate of the p-quantile. Raises `ArgumentOutOfRangeException` unless 0 < p < 1, and so for a NaN p.
    let create (p: float) : QuantileEstimate =
        if not (p > 0.0 && p < 1.0) then
            raise (ArgumentOutOfRangeException("p", "A probability must be strictly between 0 and 1."))

        {
            P = p
            N = 0
            Heights = Array.zeroCreate 5
            Positions = Array.zeroCreate 5
            Desired = Array.zeroCreate 5
            Poisoned = false
        }

    // Marker increments 0, p/2, p, (1 + p)/2, 1 and initial desired positions 1, 1 + 2p, 1 + 4p, 3 + 2p, 5, from
    // Jain and Chlamtac 1985 (general knowledge: the paper was not read here).
    let private increments (p: float) = [| 0.0; p / 2.0; p; (1.0 + p) / 2.0; 1.0 |]

    let private sign (d: float) = if d >= 0.0 then 1.0 else -1.0

    /// The estimate with one more observation. A NaN or infinite observation makes `value` NaN from then on. O(1): the
    /// state of 15 numbers is copied.
    let add (e: QuantileEstimate) (x: float) : QuantileEstimate =
        if e.Poisoned || Double.IsNaN x || Double.IsInfinity x then
            { e with N = e.N + 1; Poisoned = true }
        elif e.N < 4 then
            let q = Array.copy e.Heights
            q[e.N] <- x
            { e with N = e.N + 1; Heights = q }
        elif e.N = 4 then
            let q = Array.copy e.Heights
            q[4] <- x
            Array.sortInPlace q
            let p = e.P

            { e with
                N = 5
                Heights = q
                Positions = [| 1.0; 2.0; 3.0; 4.0; 5.0 |]
                Desired = [| 1.0; 1.0 + 2.0 * p; 1.0 + 4.0 * p; 3.0 + 2.0 * p; 5.0 |] }
        else
            let q = Array.copy e.Heights
            let n = Array.copy e.Positions
            let nd = Array.copy e.Desired
            let dn = increments e.P

            let k =
                if x < q[0] then
                    q[0] <- x
                    0
                elif x < q[1] then 0
                elif x < q[2] then 1
                elif x < q[3] then 2
                elif x <= q[4] then 3
                else
                    q[4] <- x
                    3

            for i in k + 1 .. 4 do
                n[i] <- n[i] + 1.0

            for i in 0 .. 4 do
                nd[i] <- nd[i] + dn[i]

            for i in 1..3 do
                let d = nd[i] - n[i]

                if (d >= 1.0 && n[i + 1] - n[i] > 1.0) || (d <= -1.0 && n[i - 1] - n[i] < -1.0) then
                    let s = sign d

                    let parabolic =
                        q[i]
                        + s / (n[i + 1] - n[i - 1])
                          * ((n[i] - n[i - 1] + s) * (q[i + 1] - q[i]) / (n[i + 1] - n[i])
                             + (n[i + 1] - n[i] - s) * (q[i] - q[i - 1]) / (n[i] - n[i - 1]))

                    if q[i - 1] < parabolic && parabolic < q[i + 1] then
                        q[i] <- parabolic
                    else
                        let j = if s > 0.0 then i + 1 else i - 1
                        q[i] <- q[i] + s * (q[j] - q[i]) / (n[j] - n[i])

                    n[i] <- n[i] + s

            { e with N = e.N + 1; Heights = q; Positions = n; Desired = nd }

    /// The number of observations added, non-finite ones included.
    let count (e: QuantileEstimate) : float = float e.N

    /// The estimate: NaN when empty or after a non-finite observation; below five observations the type-7 quantile of the
    /// sorted observations (`OrderStatistic.quantileSorted`); from the fifth, the height of the middle marker, an
    /// estimate with no error bound given here.
    let value (e: QuantileEstimate) : float =
        if e.Poisoned || e.N = 0 then
            nan
        elif e.N < 5 then
            let sorted = Array.sub e.Heights 0 e.N
            Array.sortInPlace sorted
            OrderStatistic.quantileSorted e.P sorted
        else
            e.Heights[2]
