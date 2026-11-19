module Ranvier.Science.Tests.MomentsTests

open System
open Expecto
open Ranvier.Numerics

let private ofArray c xs = Array.fold (Moments.add c) Moments.zero xs
let private rel (a: float) (b: float) = abs (a - b) / abs b
let private sample = Array.init 1000 (fun k -> sqrt (float k) + 0.1)

let private sameMoments (actual: Moments) (expected: Moments) =
    Expect.isTrue (rel (Moments.mean actual) (Moments.mean expected) <= 1e-12) "mean within 1e-12"
    Expect.isTrue (rel (Moments.variance actual) (Moments.variance expected) <= 1e-9) "variance within 1e-9"
    Expect.equal (Moments.count actual) (Moments.count expected) "counts equal"

[<Tests>]
let tests =
    testList
        "Moments"
        [
            test "0 to 999 has mean 499.5 and variance 83333.25" {
                let m = ofArray true (Array.init 1000 float)
                Expect.equal (Moments.mean m) 499.5 "mean"
                Expect.equal (Moments.variance m) 83333.25 "variance"
            }

            test "1e9 + k keeps the variance" {
                let m = ofArray true (Array.init 1000 (fun k -> 1e9 + float k))
                Expect.isTrue (rel (Moments.variance m) 83333.25 <= 1e-9) "variance"
                Expect.equal (Moments.mean m) (1e9 + 499.5) "mean"
            }

            test "folding twice gives the same bits" {
                let xs = Array.init 5000 (fun k -> sqrt (float k) + 0.1)
                Expect.isTrue (ofArray true xs = ofArray true xs) "states equal"
            }

            test "merge of two halves matches one fold" {
                let a = ofArray true sample[.. 399]
                let b = ofArray true sample[400..]
                sameMoments (Moments.merge true a b) (ofArray true sample)
            }

            test "unmerge undoes merge" {
                let a = ofArray true sample[.. 399]
                let b = ofArray true sample[400..]
                sameMoments (Moments.unmerge true (Moments.merge true a b) b) a
            }

            test "remove undoes add" {
                let mutable m = ofArray true sample

                for k in 999 .. -1 .. 500 do
                    m <- Moments.remove true m sample[k]

                sameMoments m (ofArray true sample[.. 499])
            }

            test "removing everything returns zero" {
                let xs = sample[.. 99]
                let mutable m = ofArray true xs

                for k in 99 .. -1 .. 0 do
                    m <- Moments.remove true m xs[k]

                Expect.isTrue (m = Moments.zero) "zero"
            }

            test "remove from empty raises" {
                Expect.throwsT<InvalidOperationException> (fun () -> Moments.remove true Moments.zero 1.0 |> ignore) "remove"

                Expect.throwsT<InvalidOperationException>
                    (fun () -> Moments.unmerge true Moments.zero (ofArray true [| 1.0 |]) |> ignore)
                    "unmerge"
            }

            test "an empty sample has NaN mean and variance" {
                Expect.isTrue (Double.IsNaN (Moments.mean Moments.zero)) "mean"
                Expect.isTrue (Double.IsNaN (Moments.variance Moments.zero)) "variance"
            }

            test "one observation has zero variance" {
                let m = ofArray true [| 3.5 |]
                Expect.equal (Moments.variance m) 0.0 "variance"
                Expect.equal (Moments.standardError m) 0.0 "standard error"
                Expect.equal (Moments.mean m) 3.5 "mean"
            }

            test "the count stays exact past 2^31" {
                let mutable m = ofArray true [| 1.0 |]

                for _ in 1..32 do
                    m <- Moments.merge true m m

                Expect.equal (Moments.count m) 4294967296.0 "count"
            }

            test "a NaN observation gives NaN and raises nothing" {
                let m = Moments.add true (ofArray true [| 1.0; 2.0 |]) nan
                Expect.isTrue (Double.IsNaN (Moments.mean m)) "mean"
            }
        ]
