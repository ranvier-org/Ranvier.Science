// Reference values: the closed forms written in each test (the natural spline through (0, 0), (1, 1), (2, 0) is
// -x^3/2 + 3x/2 on [0, 1], from M0 + 4 M1 + M2 = -12; both cubics reproduce a line; a monotone interpolant of a step
// stays between its two levels) and the piecewise-linear values worked by hand.
module Ranvier.Science.Tests.InterpolateTests

open System
open Expecto
open Ranvier.Numerics

let private kinds: (string * (float[] -> float[] -> Interpolant)) list =
    [ "linear", Interpolate.linear
      "natural cubic", Interpolate.naturalCubic
      "monotone cubic", Interpolate.monotoneCubic ]

[<Tests>]
let tests =
    testList
        "Interpolate"
        [
            test "linear" {
                let f = Interpolate.linear [| 0.; 1.; 3. |] [| 0.; 2.; -2. |]
                Expect.equal (Interpolant.evaluate f 0.5) 1.0 "0.5"
                Expect.equal (Interpolant.evaluate f 2.0) 0.0 "2"
                Expect.equal (Interpolant.evaluate f 0.0) 0.0 "knot 0"
                Expect.equal (Interpolant.evaluate f 1.0) 2.0 "knot 1"
                Expect.equal (Interpolant.evaluate f 3.0) -2.0 "knot 3"
                Expect.equal (Interpolant.derivative f 2.0) -2.0 "slope on [1, 3]"
                Expect.equal (Interpolant.derivative f 1.0) -2.0 "right-hand slope at an interior knot"
                Expect.equal (Interpolant.derivative f 3.0) -2.0 "left-hand slope at the last knot"
            }

            test "outside the range is NaN" {
                for name, make in kinds do
                    let f = make [| 0.; 1.; 3. |] [| 0.; 2.; -2. |]
                    Expect.isTrue (Double.IsNaN(Interpolant.evaluate f -0.1)) (name + ": below")
                    Expect.isTrue (Double.IsNaN(Interpolant.evaluate f 3.1)) (name + ": above")
                    Expect.isTrue (Double.IsNaN(Interpolant.evaluate f nan)) (name + ": nan")
                    Expect.isTrue (Double.IsNaN(Interpolant.derivative f -0.1)) (name + ": derivative below")
                    Expect.isTrue (Double.IsNaN(Interpolant.derivative f nan)) (name + ": derivative nan")
                    Expect.equal (Interpolant.evaluate f 0.0) 0.0 (name + ": first knot")
                    Expect.equal (Interpolant.evaluate f 3.0) -2.0 (name + ": last knot")
            }

            test "natural cubic closed form" {
                let f = Interpolate.naturalCubic [| 0.; 1.; 2. |] [| 0.; 1.; 0. |]
                // -x^3/2 + 3x/2 on [0, 1]: at 0.5 it is -0.0625 + 0.75; its derivative -3x^2/2 + 3/2 is 1.125 at 0.5.
                expectClose 1e-15 1e-15 0.6875 (Interpolant.evaluate f 0.5) "evaluate 0.5"
                expectClose 1e-15 1e-15 0.6875 (Interpolant.evaluate f 1.5) "evaluate 1.5 by symmetry"
                expectClose 1e-15 1e-15 1.125 (Interpolant.derivative f 0.5) "derivative 0.5"
                expectClose 1e-15 1e-15 0.0 (Interpolant.derivative f 1.0) "derivative 1"
            }

            test "cubics reproduce lines" {
                let xs = [| 0.; 0.4; 1.7; 2.; 3.5 |]
                let ys = xs |> Array.map (fun x -> 2.0 * x + 1.0)

                for name, make in kinds |> List.tail do
                    let f = make xs ys

                    for k in 0..49 do
                        let x = 3.5 * float k / 49.0
                        expectClose 0.0 1e-14 (2.0 * x + 1.0) (Interpolant.evaluate f x) (sprintf "%s at %g" name x)
                        expectClose 0.0 1e-14 2.0 (Interpolant.derivative f x) (sprintf "%s slope at %g" name x)
            }

            test "monotone cubic does not overshoot" {
                let f = Interpolate.monotoneCubic [| 0.; 1.; 2.; 3.; 4.; 5. |] [| 0.; 0.; 0.; 1.; 1.; 1. |]
                let values = Array.init 1001 (fun k -> Interpolant.evaluate f (5.0 * float k / 1000.0))

                for k in 1..1000 do
                    Expect.isTrue (values[k] - values[k - 1] >= -1e-15) (sprintf "step %d" k)

                for k in 0..1000 do
                    let x = 5.0 * float k / 1000.0
                    Expect.isTrue (values[k] >= -1e-15 && values[k] <= 1.0 + 1e-15) (sprintf "range at %g: %.17g" x values[k])

                    if x <= 2.0 then
                        expectClose 0.0 1e-15 0.0 values[k] (sprintf "flat low at %g" x)

                    if x >= 3.0 then
                        expectClose 0.0 1e-15 1.0 values[k] (sprintf "flat high at %g" x)
            }

            test "copies and equality" {
                let xs = [| 0.; 1.; 2.; 4. |]
                let ys = [| 1.; 3.; 2.; 5. |]

                for name, make in kinds do
                    let f = make xs ys
                    let before = [| 0.5; 1.5; 3.0 |] |> Array.map (Interpolant.evaluate f)
                    let other = make (Array.copy xs) (Array.copy ys)
                    xs[1] <- 9.0
                    ys[2] <- -7.0
                    let after = [| 0.5; 1.5; 3.0 |] |> Array.map (Interpolant.evaluate f)
                    Expect.equal after before (name + ": unchanged by mutation")
                    xs[1] <- 1.0
                    ys[2] <- 2.0
                    Expect.isTrue (f = other) (name + ": equal from equal arrays")
                    Expect.isFalse (f = make xs [| 1.; 3.; 2.; 6. |]) (name + ": differs with other ys")
            }

            test "invalid knots raise" {
                for name, make in kinds do
                    let raises (what: string) (xs: float[]) (ys: float[]) =
                        Expect.throwsT<ArgumentException> (fun () -> make xs ys |> ignore) (name + ": " + what)

                    raises "repeated x" [| 0.; 0.; 1. |] [| 1.; 2.; 3. |]
                    raises "NaN x" [| 0.; nan; 1. |] [| 1.; 2.; 3. |]
                    raises "decreasing" [| 1.; 0. |] [| 1.; 2. |]
                    raises "one point" [| 1. |] [| 1. |]
                    raises "ys shorter" [| 0.; 1.; 2. |] [| 1.; 2. |]

                    Expect.throwsC
                        (fun () -> make [| 0.; 1.; 2. |] [| 1.; 2. |] |> ignore)
                        (fun e -> Expect.isTrue (e.Message.Contains "3" && e.Message.Contains "2") (name + ": names both lengths"))
            }

            test "a non-finite y gives NaN everywhere" {
                for name, make in kinds do
                    for bad in [ nan; infinity; -infinity ] do
                        let f = make [| 0.; 1.; 2.; 3.; 4. |] [| bad; 1.; 2.; 1.; 0. |]
                        Expect.isTrue (Double.IsNaN(Interpolant.evaluate f 3.5)) (sprintf "%s %g: evaluate far from the bad y" name bad)
                        Expect.isTrue (Double.IsNaN(Interpolant.derivative f 3.5)) (sprintf "%s %g: derivative far from the bad y" name bad)
                        Expect.isTrue (Double.IsNaN(Interpolant.evaluate f 4.0)) (sprintf "%s %g: last knot" name bad)
            }
        ]
