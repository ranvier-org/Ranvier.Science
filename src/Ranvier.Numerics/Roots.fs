namespace Ranvier.Numerics

open System

/// The result of `Minimize.brent`. `Converged` is false when the iteration cap was reached or the function returned
/// NaN; `X` and `Value` are then NaN or the last iterate, as that function's documentation says.
type Minimum =
    {
        /// The abscissa of the minimum found.
        X: float
        /// The function value at `X`.
        Value: float
        /// The number of iterations performed.
        Iterations: int
        /// True when the search met its tolerance before the cap.
        Converged: bool
    }

/// The result of `Minimize.nelderMead`.
type MinimumN =
    {
        /// The best vertex found: a fresh array.
        X: float[]
        /// The function value at `X`; +∞ when every vertex gave NaN or +∞.
        Value: float
        /// The number of reflection cycles performed.
        Iterations: int
        /// The number of calls of the function.
        Evaluations: int
        /// True when both simplex tests were met before the cap.
        Converged: bool
    }

/// Options of `Minimize.nelderMead`.
type NelderMeadOptions =
    {
        /// Relative tolerance of both convergence tests; at least 0.
        Tolerance: float
        /// The offset of vertex i + 1 from the start along axis i; greater than 0.
        InitialStep: float
        /// The cap on reflection cycles; at least 1.
        MaximumIterations: int
    }

module NelderMeadOptions =
    /// Tolerance 1e-8, initial step 0.1, at most 20 000 iterations.
    let standard: NelderMeadOptions =
        {
            Tolerance = 1e-8
            InitialStep = 0.1
            MaximumIterations = 20_000
        }

