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

            test "the sample variance divides by n − 1" {
                // Closed form: Σ(k − 499.5)² = n(n² − 1)/12 = 83 333 250 for n = 1000; over n it is 83 333.25.
                let m = ofArray true (Array.init 1000 float)
                let expected = 83333250.0 / 999.0
                Expect.isTrue (rel (Moments.sampleVariance m) expected <= 1e-15) "sample variance within relative 1e-15"

                Expect.isTrue
                    (rel (Moments.sampleStandardDeviation m) (sqrt expected) <= 1e-15)
                    "sample standard deviation within relative 1e-15"

                Expect.equal (Moments.standardDeviation m) (sqrt 83333.25) "standard deviation"
            }

            test "two observations" {
                // The fold's mean is 2 and its M2 is 2, both exact.
                let m = ofArray true [| 1.0; 3.0 |]
                Expect.equal (Moments.standardDeviation m) 1.0 "standard deviation"
                Expect.equal (Moments.sampleVariance m) 2.0 "sample variance"
                Expect.equal (Moments.sampleStandardDeviation m) (sqrt 2.0) "sample standard deviation"
            }

            test "fewer than two observations" {
                Expect.isTrue (Double.IsNaN (Moments.sampleVariance Moments.zero)) "zero: sample variance"
                Expect.isTrue (Double.IsNaN (Moments.standardDeviation Moments.zero)) "zero: standard deviation"
                Expect.isTrue (Double.IsNaN (Moments.sampleStandardDeviation Moments.zero)) "zero: sample standard deviation"
                let one = ofArray true [| 3.5 |]
                Expect.isTrue (Double.IsNaN (Moments.sampleVariance one)) "one: sample variance"
                Expect.isTrue (Double.IsNaN (Moments.sampleStandardDeviation one)) "one: sample standard deviation"
                Expect.equal (Moments.standardDeviation one) 0.0 "one: standard deviation"
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

            test "a sample of one reached by removal has zero spread" {
                let mutable firstInFirstOut = ofArray true sample

                for k in 0..998 do
                    firstInFirstOut <- Moments.remove true firstInFirstOut sample[k]

                let mutable lastInFirstOut = ofArray true sample

                for k in 999 .. -1 .. 1 do
                    lastInFirstOut <- Moments.remove true lastInFirstOut sample[k]

                let unmerged = Moments.unmerge true (ofArray true sample) (ofArray true sample[1..])

                for name, m, kept in
                    [
                        "first-in-first-out", firstInFirstOut, sample[999]
                        "last-in-first-out", lastInFirstOut, sample[0]
                        "unmerge", unmerged, sample[0]
                    ] do
                    Expect.equal (Moments.count m) 1.0 (name + ": count")
                    Expect.equal (Moments.variance m) 0.0 (name + ": variance")
                    Expect.equal (Moments.standardError m) 0.0 (name + ": standard error")
                    Expect.isTrue (rel (Moments.mean m) kept <= 1e-11) (name + ": mean within relative 1e-11 of the remaining observation")
            }

            test "merge and unmerge with an empty side return the other side" {
                let a = ofArray true sample
                let left = Moments.merge true Moments.zero a
                let right = Moments.merge true a Moments.zero
                let unmerged = Moments.unmerge true a Moments.zero
                Expect.equal left a "merge zero a"
                Expect.equal right a "merge a zero"
                Expect.equal unmerged a "unmerge a zero"
#if FABLE_COMPILER
                Expect.isTrue (obj.ReferenceEquals (box left, box a)) "merge zero a is the instance a"
                Expect.isTrue (obj.ReferenceEquals (box right, box a)) "merge a zero is the instance a"
                Expect.isTrue (obj.ReferenceEquals (box unmerged, box a)) "unmerge a zero is the instance a"
#endif
            }

            test "a NaN observation gives NaN and raises nothing" {
                let m = Moments.add true (ofArray true [| 1.0; 2.0 |]) nan
                Expect.isTrue (Double.IsNaN (Moments.mean m)) "mean"
            }
        ]
