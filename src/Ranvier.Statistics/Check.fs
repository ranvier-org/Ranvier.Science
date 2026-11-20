namespace Ranvier.Statistics

open System

/// Argument checks shared by the statistics modules. Each raises ArgumentOutOfRangeException naming the parameter.
module internal Check =
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
