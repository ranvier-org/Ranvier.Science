module Ranvier.Science.Tests.ComomentsTests

open System
open Expecto
open Ranvier.Numerics

let private ofPairs c (xs: float[]) (ys: float[]) =
    Array.fold2 (fun m x y -> Comoments.add c m x y) Comoments.zero xs ys

let private rel (a: float) (b: float) = abs (a - b) / abs b
let private xs1000 = Array.init 1000 (fun k -> sqrt (float k) + 0.1)
let private ys1000 = Array.init 1000 (fun k -> 0.3 * float k - sqrt (float k + 2.0))

let private sameComoments (actual: Comoments) (expected: Comoments) =
    Expect.equal (Comoments.count actual) (Comoments.count expected) "counts equal"
    Expect.isTrue (rel (Comoments.meanX actual) (Comoments.meanX expected) <= 1e-12) "meanX within 1e-12"
    Expect.isTrue (rel (Comoments.meanY actual) (Comoments.meanY expected) <= 1e-12) "meanY within 1e-12"
    Expect.isTrue (rel (Comoments.varianceX actual) (Comoments.varianceX expected) <= 1e-9) "varianceX within 1e-9"
    Expect.isTrue (rel (Comoments.varianceY actual) (Comoments.varianceY expected) <= 1e-9) "varianceY within 1e-9"
    Expect.isTrue (rel (Comoments.covariance actual) (Comoments.covariance expected) <= 1e-9) "covariance within 1e-9"

[<Tests>]
let tests =
    testList
        "Comoments"
        [
            test "closed-form reads" {
                // Closed forms: means 2.5 and 5; Σ(x − x̄)² = 5, Σ(y − ȳ)² = 26, Σ(x − x̄)(y − ȳ) = 11 over n = 4.
                let c = ofPairs true [| 1.0; 2.0; 3.0; 4.0 |] [| 2.0; 4.0; 5.0; 9.0 |]
                Expect.equal (Comoments.count c) 4.0 "count"
                expectClose 4e-15 4e-15 2.5 (Comoments.meanX c) "meanX"
                expectClose 4e-15 4e-15 5.0 (Comoments.meanY c) "meanY"
                expectClose 4e-15 4e-15 1.25 (Comoments.varianceX c) "varianceX"
                expectClose 4e-15 4e-15 6.5 (Comoments.varianceY c) "varianceY"
                expectClose 4e-15 4e-15 2.75 (Comoments.covariance c) "covariance"
                expectClose 4e-15 4e-15 (11.0 / 3.0) (Comoments.sampleCovariance c) "sampleCovariance"
                expectClose 4e-15 4e-15 (11.0 / sqrt 130.0) (Comoments.correlation c) "correlation"
                expectClose 4e-15 4e-15 2.2 (Comoments.slope c) "slope"
                expectClose 4e-15 4e-15 -0.5 (Comoments.intercept c) "intercept"
            }

            test "merge, unmerge and remove agree with one fold" {
                let fold lo hi =
                    ofPairs true xs1000.[lo .. hi - 1] ys1000.[lo .. hi - 1]

                let a = fold 0 500
                let b = fold 500 1000
                let whole = fold 0 1000
                sameComoments (Comoments.merge true a b) whole
                sameComoments (Comoments.unmerge true (Comoments.merge true a b) b) a

                let mutable m = whole

                for k in 999 .. -1 .. 500 do
                    m <- Comoments.remove true m xs1000.[k] ys1000.[k]

                sameComoments m a
            }

            test "a large offset keeps the covariance" {
                let xs = Array.init 1000 (fun k -> 1e9 + float k)
                let ys = Array.init 1000 (fun k -> 2e9 + 3.0 * float k)
                // Closed form: y = 2e9 + 3 (x − 1e9), so the covariance is 3 · variance of 0..999 = 3 · 83333.25.
                let c = ofPairs true xs ys
                Expect.isTrue (rel (Comoments.covariance c) (3.0 * 83333.25) <= 1e-9) "covariance within relative 1e-9"
            }

            test "empty, single and NaN" {
                let z = Comoments.zero
                Expect.equal (Comoments.count z) 0.0 "count"

                for (name, v) in
                    [ "meanX", Comoments.meanX z
                      "meanY", Comoments.meanY z
                      "varianceX", Comoments.varianceX z
                      "varianceY", Comoments.varianceY z
                      "covariance", Comoments.covariance z
                      "sampleCovariance", Comoments.sampleCovariance z
                      "correlation", Comoments.correlation z
                      "slope", Comoments.slope z
                      "intercept", Comoments.intercept z ] do
                    Expect.isTrue (Double.IsNaN v) (name + " is NaN")

                let one = Comoments.add true z 1.0 2.0
                Expect.equal (Comoments.covariance one) 0.0 "covariance of one pair"
                Expect.isTrue (Double.IsNaN(Comoments.sampleCovariance one)) "sampleCovariance of one pair"
                Expect.isTrue (Double.IsNaN(Comoments.correlation one)) "correlation of one pair"

                let bad = Comoments.add true one nan 3.0
                Expect.isTrue (Double.IsNaN(Comoments.meanX bad)) "meanX with a NaN x"
                Expect.isTrue (Double.IsNaN(Comoments.covariance bad)) "covariance with a NaN x"
            }

            test "removal from empty raises" {
                Expect.throwsT<InvalidOperationException> (fun () -> Comoments.remove true Comoments.zero 1.0 2.0 |> ignore) "remove"

                Expect.throwsT<InvalidOperationException>
                    (fun () -> Comoments.unmerge true Comoments.zero (Comoments.add true Comoments.zero 1.0 2.0) |> ignore)
                    "unmerge"
            }

            test "removing everything returns zero" {
                let pairs = [| 1.0, 2.0; 3.5, -1.0; 0.25, 7.0 |]
                let c = pairs |> Array.fold (fun m (x, y) -> Comoments.add true m x y) Comoments.zero
                let back = pairs |> Array.fold (fun m (x, y) -> Comoments.remove true m x y) c
                Expect.equal back Comoments.zero "zero"
            }

            test "the correlation survives data scaled by 1e100 and 1e-80" {
                // Identity: the correlation is invariant under positive scaling of x and y. The product M2x · M2y
                // overflows near 1e100 and underflows near 1e-80, so the correlation takes the two roots separately.
                let reference = Comoments.correlation (ofPairs true xs1000 ys1000)

                for scale in [ 1e100; 1e-80 ] do
                    let c = ofPairs true (Array.map ((*) scale) xs1000) (Array.map ((*) scale) ys1000)
                    expectClose 1e-14 0.0 reference (Comoments.correlation c) (sprintf "scale %g" scale)
            }
        ]
