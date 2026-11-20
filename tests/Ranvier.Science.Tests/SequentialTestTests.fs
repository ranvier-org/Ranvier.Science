// Reference values: the closed form of the normal mixture likelihood ratio (Robbins; spec §7.5), written in
// `logRatio` below, and the duality with ConfidenceSequence.interval, which the tests compute independently of the
// SequentialTest code.
module Ranvier.Science.Tests.SequentialTestTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private s = ConfidenceSequence.tunedFor 0.05 1000.0 ConfidenceSequence.standardBurnIn

let private fold (xs: float[]) =
    Array.fold (SequentialTest.add true) (SequentialTest.create s 0.0) xs

let private relative (a: float) (b: float) : float =
    if a = b then 0.0 else abs (a - b) / max (abs a) (abs b)

// ln Λ_u = −½ ln(uρ² + 1) + ρ²(u(x̄_u − θ₀))² / (2σ²_u(uρ² + 1)), with x̄_u and σ²_u of the prefix moments.
let private logRatio (settings: SequenceSettings) (theta0: float) (m: Moments) : float =
    let u = Moments.count m
    let rho2 = settings.Rho * settings.Rho
    let spread = u * rho2 + 1.0
    let d = u * (Moments.mean m - theta0)
    -0.5 * log spread + rho2 * d * d / (2.0 * Moments.variance m * spread)

let private stream (mean: float) (count: int) (seed: int) : float[] =
    let struct (xs, _) = Generator.normals mean 1.0 count (Generator.create seed)
    xs

