// Reference values: the closed forms written in each test (the root of x² − 2 is √2, the root of x³ − 2 satisfies
// r³ = 2, the minimum of (x − 2)² + 1 is at 2, cos has its minimum at π on [0, 2π], the Rosenbrock function has its
// minimum 0 at (1, 1), Σ (xᵢ − i)² has its minimum 0 at (0, 1, 2, 3, 4)); the root of cos x = x is checked by a sign
// change of f around the result.
module Ranvier.Science.Tests.RootsTests

open System
open Expecto
open Ranvier.Numerics

let private rosenbrock (x: float[]) : float =
    100.0 * (x[1] - x[0] * x[0]) * (x[1] - x[0] * x[0]) + (1.0 - x[0]) * (1.0 - x[0])

let private shiftedSquares (x: float[]) : float =
    let mutable s = 0.0

    for i in 0 .. x.Length - 1 do
        s <- s + (x[i] - float i) * (x[i] - float i)

    s

[<Tests>]
let tests =
    testList
        "Roots"
        [
            test "bisection and brent find the root of two" {
                let f (x: float) = x * x - 2.0
                let viaBisection = Roots.bisection 1e-12 f 0.0 2.0 |> expectOk "bisection"
                let viaBrent = Roots.brent 1e-12 f 0.0 2.0 |> expectOk "brent"
                expectClose 0.0 2e-12 (sqrt 2.0) viaBisection "bisection root of x² − 2"
                expectClose 0.0 2e-12 (sqrt 2.0) viaBrent "brent root of x² − 2"
            }

            test "brent finds a root within its tolerance" {
                let check (name: string) (f: float -> float) (a: float) (b: float) =
                    let r = Roots.brent 1e-10 f a b |> expectOk name
                    Expect.isLessThanOrEqual (f (r - 2e-10) * f (r + 2e-10)) 0.0 (name + ": a sign change within 2t")

                check "cos x − x" (fun x -> cos x - x) 0.0 1.0
                check "(x − 1)³" (fun x -> (x - 1.0) * (x - 1.0) * (x - 1.0)) 0.0 3.0
            }

            test "brent accepts either order and exact endpoints" {
                let f (x: float) = x * x - 2.0
                Expect.equal (Roots.brent 1e-12 f 2.0 0.0) (Roots.brent 1e-12 f 0.0 2.0) "brent, either order"
                Expect.equal (Roots.bisection 1e-12 f 2.0 0.0) (Roots.bisection 1e-12 f 0.0 2.0) "bisection, either order"
                let g (x: float) = x - 1.0
                Expect.equal (Roots.brent 1e-12 g 1.0 3.0) (Ok 1.0) "brent, root at a"
                Expect.equal (Roots.brent 1e-12 g -1.0 1.0) (Ok 1.0) "brent, root at b"
                Expect.equal (Roots.bisection 1e-12 g 1.0 3.0) (Ok 1.0) "bisection, root at a"
            }

            test "an unbracketed or NaN function gives Error" {
                let positive (x: float) = x * x + 1.0
                Roots.brent 1e-12 positive -1.0 1.0 |> expectError "not bracketed" "brent, no sign change"
                Roots.bisection 1e-12 positive -1.0 1.0 |> expectError "not bracketed" "bisection, no sign change"
                let broken (x: float) = if x > 0.5 then nan else x - 1.0
                Roots.brent 1e-12 broken 0.0 2.0 |> expectError "NaN" "brent, NaN at b"
                Roots.bisection 1e-12 broken 0.0 2.0 |> expectError "NaN" "bisection, NaN at b"
                let interior (x: float) = if x > 0.5 && x < 1.5 then nan else x - 1.0
                Roots.brent 1e-12 interior 0.0 2.0 |> expectError "NaN" "brent, NaN inside"
                Roots.bisection 1e-12 interior 0.0 2.0 |> expectError "NaN" "bisection, NaN inside"
            }

            test "newton" {
                let r = Roots.newton 1e-12 (fun x -> x * x - 2.0) (fun x -> 2.0 * x) 1.0 |> expectOk "newton"
                expectClose 0.0 1e-12 (sqrt 2.0) r "root of x² − 2"

                Roots.newton 1e-12 (fun x -> x * x - 2.0) (fun x -> 2.0 * x) 0.0
                |> expectError "derivative" "zero derivative"

                match Roots.newton 1e-12 atan (fun x -> 1.0 / (1.0 + x * x)) 2.0 with
                | Ok value -> failtestf "atan from 2 should fail, got Ok %g" value
                | Error _ -> ()
            }

            test "a raising function propagates" {
                let boom (_: float) : float = failwith "boom"

                Expect.throwsC
                    (fun () -> Roots.brent 1e-9 boom 0.0 1.0 |> ignore)
                    (fun e -> Expect.equal e.Message "boom" "brent")

                Expect.throwsC
                    (fun () -> Roots.newton 1e-9 boom (fun _ -> 1.0) 0.0 |> ignore)
                    (fun e -> Expect.equal e.Message "boom" "newton")

                Expect.throwsC
                    (fun () -> Minimize.brent 1e-9 boom 0.0 1.0 |> ignore)
                    (fun e -> Expect.equal e.Message "boom" "Minimize.brent")
            }

            test "tolerance zero ends before the cap" {
                let cubic (x: float) = x * x * x - 2.0
                let viaBisection = Roots.bisection 0.0 cubic 0.0 2.0 |> expectOk "bisection"
                let viaBrent = Roots.brent 0.0 cubic 0.0 2.0 |> expectOk "brent"
                let viaNewton = Roots.newton 0.0 cubic (fun x -> 3.0 * x * x) 1.0 |> expectOk "newton"
                expectClose 1e-15 0.0 2.0 (viaBisection * viaBisection * viaBisection) "bisection: r³ = 2"
                expectClose 1e-15 0.0 2.0 (viaBrent * viaBrent * viaBrent) "brent: r³ = 2"
                expectClose 1e-15 0.0 2.0 (viaNewton * viaNewton * viaNewton) "newton: r³ = 2"

                // a simple root at zero, where the relative part of Brent's tolerance vanishes
                let odd (x: float) = x + x * x * x
                let brentZero = Roots.brent 0.0 odd -1.0 2.0 |> expectOk "brent at zero"
                let bisectionZero = Roots.bisection 0.0 odd -1.0 2.0 |> expectOk "bisection at zero"
                expectClose 0.0 1e-12 0.0 brentZero "brent root at zero"
                expectClose 0.0 1e-12 0.0 bisectionZero "bisection root at zero"

                let interior: Minimum = Minimize.brent 0.0 (fun x -> (x - 2.0) * (x - 2.0)) 0.0 5.0
                Expect.isTrue interior.Converged "Minimize.brent, interior"
                let endpoint: Minimum = Minimize.brent 0.0 (fun x -> -x) 0.0 1.0
                Expect.isTrue endpoint.Converged "Minimize.brent, endpoint"
            }

            test "Minimize.brent finds interior and endpoint minima" {
                let m: Minimum = Minimize.brent 1e-8 (fun x -> (x - 2.0) * (x - 2.0) + 1.0) 0.0 5.0
                expectClose 0.0 1e-6 2.0 m.X "interior X"
                expectClose 0.0 1e-12 1.0 m.Value "interior value"
                Expect.isTrue m.Converged "interior converged"
                let c: Minimum = Minimize.brent 1e-8 cos 0.0 (2.0 * Math.PI)
                expectClose 0.0 1e-6 Math.PI c.X "cos X"
                let e: Minimum = Minimize.brent 1e-8 id 0.0 1.0
                expectClose 0.0 1e-6 0.0 e.X "x on [0, 1]"
            }

            test "Minimize.brent reports a NaN function" {
                let m: Minimum = Minimize.brent 1e-8 (fun _ -> nan) 0.0 1.0
                Expect.isFalse m.Converged "not converged"
                Expect.isTrue (Double.IsNaN m.Value) "Value is NaN"
                let later: Minimum = Minimize.brent 1e-8 (fun x -> if x > 0.5 then nan else x) 0.0 1.0
                Expect.isFalse later.Converged "NaN met later: not converged"
            }

            test "brent over a bracket wider than the largest double" {
                // sign change at 1e300; x / 1e10 − 1 has its root at 1e10 (closed forms)
                let step (x: float) = float (sign (x - 1e300))
                let r = Roots.brent 0.0 step -1.7e308 1.7e308 |> expectOk "step"
                Expect.isFalse (Double.IsInfinity r || Double.IsNaN r) "finite"
                expectClose 1e-14 0.0 1e300 r "step root"

                for bound in [ 1.7e308; 1e308 ] do
                    let line (x: float) = x / 1e10 - 1.0
                    let r = Roots.brent 1e-3 line -bound bound |> expectOk (sprintf "line, bound %g" bound)
                    expectClose 0.0 2e-3 1e10 r (sprintf "line root, bound %g" bound)
            }

            test "Minimize.brent raises when the interval width overflows" {
                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.brent 1e-8 id -1.7e308 1.7e308 |> ignore)
                    "[-1.7e308, 1.7e308]"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.brent 1e-8 id -1e308 1e308 |> ignore)
                    "[-1e308, 1e308]"
            }

            test "nelderMead does not converge on NaN everywhere" {
                let m: MinimumN = Minimize.nelderMead NelderMeadOptions.standard (fun _ -> nan) [| 0.0; 0.0 |]
                Expect.isFalse m.Converged "all NaN"
                Expect.equal m.Value Double.PositiveInfinity "NaN is ordered as +∞"
                let inf: MinimumN = Minimize.nelderMead NelderMeadOptions.standard (fun _ -> infinity) [| 0.0 |]
                Expect.isFalse inf.Converged "all +∞"
            }

            test "nelderMead leaves a NaN region" {
                // NaN for x < 0.5, so x0 = 0.45 is NaN and its only neighbour 0.55 is finite; the minimum is at 1
                let f (x: float[]) = if x[0] < 0.5 then nan else (x[0] - 1.0) * (x[0] - 1.0)
                let m: MinimumN = Minimize.nelderMead NelderMeadOptions.standard f [| 0.45 |]
                Expect.isTrue m.Converged "converged"
                expectClose 0.0 1e-5 1.0 m.X[0] "X"
            }

            test "nelderMead minimises Rosenbrock" {
                let x0 = [| -1.2; 1.0 |]
                let m: MinimumN = Minimize.nelderMead NelderMeadOptions.standard rosenbrock x0
                expectClose 0.0 1e-5 1.0 m.X[0] "x0"
                expectClose 0.0 1e-5 1.0 m.X[1] "x1"
                Expect.isTrue m.Converged "converged"
                Expect.isGreaterThan m.Evaluations m.Iterations "more evaluations than iterations"
                Expect.equal x0 [| -1.2; 1.0 |] "x0 unchanged"
            }

            test "nelderMead in five dimensions" {
                let m: MinimumN = Minimize.nelderMead NelderMeadOptions.standard shiftedSquares (Array.zeroCreate 5)

                for i in 0 .. 4 do
                    expectClose 0.0 1e-5 (float i) m.X[i] (sprintf "coordinate %d" i)

                Expect.isTrue m.Converged "converged"
            }

            test "nelderMead stops at its iteration limit" {
                let options: NelderMeadOptions = { NelderMeadOptions.standard with MaximumIterations = 5 }
                let m: MinimumN = Minimize.nelderMead options rosenbrock [| -1.2; 1.0 |]
                Expect.isFalse m.Converged "not converged"
                Expect.equal m.Iterations 5 "iterations"
            }

            test "domain" {
                let f (x: float) = x
                let negative = -1e-9

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Roots.bisection negative f -1.0 1.0 |> ignore)
                    "bisection tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Roots.brent negative f -1.0 1.0 |> ignore)
                    "brent tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Roots.newton negative f (fun _ -> 1.0) 1.0 |> ignore)
                    "newton tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Roots.brent nan f -1.0 1.0 |> ignore)
                    "brent NaN tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.brent negative f 0.0 1.0 |> ignore)
                    "Minimize.brent tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.brent 1e-8 f 1.0 1.0 |> ignore)
                    "Minimize.brent a = b"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.brent 1e-8 f 2.0 1.0 |> ignore)
                    "Minimize.brent a > b"

                let badTolerance: NelderMeadOptions = { NelderMeadOptions.standard with Tolerance = negative }

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.nelderMead badTolerance shiftedSquares [| 0.0 |] |> ignore)
                    "nelderMead tolerance"

                let noStep: NelderMeadOptions = { NelderMeadOptions.standard with InitialStep = 0.0 }

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.nelderMead noStep shiftedSquares [| 0.0 |] |> ignore)
                    "nelderMead step"

                let noIterations: NelderMeadOptions = { NelderMeadOptions.standard with MaximumIterations = 0 }

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Minimize.nelderMead noIterations shiftedSquares [| 0.0 |] |> ignore)
                    "nelderMead iterations"

                Expect.throwsT<ArgumentException>
                    (fun () -> Minimize.nelderMead NelderMeadOptions.standard shiftedSquares [||] |> ignore)
                    "nelderMead empty x0"
            }
        ]