/// Root finding in one variable. `tolerance` is absolute in x and at least 0; a negative or NaN tolerance raises
/// `ArgumentOutOfRangeException`, and so does an infinite endpoint. The bracketing searches accept the endpoints in
/// either order and give the same bits for both. A NaN endpoint, a NaN function value or an unbracketed interval gives
/// an `Error` naming the condition. An exception raised by the caller's function propagates unchanged.
module Roots =
    let private checkTolerance (tolerance: float) : unit =
        if not (tolerance >= 0.0) then
            raise (ArgumentOutOfRangeException("tolerance", sprintf "The tolerance must be at least 0; it is %g." tolerance))

    let private checkEndpoints (a: float) (b: float) : unit =
        if Double.IsInfinity a || Double.IsInfinity b then
            raise (ArgumentOutOfRangeException("a", sprintf "The endpoints must be finite; a = %g, b = %g." a b))

    let private notBracketed (a: float) (fa: float) (b: float) (fb: float) : Result<float, string> =
        Error(sprintf "not bracketed: f(%.17g) = %.17g and f(%.17g) = %.17g have the same sign" a fa b fb)

    let private nanAt (x: float) : Result<float, string> =
        Error(sprintf "f returned NaN at %.17g" x)

    /// Opens a bracketing search: Some result when it is already decided, None with the ordered endpoints and their
    /// values otherwise.
    let private startSearch (f: float -> float) (a: float) (b: float) : Result<float, string> option * (float * float * float * float) =
        let lo = if a <= b then a else b
        let hi = if a <= b then b else a

        if Double.IsNaN a || Double.IsNaN b then
            Some(Error(sprintf "an endpoint is NaN: a = %.17g, b = %.17g" a b)), (lo, hi, nan, nan)
        else
            let flo = f lo

            if Double.IsNaN flo then
                Some(nanAt lo), (lo, hi, flo, nan)
            elif flo = 0.0 then
                Some(Ok lo), (lo, hi, flo, nan)
            else
                let fhi = f hi

                if Double.IsNaN fhi then Some(nanAt hi), (lo, hi, flo, fhi)
                elif fhi = 0.0 then Some(Ok hi), (lo, hi, flo, fhi)
                elif (flo > 0.0) = (fhi > 0.0) then Some(notBracketed lo flo hi fhi), (lo, hi, flo, fhi)
                else None, (lo, hi, flo, fhi)

    /// A root of f in [a, b] by bisection. Needs f a and f b of opposite signs, or one of them exactly 0 (that
    /// endpoint is returned). Halves the bracket until its width is at most the tolerance or its midpoint equals an
    /// endpoint, so with tolerance 0 it ends on two adjacent doubles; returns the endpoint where |f| is smaller. At
    /// most 2200 iterations, enough to halve the whole double range (a definition), then an `Error` naming the cap;
    /// cost one evaluation of f per iteration. Error when the endpoints are not bracketing ("not bracketed") or f
    /// returns NaN ("f returned NaN").
    let bisection (tolerance: float) (f: float -> float) (a: float) (b: float) : Result<float, string> =
        checkTolerance tolerance
        checkEndpoints a b

        match startSearch f a b with
        | Some decided, _ -> decided
        | None, (lo, hi, flo, fhi) ->
            let mutable a = lo
            let mutable b = hi
            let mutable fa = flo
            let mutable fb = fhi
            let mutable result: Result<float, string> option = None
            let mutable iterations = 0

            while result.IsNone && iterations < 2200 do
                iterations <- iterations + 1
                let half = (b - a) / 2.0
                // b - a overflows only when the endpoints are opposite and huge: halve each before adding.
                let m = if Double.IsInfinity half then a / 2.0 + b / 2.0 else a + half

                if m <= a || m >= b then
                    result <- Some(Ok(if abs fa <= abs fb then a else b))
                else
                    let fm = f m

                    if Double.IsNaN fm then
                        result <- Some(nanAt m)
                    elif fm = 0.0 then
                        result <- Some(Ok m)
                    else
                        if (fm > 0.0) = (fa > 0.0) then
                            a <- m
                            fa <- fm
                        else
                            b <- m
                            fb <- fm

                        if b - a <= tolerance then
                            result <- Some(Ok(if abs fa <= abs fb then a else b))

            match result with
            | Some r -> r
            | None -> Error "bisection did not converge within 2200 iterations"

    /// A root of f in [a, b] by Brent's zeroin (Brent 1973, Algorithms for Minimization without Derivatives, chapter
    /// 4: bisection, secant and inverse quadratic interpolation; from documentation). Needs f a and f b of opposite
    /// signs, or one of them exactly 0 (that endpoint is returned). Stops when the bracket's half-width is at most
    /// 2ε|b| + tolerance/2 with ε = 2⁻⁵² (the netlib zeroin.f form, with the term floored at the smallest positive
    /// double so that tolerance 0 still ends), so the final bracket is at most tolerance + 4ε|x| wide. At most 1000
    /// iterations (a definition), then an `Error` naming the cap; one evaluation of f per iteration. A bracket whose
    /// width exceeds the largest double is first bisected at a/2 + b/2 until its width is finite, those evaluations
    /// counting toward the cap, so an `Ok` value is always finite. A root of multiplicity above 1 converges only
    /// linearly and may reach the cap at small tolerances. Error when the endpoints are not bracketing
    /// ("not bracketed") or f returns NaN ("f returned NaN").
    let brent (tolerance: float) (f: float -> float) (a: float) (b: float) : Result<float, string> =
        checkTolerance tolerance
        checkEndpoints a b

        match startSearch f a b with
        | Some decided, _ -> decided
        | None, (lo, hi, flo, fhi) ->
            let mutable a = lo
            let mutable b = hi
            let mutable fa = flo
            let mutable fb = fhi
            let mutable result: Result<float, string> option = None
            let mutable iterations = 0

            // A bracket wider than the largest double is bisected at a/2 + b/2 until its width is finite.
            while result.IsNone && Double.IsInfinity(b - a) && iterations < 1000 do
                iterations <- iterations + 1
                let m = a / 2.0 + b / 2.0
                let fm = f m

                if Double.IsNaN fm then
                    result <- Some(nanAt m)
                elif fm = 0.0 then
                    result <- Some(Ok m)
                elif (fm > 0.0) = (fa > 0.0) then
                    a <- m
                    fa <- fm
                else
                    b <- m
                    fb <- fm

            let mutable c = a
            let mutable fc = fa
            let mutable d = b - a
            let mutable e = d

            while result.IsNone && iterations < 1000 do
                iterations <- iterations + 1

                if (fb > 0.0) = (fc > 0.0) then
                    c <- a
                    fc <- fa
                    d <- b - a
                    e <- d

                if abs fc < abs fb then
                    a <- b
                    b <- c
                    c <- a
                    fa <- fb
                    fb <- fc
                    fc <- fa

                let tol1 = max (2.0 * Doubles.epsilon * abs b + 0.5 * tolerance) Double.Epsilon
                let m = 0.5 * (c - b)

                if abs m <= tol1 || fb = 0.0 then
                    result <- Some(Ok b)
                else
                    if abs e < tol1 || abs fa <= abs fb then
                        d <- m
                        e <- m
                    else
                        let s = fb / fa
                        let mutable p = 0.0
                        let mutable q = 0.0

                        if a = c then
                            p <- 2.0 * m * s
                            q <- 1.0 - s
                        else
                            let qq = fa / fc
                            let r = fb / fc
                            p <- s * (2.0 * m * qq * (qq - r) - (b - a) * (r - 1.0))
                            q <- (qq - 1.0) * (r - 1.0) * (s - 1.0)

                        if p > 0.0 then q <- -q else p <- -p
                        let previous = e
                        e <- d

                        if 2.0 * p < 3.0 * m * q - abs (tol1 * q) && p < abs (0.5 * previous * q) then
                            d <- p / q
                        else
                            d <- m
                            e <- m

                    a <- b
                    fa <- fb
                    b <- b + (if abs d > tol1 then d elif m > 0.0 then tol1 else -tol1)
                    fb <- f b

                    if Double.IsNaN fb then result <- Some(nanAt b)

            match result with
            | Some r -> r
            | None -> Error "brent did not converge within 1000 iterations"

    /// A root of f from x0 by Newton's method with derivative df. Stops with Ok when f is exactly 0 at the iterate, or
    /// when the step is at most the tolerance or at most 4ε|x| with ε = 2⁻⁵² (the floor lets tolerance 0 end), or
    /// when the step no longer moves the iterate. At most 100 iterations (a definition), then an `Error` naming the
    /// cap; two evaluations (f and df) per iteration. Error on a zero or non-finite derivative ("derivative"), a NaN
    /// value of f ("f returned NaN") or an iterate that leaves the finite numbers ("not finite"). Convergence is only
    /// local: a start far from the root may fail or find another root.
    let newton (tolerance: float) (f: float -> float) (df: float -> float) (x0: float) : Result<float, string> =
        checkTolerance tolerance
        let mutable x = x0
        let mutable result: Result<float, string> option = None
        let mutable iterations = 0

        while result.IsNone && iterations < 100 do
            iterations <- iterations + 1
            let fx = f x

            if Double.IsNaN fx then
                result <- Some(nanAt x)
            elif fx = 0.0 then
                result <- Some(Ok x)
            else
                let dfx = df x

                if dfx = 0.0 || not (Doubles.isFinite dfx) then
                    result <- Some(Error(sprintf "the derivative is zero or not finite at %.17g (df = %.17g)" x dfx))
                else
                    let step = fx / dfx
                    let next = x - step

                    if not (Doubles.isFinite next) then
                        result <- Some(Error(sprintf "the iterate is not finite after x = %.17g" x))
                    elif abs step <= max tolerance (4.0 * Doubles.epsilon * abs next) || next = x then
                        result <- Some(Ok next)
                    else
                        x <- next

        match result with
        | Some r -> r
        | None -> Error "newton did not converge within 100 iterations"

