namespace Ranvier.Quantities

open System
open System.Collections.Generic
open Ranvier.Numerics

[<RequireQualifiedAccess>]
type QuantityKind =
    | Input
    | Constant
    | Derived

[<RequireQualifiedAccess>]
type QuantityStatus =
    | Ready
    | Pending
    | Failed of exn

/// What a model holds for one quantity.
[<NoEquality; NoComparison>]
type Entry =
    {
        Id: string
        Name: string
        Unit: UnitInfo
        Kind: QuantityKind
        /// The reference a constant was taken from; "" otherwise.
        Source: string
        /// A tracked read of the value as a plain number in `Unit`. Raises as the node's read does.
        Read: unit -> float
        /// A tracked read of the status. Never raises.
        Status: unit -> QuantityStatus
        /// A tracked read of the standard uncertainty in `Unit`: 0 for an exact value, NaN when unknown. The read of an
        /// entry of `Quantity.deriveMeasured` or `Quantity.formula` raises as `Read` does.
        Uncertainty: unit -> float
        /// A tracked read of the value with its contributions by source. Raises as `Read` does.
        Measured: unit -> Uncertain
        /// The formula text of an entry declared by `Quantity.formula`; "" otherwise.
        Formula: string
    }

/// The tree of a formula entry, with the entries its identifiers resolved to at the entry's declaration.
[<NoEquality; NoComparison>]
type internal FormulaRecord =
    {
        Expr: Expr
        /// Each identifier of the tree that resolved to an entry, with that entry and, for a formula entry, its record.
        Names: Map<string, Entry * FormulaRecord voption>
        /// The catalogue the tree's unit literals are read against.
        Catalogue: UnitCatalogue
    }

