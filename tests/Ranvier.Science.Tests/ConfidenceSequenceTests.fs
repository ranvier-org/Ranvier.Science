module Ranvier.Science.Tests.ConfidenceSequenceTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private excludes (x: float) (i: Interval) = x < i.Lower || x > i.Upper

/// True when each of the two intervals misses 0 at some t >= 30 of a stream of `length` standard normal deviates.
let private misses (alpha: float) (rho: float) (length: int) (g0: Generator) : struct (bool * bool) =
    let settings = ConfidenceSequence.settings alpha rho ConfidenceSequence.standardBurnIn
    let mutable g = g0
    let mutable m = Moments.zero
    let mutable sequenceMissed = false
    let mutable fixedMissed = false
    let mutable t = 0

    while t < length && not (sequenceMissed && fixedMissed) do
        let struct (z, next) = Generator.normal g
        g <- next
        m <- Moments.add true m z
        t <- t + 1

        if t >= 30 then
            if not sequenceMissed && excludes 0.0 (ConfidenceSequence.interval settings m) then
                sequenceMissed <- true

            if not fixedMissed && excludes 0.0 (FixedSample.ofMoments alpha m) then
                fixedMissed <- true

    struct (sequenceMissed, fixedMissed)

[<Tests>]
let tests =
    testList
        "ConfidenceSequence"
        [
            test "radius at alpha 0.05, rho 1, t 100, sd 1" {
                let r = ConfidenceSequence.radius 0.05 1.0 100.0 1.0
                Expect.isLessThanOrEqual (abs (r - 0.3273019)) 1e-6 "radius within 1e-6 of 0.3273019"
            }

            test "radius scales with sd" {
                let a, r, t = 0.05, 0.3, 250.0
                let one = ConfidenceSequence.radius a r t 1.0
                let three = ConfidenceSequence.radius a r t 3.0
                Expect.isTrue (abs (three - 3.0 * one) <= 1e-15 * abs (3.0 * one)) "radius at sd 3 is three times radius at sd 1"
            }

            test "rhoTightestAt is a minimum at n" {
                let alpha, n = 0.05, 1000.0
                let r = ConfidenceSequence.rhoTightestAt alpha n
                let atR = ConfidenceSequence.radius alpha r n 1.0

                for k in [ 0.5; 0.9; 1.1; 2.0 ] do
                    Expect.isLessThanOrEqual atR (ConfidenceSequence.radius alpha (r * k) n 1.0) (sprintf "k = %g" k)
            }

            test "normal quantile" {
                let upper = NormalQuantile.ofProbability 0.975
                Expect.isLessThanOrEqual (abs (upper - 1.959963985)) 1e-8 "0.975 within 1e-8 of 1.959963985"
                Expect.isLessThanOrEqual (abs (NormalQuantile.ofProbability 0.5)) 1e-12 "0.5 within 1e-12 of 0"

                Expect.isLessThanOrEqual
                    (abs (NormalQuantile.ofProbability 0.025 + upper))
                    1e-9
                    "0.025 within 1e-9 of the negated 0.975 quantile"

                Expect.isLessThanOrEqual (abs (NormalQuantile.ofProbability 0.999 - 3.090232306)) 1e-8 "0.999 within 1e-8 of 3.090232306"
                Expect.isLessThanOrEqual (abs (NormalQuantile.ofProbability 1e-10 + 6.361340902)) 1e-8 "1e-10 within 1e-8 of -6.361340902"
            }

            test "normal quantile relative error is within 1.2e-9 against Continuous.quantile" {
                // Pins the bound stated on NormalQuantile.ofProbability. The reference is Continuous.quantile of the standard
                // normal, itself pinned to 1e-12 by ContinuousTests "normal quantile against the reference"; Acklam's
                // published bound is 1.15e-9 (general knowledge). p = 0.5 is left out: its quantile is 0, which has no
                // relative error.
                let d = Continuous.normal 0.0 1.0
                let thousandths = [ for k in 1..999 -> float k / 1000.0 ]
                let tails = [ for j in 3..300 -> pown 10.0 (-j) ]
                let nearOne = [ for j in 3..15 -> 1.0 - pown 10.0 (-j) ]
                let switches = [ 0.02424; 0.02425; 0.02426; 0.97574; 0.97575; 0.97576 ]

                for p in (thousandths @ tails @ nearOne @ switches) |> List.filter (fun p -> p <> 0.5) do
                    expectClose 1.2e-9 0.0 (Continuous.quantile d p) (NormalQuantile.ofProbability p) (sprintf "p = %.17g" p)
            }

            test "the fixed-sample interval accepts a very small alpha" {
                let m = Array.fold (Moments.add true) Moments.zero [| 1.0; 2.0; 4.0 |]
                let f = FixedSample.ofMoments 1e-17 m
                let finite (x: float) = not (Double.IsNaN x || Double.IsInfinity x)
                Expect.isTrue (finite f.Lower && finite f.Upper) "finite bounds"
            }

            // Mean 2, population standard deviation 1, count 2.
            test "the fixed-sample half-width at alpha 0.0125" {
                let m = Array.fold (Moments.add true) Moments.zero [| 1.0; 3.0 |]
                let f = FixedSample.ofMoments 0.0125 m
                let expected = 2.497705474 * 1.0 / sqrt 2.0
                let half = (f.Upper - f.Lower) / 2.0
                Expect.isLessThanOrEqual (abs (half - expected) / expected) 1e-8 "half-width within relative 1e-8"
            }

            // Seed 1.
            test "the sequence at one observation excludes every other value" {
                let struct (z, _) = Generator.normal (Generator.create 1)

                for x in [ z; 3.5; -0.25 ] do
                    let s = ConfidenceSequence.ofMoments 0.05 1.0 (Moments.add true Moments.zero x)
                    Expect.equal (s.Lower, s.Upper) (x, x) (sprintf "[x, x] at x = %g" x)
                    Expect.isTrue (excludes (x + 1e-9) s && excludes (x - 1e-9) s) (sprintf "excludes neighbours of %g" x)

                Expect.notEqual z 0.0 "the deviate differs from the mean 0"
                Expect.isTrue (excludes 0.0 (ConfidenceSequence.ofMoments 0.05 1.0 (Moments.add true Moments.zero z))) "excludes the mean 0"
            }

            test "an empty sample has NaN bounds" {
                let s = ConfidenceSequence.ofMoments 0.05 1.0 Moments.zero
                let f = FixedSample.ofMoments 0.05 Moments.zero
                Expect.isTrue (Double.IsNaN s.Lower && Double.IsNaN s.Upper) "sequence bounds are NaN"
                Expect.isTrue (Double.IsNaN f.Lower && Double.IsNaN f.Upper) "fixed-sample bounds are NaN"
            }

            test "one observation gives a zero-width interval" {
                let m = Array.fold (Moments.add true) Moments.zero [| 3.5 |]
                let s = ConfidenceSequence.ofMoments 0.05 1.0 m
                let f = FixedSample.ofMoments 0.05 m
                Expect.equal (s.Lower, s.Upper) (3.5, 3.5) "sequence"
                Expect.equal (f.Lower, f.Upper) (3.5, 3.5) "fixed sample"
            }

            test "arguments out of range are rejected" {
                let m = Array.fold (Moments.add true) Moments.zero [| 1.0; 2.0 |]

                for alpha in [ 0.0; 1.0; nan ] do
                    let label = sprintf "alpha %g" alpha
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.radius alpha 1.0 10.0 1.0 |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.ofMoments alpha 1.0 m |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.rhoTightestAt alpha 100.0 |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> FixedSample.ofMoments alpha m |> ignore) label

                for rho in [ 0.0; -1.0; infinity; nan ] do
                    let label = sprintf "rho %g" rho
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.radius 0.05 rho 10.0 1.0 |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.ofMoments 0.05 rho m |> ignore) label

                for n in [ 0.0; infinity; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.rhoTightestAt 0.05 n |> ignore) (sprintf "n %g" n)

                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.radius 0.05 1.0 10.0 -1.0 |> ignore) "sd -1"
            }

            test "radius with a NaN sd is NaN" {
                Expect.isTrue (Double.IsNaN (ConfidenceSequence.radius 0.05 1.0 10.0 nan)) "radius"
            }

            test "a sample of one reached by removal gives a zero-width interval" {
                let xs = Array.init 1000 (fun k -> sqrt (float k) + 0.1)
                let mutable m = Array.fold (Moments.add true) Moments.zero xs

                for k in 0..998 do
                    m <- Moments.remove true m xs[k]

                let s = ConfidenceSequence.ofMoments 0.05 1.0 m
                Expect.equal s.Lower s.Upper "zero width"
                Expect.equal s.Lower (Moments.mean m) "at the mean"
            }

            test "no interval before the burn-in" {
                let alpha, rho = 0.05, 0.3
                let xs = Array.init 30 (fun k -> sin (float k) * 2.0 + 1.0)
                let before = Array.fold (Moments.add true) Moments.zero xs[0..28]
                let at = Moments.add true before xs[29]
                let s = ConfidenceSequence.settings alpha rho 30.0
                let early = ConfidenceSequence.interval s before
                Expect.isTrue (Double.IsNaN early.Lower && Double.IsNaN early.Upper) "29 observations: NaN bounds"
                let reached = ConfidenceSequence.interval s at
                let plain = ConfidenceSequence.ofMoments alpha rho at
                Expect.equal reached.Lower plain.Lower "30 observations: lower bound"
                Expect.equal reached.Upper plain.Upper "30 observations: upper bound"
            }

            test "a burn-in out of range is rejected" {
                let m = Array.fold (Moments.add true) Moments.zero [| 1.0; 2.0 |]

                for burnIn in [ 0.5; nan; infinity ] do
                    let label = sprintf "burnIn %g" burnIn
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.settings 0.05 1.0 burnIn |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.tunedFor 0.05 1000.0 burnIn |> ignore) label

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> ConfidenceSequence.interval { Alpha = 0.05; Rho = 1.0; BurnIn = 0.5 } m |> ignore)
                    "interval on a record with burnIn 0.5"
            }

            test "settings and tunedFor" {
                Expect.equal (ConfidenceSequence.settings 0.05 0.3 30.0) { Alpha = 0.05; Rho = 0.3; BurnIn = 30.0 } "settings"
                Expect.equal (ConfidenceSequence.tunedFor 0.05 1000.0 30.0).Rho (ConfidenceSequence.rhoTightestAt 0.05 1000.0) "tunedFor rho"
                Expect.equal (ConfidenceSequence.tunedFor 0.05 1000.0 30.0).BurnIn 30.0 "tunedFor burn-in"
                Expect.equal ConfidenceSequence.standardBurnIn 30.0 "standardBurnIn"
            }

            test "settings out of range are rejected" {
                let m = Array.fold (Moments.add true) Moments.zero [| 1.0; 2.0 |]

                for alpha in [ 0.0; 1.0; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.settings alpha 1.0 30.0 |> ignore) (sprintf "settings alpha %g" alpha)

                for rho in [ 0.0; -1.0; infinity; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.settings 0.05 rho 30.0 |> ignore) (sprintf "settings rho %g" rho)

                for n in [ 0.0; infinity; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.tunedFor 0.05 n 30.0 |> ignore) (sprintf "tunedFor plannedCount %g" n)

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> ConfidenceSequence.interval { Alpha = 1.0; Rho = 1.0; BurnIn = 30.0 } m |> ignore)
                    "interval on a record with alpha 1"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> ConfidenceSequence.interval { Alpha = 0.05; Rho = 0.0; BurnIn = 30.0 } m |> ignore)
                    "interval on a record with rho 0"
            }

            // Seeds 1 to 2,000, 10,000 deviates each.
            test "the sequence covers under continuous monitoring and the fixed-sample interval does not" {
                let alpha = 0.05
                let rho = ConfidenceSequence.rhoTightestAt alpha 1000.0
                let streams = 2000
                let mutable sequenceMisses = 0
                let mutable fixedMisses = 0

                for i in 1..streams do
                    let struct (s, f) = misses alpha rho 10_000 (Generator.create i)
                    if s then sequenceMisses <- sequenceMisses + 1
                    if f then fixedMisses <- fixedMisses + 1

                let sequenceFraction = float sequenceMisses / float streams
                let fixedFraction = float fixedMisses / float streams

                printfn
                    "Coverage under continuous monitoring: confidence sequence misses %g, fixed-sample interval misses %g, of %d streams"
                    sequenceFraction
                    fixedFraction
                    streams

                Expect.isLessThanOrEqual sequenceFraction 0.07 "sequence miss fraction <= 0.07"
                Expect.isTrue (fixedFraction >= 0.15) "fixed-sample miss fraction >= 0.15"
            }

            test "the sequence covers at the page's setting" {
                let settings = ConfidenceSequence.tunedFor (0.05 / 4.0) 1000.0 ConfidenceSequence.standardBurnIn
                let streams = 2000
                let mutable misses = 0

                for i in 1..streams do
                    let mutable g = Generator.create i
                    let mutable m = Moments.zero
                    let mutable missed = false
                    let mutable t = 0

                    while t < 10_000 && not missed do
                        let struct (z, next) = Generator.normal g
                        g <- next
                        m <- Moments.add true m z
                        t <- t + 1

                        if excludes 0.0 (ConfidenceSequence.interval settings m) then
                            missed <- true

                    if missed then misses <- misses + 1

                let fraction = float misses / float streams
                printfn "Coverage at the page's setting: confidence sequence misses %g of %d streams" fraction streams
                Expect.isLessThanOrEqual fraction 0.0225 "miss fraction <= 0.0225"
            }
        ]
