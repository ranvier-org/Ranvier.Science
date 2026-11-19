namespace Ranvier.Numerics

open System
open Ranvier

/// An aggregate kept current per row change.
type Maintained<'T> =
    {
        Value: Memo<'T>
        /// The re-folds since creation: each time a row's removal raised, by the policy's request on a non-finite state
        /// or on removals reaching the larger of the row count and the policy's `RefoldFloor`, or by an exception out
        /// of the accumulator's removal. Read it after `Value`.
        Refolds: unit -> int
    }

/// The accumulator of a maintained aggregate, its row count and the removals since the last re-fold.
[<Struct; NoComparison; NoEquality>]
type internal AggregateState<'A> =
    {
        Accumulator: 'A
        Rows: int
        Removals: int
    }

module Aggregate =
    let private isFinite (x: float) = abs x < infinity

    let private momentsFinite (m: Moments) =
        Moments.count m = 0.0
        || (isFinite (Moments.mean m) && isFinite (Moments.variance m))

    /// A `foldGroupBy` whose `subtract` raises, and so requests a re-fold from the cached row values, when the
    /// accumulator is not finite or, under `RefoldAfterRemovals`, when the removals including this one reach the larger
    /// of the row count before it and the policy's `RefoldFloor`.
    let private maintain
        (policy: FloatPolicy)
        (finite: 'A -> bool)
        (add: 'A -> 'U -> 'A)
        (remove: 'A -> 'U -> 'A)
        (zero: 'A)
        (result: 'A -> 'T)
        (projection: 'V -> 'U)
        (upstream: Projection<'K, 'V>)
        : Maintained<'T> =
        let refolds = ref 0

        let addRow (s: AggregateState<'A>) (u: 'U) =
            {
                Accumulator = add s.Accumulator u
                Rows = s.Rows + 1
                Removals = s.Removals
            }

        let subtractRow (s: AggregateState<'A>) (u: 'U) =
            let removals = s.Removals + 1

            if
                not (finite s.Accumulator)
                || (policy.RefoldAfterRemovals && removals >= max s.Rows policy.RefoldFloor)
            then
                refolds.Value <- refolds.Value + 1
                raise (ArithmeticException "Re-fold the aggregate from its cached rows.")

            let accumulator =
                try
                    remove s.Accumulator u
                with e ->
                    refolds.Value <- refolds.Value + 1
                    raise e

            {
                Accumulator = accumulator
                Rows = s.Rows - 1
                Removals = removals
            }

        let start =
            {
                Accumulator = zero
                Rows = 0
                Removals = 0
            }

        {
            Value =
                upstream
                |> Projection.foldGroupBy projection addRow subtractRow start (fun s -> result s.Accumulator)
            Refolds = fun () -> refolds.Value
        }

    /// <summary>The sum of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit costs one call of <c>projection</c>, one removal and one addition. A re-fold adds every cached
    /// projected value in <c>Keys</c> order and costs O(N). While a non-finite row is present every edit re-folds.
    /// <c>Refolds</c> counts the re-folds; the removal of the last row resets the sum to zero without one. A row
    /// counts as edited when its projected value differs under the graph's equality, which compares a <c>float</c>
    /// by value on both targets.
    /// </remarks>
    let sumBy (policy: FloatPolicy) (projection: 'V -> float) (upstream: Projection<'K, 'V>) : Maintained<float> =
        let c = policy.Compensated
        maintain policy (Sum.value >> isFinite) (Sum.add c) (Sum.remove c) Sum.zero Sum.value projection upstream

    /// <summary>The <c>Moments.merge</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit is one <c>Moments.unmerge</c> and one <c>Moments.merge</c>; a re-fold merges every cached projected
    /// value in <c>Keys</c> order. The state is not finite when its mean or variance is infinite or NaN; while a
    /// non-finite row is present every edit re-folds. Re-folds otherwise follow <c>sumBy</c>.
    /// A row counts as edited when its projected value differs under the graph's equality, which compares a
    /// <c>Moments</c> by value on .NET and by reference under JavaScript. A projection that leaves a row unedited
    /// returns the <c>Moments</c> instance it returned before.
    /// </remarks>
    let momentsBy
        (policy: FloatPolicy)
        (projection: 'V -> Moments)
        (upstream: Projection<'K, 'V>)
        : Maintained<Moments> =
        let c = policy.Compensated
        maintain policy momentsFinite (Moments.merge c) (Moments.unmerge c) Moments.zero id projection upstream

    /// The same value folded from every row on each change.
    let momentsFromScratchBy
        (policy: FloatPolicy)
        (projection: 'V -> Moments)
        (upstream: Projection<'K, 'V>)
        : Memo<Moments> =
        let c = policy.Compensated

        upstream
        |> Projection.fold (fun s v -> Moments.merge c s (projection v)) Moments.zero
