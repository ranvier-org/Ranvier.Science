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

/// Quantities by identifier, with an optional parent searched after it.
[<Sealed>]
type Model internal (name: string, parent: Model voption) =
    let entries = ResizeArray<Entry>()
    let byId = Dictionary<string, Entry>()
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

module Model =
    let create (name: string) : Model = Model(name, ValueNone)

    let child (parent: Model) (name: string) : Model = Model(name, ValueSome parent)
