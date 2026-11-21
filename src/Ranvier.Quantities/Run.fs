namespace Ranvier.Quantities

open Ranvier
open Ranvier.Numerics

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
    /// The read raises the exception of a failed <c>start</c> or <c>advance</c>, and raises it again on later reads.
    /// After a failed <c>start</c>, a change in a value <c>start</c> read runs <c>start</c> again; a clock change
    /// re-raises. After a failed <c>advance</c>, a clock change retries the advance on the state the failed call left,
    /// over the gap from the last value recorded before the failure, and a change in a value <c>start</c> read
    /// restarts the run.
    /// </remarks>
    member _.State: 's = stepped.Value.State

/// The state of a run made by `Run.ode`: the time since the start and the state vector at that time. Mutable, so a
/// memo or run holding it publishes on every recomputation.
type OdeState =
    { mutable Time: float
      mutable State: float[] }

module Run =
    /// <summary>
    /// A run whose state comes from <c>start</c> and is advanced by <c>advance</c> as <c>clock</c> moves forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>start</c> runs when the run is first read, when a value <c>start</c> read changes, and when the clock
    /// is below its last value by more than 1e-9. A start records the clock and does not advance.
    /// A signal read directly by <c>start</c> has changed when it received a write of a different value since the last
    /// read of <c>State</c>, including a write followed by a write back to the earlier value, and two such writes inside
    /// one <c>batch</c>. A memo or a derived quantity read by <c>start</c> has changed when it was evaluated at a value
    /// other than the one <c>start</c> read, under the graph's equality, by this run's next read of <c>State</c> or by
    /// any other reader in between. Writes away and back to its inputs between reads of <c>State</c> leave the run
    /// unchanged only while nothing else reads the memo between the writes; two such writes inside one <c>batch</c>
    /// leave it unchanged unless the memo is read between them. A memo that only <c>start</c> reads leaves the run
    /// unchanged across writes away and back, whatever else reads its inputs. A write of an equal value is ignored by
    /// the signal and leaves the run unchanged.
    /// When the clock is above its last value, <c>advance state (min maxStep (clock − last))</c> runs once and the
    /// clock becomes the last value; the part of the gap beyond <c>maxStep</c> is dropped.
    /// A clock equal to the last value, within 1e-9 below it, or NaN leaves the run unchanged.
    /// A start at a NaN clock leaves the run with no last value; the first later read at a clock other than NaN
    /// records that clock as the last value and does not advance.
    /// Reads inside <c>advance</c> are not tracked.
    /// </para>
    /// <para>
    /// The run is pulled: the clock is read only when something reads <c>State</c>. Clock writes between two reads
    /// cause at most one advance, at the second read. A reader of <c>State</c> is notified after every start, every
    /// advance, and the first clock recorded after a start at a NaN clock.
    /// </para>
    /// <para>Raises <c>ArgumentOutOfRangeException</c> unless <c>maxStep</c> is greater than 0; infinity is accepted.</para>
    /// </remarks>
    let createWith
        (maxStep: float)
        (clock: unit -> float)
        (start: unit -> 's)
        (advance: 's -> float -> unit)
        : Run<'s> =
        if not (maxStep > 0.0) then
            raise (System.ArgumentOutOfRangeException(nameof maxStep, "maxStep must be greater than 0."))

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

    /// <summary>
    /// A run that integrates y' = rhs t y with <c>Ode.solve</c> as the clock advances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>create</c> with a start that returns <c>Time = 0</c> and a copy of the array <c>start</c> returned,
    /// and an advance that integrates from the held time over the clock's gap. <c>Time</c> counts from 0 at each start
    /// and not from the clock. As for <c>create</c>, an advance happens only when the clock moves above its last
    /// value; a clock equal to the last value or within 1e-9 below it leaves <c>Time</c> and <c>State</c> unchanged.
    /// Each start and each advance puts a fresh array in <c>State</c>, never the one <c>start</c> returned; reads
    /// between them return that same array, which a caller must not mutate.
    /// </para>
    /// <para>
    /// Reads inside <c>rhs</c> are not tracked, so a signal it reads does not restart the run. A value <c>start</c>
    /// reads restarts it, as for <c>create</c>.
    /// </para>
    /// <para>
    /// When <c>Ode.solve</c> returns an <c>Error</c>, the read raises <c>InvalidOperationException</c> with its message
    /// and the state stays as it was; the failure rules of <c>Run</c> apply, so a later clock change retries the
    /// integration over the gap from the last clock recorded before the failure. An exception raised by <c>rhs</c>
    /// propagates unchanged, with the same retry. Raises <c>ArgumentOutOfRangeException</c> at the call for the
    /// options <c>Ode.solve</c> rejects.
    /// </para>
    /// <para>
    /// The run is two memos, as for <c>create</c>, and its state is a mutable record: the run publishes on every
    /// recomputation and never cuts off. The cost of an advance is that of <c>Ode.solve</c> over the gap.
    /// </para>
    /// </remarks>
    let ode
        (options: OdeOptions)
        (clock: unit -> float)
        (start: unit -> float[])
        (rhs: float -> float[] -> float[])
        : Run<OdeState> =
        // An empty interval checks the options without calling rhs.
        Ode.solve options rhs 0.0 [||] 0.0 |> ignore

        let start' () =
            { Time = 0.0
              State = Array.copy (start ()) }

        let advance (s: OdeState) (gap: float) =
            match Ode.solve options rhs s.Time s.State (s.Time + gap) with
            | Ok solution ->
                s.State <- solution.States[solution.States.Length - 1]
                s.Time <- solution.Times[solution.Times.Length - 1]
            | Error message -> raise (System.InvalidOperationException message)

        create clock start' advance
