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
                // Measured at counts that do not end on a re-fold: the held value carries up to one capacity of removals.
                for compensated, varianceBound in [ (true, 1e-8); (false, 1e-6) ] do
                    let mutable w = Window.moments compensated 50
                    let mutable pushed = 0

                    for n in [ 100001; 100010; 100049 ] do
                        while pushed < n do
                            w <- Window.push w (1e9 + float (pushed % 7))
                            pushed <- pushed + 1

                        let fresh = Array.fold (Moments.add compensated) Moments.zero (Window.items w)
                        let held = Window.value w
                        expectClose 1e-12 0.0 (Moments.mean fresh) (Moments.mean held) $"mean at {n}, compensated {compensated}"
                        expectClose varianceBound 0.0 (Moments.variance fresh) (Moments.variance held) $"variance at {n}, compensated {compensated}"
            }

            test "work per push is amortised constant" {
                let pushes = 5000

                for capacity in [ 1; 3; 50; 1000 ] do
                    let adds = ref 0
                    let removes = ref 0

                    let mutable w =
                        Window.create
                            capacity
                            (fun (a: float) (x: float) ->
                                adds.Value <- adds.Value + 1
                                a + x)
                            (fun a x ->
                                removes.Value <- removes.Value + 1
                                a - x)
                            0.0

                    for i in 1..pushes do
                        w <- Window.push w (float i)

                    Expect.isLessThanOrEqual adds.Value (2 * pushes) $"adds at capacity {capacity}"
                    Expect.isLessThanOrEqual removes.Value pushes $"removes at capacity {capacity}"
                    // Closed form: the last items are pushes - capacity + 1 .. pushes, summing to capacity * (2 * pushes - capacity + 1) / 2.
                    Expect.equal (Window.value w) (float capacity * float (2 * pushes - capacity + 1) / 2.0) $"sum at capacity {capacity}"
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

            test "a window is persistent over a branching tree" {
                let root = pushAll (Window.moments true 3) [| 1.0; 2.0; 3.0 |]

                let rec branch (depth: int) (parent: float list) (w: Window<float, Moments>) =
                    if depth > 0 then
                        for k in 1..3 do
                            let x = float (depth * 100 + k)
                            let expected = parent @ [ x ] |> List.rev |> List.truncate 3 |> List.rev
                            let child = Window.push w x
                            Expect.equal (Window.items child) (List.toArray expected) $"items of a child at depth {depth}"
                            expectClose 1e-12 0.0 (List.average expected) (Moments.mean (Window.value child)) "child mean"
                            branch (depth - 1) expected child
                            Expect.equal (Window.items child) (List.toArray expected) "child unchanged by its own branches"

                branch 3 [ 1.0; 2.0; 3.0 ] root
                Expect.equal (Window.items root) [| 1.0; 2.0; 3.0 |] "root unchanged"
            }

            test "a non-finite item stops poisoning once it has left" {
                let items = [| 1.0; 2.0; nan; 4.0; 5.0; 6.0; 7.0; 8.0; infinity; nan; 3.0; 4.0; 5.0; 6.0; 7.0; 8.0; 9.0 |]

                let check (what: string) (i: int) (held: Moments) (current: float[]) =
                    if Array.forall (fun (x: float) -> not (Double.IsNaN x || Double.IsInfinity x)) current then
                        let fresh = Array.fold (Moments.add true) Moments.zero current
                        expectClose 1e-12 0.0 (Moments.mean fresh) (Moments.mean held) $"{what} mean after push {i}"
                        expectClose 1e-9 1e-12 (Moments.variance fresh) (Moments.variance held) $"{what} variance after push {i}"

                for capacity in [ 1; 2; 5 ] do
                    let mutable w = Window.moments true capacity
                    let mutable tw = TimeWindow.moments true (float capacity - 1.0)

                    for i in 0 .. items.Length - 1 do
                        w <- Window.push w items[i]
                        tw <- TimeWindow.push tw (float i) items[i]
                        check $"window {capacity}" i (Window.value w) (Window.items w)
                        check $"time window {capacity}" i (TimeWindow.value tw) (TimeWindow.items tw)

                let w = pushAll (Window.moments true 5) [| 1.0; 2.0; nan; 4.0; 5.0; 6.0; 7.0; 8.0 |]
                Expect.equal (Window.items w) [| 4.0; 5.0; 6.0; 7.0; 8.0 |] "items"
                Expect.equal (Moments.mean (Window.value w)) 6.0 "mean after the NaN left"
            }

            test "a time window re-folds, bounds drift and does amortised work" {
                let adds = ref 0
                let removes = ref 0
                let pushes = 100049
                let compensated = true

                let add (a: Moments) x =
                    adds.Value <- adds.Value + 1
                    Moments.add compensated a x

                let remove (a: Moments) x =
                    removes.Value <- removes.Value + 1
                    Moments.remove compensated a x

                let mutable w = TimeWindow.create 50.0 add remove Moments.zero

                for i in 0 .. pushes - 1 do
                    w <- TimeWindow.push w (float i) (1e9 + float (i % 7))

                Expect.equal (TimeWindow.count w) 51 "t - span <= ti keeps 51 items"
                Expect.isLessThanOrEqual adds.Value (2 * pushes) "adds"
                Expect.isLessThanOrEqual removes.Value pushes "removes"
                let fresh = Array.fold (Moments.add compensated) Moments.zero (TimeWindow.items w)
                expectClose 1e-12 0.0 (Moments.mean fresh) (Moments.mean (TimeWindow.value w)) "mean"
                expectClose 1e-8 0.0 (Moments.variance fresh) (Moments.variance (TimeWindow.value w)) "variance"
            }

            test "a time window boundary, zero span and infinite span" {
                // 0.3 - 0.1 rounds to 0.19999999999999998, so the item at 0.2 is kept (0.2 >= the rounded cutoff).
                let r = timed 0.1 [ (0.2, 1.0); (0.3, 2.0) ]
                Expect.equal (TimeWindow.items r) [| 1.0; 2.0 |] "rounded cutoff keeps the item"
                let z = timed 0.0 [ (1.0, 1.0); (1.0, 2.0) ]
                Expect.equal (TimeWindow.items z) [| 1.0; 2.0 |] "span 0 keeps equal times"
                Expect.equal (TimeWindow.items (TimeWindow.push z 2.0 3.0)) [| 3.0 |] "span 0 drops earlier times"
                let i = timed infinity [ (-1e300, 1.0); (0.0, 2.0); (1e300, 3.0) ]
                Expect.equal (TimeWindow.items i) [| 1.0; 2.0; 3.0 |] "infinite span keeps everything"
            }

            test "decayed moments match a direct weighted fold" {
                // Direct fold: with the final time T, item i has weight 2^(-(T - t_i) / h); mean = sum w x / sum w,
                // variance = sum w (x - mean)^2 / sum w.
                let h = 3.0
                let mutable state = 12345
                let mutable t = 0.0
                let mutable s = DecayedMoments.create h
                let times = ResizeArray<float>()
                let xs = ResizeArray<float>()

                for _ in 1..300 do
                    state <- (state * 75 + 74) % 65537
                    t <- t + 0.05 + float (state % 100) / 40.0
                    state <- (state * 75 + 74) % 65537
                    let x = float (state % 1000) / 10.0
                    times.Add t
                    xs.Add x
                    s <- DecayedMoments.addAt s t x

                let w = times |> Seq.map (fun ti -> Math.Pow(2.0, -((t - ti) / h))) |> Seq.toArray
                let sw = Array.sum w
                let m = Seq.fold2 (fun acc wi x -> acc + wi * x) 0.0 w xs / sw
                let v = Seq.fold2 (fun acc wi x -> acc + wi * (x - m) * (x - m)) 0.0 w xs / sw
                expectClose 1e-13 0.0 sw (DecayedMoments.weight s) "weight"
                expectClose 1e-13 0.0 m (DecayedMoments.mean s) "mean"
                expectClose 1e-13 0.0 v (DecayedMoments.variance s) "variance"
            }
        ]
