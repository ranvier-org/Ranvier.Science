module Ranvier.Science.Tests.WeightedMomentsTests

open System
open Expecto
open Ranvier.Numerics

let private ofPairs c (xs: float[]) (ws: float[]) =
    Array.fold2 (fun m x w -> WeightedMoments.add c m x w) WeightedMoments.zero xs ws

let private rel (a: float) (b: float) = abs (a - b) / abs b
let private xs1000 = Array.init 1000 (fun k -> sqrt (float k) + 0.1)
let private ws1000 = Array.init 1000 (fun k -> 0.5 + float (k % 7))

let private sameWeighted (actual: WeightedMoments) (expected: WeightedMoments) =
    Expect.equal (WeightedMoments.count actual) (WeightedMoments.count expected) "counts equal"
    Expect.isTrue (rel (WeightedMoments.totalWeight actual) (WeightedMoments.totalWeight expected) <= 1e-12) "weight"
    Expect.isTrue (rel (WeightedMoments.mean actual) (WeightedMoments.mean expected) <= 1e-12) "mean within 1e-12"
    Expect.isTrue (rel (WeightedMoments.variance actual) (WeightedMoments.variance expected) <= 1e-9) "variance within 1e-9"
    Expect.isTrue (rel (WeightedMoments.effectiveCount actual) (WeightedMoments.effectiveCount expected) <= 1e-9) "effectiveCount"

[<Tests>]
let tests =
    testList
        "WeightedMoments"
        [
            test "weighted closed form" {
                // Closed forms: W = 4, sum of w^2 = 6, mean = (1 + 4 + 4) / 4 = 2.25, S = sum of w (x - mean)^2 = 1.5625 + 0.125 + 3.0625 = 4.75.
                let w = ofPairs true [| 1.0; 2.0; 4.0 |] [| 1.0; 2.0; 1.0 |]
                Expect.equal (WeightedMoments.count w) 3.0 "count"
                expectClose 1e-15 1e-15 4.0 (WeightedMoments.totalWeight w) "totalWeight"
                expectClose 1e-15 1e-15 2.25 (WeightedMoments.mean w) "mean"
                expectClose 1e-15 1e-15 1.1875 (WeightedMoments.variance w) "variance"
                expectClose 1e-15 1e-15 (4.75 / 3.0) (WeightedMoments.frequencyVariance w) "frequencyVariance"
                expectClose 1e-15 1e-15 1.9 (WeightedMoments.reliabilityVariance w) "reliabilityVariance"
                expectClose 1e-15 1e-15 (16.0 / 6.0) (WeightedMoments.effectiveCount w) "effectiveCount"
            }

            test "unit weights are plain moments" {
                let w = ofPairs true xs1000 (Array.create 1000 1.0)
                let m = Array.fold (Moments.add true) Moments.zero xs1000
                Expect.isTrue (rel (WeightedMoments.variance w) (Moments.variance m) <= 1e-12) "variance"
                Expect.isTrue (rel (WeightedMoments.frequencyVariance w) (Moments.sampleVariance m) <= 1e-12) "frequencyVariance"
            }

            test "zero, negative and NaN weights" {
                let w = ofPairs true [| 1.0; 3.0 |] [| 1.0; 1.0 |]
                let z = WeightedMoments.add true w 100.0 0.0
                Expect.equal (WeightedMoments.count z) 3.0 "count rises"
                Expect.equal (WeightedMoments.totalWeight z) (WeightedMoments.totalWeight w) "weight unchanged"
                Expect.equal (WeightedMoments.mean z) (WeightedMoments.mean w) "mean unchanged"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> WeightedMoments.add true w 1.0 -1.0 |> ignore) "negative add"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> WeightedMoments.remove true w 1.0 -1.0 |> ignore) "negative remove"
                Expect.isTrue (Double.IsNaN(WeightedMoments.mean (WeightedMoments.add true w 5.0 nan))) "NaN weight"
                Expect.equal (WeightedMoments.remove true z 100.0 0.0) w "removing the zero-weight observation restores the state"
            }

            test "weighted merge, unmerge and remove" {
                let fold lo hi = ofPairs true xs1000.[lo .. hi - 1] ws1000.[lo .. hi - 1]
                let a = fold 0 500
                let b = fold 500 1000
                let whole = fold 0 1000
                sameWeighted (WeightedMoments.merge true a b) whole
                sameWeighted (WeightedMoments.unmerge true (WeightedMoments.merge true a b) b) a

                let mutable m = whole

                for k in 999 .. -1 .. 500 do
                    m <- WeightedMoments.remove true m xs1000.[k] ws1000.[k]

                sameWeighted m a
            }

            test "empty and removal from empty" {
                let z = WeightedMoments.zero
                Expect.equal (WeightedMoments.count z) 0.0 "count"

                for (name, v) in
                    [ "totalWeight", WeightedMoments.totalWeight z
                      "mean", WeightedMoments.mean z
                      "variance", WeightedMoments.variance z
                      "frequencyVariance", WeightedMoments.frequencyVariance z
                      "reliabilityVariance", WeightedMoments.reliabilityVariance z
                      "effectiveCount", WeightedMoments.effectiveCount z ] do
                    Expect.isTrue (Double.IsNaN v) (name + " is NaN on empty")

                let one = WeightedMoments.add true z 1.0 2.0
                Expect.throwsT<InvalidOperationException> (fun () -> WeightedMoments.remove true z 1.0 1.0 |> ignore) "remove"
                Expect.throwsT<InvalidOperationException> (fun () -> WeightedMoments.unmerge true z one |> ignore) "unmerge"
                Expect.equal (WeightedMoments.remove true one 1.0 2.0) z "removing the last observation"
                Expect.equal (WeightedMoments.merge true z one) one "merge with empty"
                Expect.equal (WeightedMoments.unmerge true one z) one "unmerge of empty"
            }

            test "a single observation has no sample spread" {
                let one = WeightedMoments.add true WeightedMoments.zero 3.0 0.7
                Expect.equal (WeightedMoments.variance one) 0.0 "variance"
                Expect.isTrue (Double.IsNaN(WeightedMoments.frequencyVariance one)) "frequencyVariance"
                Expect.isTrue (Double.IsNaN(WeightedMoments.reliabilityVariance one)) "reliabilityVariance"
            }
        ]