/// <summary>Quantities by identifier, with an optional parent searched after it.</summary>
/// <remarks>
/// <para>
/// Under an active graph each declaration of <c>Quantity</c> registers, through <c>onCleanup</c>, one cleanup with the
/// current owner. The cleanup removes the declaration's entry, or a block's reserved identifier, from the model, and
/// keeps the order of the other entries; it removes only the declaration that registered it. Once it has run,
/// <c>TryFind</c> no longer finds the entry and the identifier may be declared again. The cleanup is registered before
/// the entry is added: a registration that raises leaves the model unchanged. The current owner, from core 0.2.1
/// (<c>Api.fs</c>, <c>onCleanup</c>; <c>Core.fs</c>, <c>currentOwner</c>, <c>Graph.AddCleanup</c>,
/// <c>Owner.AddCleanup</c> and <c>Graph.Dispose</c>):
/// </para>
/// <list type="bullet">
/// <item><description>With no active graph (<c>Graph.TryCurrent</c> is <c>ValueNone</c>) a declaration registers
/// nothing and is permanent. Only <c>Quantity.constant</c> and <c>Quantity.measuredConstant</c> can be declared then,
/// since the others create nodes.</description></item>
/// <item><description>Under a graph, and outside any root, effect, owning memo or projection factory, the owner is the
/// graph's root scope: the entry leaves its model when the graph is disposed.</description></item>
/// <item><description>Inside <c>createRoot</c> the owner is the root, and the entry leaves its model when the root is
/// disposed; inside a projection's factory it is the row's scope, disposed when the row's key leaves the
/// projection.</description></item>
/// <item><description>Inside an effect or an owning memo the owner is the computation's scope: the entry leaves its model
/// before each re-run and at disposal.</description></item>
/// <item><description>Inside a pure body (a <c>createMemo</c> computation, a projection row's reader) <c>onCleanup</c>
/// raises <c>InvalidOperationException</c>, and so does every declaration.</description></item>
/// <item><description>Under an owner already disposed the core runs the cleanup at once, and the declaration leaves the
/// model unchanged.</description></item>
/// </list>
/// <para>
/// A read through a removed entry's functions behaves as the core's disposed node does: a disposed memo answers with its
/// last computed outcome and stops tracking (core 0.2.1 <c>Core.fs</c>, <c>Memo.Dispose</c>). An input's signals belong
/// to the graph, not to an owner, and stay settable and tracked.
/// </para>
/// <para>
/// Names resolve once, when an entry, an evaluator or a <c>Formula.monteCarlo</c> reader is created. A formula entry,
/// evaluator or reader that outlives the owner of an entry it reads keeps reading that entry's last value and does not
/// follow a later declaration of the identifier. A reader inlines a formula entry with the names the entry resolved at
/// its declaration.
/// </para>
/// <para>A removal costs O(n) in the model's number of entries.</para>
/// </remarks>
[<Sealed>]
type Model internal (name: string, parent: Model voption, catalogue: UnitCatalogue) =
    let entries = ResizeArray<Entry>()
    let byId = Dictionary<string, Entry>()
    let formulas = Dictionary<string, FormulaRecord>()
    let declared = HashSet<string>()

    static let isLetter (c: char) =
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c = '_'

    static let isIdentifier (id: string) =
        if isNull id || id.Length = 0 || not (isLetter id[0]) then
            false
        else
            let mutable ok = true
            let mutable i = 1

            while ok && i < id.Length do
                let c = id[i]
                ok <- isLetter c || (c >= '0' && c <= '9')
                i <- i + 1

            ok

    member _.Name: string = name
    member _.Parent: Model voption = parent

    /// The catalogue for the unit literals of the formula notation: `UnitCatalogue.si` for `Model.create`, the
    /// catalogue given to `Model.createWith`, and the parent's for `Model.child`.
    member _.Catalogue: UnitCatalogue = catalogue

    /// This model's own entries, in declaration order.
    member _.Entries: Entry list = List.ofSeq entries

    /// This model's entry, else the nearest ancestor's. A block's identifier is reserved in its model and is not an
    /// entry, so a child's block leaves an ancestor's entry of that identifier visible.
    member _.TryFind(id: string) : Entry voption =
        match byId.TryGetValue id with
        | true, entry -> ValueSome entry
        | _ ->
            match parent with
            | ValueSome p -> p.TryFind id
            | ValueNone -> ValueNone

    /// Raises `ArgumentException` for an identifier outside `[A-Za-z_][A-Za-z0-9_]*` and for one the model already declares.
    member internal _.Check(id: string) : unit =
        if not (isIdentifier id) then
            raise (ArgumentException("\"" + string id + "\" is not an identifier: it must match [A-Za-z_][A-Za-z0-9_]*.", "id"))

        if declared.Contains id then
            raise (ArgumentException("The model \"" + name + "\" already declares \"" + id + "\".", "id"))

    member internal _.Reserve(id: string) : unit = declared.Add id |> ignore

    member internal this.Add(entry: Entry) : unit =
        this.Reserve entry.Id
        entries.Add entry
        byId[entry.Id] <- entry

    /// Adds a formula entry with its record.
    member internal this.AddFormula(entry: Entry, record: FormulaRecord) : unit =
        this.Add entry
        formulas[entry.Id] <- record

    /// The entry `TryFind` finds, with its record when it is a formula entry.
    member internal _.TryResolve(id: string) : (Entry * FormulaRecord voption) voption =
        match byId.TryGetValue id with
        | true, entry ->
            match formulas.TryGetValue id with
            | true, record -> ValueSome(entry, ValueSome record)
            | _ -> ValueSome(entry, ValueNone)
        | _ ->
            match parent with
            | ValueSome p -> p.TryResolve id
            | ValueNone -> ValueNone

    /// Removes the entry, when it is this model's entry of its identifier, from the entries, the index, the formula
    /// records and the reserved identifiers. O(n) in the number of entries.
    member internal _.Remove(entry: Entry) : unit =
        match byId.TryGetValue entry.Id with
        | true, held when obj.ReferenceEquals(held, entry) ->
            byId.Remove entry.Id |> ignore
            formulas.Remove entry.Id |> ignore
            declared.Remove entry.Id |> ignore
            entries.RemoveAt(entries.FindIndex(fun e -> obj.ReferenceEquals(e, entry)))
        | _ -> ()

    /// Frees an identifier reserved by a block; an identifier held by an entry stays reserved.
    member internal _.Release(id: string) : unit =
        if not (byId.ContainsKey id) then
            declared.Remove id |> ignore

module Model =
    /// A model without a parent, over `UnitCatalogue.si`.
    let create (name: string) : Model = Model(name, ValueNone, UnitCatalogue.si)

    /// A model without a parent, over the given catalogue.
    let createWith (catalogue: UnitCatalogue) (name: string) : Model = Model(name, ValueNone, catalogue)

    /// A model whose `TryFind` falls back to the parent, over the parent's catalogue.
    let child (parent: Model) (name: string) : Model = Model(name, ValueSome parent, parent.Catalogue)
