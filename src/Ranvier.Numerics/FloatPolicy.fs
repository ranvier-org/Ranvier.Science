namespace Ranvier.Numerics

/// How a floating-point aggregate accumulates, and when it re-folds from its cached inputs.
type FloatPolicy =
    {
        /// Carry rounding error beside every running total.
        Compensated: bool
        /// Re-fold once removals since the last re-fold reach the number of rows.
        RefoldAfterRemovals: bool
    }

module FloatPolicy =
    /// Compensated, and re-folded once removals since the last re-fold reach the number of rows.
    let standard: FloatPolicy =
        {
            Compensated = true
            RefoldAfterRemovals = true
        }

    /// Plain addition, re-folded only when the state is not finite.
    let fast: FloatPolicy =
        {
            Compensated = false
            RefoldAfterRemovals = false
        }
