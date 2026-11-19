module Ranvier.Science.Tests.CrossTargetTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private rel (actual: float) (expected: float) = abs (actual - expected) / abs expected

[<Tests>]
let tests =
    testList
        "CrossTarget"
        [
            test "the recorded stream gives pinned numbers" {
                let m = Array.fold (Moments.add true) Moments.zero Recorded.stream
                Expect.equal (Moments.count m) 1000.0 "count"
                Expect.equal (Moments.mean m) 0.9199618814567002 "mean"
                Expect.equal (Moments.variance m) 8.34324586928672 "variance"

                let i = ConfidenceSequence.ofMoments 0.05 (ConfidenceSequence.rhoTightestAt 0.05 1000.0) m
                Expect.isTrue (rel i.Lower 0.642729642202583 <= 1e-12) (sprintf "lower %.17g" i.Lower)
                Expect.isTrue (rel i.Upper 1.1971941207108174 <= 1e-12) (sprintf "upper %.17g" i.Upper)
            }

#if !FABLE_COMPILER
            // .NET only: the stream is the generator's output today. Under JavaScript `log` and `cos`
            // may differ in the last place, so the same comparison there could fail without any defect.
            test "the recorded stream is what the generator gives on .NET" {
                let struct (xs, _) = Generator.normals 0.84 3.0 1000 (Generator.create 20261119)
                Expect.equal xs Recorded.stream "same 1000 doubles"
            }
#endif
        ]
