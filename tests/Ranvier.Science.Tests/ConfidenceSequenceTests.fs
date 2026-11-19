module Ranvier.Science.Tests.ConfidenceSequenceTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private excludes (x: float) (i: Interval) = x < i.Lower || x > i.Upper

/// True when each of the two intervals misses 0 at some t >= 30 of a stream of `length` standard normal deviates.
let private misses (alpha: float) (rho: float) (length: int) (g0: Generator) : struct (bool * bool) =
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
            if not sequenceMissed && excludes 0.0 (ConfidenceSequence.ofMoments alpha rho m) then
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

                for rho in [ 0.0; -1.0 ] do
                    let label = sprintf "rho %g" rho
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.radius 0.05 rho 10.0 1.0 |> ignore) label
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.ofMoments 0.05 rho m |> ignore) label

                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> ConfidenceSequence.rhoTightestAt 0.05 0.0 |> ignore) "n 0"
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
        ]
