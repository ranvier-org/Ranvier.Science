/// <summary>
/// Opens the core before the quantities, and in a nested module the reverse: the core's <c>Reading</c> cases match
/// unqualified and <c>QuantityStatus</c> cases qualified in both orders.
/// </summary>
module Ranvier.Science.Tests.OpenOrderTests

open Expecto
open Ranvier
open Ranvier.Quantities

let private reading (m: Memo<float>) =
    match m.TryValue with
    | Ready v -> sprintf "ready %g" v
    | Pending -> "pending"
    | Failed e -> e.Message

let private status (e: Entry) =
    match e.Status () with
    | QuantityStatus.Ready -> "ready"
    | QuantityStatus.Pending -> "pending"
    | QuantityStatus.Failed error -> error.Message

module private QuantitiesFirst =
    open Ranvier.Quantities
    open Ranvier

    let reading (m: Memo<float>) =
        match m.TryValue with
        | Ready v -> sprintf "ready %g" v
        | Pending -> "pending"
        | Failed e -> e.Message

    let status (e: Entry) =
        match e.Status () with
        | QuantityStatus.Ready -> "ready"
        | QuantityStatus.Pending -> "pending"
        | QuantityStatus.Failed error -> error.Message

[<Tests>]
let tests =
    testList
        "Open order"
        [
            test "a core reading and a quantity status match in either open order" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.one 2.5
                    let memo = createMemo (fun _ -> float a.Value)
                    Expect.equal (reading memo) "ready 2.5" "the core first: the reading"
                    Expect.equal (status a.Entry) "ready" "the core first: the status"
                    Expect.equal (QuantitiesFirst.reading memo) "ready 2.5" "the quantities first: the reading"
                    Expect.equal (QuantitiesFirst.status a.Entry) "ready" "the quantities first: the status")
            }
        ]
