namespace Ranvier.Numerics

open System

/// Settings of `Ode.solve` and `Ode.solveAt`. A step is accepted when the root-mean-square of
/// eᵢ / (AbsoluteTolerance + RelativeTolerance · max(|yᵢ|, |y_newᵢ|)) over the components is at most 1, with e the
/// embedded error estimate of the step.
type OdeOptions =
    {
        /// The absolute tolerance, at least 0; the two tolerances are not both 0.
        AbsoluteTolerance: float
        /// The relative tolerance, at least 0.
        RelativeTolerance: float
        /// The first step; 0 lets the solver choose it. Never below 0.
        InitialStep: float
        /// The largest step, above 0; `infinity` for no limit.
        MaximumStep: float
        /// The largest number of accepted plus rejected steps, at least 1.
        MaximumSteps: int
    }

/// Defaults for `OdeOptions`.
module OdeOptions =
    /// Both tolerances 1e-9, the starting step chosen by the solver, no step limit and at most 100 000 steps.
    let standard: OdeOptions =
        {
            AbsoluteTolerance = 1e-9
            RelativeTolerance = 1e-9
            InitialStep = 0.0
            MaximumStep = infinity
            MaximumSteps = 100_000
        }

/// The result of `Ode.solve`: the grid of accepted steps and the work done.
type OdeSolution =
    {
        /// t0 and the end of every accepted step, ascending; the last is t1 exactly.
        Times: float[]
        /// The state at each of `Times`; the first is a copy of y0. Each is a fresh array.
        States: float[][]
        /// The number of accepted steps.
        Accepted: int
        /// The number of rejected steps.
        Rejected: int
        /// The number of calls of the right-hand side.
        Evaluations: int
    }

