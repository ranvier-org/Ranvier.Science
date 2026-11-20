namespace Ranvier.Numerics

open System

/// <summary>Exponentially decayed count, mean and variance of timed observations. Immutable.</summary>
/// <remarks>
/// An observation's weight halves every half-life. It is a struct of floats with structural equality, which under the
/// core's default equality policy is not a cut-off: a reader of one publishes on every recomputation.
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
    /// The accumulator folded from the zero over the items, and the sequence number of the first item whose addition
    /// leaves the value non-finite (-1 when none). The first item has the sequence number firstSeq.
    let run (add: 'A -> 'T -> 'A) (finite: 'A -> bool) (zero: 'A) (firstSeq: float) (xs: 'T[]) : 'A * float =
        let mutable v = zero
        let mutable culprit = -1.0

        for i in 0 .. xs.Length - 1 do
            v <- add v xs[i]

            if culprit < 0.0 && not (finite v) then
                culprit <- firstSeq + float i

        v, culprit

    /// The finiteness test of a Moments value: the count is zero, or its mean and variance are finite.
    let momentsFinite (m: Moments) : bool =
        Moments.count m = 0.0
        || (abs (Moments.mean m) < infinity && abs (Moments.variance m) < infinity)

/// <summary>A sliding window of the last items pushed, folded through an accumulator. Immutable.</summary>
/// <remarks>
/// A push removes the oldest item from the accumulator when the window is full, then adds the new one. The value is
/// re-folded from the zero over the items once the removals since the last re-fold reach the item count, which bounds
/// the drift of a remove and costs O(1) amortised per push (general knowledge; the "Window" test list counts the calls
/// over 1000 pushes). The held value carries up to one capacity of removals since the last re-fold. For
/// <c>Window.moments</c> at capacity 50 over 1e9 + (i mod 7), measured here by the "Window" test list
/// ("re-folds bound drift", at 100001, 100010 and 100049 pushes): the mean within relative 1e-12 of a fresh fold of the
/// items, the compensated variance within relative 1e-8 and the uncompensated variance within relative 1e-6.
/// A window built by <c>Window.create</c> keeps a non-finite value until the next re-fold. <c>Window.moments</c>
/// re-folds when the item whose addition first made the value non-finite is removed, and again for the next such item
/// in the re-folded window, so a NaN or infinite observation stops affecting the value once it has left. The window
/// holds functions, so it has no equality: a memo of one cuts off only by returning the previous instance.
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
            /// The number of items pushed so far, as a float so it is exact to 2^53 on both targets.
            Seq: float
            /// The sequence number of the item whose addition first made the value non-finite; -1 when none.
            Culprit: float
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
            Seq = 0.0
            Culprit = -1.0
            Count = 0
            Removals = 0
        }

    /// An empty window. Raises ArgumentOutOfRangeException unless the capacity is at least 1. An exception raised by
    /// the accumulator functions propagates unchanged. A non-finite value persists until the next re-fold.
    let create (capacity: int) (add: 'A -> 'T -> 'A) (remove: 'A -> 'T -> 'A) (zero: 'A) : Window<'T, 'A> =
        createChecked capacity (fun _ -> true) add remove zero

    /// A window over the last items, which drops the oldest when full. Re-folds as described on the type.
    let push (w: Window<'T, 'A>) (x: 'T) : Window<'T, 'A> =
        let mutable forced = false

        let value, items, count, removals =
            if w.Count = w.Capacity then
                let oldest, rest = Queue.dequeue w.Items
                forced <- (w.Seq - float w.Count = w.Culprit)
                w.Remove w.Value oldest, rest, w.Count - 1, w.Removals + 1
            else
                w.Value, w.Items, w.Count, w.Removals

        let value = w.Add value x
        let items = Queue.enqueue x items
        let count = count + 1
        let seq = w.Seq + 1.0

        if forced || removals >= count then
            let folded, culprit = Refold.run w.Add w.Finite w.Zero (seq - float count) (Queue.toArray items)
            { w with Value = folded; Items = items; Seq = seq; Culprit = culprit; Count = count; Removals = 0 }
        else
            let culprit =
                if w.Culprit >= 0.0 then w.Culprit
                elif w.Finite value then -1.0
                else w.Seq

            { w with Value = value; Items = items; Seq = seq; Culprit = culprit; Count = count; Removals = removals }

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
/// <c>Window</c>: O(1) amortised per push (general knowledge), pinned by the "Window" test list. The window holds functions, so it has no equality: a memo of one cuts off only by returning the previous
/// instance.
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
            Seq: float
            Culprit: float
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
            Seq = 0.0
            Culprit = -1.0
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
        let mutable forced = false

        while go && count > 0 do
            items <- Queue.prime items

            match items.Front with
            | (ti, old) :: rest when ti < cutoff ->
                if w.Seq - float count = w.Culprit then
                    forced <- true

                value <- w.Remove value old
                items <- { items with Front = rest }
                count <- count - 1
                removals <- removals + 1
            | _ -> go <- false

        value <- w.Add value x
        items <- Queue.enqueue (t, x) items
        count <- count + 1

        let seq = w.Seq + 1.0

        if forced || removals >= count then
            let xs = Queue.toArray items |> Array.map snd
            let folded, culprit = Refold.run w.Add w.Finite w.Zero (seq - float count) xs
            { w with Value = folded; Items = items; Seq = seq; Culprit = culprit; Latest = t; Count = count; Removals = 0 }
        else
            let culprit =
                if w.Culprit >= 0.0 then w.Culprit
                elif w.Finite value then -1.0
                else w.Seq

            { w with Value = value; Items = items; Seq = seq; Culprit = culprit; Latest = t; Count = count; Removals = removals }

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
