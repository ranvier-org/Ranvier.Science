namespace Ranvier.Numerics

open System

/// <summary>Exponentially decayed count, mean and variance of timed observations. Immutable.</summary>
/// <remarks>
/// An observation's weight halves every half-life. It is a struct: on .NET the core's default policy compares it by
/// value, so an equal result cuts off; under Fable it compares by reference, so every recomputation publishes. A
/// caller must not rely on a cut-off.
/// </remarks>
[<Struct; NoComparison>]
type DecayedMoments =
    private
        {
            HalfLife: float
            /// NaN before the first observation.
            Last: float
            Weight: float
            Mean: float
            S: float
        }

module DecayedMoments =
    /// An empty accumulator. Raises ArgumentOutOfRangeException unless the half-life is positive; infinity means no decay.
    let create (halfLife: float) : DecayedMoments =
        if not (halfLife > 0.0) then
            raise (ArgumentOutOfRangeException("halfLife", "The half-life must be positive."))

        {
            HalfLife = halfLife
            Last = nan
            Weight = 0.0
            Mean = nan
            S = 0.0
        }

    /// <summary>
    /// Adds an observation of weight 1 at a time. The existing weight and spread are first multiplied by
    /// 2^(-(elapsed / half-life)) when the time is later than the latest seen; an earlier or equal time applies no
    /// decay and leaves the latest time unchanged. The decay uses <c>Math.Pow</c>, so it is not an exact kernel. A gap so
    /// large that the factor underflows leaves only the new observation, with weight 1 and mean x. The first
    /// observation is never decayed. A NaN time gives NaN reads, as does a NaN observation.
    /// </summary>
    let addAt (s: DecayedMoments) (t: float) (x: float) : DecayedMoments =
        if Double.IsNaN t then
            { s with Last = nan; Weight = nan; Mean = nan; S = nan }
        else
            let decayed = s.Weight > 0.0 && t > s.Last

            let factor =
                if not decayed || Double.IsInfinity s.HalfLife then
                    1.0
                else
                    Math.Pow(2.0, -((t - s.Last) / s.HalfLife))

            let last = if s.Weight = 0.0 || t > s.Last then t else s.Last
            let weight = s.Weight * factor

            if weight = 0.0 then
                { s with Last = last; Weight = 1.0; Mean = x; S = 0.0 }
            else
                let w = weight + 1.0
                let delta = x - s.Mean
                let mean = s.Mean + delta / w
                { s with Last = last; Weight = w; Mean = mean; S = s.S * factor + delta * (x - mean) }

    /// The observation at the time after the latest, the first at time 0: it decays the existing weight by one step.
    let addItem (s: DecayedMoments) (x: float) : DecayedMoments =
        addAt s (if s.Weight = 0.0 then 0.0 else s.Last + 1.0) x

    /// The sum of the decayed weights; 0 when empty.
    let weight (s: DecayedMoments) : float = s.Weight

    /// The weighted mean; NaN when empty.
    let mean (s: DecayedMoments) : float = s.Mean

    /// The weighted population variance, never negative; NaN when empty.
    let variance (s: DecayedMoments) : float =
        if s.Weight = 0.0 then nan
        else
            let v = s.S / s.Weight
            if v < 0.0 then 0.0 else v

/// A persistent first-in first-out queue of two lists: the front oldest first, the back newest first.
type internal Queue<'T> =
    {
        Front: 'T list
        Back: 'T list
    }

