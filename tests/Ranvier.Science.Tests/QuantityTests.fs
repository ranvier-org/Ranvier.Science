module Ranvier.Science.Tests.QuantityTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities

[<Tests>]
let tests =
    testList
        "Quantity"
        [
            test "a derived quantity recomputes for the inputs it reads and no others" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.metre 1.0<m>
                    let b = Quantity.input model "b" "B" Units.metre 1.0<m>
                    let mutable runs = 0

                    let d =
                        Quantity.derive model "d" "D" Units.metre (fun () ->
                            runs <- runs + 1
                            a.Value * 2.0)

                    Expect.equal d.Value 2.0<m> "the first read computes a * 2"
                    Expect.equal runs 1 "the first read runs the body once"
                    b.Set 5.0<m>
                    Expect.equal d.Value 2.0<m> "a write to b leaves the value"
                    Expect.equal runs 1 "a write to b recomputes nothing"
                    a.Set 3.0<m>
                    Expect.equal d.Value 6.0<m> "a write to a recomputes the value"
                    Expect.equal runs 2 "a write to a recomputes once")
            }

            test "an equal write recomputes nothing" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.metre 1.0<m>
                    let mutable runs = 0

                    let d =
                        Quantity.derive model "d" "D" Units.metre (fun () ->
                            runs <- runs + 1
                            a.Value * 2.0)

                    d.Value |> ignore
                    Expect.equal runs 1 "the first read runs the body once"
                    a.Set a.Value
                    d.Value |> ignore
                    Expect.equal runs 1 "writing the current value recomputes nothing")
            }

            test "deriveWithin holds its value inside the tolerance" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let x = Quantity.input model "x" "X" Units.metre 0.0<m>
                    let held = Quantity.deriveWithin (Tolerance.absolute 1.0) model "held" "Held" Units.metre (fun () -> x.Value)
                    let mutable runs = 0

                    let downstream =
                        Quantity.derive model "downstream" "Downstream" Units.metre (fun () ->
                            runs <- runs + 1
                            held.Value)

                    Expect.equal downstream.Value 0.0<m> "the first read is the initial value"
                    Expect.equal runs 1 "the first read runs the body once"
                    x.Set 0.4<m>
                    Expect.equal downstream.Value 0.0<m> "0.4 is within 1.0 of 0.0"
                    Expect.equal runs 1 "0.4 recomputes nothing downstream"
                    x.Set 0.8<m>
                    Expect.equal downstream.Value 0.0<m> "0.8 is within 1.0 of 0.0"
                    Expect.equal runs 1 "0.8 recomputes nothing downstream"
                    x.Set 1.2<m>
                    Expect.equal downstream.Value 1.2<m> "1.2 is beyond 1.0 of 0.0"
                    Expect.equal runs 2 "1.2 recomputes downstream once")
            }

            test "an entry describes its quantity" {
                withGraph (fun () ->
                    let model = Model.create "m"

                    let g0 =
                        Quantity.constant
                            model
                            "g0"
                            "Standard gravity"
                            (Units.metre / (UnitOf.squared Units.second))
                            9.80665<_>
                            "CGPM 1901"

                    let entry = g0.Entry
                    Expect.equal entry.Id "g0" "Id"
                    Expect.equal entry.Name "Standard gravity" "Name"
                    Expect.equal entry.Kind Constant "Kind"
                    Expect.equal entry.Source "CGPM 1901" "Source"
                    Expect.equal (UnitInfo.symbol entry.Unit) "m/s^2" "the unit's symbol"
                    Expect.equal (entry.Read ()) 9.80665 "Read"
                    Expect.equal (entry.Status ()) QuantityStatus.Ready "Status")
            }

            test "a child model finds its own entry, then its parent's" {
                withGraph (fun () ->
                    let parent = Model.create "parent"
                    let child = Model.child parent "child"
                    let sibling = Model.child parent "sibling"
                    Quantity.input parent "alpha" "Parent alpha" Units.one 1.0 |> ignore
                    Quantity.input child "mean" "Mean" Units.one 2.0 |> ignore
                    Quantity.input child "alpha" "Child alpha" Units.one 3.0 |> ignore

                    let read (found: Entry voption) =
                        match found with
                        | ValueSome entry -> entry.Read ()
                        | ValueNone -> failtest "expected an entry"

                    Expect.equal (read (child.TryFind "mean")) 2.0 "the child finds its own mean"
                    Expect.equal (read (child.TryFind "alpha")) 3.0 "the child's alpha shadows its parent's"
                    Expect.isTrue (parent.TryFind "mean").IsNone "the parent does not see its child's entries"
                    Expect.equal (read (sibling.TryFind "alpha")) 1.0 "a child without alpha finds its parent's"
                    Expect.equal child.Parent (ValueSome parent) "the child's parent"
                    Expect.equal (child.Entries |> List.map (fun e -> e.Id)) [ "mean"; "alpha" ] "the child's own entries, in order")
            }

            test "a duplicate or malformed identifier is rejected" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.input model "alpha" "Alpha" Units.one 1.0 |> ignore

                    Expect.throwsT<ArgumentException>
                        (fun () -> Quantity.input model "alpha" "Alpha again" Units.one 2.0 |> ignore)
                        "a second alpha in one model"

                    for id in [ "2x"; "a b"; "" ] do
                        Expect.throwsT<ArgumentException>
                            (fun () -> Quantity.input model id "Malformed" Units.one 1.0 |> ignore)
                            ("the identifier \"" + id + "\""))
            }

            test "a body that raises fails its quantity alone" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.one 1.0
                    let b = Quantity.input model "b" "B" Units.one 4.0

                    let d1 =
                        Quantity.derive model "d1" "D1" Units.one (fun () ->
                            if a.Value = 1.0 then
                                raise (InvalidOperationException "boom")

                            a.Value)

                    let d2 = Quantity.derive model "d2" "D2" Units.one (fun () -> b.Value * 2.0)

                    match d1.Entry.Status () with
                    | QuantityStatus.Failed error ->
                        Expect.isTrue (error :? InvalidOperationException) "Failed carries an InvalidOperationException"
                        Expect.equal error.Message "boom" "Failed carries the body's message"
                    | other -> failtestf "expected Failed, got %A" other

                    Expect.throwsT<InvalidOperationException> (fun () -> d1.Value |> ignore) "reading d1 raises the body's exception"
                    Expect.equal d2.Value 8.0 "d2 reads its own input"
                    a.Set 2.0
                    Expect.equal (d1.Entry.Status ()) QuantityStatus.Ready "d1 recovers once its input stops raising"
                    Expect.equal d1.Value 2.0 "d1's recovered value")
            }

            test "a quantity over a pending stream is pending" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let feed = ManualStream<float>()
                    let total = createStreamFold 0.0 (+) (fun _ -> feed.Source)
                    let d = Quantity.derive model "d" "D" Units.metre (fun () -> total.Value * 1.0<m>)
                    Expect.equal (d.Entry.Status ()) QuantityStatus.Pending "pending before the first item"
                    feed.Push 2.5
                    Expect.equal (d.Entry.Status ()) QuantityStatus.Ready "ready after an item"
                    Expect.equal d.Value 2.5<m> "the fold of the item")
            }

            test "a block is tracked like a quantity" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.one 1.0
                    let b = Quantity.input model "b" "B" Units.one 1.0
                    let mutable runs = 0

                    let block =
                        Quantity.block model "series" "Series" (fun () ->
                            runs <- runs + 1
                            Array.init 3 (fun i -> a.Value * float i))

                    Expect.equal block.Value [| 0.0; 1.0; 2.0 |] "the first read"
                    Expect.equal runs 1 "the first read runs the body once"
                    Expect.equal block.Id "series" "Id"
                    Expect.equal block.Name "Series" "Name"
                    b.Set 2.0
                    block.Value |> ignore
                    Expect.equal runs 1 "a write to b recomputes nothing"
                    a.Set 2.0
                    Expect.equal block.Value [| 0.0; 2.0; 4.0 |] "a write to a recomputes"
                    Expect.equal runs 2 "a write to a recomputes once"
                    Expect.isTrue (model.TryFind "series").IsNone "a block has no entry")
            }
        ]
