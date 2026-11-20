module Ranvier.Science.Tests.HigherMomentsTests

open System
open Expecto
open Ranvier.Numerics

let private ofValues c (xs: float[]) =
    Array.fold (HigherMoments.add c) HigherMoments.zero xs

let private rel (a: float) (b: float) = abs (a - b) / abs b
let private xs1000 = Array.init 1000 (fun k -> sqrt (float k) + 0.1)

let private sameHigher (actual: HigherMoments) (expected: HigherMoments) =
    Expect.equal (HigherMoments.count actual) (HigherMoments.count expected) "counts equal"
    Expect.isTrue (rel (HigherMoments.mean actual) (HigherMoments.mean expected) <= 1e-12) "mean within 1e-12"
    Expect.isTrue (rel (HigherMoments.variance actual) (HigherMoments.variance expected) <= 1e-9) "variance within 1e-9"
    Expect.isTrue (abs (HigherMoments.skewness actual - HigherMoments.skewness expected) <= 1e-8) "skewness within 1e-8"
    Expect.isTrue (abs (HigherMoments.kurtosis actual - HigherMoments.kurtosis expected) <= 1e-8) "kurtosis within 1e-8"

[<Tests>]
let tests =
    testList
        "HigherMoments"
        [
            test "higher moments closed form" {
                // Closed forms for 1, 2, 3, 4, 10: mean 4; M2 = 50, M3 = 180, M4 = 1394 over n = 5.
                let h = ofValues true [| 1.0; 2.0; 3.0; 4.0; 10.0 |]
                Expect.equal (HigherMoments.count h) 5.0 "count"
                expectClose 1e-14 1e-14 4.0 (HigherMoments.mean h) "mean"
                expectClose 1e-14 1e-14 10.0 (HigherMoments.variance h) "variance"
                expectClose 1e-14 1e-14 12.5 (HigherMoments.sampleVariance h) "sampleVariance"
                expectClose 1e-14 1e-14 (sqrt 5.0 * 180.0 / (50.0 * sqrt 50.0)) (HigherMoments.skewness h) "skewness"
                expectClose 1e-14 1e-14 (5.0 * 1394.0 / (50.0 * 50.0) - 3.0) (HigherMoments.kurtosis h) "kurtosis"
                expectClose 1e-14 1e-14 -0.212 (HigherMoments.kurtosis h) "kurtosis literal"
            }

            test "a large offset keeps the shape" {
                let data = [| 1.0; 2.0; 3.0; 4.0; 10.0 |]
                let plain = ofValues true data
                let shifted = ofValues true (Array.map (fun x -> x + 1e6) data)
                Expect.isTrue (rel (HigherMoments.skewness shifted) (HigherMoments.skewness plain) <= 1e-8) "skewness"
                Expect.isTrue (rel (HigherMoments.kurtosis shifted) (HigherMoments.kurtosis plain) <= 1e-8) "kurtosis"
            }

            test "higher moments merge, unmerge and remove" {
                let fold lo hi = ofValues true xs1000.[lo .. hi - 1]
                let a = fold 0 500
                let b = fold 500 1000
                let whole = fold 0 1000
                sameHigher (HigherMoments.merge true a b) whole
                sameHigher (HigherMoments.unmerge true (HigherMoments.merge true a b) b) a

                let mutable m = whole

                for k in 999 .. -1 .. 500 do
                    m <- HigherMoments.remove true m xs1000.[k]

                sameHigher m a
            }

            test "toMoments matches Moments" {
                let h = ofValues true xs1000
                let m = Array.fold (Moments.add true) Moments.zero xs1000
                let t = HigherMoments.toMoments h
                Expect.equal (Moments.count t) (Moments.count m) "counts equal"
                Expect.isTrue (rel (Moments.mean t) (Moments.mean m) <= 1e-15) "mean within 1e-15"
                Expect.isTrue (rel (Moments.variance t) (Moments.variance m) <= 1e-14) "variance within 1e-14"
            }

            test "the shape survives data whose M2 squared overflows, and the same data scaled down" {
                // Identity: skewness and excess kurtosis are invariant under positive scaling. The scale makes M4 near
                // 1e306 while M2 * M2 is infinite, so n * M4 / (M2 * M2) reads -3 and M2 * sqrt M2 is wrong.
                let unit = ofValues true xs1000
                // M4 of the unit data is (kurtosis + 3) * M2^2 / n by the definition of the kurtosis; the scale s gives s^4 * M4 = 3e306.
                let m2Unit = HigherMoments.variance unit * 1000.0
                let m4Unit = (HigherMoments.kurtosis unit + 3.0) * m2Unit * m2Unit / 1000.0
                let scale = Math.Pow(3e306 / m4Unit, 0.25)
                let huge = Array.map ((*) scale) xs1000
                let m2 = HigherMoments.variance (ofValues true huge) * 1000.0
                Expect.isTrue (Double.IsInfinity(m2 * m2)) "the precondition: M2 squared overflows"
                Expect.isFalse (Double.IsInfinity(m2 * sqrt m2)) "M2 times its root is finite"

                for (name, data) in [ "large", huge; "small", Array.map ((*) 1e-70) huge ] do
                    let s = ofValues true data
                    expectClose 1e-12 0.0 (HigherMoments.skewness unit) (HigherMoments.skewness s) (name + " skewness")
                    expectClose 1e-12 0.0 (HigherMoments.kurtosis unit) (HigherMoments.kurtosis s) (name + " kurtosis")
            }

            test "empty, single, constant and NaN" {
                let z = HigherMoments.zero
                Expect.equal (HigherMoments.count z) 0.0 "count"

                for (name, v) in
                    [ "mean", HigherMoments.mean z
                      "variance", HigherMoments.variance z
                      "sampleVariance", HigherMoments.sampleVariance z
                      "skewness", HigherMoments.skewness z
                      "kurtosis", HigherMoments.kurtosis z ] do
                    Expect.isTrue (Double.IsNaN v) (name + " is NaN on empty")

                let one = HigherMoments.add true z 3.0
                Expect.isTrue (Double.IsNaN(HigherMoments.sampleVariance one)) "sampleVariance of one value"
                Expect.isTrue (Double.IsNaN(HigherMoments.skewness one)) "skewness of one value"
                let constant = ofValues true [| 2.0; 2.0; 2.0 |]
                Expect.isTrue (Double.IsNaN(HigherMoments.kurtosis constant)) "kurtosis of a constant"
                let bad = HigherMoments.add true one nan
                Expect.isTrue (Double.IsNaN(HigherMoments.mean bad)) "mean with a NaN"
                Expect.isTrue (Double.IsNaN(HigherMoments.kurtosis bad)) "kurtosis with a NaN"
            }

            test "removal from empty raises, and removing everything returns zero" {
                let one = HigherMoments.add true HigherMoments.zero 1.0
                Expect.throwsT<InvalidOperationException> (fun () -> HigherMoments.remove true HigherMoments.zero 1.0 |> ignore) "remove"
                Expect.throwsT<InvalidOperationException> (fun () -> HigherMoments.unmerge true HigherMoments.zero one |> ignore) "unmerge"
                let values = [| 1.0; 3.5; 0.25 |]
                let back = values |> Array.fold (HigherMoments.remove true) (ofValues true values)
                Expect.equal back HigherMoments.zero "zero"
                Expect.equal (HigherMoments.merge true HigherMoments.zero one) one "merge with empty"
                Expect.equal (HigherMoments.unmerge true one HigherMoments.zero) one "unmerge of empty"
            }
        ]
