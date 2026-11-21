namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// Argument checks shared by the statistics modules. Each raises ArgumentOutOfRangeException naming the parameter.
module internal Check =
    // 2^-480: from this mean on, two distinct doubles near the mean differ by at least 2^-533, whose square is a
    // non-zero subnormal.
    let private resolvedMean = 3.2033329522929615e-145

    // 2^-1022, the least normal double.
    let private leastNormal = 2.2250738585072014e-308

    /// <summary>
    /// The variance that the given function reads from the moments, where the moments resolve it, and NaN where they
    /// do not: NaN for a NaN or infinite variance (the sum of squares overflowed), for a non-zero variance below
    /// 2^-1022 (a subnormal sum of squares, which has lost bits), and for a variance of exactly 0 at a non-zero mean
    /// below 2^-480 in magnitude, where squares that underflowed to 0 cannot be told from a constant sample. From a
    /// mean of 2^-480 two distinct doubles differ by at least 2^-533, so a sample that is not constant leaves a non-zero
    /// sum of squares, and an exact 0 there is a constant sample. A variance of exactly 0 at a mean of exactly 0 is a
    /// constant sample of zeros, and returns 0. Below a count of 2 the variance is returned as read: one observation
    /// has no spread to lose. So a spread below about 1.5e-154 or above about 1.3e154 gives NaN, except that a sample
    /// symmetric about exactly 0 with a spread below about 1.5e-154 reads as constant.
    /// </summary>
    let resolvedVariance (variance: Moments -> float) (m: Moments) : float =
        let v = variance m

        if Moments.count m < 2.0 then v
        elif Double.IsNaN v || Double.IsInfinity v then nan
        elif v = 0.0 then
            let mean = Moments.mean m
            if mean <> 0.0 && abs mean < resolvedMean then nan else 0.0
        elif v < leastNormal then nan
        else v

    /// Raises unless 0 < alpha < 1; NaN raises.
    let alpha (alpha: float) : unit =
        if not (alpha > 0.0 && alpha < 1.0) then
            raise (ArgumentOutOfRangeException(nameof alpha, "alpha must lie strictly between 0 and 1."))

    /// Raises unless x is finite; NaN and the infinities raise.
    let finite (name: string) (x: float) : unit =
        if Double.IsNaN x || Double.IsInfinity x then
            raise (ArgumentOutOfRangeException(name, name + " must be finite."))

    /// Raises unless 0 < x < ∞; NaN raises.
    let finitePositive (name: string) (x: float) : unit =
        if not (x > 0.0 && x < infinity) then
            raise (ArgumentOutOfRangeException(name, name + " must be finite and greater than 0."))

    /// Raises unless x ≥ 0; NaN raises.
    let notNegative (name: string) (x: float) : unit =
        if not (x >= 0.0) then
            raise (ArgumentOutOfRangeException(name, name + " must be zero or more."))

    /// Raises unless 0 ≤ p ≤ 1; NaN raises.
    let probability (name: string) (p: float) : unit =
        if not (p >= 0.0 && p <= 1.0) then
            raise (ArgumentOutOfRangeException(name, name + " must lie between 0 and 1."))
