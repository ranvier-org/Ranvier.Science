// Reference values: the closed forms written in each test (∫₀¹ x² = 1/3, ∫₀^π sin = 2, ∫₀¹ eˣ = e − 1, ∫₀^∞ e⁻ˣ = 1,
// ∫ e^(−x²) = √π, ∫ 1/(1 + x²) = π, ∫₀¹ 1/√x = 2, ∫₀¹ ln x = −1, ∫₀¹ cos 50x = sin 50 / 50, ∫₀¹ x¹³ + x¹² =
// 1/14 + 1/13, ∫₀¹ x^(2n−1) = 1/(2n), ∫₋₁¹ eˣ = e − 1/e, ∫₀² x² = 8/3, ∫₀² x³ = 4, d/dx eˣ = eˣ, d/dx sin at 0 = 1,
// d/dx x³ at 2 = 12) and the trapezoid sums of the samples (0, 1, 3) over (0, 1, 3), worked by hand.
module Ranvier.Science.Tests.CalculusTests

open System
open Expecto
open Ranvier.Numerics

let private absolute = 1e-12
let private relative = 1e-10

[<Tests>]
let tests =
    testList
        "Calculus"
        [
            test "adaptive meets its tolerance on closed forms" {
                let check (name: string) (f: float -> float) (a: float) (b: float) (exact: float) =
                    let r = Integrate.adaptive absolute relative f a b
                    Expect.isTrue r.Converged (name + ": converged")

                    Expect.isLessThanOrEqual
                        (abs (r.Value - exact))
                        (max absolute (relative * abs exact))
                        (sprintf "%s: |%.17g − %.17g|" name r.Value exact)

                check "x²" (fun x -> x * x) 0.0 1.0 (1.0 / 3.0)
                check "sin" sin 0.0 Math.PI 2.0
                check "exp" exp 0.0 1.0 (Math.E - 1.0)
                check "e⁻ˣ on [0, ∞)" (fun x -> exp -x) 0.0 Double.PositiveInfinity 1.0
                check "e^(−x²)" (fun x -> exp (-(x * x))) Double.NegativeInfinity Double.PositiveInfinity (sqrt Math.PI)
                check "1/(1 + x²)" (fun x -> 1.0 / (1.0 + x * x)) Double.NegativeInfinity Double.PositiveInfinity Math.PI
                check "1/√x" (fun x -> 1.0 / sqrt x) 0.0 1.0 2.0
                check "ln x" log 0.0 1.0 -1.0
                check "cos 50x" (fun x -> cos (50.0 * x)) 0.0 1.0 (sin 50.0 / 50.0)
                check "eˣ on (−∞, 0]" exp Double.NegativeInfinity 0.0 1.0
            }

            test "a polynomial of degree 13 converges on one panel" {
                let r = Integrate.adaptive absolute relative (fun x -> pown x 13 + pown x 12) 0.0 1.0
                Expect.isTrue r.Converged "converged"
                Expect.equal r.Evaluations 15 "one panel"
                expectClose 0.0 1e-15 (1.0 / 14.0 + 1.0 / 13.0) r.Value "x¹³ + x¹²"
            }

            test "reversed and equal bounds" {
                let f (x: float) = exp x
                let forward = Integrate.adaptive absolute relative f 0.0 1.0
                let backward = Integrate.adaptive absolute relative f 1.0 0.0
                Expect.equal backward.Value -forward.Value "negated"
                Expect.isTrue backward.Converged "reversed converged"
                let same = Integrate.adaptive absolute relative f 1.0 1.0
                Expect.equal same.Value 0.0 "a = b"
                Expect.isTrue same.Converged "a = b converged"
                Expect.equal same.Evaluations 0 "a = b evaluates nothing"
            }

            test "a divergent integral reports it" {
                let r = Integrate.adaptive absolute relative (fun x -> 1.0 / x) 0.0 1.0
                Expect.isFalse r.Converged "not converged"

                Expect.isGreaterThan
                    r.ErrorEstimate
                    (max absolute (relative * abs r.Value))
                    "the estimate exceeds the tolerance"
            }

            test "NaN and a raising integrand" {
                let r = Integrate.adaptive absolute relative (fun _ -> nan) 0.0 1.0
                Expect.isTrue (Double.IsNaN r.Value) "NaN value"
                Expect.isFalse r.Converged "NaN not converged"
                Expect.equal r.Evaluations 15 "stops after the first panel"
                let bound = Integrate.adaptive absolute relative exp nan 1.0
                Expect.isTrue (Double.IsNaN bound.Value) "NaN bound"
                Expect.isFalse bound.Converged "NaN bound not converged"
                Expect.equal bound.Evaluations 0 "NaN bound evaluates nothing"

                Expect.throwsC
                    (fun () -> Integrate.adaptive absolute relative (fun _ -> failwith "boom") 0.0 1.0 |> ignore)
                    (fun e -> Expect.equal e.Message "boom" "propagated unchanged")
            }

            test "a negative or NaN tolerance raises" {
                let f (x: float) = x

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Integrate.adaptive -1.0 relative f 0.0 1.0 |> ignore)
                    "negative absolute"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Integrate.adaptive absolute nan f 0.0 1.0 |> ignore)
                    "NaN relative"
            }

            test "Gauss–Legendre is exact to degree 2n − 1" {
                for n in [ 1; 2; 5; 10; 20; 64 ] do
                    let degree = 2 * n - 1
                    let value = Integrate.gaussLegendre n (fun x -> pown x degree) 0.0 1.0
                    expectClose 1e-14 0.0 (1.0 / float (2 * n)) value (sprintf "n = %d, x^%d" n degree)

                let value = Integrate.gaussLegendre 20 exp -1.0 1.0
                expectClose 1e-14 0.0 (Math.E - 1.0 / Math.E) value "n = 20, eˣ on [−1, 1]"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Integrate.gaussLegendre 0 exp 0.0 1.0 |> ignore)
                    "points < 1"
            }

            test "trapezoid, simpson and cumulativeTrapezoid" {
                let xs = [| 0.0; 1.0; 3.0 |]
                Expect.equal (Integrate.trapezoid xs xs) 4.5 "trapezoid"
                Expect.equal (Integrate.cumulativeTrapezoid xs xs) [| 0.0; 0.5; 4.5 |] "cumulative"
                let square (x: float) = x * x
                let even = [| 0.0; 0.3; 1.0; 1.2; 2.0 |]
                expectClose 1e-14 0.0 (8.0 / 3.0) (Integrate.simpson even (Array.map square even)) "simpson, four intervals"
                let odd = [| 0.0; 0.5; 1.5; 2.0 |]
                expectClose 1e-14 0.0 (8.0 / 3.0) (Integrate.simpson odd (Array.map square odd)) "simpson, three intervals"
                let uniform = [| 0.0; 0.5; 1.0; 1.5; 2.0 |]
                let cube (x: float) = x * x * x
                expectClose 1e-14 0.0 4.0 (Integrate.simpson uniform (Array.map cube uniform)) "simpson of x³"
                Expect.equal (Integrate.simpson [| 0.0; 2.0 |] [| 1.0; 3.0 |]) 4.0 "two points give the trapezoid"
            }

            test "sample rules at the edges" {
                Expect.equal (Integrate.trapezoid [| 1.0 |] [| 5.0 |]) 0.0 "trapezoid, one point"
                Expect.equal (Integrate.trapezoid [||] [||]) 0.0 "trapezoid, none"
                Expect.equal (Integrate.simpson [| 1.0 |] [| 5.0 |]) 0.0 "simpson, one point"
                Expect.equal (Integrate.simpson [||] [||]) 0.0 "simpson, none"
                Expect.equal (Integrate.cumulativeTrapezoid [| 1.0 |] [| 5.0 |]) [| 0.0 |] "cumulative, one point"
                Expect.equal (Integrate.cumulativeTrapezoid [||] [||]) [||] "cumulative, none"

                Expect.throwsT<ArgumentException>
                    (fun () -> Integrate.trapezoid [| 0.0; 1.0; 2.0 |] [| 0.0; 1.0 |] |> ignore)
                    "trapezoid lengths"

                Expect.throwsT<ArgumentException>
                    (fun () -> Integrate.simpson [| 0.0; 1.0; 2.0 |] [| 0.0; 1.0 |] |> ignore)
                    "simpson lengths"

                Expect.throwsT<ArgumentException>
                    (fun () -> Integrate.cumulativeTrapezoid [| 0.0; 1.0; 2.0 |] [| 0.0; 1.0 |] |> ignore)
                    "cumulative lengths"
            }

            test "derivative" {
                let d = Differentiate.derivative exp 1.0
                expectClose 1e-10 0.0 Math.E d.Value "d/dx eˣ at 1"
                Expect.isLessThanOrEqual d.ErrorEstimate 1e-9 "error estimate of eˣ"
                expectClose 0.0 1e-12 1.0 (Differentiate.derivative sin 0.0).Value "d/dx sin at 0"
                expectClose 1e-12 0.0 12.0 (Differentiate.derivative (fun x -> x * x * x) 2.0).Value "d/dx x³ at 2"
                let broken = Differentiate.derivative (fun _ -> nan) 1.0
                Expect.isTrue (Double.IsNaN broken.Value) "NaN value"
                Expect.isTrue (Double.IsNaN broken.ErrorEstimate) "NaN estimate"
                Expect.isTrue (Double.IsNaN (Differentiate.derivative exp nan).Value) "NaN point"
            }
        ]
