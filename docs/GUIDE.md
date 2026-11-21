# Guide

Ranvier.Numerics holds numerical kernels (linear algebra, root finding, least squares, ordinary differential equations, Fourier transforms, running moments, windows, random generators) and their reactive forms over the Ranvier graph. Ranvier.Quantities holds units, the model of named quantities with uncertainty, the formula notation, Monte Carlo propagation, the physical constants and the `Run` of a simulation. Ranvier.Statistics holds distributions, intervals, hypothesis tests, p-value adjustment, regression, sequential tests and resampling. Each of the twelve tasks below is one short snippet that does the task and checks its own result.

Every snippet assumes `open System`, `open Expecto`, `open Ranvier`, `open Ranvier.Numerics`, `open Ranvier.Quantities` and `open Ranvier.Statistics`. Each snippet is the body of a test in [`tests/Ranvier.Science.Tests/GuideTests.fs`](../tests/Ranvier.Science.Tests/GuideTests.fs), which runs on .NET and, compiled by Fable, on JavaScript; `tests/guide-snippets.py` fails when a snippet and its test differ.

Two helpers from [`tests/Ranvier.Science.Tests/Support.fs`](../tests/Ranvier.Science.Tests/Support.fs) appear. `withGraph` runs a function with a fresh graph active and disposes the graph afterwards; a snippet that reads a node, a signal or a memo runs inside it. `ManualStream<'T>` is a stream source the code drives: its `Source` member is what `createStreamFold` takes, and its `Push` member delivers one item. A stream starts at the first read of the fold's `TryValue`, so a snippet reads it once before the first push.

## Fit a curve to data

Regression.curve fits a model `float[] -> float -> float` to points from an initial parameter vector by Levenberg-Marquardt, and returns the parameters, their standard errors, the covariance and whether the search converged. Start within a few orders of magnitude of the fitted values. Here the data are exact, so the fit recovers the amplitude and rate that made them.

Test: "fit a curve to data" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
let xs = Array.init 21 (fun i -> 0.25 * float i)
let ys = xs |> Array.map (fun x -> 2.5 * exp (-1.3 * x))
let fit = Regression.curve (fun p x -> p[0] * exp (-p[1] * x)) [| 1.0; 1.0 |] xs ys
Expect.isTrue fit.Converged "the fit converged"
Expect.isTrue (abs (fit.Parameters[0] - 2.5) <= 1e-8 * 2.5) "the amplitude"
Expect.isTrue (abs (fit.Parameters[1] - 1.3) <= 1e-8 * 1.3) "the rate"
```

## Integrate an ODE in a Run

Run.ode integrates a system as a function of a clock. The run starts at its first read, at the clock's value then, with `Time` 0 and a copy of the start state; `Time` counts from 0 at each start, not from the clock. Each later rise of the clock integrates the state over the gap, so the snippet reads the state once before it moves the clock: without that read the run would start at the clock's value of 1 and the state would stay at 1. The clock starts at 0 here, so `Time` is 1 after the move. The array in `State` is the run's own and is not to be mutated.

Test: "integrate an ODE in a Run" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
withGraph (fun () ->
    let clock = createSignal 0.0
    let options = OdeOptions.standard
    let decay = Run.ode options (fun () -> clock.Value) (fun () -> [| 1.0 |]) (fun _ y -> [| -y[0] |])
    decay.State |> ignore
    clock.Value <- 1.0
    let bound = 100.0 * (options.AbsoluteTolerance + options.RelativeTolerance)
    Expect.isTrue (abs (decay.State.State[0] - exp -1.0) <= bound) "y(1) is e^-1"
    Expect.equal decay.State.Time 1.0 "the run stopped at the clock")
```

## Propagate uncertainty through a formula

An input with an uncertainty carries a standard uncertainty beside its value, and a formula entry computes its value and its first-order uncertainty from those of the entries it names. Formula.monteCarlo draws from the same inputs and returns the spread of the formula's value, a check of the linearisation. The graph recomputes both when an input changes.

Test: "propagate uncertainty through a formula" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Parse and convert units

UnitInfo.parse reads unit text against a catalogue: prefixes, products, quotients and powers. UnitInfo.convert scales a value from one unit to another of the same dimension and returns an `Error` for units of different dimension. Text that is not a unit is an `Error` too.

Test: "parse and convert units" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Use the physical constants in a formula

Constants.declare adds the CODATA 2022 constants to a model as entries, the measured ones (`m_e`, `G`, `alpha` and others) with their CODATA 2022 uncertainty and the defining ones (`c`, `h`, `e`, `k_B`, `N_A`) exact. A formula names them like any other entry. The speed of light is exact, so the relative uncertainty of the rest energy is that of the electron mass.

