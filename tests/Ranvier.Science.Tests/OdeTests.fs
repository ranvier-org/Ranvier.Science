// Reference values: the closed forms written in each test (y' = y gives eᵗ, y' = −y gives e⁻ᵗ, the oscillator
// (y₀, y₁)' = (y₁, −y₀) gives (cos t, −sin t), the logistic y' = y(1 − y) from 0.1 gives 1/(1 + 9e⁻ᵗ), y' = cos t gives
// sin t, y' = 5t⁴ gives t⁵, y' = 4t³ gives t⁴) and the Taylor polynomial of degree 4, which is what one RK4 step is on a
// linear equation (general knowledge).
module Ranvier.Science.Tests.OdeTests

open System
open Expecto
open Ranvier.Numerics

let private tight =
    { OdeOptions.standard with
        AbsoluteTolerance = 1e-10
        RelativeTolerance = 1e-10 }

// The bound of the ledger ruling on spec 5.1: the global error is at most twice atol + rtol · |exact|.
let private within (exact: float) (v: float) = abs (v - exact) <= 2.0 * (1e-10 + 1e-10 * abs exact)

let private oscillator (_: float) (y: float[]) : float[] = [| y[1]; -y[0] |]

[<Tests>]
let tests =
    testList
        "Ode"
        [
            test "one RK4 step is the quartic Taylor polynomial" {
                let h = 0.1
                let y = Ode.rungeKutta4Step (fun _ y -> [| y[0] |]) 0.0 [| 1.0 |] h
                let taylor = 1.0 + h + h * h / 2.0 + h * h * h / 6.0 + h * h * h * h / 24.0
                expectClose 1e-15 0.0 taylor y.[0] "step of y' = y"
            }

            test "RK4 is fourth order" {
                let error steps =
                    let y = Ode.rungeKutta4 (fun _ y -> [| y[0] |]) 0.0 [| 1.0 |] 1.0 steps
                    abs (y[0] - Math.E)

                let ratio = error 10 / error 20
                Expect.isTrue (ratio >= 14.0 && ratio <= 18.0) (sprintf "the error ratio is %g" ratio)
            }

            test "solve meets the tolerance on closed forms" {
                let check (name: string) (f: float -> float[] -> float[]) (y0: float[]) (t1: float) (exact: float[]) =
                    let s = Ode.solve tight f 0.0 y0 t1 |> expectOk name
                    let last = s.States[s.States.Length - 1]

                    for i in 0 .. exact.Length - 1 do
                        Expect.isTrue
                            (within exact[i] last[i])
                            (sprintf "%s[%d]: %.17g against %.17g, %d steps" name i last[i] exact[i] s.Accepted)

                check "y' = −y" (fun _ y -> [| -y[0] |]) [| 1.0 |] 5.0 [| exp -5.0 |]
                check "oscillator" oscillator [| 1.0; 0.0 |] (2.0 * Math.PI) [| 1.0; 0.0 |]
                check "logistic" (fun _ y -> [| y[0] * (1.0 - y[0]) |]) [| 0.1 |] 10.0 [| 1.0 / (1.0 + 9.0 * exp -10.0) |]
                check "y' = cos t" (fun t _ -> [| cos t |]) [| 0.0 |] 10.0 [| sin 10.0 |]
            }

            test "the fifth-order rule integrates a quartic exactly" {
                let options = { OdeOptions.standard with MaximumStep = 0.25 }
                let s = Ode.solve options (fun t _ -> [| 5.0 * t * t * t * t |]) 0.0 [| 0.0 |] 1.0 |> expectOk "quartic"
                let last = s.States.[s.States.Length - 1]
                expectClose 0.0 1e-15 1.0 last.[0] "y(1)"
            }

            test "dense output reproduces a quartic" {
                let times = [| 0.3; 0.77; 1.5 |]

                let states =
                    Ode.solveAt OdeOptions.standard (fun t _ -> [| 4.0 * t * t * t |]) 0.0 [| 0.0 |] times |> expectOk "quartic"

                for i in 0 .. times.Length - 1 do
                    let t = times.[i]
                    expectClose 1e-14 0.0 (t * t * t * t) states.[i].[0] (sprintf "t = %g" t)
            }

            test "solveAt on the oscillator" {
                let times = [| 0.0; 0.5; 1.0; 2.0; Math.PI; 6.0 |]
                let y0 = [| 1.0; 0.0 |]
                let states = Ode.solveAt tight oscillator 0.0 y0 times |> expectOk "oscillator"
                Expect.equal states.Length times.Length "one state per time"
                Expect.equal states[0] y0 "the state at t0 is y0"

                for i in 0 .. times.Length - 1 do
                    let t = times[i]
                    let bound = 10.0 * (1e-10 + 1e-10 * abs (cos t))
                    Expect.isTrue (abs (states.[i].[0] - cos t) <= bound) (sprintf "cos at t = %g: %.17g" t states.[i].[0])
                    let bound1 = 10.0 * (1e-10 + 1e-10 * abs (sin t))
                    Expect.isTrue (abs (states.[i].[1] + sin t) <= bound1) (sprintf "−sin at t = %g: %.17g" t states.[i].[1])
            }

            test "the solution's shape" {
                let y0 = [| 1.0; 0.0 |]
                let calls = ref 0

                let f t y =
                    calls.Value <- calls.Value + 1
                    oscillator t y

                let s = Ode.solve OdeOptions.standard f 0.0 y0 3.0 |> expectOk "oscillator"
                Expect.equal s.Times[0] 0.0 "first time"
                Expect.equal s.Times[s.Times.Length - 1] 3.0 "last time is t1 exactly"

                for i in 1 .. s.Times.Length - 1 do
                    Expect.isTrue (s.Times[i] > s.Times[i - 1]) (sprintf "ascending at %d" i)

                Expect.equal s.States.Length s.Times.Length "a state per time"
                Expect.equal s.States[0] y0 "the first state is y0"
                Expect.isFalse (obj.ReferenceEquals(s.States[0], y0)) "and another array"
                Expect.equal y0 [| 1.0; 0.0 |] "y0 is unchanged"
                Expect.equal s.Evaluations calls.Value "Evaluations counts the calls of f"
                Expect.isTrue (s.Accepted + s.Rejected >= 1) "at least one step"
                Expect.equal s.Times.Length (s.Accepted + 1) "t0 and the end of each accepted step"
            }

            test "MaximumStep bounds each step" {
                let options = { OdeOptions.standard with MaximumStep = 0.1 }
                let s = Ode.solve options (fun _ y -> [| -y[0] |]) 0.0 [| 1.0 |] 1.0 |> expectOk "decay"

                for i in 1 .. s.Times.Length - 1 do
                    Expect.isTrue
                        (s.Times[i] - s.Times[i - 1] <= 0.1 + 1e-15)
                        (sprintf "step %d is %.17g" i (s.Times[i] - s.Times[i - 1]))
            }

            test "failures give Error" {
                match Ode.solve OdeOptions.standard (fun _ y -> [| y[0] * y[0] |]) 0.0 [| 1.0 |] 2.0 with
                | Error _ -> ()
                | Ok _ -> failtest "y' = y² blows up at t = 1"

                Ode.solve { OdeOptions.standard with MaximumSteps = 10 } oscillator 0.0 [| 1.0; 0.0 |] 1000.0
                |> expectError "MaximumSteps" "the step budget"

                match Ode.solve { OdeOptions.standard with MaximumSteps = 1000 } (fun _ _ -> [| nan |]) 0.0 [| 1.0 |] 1.0 with
                | Error _ -> ()
                | Ok _ -> failtest "a NaN right-hand side"
            }

            test "a state that overflows with a finite right-hand side gives Error" {
                let overflow (y0: float) =
                    match Ode.solve OdeOptions.standard (fun _ _ -> [| 1e308 |]) 0.0 [| y0 |] 10.0 with
                    | Error _ -> ()
                    | Ok s -> failtestf "from %g: Ok with last state %g" y0 s.States.[s.States.Length - 1].[0]

                overflow 1e308
                overflow 0.0
            }

            test "solveAt reads the states of the accepted steps at their ends" {
                let s = Ode.solve tight oscillator 0.0 [| 1.0; 0.0 |] 3.0 |> expectOk "solve"
                let states = Ode.solveAt tight oscillator 0.0 [| 1.0; 0.0 |] s.Times |> expectOk "solveAt"
                Expect.equal states s.States "the state at each accepted step's end"
            }

            test "solveAt with MaximumStep and many times stays within its bound" {
                let options = { tight with MaximumStep = 0.05 }
                let times = Array.init 40 (fun k -> 0.15 * float (k + 1))
                let states = Ode.solveAt options oscillator 0.0 [| 1.0; 0.0 |] times |> expectOk "oscillator"

                for i in 0 .. times.Length - 1 do
                    let t = times.[i]
                    Expect.isTrue (abs (states.[i].[0] - cos t) <= 10.0 * (1e-10 + 1e-10 * abs (cos t))) (sprintf "cos at t = %g" t)
                    Expect.isTrue (abs (states.[i].[1] + sin t) <= 10.0 * (1e-10 + 1e-10 * abs (sin t))) (sprintf "-sin at t = %g" t)
            }

            test "solveAt gives Error and raises like solve" {
                Ode.solveAt { OdeOptions.standard with MaximumSteps = 10 } oscillator 0.0 [| 1.0; 0.0 |] [| 1.0; 1000.0 |]
                |> expectError "MaximumSteps" "the step budget"

                Expect.throwsT<ArgumentException>
                    (fun () -> Ode.solveAt OdeOptions.standard (fun _ _ -> [| 0.0; 0.0 |]) 0.0 [| 1.0 |] [| 1.0 |] |> ignore)
                    "a result of length 2 for a state of length 1"
            }

            test "a raising or misshapen right-hand side" {
                Expect.throwsC
                    (fun () -> Ode.solve OdeOptions.standard (fun _ _ -> failwith "boom") 0.0 [| 1.0 |] 1.0 |> ignore)
                    (fun e -> Expect.equal e.Message "boom" "the caller's exception propagates unchanged")

                Expect.throwsT<ArgumentException>
                    (fun () -> Ode.solve OdeOptions.standard (fun _ _ -> [| 0.0; 0.0 |]) 0.0 [| 1.0 |] 1.0 |> ignore)
                    "a result of length 2 for a state of length 1"
            }

            test "domain" {
                let decay (_: float) (y: float[]) = [| -y[0] |]
                let raises (message: string) (call: unit -> unit) = Expect.throwsT<ArgumentOutOfRangeException> call message

                raises "t1 < t0" (fun () -> Ode.solve OdeOptions.standard decay 1.0 [| 1.0 |] 0.0 |> ignore)
                raises "RK4 with t1 < t0" (fun () -> Ode.rungeKutta4 decay 1.0 [| 1.0 |] 0.0 4 |> ignore)
                raises "unsorted times" (fun () -> Ode.solveAt OdeOptions.standard decay 0.0 [| 1.0 |] [| 0.5; 0.2 |] |> ignore)
                raises "times below t0" (fun () -> Ode.solveAt OdeOptions.standard decay 0.0 [| 1.0 |] [| -0.1; 0.2 |] |> ignore)
                raises "steps = 0" (fun () -> Ode.rungeKutta4 decay 0.0 [| 1.0 |] 1.0 0 |> ignore)

                let invalid (message: string) (options: OdeOptions) =
                    raises message (fun () -> Ode.solve options decay 0.0 [| 1.0 |] 1.0 |> ignore)

                invalid "negative AbsoluteTolerance" { OdeOptions.standard with AbsoluteTolerance = -1.0 }
                invalid "NaN AbsoluteTolerance" { OdeOptions.standard with AbsoluteTolerance = nan }
                invalid "negative RelativeTolerance" { OdeOptions.standard with RelativeTolerance = -1.0 }
                invalid "NaN RelativeTolerance" { OdeOptions.standard with RelativeTolerance = nan }
                invalid "both tolerances 0" { OdeOptions.standard with AbsoluteTolerance = 0.0; RelativeTolerance = 0.0 }
                invalid "MaximumStep 0" { OdeOptions.standard with MaximumStep = 0.0 }
                invalid "negative InitialStep" { OdeOptions.standard with InitialStep = -1.0 }
                invalid "MaximumSteps 0" { OdeOptions.standard with MaximumSteps = 0 }

                let s = Ode.solve OdeOptions.standard decay 2.0 [| 1.0 |] 2.0 |> expectOk "t1 = t0"
                Expect.equal s.Times [| 2.0 |] "one time"
                Expect.equal s.States [| [| 1.0 |] |] "a copy of y0"
                Expect.equal (Ode.solveAt OdeOptions.standard decay 0.0 [| 1.0 |] [||] |> expectOk "empty") [||] "no times"
            }
        ]
