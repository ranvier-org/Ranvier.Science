namespace Ranvier.Numerics

open System

/// How an `Interpolant` joins its knots.
type InterpolationKind =
    | Linear
    | NaturalCubic
    | MonotoneCubic

/// A piecewise interpolant through knots (x, y), made by `Interpolate.linear`, `Interpolate.naturalCubic` or
/// `Interpolate.monotoneCubic`. Immutable; it holds copies of the arrays it was built from. `=` compares the kind, the
/// knots and the per-knot coefficients with float equality, so two interpolants built from equal arrays are equal. One
/// built from a NaN y is, on .NET, not `=` to anything, itself included, as NaN is not (under Fable the same value
/// compared with itself was equal, measured here by the mocha run of InterpolateTests); one built from an infinite y
/// compares as its arrays do. Built from a y that is NaN or infinite, it evaluates and differentiates to NaN everywhere.
type Interpolant =
    internal
        { Kind: InterpolationKind
          Xs: float[]
          Ys: float[]
          /// Cubics: the slope at each knot. Empty for linear.
          Coefficients: float[]
          NonFinite: bool }

/// Evaluating and differentiating an `Interpolant`. Each call finds the interval by binary search, cost O(log n)
/// (general knowledge), and uses only + − × ÷ and comparisons.
module Interpolant =
    /// The index i of the interval [xs[i], xs[i + 1]] holding x, the last interval for x equal to the last knot, or -1
    /// when x is NaN or outside [xs[0], xs[n - 1]].
    let private locate (xs: float[]) (x: float) : int =
        let n = xs.Length

        if not (x >= xs[0] && x <= xs[n - 1]) then
            -1
        else
            let mutable low = 0
            let mutable high = n - 1

            while high - low > 1 do
                let mid = low + (high - low) / 2

                if xs[mid] <= x then
                    low <- mid
                else
                    high <- mid

            low

    /// The value at x: the interpolant's piece on the interval holding x. NaN for a NaN x, for an x outside
    /// [xs[0], xs[n - 1]], and for an interpolant built from a non-finite y. At a knot it is that knot's y. Cost O(log n).
    let evaluate (interpolant: Interpolant) (x: float) : float =
        if interpolant.NonFinite then
            nan
        else
            let xs = interpolant.Xs
            let ys = interpolant.Ys
            let i = locate xs x

            if i < 0 then
                nan
            else
                let h = xs[i + 1] - xs[i]

                match interpolant.Kind with
                | Linear ->
                    let a = (xs[i + 1] - x) / h
                    let b = (x - xs[i]) / h
                    a * ys[i] + b * ys[i + 1]
                | NaturalCubic
                | MonotoneCubic ->
                    let d = interpolant.Coefficients
                    let t = (x - xs[i]) / h
                    let t2 = t * t
                    let t3 = t2 * t
                    let h00 = 2.0 * t3 - 3.0 * t2 + 1.0
                    let h10 = t3 - 2.0 * t2 + t
                    let h01 = -2.0 * t3 + 3.0 * t2
                    let h11 = t3 - t2
                    h00 * ys[i] + h10 * h * d[i] + h01 * ys[i + 1] + h11 * h * d[i + 1]

    /// The first derivative at x. At an interior knot it is the right-hand interval's, at the last knot the left-hand
    /// interval's. NaN for a NaN x, for an x outside [xs[0], xs[n - 1]], and for an interpolant built from a non-finite
    /// y. A cubic interpolant's derivative is continuous at the interior knots; a linear one's is not. Cost O(log n).
    let derivative (interpolant: Interpolant) (x: float) : float =
        if interpolant.NonFinite then
            nan
        else
            let xs = interpolant.Xs
            let ys = interpolant.Ys
            let i = locate xs x

            if i < 0 then
                nan
            else
                let h = xs[i + 1] - xs[i]

                match interpolant.Kind with
                | Linear -> (ys[i + 1] - ys[i]) / h
                | NaturalCubic
                | MonotoneCubic ->
                    let d = interpolant.Coefficients
                    let t = (x - xs[i]) / h
                    let t2 = t * t
                    let dh01 = -6.0 * t2 + 6.0 * t
                    let dh10 = 3.0 * t2 - 4.0 * t + 1.0
                    let dh11 = 3.0 * t2 - 2.0 * t
                    dh01 * ((ys[i + 1] - ys[i]) / h) + dh10 * d[i] + dh11 * d[i + 1]