[<Tests>]
let tests =
    testList
        "SequentialTest"
        [
            testCase "the p-value is 1 before the burn-in" (fun () ->
                let mutable t = SequentialTest.create s 0.0

                for x in stream 1.0 29 1 do
                    t <- SequentialTest.add true t x
                    Expect.equal (SequentialTest.pValue t) 1.0 "p-value before the burn-in"
                    Expect.isFalse (SequentialTest.rejects t) "no rejection before the burn-in"

                Expect.equal (Moments.count (SequentialTest.moments t)) 29.0 "29 observations")

            testCase "the p-value is the running minimum of the inverse mixture likelihood ratio" (fun () ->
                let xs = stream 0.2 2000 5
                let mutable t = SequentialTest.create s 0.0
                let mutable m = Moments.zero
                let mutable best = 1.0
                let mutable worst = 0.0

                for x in xs do
                    t <- SequentialTest.add true t x
                    m <- Moments.add true m x

                    if Moments.count m >= 30.0 then
                        best <- min best (exp (-(logRatio s 0.0 m)))

                    worst <- max worst (relative (SequentialTest.pValue t) best)

                Expect.isLessThan worst 1e-12 "largest relative difference over 2000 prefixes"
                Expect.isLessThan best 1.0 "the stream carries evidence, so the minimum fell below 1")

            testCase "the p-value never increases after the burn-in" (fun () ->
                let mutable t = SequentialTest.create s 0.0
                let mutable previous = 1.0

                for x in stream 0.2 2000 5 do
                    t <- SequentialTest.add true t x
                    Expect.isLessThanOrEqual (SequentialTest.pValue t) previous "non-increasing"
                    previous <- SequentialTest.pValue t)

            testCase "rejects exactly when an interval from the burn-in on excluded theta0" (fun () ->
                let mutable rejectingPoints = 0
                let mutable holdingPoints = 0
                let mutable mismatches = 0

                for seed in 1..50 do
                    let mutable t = SequentialTest.create s 0.0
                    let mutable m = Moments.zero
                    let mutable excluded = false

                    for x in stream 0.15 3000 seed do
                        t <- SequentialTest.add true t x
                        m <- Moments.add true m x

                        if Moments.count m >= 30.0 then
                            let i = ConfidenceSequence.interval s m
                            excluded <- excluded || 0.0 < i.Lower || 0.0 > i.Upper

                        if SequentialTest.rejects t <> excluded then
                            mismatches <- mismatches + 1

                        if excluded then
                            rejectingPoints <- rejectingPoints + 1
                        else
                            holdingPoints <- holdingPoints + 1

                Expect.equal mismatches 0 "points where rejects and the interval disagree"
                Expect.isGreaterThan rejectingPoints 0 "some points reject, so the equality is not vacuous"
                Expect.isGreaterThan holdingPoints 0 "some points do not reject")

            testCase "a NaN observation gives a NaN p-value and no rejection" (fun () ->
                for position in [| 5; 100 |] do
                    let xs = stream 1.0 200 3
                    xs[position] <- nan
                    let mutable t = SequentialTest.create s 0.0

                    for i in 0 .. xs.Length - 1 do
                        t <- SequentialTest.add true t xs[i]

                        if i >= position then
                            Expect.isTrue (Double.IsNaN (SequentialTest.pValue t)) "NaN from the observation on"
                            Expect.isFalse (SequentialTest.rejects t) "NaN does not reject")

            testCase "an infinite observation gives a NaN p-value" (fun () ->
                for bad in [| infinity; -infinity |] do
                    let t = SequentialTest.add true (fold (stream 1.0 60 2)) bad
                    Expect.isTrue (Double.IsNaN (SequentialTest.pValue t)) "NaN"
                    Expect.isFalse (SequentialTest.rejects t) "no rejection")

            testCase "zero spread at theta0 keeps the p-value at 1" (fun () ->
                let t = fold (Array.create 40 0.0)
                Expect.equal (SequentialTest.pValue t) 1.0 "p-value"
                Expect.isFalse (SequentialTest.rejects t) "no rejection")

            testCase "zero spread away from theta0 rejects with p-value 0" (fun () ->
                let t = fold (Array.create 40 1.0)
                Expect.equal (SequentialTest.pValue t) 0.0 "p-value"
                Expect.isTrue (SequentialTest.rejects t) "rejection")

            testCase "a huge deviation never gives a positive p-value" (fun () ->
                let t = fold (Array.init 60 (fun i -> 1e300 + float (i % 2)))
                let p = SequentialTest.pValue t
                Expect.isFalse (p > 0.0) "0 or NaN, never a confident positive number")

            testCase "the burn-in boundary: the 30th observation is the first that counts" (fun () ->
                let xs = Array.create 30 5.0
                xs[0] <- 4.0
                let before = fold xs[0..28]
                let after = fold xs
                Expect.equal (SequentialTest.pValue before) 1.0 "count 29"
                Expect.isLessThan (SequentialTest.pValue after) 1.0 "count 30")

            testCase "moments are the folded observations" (fun () ->
                let xs = stream 0.3 500 7
                let m = SequentialTest.moments (fold xs)
                let expected = Array.fold (Moments.add true) Moments.zero xs
                Expect.equal (Moments.count m) (Moments.count expected) "count"
                Expect.equal (Moments.mean m) (Moments.mean expected) "mean"
                Expect.equal (Moments.variance m) (Moments.variance expected) "variance")

            testCase "equal histories give equal tests" (fun () ->
                let xs = stream 0.3 100 7
                Expect.isTrue (fold xs = fold xs) "equal"
                Expect.isFalse (fold xs = fold (Array.append xs [| 0.0 |])) "different histories differ")

            testCase "create rejects invalid settings and theta0" (fun () ->
                let bad = [| { s with Alpha = 0.0 }; { s with Rho = -1.0 }; { s with BurnIn = 0.5 } |]

                for b in bad do
                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> SequentialTest.create b 0.0 |> ignore)
                        ("settings " + string b.Alpha + " " + string b.Rho + " " + string b.BurnIn)

                for theta0 in [| nan; infinity; -infinity |] do
                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> SequentialTest.create s theta0 |> ignore)
                        ("theta0 " + string theta0))
        ]
