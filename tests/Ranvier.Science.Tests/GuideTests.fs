// The body of each test is a snippet of docs/GUIDE.md: tests/guide-snippets.py checks that the two stay identical.
// Reference values are closed forms written in each test, or the published sleep data (Student, 1908; R's `sleep`).
module Ranvier.Science.Tests.GuideTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities
open Ranvier.Statistics

[<Tests>]
let tests =
    testList
        "Guide"
        [
            test "fit a curve to data" {
                let xs = Array.init 21 (fun i -> 0.25 * float i)
                let ys = xs |> Array.map (fun x -> 2.5 * exp (-1.3 * x))
                let fit = Regression.curve (fun p x -> p[0] * exp (-p[1] * x)) [| 1.0; 1.0 |] xs ys
                Expect.isTrue fit.Converged "the fit converged"
                Expect.isTrue (abs (fit.Parameters[0] - 2.5) <= 1e-8 * 2.5) "the amplitude"
                Expect.isTrue (abs (fit.Parameters[1] - 1.3) <= 1e-8 * 1.3) "the rate"
            }

            test "integrate an ODE in a Run" {
                withGraph (fun () ->
                    let clock = createSignal 0.0
                    let options = OdeOptions.standard
                    let decay = Run.ode options (fun () -> clock.Value) (fun () -> [| 1.0 |]) (fun _ y -> [| -y[0] |])
                    decay.State |> ignore
                    clock.Value <- 1.0
                    let bound = 100.0 * (options.AbsoluteTolerance + options.RelativeTolerance)
                    Expect.isTrue (abs (decay.State.State[0] - exp -1.0) <= bound) "y(1) is e^-1"
                    Expect.equal decay.State.Time 1.0 "the run stopped at the clock")
            }

            test "propagate uncertainty through a formula" {
                withGraph (fun () ->
                    let model = Model.create "pendulum"
                    Quantity.inputWithUncertainty model "L" "Length" Units.metre 0.9945<m> 0.0005<m> |> ignore
                    Quantity.inputWithUncertainty model "T" "Period" Units.second 2.001<s> 0.002<s> |> ignore
                    let text = "4 * pi^2 * L / T^2"

                    match Quantity.formula model "g" "Gravity" text, Formula.parse text with
                    | Ok g, Ok expr ->
                        let relL = 0.0005 / 0.9945
                        let relT = 2.0 * 0.002 / 2.001
                        let expected = g.Read () * sqrt (relL * relL + relT * relT)
                        Expect.isTrue (abs (g.Uncertainty () - expected) <= 1e-12 * expected) "first-order uncertainty"
                        let sampled = Formula.monteCarlo 20000 1 0.95 model expr ()
                        Expect.isTrue (abs (sampled.StandardDeviation - expected) <= 0.05 * expected) "Monte Carlo spread"
                    | _ -> failtest "the formula should parse")
            }

            test "parse and convert units" {
                let parse text = UnitInfo.parse UnitCatalogue.si text

                match parse "km/h", parse "m/s" with
                | Ok kmh, Ok ms ->
                    Expect.equal (UnitInfo.symbol kmh) "km/h" "the symbol round-trips"

                    match UnitInfo.convert kmh ms 36.0 with
                    | Ok speed -> Expect.isTrue (abs (speed - 10.0) <= 1e-15 * 10.0) "36 km/h is 10 m/s"
                    | Error message -> failtest message
                | _ -> failtest "both units should parse"

                match parse "m/" with
                | Error _ -> ()
                | Ok _ -> failtest "a dangling slash should not parse"
            }

            test "use the physical constants in a formula" {
                withGraph (fun () ->
                    let model = Model.create "rest energy"
                    let constants = Constants.declare model
                    let electron = constants.ElectronMass.Entry
                    let c = constants.SpeedOfLight.Entry.Read ()

                    match Quantity.formula model "rest" "Rest energy" "m_e * c^2" with
                    | Ok rest ->
                        let expected = electron.Read () * (c * c)
                        Expect.isTrue (abs (rest.Read () - expected) <= 1e-15 * expected) "m_e c^2"
                        let relative = rest.Uncertainty () / rest.Read ()
                        let electronRelative = electron.Uncertainty () / electron.Read ()
                        Expect.isTrue (abs (relative - electronRelative) <= 1e-12 * electronRelative) "c is exact"
                    | Error message -> failtest message)
            }

            test "test two samples" {
                let moments (xs: float list) = List.fold (Moments.add true) Moments.zero xs
                let a = [ 0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0 ]
                let b = [ 1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4 ]
                let differences = [ -1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4 ]
                let unpaired = HypothesisTest.welchT TwoSided (moments a) (moments b)
                let paired = HypothesisTest.oneSampleT TwoSided 0.0 (moments differences)
                Expect.isTrue (unpaired.PValue > 0.05) "treated as two groups, no effect shows"
                Expect.isTrue (paired.PValue < 0.01) "paired, the effect shows"
            }

            test "adjust a family of p-values" {
                let p = [| 0.04; 0.001; 0.03; 0.2; 0.01 |]
                let ulp = 2.220446049250313e-16

                let within (expected: float[]) (actual: float[]) =
                    Array.iter2 (fun e a -> Expect.isTrue (abs (a - e) <= 4.0 * ulp * abs e) "within 4 ulps") expected actual

                within [| 0.09; 0.005; 0.09; 0.2; 0.04 |] (Adjust.holm p)
                within [| 0.05; 0.005; 0.05; 0.2; 0.025 |] (Adjust.benjaminiHochberg p)
            }

            test "monitor a stream with a sequential test" {
                withGraph (fun () ->
                    let feed = ManualStream<float>()
                    let settings = ConfidenceSequence.tunedFor 0.05 1000.0 ConfidenceSequence.standardBurnIn
                    let initial = SequentialTest.create settings 0.0
                    let monitor = createStreamFold initial (SequentialTest.add true) (fun _ -> feed.Source)
                    monitor.TryValue |> ignore
                    let struct (data, _) = Generator.normals 0.3 1.0 2000 (Generator.create 7)

                    for x in data do
                        feed.Push x

                    Expect.isTrue (SequentialTest.rejects monitor.Value) "the null of mean 0 is rejected"
                    Expect.isTrue (SequentialTest.pValue monitor.Value <= 0.05) "at the 5% level")
            }

            test "keep a regression current over a projection" {
                withGraph (fun () ->
                    let rows = createKeyedCollection (fun (struct (k: int, _: float, _: float)) -> k)

                    for k in 0..9 do
                        rows.AddOrUpdate (struct (k, float k, 1.0 + 2.0 * float k))

                    let project (struct (_: int, x: float, y: float)) = Comoments.add true Comoments.zero x y
                    let kept = rows.Rows |> Aggregate.comomentsBy FloatPolicy.standard project
                    let line = Regression.simple kept.Value.Value
                    Expect.isTrue (abs (line.Slope - 2.0) <= 1e-12) "the slope"
                    Expect.isTrue (abs (line.Intercept - 1.0) <= 1e-12) "the intercept"
                    rows.AddOrUpdate (struct (3, 3.0, 9.0))

                    let fresh =
                        [ for k in 0..9 -> if k = 3 then (3.0, 9.0) else (float k, 1.0 + 2.0 * float k) ]
                        |> List.fold (fun m (x, y) -> Comoments.add true m x y) Comoments.zero
                        |> Regression.simple

                    let edited = Regression.simple kept.Value.Value
                    Expect.isTrue (abs (edited.Slope - fresh.Slope) <= 1e-12) "the slope after the edit"
                    Expect.isTrue (abs (edited.Intercept - fresh.Intercept) <= 1e-12) "the intercept after the edit")
            }

            test "take the spectrum of a signal" {
                let samples = Array.init 64 (fun t -> sin (2.0 * Math.PI * 5.0 * float t / 64.0))
                let power = Fourier.powerSpectrum samples
                let peak = power |> Array.indexed |> Array.maxBy snd |> fst
                Expect.equal peak 5 "the largest bin"
                let frequencies = Fourier.frequencies 64 (1.0 / 64.0)
                Expect.isTrue (abs (frequencies[5] - 5.0) <= 1e-12) "bin 5 is 5 Hz"
            }

            test "solve a linear system and a least-squares problem" {
                match Matrix.solve (Matrix.ofRows [| [| 4.0; 1.0 |]; [| 1.0; 3.0 |] |]) [| 1.0; 2.0 |] with
                | Ok x ->
                    Expect.isTrue (abs (x[0] - 1.0 / 11.0) <= 1e-15) "x[0]"
                    Expect.isTrue (abs (x[1] - 7.0 / 11.0) <= 1e-15) "x[1]"
                | Error message -> failtest message

                let design = Matrix.ofRows [| [| 1.0; 0.0 |]; [| 1.0; 1.0 |]; [| 1.0; 2.0 |]; [| 1.0; 3.0 |] |]

                match QrDecomposition.leastSquares (Matrix.qr design) [| 1.0; 3.0; 2.0; 5.0 |] with
                | Ok line ->
                    Expect.isTrue (abs (line[0] - 1.1) <= 1e-14) "the intercept"
                    Expect.isTrue (abs (line[1] - 1.1) <= 1e-14) "the slope"
                | Error message -> failtest message
            }

            test "window a stream" {
                withGraph (fun () ->
                    let feed = ManualStream<float>()
                    let latest = createStreamFold (Window.moments true 3) Window.push (fun _ -> feed.Source)
                    latest.TryValue |> ignore

                    for i in 1..5 do
                        feed.Push (float i)

                    let w = latest.Value
                    Expect.isTrue (abs (Moments.mean (Window.value w) - 4.0) <= 1e-12) "the mean of the last three"
                    Expect.equal (Seq.toList (Window.items w)) [ 3.0; 4.0; 5.0 ] "the last three items")
            }
        ]
