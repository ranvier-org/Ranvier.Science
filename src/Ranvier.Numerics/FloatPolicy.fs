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
    /// Compensated, and re-folded once the removals since the last re-fold reach the larger of the row count and 64.
    /// Between re-folds a maintained sum of 8 rows stayed within 0 ulp of the from-scratch compensated fold over
    /// 1,000,000 edits on .NET and 0 ulp over 100,000 under JavaScript (*measured here*, 2026-11-20: the test
    /// `between re-folds under the standard floor a small sum stays within 2 ulp of the from-scratch fold` in
    /// `tests/Ranvier.Science.Tests/AggregateTests.fs`, run by
    /// `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "Aggregate"` and by
    /// `npx mocha dist/tests/Main.fs.js --grep "standard floor"`).
    let standard: FloatPolicy =
        {
            Compensated = true
            RefoldAfterRemovals = true
            RefoldFloor = 64
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