module internal Queue =
    let empty<'T> : Queue<'T> = { Front = []; Back = [] }

    let enqueue (x: 'T) (q: Queue<'T>) : Queue<'T> = { q with Back = x :: q.Back }

    /// The queue with the back moved to the front when the front is empty, so the oldest item heads the front.
    let prime (q: Queue<'T>) : Queue<'T> =
        match q.Front with
        | [] -> { Front = List.rev q.Back; Back = [] }
        | _ -> q

    /// The oldest item and the queue without it; the queue must not be empty. O(1) amortised.
    let dequeue (q: Queue<'T>) : 'T * Queue<'T> =
        let primed = prime q

        match primed.Front with
        | x :: rest -> x, { primed with Front = rest }
        | [] -> invalidOp "The queue is empty."

    /// A fresh array, oldest first.
    let toArray (q: Queue<'T>) : 'T[] =
        Array.append (List.toArray q.Front) (List.toArray (List.rev q.Back))

module internal Refold =
    /// The accumulator folded from the zero over the items.
    let run (add: 'A -> 'T -> 'A) (zero: 'A) (xs: 'T[]) : 'A = Array.fold add zero xs

    /// The finiteness test of a Moments value: the count is zero, or its mean and variance are finite.
    let momentsFinite (m: Moments) : bool =
        Moments.count m = 0.0
        || (Doubles.isFinite (Moments.mean m) && Doubles.isFinite (Moments.variance m))

/// <summary>A sliding window of the last items pushed, folded through an accumulator. Immutable.</summary>
/// <remarks>
/// A push removes the oldest item from the accumulator when the window is full, then adds the new one. The value is
/// re-folded from the zero over the items once the removals since the last re-fold reach the item count, which bounds
/// the drift of a remove. On a stream whose values stay finite, pushed in sequence without branching, this costs O(1)
/// amortised per push (general knowledge; the "Window" test list counts the calls over 5000 pushes). Each of several
/// pushes onto the same window pays that window's pending re-fold and queue reversal, up to O(count) each. The held
/// value carries up to one
/// capacity of removals since the last re-fold. For <c>Window.moments</c> at capacity 50 over 1e9 + (i mod 7), measured
/// here by the "Window" test list ("re-folds bound drift", at 100001, 100010 and 100049 pushes): the mean within
/// relative 1e-12 of a fresh fold of the items, the compensated variance within relative 1e-8 and the uncompensated
/// variance within relative 1e-6. After an item far larger than the rest leaves, the mean of <c>Window.moments</c>
/// carries an absolute error of order count·ε times the largest |item| since the last re-fold, and the variance of
/// order count·ε times that item's square (ε = 2^-52; general knowledge), until the next re-fold, at most one capacity
/// of pushes later: at capacity 3, after 1e20, 1, 2, 3 the compensated mean reads 1366.33 where the items' mean is 2
/// (measured here by the "Window" test list, "a dominant item's error lasts until the next re-fold, within the stated
/// bound"). A fresh fold of <c>items</c> is free of that error after such a departure.
/// A window built by <c>Window.create</c> keeps a non-finite value until the next re-fold. <c>Window.moments</c>
/// re-folds in place of each removal from a non-finite value, and of a removal that leaves a finite value non-finite,
/// so a NaN or infinite observation, or an overflow, stops affecting the value once its items have left; while the
/// value is non-finite every departure re-folds, O(count) each. The window holds functions, so it has no equality: a
/// memo of one cuts off only by returning the previous instance.
/// </remarks>
[<NoEquality; NoComparison>]
type Window<'T, 'A> =
    private
        {
            Capacity: int
            Add: 'A -> 'T -> 'A
            Remove: 'A -> 'T -> 'A
            Zero: 'A
            Value: 'A
            Finite: 'A -> bool
            Items: Queue<'T>
            Count: int
            Removals: int
        }

module Window =
    let internal createChecked
        (capacity: int)
        (finite: 'A -> bool)
        (add: 'A -> 'T -> 'A)
        (remove: 'A -> 'T -> 'A)
        (zero: 'A)
        : Window<'T, 'A> =
        if capacity < 1 then
            raise (ArgumentOutOfRangeException("capacity", "The capacity must be at least 1."))

        {
            Capacity = capacity
            Add = add
            Remove = remove
            Zero = zero
            Value = zero
            Finite = finite
            Items = Queue.empty
            Count = 0
            Removals = 0
        }

    /// An empty window. Raises ArgumentOutOfRangeException unless the capacity is at least 1. An exception raised by
    /// the accumulator functions propagates unchanged. A non-finite value persists until the next re-fold.
    let create (capacity: int) (add: 'A -> 'T -> 'A) (remove: 'A -> 'T -> 'A) (zero: 'A) : Window<'T, 'A> =
        createChecked capacity (fun _ -> true) add remove zero

    /// A window over the last items, which drops the oldest when full. Re-folds as described on the type.
    let push (w: Window<'T, 'A>) (x: 'T) : Window<'T, 'A> =
        let full = w.Count = w.Capacity
        let count = if full then w.Count else w.Count + 1
        let removals = if full then w.Removals + 1 else w.Removals
        let mutable refold = removals >= count
        let mutable value = w.Value
        let mutable items = w.Items

        if full then
            let oldest, rest = Queue.dequeue items
            items <- rest

            if not refold then
                if w.Finite value then
                    value <- w.Remove value oldest
                    refold <- not (w.Finite value)
                else
                    refold <- true

        items <- Queue.enqueue x items

        if refold then
            { w with Value = Refold.run w.Add w.Zero (Queue.toArray items); Items = items; Count = count; Removals = 0 }
        else
            { w with Value = w.Add value x; Items = items; Count = count; Removals = removals }

    /// The accumulator over the items in the window.
    let value (w: Window<'T, 'A>) : 'A = w.Value

    /// The number of items in the window, at most the capacity.
    let count (w: Window<'T, 'A>) : int = w.Count

    /// The items, oldest first, in a fresh array. O(count).
    let items (w: Window<'T, 'A>) : 'T[] = Queue.toArray w.Items

    /// A window of the last capacity observations over <c>Moments</c>.
    let moments (compensated: bool) (capacity: int) : Window<float, Moments> =
        createChecked capacity Refold.momentsFinite (Moments.add compensated) (Moments.remove compensated) Moments.zero

/// <summary>A sliding window of the items pushed within a time span, folded through an accumulator. Immutable.</summary>
/// <remarks>
/// A push at time t drops the items older than t - span (it keeps the items with time at least t - span), removing each
/// from the accumulator, then adds the new one. Re-folds, drift and the treatment of a non-finite value are those of
/// <c>Window</c>: O(1) amortised per push on a stream whose values stay finite, pushed in sequence without branching
/// (general knowledge; the "Window" test list counts the calls over 100049 pushes), and O(count) for each departure
/// while the value of <c>TimeWindow.moments</c> is non-finite. After an item far larger than the rest leaves, the mean
/// of <c>TimeWindow.moments</c> carries an absolute error of order count·ε times the largest |item| since the last
/// re-fold, and the variance of order count·ε times that item's square (ε = 2^-52; general knowledge), until the
/// removals since the last re-fold reach the item count (measured here by the "Window" test list, "a dominant item's
/// error lasts until the next re-fold, within the stated bound"). A fresh fold of <c>items</c> is free of that error
/// after such a departure. The window holds functions, so it has no equality: a memo of one cuts off only by returning
/// the previous instance.
/// </remarks>
[<NoEquality; NoComparison>]
type TimeWindow<'T, 'A> =
    private
        {
            Span: float
            Add: 'A -> 'T -> 'A
            Remove: 'A -> 'T -> 'A
            Zero: 'A
            Value: 'A
            Finite: 'A -> bool
            Items: Queue<float * 'T>
            Latest: float
            Count: int
            Removals: int
        }

module TimeWindow =
    let internal createChecked
        (span: float)
        (finite: 'A -> bool)
        (add: 'A -> 'T -> 'A)
        (remove: 'A -> 'T -> 'A)
        (zero: 'A)
        : TimeWindow<'T, 'A> =
        if not (span >= 0.0) then
            raise (ArgumentOutOfRangeException("span", "The span must be at least 0."))

        {
            Span = span
            Add = add
            Remove = remove
            Zero = zero
            Value = zero
            Finite = finite
            Items = Queue.empty
            Latest = -infinity
            Count = 0
            Removals = 0
        }

    /// An empty window. Raises ArgumentOutOfRangeException unless the span is at least 0 (infinity keeps every item).
    /// A non-finite value persists until the next re-fold.
    let create (span: float) (add: 'A -> 'T -> 'A) (remove: 'A -> 'T -> 'A) (zero: 'A) : TimeWindow<'T, 'A> =
        createChecked span (fun _ -> true) add remove zero

    /// Raises ArgumentOutOfRangeException for a NaN time or a time below the latest pushed; an equal time is accepted.
    let push (w: TimeWindow<'T, 'A>) (t: float) (x: 'T) : TimeWindow<'T, 'A> =
        if not (t >= w.Latest) then
            raise (ArgumentOutOfRangeException("t", "The time must not be NaN or below the latest time pushed."))

        let cutoff = t - w.Span
        let mutable value = w.Value
        let mutable items = w.Items
        let mutable count = w.Count
        let mutable removals = w.Removals
        let mutable go = true
        let mutable refold = false

        while go && count > 0 do
            items <- Queue.prime items

            match items.Front with
            | (ti, old) :: rest when ti < cutoff ->
                if not refold then
                    if w.Finite value then
                        value <- w.Remove value old
                        refold <- not (w.Finite value)
                    else
                        refold <- true

                items <- { items with Front = rest }
                count <- count - 1
                removals <- removals + 1
            | _ -> go <- false

        items <- Queue.enqueue (t, x) items
        count <- count + 1

        if refold || removals >= count then
            let xs = Queue.toArray items |> Array.map snd
            { w with Value = Refold.run w.Add w.Zero xs; Items = items; Latest = t; Count = count; Removals = 0 }
        else
            { w with Value = w.Add value x; Items = items; Latest = t; Count = count; Removals = removals }

    /// The accumulator over the items in the window.
    let value (w: TimeWindow<'T, 'A>) : 'A = w.Value

    /// The number of items in the window.
    let count (w: TimeWindow<'T, 'A>) : int = w.Count

    /// The items, oldest first, in a fresh array. O(count).
    let items (w: TimeWindow<'T, 'A>) : 'T[] =
        Queue.toArray w.Items |> Array.map snd

    /// A window of the observations within the span over <c>Moments</c>.
    let moments (compensated: bool) (span: float) : TimeWindow<float, Moments> =
        createChecked span Refold.momentsFinite (Moments.add compensated) (Moments.remove compensated) Moments.zero
