namespace Ranvier.Numerics

open System

/// How a floating-point aggregate accumulates, and when it re-folds from its cached inputs.
type FloatPolicy =
    {
        /// Carry rounding error beside every running total.
        Compensated: bool
        /// Re-fold once the removals since the last re-fold reach the number of rows and `RefoldFloor`.
        RefoldAfterRemovals: bool
        /// With `RefoldAfterRemovals`, the removals since the last re-fold must reach this many as well as the row count.
        RefoldFloor: int
    }

module FloatPolicy =
    /// Compensated, and re-folded once the removals since the last re-fold reach the number of rows. `RefoldFloor` is 0.
    let standard: FloatPolicy =
        {
            Compensated = true
            RefoldAfterRemovals = true
            RefoldFloor = 0
        }

    /// Plain addition, re-folded only when the state is not finite.
    let fast: FloatPolicy =
        {
            Compensated = false
            RefoldAfterRemovals = false
            RefoldFloor = 0
        }

    /// `policy` with the given floor. Raises ArgumentOutOfRangeException for a negative floor.
    let withRefoldFloor (floor: int) (policy: FloatPolicy) : FloatPolicy =
        if floor < 0 then
            raise (ArgumentOutOfRangeException(nameof floor, "floor must be zero or more."))

        { policy with RefoldFloor = floor }
