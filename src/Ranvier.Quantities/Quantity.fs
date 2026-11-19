namespace Ranvier.Quantities

open Ranvier
open Ranvier.Numerics

/// A named quantity of a model.
type Quantity<[<Measure>] 'u> internal (entry: Entry) =
    member _.Entry: Entry = entry

    /// A tracked read.
    member _.Value: float<'u> = LanguagePrimitives.FloatWithMeasure<'u>(entry.Read ())

/// A quantity set from outside the graph.
[<Sealed>]
type Input<[<Measure>] 'u> internal (entry: Entry, signal: Signal<float>) =
    inherit Quantity<'u>(entry)

    /// Writes the value; a write equal to the current value recomputes nothing.
    member _.Set(value: float<'u>) : unit = signal.Value <- float value

/// A named derived value with no single unit, such as an array.
[<Sealed>]
type Block<'T> internal (id: string, name: string, memo: Memo<'T>) =
    member _.Id: string = id
    member _.Name: string = name

    /// A tracked read.
    member _.Value: 'T = memo.Value

module Quantity =
    let private ready () = QuantityStatus.Ready

    let private statusOf (memo: Memo<float>) () : QuantityStatus =
        match memo.TryValue with
        | Reading.Ready _ -> QuantityStatus.Ready
        | Reading.Pending -> QuantityStatus.Pending
        | Reading.Failed error -> QuantityStatus.Failed error

    let private entry (id: string) (name: string) (unit: UnitInfo) (kind: QuantityKind) (source: string) read status =
        {
            Id = id
            Name = name
            Unit = unit
            Kind = kind
            Source = source
            Read = read
            Status = status
        }

    let private derived (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (memo: Memo<float>) : Quantity<'u> =
        let e = entry id name unit.Info QuantityKind.Derived "" (fun () -> memo.Value) (statusOf memo)
        model.Add e
        Quantity<'u>(e)

    /// An input of value `initial`, over a signal owned by the current owner. Raises `ArgumentException` for an
    /// identifier outside `[A-Za-z_][A-Za-z0-9_]*` and for one the model already declares.
    let input (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (initial: float<'u>) : Input<'u> =
        model.Check id
        let signal = createSignal (float initial)
        let e = entry id name unit.Info QuantityKind.Input "" (fun () -> signal.Value) ready
        model.Add e
        Input<'u>(e, signal)

    /// A fixed value taken from `source`. Raises `ArgumentException` as `input` does.
    let constant (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (value: float<'u>) (source: string) : Quantity<'u> =
        model.Check id
        let plain = float value
        let e = entry id name unit.Info QuantityKind.Constant source (fun () -> plain) ready
        model.Add e
        Quantity<'u>(e)

    /// The value of `compute`, over a memo owned by the current owner. Raises `ArgumentException` as `input` does.
    let derive (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (compute: unit -> float<'u>) : Quantity<'u> =
        model.Check id
        derived model id name unit (createMemo (fun _ -> float (compute ())))

    /// The value of `compute`, held at the last published value while each new value is within `tolerance` of it.
    /// Raises `ArgumentException` as `input` does.
    let deriveWithin
        (tolerance: Tolerance)
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (compute: unit -> float<'u>)
        : Quantity<'u> =
        model.Check id
        derived model id name unit (createMemo (fun previous -> Tolerance.hold tolerance previous (float (compute ()))))

    /// The value of `compute`, over a memo owned by the current owner. Not entered in the model's entries: a block has
    /// no single unit. Its identifier is checked against every declaration of the model, and raises as `input` does.
    let block (model: Model) (id: string) (name: string) (compute: unit -> 'T) : Block<'T> =
        model.Check id
        let memo = createMemo (fun _ -> compute ())
        model.Reserve id
        Block<'T>(id, name, memo)