/// Minimisation of a function of one or several variables. A negative or NaN tolerance raises
/// `ArgumentOutOfRangeException`. An exception raised by the caller's function propagates unchanged.
module Minimize =
    /// A local minimum of f on [a, b] by Brent's localmin (Brent 1973, Algorithms for Minimization without
    /// Derivatives, chapter 5: golden section and parabolic interpolation; from documentation). `tolerance` is the
    /// absolute part t of the stopping rule: it stops when the interval around the best point is within
    /// 2(√ε|x| + t/3), ε = 2⁻⁵². Needs finite a < b with b − a finite, else raises `ArgumentOutOfRangeException`; a NaN endpoint gives NaN
    /// fields with `Converged = false`. At most 500 iterations (a definition), then `Converged = false` with the best
    /// point so far; one evaluation of f per iteration. When f returns NaN the search returns at once with
    /// `Converged = false` and both `X` and `Value` NaN, since comparisons with NaN would otherwise misdirect it. The
    /// minimum is local, f need not be unimodal, and an endpoint is never evaluated. A minimum at exactly 0, such as
    /// x on [0, 1], has no relative scale: at a very small tolerance the search reaches the cap and reports
    /// `Converged = false`.
    let brent (tolerance: float) (f: float -> float) (a: float) (b: float) : Minimum =
        if not (tolerance >= 0.0) then
            raise (ArgumentOutOfRangeException("tolerance", sprintf "The tolerance must be at least 0; it is %g." tolerance))

        if Double.IsNaN a || Double.IsNaN b then
            { X = nan; Value = nan; Iterations = 0; Converged = false }
        else
            if Double.IsInfinity a || Double.IsInfinity b || not (a < b) || Double.IsInfinity(b - a) then
                raise (ArgumentOutOfRangeException("a", sprintf "The interval needs finite endpoints with a < b and a finite width; a = %g, b = %g." a b))

            let golden = 0.5 * (3.0 - sqrt 5.0)
            let eps = sqrt Doubles.epsilon
            let tol3 = tolerance / 3.0
            let mutable a = a
            let mutable b = b
            let mutable v = a + golden * (b - a)
            let mutable w = v
            let mutable x = v
            let mutable e = 0.0
            let mutable d = 0.0
            let mutable fx = f x
            let mutable fv = fx
            let mutable fw = fx
            let mutable iterations = 0
            let mutable converged = false
            let mutable failed = Double.IsNaN fx

            while not failed && not converged && iterations < 500 do
                let xm = 0.5 * (a + b)
                let tol1 = max (eps * abs x + tol3) Double.Epsilon
                let tol2 = 2.0 * tol1

                if abs (x - xm) <= tol2 - 0.5 * (b - a) then
                    converged <- true
                else
                    iterations <- iterations + 1
                    let mutable useGolden = true

                    if abs e > tol1 then
                        let mutable r = (x - w) * (fx - fv)
                        let mutable q = (x - v) * (fx - fw)
                        let mutable p = (x - v) * q - (x - w) * r
                        q <- 2.0 * (q - r)

                        if q > 0.0 then p <- -p
                        q <- abs q
                        r <- e
                        e <- d

                        if abs p < abs (0.5 * q * r) && p > q * (a - x) && p < q * (b - x) then
                            d <- p / q
                            let u = x + d

                            if u - a < tol2 || b - u < tol2 then d <- (if xm - x >= 0.0 then tol1 else -tol1)
                            useGolden <- false

                    if useGolden then
                        e <- (if x >= xm then a - x else b - x)
                        d <- golden * e

                    let u = if abs d >= tol1 then x + d elif d > 0.0 then x + tol1 else x - tol1
                    let fu = f u

                    if Double.IsNaN fu then
                        failed <- true
                    elif fu <= fx then
                        if u >= x then a <- x else b <- x
                        v <- w
                        fv <- fw
                        w <- x
                        fw <- fx
                        x <- u
                        fx <- fu
                    else
                        if u < x then a <- u else b <- u

                        if fu <= fw || w = x then
                            v <- w
                            fv <- fw
                            w <- u
                            fw <- fu
                        elif fu <= fv || v = x || v = w then
                            v <- u
                            fv <- fu

            if failed then
                { X = nan; Value = nan; Iterations = iterations; Converged = false }
            else
                { X = x; Value = fx; Iterations = iterations; Converged = converged }

    let private orderedValue (v: float) : float = if Double.IsNaN v then Double.PositiveInfinity else v

    /// A local minimum of f from x0 by the Nelder–Mead simplex method with reflection 1, expansion 2, contraction ½
    /// and shrink ½ (Lagarias et al. 1998, general knowledge). The simplex starts at x0 and at x0 plus
    /// `InitialStep` along each axis; a NaN value of f is ordered as +∞. It converges when, over all vertices, the
    /// largest |fᵢ − f_best| is at most Tolerance · max(1, |f_best|) and the largest ‖xᵢ − x_best‖∞ is at most
    /// Tolerance · max(1, ‖x_best‖∞). An iteration is one reflection cycle (reflect, then expand, contract or shrink);
    /// When the best vertex is +∞ (every vertex NaN or +∞) it stops at once with `Converged = false`.
    /// `Evaluations` counts calls of f. At most `MaximumIterations` iterations, then `Converged = false` with the best
    /// vertex so far. The method is derivative-free and local: it may stall on a non-smooth function or stop on a
    /// flat simplex away from a minimum. x0 is not modified and `X` is a fresh array. Raises `ArgumentException` for
    /// an empty x0, and `ArgumentOutOfRangeException` for a Tolerance below 0 or NaN, an InitialStep not above 0 or a
    /// MaximumIterations below 1. Cost O(n²) per iteration (centroid, convergence test, shrink and worst-case
    /// ordering of the n + 1 vertices) besides the evaluations of f (one or two, or n + 1 on a shrink).
    let nelderMead (options: NelderMeadOptions) (f: float[] -> float) (x0: float[]) : MinimumN =
        if x0.Length = 0 then
            raise (ArgumentException("The start x0 must have at least one coordinate.", "x0"))

        if not (options.Tolerance >= 0.0) then
            raise (ArgumentOutOfRangeException("options", sprintf "Tolerance must be at least 0; it is %g." options.Tolerance))

        if not (options.InitialStep > 0.0) then
            raise (ArgumentOutOfRangeException("options", sprintf "InitialStep must be above 0; it is %g." options.InitialStep))

        if options.MaximumIterations < 1 then
            raise (ArgumentOutOfRangeException("options", sprintf "MaximumIterations must be at least 1; it is %d." options.MaximumIterations))

        let n = x0.Length
        let evaluations = ref 0

        let evaluate (x: float[]) : float =
            evaluations.Value <- evaluations.Value + 1
            orderedValue (f (Array.copy x))

        let vertices: float[][] =
            Array.init (n + 1) (fun i ->
                let x = Array.copy x0
                if i > 0 then x[i - 1] <- x[i - 1] + options.InitialStep
                x)

        let values: float[] = Array.map evaluate vertices

        // Insertion sort by value: stable, so both targets order ties alike.
        let order () : unit =
            for i in 1..n do
                let x = vertices[i]
                let v = values[i]
                let mutable j = i - 1

                while j >= 0 && values[j] > v do
                    vertices[j + 1] <- vertices[j]
                    values[j + 1] <- values[j]
                    j <- j - 1

                vertices[j + 1] <- x
                values[j + 1] <- v

        let converged () : bool =
            let best = vertices[0]
            let fBest = values[0]
            let mutable valueSpread = 0.0
            let mutable pointSpread = 0.0
            let mutable scale = 0.0

            for j in 0 .. n - 1 do
                scale <- max scale (abs best[j])

            for i in 1..n do
                valueSpread <- max valueSpread (abs (values[i] - fBest))

                if Double.IsNaN(values[i] - fBest) then valueSpread <- Double.PositiveInfinity

                for j in 0 .. n - 1 do
                    pointSpread <- max pointSpread (abs (vertices[i][j] - best[j]))

            valueSpread <= options.Tolerance * max 1.0 (abs fBest)
            && pointSpread <= options.Tolerance * max 1.0 scale

        let replaceWorst (x: float[]) (v: float) : unit =
            vertices[n] <- x
            values[n] <- v

        let mutable iterations = 0
        let mutable finished = false
        let mutable isConverged = false

        while not finished do
            order ()

            if values[0] = Double.PositiveInfinity then
                // every vertex is NaN or +∞: nothing to converge to
                finished <- true
            elif converged () then
                isConverged <- true
                finished <- true
            elif iterations >= options.MaximumIterations then
                finished <- true
            else
                iterations <- iterations + 1
                let worst = vertices[n]
                let fWorst = values[n]
                let centroid = Array.zeroCreate n

                for i in 0 .. n - 1 do
                    for j in 0 .. n - 1 do
                        centroid[j] <- centroid[j] + vertices[i][j]

                for j in 0 .. n - 1 do
                    centroid[j] <- centroid[j] / float n

                let along (t: float) : float[] =
                    Array.init n (fun j -> centroid[j] + t * (centroid[j] - worst[j]))

                let reflected = along 1.0
                let fReflected = evaluate reflected

                let shrink () : unit =
                    let best = vertices[0]

                    for i in 1..n do
                        vertices[i] <- Array.init n (fun j -> best[j] + 0.5 * (vertices[i][j] - best[j]))
                        values[i] <- evaluate vertices[i]

                if fReflected < values[0] then
                    let expanded = along 2.0
                    let fExpanded = evaluate expanded

                    if fExpanded < fReflected then replaceWorst expanded fExpanded
                    else replaceWorst reflected fReflected
                elif fReflected < values[n - 1] then
                    replaceWorst reflected fReflected
                elif fReflected < fWorst then
                    let contracted = along 0.5
                    let fContracted = evaluate contracted

                    if fContracted <= fReflected then replaceWorst contracted fContracted else shrink ()
                else
                    let contracted = along (-0.5)
                    let fContracted = evaluate contracted

                    if fContracted < fWorst then replaceWorst contracted fContracted else shrink ()

        {
            X = Array.copy vertices[0]
            Value = values[0]
            Iterations = iterations
            Evaluations = evaluations.Value
            Converged = isConverged
        }
