namespace Ranvier.Quantities

open System
open Ranvier
open Ranvier.Numerics

module internal StandardUncertainty =
    /// Raises `ArgumentOutOfRangeException` for a negative standard uncertainty; NaN passes.
    let check (uncertainty: float) : unit =
        if uncertainty < 0.0 then
            raise (ArgumentOutOfRangeException("uncertainty", "A standard uncertainty must be zero or more, or NaN."))

/// A named quantity of a model.
type Quantity<[<Measure>] 'u> internal (entry: Entry) =
    member _.Entry: Entry = entry

    /// A tracked read.
    member _.Value: float<'u> = LanguagePrimitives.FloatWithMeasure<'u>(entry.Read ())

/// A quantity set from outside the graph.
[<Sealed>]
type Input<[<Measure>] 'u> internal (entry: Entry, signal: Signal<float>, uncertaintySignal: Signal<float>) =
    inherit Quantity<'u>(entry)

    /// Writes the value; a write equal to the current value recomputes nothing.
    member _.Set(value: float<'u>) : unit = signal.Value <- float value

    /// Writes the standard uncertainty read by `Entry.Uncertainty` and `Entry.Measured`. Readers of the value alone stay
    /// clean, and a write equal to the current uncertainty recomputes nothing. Raises `ArgumentOutOfRangeException` for
    /// a negative uncertainty; NaN is accepted and reads NaN.
    member _.SetUncertainty(uncertainty: float<'u>) : unit =
        StandardUncertainty.check (float uncertainty)
        uncertaintySignal.Value <- float uncertainty

/// A named derived value with no single unit, such as an array.
[<Sealed>]
type Block<'T> internal (id: string, name: string, memo: Memo<'T>) =
    member _.Id: string = id
    member _.Name: string = name

    /// A tracked read.
    member _.Value: 'T = memo.Value

/// Declarations of a model's entries and blocks. Under an active graph each declaration registers, with the current
/// owner, one cleanup that removes it from its model; inside a pure body it raises `InvalidOperationException`.
/// `Model`'s remarks give the rules.
module Quantity =
    let private ready () = QuantityStatus.Ready

    let private statusOf (memo: Memo<float>) () : QuantityStatus =
        match memo.TryValue with
        | Reading.Ready _ -> QuantityStatus.Ready
        | Reading.Pending -> QuantityStatus.Pending
        | Reading.Failed error -> QuantityStatus.Failed error

    let private unknown () = nan

    /// `value`, holding the one source of `unitSource` (a source of contribution 1) with contribution `uncertainty`.
    let private measured (unitSource: Uncertain) (value: float) (uncertainty: float) : Uncertain =
        Uncertain.map (fun _ -> value) (fun _ -> uncertainty) unitSource

    let private entry
        (id: string)
        (name: string)
        (unit: UnitInfo)
        (kind: QuantityKind)
        (source: string)
        read
        status
        uncertainty
        measured
        (formula: string)
        =
        {
            Id = id
            Name = name
            Unit = unit
            Kind = kind
            Source = source
            Read = read
            Status = status
            Uncertainty = uncertainty
            Measured = measured
            Formula = formula
        }

    /// Registers `remove` with the current owner through `onCleanup`, then runs `add`; with no active graph, runs `add`
    /// alone. Under an owner already disposed the core runs `remove` at once, and `add` is skipped. Raises as `onCleanup`
    /// does, before `add` runs.
    let private declareOwned (add: unit -> unit) (remove: unit -> unit) : unit =
        match Graph.TryCurrent with
        | ValueNone -> add ()
        | ValueSome _ ->
            let removed = ref false

            onCleanup (fun () ->
                removed.Value <- true
                remove ())

            if not removed.Value then
                add ()

    /// Adds the entry to the model, removed from it when the current owner is cleaned up.
    let private add (model: Model) (e: Entry) : unit =
        declareOwned (fun () -> model.Add e) (fun () -> model.Remove e)

    let private derived (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (memo: Memo<float>) : Quantity<'u> =
        let unitSource = Uncertain.source 1.0 0.0

        let e =
            entry
                id
                name
                unit.Info
                QuantityKind.Derived
                ""
                (fun () -> memo.Value)
                (statusOf memo)
                unknown
                (fun () -> measured unitSource memo.Value nan)
                ""

        add model e
        Quantity<'u>(e)

    let private declareInput
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (initial: float<'u>)
        (uncertainty: float<'u>)
        : Input<'u> =
        let unitSource = Uncertain.source 1.0 0.0
        let signal = createSignal (float initial)
        let uncertaintySignal = createSignal (float uncertainty)

        let e =
            entry
                id
                name
                unit.Info
                QuantityKind.Input
                ""
                (fun () -> signal.Value)
                ready
                (fun () -> uncertaintySignal.Value)
                (fun () -> measured unitSource signal.Value uncertaintySignal.Value)
                ""

        add model e
        Input<'u>(e, signal, uncertaintySignal)

    let private declareConstant
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (value: float<'u>)
        (uncertainty: float<'u>)
        (source: string)
        : Quantity<'u> =
        let plain = float value
        let u = float uncertainty
        let fixedMeasured = measured (Uncertain.source 1.0 0.0) plain u

        let e =
            entry
                id
                name
                unit.Info
                QuantityKind.Constant
                source
                (fun () -> plain)
                ready
                (fun () -> u)
                (fun () -> fixedMeasured)
                ""

        add model e
        Quantity<'u>(e)

    /// An input of value `initial` and uncertainty 0, over two signals, one for the value and one for the standard
    /// uncertainty, and an uncertainty source of its own. Raises `ArgumentException` for an
    /// identifier outside `[A-Za-z_][A-Za-z0-9_]*` and for one the model already declares.
    let input (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (initial: float<'u>) : Input<'u> =
        model.Check id
        declareInput model id name unit initial (LanguagePrimitives.FloatWithMeasure 0.0)

    /// An input of value `initial` and standard uncertainty `uncertainty`, as `input` makes. Raises `ArgumentException`
    /// as `input` does, and `ArgumentOutOfRangeException` for a negative uncertainty; NaN is accepted and reads NaN.
    let inputWithUncertainty
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (initial: float<'u>)
        (uncertainty: float<'u>)
        : Input<'u> =
        model.Check id
        StandardUncertainty.check (float uncertainty)
        declareInput model id name unit initial uncertainty

    /// A fixed value taken from `source`, of uncertainty 0. Raises `ArgumentException` as `input` does.
    let constant (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (value: float<'u>) (source: string) : Quantity<'u> =
        model.Check id
        declareConstant model id name unit value (LanguagePrimitives.FloatWithMeasure 0.0) source

    /// A fixed value taken from `source`, of standard uncertainty `uncertainty` from an uncertainty source of its own,
    /// with `QuantityKind.Constant`. Values computed from its `Entry.Measured` hold that source and are correlated
    /// through it. Raises `ArgumentException` as `input` does, and `ArgumentOutOfRangeException` for a negative
    /// uncertainty; NaN is accepted and reads NaN.
    let measuredConstant
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (value: float<'u>)
        (uncertainty: float<'u>)
        (source: string)
        : Quantity<'u> =
        model.Check id
        StandardUncertainty.check (float uncertainty)
        declareConstant model id name unit value uncertainty source

    /// The value of `compute`, over a memo owned by the current owner. The uncertainty is unknown: NaN, from a source of
    /// its own with a NaN contribution. `deriveMeasured` propagates an uncertainty. Raises `ArgumentException` as `input`
    /// does.
    let derive (model: Model) (id: string) (name: string) (unit: UnitOf<'u>) (compute: unit -> float<'u>) : Quantity<'u> =
        model.Check id
        derived model id name unit (createMemo (fun _ -> float (compute ())))

    /// The value of `compute`, held at the last published value while each new value is within `tolerance` of it. The
    /// uncertainty is unknown, as `derive`'s is. `deriveMeasured` propagates an uncertainty. Raises `ArgumentException`
    /// as `input` does.
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

    /// `deriveMeasured` with `source` as the entry's `Source`.
    let internal deriveMeasuredFrom
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (source: string)
        (compute: unit -> Uncertain)
        : Quantity<'u> =
        model.Check id
        let result = createMemo (fun _ -> compute ())
        let value = createMemo (fun _ -> Uncertain.value result.Value)

        let e =
            entry
                id
                name
                unit.Info
                QuantityKind.Derived
                source
                (fun () -> value.Value)
                (statusOf value)
                (fun () -> Uncertain.standardUncertainty result.Value)
                (fun () -> result.Value)
                ""

        add model e
        Quantity<'u>(e)

    /// The value of `compute`, with the standard uncertainty and contributions of its result: first-order propagation
    /// from the `Entry.Measured` reads in `compute`. A `QuantityKind.Derived` over two memos owned by the current owner,
    /// the result and its value. `Entry.Uncertainty` and `Entry.Measured` read the result and are notified on every
    /// recomputation; `Value`, `Entry.Read` and `Entry.Status` read the value and are notified only when it changes, so
    /// they stay clean through a change of an uncertainty alone. Raises `ArgumentException` as `input` does.
    let deriveMeasured
        (model: Model)
        (id: string)
        (name: string)
        (unit: UnitOf<'u>)
        (compute: unit -> Uncertain)
        : Quantity<'u> =
        deriveMeasuredFrom model id name unit "" compute

    /// The entry of the formula `text` over the identifiers of `model`: a `QuantityKind.Derived` with the unit
    /// `Formula.check` gives and `Entry.Formula` set to `text`, over two memos owned by the current owner, the value
    /// (as `Formula.evaluator` computes it) and the measured value (as `Formula.measuredEvaluator` computes it, only
    /// when read). `Entry.Read` and `Entry.Status` read the value and are notified only when it changes, so they stay
    /// clean through a change of an uncertainty alone; `Entry.Uncertainty` and `Entry.Measured` read the measured value
    /// and are notified on every recomputation. Names resolve once, when the entry is created: an entry that outlives
    /// the owner of an entry it reads keeps reading that entry's last value and does not follow a later declaration of
    /// the identifier, and `Formula.monteCarlo` inlines the entry's formula with those names. Raises
    /// `ArgumentException` as `input` does, before `text` is parsed. `Error` with the message of
    /// `Formula.parse` or `Formula.check` for text that does not parse or check, and then the model is unchanged.
    let formula (model: Model) (id: string) (name: string) (text: string) : Result<Entry, string> =
        model.Check id

        Formula.parse text
        |> Result.bind (fun expr -> Formula.check model expr |> Result.map (fun unitInfo -> expr, unitInfo))
        |> Result.map (fun (expr, unitInfo) ->
            let record = Formula.recordOf model expr
            let evaluate = Formula.evaluator model expr
            let evaluateMeasured = Formula.measuredEvaluator model expr
            let value = createMemo (fun _ -> evaluate ())
            let measuredValue = createMemo (fun _ -> evaluateMeasured ())

            let e =
                entry
                    id
                    name
                    unitInfo
                    QuantityKind.Derived
                    ""
                    (fun () -> value.Value)
                    (statusOf value)
                    (fun () -> Uncertain.standardUncertainty measuredValue.Value)
                    (fun () -> measuredValue.Value)
                    text

            declareOwned (fun () -> model.AddFormula(e, record)) (fun () -> model.Remove e)
            e)

    /// The entry as a quantity in `unit`. `Error` stating both units unless `UnitInfo.sameUnit` holds for the unit and
    /// the entry's unit.
    let view (unit: UnitOf<'u>) (entry: Entry) : Result<Quantity<'u>, string> =
        if UnitInfo.sameUnit unit.Info entry.Unit then
            Result.Ok(Quantity<'u>(entry))
        else
            Result.Error(
                "The entry \""
                + entry.Id
                + "\" is in "
                + Formula.describe entry.Unit
                + ", not in "
                + Formula.describe unit.Info
                + "."
            )

    /// The value of `compute`, which receives the block's last published value (`ValueNone` before the first), over a
    /// memo owned by the current owner. Returning that value keeps the block's readers clean. Identifiers follow `block`.
    let blockFrom (model: Model) (id: string) (name: string) (compute: 'T voption -> 'T) : Block<'T> =
        model.Check id
        let memo = createMemo compute
        declareOwned (fun () -> model.Reserve id) (fun () -> model.Release id)
        Block<'T>(id, name, memo)

    /// The value of `compute`, over a memo owned by the current owner. A block has no single unit and is not an entry
    /// of its model: its identifier is reserved in the model, so a later declaration of it in the model raises as
    /// `input` does, and `TryFind` of it from a child model finds an ancestor's entry. Raises as `input` does.
    let block (model: Model) (id: string) (name: string) (compute: unit -> 'T) : Block<'T> =
        blockFrom model id name (fun _ -> compute ())
