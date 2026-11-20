// Reference values: the exponential data are the model at (3, 0.5), so the fit has residuals 0 there (closed form); the
// Rosenbrock residuals [10(p1 - p0^2); 1 - p0] vanish at (1, 1) (closed form); the analytic Jacobian of the exponential
// model is written in the test; the Misra1a values are the NIST StRD Nonlinear Regression literals cited beside them.
module Ranvier.Science.Tests.LeastSquaresTests

open System
open Expecto
open Ranvier.Numerics

let private times = Array.init 10 float

let private exponentialResiduals (p: float[]) : float[] =
    times |> Array.map (fun t -> p[0] * exp (-p[1] * t) - 3.0 * exp (-0.5 * t))

let private exponentialJacobian (p: float[]) : Matrix =
    Matrix.init 10 2 (fun i j ->
        let t = times[i]

        if j = 0 then exp (-p[1] * t) else -p[0] * t * exp (-p[1] * t))

let private rosenbrock (p: float[]) : float[] = [| 10.0 * (p[1] - p[0] * p[0]); 1.0 - p[0] |]

// NIST StRD Nonlinear Regression, Misra1a: https://www.itl.nist.gov/div898/strd/nls/data/misra1a.shtml (from
// documentation, read on 2026-11-20). Model y = b1 (1 - exp(-b2 x)).
// Observations as (y, x):
let private misraData: (float * float)[] =
    [| (10.07, 77.6)
       (14.73, 114.9)
       (17.94, 141.1)
       (23.93, 190.8)
       (29.61, 239.9)
       (35.18, 289.0)
       (40.02, 332.8)
       (44.82, 378.4)
       (50.76, 434.8)
       (55.05, 477.3)
       (61.01, 536.8)
       (66.40, 593.1)
       (75.47, 689.1)
       (81.78, 760.0) |]

// Start 1: b1 = 500, b2 = 1e-4.
let private misraStart: float[] = [| 500.0; 1e-4 |]
// Certified values: b1 = 2.3894212918E+02, b2 = 5.5015643181E-04.
let private certifiedB1 = 2.3894212918E+02
let private certifiedB2 = 5.5015643181E-04
// Certified residual sum of squares: 1.2455138894E-01.
let private certifiedRss = 1.2455138894E-01

let private misraResiduals (p: float[]) : float[] =
    misraData |> Array.map (fun (y, x) -> p[0] * (1.0 - exp (-p[1] * x)) - y)

