namespace Ranvier.Quantities

open Ranvier

/// The state returned by one call of `start`.
[<Sealed>]
type internal Started<'s>(state: 's) =
    member _.State: 's = state

/// A published step of a run. Each start, each advance, and the first clock recorded after a start at a NaN clock
/// publish a new instance.
[<Sealed>]
type internal Stepped<'s>(origin: Started<'s>, state: 's, last: float) =
    member _.Origin: Started<'s> = origin
    member _.State: 's = state
    member _.Last: float = last

/// A simulation advanced with a clock. Its whole state is one node.
[<Sealed>]
type Run<'s> internal (stepped: Memo<Stepped<'s>>) =
    /// <summary>A tracked read that first brings the state up to the clock's current value.</summary>
    /// <remarks>
    /// The read raises the exception of a failed <c>start</c> or <c>advance</c>, and raises it again on later reads
    /// until the clock or a value read by <c>start</c> changes. A change to the clock after a failed advance retries
    /// the advance on the state the failed call left, over the gap from the last value recorded before the failure.
    /// </remarks>
    member _.State: 's = stepped.Value.State

module Run =
    /// <summary>
    /// A run whose state comes from <c>start</c> and is advanced by <c>advance</c> as <c>clock</c> moves forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>start</c> runs when the run is first read, when a quantity <c>start</c> read changes, and when the clock
    /// is below its last value by more than 1e-9. A start records the clock and does not advance.
    /// A quantity <c>start</c> read has changed when it received a write of a different value since the last read of
    /// <c>State</c>, including a write followed by a write back to the earlier value, and two such writes inside one
    /// <c>batch</c>. A write of an equal value is ignored by the signal and leaves the run.
    /// When the clock is above its last value, <c>advance state (min maxStep (clock − last))</c> runs once and the
    /// clock becomes the last value; the part of the gap beyond <c>maxStep</c> is dropped.
    /// A clock equal to the last value, within 1e-9 below it, or NaN leaves the run unchanged.
    /// A start at a NaN clock leaves the run with no last value; the first later read at a clock other than NaN
    /// records that clock as the last value and does not advance.
    /// Reads inside <c>advance</c> are not tracked.
    /// </para>
    /// <para>
    /// The run is pulled: the clock is read only when something reads <c>State</c>, so clock writes between reads
    /// cause one advance at the next read. A reader of <c>State</c> is notified after every start and every advance.
    /// </para>
    /// </remarks>
    let createWith
        (maxStep: float)
        (clock: unit -> float)
        (start: unit -> 's)
        (advance: 's -> float -> unit)
        : Run<'s> =
        let started = createMemo (fun _ -> Started (start ()))

        let stepped =
            createMemo (fun (previous: Stepped<'s> voption) ->
                let origin = started.Value
                let now = clock ()

                match previous with
                | ValueSome last when obj.ReferenceEquals (last.Origin, origin) ->
                    if System.Double.IsNaN last.Last then
                        if System.Double.IsNaN now then last else Stepped (origin, last.State, now)
                    elif now < last.Last - 1e-9 then
                        Stepped (origin, untrack start, now)
                    elif now > last.Last then
                        untrack (fun () -> advance last.State (min maxStep (now - last.Last)))
                        Stepped (origin, last.State, now)
                    else
                        last
                | _ -> Stepped (origin, origin.State, now))

        Run stepped

    /// `createWith infinity`.
    let create (clock: unit -> float) (start: unit -> 's) (advance: 's -> float -> unit) : Run<'s> =
        createWith infinity clock start advance