Test: "use the physical constants in a formula" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Test two samples

HypothesisTest.welchT compares the means of two independent samples and HypothesisTest.oneSampleT tests one sample's mean against a value; both take the running moments of the samples. The data are the ten patients of the sleep experiment, measured under two drugs. Treated as independent the groups do not differ, and the paired differences do: pairing removes the variation between patients.

Test: "test two samples" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
let moments (xs: float list) = List.fold (Moments.add true) Moments.zero xs
let a = [ 0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0 ]
let b = [ 1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4 ]
let differences = [ -1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4 ]
let unpaired = HypothesisTest.welchT TwoSided (moments a) (moments b)
let paired = HypothesisTest.oneSampleT TwoSided 0.0 (moments differences)
Expect.isTrue (unpaired.PValue > 0.05) "treated as two groups, no effect shows"
Expect.isTrue (paired.PValue < 0.01) "paired, the effect shows"
```

## Adjust a family of p-values

Adjust.holm controls the family-wise error rate and Adjust.benjaminiHochberg the false discovery rate of a family of p-values. Each returns the adjusted values in the order given, under the definitions of R's `p.adjust`.

Test: "adjust a family of p-values" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
let p = [| 0.04; 0.001; 0.03; 0.2; 0.01 |]
let ulp = 2.220446049250313e-16

let within (expected: float[]) (actual: float[]) =
    Array.iter2 (fun e a -> Expect.isTrue (abs (a - e) <= 4.0 * ulp * abs e) "within 4 ulps") expected actual

within [| 0.09; 0.005; 0.09; 0.2; 0.04 |] (Adjust.holm p)
within [| 0.05; 0.005; 0.05; 0.2; 0.025 |] (Adjust.benjaminiHochberg p)
```

## Monitor a stream with a sequential test

A sequential test keeps a confidence sequence over a stream of observations. The sequence is valid at every count at once, so reading the verdict after each observation does not inflate the error rate; the guarantee is asymptotic, with no statement at any finite count, and the sequence shows no bounds before its burn-in count. SequentialTest.add folds in one observation, and `createStreamFold` keeps the test current as items arrive. The snippet tunes the sequence for 1000 observations at level 0.05 and feeds it 2000 draws of a normal distribution whose mean is 0.3, against the null of 0.

Test: "monitor a stream with a sequential test" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Keep a regression current over a projection

Aggregate.comomentsBy keeps the running co-moments of a projection of a keyed collection, and Regression.simple fits a line to them. An edit of one row subtracts the row's old pair and adds its new one, so the fit follows the collection without a pass over its rows.

Test: "keep a regression current over a projection" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Take the spectrum of a signal

Fourier.powerSpectrum returns the squared magnitudes of the first half of the transform of a real signal, and Fourier.frequencies the frequency of each bin for a given sample spacing. The signal here is a 5 Hz sine sampled at 64 Hz for one second.

Test: "take the spectrum of a signal" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
let samples = Array.init 64 (fun t -> sin (2.0 * Math.PI * 5.0 * float t / 64.0))
let power = Fourier.powerSpectrum samples
let peak = power |> Array.indexed |> Array.maxBy snd |> fst
Expect.equal peak 5 "the largest bin"
let frequencies = Fourier.frequencies 64 (1.0 / 64.0)
Expect.isTrue (abs (frequencies[5] - 5.0) <= 1e-12) "bin 5 is 5 Hz"
```

## Solve a linear system and a least-squares problem

Matrix.solve solves a square system by LU decomposition, and QrDecomposition.leastSquares fits an overdetermined one by QR. The least-squares problem is the line through four points, whose normal equations give the answer by hand.

Test: "solve a linear system and a least-squares problem" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
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
```

## Window a stream

Window.moments keeps the running moments of the last `n` items, with `Window.push` adding an item and removing the oldest. `createStreamFold` keeps a window current over a stream. The window of capacity 3, fed the numbers 1 to 5, holds the last three.

Test: "window a stream" in tests/Ranvier.Science.Tests/GuideTests.fs

```fsharp
withGraph (fun () ->
    let feed = ManualStream<float>()
    let latest = createStreamFold (Window.moments true 3) Window.push (fun _ -> feed.Source)
    latest.TryValue |> ignore

    for i in 1..5 do
        feed.Push (float i)

    let w = latest.Value
    Expect.isTrue (abs (Moments.mean (Window.value w) - 4.0) <= 1e-12) "the mean of the last three"
    Expect.equal (Seq.toList (Window.items w)) [ 3.0; 4.0; 5.0 ] "the last three items")
```
