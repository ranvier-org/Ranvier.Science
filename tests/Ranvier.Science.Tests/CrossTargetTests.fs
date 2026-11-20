module Ranvier.Science.Tests.CrossTargetTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics
open SequentialMonitor

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

            // Literals: Matrix.solve and CholeskyDecomposition.solve printed with ToString("R") on .NET; JavaScript must match.
            test "LU solve gives the same bits on both targets" {
                let a4 =
                    Matrix.ofRows
                        [| [| 2.0; 1.0; -1.0; 3.0 |]
                           [| 4.0; -6.0; 0.0; 1.0 |]
                           [| -2.0; 7.0; 2.0; 0.0 |]
                           [| 1.0; 0.0; 5.0; -3.0 |] |]

                let x = Matrix.solve a4 [| 0.1; -0.7; 1.3; 2.9 |] |> Result.defaultWith failwith
                Expect.equal x.[0] (3.1758620689655155) "x0"
                Expect.equal x.[1] (1.6793103448275852) "x1"
                Expect.equal x.[2] (-2.0517241379310325) "x2"
                Expect.equal x.[3] (-3.3275862068965494) "x3"
            }

            test "Cholesky solve gives the same bits on both targets" {
                let s4 =
                    Matrix.ofRows
                        [| [| 4.0; 1.0; 0.5; 0.0 |]
                           [| 1.0; 3.0; 0.25; 0.1 |]
                           [| 0.5; 0.25; 2.0; 0.3 |]
                           [| 0.0; 0.1; 0.3; 1.5 |] |]

                let d = Matrix.cholesky s4 |> Result.defaultWith failwith
                let x = CholeskyDecomposition.solve d [| 1.0; 2.0; 3.0; 4.0 |]
                Expect.equal x.[0] (-0.009525739764896651) "x0"
                Expect.equal x.[1] (0.4994730441832184) "x1"
                Expect.equal x.[2] (1.0772598297527363) "x2"
                Expect.equal x.[3] (2.417916497770572) "x3"
            }

#if !FABLE_COMPILER
            // The stream was recorded on Windows. `log` and `cos` come from the platform's math
            // library, so another platform may differ in the last places.
            test "the recorded stream is the generator's output to within 4 ulp on .NET" {
                let struct (xs, _) = Generator.normals 0.84 3.0 1000 (Generator.create 20261119)
                Expect.equal xs.Length Recorded.stream.Length "same length"

                let ulps (a: float) (b: float) =
                    let ia = BitConverter.DoubleToInt64Bits a
                    let ib = BitConverter.DoubleToInt64Bits b
                    if (ia < 0L) <> (ib < 0L) then Int64.MaxValue else abs (ia - ib)

                let distances = Array.map2 ulps xs Recorded.stream
                let differing = distances |> Array.filter (fun d -> d > 0L) |> Array.length
                let worst = Array.max distances
                let worstAt = Array.findIndex (fun d -> d = worst) distances

                printfn
                    "Recorded stream against the generator on .NET (%s): %d of 1000 differ, at most %d ulp"
                    System.Runtime.InteropServices.RuntimeInformation.OSDescription
                    differing
                    worst

                Expect.isTrue (worst <= 4L) (sprintf "largest distance %d ulp at index %d" worst worstAt)
            }
#endif
        ]
