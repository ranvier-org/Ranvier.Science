namespace Ranvier.Numerics

open System

/// The result of `Integrate.adaptive`. `Value` is the sum of the panel integrals and `ErrorEstimate` the sum of the
/// panel error estimates. `Evaluations` counts calls of the integrand: 15 per panel evaluated. `Converged` is true when
/// the estimate met the requested tolerance, a heuristic and not a guarantee about `Value`; it is false at the panel
/// limit and for a NaN bound or integrand, whose `Value` is NaN.
type Integral =
    {
        /// The integral over the requested interval.
        Value: float
        /// The estimated absolute error of `Value`.
        ErrorEstimate: float
        /// The number of calls of the integrand.
        Evaluations: int
        /// True when `ErrorEstimate` is at most max(absolute, relative · |Value|).
        Converged: bool
    }

/// The result of `Differentiate.derivative`: the derivative and Ridders' estimate of its absolute error. Both are NaN
/// when the point or a function value is NaN.
type Derivative =
    {
        /// The estimated derivative.
        Value: float
        /// The estimated absolute error of `Value`.
        ErrorEstimate: float
    }

/// Quadrature over a function and over samples. Every rule leaves its inputs untouched, and an exception raised by the
/// caller's function propagates unchanged.
module Integrate =
    // The largest number of panels `adaptive` holds; a definition.
    let private maximumPanels = 200

    // Machine epsilon 2^-52 and the smallest normal double: d1mach(4) and d1mach(1) of QUADPACK.
    let private epmach = 2.220446049250313e-16
    let private uflow = 2.2250738585072014e-308

    // From documentation: Piessens, de Doncker-Kapenga, Ueberhuber and Kahaner, QUADPACK (Springer, 1983), the data
    // statements of dqk15. Abscissae of the 15-point Kronrod rule on [-1, 1], largest first, ending at the centre. The
    // odd positions 1, 3 and 5 are also the nodes of the 7-point Gauss rule.
    let private xgk =
        [|
            0.991455371120812639206854697526329
            0.949107912342758524526189684047851
            0.864864423359769072789712788640926
            0.741531185599394439863864773280788
            0.586087235467691130294144838258730
            0.405845151377397166906606412076961
            0.207784955007898467600689403773245
            0.000000000000000000000000000000000
        |]

    // From documentation: QUADPACK dqk15. Weights of the 15-point Kronrod rule for the nodes of xgk.
    let private wgk =
        [|
            0.022935322010529224963732008058970
            0.063092092629978553290700663189204
            0.104790010322250183839876322541518
            0.140653259715525918745189590510238
            0.169004726639267902826583426598550
            0.190350578064785409913256402421014
            0.204432940075298892414161999234649
            0.209482141084727828012999174891714
        |]

    // From documentation: QUADPACK dqk15. Weights of the 7-point Gauss rule for xgk positions 1, 3 and 5, then for the
    // centre.
    let private wg =
        [|
            0.129484966168869693270611432679082
            0.279705391489276667901467771423780
            0.381830050505118944950369775488975
            0.417959183673469387755102040816327
        |]

    // QUADPACK dqk15 on [a, b]: the Kronrod value and its error estimate, with the 15 evaluations at interior points.
    let private qk15 (g: float -> float) (a: float) (b: float) : struct (float * float) =
        let centre = 0.5 * (a + b)
        let half = 0.5 * (b - a)
        let fv1 = Array.zeroCreate<float> 7
        let fv2 = Array.zeroCreate<float> 7
        let fc = g centre
        let mutable resg = wg[3] * fc
        let mutable resk = wgk[7] * fc
        let mutable resabs = abs resk

        for j in 0 .. 6 do
            let absc = half * xgk[j]
            let f1 = g (centre - absc)
            let f2 = g (centre + absc)
            fv1[j] <- f1
            fv2[j] <- f2
            let fsum = f1 + f2

            if j % 2 = 1 then
                resg <- resg + wg[j / 2] * fsum

            resk <- resk + wgk[j] * fsum
            resabs <- resabs + wgk[j] * (abs f1 + abs f2)

        let reskh = resk * 0.5
        let mutable resasc = wgk[7] * abs (fc - reskh)

        for j in 0 .. 6 do
            resasc <- resasc + wgk[j] * (abs (fv1[j] - reskh) + abs (fv2[j] - reskh))

        let result = resk * half
        let resabs = resabs * abs half
        let resasc = resasc * abs half
        let mutable err = abs ((resk - resg) * half)

        if resasc <> 0.0 && err <> 0.0 then
            let scaled = 200.0 * err / resasc
            err <- resasc * min 1.0 (scaled * sqrt scaled)

        if resabs > uflow / (50.0 * epmach) then
            err <- max (50.0 * epmach * resabs) err

        struct (result, err)

    // f x, or 0 where x is infinite (a node of an infinite-range transform that reached the infinite end).
    let private finiteOrZero (f: float -> float) (x: float) : float =
        if Double.IsInfinity x then 0.0 else f x

    /// Integral of the function over [a, b] by the adaptive Gauss–Kronrod (7, 15) rule of QUADPACK's `dqk15`, with its
    /// error estimate. The panel of largest error estimate (found by a linear scan) is bisected, up to 200 panels; the
    /// rule evaluates 15 interior points per panel, so the cost is at most 15 + 30 · 199 calls and O(panels²) further
    /// work. The result is converged when the sum of the panels' error estimates is at most
    /// max(absolute, relative · |Value|), with Value the sum of the panels' values; this is tested after the first
    /// panel and after each bisection. At 200 panels without meeting it, the result has `Converged = false` with the
    /// current `Value` and `ErrorEstimate`. Singular endpoints are never evaluated; 1/√x and ln x at 0 converge by
    /// repeated bisection of the panel at 0, without extrapolation (66 and 34 panels at absolute 1e-12 and relative
    /// 1e-10, measured here by `Calculus`' closed-form list).
    ///
    /// The error estimate is heuristic, not a bound: it sees only the 15 nodes of each panel. A feature narrower than a
    /// panel's node spacing, such as a peak of width 1e-3 inside [0, 1], can fall between the nodes and give a
    /// converged result that is wrong by the whole integral; callers split the interval at such features. With both
    /// tolerances 0, or below about 50 · 2⁻⁵² · (the integral of |f|), the panel limit is always reached.
    ///
    /// An infinite bound maps to (0, 1] with the infinite end at t = 0 (QUADPACK `dqagi` half-line transform): [a, ∞) by
    /// x = a + (1 − t)/t, (−∞, b] by x = b − (1 − t)/t, each integrand multiplied by 1/t². A node whose x is infinite
    /// contributes 0, so algebraic tails such as (1 + x)^−1.5 converge by bisecting at t = 0. The whole line (−∞, ∞) is
    /// the sum of the two half-line integrals, run as two starting panels that share the 200-panel limit and the
    /// convergence test; it is the improper integral, not the principal value, so an odd integrand whose halves
    /// diverge, such as x, reports `Converged = false`. For b less than a the result is the negated integral over
    /// [b, a]. For a = b the result is 0, converged, with no evaluation. A NaN bound, or a NaN from the function, stops
    /// the integration at once with a NaN `Value` and `Converged = false`. A negative or NaN tolerance raises
    /// `ArgumentOutOfRangeException`.
    let adaptive (absolute: float) (relative: float) (f: float -> float) (a: float) (b: float) : Integral =
        if not (absolute >= 0.0) then
            raise (
                ArgumentOutOfRangeException(
                    "absolute",
                    sprintf "The absolute tolerance must be at least 0; it is %g." absolute
                )
            )

        if not (relative >= 0.0) then
            raise (
                ArgumentOutOfRangeException(
                    "relative",
                    sprintf "The relative tolerance must be at least 0; it is %g." relative
                )
            )

        if Double.IsNaN a || Double.IsNaN b then
            {
                Value = nan
                ErrorEstimate = nan
                Evaluations = 0
                Converged = false
            }
        elif a = b then
            {
                Value = 0.0
                ErrorEstimate = 0.0
                Evaluations = 0
                Converged = true
            }
        else
            let negate = b < a
            let lo = if negate then b else a
            let hi = if negate then a else b

            let g, ta, tb =
                if not (Double.IsInfinity lo) && not (Double.IsInfinity hi) then
                    f, lo, hi
                // From documentation: the transforms of QUADPACK dqagi, which put the infinite end at t = 0, where
                // bisection can narrow a panel without limit. A node whose x is infinite contributes 0.
                elif not (Double.IsInfinity lo) then
                    (fun (t: float) -> finiteOrZero f (lo + (1.0 - t) / t) / (t * t)), 0.0, 1.0
                elif not (Double.IsInfinity hi) then
                    (fun (t: float) -> finiteOrZero f (hi - (1.0 - t) / t) / (t * t)), 0.0, 1.0
                else
                    // The whole line: s in (−1, 0) is the half-line x = −(1 − t)/t and s in (0, 1) the half-line
                    // x = (1 − t)/t, with t = |s|; two starting panels meet at s = 0, the infinite end of both.
                    (fun (s: float) ->
                        let t = abs s
                        let x = (1.0 - t) / t
                        finiteOrZero f (if s < 0.0 then -x else x) / (t * t)),
                    -1.0,
                    1.0

            let split = Double.IsInfinity lo && Double.IsInfinity hi

            let pa = Array.zeroCreate<float> maximumPanels
            let pb = Array.zeroCreate<float> maximumPanels
            let pv = Array.zeroCreate<float> maximumPanels
            let pe = Array.zeroCreate<float> maximumPanels
            let mutable count = 0
            let mutable evaluated = 0
            let mutable nanSeen = false

            for (x0, x1) in (if split then [ (-1.0, 0.0); (0.0, 1.0) ] else [ (ta, tb) ]) do
                if not nanSeen then
                    let struct (v0, e0) = qk15 g x0 x1
                    evaluated <- evaluated + 1
                    pa[count] <- x0
                    pb[count] <- x1
                    pv[count] <- v0
                    pe[count] <- e0
                    count <- count + 1
                    nanSeen <- Double.IsNaN v0 || Double.IsNaN e0

            let mutable converged = false
            let mutable fin = nanSeen
            let mutable value = nan
            let mutable error = nan

            while not fin do
                value <- 0.0
                error <- 0.0

                for i in 0 .. count - 1 do
                    value <- value + pv[i]
                    error <- error + pe[i]

                if error <= max absolute (relative * abs value) then
                    converged <- true
                    fin <- true
                elif count = maximumPanels then
                    fin <- true
                else
                    let mutable worst = 0

                    for i in 1 .. count - 1 do
                        if pe[i] > pe[worst] then
                            worst <- i

                    let a0 = pa[worst]
                    let b0 = pb[worst]
                    let mid = 0.5 * (a0 + b0)
                    let struct (v1, e1) = qk15 g a0 mid
                    let struct (v2, e2) = qk15 g mid b0
                    evaluated <- evaluated + 2

                    if Double.IsNaN v1 || Double.IsNaN e1 || Double.IsNaN v2 || Double.IsNaN e2 then
                        nanSeen <- true
                        fin <- true
                    else
                        pb[worst] <- mid
                        pv[worst] <- v1
                        pe[worst] <- e1
                        pa[count] <- mid
                        pb[count] <- b0
                        pv[count] <- v2
                        pe[count] <- e2
                        count <- count + 1

            if nanSeen then
                {
                    Value = nan
                    ErrorEstimate = nan
                    Evaluations = 15 * evaluated
                    Converged = false
                }
            else
                {
                    Value = if negate then -value else value
                    ErrorEstimate = error
                    Evaluations = 15 * evaluated
                    Converged = converged
                }

    // Double-double arithmetic (a value hi + lo with |lo| at most half an ulp of hi), after Dekker (1971) and Knuth's
    // TwoSum; used only to compute Gauss–Legendre weights to full double accuracy.
    let private twoSum (a: float) (b: float) : struct (float * float) =
        let s = a + b
        let bb = s - a
        struct (s, (a - (s - bb)) + (b - bb))

    let private quickTwoSum (a: float) (b: float) : struct (float * float) =
        let s = a + b
        struct (s, b - (s - a))

    let private twoProd (a: float) (b: float) : struct (float * float) =
        let p = a * b
        let ca = 134217729.0 * a
        let ah = ca - (ca - a)
        let al = a - ah
        let cb = 134217729.0 * b
        let bh = cb - (cb - b)
        let bl = b - bh
        struct (p, ((ah * bh - p) + ah * bl + al * bh) + al * bl)

    let private ddAdd (struct (ah, al): struct (float * float)) (struct (bh, bl): struct (float * float)) =
        let struct (s, e) = twoSum ah bh
        let struct (t, f) = twoSum al bl
        let struct (s2, e2) = quickTwoSum s (e + t)
        quickTwoSum s2 (e2 + f)

    let private ddMul (struct (ah, al): struct (float * float)) (struct (bh, bl): struct (float * float)) =
        let struct (p, e) = twoProd ah bh
        quickTwoSum p (e + (ah * bl + al * bh))

    let private ddMulFloat (struct (ah, al): struct (float * float)) (b: float) =
        let struct (p, e) = twoProd ah b
        quickTwoSum p (e + al * b)

    let private ddDivFloat (struct (ah, al): struct (float * float)) (b: float) =
        let q1 = ah / b
        let struct (p, e) = twoProd q1 b
        let r = ((ah - p) - e) + al
        quickTwoSum q1 (r / b)

    // P_n(z) and P_(n-1)(z) by the three-term recurrence in double-double arithmetic.
    let private legendreDd (n: int) (z: struct (float * float)) : struct (struct (float * float) * struct (float * float)) =
        let mutable p1 = struct (1.0, 0.0)
        let mutable p2 = struct (0.0, 0.0)

        for j in 0 .. n - 1 do
            let p3 = p2
            p2 <- p1
            let a = ddMulFloat (ddMul z p2) (float (2 * j + 1))
            let b = ddMulFloat p3 (-(float j))
            p1 <- ddDivFloat (ddAdd a b) (float (j + 1))

        struct (p1, p2)

    // The root of P_n nearest the starting guess, as a double, and its Gauss weight. Newton's method in double
    // arithmetic, then one correction and the weight 2 (1 − z²) / (n P_(n-1)(z))² in double-double arithmetic.
    let private legendreNode (n: int) (start: float) : struct (float * float) =
        let evaluate (z: float) : struct (float * float) =
            let mutable p1 = 1.0
            let mutable p2 = 0.0

            for j in 0 .. n - 1 do
                let p3 = p2
                p2 <- p1
                p1 <- (float (2 * j + 1) * z * p2 - float j * p3) / float (j + 1)

            struct (p1, float n * (z * p1 - p2) / (z * z - 1.0))

        let mutable z = start
        let mutable steps = 0
        let mutable moving = true

        while moving && steps < 100 do
            let struct (p, pp) = evaluate z
            let next = z - p / pp
            moving <- abs (next - z) > 1e-15
            z <- next
            steps <- steps + 1

        let struct (pn, _) = legendreDd n (struct (z, 0.0))
        let struct (pnh, _) = pn
        let struct (_, pp) = evaluate z
        let zdd = quickTwoSum z (-pnh / pp)
        let struct (zh, _) = zdd
        let struct (_, pm) = legendreDd n zdd
        let struct (pmh, _) = pm
        let struct (om, _) = ddAdd (struct (1.0, 0.0)) (ddMulFloat (ddMul zdd zdd) -1.0)
        let nn = float n * pmh
        struct (zh, 2.0 * om / (nn * nn))

    /// Integral of the function over [a, b] by the n-point Gauss–Legendre rule, exact for polynomials up to degree
    /// 2n − 1. The nodes and weights are computed on each call by Newton's method on the Legendre polynomial Pₙ from
    /// cos(π(i − ¼)/(n + ½)), with the weights taken from a double-double evaluation of Pₙ₋₁; the cost is O(n²) for them
    /// plus n evaluations. For b less than a the result is negated. A NaN bound or integrand gives NaN. Fewer than one point raises `ArgumentOutOfRangeException`.
    let gaussLegendre (points: int) (f: float -> float) (a: float) (b: float) : float =
        if points < 1 then
            raise (
                ArgumentOutOfRangeException(
                    "points",
                    sprintf "The number of points must be at least 1; it is %d." points
                )
            )

        let n = points
        let mid = 0.5 * (a + b)
        let half = 0.5 * (b - a)
        let mutable sum = 0.0

        for i in 0 .. (n + 1) / 2 - 1 do
            let start = cos (Math.PI * (float i + 0.75) / (float n + 0.5))
            let struct (z, weight) = legendreNode n start

            if n % 2 = 1 && i = n / 2 then
                sum <- sum + weight * f mid
            else
                let offset = half * abs z
                sum <- sum + weight * (f (mid - offset) + f (mid + offset))

        half * sum

    let private checkLengths (xs: float[]) (ys: float[]) : unit =
        if xs.Length <> ys.Length then
            raise (
                ArgumentException(
                    sprintf "The abscissae and ordinates must have the same length; they have %d and %d." xs.Length ys.Length
                )
            )

    /// Composite trapezoid rule over the samples: the sum of (xs[i + 1] − xs[i]) · (ys[i] + ys[i + 1]) / 2. Each
    /// interval adds its signed width, so decreasing or unordered abscissae are not rejected. Fewer than two samples give
    /// 0. The cost is O(n). Lengths that differ raise `ArgumentException` naming both.
    let trapezoid (xs: float[]) (ys: float[]) : float =
        checkLengths xs ys
        let mutable sum = 0.0

        for i in 0 .. xs.Length - 2 do
            sum <- sum + (xs[i + 1] - xs[i]) * (ys[i] + ys[i + 1]) * 0.5

        sum

    /// Composite Simpson rule over the samples, with uneven spacing allowed, exact for quadratics. Pairs of intervals take
    /// the uneven-spacing Simpson weights; for an odd number of intervals the last interval takes the integral of the
    /// quadratic through the last three points (Cartwright, 2017; the treatment of SciPy 1.11, from documentation). Two
    /// samples give the trapezoid and fewer give 0. A repeated abscissa inside a pair gives a non-finite result. The
    /// cost is O(n). Lengths that differ raise `ArgumentException` naming both.
    let simpson (xs: float[]) (ys: float[]) : float =
        checkLengths xs ys
        let n = xs.Length

        if n < 3 then
            trapezoid xs ys
        else
            let intervals = n - 1
            let paired = intervals - intervals % 2
            let mutable sum = 0.0
            let mutable i = 0

            while i < paired do
                let h0 = xs[i + 1] - xs[i]
                let h1 = xs[i + 2] - xs[i + 1]
                let hsum = h0 + h1
                let ratio = h0 / h1

                sum <-
                    sum
                    + hsum
                      / 6.0
                      * (ys[i] * (2.0 - 1.0 / ratio) + ys[i + 1] * (hsum * hsum / (h0 * h1)) + ys[i + 2] * (2.0 - ratio))

                i <- i + 2

            if intervals % 2 = 1 then
                let h0 = xs[n - 2] - xs[n - 3]
                let h1 = xs[n - 1] - xs[n - 2]
                let alpha = (2.0 * h1 * h1 + 3.0 * h0 * h1) / (6.0 * (h0 + h1))
                let beta = (h1 * h1 + 3.0 * h0 * h1) / (6.0 * h0)
                let eta = h1 * h1 * h1 / (6.0 * h0 * (h0 + h1))
                sum <- sum + alpha * ys[n - 1] + beta * ys[n - 2] - eta * ys[n - 3]

            sum

    /// The running trapezoid integral: a fresh array with one value per sample, the first 0 and element i the integral
    /// from xs[0] to xs[i]. No samples give an empty array. The cost is O(n). Lengths that differ raise
    /// `ArgumentException` naming both.
    let cumulativeTrapezoid (xs: float[]) (ys: float[]) : float[] =
        checkLengths xs ys
        let result = Array.zeroCreate<float> xs.Length

        for i in 1 .. xs.Length - 1 do
            result[i] <- result[i - 1] + (xs[i] - xs[i - 1]) * (ys[i] + ys[i - 1]) * 0.5

        result