/// Integration of y' = f(t, y) forward in time. Every function leaves its inputs untouched and returns fresh arrays,
/// an exception raised by the caller's function propagates unchanged, and a result of f whose length differs from the
/// state's raises `ArgumentException` naming both lengths.
module Ode =
    // The smallest normal double.
    let private smallestNormal = 2.2250738585072014e-308

    let private checkLength (n: int) (k: float[]) : float[] =
        if k.Length <> n then
            raise (ArgumentException(sprintf "The right-hand side returned %d components for a state of %d." k.Length n, "f"))

        k

    let private checkRange (t0: float) (t1: float) : unit =
        if not (t1 >= t0) then
            raise (ArgumentOutOfRangeException("t1", sprintf "The end t1 must be at least t0; t0 = %g, t1 = %g." t0 t1))

    let private checkOptions (options: OdeOptions) : unit =
        let fail (text: string) = raise (ArgumentOutOfRangeException("options", text))

        if not (options.AbsoluteTolerance >= 0.0) then
            fail (sprintf "AbsoluteTolerance must be at least 0; it is %g." options.AbsoluteTolerance)

        if not (options.RelativeTolerance >= 0.0) then
            fail (sprintf "RelativeTolerance must be at least 0; it is %g." options.RelativeTolerance)

        if options.AbsoluteTolerance = 0.0 && options.RelativeTolerance = 0.0 then
            fail "AbsoluteTolerance and RelativeTolerance are both 0."

        if not (options.MaximumStep > 0.0) then
            fail (sprintf "MaximumStep must be above 0; it is %g." options.MaximumStep)

        if not (options.InitialStep >= 0.0) then
            fail (sprintf "InitialStep must be at least 0; it is %g." options.InitialStep)

        if options.MaximumSteps < 1 then
            fail (sprintf "MaximumSteps must be at least 1; it is %d." options.MaximumSteps)

    /// One step of the classic fourth-order Runge–Kutta method from (t, y) with step h, any real h: k₁ = f(t, y),
    /// k₂ = f(t + h/2, y + h/2 · k₁), k₃ = f(t + h/2, y + h/2 · k₂), k₄ = f(t + h, y + h · k₃), and
    /// y + h/6 · (k₁ + 2k₂ + 2k₃ + k₄). Four evaluations of f. Exact kernel: it uses only +, −, × and ÷, so .NET and
    /// JavaScript give the same bits (pinned by the CrossTarget list). On y' = y it equals the Taylor polynomial of
    /// degree 4 (general knowledge).
    let rungeKutta4Step (f: float -> float[] -> float[]) (t: float) (y: float[]) (h: float) : float[] =
        let n = y.Length
        let half = h / 2.0
        let k1 = checkLength n (f t y)
        let y2 = Array.zeroCreate<float> n

        for i in 0 .. n - 1 do
            y2[i] <- y[i] + half * k1[i]

        let k2 = checkLength n (f (t + half) y2)
        let y3 = Array.zeroCreate<float> n

        for i in 0 .. n - 1 do
            y3[i] <- y[i] + half * k2[i]

        let k3 = checkLength n (f (t + half) y3)
        let y4 = Array.zeroCreate<float> n

        for i in 0 .. n - 1 do
            y4[i] <- y[i] + h * k3[i]

        let k4 = checkLength n (f (t + h) y4)
        let result = Array.zeroCreate<float> n

        for i in 0 .. n - 1 do
            result[i] <- y[i] + h / 6.0 * (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i])

        result

    /// The state at t1 by `steps` equal RK4 steps of h = (t1 − t0)/steps, the k-th from tₖ = t0 + k·h. Raises
    /// `ArgumentOutOfRangeException` for steps below 1 or t1 below t0 (a NaN t1 included). Exact kernel, as
    /// `rungeKutta4Step`. 4 · steps evaluations of f; the error is O(h⁴) (general knowledge), and the "RK4 is fourth
    /// order" test of the "Ode" list measures an error ratio between 14 and 18 on halving h for y' = y on [0, 1].
    let rungeKutta4 (f: float -> float[] -> float[]) (t0: float) (y0: float[]) (t1: float) (steps: int) : float[] =
        if steps < 1 then
            raise (ArgumentOutOfRangeException("steps", sprintf "The number of steps must be at least 1; it is %d." steps))

        checkRange t0 t1
        let h = (t1 - t0) / float steps
        let mutable y = y0

        for k in 0 .. steps - 1 do
            y <- rungeKutta4Step f (t0 + float k * h) y h

        y

    // From documentation: Hairer, Nørsett and Wanner, Solving Ordinary Differential Equations I, 2nd edition (Springer,
    // 1993), Table II.5.2, the Dormand–Prince 5(4) pair. The nodes c₂…c₆ (c₇ = 1), the matrix a, the fifth-order weights
    // b (the last row of a, first-same-as-last) and the error weights e = b − b̂ (as in dopri5.f).
    let private c2 = 1.0 / 5.0
    let private c3 = 3.0 / 10.0
    let private c4 = 4.0 / 5.0
    let private c5 = 8.0 / 9.0

    let private a21 = 1.0 / 5.0
    let private a31 = 3.0 / 40.0
    let private a32 = 9.0 / 40.0
    let private a41 = 44.0 / 45.0
    let private a42 = -56.0 / 15.0
    let private a43 = 32.0 / 9.0
    let private a51 = 19372.0 / 6561.0
    let private a52 = -25360.0 / 2187.0
    let private a53 = 64448.0 / 6561.0
    let private a54 = -212.0 / 729.0
    let private a61 = 9017.0 / 3168.0
    let private a62 = -355.0 / 33.0
    let private a63 = 46732.0 / 5247.0
    let private a64 = 49.0 / 176.0
    let private a65 = -5103.0 / 18656.0
    let private b1 = 35.0 / 384.0
    let private b3 = 500.0 / 1113.0
    let private b4 = 125.0 / 192.0
    let private b5 = -2187.0 / 6784.0
    let private b6 = 11.0 / 84.0

    let private e1 = 71.0 / 57600.0
    let private e3 = -71.0 / 16695.0
    let private e4 = 71.0 / 1920.0
    let private e5 = -17253.0 / 339200.0
    let private e6 = 22.0 / 525.0
    let private e7 = -1.0 / 40.0

    // From documentation: the dense-output coefficients d₁, d₃…d₇ of §II.6 (Hairer et al., ibid.) as coded in dopri5.f.
    let private d1 = -12715105075.0 / 11282082432.0
    let private d3 = 87487479700.0 / 32700410799.0
    let private d4 = -10690763975.0 / 1880347072.0
    let private d5 = 701980252875.0 / 199316789632.0
    let private d6 = -1453857185.0 / 822651844.0
    let private d7 = 69997945.0 / 29380423.0

    // An accepted step [Start, End] of size Step, the state at its end, and the five coefficient vectors of its dense
    // output (empty when none was requested).
    type private Segment =
        {
            Start: float
            Step: float
            End: float
            Final: float[]
            Dense: float[][]
        }

    // dopri5.f contd5: the fourth-order continuous extension on a segment, at a time inside it.
    let private denseAt (segment: Segment) (t: float) : float[] =
        let s = (t - segment.Start) / segment.Step
        let s1 = 1.0 - s
        let r = segment.Dense
        Array.init r[0].Length (fun i -> r[0][i] + s * (r[1][i] + s1 * (r[2][i] + s * (r[3][i] + s1 * r[4][i]))))

    // Root mean square of v[i] / scale[i]; 0 for no components.
    let private rms (v: float[]) (scale: float[]) : float =
        let n = v.Length

        if n = 0 then
            0.0
        else
            let mutable sum = 0.0

            for i in 0 .. n - 1 do
                let q = v[i] / scale[i]
                sum <- sum + q * q

            sqrt (sum / float n)

    // Hairer et al., ibid., §II.4: a first step from the sizes of y and f at the start and of f's change over a trial
    // Euler step. The result may be NaN or non-positive when f is; the caller falls back.
    let private startingStep
        (options: OdeOptions)
        (call: float -> float[] -> float[])
        (t0: float)
        (y0: float[])
        (f0: float[])
        (t1: float)
        : float =
        let n = y0.Length
        let scale = Array.init n (fun i -> options.AbsoluteTolerance + options.RelativeTolerance * abs y0[i])
        let d0 = rms y0 scale
        let d1 = rms f0 scale
        let guess = if d0 < 1e-5 || d1 < 1e-5 then 1e-6 else 0.01 * d0 / d1
        let h0 = min guess (min options.MaximumStep (t1 - t0))
        let y1 = Array.init n (fun i -> y0[i] + h0 * f0[i])
        let f1 = call (t0 + h0) y1
        let d2 = rms (Array.init n (fun i -> f1[i] - f0[i])) scale / h0
        let d12 = max d1 d2

        let h1 =
            if d12 <= 1e-15 then
                max 1e-6 (h0 * 1e-3)
            else
                Math.Pow(0.01 / d12, 1.0 / 5.0)

        min (100.0 * h0) h1

    // Dormand–Prince 5(4) from t0 to t1 (t0 < t1), with first-same-as-last. Records the grid when `record`, and passes
    // each accepted step to `onStep`, with its dense output when `dense`.
    let private integrate
        (options: OdeOptions)
        (f: float -> float[] -> float[])
        (t0: float)
        (y0: float[])
        (t1: float)
        (record: bool)
        (dense: bool)
        (onStep: Segment -> unit)
        : Result<OdeSolution, string> =
        let n = y0.Length
        let evaluations = ref 0

        let call (t: float) (y: float[]) : float[] =
            evaluations.Value <- evaluations.Value + 1
            Array.copy (checkLength n (f t y))

        let times = ResizeArray<float>()
        let states = ResizeArray<float[]>()

        if record then
            times.Add t0
            states.Add(Array.copy y0)

        let mutable t = t0
        let mutable y = Array.copy y0
        let mutable accepted = 0
        let mutable rejected = 0
        let mutable failure: string option = None

        if t1 > t0 then
            let mutable k1 = call t0 y0

            let start =
                if options.InitialStep > 0.0 then
                    options.InitialStep
                else
                    let guess = startingStep options call t0 y0 k1 t1
                    if Doubles.isFinite guess && guess > 0.0 then guess else 1e-6 * (t1 - t0)

            let mutable h = start
            let mutable afterRejection = false
            // What was not finite in the last step, when that rejected it: its new state or its error estimate.
            let mutable nonFinite: string option = None

            while failure.IsNone && t < t1 do
                if accepted + rejected >= options.MaximumSteps then
                    failure <- Some(sprintf "MaximumSteps (%d) reached at t = %g" options.MaximumSteps t)
                else
                    let proposed = min h options.MaximumStep

                    if not (proposed > 4.0 * Doubles.epsilon * abs t && proposed >= smallestNormal) then
                        failure <-
                            Some(
                                match nonFinite with
                                | Some what -> sprintf "%s is not finite at t = %g" what t
                                | None -> sprintf "step size underflow at t = %g" t
                            )
                    else
                        let last = t + 1.01 * proposed >= t1 && t1 - t <= options.MaximumStep
                        let step = if last then t1 - t else proposed
                        let tNew = if last then t1 else t + step

                        let y2 = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            y2[i] <- y[i] + step * (a21 * k1[i])

                        let k2 = call (t + c2 * step) y2
                        let y3 = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            y3[i] <- y[i] + step * (a31 * k1[i] + a32 * k2[i])

                        let k3 = call (t + c3 * step) y3
                        let y4 = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            y4[i] <- y[i] + step * (a41 * k1[i] + a42 * k2[i] + a43 * k3[i])

                        let k4 = call (t + c4 * step) y4
                        let y5 = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            y5[i] <- y[i] + step * (a51 * k1[i] + a52 * k2[i] + a53 * k3[i] + a54 * k4[i])

                        let k5 = call (t + c5 * step) y5
                        let y6 = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            y6[i] <- y[i] + step * (a61 * k1[i] + a62 * k2[i] + a63 * k3[i] + a64 * k4[i] + a65 * k5[i])

                        let k6 = call (t + step) y6
                        let yNew = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            yNew[i] <- y[i] + step * (b1 * k1[i] + b3 * k3[i] + b4 * k4[i] + b5 * k5[i] + b6 * k6[i])

                        let k7 = call tNew yNew
                        let estimate = Array.zeroCreate<float> n
                        let scale = Array.zeroCreate<float> n

                        for i in 0 .. n - 1 do
                            estimate[i] <- step * (e1 * k1[i] + e3 * k3[i] + e4 * k4[i] + e5 * k5[i] + e6 * k6[i] + e7 * k7[i])
                            scale[i] <- options.AbsoluteTolerance + options.RelativeTolerance * max (abs y[i]) (abs yNew[i])

                        let err = rms estimate scale
                        let mutable finiteState = true
                        let mutable finiteEstimate = true

                        for i in 0 .. n - 1 do
                            if not (Doubles.isFinite yNew[i]) then finiteState <- false
                            if not (Doubles.isFinite estimate[i]) then finiteEstimate <- false

                        if not finiteState || not (Doubles.isFinite err) then
                            h <- step / 5.0
                            rejected <- rejected + 1
                            afterRejection <- true

                            nonFinite <-
                                if not finiteState then Some "the state"
                                elif not finiteEstimate then Some "the right-hand side"
                                else None
                        else
                            nonFinite <- None

                            let factor =
                                if err = 0.0 then
                                    5.0
                                else
                                    min 5.0 (max 0.2 (0.9 * Math.Pow(err, -1.0 / 5.0)))

                            if err <= 1.0 then
                                if record then
                                    times.Add tNew
                                    states.Add yNew

                                let continuous =
                                    if dense then
                                        let diff = Array.zeroCreate<float> n
                                        let spline = Array.zeroCreate<float> n
                                        let r4 = Array.zeroCreate<float> n
                                        let r5 = Array.zeroCreate<float> n

                                        for i in 0 .. n - 1 do
                                            diff[i] <- yNew[i] - y[i]
                                            spline[i] <- step * k1[i] - diff[i]
                                            r4[i] <- diff[i] - step * k7[i] - spline[i]

                                            r5[i] <-
                                                step * (d1 * k1[i] + d3 * k3[i] + d4 * k4[i] + d5 * k5[i] + d6 * k6[i] + d7 * k7[i])

                                        [| y; diff; spline; r4; r5 |]
                                    else
                                        [||]

                                onStep
                                    {
                                        Start = t
                                        Step = step
                                        End = tNew
                                        Final = yNew
                                        Dense = continuous
                                    }

                                accepted <- accepted + 1
                                t <- tNew
                                y <- yNew
                                k1 <- k7
                                h <- step * (if afterRejection then min 1.0 factor else factor)
                                afterRejection <- false
                            else
                                h <- step * factor
                                rejected <- rejected + 1
                                afterRejection <- true

        match failure with
        | Some text -> Error text
        | None ->
            Ok
                {
                    Times = times.ToArray()
                    States = states.ToArray()
                    Accepted = accepted
                    Rejected = rejected
                    Evaluations = evaluations.Value
                }

    /// Integrates y' = f(t, y) from (t0, y0) to t1 by the Dormand–Prince 5(4) pair (Hairer, Nørsett and Wanner, Solving
    /// Ordinary Differential Equations I, 2nd edition, Table II.5.2; from documentation) with the first stage reused
    /// from the last. The step is accepted when the options' error norm is at most 1; the next step is
    /// h · min(5, max(0.2, 0.9 · err^(−1/5))), not above h straight after a rejection, and a non-finite norm rejects
    /// with h/5, as does a new state with a non-finite component. The last step is shortened to end at t1 exactly. An
    /// `InitialStep` of 0 uses the starting-step rule of
    /// ibid. §II.4, and 1e-6 · (t1 − t0) when that is not a positive finite number.
    ///
    /// Returns `Error` when a step is not above 4ε|t| (ε = 2⁻⁵²) or below the smallest normal double, with "the state is
    /// not finite at t = …" when the step rejected before it had a new state with a NaN or infinite component (a NaN in
    /// y0, or a state driven past the largest double), "the right-hand side is not finite at t = …" when that step's
    /// error estimate was not finite, and "step size underflow at t = …" otherwise; and with "MaximumSteps (n) reached
    /// at t = …" when the accepted plus rejected steps reach `MaximumSteps`. A right-hand side that blows up or returns
    /// NaN ends in one of these. Raises `ArgumentOutOfRangeException` for t1 below t0 (NaN included) and for options
    /// with a negative or NaN tolerance, both tolerances 0, a `MaximumStep` not above 0, a negative or NaN
    /// `InitialStep` or `MaximumSteps` below 1. t1 = t0 gives the one time t0 and a copy of y0 without calling f.
    ///
    /// Six evaluations of f per step after the first, one more for the starting step.
    ///
    /// The step control bounds the local error norm of each step, and the global error is the accumulation of those, so
    /// no tolerance bounds it in general. On the tested closed-form problems (y' = −y, the harmonic oscillator, the
    /// logistic equation and y' = cos t, with both tolerances 1e-10) the global error was at most
    /// 2 · (AbsoluteTolerance + RelativeTolerance · |exact|) per component (measured here: the "solve meets the
    /// tolerance on closed forms" case of the "Ode" list, `dotnet run -c Release --project tests/Ranvier.Science.Tests
    /// -- --filter "Ode"`).
    let solve
        (options: OdeOptions)
        (f: float -> float[] -> float[])
        (t0: float)
        (y0: float[])
        (t1: float)
        : Result<OdeSolution, string> =
        checkOptions options
        checkRange t0 t1
        integrate options f t0 y0 t1 true false ignore

    /// The states at `times`, ascending and none below t0, by one integration to the last time with the options of
    /// `solve`, evaluating the fourth-order continuous extension of the Dormand–Prince step (Hairer et al., ibid.
    /// §II.6, dopri5.f; from documentation) inside the accepted step that contains each time. A time equal to t0 reads a
    /// copy of y0 and a time equal to the end of an accepted step reads that step's state. Returns `[||]` for no times,
    /// without calling f, and `Error` as `solve` does. Raises `ArgumentOutOfRangeException` for times unsorted, below t0
    /// or NaN, and for invalid options.
    ///
    /// The cost is that of `solve` to the last time, which the accepted steps set and not the number of times, plus
    /// O(n) per time and no further evaluations of f. A state at an interior time has an error of at most ten times the
    /// tolerance on the tested problems (measured here: the "solveAt on the oscillator" and "dense output reproduces a
    /// quartic" cases of the "Ode" list), a bound the continuous extension does not guarantee in general.
    let solveAt
        (options: OdeOptions)
        (f: float -> float[] -> float[])
        (t0: float)
        (y0: float[])
        (times: float[])
        : Result<float[][], string> =
        checkOptions options

        for i in 0 .. times.Length - 1 do
            let floor = if i = 0 then t0 else times[i - 1]

            if not (times[i] >= floor) then
                raise (
                    ArgumentOutOfRangeException(
                        "times",
                        sprintf "The times must ascend from t0 = %g; times[%d] = %g follows %g." t0 i times[i] floor
                    )
                )

        if times.Length = 0 then
            Ok [||]
        else
            let result = Array.zeroCreate<float[]> times.Length
            let next = ref 0

            while next.Value < times.Length && times[next.Value] = t0 do
                result[next.Value] <- Array.copy y0
                next.Value <- next.Value + 1

            let serve (segment: Segment) : unit =
                while next.Value < times.Length && times[next.Value] <= segment.End do
                    let t = times[next.Value]

                    result[next.Value] <-
                        if t = segment.End then Array.copy segment.Final else denseAt segment t

                    next.Value <- next.Value + 1

            match integrate options f t0 y0 times[times.Length - 1] false true serve with
            | Ok _ -> Ok result
            | Error text -> Error text
