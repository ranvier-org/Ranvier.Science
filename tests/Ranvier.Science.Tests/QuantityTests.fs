module Ranvier.Science.Tests.QuantityTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities

// Fable 5.27 miscompiles an array pattern over struct tuples, so the tests project the entries instead.
let private sourcesOf (u: Uncertain) : int[] =
    Uncertain.contributions u |> Array.map (fun struct (id, _) -> id)

let private relative (actual: float) (expected: float) = abs (actual - expected) / abs expected

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
                    Expect.equal entry.Kind QuantityKind.Constant "Kind"
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

            test "an identifier held by a quantity or a block is rejected for every declaration" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Quantity.input model "alpha" "Alpha" Units.one 1.0 |> ignore
                    Quantity.block model "series" "Series" (fun () -> [| 1.0 |]) |> ignore
                    let ids () = model.Entries |> List.map (fun e -> e.Id)

                    let declarations: (string * (string -> unit)) list =
                        [
                            "input", (fun id -> Quantity.input model id "Again" Units.one 2.0 |> ignore)
                            "constant", (fun id -> Quantity.constant model id "Again" Units.one 2.0 "source" |> ignore)
                            "derive", (fun id -> Quantity.derive model id "Again" Units.one (fun () -> 2.0) |> ignore)
                            "block", (fun id -> Quantity.block model id "Again" (fun () -> 2.0) |> ignore)
                        ]

                    for kind, declare in declarations do
                        for held in [ "alpha"; "series" ] do
                            Expect.throwsT<ArgumentException> (fun () -> declare held) (sprintf "a %s named %s" kind held)
                            Expect.equal (ids ()) [ "alpha" ] (sprintf "the entries after a rejected %s named %s" kind held))
            }

            test "a child's block does not shadow its parent's quantity" {
                withGraph (fun () ->
                    let parent = Model.create "parent"
                    let child = Model.child parent "child"
                    Quantity.input parent "alpha" "Parent alpha" Units.one 1.0 |> ignore
                    Quantity.block child "alpha" "Child block alpha" (fun () -> [| 1.0 |]) |> ignore

                    match child.TryFind "alpha" with
                    | ValueSome entry -> Expect.equal entry.Name "Parent alpha" "the child finds the parent's entry"
                    | ValueNone -> failtest "expected the parent's entry"

                    Expect.throwsT<ArgumentException>
                        (fun () -> Quantity.input child "alpha" "Child alpha" Units.one 2.0 |> ignore)
                        "the child's block holds the identifier in the child")
            }

            test "a block built with blockFrom holds a series within a tolerance" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.one 1.0

                    let series =
                        Quantity.blockFrom model "series" "Series" (fun previous ->
                            Tolerance.holdSeries (Tolerance.absolute 0.1) previous (Array.init 3 (fun i -> a.Value * float i)))

                    let first = series.Value
                    let mutable runs = 0

                    createEffect (fun () ->
                        series.Value |> ignore
                        runs <- runs + 1)

                    a.Set 1.04
                    Expect.isTrue (obj.ReferenceEquals (series.Value, first)) "every element within 0.1: the same array instance"
                    Expect.equal runs 1 "every element within 0.1: the reader is not notified"
                    a.Set 1.06
                    Expect.isFalse (obj.ReferenceEquals (series.Value, first)) "one element past 0.1: a new array"
                    Expect.equal series.Value [| 0.0; 1.06; 2.12 |] "one element past 0.1: the new values"
                    Expect.equal runs 2 "one element past 0.1: the reader is notified")
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

            test "an input carries a source of uncertainty 0" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.metre 2.0<m>
                    let measured = a.Entry.Measured ()
                    Expect.equal (a.Entry.Uncertainty ()) 0.0 "Uncertainty"
                    Expect.equal (Uncertain.value measured) 2.0 "the measured value"
                    Expect.equal (Uncertain.standardUncertainty measured) 0.0 "the measured standard uncertainty"
                    Expect.equal (sourcesOf measured).Length 1 "one contribution"
                    Expect.equal a.Entry.Formula "" "Formula")
            }

            test "inputWithUncertainty and SetUncertainty" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let b = Quantity.inputWithUncertainty model "b" "B" Units.metre 5.0<m> 0.25<m>
                    Expect.equal (b.Entry.Uncertainty ()) 0.25 "Uncertainty"
                    Expect.equal (Uncertain.standardUncertainty (b.Entry.Measured ())) 0.25 "the measured standard uncertainty"
                    let mutable runs = 0
                    let mutable valueRuns = 0

                    let uncertainty =
                        createMemo (fun _ ->
                            runs <- runs + 1
                            b.Entry.Uncertainty ())

                    createEffect (fun () ->
                        b.Value |> ignore
                        valueRuns <- valueRuns + 1)

                    Expect.equal uncertainty.Value 0.25 "the memo's first read"
                    Expect.equal runs 1 "the first read runs the memo once"
                    b.SetUncertainty 0.5<m>
                    Expect.equal uncertainty.Value 0.5 "the new uncertainty"
                    Expect.equal runs 2 "a new uncertainty runs the memo once more"
                    b.SetUncertainty 0.5<m>
                    Expect.equal uncertainty.Value 0.5 "an equal uncertainty"
                    Expect.equal runs 2 "an equal uncertainty runs nothing"
                    Expect.equal valueRuns 1 "an uncertainty change leaves a reader of the value unnotified"
                    let before = sourcesOf (b.Entry.Measured ())
                    b.Set 6.0<m>
                    Expect.equal (Uncertain.value (b.Entry.Measured ())) 6.0 "the measured value after Set"
                    Expect.equal (sourcesOf (b.Entry.Measured ())) before "Set keeps the input's source")
            }

            test "an uncertainty must not be negative" {
                withGraph (fun () ->
                    let model = Model.create "m"

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Quantity.inputWithUncertainty model "a" "A" Units.metre 1.0<m> (-0.1<m>) |> ignore)
                        "inputWithUncertainty with -0.1"

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Quantity.measuredConstant model "c" "C" Units.metre 1.0<m> (-0.1<m>) "a source" |> ignore)
                        "measuredConstant with -0.1"

                    Expect.isEmpty model.Entries "a rejected declaration adds no entry"
                    let b = Quantity.input model "b" "B" Units.metre 1.0<m>
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> b.SetUncertainty(-0.1<m>)) "SetUncertainty -0.1"
                    Expect.equal (b.Entry.Uncertainty ()) 0.0 "a rejected SetUncertainty leaves the uncertainty"
                    let n = Quantity.inputWithUncertainty model "n" "N" Units.metre 1.0<m> (nan * 1.0<m>)
                    Expect.isTrue (Double.IsNaN (n.Entry.Uncertainty ())) "a NaN uncertainty reads NaN")
            }

            test "exact and measured constants" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let c = Quantity.constant model "c" "C" Units.metre 2.0<m> "a reference"
                    Expect.equal (c.Entry.Uncertainty ()) 0.0 "an exact constant's Uncertainty"

                    Expect.equal
                        (Uncertain.standardUncertainty (c.Entry.Measured ()))
                        0.0
                        "an exact constant's measured standard uncertainty"

                    let g = Quantity.measuredConstant model "g" "G" Units.one 9.8 0.1 "a source"
                    Expect.equal g.Entry.Kind QuantityKind.Constant "Kind"
                    Expect.equal g.Entry.Source "a source" "Source"
                    Expect.equal (g.Entry.Read ()) 9.8 "Read"
                    Expect.equal (g.Entry.Uncertainty ()) 0.1 "Uncertainty"
                    Expect.equal (Uncertain.standardUncertainty (g.Entry.Measured ())) 0.1 "the measured standard uncertainty")
            }

            test "derived entries have an unknown uncertainty" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.input model "a" "A" Units.metre 3.0<m>
                    let d = Quantity.derive model "d" "D" Units.metre (fun () -> a.Value * 2.0)

                    let w =
                        Quantity.deriveWithin (Tolerance.absolute 0.5) model "w" "W" Units.metre (fun () -> a.Value + 1.0<m>)

                    for e in [ d.Entry; w.Entry ] do
                        Expect.isTrue (Double.IsNaN (e.Uncertainty ())) (e.Id + ": Uncertainty is NaN")
                        Expect.equal (Uncertain.value (e.Measured ())) (e.Read ()) (e.Id + ": the measured value is Read")

                        Expect.isTrue
                            (Double.IsNaN (Uncertain.standardUncertainty (e.Measured ())))
                            (e.Id + ": the measured standard uncertainty is NaN"))
            }

            test "deriveMeasured propagates from the entries it reads" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.inputWithUncertainty model "a" "A" Units.metre 3.0<m> 0.1<m>
                    let b = Quantity.inputWithUncertainty model "b" "B" Units.metre 4.0<m> 0.2<m>

                    let area =
                        Quantity.deriveMeasured model "area" "Area" (UnitOf.squared Units.metre) (fun () ->
                            a.Entry.Measured () * b.Entry.Measured ())

                    Expect.equal area.Value 12.0<m^2> "the value"
                    // ∂(ab)/∂a = b and ∂(ab)/∂b = a, so u = √((0.1 · 4)² + (0.2 · 3)²).
                    let expected = sqrt ((0.1 * 4.0) ** 2.0 + (0.2 * 3.0) ** 2.0)
                    let actual = area.Entry.Uncertainty ()

                    Expect.isTrue
                        (relative actual expected <= 1e-15)
                        (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" actual expected)

                    Expect.equal area.Entry.Kind QuantityKind.Derived "Kind")
            }

            test "two entries that read one input are correlated" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.inputWithUncertainty model "a" "A" Units.metre 3.0<m> 0.1<m>
                    let d1 = Quantity.deriveMeasured model "d1" "D1" Units.metre (fun () -> 2.0 * a.Entry.Measured ())
                    let d2 = Quantity.deriveMeasured model "d2" "D2" Units.metre (fun () -> -(a.Entry.Measured ()))
                    // 2a and −a hold a's source with contributions 0.2 and −0.1: correlation −1.
                    let r = Uncertain.correlation (d1.Entry.Measured ()) (d2.Entry.Measured ())
                    Expect.isTrue (abs (r + 1.0) <= 1e-15) (sprintf "correlation %.17g within 1e-15 of -1" r))
            }

            test "an uncertainty change leaves deriveMeasured's value readers unnotified" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let a = Quantity.inputWithUncertainty model "a" "A" Units.metre 3.0<m> 0.1<m>
                    let b = Quantity.inputWithUncertainty model "b" "B" Units.metre 4.0<m> 0.2<m>

                    let area =
                        Quantity.deriveMeasured model "area" "Area" (UnitOf.squared Units.metre) (fun () ->
                            a.Entry.Measured () * b.Entry.Measured ())

                    let mutable runs = 0

                    createEffect (fun () ->
                        area.Value |> ignore
                        runs <- runs + 1)

                    let before = area.Entry.Uncertainty ()
                    a.SetUncertainty 0.2<m>
                    Expect.equal runs 1 "a new uncertainty of a leaves the effect unrun"
                    let after = area.Entry.Uncertainty ()
                    Expect.notEqual after before "the area's uncertainty follows a's"
                    // u = √((0.2 · 4)² + (0.2 · 3)²) once a's uncertainty is 0.2.
                    let expected = sqrt ((0.2 * 4.0) ** 2.0 + (0.2 * 3.0) ** 2.0)

                    Expect.isTrue
                        (relative after expected <= 1e-15)
                        (sprintf "Uncertainty %.17g within relative 1e-15 of %.17g" after expected)

                    a.Set 5.0<m>
                    Expect.equal runs 2 "a new value of a runs the effect once"
                    Expect.equal area.Value 20.0<m^2> "the value after the write")
            }
        ]