/// Numerical differentiation. An exception raised by the caller's function propagates unchanged.
module Differentiate =
    /// Derivative of the function at the point by Ridders' extrapolation of central differences (Numerical Recipes, 3rd
    /// edition, section 5.7, `dfridr`; from documentation): initial step 0.1 · max(1, |x|), shrunk by the factor 1.4 up
    /// to 10 times, stopping early when the error grows past twice the best estimate. The function is called at most 20
    /// times. A NaN or infinite point, or a NaN function value, gives NaN in both fields; a function undefined within the
    /// initial step of the point therefore gives NaN. `ErrorEstimate` is not a bound: when the function varies on a
    /// scale shorter than the initial step (sin at 1e3, for example) both fields can be wrong by orders of magnitude.
    let derivative (f: float -> float) (x: float) : Derivative =
        let failed = { Value = nan; ErrorEstimate = nan }

        if Double.IsNaN x || Double.IsInfinity x then
            failed
        else
            let con = 1.4
            let con2 = con * con
            let size = 10
            let table = Array2D.zeroCreate<float> size size
            let mutable hh = 0.1 * max 1.0 (abs x)
            let central (h: float) = (f (x + h) - f (x - h)) / (2.0 * h)
            table[0, 0] <- central hh
            let mutable answer = table[0, 0]
            let mutable err = Double.PositiveInfinity
            let mutable bad = Double.IsNaN table[0, 0]
            let mutable i = 1

            while not bad && i < size do
                hh <- hh / con
                table[0, i] <- central hh
                bad <- Double.IsNaN table[0, i]
                let mutable fac = con2
                let mutable j = 1

                while not bad && j <= i do
                    table[j, i] <- (table[j - 1, i] * fac - table[j - 1, i - 1]) / (fac - 1.0)
                    fac <- con2 * fac
                    let errt = max (abs (table[j, i] - table[j - 1, i])) (abs (table[j, i] - table[j - 1, i - 1]))

                    if errt <= err then
                        err <- errt
                        answer <- table[j, i]

                    j <- j + 1

                if not bad && abs (table[i, i] - table[i - 1, i - 1]) >= 2.0 * err then
                    i <- size
                else
                    i <- i + 1

            if bad || Double.IsNaN answer || Double.IsNaN err || Double.IsInfinity err then
                failed
            else
                { Value = answer; ErrorEstimate = err }
