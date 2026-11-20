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

    /// <summary>
    /// A memo of <c>result</c> over an accumulator that adds and removes the <c>projection</c> of each row of
    /// <c>upstream</c> through <c>add</c> and <c>remove</c> from <c>zero</c>, kept current per row change.
    /// </summary>
    /// <remarks>
    /// A row edit is one <c>remove</c> and one <c>add</c> and one call of <c>projection</c>. A removal raises, and so
    /// requests a re-fold that adds every cached projected value to <c>zero</c> in <c>Keys</c> order at O(N), when
    /// <c>finite</c> is false for the accumulator, when <c>remove</c> raises, or, under <c>RefoldAfterRemovals</c>, when
    /// the removals including this one reach the larger of the row count before it and the policy's <c>RefoldFloor</c>.
    /// <c>finite</c> must be false for a state that <c>remove</c> cannot correct, such as one poisoned by a NaN.
    /// <c>Refolds</c> counts the re-folds. The memo publishes <c>result</c> on every recomputation, and cuts off only
    /// when the graph's equality finds the result equal. The memo is one fold node, and a projection from
    /// <c>createProjectionOnce</c> raises <c>NotSupportedException</c>.
    /// </remarks>
    let maintainedBy
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
        maintainedBy policy (Sum.value >> isFinite) (Sum.add c) (Sum.remove c) Sum.zero Sum.value projection upstream

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
        maintainedBy policy momentsFinite (Moments.merge c) (Moments.unmerge c) Moments.zero id projection upstream

    /// The same value folded from every row on each change.
    let momentsFromScratchBy
        (policy: FloatPolicy)
        (projection: 'V -> Moments)
        (upstream: Projection<'K, 'V>)
        : Memo<Moments> =
        let c = policy.Compensated

        upstream
        |> Projection.fold (fun s v -> Moments.merge c s (projection v)) Moments.zero

    /// <summary>The <c>Comoments.merge</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit is one <c>Comoments.unmerge</c> and one <c>Comoments.merge</c>. A NaN in a row poisons the state for as
    /// long as any row remains, and every edit then re-folds from the current rows, so the reads are clean once the row
    /// is removed or edited. Re-folds otherwise follow <c>maintainedBy</c>. The memo publishes on every recomputation: a
    /// <c>Comoments</c> compares by reference under the default equality. One fold node. A projection from
    /// <c>createProjectionOnce</c> raises <c>NotSupportedException</c>.
    /// </remarks>
    let comomentsBy
        (policy: FloatPolicy)
        (projection: 'V -> Comoments)
        (upstream: Projection<'K, 'V>)
        : Maintained<Comoments> =
        let c = policy.Compensated
        maintainedBy policy Comoments.isFinite (Comoments.merge c) (Comoments.unmerge c) Comoments.zero id projection upstream

    /// <summary>The <c>HigherMoments.merge</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit is one <c>HigherMoments.unmerge</c> and one <c>HigherMoments.merge</c>. A NaN in a row poisons the
    /// state for as long as any row remains, and every edit then re-folds from the current rows. Re-folds otherwise
    /// follow <c>maintainedBy</c>. The memo publishes on every recomputation: a <c>HigherMoments</c> compares by
    /// reference under the default equality. One fold node. A projection from <c>createProjectionOnce</c> raises
    /// <c>NotSupportedException</c>.
    /// </remarks>
    let higherMomentsBy
        (policy: FloatPolicy)
        (projection: 'V -> HigherMoments)
        (upstream: Projection<'K, 'V>)
        : Maintained<HigherMoments> =
        let c = policy.Compensated

        maintainedBy
            policy
            HigherMoments.isFinite
            (HigherMoments.merge c)
            (HigherMoments.unmerge c)
            HigherMoments.zero
            id
            projection
            upstream

    /// <summary>The <c>WeightedMoments.merge</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit is one <c>WeightedMoments.unmerge</c> and one <c>WeightedMoments.merge</c>. A NaN weight in a row
    /// poisons the state for as long as any row remains, and every edit then re-folds from the current rows. Re-folds
    /// otherwise follow <c>maintainedBy</c>. The memo publishes on every recomputation: a <c>WeightedMoments</c>
    /// compares by reference under the default equality. One fold node. A projection from <c>createProjectionOnce</c>
    /// raises <c>NotSupportedException</c>.
    /// </remarks>
    let weightedMomentsBy
        (policy: FloatPolicy)
        (projection: 'V -> WeightedMoments)
        (upstream: Projection<'K, 'V>)
        : Maintained<WeightedMoments> =
        let c = policy.Compensated

        maintainedBy
            policy
            WeightedMoments.isFinite
            (WeightedMoments.merge c)
            (WeightedMoments.unmerge c)
            WeightedMoments.zero
            id
            projection
            upstream

    /// <summary>The <c>Histogram</c> over <c>edges</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// A row edit is one <c>Histogram.remove</c> and one <c>Histogram.add</c>, each O(B) over B bins
    /// (from documentation: <c>Histogram.add</c>). Counts are integers, so removal is exact and the fold never re-folds. A NaN counts as
    /// missing. Raises what <c>Histogram.create</c> raises for invalid edges. The memo publishes on every
    /// recomputation: a <c>Histogram</c> compares by reference under the default equality. One fold node. A projection
    /// from <c>createProjectionOnce</c> raises <c>NotSupportedException</c>.
    /// </remarks>
    let histogramBy (edges: float[]) (projection: 'V -> float) (upstream: Projection<'K, 'V>) : Memo<Histogram> =
        upstream
        |> Projection.foldGroupBy projection Histogram.add Histogram.remove (Histogram.create edges) id

    /// <summary>The type-7 quantile at the probability <c>p</c> of <c>projection</c> over the rows of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// Equal to <c>OrderStatistic.quantileSorted</c> over the sorted projected values. NaN when there are no rows or
    /// when a projected value is NaN. Raises <c>ArgumentOutOfRangeException</c> unless 0 ≤ p ≤ 1. The read costs O(1):
    /// the last sorted row, then the two rows around the rank, with <c>projection</c> called on each. A row change
    /// costs O(1) neighbour checks and a binary search and shift per touched position, and O(N log N) on dense bursts
    /// (from documentation: <c>Projection.sortBy</c> in core 0.2.1, <c>Combinators.fs</c>). The memo is a sort view
    /// and one memo, and cuts off an unchanged <c>float</c> on both targets. A projection from
    /// <c>createProjectionOnce</c> raises <c>NotSupportedException</c>.
    /// </remarks>
    let quantileBy (p: float) (projection: 'V -> float) (upstream: Projection<'K, 'V>) : Memo<float> =
        if not (p >= 0.0 && p <= 1.0) then
            raise (ArgumentOutOfRangeException("p", "A quantile probability must lie in [0, 1]."))

        let sorted = upstream |> Projection.sortBy projection

        createMemo (fun _ ->
            let keys = sorted.Keys
            let n = keys.Length
            let at (i: int) = projection (sorted.Get keys[i])

            if n = 0 then
                nan
            else
                let last = at (n - 1)

                if Double.IsNaN last then
                    nan
                else
                    let h = float (n - 1) * p
                    let l = int (Math.Floor h)

                    if l >= n - 1 then
                        OrderStatistic.quantileSorted 0.0 [| last |]
                    else
                        OrderStatistic.quantileSorted (h - float l) [| at l; at (l + 1) |])
