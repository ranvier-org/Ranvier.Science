namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>
/// A discrete distribution on the integers with fixed parameters. Build one with the constructors of the
/// <c>Discrete</c> module, which validate the parameters. Two values with equal parameters are equal.
/// </summary>
type Discrete =
    private
    | Binomial of trials: int * p: float
    | Poisson of mean: float
    | Geometric of p: float
    | NegativeBinomial of r: float * p: float
    | Hypergeometric of population: int * successes: int * draws: int
    | Bernoulli of p: float
    | DiscreteUniform of lower: int * upper: int

/// <summary>
/// Mass, distribution function, survival function, quantile, moments, support and sampling of discrete
/// distributions.
/// </summary>
/// <remarks>
/// <c>mass</c>, <c>logMass</c>, <c>cdf</c> and <c>survival</c> state their error bound over one domain: the binomial
/// with trials up to 1000 and p from 1e-10 to 0.999; the Poisson with mean from 0.001 to 1000; the geometric with p
/// from 1e-10 to 0.999; the negative binomial with r from 0.5 to 100, p from 0.002 to 0.999 and mean r(1 − p)/p up to
/// 1000; the hypergeometric with population up to 1000. The Bernoulli and discrete uniform are closed forms of one
/// rounding each. Rows inside the domain are pinned against <c>tests/reference/special.py</c> (section discrete) by
/// the tests "binomial against exact fractions", "binomial tails", "Poisson against the reference", "negative binomial
/// against the reference" and "hypergeometric against exact fractions", and rows at its ends by "the stated domains at
/// their edges against the reference". Between the pinned rows the bound rests on a dense scan on .NET and on
/// JavaScript separately against exact fractions (the binomial and hypergeometric) and 50-digit decimal sums (the
/// Poisson, geometric and negative binomial), measured here and not committed. The two targets can differ in the last
/// digits.
/// </remarks>
module Discrete =
    let private maxInt = float Int32.MaxValue

    // Raises unless n ≥ 0.
    let private notNegative (name: string) (n: int) : unit =
        if n < 0 then
            raise (ArgumentOutOfRangeException(name, name + " must be zero or more."))

    // Raises unless 0 < p ≤ 1; NaN raises.
    let private successProbability (name: string) (p: float) : unit =
        if not (p > 0.0 && p <= 1.0) then
            raise (ArgumentOutOfRangeException(name, name + " must be greater than 0 and at most 1."))

    /// <summary>The number of successes in a fixed number of independent trials of the same success probability.
    /// Raises ArgumentOutOfRangeException unless trials ≥ 0 and 0 ≤ p ≤ 1.</summary>
    let binomial (trials: int) (p: float) : Discrete =
        notNegative (nameof trials) trials
        Check.probability (nameof p) p
        Binomial(trials, p)

    /// <summary>The Poisson distribution with the given mean. Raises ArgumentOutOfRangeException unless the mean is
    /// finite and greater than 0. From a mean of about 1.674e6 on, <c>cdf</c> and <c>survival</c> are NaN near the
    /// mean and <c>quantile</c> raises there (see <c>cdf</c>).</summary>
    let poisson (mean: float) : Discrete =
        Check.finitePositive (nameof mean) mean
        Poisson mean

    /// <summary>The number of failures before the first success in independent trials of success probability p.
    /// Raises ArgumentOutOfRangeException unless 0 &lt; p ≤ 1.</summary>
    let geometric (p: float) : Discrete =
        successProbability (nameof p) p
        Geometric p

    /// <summary>The number of failures before the r-th success in independent trials of success probability p, for a
    /// real r: mass Γ(r + k) / (Γ(r) k!) p^r (1 − p)^k at k ≥ 0. Raises ArgumentOutOfRangeException unless r is finite
    /// and greater than 0 and 0 &lt; p ≤ 1.</summary>
    let negativeBinomial (r: float) (p: float) : Discrete =
        Check.finitePositive (nameof r) r
        successProbability (nameof p) p
        NegativeBinomial(r, p)

    /// <summary>The number of successes in draws taken without replacement from a population that holds the given
    /// number of successes. Raises ArgumentOutOfRangeException unless population ≥ 0, 0 ≤ successes ≤ population and
    /// 0 ≤ draws ≤ population.</summary>
    let hypergeometric (population: int) (successes: int) (draws: int) : Discrete =
        notNegative (nameof population) population

        if successes < 0 || successes > population then
            raise (ArgumentOutOfRangeException(nameof successes, "successes must lie between 0 and population."))

        if draws < 0 || draws > population then
            raise (ArgumentOutOfRangeException(nameof draws, "draws must lie between 0 and population."))

        Hypergeometric(population, successes, draws)

    /// <summary>1 with probability p and 0 otherwise. Raises ArgumentOutOfRangeException unless 0 ≤ p ≤ 1.</summary>
    let bernoulli (p: float) : Discrete =
        Check.probability (nameof p) p
        Bernoulli p

    /// <summary>The uniform distribution on the integers from lower to upper, both included. Raises
    /// ArgumentOutOfRangeException unless lower ≤ upper &lt; Int32.MaxValue.</summary>
    let uniform (lower: int) (upper: int) : Discrete =
        if upper < lower then
            raise (ArgumentOutOfRangeException(nameof upper, "upper must be at least lower."))

        if upper = Int32.MaxValue then
            raise (ArgumentOutOfRangeException(nameof upper, "upper must be less than Int32.MaxValue."))

        DiscreteUniform(lower, upper)

    /// <summary>The least and greatest values of positive mass, Int32.MaxValue standing for an unbounded upper end
    /// (the Poisson, and the geometric and negative binomial with p &lt; 1). A binomial or Bernoulli with p of 0 or
    /// 1, and a geometric or negative binomial with p = 1, have a single value.</summary>
    let support (d: Discrete) : struct (int * int) =
        match d with
        | Binomial (n, p) ->
            if p = 0.0 then struct (0, 0)
            elif p = 1.0 then struct (n, n)
            else struct (0, n)
        | Poisson _ -> struct (0, Int32.MaxValue)
        | Geometric p
        | NegativeBinomial (_, p) -> if p = 1.0 then struct (0, 0) else struct (0, Int32.MaxValue)
        | Hypergeometric (population, successes, draws) ->
            struct (max 0 (draws - (population - successes)), min draws successes)
        | Bernoulli p ->
            if p = 0.0 then struct (0, 0)
            elif p = 1.0 then struct (1, 1)
            else struct (0, 1)
        | DiscreteUniform (lower, upper) -> struct (lower, upper)

    let private halfLnTwoPi = 0.5 * log (2.0 * Math.PI)

    // B₂ⱼ / (2j(2j − 1)), j = 1..6, the coefficients of Stirling's series for ln Γ, which
    // python -I tests/reference/special.py gamma prints for Special.fs.
    let private stirlingCoefficients =
        [|
            1.0 / 12.0
            -1.0 / 360.0
            1.0 / 1260.0
            -1.0 / 1680.0
            1.0 / 1188.0
            -691.0 / 360360.0
        |]

    // δ(x) = ln Γ(x + 1) − ((x + ½) ln x − x + ½ ln 2π) for x > 0, the error of Stirling's formula for ln x!. Above 15,
    // Stirling's series Σ B₂ⱼ / (2j(2j − 1) x^(2j − 1)) over j = 1..6, whose first omitted term, 1/(156 x¹³), is below
    // 4e-18 there; below, from Special.gammaLn, the terms of the difference staying below 45 in magnitude.
    let private stirlingError (x: float) : float =
        if x > 15.0 then
            let w = 1.0 / (x * x)
            let mutable sum = stirlingCoefficients[5]

            for j in 4 .. -1 .. 0 do
                sum <- sum * w + stirlingCoefficients[j]

            sum / x
        else
            Special.gammaLn (x + 1.0) - (x + 0.5) * log x + x - halfLnTwoPi

    // x ln(x/m) + m − x for x ≥ 0 and m > 0. Where |x − m| < 0.1 (x + m), the series
    // (x − m) v + 2x Σ v^(2j + 1)/(2j + 1) over j ≥ 1 with v = (x − m)/(x + m), |v| < 0.1, summed until a term leaves
    // the sum unchanged, so that x ln(x/m) and x − m, which nearly cancel there, are never formed.
    let private deviance (x: float) (m: float) : float =
        if x = 0.0 then
            m
        elif abs (x - m) < 0.1 * (x + m) then
            let v = (x - m) / (x + m)
            let v2 = v * v
            let mutable sum = (x - m) * v
            let mutable term = 2.0 * x * v
            let mutable j = 1.0
            let mutable finished = false

            while not finished do
                term <- term * v2
                let next = sum + term / (2.0 * j + 1.0)
                finished <- next = sum
                sum <- next
                j <- j + 1.0

            sum
        else
            x * log (x / m) + m - x

    // ln of x successes and y failures in x + y trials, Γ(n + 1)/(Γ(x + 1)Γ(y + 1)) p^x q^y with n = x + y, for
    // x, y > 0, 0 < p < 1 and q = 1 − p: Loader's saddle-point form
    // δ(n) − δ(x) − δ(y) − D(x, np) − D(y, nq) − ½ ln(2π x y/n), with δ = stirlingError and D = deviance, whose terms
    // stay small where the mass is not.
    let private binomialSaddle (x: float) (y: float) (p: float) (q: float) : float =
        let n = x + y

        stirlingError n - stirlingError x - stirlingError y - deviance x (n * p) - deviance y (n * q)
        - 0.5 * (log (2.0 * Math.PI * x) + Special.log1p (-x / n))

    /// <summary>The natural logarithm of the mass at k: −∞ outside the support. Absolute error at most 1e-12 where the
    /// mass is at least 1e-300, over the domain the module states (pinned by the test "the stated domains at their
    /// edges against the reference"; between the pinned rows, the module's uncommitted scan). <c>mass</c> is its
    /// exponential but for the Bernoulli and the discrete uniform.</summary>
    let logMass (d: Discrete) (k: int) : float =
        let struct (lower, upper) = support d

        if k < lower || k > upper then
            -infinity
        elif lower = upper then
            0.0
        else
            match d with
            | Binomial (n, p) ->
                if k = 0 then float n * Special.log1p (-p)
                elif k = n then float n * log p
                else binomialSaddle (float k) (float (n - k)) p (1.0 - p)
            | Poisson mean ->
                if k = 0 then
                    -mean
                else
                    let x = float k
                    -(stirlingError x) - deviance x mean - 0.5 * log (2.0 * Math.PI * x)
            | Geometric p -> log p + float k * Special.log1p (-p)
            | NegativeBinomial (r, p) ->
                // r/(r + k) times the binomial mass of r successes and k failures.
                if k = 0 then
                    r * log p
                else
                    -(Special.log1p (float k / r)) + binomialSaddle r (float k) p (1.0 - p)
            | Hypergeometric (population, successes, draws) ->
                Special.logChoose successes k + Special.logChoose (population - successes) (draws - k)
                - Special.logChoose population draws
            | Bernoulli p -> if k = 0 then Special.log1p (-p) else log p
            | DiscreteUniform (lower, upper) -> -(log (float upper - float lower + 1.0))

    /// <summary>
    /// P(X = k): 0 outside the support. Relative error at most 1e-12 where the value is at least 1e-300, over the
    /// domain the module states (pinned as the module states). The binomial, Poisson and negative binomial use
    /// Loader's saddle-point form (C. Loader, "Fast and accurate computation of binomial probabilities", 2000; general
    /// knowledge: the Stirling-error and deviance terms behind R's dbinom and dpois), whose terms stay small where the
    /// mass is not. The direct exp(k ln mean − mean − ln k!), whose terms reach 16 000 at mean 1000, loses more than
    /// the bound there (the module's uncommitted scan read 4.3e-12); the test "where the direct mass and the rounded
    /// 1 - p lose accuracy, against the reference" holds rows at means 300 and 1000. The geometric is
    /// exp(ln p + k ln(1 − p)), the hypergeometric the exponential of three
    /// <c>Special.logChoose</c> terms, and the Bernoulli and discrete uniform 1 − p, p and 1/(upper − lower + 1).
    /// </summary>
    let mass (d: Discrete) (k: int) : float =
        match d with
        | Bernoulli p ->
            if k = 0 then 1.0 - p
            elif k = 1 then p
            else 0.0
        | DiscreteUniform (lower, upper) ->
            if k < lower || k > upper then 0.0 else 1.0 / (float upper - float lower + 1.0)
        | _ -> exp (logMass d k)

    // The most likely value of the hypergeometric, ⌊(draws + 1)(successes + 1) / (population + 2)⌋.
    let private hypergeometricMode (population: int) (successes: int) (draws: int) : int =
        int (Math.Floor((float draws + 1.0) * (float successes + 1.0) / (float population + 2.0)))

    // The sum of the masses at from..until, smallest first: upwards below the mode, downwards above it.
    let private massSum (d: Discrete) (from: int) (until: int) (upwards: bool) : float =
        let mutable sum = 0.0

        if upwards then
            for j in from..until do
                sum <- sum + mass d j
        else
            for j in until .. -1 .. from do
                sum <- sum + mass d j

        sum

    /// <summary>
    /// P(X ≤ k): 0 below the support and 1 at and above the upper end of a bounded support. Relative error at most
    /// 1e-12 where the value is at least 1e-300, over the domain the module states (pinned as the module states). The
    /// binomial is I_(1 − p)(n − k, k + 1), the Poisson Q(k + 1, mean) and the
    /// negative binomial I_p(r, k + 1), by <c>Special.betaRegularized</c> and <c>Special.gammaUpperRegularized</c>; the
    /// rounding of 1 − p moves the binomial by about 2 · trials · 2^-54 relative at most (general knowledge). The
    /// geometric is −expm1((k + 1) ln(1 − p)). The hypergeometric is the sum of the masses from the lower end of the
    /// support to k below the mode ⌊(draws + 1)(successes + 1)/(population + 2)⌋, and from the mode on 1 minus the sum
    /// from k + 1 to the upper end, which is then at most about ½. Cost: one evaluation of the incomplete beta or gamma
    /// function; for the hypergeometric k − lower + 1 masses below the mode and upper − k from it.
    /// The Poisson's is finite for means up to 1.674e6 and NaN near the mean from about 1 674 055 on, where the
    /// series that <c>Special.gammaUpperRegularized</c> and <c>Special.gammaLowerRegularized</c> share passes its
    /// 10 000 terms with k + 1 close to the mean (measured here on .NET and on JavaScript, at every k within 8 standard
    /// deviations of the mean, where that series converges slowest: the least mean with a NaN, by bisection over means
    /// m + f, is 1 674 054.999999 at f = 0.999999, 1 674 222.5 at f = ½ and 1 674 390 at whole means, and 157 means
    /// from 1e3 to 1.674e6 have none). The tests "a Poisson past its mean limit: NaN near the mean, and quantile
    /// raises" and "a Poisson at its mean limit: the quantile is the least k" hold means 2e6 and 1.674e6. Beyond mean
    /// 1000 no accuracy is stated.
    /// </summary>
    let cdf (d: Discrete) (k: int) : float =
        let struct (lower, upper) = support d

        if k < lower then
            0.0
        elif lower = upper then
            1.0
        else
            match d with
            | Binomial (n, p) ->
                if k >= n then 1.0 else Special.betaRegularized (float (n - k)) (float k + 1.0) (1.0 - p)
            | Poisson mean -> Special.gammaUpperRegularized (float k + 1.0) mean
            | Geometric p -> -(Special.expm1 ((float k + 1.0) * Special.log1p (-p)))
            | NegativeBinomial (r, p) -> Special.betaRegularized r (float k + 1.0) p
            | Hypergeometric (population, successes, draws) ->
                if k >= upper then 1.0
                elif k < hypergeometricMode population successes draws then massSum d lower k true
                else 1.0 - massSum d (k + 1) upper false
            | Bernoulli p -> if k >= 1 then 1.0 else 1.0 - p
            | DiscreteUniform (lower, upper) ->
                if k >= upper then 1.0 else (float k - float lower + 1.0) / (float upper - float lower + 1.0)

    /// <summary>
    /// P(X &gt; k), accurate in the upper tail: 1 below the support and 0 at and above the upper end of a bounded
    /// support. Relative error at most 1e-12 where the value is at least 1e-300, over the domain the module states
    /// (pinned as the module states). The binomial is I_p(k + 1, n − k), the Poisson P(k + 1, mean) and
    /// the geometric (1 − p)^(k + 1) by its logarithm; the hypergeometric is the sum of the masses from k + 1 to the
    /// upper end from the mode on, and below it 1 minus the sum up to k, at the cost <c>cdf</c> states. The negative
    /// binomial is 1 − I_p(r, k + 1) where that cdf is at most ½, and above it I_q(k + 1, r) at q, the double nearest
    /// 1 − p, plus its derivative q^k p^(r − 1)/B(k + 1, r) times the rounding error (1 − q) − p, which is exact.
    /// Without that term the rounding moves the upper tail by about k · 2^-54 / q relative at most (general knowledge),
    /// measured here 7.6e-11 at r = 0.5, p = 5e-4 and 1.1e-11 at r = 0.5, p = 0.00272, k = 200 000; with the term but
    /// not the switch at ½, the survival near 1 is 4.3e-9 off at r = 0.5, p = 1e-17, k = 5, where 1 − p rounds to 1
    /// (measured here). The test "where the direct mass and the rounded 1 - p lose accuracy, against the reference"
    /// holds both rows. Below p = 0.002, where k + 1 reaches far past the shapes up to 1000 that the bound of
    /// <c>Special.betaRegularized</c> covers, the error grows: measured here 1.9e-12 at r = 0.5, p = 5e-4. Cost: as
    /// <c>cdf</c>; for the negative binomial above the cdf's ½, two evaluations of the incomplete beta function and one
    /// of <c>Special.betaLn</c>. The Poisson's is NaN where <c>cdf</c> is, near the mean from a mean of about
    /// 1 674 055 on.
    /// </summary>
    let survival (d: Discrete) (k: int) : float =
        let struct (lower, upper) = support d

        if k < lower then
            1.0
        elif lower = upper then
            0.0
        else
            match d with
            | Binomial (n, p) -> if k >= n then 0.0 else Special.betaRegularized (float k + 1.0) (float (n - k)) p
            | Poisson mean -> Special.gammaLowerRegularized (float k + 1.0) mean
            | Geometric p -> exp ((float k + 1.0) * Special.log1p (-p))
            | NegativeBinomial (r, p) ->
                let a = float k + 1.0
                let lowerTail = Special.betaRegularized r a p

                if lowerTail <= 0.5 then
                    1.0 - lowerTail
                else
                    // I_q(k + 1, r) at q, 1 − p rounded, plus e times its derivative q^k p^(r − 1) / B(k + 1, r),
                    // where e = (1 − q) − p, exact by Sterbenz's lemma, is the rounding error of q.
                    let q = 1.0 - p
                    let e = (1.0 - q) - p
                    let atQ = Special.betaRegularized a r q

                    if e = 0.0 then
                        atQ
                    else
                        atQ + e * exp (float k * Special.log1p (-p) + (r - 1.0) * log p - Special.betaLn a r)
            | Hypergeometric (population, successes, draws) ->
                if k >= upper then 0.0
                elif k >= hypergeometricMode population successes draws then massSum d (k + 1) upper false
                else 1.0 - massSum d lower k true
            | Bernoulli p -> if k >= 1 then 0.0 else p
            | DiscreteUniform (lower, upper) ->
                if k >= upper then 0.0 else (float upper - float k) / (float upper - float lower + 1.0)

    /// <summary>The mean: trials · p, the Poisson's mean, (1 − p)/p, r(1 − p)/p, draws · successes / population (0 for
    /// an empty population), p and (lower + upper)/2.</summary>
    let mean (d: Discrete) : float =
        match d with
        | Binomial (n, p) -> float n * p
        | Poisson mean -> mean
        | Geometric p -> (1.0 - p) / p
        | NegativeBinomial (r, p) -> r * (1.0 - p) / p
        | Hypergeometric (population, successes, draws) ->
            if population = 0 then 0.0 else float draws * (float successes / float population)
        | Bernoulli p -> p
        | DiscreteUniform (lower, upper) -> (float lower + float upper) / 2.0

    /// <summary>The variance: trials · p(1 − p), the Poisson's mean, (1 − p)/p², r(1 − p)/p²,
    /// draws · successes (population − successes)(population − draws) / (population² (population − 1)) (0 for a
    /// population of 0 or 1), p(1 − p) and ((upper − lower + 1)² − 1)/12.</summary>
    let variance (d: Discrete) : float =
        match d with
        | Binomial (n, p) -> float n * p * (1.0 - p)
        | Poisson mean -> mean
        | Geometric p -> (1.0 - p) / (p * p)
        | NegativeBinomial (r, p) -> r * (1.0 - p) / (p * p)
        | Hypergeometric (population, successes, draws) ->
            if population <= 1 then
                0.0
            else
                let n = float population
                let k = float successes
                let m = float draws
                m * k * (n - k) * (n - m) / (n * n * (n - 1.0))
        | Bernoulli p -> p * (1.0 - p)
        | DiscreteUniform (lower, upper) ->
            let count = float upper - float lower + 1.0
            (count * count - 1.0) / 12.0

    /// <summary>
    /// The family and parameters as <c>Name(parameter = value, ...)</c>, each value by F#'s <c>string</c>, for example
    /// <c>Binomial(trials = 10, p = 0.3)</c>. The text of a real parameter whose magnitude lies outside [1e-5, 1e15)
    /// may differ between .NET and JavaScript.
    /// </summary>
    let describe (d: Discrete) : string =
        match d with
        | Binomial (n, p) -> "Binomial(trials = " + string n + ", p = " + string p + ")"
        | Poisson mean -> "Poisson(mean = " + string mean + ")"
        | Geometric p -> "Geometric(p = " + string p + ")"
        | NegativeBinomial (r, p) -> "NegativeBinomial(r = " + string r + ", p = " + string p + ")"
        | Hypergeometric (population, successes, draws) ->
            "Hypergeometric(population = "
            + string population
            + ", successes = "
            + string successes
            + ", draws = "
            + string draws
            + ")"
        | Bernoulli p -> "Bernoulli(p = " + string p + ")"
        | DiscreteUniform (lower, upper) ->
            "DiscreteUniform(lower = " + string lower + ", upper = " + string upper + ")"

    let private standardNormal = Continuous.normal 0.0 1.0

    // The least k in [lower, upper] at which the monotone condition holds, from start: steps of 1, 2, 4, … away from
    // start until the condition changes, then bisection. upper when the condition holds nowhere below it, which is the
    // saturation at Int32.MaxValue of an unbounded support.
    let private leastHolding (holds: int -> bool) (lower: int) (upper: int) (start: int) : int =
        // The condition holds at hi (or hi is upper) and fails at lo (or lo is lower − 1).
        let mutable lo = 0.0
        let mutable hi = 0.0
        let mutable step = 1.0
        let mutable searching = true

        if holds start then
            hi <- float start

            while searching do
                let candidate = hi - step

                if candidate < float lower then
                    lo <- float lower - 1.0
                    searching <- false
                elif holds (int candidate) then
                    hi <- candidate
                    step <- 2.0 * step
                else
                    lo <- candidate
                    searching <- false
        else
            lo <- float start

            while searching do
                let candidate = lo + step

                if candidate >= float upper then
                    hi <- float upper
                    searching <- false
                elif holds (int candidate) then
                    hi <- candidate
                    searching <- false
                else
                    lo <- candidate
                    step <- 2.0 * step

        while hi - lo > 1.0 do
            let middle = Math.Floor((lo + hi) / 2.0)

            if holds (int middle) then hi <- middle else lo <- middle

        int hi

    /// <summary>
    /// The least k with P(X ≤ k) ≥ p, as the computed <c>cdf</c> decides it for p ≤ ½ and the computed
    /// <c>survival</c> (survival k ≤ 1 − p) above, so that a p within the error of either from one of its values can
    /// give a result one away from the exact quantile. The support's lower bound at p = 0 and its upper bound at p = 1,
    /// Int32.MaxValue where the support is unbounded; a quantile beyond Int32.MaxValue gives Int32.MaxValue. Raises
    /// ArgumentOutOfRangeException unless 0 ≤ p ≤ 1; NaN raises, since an int has no NaN. Raises
    /// InvalidOperationException, naming the distribution and the k, where a value of <c>cdf</c> or <c>survival</c>
    /// that the search needs is NaN, as for the Poisson near its mean from a mean of about 1 674 055 on (see
    /// <c>cdf</c>); up to a mean of 1.674e6 the Poisson's quantile is the least k. From the start
    /// ⌊mean + sd · z⌋ clamped to the support, z the standard normal quantile of p, it takes steps of 1, 2, 4, … away
    /// from the start until the condition changes, then bisects. Cost: about 2 log₂(d + 1) + 2 evaluations of
    /// <c>cdf</c> or <c>survival</c> for a result d away from the start (general knowledge), which the test "quantile
    /// far from the normal start" holds at d = 3e8.
    /// </summary>
    let quantile (d: Discrete) (p: float) : int =
        Check.probability (nameof p) p
        let struct (lower, upper) = support d

        if p = 0.0 || lower = upper then
            lower
        elif p = 1.0 then
            upper
        else
            let holds (k: int) =
                let value = if p <= 0.5 then cdf d k else survival d k

                if Double.IsNaN value then
                    raise (
                        InvalidOperationException(
                            "The "
                            + (if p <= 0.5 then "cdf" else "survival")
                            + " of "
                            + describe d
                            + " is not available near "
                            + string k
                            + ", where it is NaN."
                        )
                    )

                if p <= 0.5 then value >= p else value <= 1.0 - p

            let guess = Math.Floor(mean d + sqrt (variance d) * Continuous.quantile standardNormal p)
            let start = if guess >= float upper then upper elif guess > float lower then int guess else lower
            leastHolding holds lower upper start

    // The largest mean Generator.poisson accepts, 2^30.
    let private poissonLimit = 1073741824.0

    // A Poisson draw of any mean and the next generator, saturating at Int32.MaxValue: above 2^30 the sum of draws of
    // mean 2^30 and one of the rest, which has the same distribution.
    let private poissonDraw (mean: float) (g: Generator) : struct (int * Generator) =
        let mutable current = g
        let mutable rest = mean
        let mutable total = 0.0

        while rest > poissonLimit && total < maxInt do
            let struct (k, next) = Generator.poisson poissonLimit current
            current <- next
            total <- total + float k
            rest <- rest - poissonLimit

        if total < maxInt then
            let struct (k, next) = Generator.poisson rest current
            current <- next
            total <- total + float k

        struct ((if total >= maxInt then Int32.MaxValue else int total), current)

    /// <summary>
    /// A value of the distribution and the next generator; a distribution with a single value gives it and the
    /// generator unchanged. The binomial is <c>Generator.binomial</c>; the Poisson <c>Generator.poisson</c>, above a
    /// mean of 2^30, the largest it takes, the sum of draws of mean 2^30 and one of the rest; the negative binomial a
    /// Poisson draw whose mean is a <c>Generator.gamma</c> draw of shape r and scale (1 − p)/p; the geometric
    /// ⌊ln u / ln(1 − p)⌋ with u one minus a <c>Generator.uniform</c> value; the hypergeometric takes items one at a
    /// time without replacement by <c>Generator.integer</c>, at cost O(draws); the Bernoulli is 1 where a uniform value
    /// is below p; the discrete uniform is <c>Generator.integer</c>. Each is exact in distribution, the geometric and
    /// Bernoulli to the 2^-32 spacing of <c>Generator.uniform</c>'s values (from documentation: its code in
    /// Generator.fs), which caps a geometric draw at ⌊32 ln 2 / −ln(1 − p)⌋ and makes the Bernoulli's P(1)
    /// 2^-32 · ⌈p · 2^32⌉, so 2.3e-10 for any p below 2^-32 (general knowledge, from that spacing). A draw beyond
    /// Int32.MaxValue gives Int32.MaxValue.
    /// </summary>
    let sample (d: Discrete) (g: Generator) : struct (int * Generator) =
        let struct (lower, upper) = support d

        if lower = upper then
            struct (lower, g)
        else
            match d with
            | Binomial (n, p) -> Generator.binomial n p g
            | Poisson mean -> poissonDraw mean g
            | Geometric p ->
                let struct (u, next) = Generator.uniform g
                let x = Math.Floor(log (1.0 - u) / Special.log1p (-p))
                struct ((if x >= maxInt then Int32.MaxValue else int x), next)
            | NegativeBinomial (r, p) ->
                let struct (lambda, next) = Generator.gamma r ((1.0 - p) / p) g
                poissonDraw lambda next
            | Hypergeometric (population, successes, draws) ->
                let mutable current = g
                let mutable remaining = population
                let mutable left = successes
                let mutable found = 0

                for _ in 1..draws do
                    let struct (j, next) = Generator.integer 0 remaining current
                    current <- next

                    if j < left then
                        found <- found + 1
                        left <- left - 1

                    remaining <- remaining - 1

                struct (found, current)
            | Bernoulli p ->
                let struct (u, next) = Generator.uniform g
                struct ((if u < p then 1 else 0), next)
            | DiscreteUniform (lower, upper) -> Generator.integer lower (upper + 1) g

    /// <summary>
    /// count values, drawn as count successive calls of <c>sample</c> would, and the next generator. Raises
    /// ArgumentOutOfRangeException when count is negative. Cost: O(count) draws.
    /// </summary>
    let samples (d: Discrete) (count: int) (g: Generator) : struct (int[] * Generator) =
        if count < 0 then
            raise (ArgumentOutOfRangeException(nameof count, "count must be zero or more."))

        let out: int[] = Array.zeroCreate count
        let mutable current = g

        for i in 0 .. count - 1 do
            let struct (x, next) = sample d current
            out[i] <- x
            current <- next

        struct (out, current)