[<Tests>]
let tests =
    testList
        "LeastSquares"
        [
            test "an exact exponential fit" {
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard exponentialResiduals [| 1.0; 0.1 |]
                expectClose 1e-8 0.0 3.0 r.Parameters[0] "amplitude"
                expectClose 1e-8 0.0 0.5 r.Parameters[1] "rate"
                Expect.isLessThanOrEqual r.Cost 1e-20 "cost"
                Expect.isTrue r.Converged "converged"
            }

            test "Rosenbrock as least squares" {
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard rosenbrock [| -1.2; 1.0 |]
                expectClose 0.0 1e-8 1.0 r.Parameters[0] "p0"
                expectClose 0.0 1e-8 1.0 r.Parameters[1] "p1"
                Expect.isTrue r.Converged "converged"
            }

            test "an analytic Jacobian gives the same answer" {
                let analytic =
                    LeastSquares.levenbergMarquardtWith
                        LeastSquaresOptions.standard
                        exponentialResiduals
                        exponentialJacobian
                        [| 1.0; 0.1 |]

                expectClose 1e-10 0.0 3.0 analytic.Parameters[0] "amplitude"
                expectClose 1e-10 0.0 0.5 analytic.Parameters[1] "rate"

                let numeric =
                    LeastSquares.levenbergMarquardt LeastSquaresOptions.standard exponentialResiduals [| 1.0; 0.1 |]

                let reference = exponentialJacobian numeric.Parameters

                for i in 0 .. 9 do
                    for j in 0 .. 1 do
                        expectClose 1e-6 0.0 (Matrix.get reference i j) (Matrix.get numeric.Jacobian i j) "Jacobian entry"
            }

            test "NIST Misra1a" {
                let options =
                    { Tolerance = 1e-10
                      MaximumIterations = 500 }

                let r = LeastSquares.levenbergMarquardt options misraResiduals misraStart
                expectClose 1e-6 0.0 certifiedB1 r.Parameters[0] "b1"
                expectClose 1e-6 0.0 certifiedB2 r.Parameters[1] "b2"
                expectClose 1e-6 0.0 certifiedRss (2.0 * r.Cost) "residual sum of squares"
                Expect.isTrue r.Converged "converged"
            }

            test "the iteration limit" {
                let options =
                    { Tolerance = 1e-10
                      MaximumIterations = 1 }

                let r = LeastSquares.levenbergMarquardt options misraResiduals misraStart
                Expect.isFalse r.Converged "not converged"
                Expect.equal r.Iterations 1 "iterations"
            }

            test "non-finite initial residuals" {
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard (fun _ -> [| nan |]) [| 1.0 |]
                Expect.isFalse r.Converged "not converged"
                Expect.equal r.Iterations 0 "iterations"
            }

            test "a residual function that is NaN away from the start rejects the trial" {
                let f (p: float[]) = if p[0] = 1.0 then [| 1.0; 2.0 |] else [| nan; nan |]
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard f [| 1.0 |]
                Expect.isFalse r.Converged "not converged"
                Expect.equal r.Parameters [| 1.0 |] "parameters stay at the start"
                Expect.equal r.Cost 2.5 "cost at the start"
            }

            test "a Jacobian of the wrong sign finds no descent and does not converge" {
                // Residual p - 3 from p = 5 (cost 2, minimum 0 at 3), with the Jacobian -1 in place of +1.
                let r =
                    LeastSquares.levenbergMarquardtWith
                        LeastSquaresOptions.standard
                        (fun p -> [| p[0] - 3.0 |])
                        (fun _ -> Matrix.init 1 1 (fun _ _ -> -1.0))
                        [| 5.0 |]

                Expect.isFalse r.Converged "no descent was found"
                Expect.equal r.Parameters [| 5.0 |] "parameters stay at the start"
                Expect.equal r.Cost 2.0 "cost at the start"
            }

            test "a cliff at the start finds no descent and does not converge" {
                // Every point but the start has residual 10 (cost 50); the start has residual 2 (cost 2).
                let f (p: float[]) = if p[0] = 5.0 then [| 2.0 |] else [| 10.0 |]
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard f [| 5.0 |]
                Expect.isFalse r.Converged "no descent was found"
                Expect.equal r.Parameters [| 5.0 |] "parameters stay at the start"
            }

            test "a raising residual function propagates" {
                let mutable calls = 0

                let f (p: float[]) =
                    calls <- calls + 1
                    if calls = 2 then failwith "boom" else [| p[0] - 3.0 |]

                let raised =
                    try
                        LeastSquares.levenbergMarquardt LeastSquaresOptions.standard f [| 1.0 |] |> ignore
                        ""
                    with e ->
                        e.Message

                Expect.equal raised "boom" "message"
            }

            test "a residual length that changes raises" {
                let f (p: float[]) = if p[0] = 1.0 then [| 1.0; 2.0; 3.0 |] else [| 1.0; 2.0 |]

                Expect.throwsT<ArgumentException>
                    (fun () -> LeastSquares.levenbergMarquardt LeastSquaresOptions.standard f [| 1.0 |] |> ignore)
                    "length 3 then 2"
            }

            test "an analytic Jacobian of the wrong shape raises" {
                Expect.throwsT<ArgumentException>
                    (fun () ->
                        LeastSquares.levenbergMarquardtWith
                            LeastSquaresOptions.standard
                            exponentialResiduals
                            (fun _ -> Matrix.zero 10 3)
                            [| 1.0; 0.1 |]
                        |> ignore)
                    "10 x 3 for two parameters"
            }

            test "options outside the domain raise" {
                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () ->
                        LeastSquares.levenbergMarquardt
                            { LeastSquaresOptions.standard with Tolerance = -1.0 }
                            rosenbrock
                            [| 0.0; 0.0 |]
                        |> ignore)
                    "negative tolerance"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () ->
                        LeastSquares.levenbergMarquardt
                            { LeastSquaresOptions.standard with MaximumIterations = 0 }
                            rosenbrock
                            [| 0.0; 0.0 |]
                        |> ignore)
                    "no iterations"
            }

            test "inputs are not mutated" {
                let initial = [| 1.0; 0.1 |]
                let r = LeastSquares.levenbergMarquardt LeastSquaresOptions.standard exponentialResiduals initial
                Expect.equal initial [| 1.0; 0.1 |] "initial"
                Expect.isFalse (Object.ReferenceEquals(r.Parameters, initial)) "fresh parameters"
            }
        ]
