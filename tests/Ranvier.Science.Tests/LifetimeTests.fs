module Ranvier.Science.Tests.LifetimeTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities

let private parsed (text: string) : Expr =
    match Formula.parse text with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected a tree for \"%s\", got: %s" text message

let private declared (result: Result<Entry, string>) : Entry =
    match result with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected an entry, got: %s" message

/// The ten declarations of `Quantity`, each declaring an identifier in a model.
let private declarations: (string * (Model -> string -> unit)) list =
    [
        "input", (fun model id -> Quantity.input model id "X" Units.one 1.0 |> ignore)
        "inputWithUncertainty", (fun model id -> Quantity.inputWithUncertainty model id "X" Units.one 1.0 0.1 |> ignore)
        "constant", (fun model id -> Quantity.constant model id "X" Units.one 1.0 "a source" |> ignore)
        "measuredConstant", (fun model id -> Quantity.measuredConstant model id "X" Units.one 1.0 0.1 "a source" |> ignore)
        "derive", (fun model id -> Quantity.derive model id "X" Units.one (fun () -> 1.0) |> ignore)
        "deriveWithin",
        (fun model id -> Quantity.deriveWithin (Tolerance.absolute 0.5) model id "X" Units.one (fun () -> 1.0) |> ignore)
        "deriveMeasured", (fun model id -> Quantity.deriveMeasured model id "X" Units.one (fun () -> Uncertain.exact 1.0) |> ignore)
        "formula", (fun model id -> declared (Quantity.formula model id "X" "2 * 3") |> ignore)
        "block", (fun model id -> Quantity.block model id "X" (fun () -> [| 1.0 |]) |> ignore)
        "blockFrom", (fun model id -> Quantity.blockFrom model id "X" (fun _ -> [| 1.0 |]) |> ignore)
    ]

[<Tests>]
let tests =
    testList
        "Lifetime"
        [
            test "each declaration leaves its model with its owner" {
                withGraph (fun () ->
                    for kind, declarationIn in declarations do
                        let model = Model.create kind
                        let declare: string -> unit = declarationIn model
                        let owner = createRoot (fun o -> declare "x"; o)

                        if kind = "block" || kind = "blockFrom" then
                            Expect.throwsT<ArgumentException>
                                (fun () -> Quantity.input model "x" "X" Units.one 2.0 |> ignore)
                                (kind + ": the block holds x before its owner is disposed")
                        else
                            Expect.isTrue (model.TryFind "x").IsSome (kind + ": TryFind finds x before its owner is disposed")

                        owner.Dispose ()
                        Expect.isTrue (model.TryFind "x").IsNone (kind + ": TryFind after the owner is disposed")
                        Expect.isFalse (model.Entries |> List.exists (fun e -> e.Id = "x")) (kind + ": the entries hold no x")
                        Quantity.input model "x" "X again" Units.one 2.0 |> ignore
                        Expect.equal (model.TryFind "x").Value.Name "X again" (kind + ": x declared again"))
            }

            test "a declaration under a disposed owner leaves the model unchanged" {
                withGraph (fun () ->
                    for kind, declarationIn in declarations do
                        let model = Model.create kind
                        let owner = createRoot id
                        owner.Dispose ()
                        runWithOwner owner (fun () -> declarationIn model "x")
                        Expect.isTrue (model.TryFind "x").IsNone (kind + ": TryFind")
                        Expect.isEmpty model.Entries (kind + ": the entries")
                        Quantity.input model "x" "X again" Units.one 2.0 |> ignore)
            }

            testCase "an entry outside any scope leaves with the graph"
            <| fun _ ->
                let model = Model.create "m"
                use g = new Graph ()
                use _ = g.Activate ()
                Quantity.input model "x" "X" Units.one 1.0 |> ignore
                Expect.isTrue (model.TryFind "x").IsSome "TryFind finds x before the graph is disposed"
                g.Dispose ()
                Expect.isTrue (model.TryFind "x").IsNone "TryFind after the graph is disposed"

            test "a constant declared with no active graph stays" {
                Expect.isTrue Graph.TryCurrent.IsNone "no graph is active"
                let model = Model.create "m"
                Quantity.constant model "k" "K" Units.one 2.0 "a source" |> ignore
                Expect.isTrue (model.TryFind "k").IsSome "TryFind finds k"
            }

            test "a declaration inside a pure memo raises" {
                withGraph (fun () ->
                    let model = Model.create "m"

                    let memo =
                        createMemo (fun _ ->
                            Quantity.constant model "k" "K" Units.one 2.0 "a source" |> ignore
                            1.0)

                    Expect.throwsT<InvalidOperationException> (fun () -> memo.Value |> ignore) "reading the memo"
                    Expect.isTrue (model.TryFind "k").IsNone "TryFind finds no k")
            }

            test "declarations inside an effect are replaced on each re-run" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let trigger = createSignal 1.0
                    createEffect (fun () -> Quantity.input model "x" "X" Units.one trigger.Value |> ignore)
                    trigger.Value <- 2.0
                    Expect.equal ((model.TryFind "x").Value.Read ()) 2.0 "the entry of the second run"
                    Expect.equal (model.Entries |> List.map (fun e -> e.Id)) [ "x" ] "the entries")
            }

            test "a re-declared identifier is not followed" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let owner, a = createRoot (fun o -> o, Quantity.input model "a" "A" Units.one 1.0)
                    let f = declared (Quantity.formula model "f" "F" "a * 2")
                    let expr = parsed "a * 2"
                    let evaluate = Formula.evaluator model expr
                    let evaluateMeasured = Formula.measuredEvaluator model expr
                    a.Set 3.0
                    owner.Dispose ()
                    Quantity.input model "a" "A again" Units.one 10.0 |> ignore
                    // 3 · 2, from the first a's last value.
                    Expect.equal (f.Read ()) 6.0 "the formula entry"
                    Expect.equal (evaluate ()) 6.0 "the evaluator"
                    Expect.equal (Uncertain.value (evaluateMeasured ())) 6.0 "the measured evaluator"
                    // 10 · 2, from the second a.
                    Expect.equal (Formula.evaluator model expr ()) 20.0 "an evaluator built after the declaration")
            }
        ]