/// Builders of `Interpolant`s from knots. Each raises `ArgumentException` when xs is not strictly increasing (a NaN
/// breaks it), when an interval width x[i + 1] - x[i] is infinite (so an infinite x, or two finite knots further
/// apart than the largest float, raises, and the message names the interval), when it has fewer than two points, or
/// when ys has another length than xs (the message names both lengths). A NaN or infinite y is not an error: the interpolant evaluates to NaN everywhere. Each builder copies
/// its arrays, so later mutation of the inputs does not reach the interpolant. Cost O(n) to build (general
/// knowledge). With two points every kind is the straight line through them.
module Interpolate =
    let private build
        (name: string)
        (kind: InterpolationKind)
        (coefficients: float[] -> float[] -> float[])
        (xs: float[])
        (ys: float[])
        : Interpolant =
        if xs.Length <> ys.Length then
            raise (
                ArgumentException(
                    sprintf "%s needs as many ys as xs; xs has length %d and ys has length %d." name xs.Length ys.Length
                )
            )

        if xs.Length < 2 then
            raise (ArgumentException(sprintf "%s needs at least two points; xs has length %d." name xs.Length))

        for i in 0 .. xs.Length - 2 do
            if not (xs[i] < xs[i + 1]) then
                raise (
                    ArgumentException(
                        sprintf
                            "%s needs xs strictly increasing; xs[%d] = %g is not below xs[%d] = %g."
                            name
                            i
                            xs[i]
                            (i + 1)
                            xs[i + 1]
                    )
                )

            if Double.IsInfinity(xs[i + 1] - xs[i]) then
                raise (
                    ArgumentException(
                        sprintf
                            "%s needs finite interval widths; the interval from xs[%d] = %g to xs[%d] = %g is wider than a float."
                            name
                            i
                            xs[i]
                            (i + 1)
                            xs[i + 1]
                    )
                )

        let xs = Array.copy xs
        let ys = Array.copy ys
        let nonFinite = ys |> Array.exists (fun y -> Double.IsNaN y || Double.IsInfinity y)

        { Kind = kind
          Xs = xs
          Ys = ys
          Coefficients = (if nonFinite then [||] else coefficients xs ys)
          NonFinite = nonFinite }

    /// The piecewise-linear interpolant. Continuous, with a derivative that jumps at each interior knot.
    let linear (xs: float[]) (ys: float[]) : Interpolant = build "linear" Linear (fun _ _ -> [||]) xs ys

    let private naturalSlopes (xs: float[]) (ys: float[]) : float[] =
        let n = xs.Length
        let h = Array.init (n - 1) (fun i -> xs[i + 1] - xs[i])
        let delta = Array.init (n - 1) (fun i -> (ys[i + 1] - ys[i]) / h[i])
        // Tridiagonal rows in the scaling of de Boor, so every term scales as y or y / h: the natural end rows
        // 2 s[0] + s[1] = 3 delta[0] and s[n - 2] + 2 s[n - 1] = 3 delta[n - 2], and for 0 < i < n - 1
        // h[i] s[i - 1] + 2 (h[i - 1] + h[i]) s[i] + h[i - 1] s[i + 1] = 3 (h[i] delta[i - 1] + h[i - 1] delta[i]).
        let lower = Array.init n (fun i -> if i = 0 then 0.0 elif i = n - 1 then 1.0 else h[i])
        let diagonal = Array.init n (fun i -> if i = 0 || i = n - 1 then 2.0 else 2.0 * (h[i - 1] + h[i]))
        let upper = Array.init n (fun i -> if i = 0 then 1.0 elif i = n - 1 then 0.0 else h[i - 1])

        let rhs =
            Array.init n (fun i ->
                if i = 0 then 3.0 * delta[0]
                elif i = n - 1 then 3.0 * delta[n - 2]
                else 3.0 * (h[i] * delta[i - 1] + h[i - 1] * delta[i]))

        for i in 1 .. n - 1 do
            let w = lower[i] / diagonal[i - 1]
            diagonal[i] <- diagonal[i] - w * upper[i - 1]
            rhs[i] <- rhs[i] - w * rhs[i - 1]

        let s: float[] = Array.zeroCreate n
        s[n - 1] <- rhs[n - 1] / diagonal[n - 1]

        for i in n - 2 .. -1 .. 0 do
            s[i] <- (rhs[i] - upper[i] * s[i + 1]) / diagonal[i]

        s

    /// The natural cubic spline: twice continuously differentiable, with zero second derivative at both end knots,
    /// found by the Thomas algorithm on the tridiagonal system of the knot slopes, scaled so that no term overflows
    /// for finite knots however small or large the spacing. It can overshoot monotone data.
    let naturalCubic (xs: float[]) (ys: float[]) : Interpolant =
        build "naturalCubic" NaturalCubic naturalSlopes xs ys

    let private monotoneSlopes (xs: float[]) (ys: float[]) : float[] =
        let n = xs.Length
        let delta = Array.init (n - 1) (fun i -> (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]))
        let d: float[] = Array.zeroCreate n
        d[0] <- delta[0]
        d[n - 1] <- delta[n - 2]

        for k in 1 .. n - 2 do
            let before = delta[k - 1]
            let after = delta[k]

            if (before > 0.0 && after > 0.0) || (before < 0.0 && after < 0.0) then
                d[k] <- (before + after) / 2.0

        for k in 0 .. n - 2 do
            if delta[k] = 0.0 then
                d[k] <- 0.0
                d[k + 1] <- 0.0
            else
                let alpha = d[k] / delta[k]
                let beta = d[k + 1] / delta[k]
                let s = alpha * alpha + beta * beta

                if s > 9.0 then
                    let tau = 3.0 / sqrt s
                    d[k] <- tau * alpha * delta[k]
                    d[k + 1] <- tau * beta * delta[k]

        d

    /// The monotone cubic Hermite interpolant of Fritsch and Carlson: continuously differentiable, and monotone on
    /// every interval where the data are. Each interior slope is the mean of the neighbouring secants, 0 where they
    /// differ in sign or either is 0; each end slope is its secant; a pair of slopes with α² + β² above 9 is scaled by
    /// 3/√(α² + β²) (Fritsch and Carlson, SIAM J. Numer. Anal. 17(2), 1980; from documentation). It never overshoots
    /// the data on an interval.
    let monotoneCubic (xs: float[]) (ys: float[]) : Interpolant =
        build "monotoneCubic" MonotoneCubic monotoneSlopes xs ys
