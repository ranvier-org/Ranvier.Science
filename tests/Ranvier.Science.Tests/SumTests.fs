module Ranvier.Science.Tests.SumTests

open Expecto
open Ranvier
open Ranvier.Numerics

let private total compensated (xs: float[]) =
    xs |> Array.fold (Sum.add compensated) Sum.zero |> Sum.value

[<Tests>]
let tests =
    testList "Sum" [
        test "1e20, 1, -1e20 sums to 1 compensated and to 0 plain" {
            let xs = [| 1e20; 1.0; -1e20 |]
            Expect.equal (total true xs) 1.0 "compensated"
            Expect.equal (total false xs) 0.0 "plain"
        }
        test "1, 1e100, 1, -1e100 sums to 2" { Expect.equal (total true [| 1.0; 1e100; 1.0; -1e100 |]) 2.0 "" }
        test "ten thousand tenths are within 2 ulp of the exact sum" {
            let xs = Array.create 10_000 0.1
            Expect.isLessThanOrEqual (ulps (total true xs) (exactSum xs)) 2.0 ""
        }
        test "alternating 1e9 with small terms is within 2 ulp of the exact sum" {
            let xs = Array.init 10_000 (fun i -> (if i % 2 = 0 then 1e9 else -1e9) + float i * 1e-3)
            Expect.isLessThanOrEqual (ulps (total true xs) (exactSum xs)) 2.0 ""
        }
        test "removing every other term leaves the exact sum of the rest within 2 ulp" {
            let xs = Array.init 10_000 (fun i -> 1.0 + float i * 1e-3)
            let all = xs |> Array.fold (Sum.add true) Sum.zero
            let rest = xs |> Array.indexed |> Array.filter (fun (i, _) -> i % 2 = 1) |> Array.map snd
            let left = xs |> Array.indexed |> Array.fold (fun s (i, x) -> if i % 2 = 0 then Sum.remove true s x else s) all
            Expect.isLessThanOrEqual (ulps (Sum.value left) (exactSum rest)) 2.0 ""
        }
        test "a NaN term gives NaN and raises nothing" { Expect.isTrue (System.Double.IsNaN (total true [| 1.0; nan; 2.0 |])) "" }
        test "1e308 + 1e308 reads NaN compensated and +infinity plain, and raises nothing" {
            Expect.isTrue (System.Double.IsNaN (total true [| 1e308; 1e308 |])) "compensated"
            Expect.equal (total false [| 1e308; 1e308 |]) infinity "plain"
        }
        test "an item that leaves the sum unchanged notifies no reader of a stream fold" {
            for compensated in [ true; false ] do
                withGraph (fun () ->
                    let feed = ManualStream<float>()
                    let sum = createStreamFold Sum.zero (Sum.add compensated) (fun _ -> feed.Source)
                    let mutable runs = 0

                    createEffect (fun () ->
                        sum.TryValue |> ignore
                        runs <- runs + 1)

                    feed.Push 1.5
                    feed.Push 0.0
                    let label = sprintf "Compensated = %b" compensated
                    Expect.equal runs 2 ("the effect runs at creation and after 1.5, and not after 0.0, with " + label)
                    Expect.equal (Sum.value sum.Peek) 1.5 ("the sum with " + label))
        }
        test "a zero total keeps its sign across an item of the other zero" {
            let negative = { Total = -0.0; Carry = 0.0 }

            for compensated in [ true; false ] do
                Expect.equal (1.0 / (Sum.add compensated negative 0.0).Total) -infinity (sprintf "Compensated = %b" compensated)
        }
    ]
