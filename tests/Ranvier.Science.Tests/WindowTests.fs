module Ranvier.Science.Tests.WindowTests

open System
open Expecto
open Ranvier.Numerics

let private pushAll (w: Window<float, Moments>) (xs: float[]) = Array.fold Window.push w xs

let private timed (span: float) (pairs: (float * float) list) =
    pairs |> List.fold (fun w (t, x) -> TimeWindow.push w t x) (TimeWindow.moments true span)

[<Tests>]
let tests =
    testList
        "Window"
        [
            test "decayed moments closed form" {
                // Closed form: half-life 1 gives the first item weight 1/2 at t = 1. Weights 1/2 and 1 on 1 and 3:
                // weight 3/2, mean (1/2 + 3) / (3/2) = 7/3, variance (1/2 + 9) / (3/2) - (7/3)^2 = 8/9.
                let a = DecayedMoments.addAt (DecayedMoments.addAt (DecayedMoments.create 1.0) 0.0 1.0) 1.0 3.0
                expectClose 1e-15 1e-15 1.5 (DecayedMoments.weight a) "weight"
                expectClose 1e-15 1e-15 (7.0 / 3.0) (DecayedMoments.mean a) "mean"
                expectClose 1e-15 1e-15 (8.0 / 9.0) (DecayedMoments.variance a) "variance"
                let b = DecayedMoments.addItem (DecayedMoments.addItem (DecayedMoments.create 1.0) 1.0) 3.0
                expectClose 1e-15 1e-15 1.5 (DecayedMoments.weight b) "addItem weight"
                expectClose 1e-15 1e-15 (7.0 / 3.0) (DecayedMoments.mean b) "addItem mean"
                expectClose 1e-15 1e-15 (8.0 / 9.0) (DecayedMoments.variance b) "addItem variance"
            }

            test "an earlier time applies no decay" {
                let s = DecayedMoments.addAt (DecayedMoments.addAt (DecayedMoments.create 1.0) 5.0 1.0) 3.0 3.0
                expectClose 1e-15 1e-15 2.0 (DecayedMoments.weight s) "weight"
                expectClose 1e-15 1e-15 2.0 (DecayedMoments.mean s) "mean"
            }

            test "decayed moments domain" {
                for h in [ 0.0; -1.0; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> DecayedMoments.create h |> ignore) $"half-life {h}"

                let e = DecayedMoments.create 2.0
                Expect.equal (DecayedMoments.weight e) 0.0 "empty weight"
                Expect.isTrue (Double.IsNaN(DecayedMoments.mean e)) "empty mean"
                Expect.isTrue (Double.IsNaN(DecayedMoments.variance e)) "empty variance"
                Expect.isTrue (Double.IsNaN(DecayedMoments.mean (DecayedMoments.addAt e nan 1.0))) "NaN t, first item"

                let n = DecayedMoments.addAt (DecayedMoments.addAt e 0.0 1.0) nan 2.0
                Expect.isTrue (Double.IsNaN(DecayedMoments.mean n)) "NaN t, later item"
            }

            test "a huge gap leaves only the new item, and an infinite half-life never decays" {
                let s = DecayedMoments.addAt (DecayedMoments.addAt (DecayedMoments.create 1.0) 0.0 5.0) 1e6 7.0
                Expect.equal (DecayedMoments.weight s) 1.0 "weight 1"
                Expect.equal (DecayedMoments.mean s) 7.0 "mean x"
                let k = DecayedMoments.addAt (DecayedMoments.addAt (DecayedMoments.create infinity) 0.0 1.0) 1e6 3.0
                Expect.equal (DecayedMoments.weight k) 2.0 "no decay"
                Expect.equal (DecayedMoments.mean k) 2.0 "plain mean"
            }

            test "a window keeps the last capacity items" {
                let w = pushAll (Window.moments true 3) [| 1.0; 2.0; 3.0; 4.0; 5.0 |]
                Expect.equal (Window.items w) [| 3.0; 4.0; 5.0 |] "items"
                Expect.equal (Window.count w) 3 "count"
                Expect.equal (Moments.mean (Window.value w)) 4.0 "mean"
            }

            test "re-folds bound drift" {
                let drive compensated =
                    let mutable w = Window.moments compensated 50

                    for i in 0..99999 do
                        w <- Window.push w (1e9 + float (i % 7))

                    let fresh = Array.fold (Moments.add compensated) Moments.zero (Window.items w)
                    Window.value w, fresh

                let held, fresh = drive true
                expectClose 1e-12 0.0 (Moments.mean fresh) (Moments.mean held) "compensated mean"
                expectClose 1e-9 0.0 (Moments.variance fresh) (Moments.variance held) "compensated variance"
                let heldPlain, freshPlain = drive false
                expectClose 1e-5 0.0 (Moments.variance freshPlain) (Moments.variance heldPlain) "plain variance"
            }

            test "work per push is amortised constant" {
                let adds = ref 0
                let removes = ref 0

                let w0 =
                    Window.create
                        10
                        (fun (a: float) (x: float) ->
                            adds.Value <- adds.Value + 1
                            a + x)
                        (fun a x ->
                            removes.Value <- removes.Value + 1
                            a - x)
                        0.0

                let mutable w = w0

                for i in 1..1000 do
                    w <- Window.push w (float i)

                Expect.isLessThanOrEqual adds.Value (2 * 1000 + 10) "adds"
                Expect.isLessThanOrEqual removes.Value 1000 "removes"
                // Closed form: the last ten items are 991..1000, whose sum is 10 * (991 + 1000) / 2.
                Expect.equal (Window.value w) 9955.0 "sum of the last ten"
            }

            test "a window is persistent" {
                let w = pushAll (Window.moments true 3) [| 1.0; 2.0; 3.0 |]
                let a = Window.push w 4.0
                let b = Window.push w 5.0
                Expect.equal (Window.items a) [| 2.0; 3.0; 4.0 |] "first branch"
                Expect.equal (Window.items b) [| 2.0; 3.0; 5.0 |] "second branch"
                Expect.equal (Window.items w) [| 1.0; 2.0; 3.0 |] "original"
                Expect.equal (Moments.mean (Window.value w)) 2.0 "original value"
                Expect.equal (Window.count w) 3 "original count"
            }

            test "copies out" {
                let w = pushAll (Window.moments true 3) [| 1.0; 2.0; 3.0 |]
                let a = Window.items w
                a[0] <- 99.0
                Expect.equal (Window.items w) [| 1.0; 2.0; 3.0 |] "items unchanged"
                let t = timed 10.0 [ (0.0, 1.0) ]
                let b = TimeWindow.items t
                b[0] <- 99.0
                Expect.equal (TimeWindow.items t) [| 1.0 |] "time window items unchanged"
            }

            test "a time window keeps items no older than span" {
                let w = timed 10.0 [ (0.0, 1.0); (5.0, 2.0); (10.0, 3.0); (15.0, 4.0) ]
                Expect.equal (TimeWindow.items w) [| 2.0; 3.0; 4.0 |] "items of 5, 10 and 15"
                let same = TimeWindow.push w 15.0 5.0
                Expect.equal (TimeWindow.items same) [| 2.0; 3.0; 4.0; 5.0 |] "an equal time is accepted"
            }

            test "a time window's moments, value and count" {
                let w = timed 10.0 [ (0.0, 1.0); (5.0, 2.0); (10.0, 3.0); (15.0, 4.0) ]
                Expect.equal (TimeWindow.count w) 3 "count"
                Expect.equal (TimeWindow.items w) [| 2.0; 3.0; 4.0 |] "items"
                let m = TimeWindow.value w
                Expect.equal (Moments.count m) 3.0 "moments count"
                Expect.equal (Moments.mean m) 3.0 "mean"
                // Closed form: the population variance of 2, 3, 4 is 2/3.
                expectClose 1e-15 1e-15 (2.0 / 3.0) (Moments.variance m) "variance"
                let far = TimeWindow.push w 40.0 9.0
                Expect.equal (TimeWindow.count far) 1 "one item left"
                Expect.equal (Moments.mean (TimeWindow.value far)) 9.0 "mean of the one"
                let e = TimeWindow.moments true 10.0
                Expect.equal (TimeWindow.count e) 0 "empty count"
                Expect.equal (Moments.count (TimeWindow.value e)) 0.0 "empty moments count"
            }

            test "a time window's domain" {
                let w = timed 10.0 [ (5.0, 1.0) ]
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> TimeWindow.push w 4.0 1.0 |> ignore) "earlier time"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> TimeWindow.push w nan 1.0 |> ignore) "NaN time"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> TimeWindow.moments true -1.0 |> ignore) "negative span"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> TimeWindow.moments true nan |> ignore) "NaN span"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Window.create 0 (fun a _ -> a) (fun a _ -> a) 0.0 |> ignore) "capacity 0"
            }
        ]
