namespace Ranvier.Numerics

open System

/// How an `Interpolant` joins its knots.
type InterpolationKind =
    | Linear
    | NaturalCubic
    | MonotoneCubic

/// A piecewise interpolant through knots (x, y), made by `Interpolate.linear`, `Interpolate.naturalCubic` or
/// `Interpolate.monotoneCubic`. Immutable; it holds copies of the arrays it was built from. `=` compares the kind, the
/// knots and the per-knot coefficients with float equality, so two interpolants built from equal arrays are equal, and
/// one built from a NaN or infinite y equals nothing. Built from a y that is NaN or infinite, it evaluates and
/// differentiates to NaN everywhere.
type Interpolant =
    internal
        { Kind: InterpolationKind
          Xs: float[]
          Ys: float[]
          /// Natural cubic: the second derivative at each knot. Monotone cubic: the slope at each knot. Empty for linear.
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
                | NaturalCubic ->
                    let m = interpolant.Coefficients
                    let a = (xs[i + 1] - x) / h
                    let b = (x - xs[i]) / h
                    a * ys[i] + b * ys[i + 1] + ((a * a * a - a) * m[i] + (b * b * b - b) * m[i + 1]) * (h * h) / 6.0
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
                | NaturalCubic ->
                    let m = interpolant.Coefficients
                    let a = (xs[i + 1] - x) / h
                    let b = (x - xs[i]) / h
                    (ys[i + 1] - ys[i]) / h - (3.0 * a * a - 1.0) * h * m[i] / 6.0 + (3.0 * b * b - 1.0) * h * m[i + 1] / 6.0
                | MonotoneCubic ->
                    let d = interpolant.Coefficients
                    let t = (x - xs[i]) / h
                    let t2 = t * t
                    let dh00 = 6.0 * t2 - 6.0 * t
                    let dh10 = 3.0 * t2 - 4.0 * t + 1.0
                    let dh01 = -6.0 * t2 + 6.0 * t
                    let dh11 = 3.0 * t2 - 2.0 * t
                    (dh00 * ys[i] + dh10 * h * d[i] + dh01 * ys[i + 1] + dh11 * h * d[i + 1]) / h

/// Builders of `Interpolant`s from knots. Each raises `ArgumentException` when xs is not strictly increasing (a NaN
/// breaks it), when it has fewer than two points, or when ys has another length than xs (the message names both
/// lengths). A NaN or infinite y is not an error: the interpolant evaluates to NaN everywhere. Each builder copies
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

    let private naturalSecondDerivatives (xs: float[]) (ys: float[]) : float[] =
        let n = xs.Length
        let m: float[] = Array.zeroCreate n

        if n > 2 then
            let k = n - 2
            // Row r (knot r + 1): h[r] M[r] + 2 (h[r] + h[r + 1]) M[r + 1] + h[r + 1] M[r + 2] = 6 (delta[r + 1] - delta[r]).
            let h = Array.init (n - 1) (fun i -> xs[i + 1] - xs[i])
            let delta = Array.init (n - 1) (fun i -> (ys[i + 1] - ys[i]) / h[i])
            let diagonal = Array.init k (fun r -> 2.0 * (h[r] + h[r + 1]))
            let rhs = Array.init k (fun r -> 6.0 * (delta[r + 1] - delta[r]))

            for r in 1 .. k - 1 do
                let w = h[r] / diagonal[r - 1]
                diagonal[r] <- diagonal[r] - w * h[r]
                rhs[r] <- rhs[r] - w * rhs[r - 1]

            m[k] <- rhs[k - 1] / diagonal[k - 1]

            for r in k - 2 .. -1 .. 0 do
                m[r + 1] <- (rhs[r] - h[r + 1] * m[r + 2]) / diagonal[r]

        m

    /// The natural cubic spline: twice continuously differentiable, with zero second derivative at both end knots,
    /// found by the Thomas algorithm on the tridiagonal system of the interior second derivatives. It can overshoot
    /// monotone data.
    let naturalCubic (xs: float[]) (ys: float[]) : Interpolant =
        build "naturalCubic" NaturalCubic naturalSecondDerivatives xs ys

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
