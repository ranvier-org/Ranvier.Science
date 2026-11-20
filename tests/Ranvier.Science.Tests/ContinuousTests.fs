// Reference values: the tables below, printed by
//   python -I tests/reference/special.py normal
//   python -I tests/reference/special.py lognormal
// and the closed forms written in each test.
module Ranvier.Science.Tests.ContinuousTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py normal
// x, N(0, 1) cdf, N(0, 1) survival
let private normalCases: struct (float * float * float)[] =
    [|
        struct (-37.0, 5.725571222524577e-300, 1.0)
        struct (-30.0, 4.906713927148187e-198, 1.0)
        struct (-10.0, 7.619853024160525e-24, 1.0)
        struct (-5.0, 2.866515718791939e-07, 0.9999997133484281)
        struct (-1.96, 0.024997895148220435, 0.9750021048517795)
        struct (-1.0, 0.15865525393145705, 0.8413447460685429)
        struct (-0.5, 0.3085375387259869, 0.6914624612740131)
        struct (0.0, 0.5, 0.5)
        struct (0.3, 0.6179114221889527, 0.3820885778110474)
        struct (1.0, 0.8413447460685429, 0.15865525393145705)
        struct (1.96, 0.9750021048517795, 0.024997895148220435)
        struct (3.0, 0.9986501019683699, 0.0013498980316300946)
        struct (5.0, 0.9999997133484281, 2.866515718791939e-07)
        struct (8.0, 0.9999999999999993, 6.220960574271784e-16)
        struct (10.0, 1.0, 7.619853024160525e-24)
        struct (30.0, 1.0, 4.906713927148187e-198)
        struct (37.0, 1.0, 5.725571222524577e-300)
    |]

// x, N(3, 2) cdf
let private normalShiftedCases: struct (float * float)[] =
    [|
        struct (-5.0, 3.1671241833119924e-05)
        struct (1.0, 0.15865525393145705)
        struct (3.0, 0.5)
        struct (7.5, 0.9877755273449553)
    |]

// p, N(0, 1) quantile
let private normalQuantileCases: struct (float * float)[] =
    [|
        struct (1e-300, -37.0470962993612)
        struct (1e-100, -21.273453560965326)
        struct (1e-10, -6.361340902404057)
        struct (0.025, -1.9599639845400543)
        struct (0.3, -0.5244005127080408)
        struct (0.975, 1.9599639845400538)
        struct (0.9999999999, 6.361340889697422)
    |]

// python -I tests/reference/special.py lognormal
// x, LN(0.5, 0.8) cdf, LN(0.5, 0.8) survival
let private logNormalCases: struct (float * float * float)[] =
    [|
        struct (1e-10, 2.2139633536948147e-190, 1.0)
        struct (0.1, 0.00022982501310415206, 0.9997701749868958)
        struct (1.0, 0.26598552904870054, 0.7340144709512995)
        struct (2.0, 0.595390608679215, 0.40460939132078505)
        struct (10.0, 0.9878777176649812, 0.012122282335018802)
        struct (1000.0, 0.9999999999999994, 5.749705686405583e-16)
    |]

// python -I tests/reference/special.py weibull
// k, scale, variance
let private weibullVarianceCases: struct (float * float * float)[] =
    [|
        struct (10.0, 1.0, 0.01310045507346831)
        struct (10.0, 1.7, 0.03786031516232341)
        struct (100.0, 1.0, 0.00016030491620026111)
        struct (100.0, 1.7, 0.0004632812078187546)
        struct (10000.0, 1.0, 1.6445038762822376e-08)
        struct (10000.0, 1.7, 4.7526162024556667e-08)
        struct (100000000.0, 1.0, 1.6449340238174553e-16)
        struct (100000000.0, 1.7, 4.753859328832446e-16)
    |]

// k, scale, x, survival
let private weibullSurvivalCases: struct (float * float * float * float)[] =
    [|
        struct (20.0, 1.7, 1.2, 0.9990571520175738)
        struct (20.0, 1.7, 1.6, 0.7427060286258715)
        struct (20.0, 1.7, 1.7, 0.36787944117144233)
        struct (20.0, 1.7, 1.8, 0.0434262777646477)
        struct (20.0, 1.7, 2.0, 6.239595687369753e-12)
        struct (20.0, 1.7, 2.2, 4.163087892779429e-76)
        struct (1000.0, 1.7, 1.69, 0.9972635447961798)
        struct (1000.0, 1.7, 1.7, 0.36787944117144233)
        struct (1000.0, 1.7, 1.7017, 0.06607770161458344)
        struct (1000.0, 1.7, 1.705, 6.467103253477042e-09)
    |]

let private rel (a: float) (b: float) : float = abs (a - b) / abs b

let private lowerGrid = [ 1e-100; 1e-10; 0.01; 0.3; 0.5 ]
let private upperGrid = [ 0.6; 0.975; 0.999; 1.0 - 1e-10 ]

/// A reference value below 1e-300 is outside the stated bound and is skipped.
let private matches (name: string) (actual: float) (reference: float) =
    if reference >= 1e-300 then
        Expect.isLessThanOrEqual
            (rel actual reference)
            1e-12
            (name + ": " + string actual + " against " + string reference)

/// matches with a stated bound in place of 1e-12.
let private matchesWithin (bound: float) (name: string) (actual: float) (reference: float) =
    if reference >= 1e-300 then
        Expect.isLessThanOrEqual (rel actual reference) bound (name + ": " + string actual + " against " + string reference)

let private roundTrip (d: Continuous) (p: float) =
    let q = Continuous.quantile d p

    if p <= 0.5 then
        Expect.isLessThanOrEqual
            (rel (Continuous.cdf d q) p)
            1e-12
            (Continuous.describe d + " cdf at the quantile of " + string p)
    else
        Expect.isLessThanOrEqual
            (rel (Continuous.survival d q) (1.0 - p))
            1e-12
            (Continuous.describe d + " survival at the quantile of " + string p)

/// The Kolmogorov-Smirnov statistic of the sample against a distribution function.
let private ksDistance (cdf: float -> float) (xs: float[]) : float =
    let sorted = Array.sort xs
    let n = float sorted.Length
    let mutable d = 0.0

    for i in 0 .. sorted.Length - 1 do
        let f = cdf sorted[i]
        d <- max d (max (float (i + 1) / n - f) (f - float i / n))

    d

let private families: Continuous list =
    [
        Continuous.normal 0.0 1.0
        Continuous.normal 3.0 2.0
        Continuous.logNormal 0.5 0.8
        Continuous.uniform 0.0 4.0
        Continuous.exponential 2.0
        Continuous.cauchy 0.0 1.0
        Continuous.weibull 1.5 2.0
        Continuous.logistic 1.0 2.0
    ]

let private isUniform (d: Continuous) = d = Continuous.uniform 0.0 4.0

let private pi = Math.PI

[<Tests>]
let tests =
    testList
        "Continuous"
        [
            testCase "normal cdf and survival against the reference" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for row in normalCases do
                    let struct (x, c, s) = row
                    matches ("cdf at " + string x) (Continuous.cdf d x) c
                    matches ("survival at " + string x) (Continuous.survival d x) s

                let shifted = Continuous.normal 3.0 2.0

                for row in normalShiftedCases do
                    let struct (x, c) = row
                    matches ("N(3, 2) cdf at " + string x) (Continuous.cdf shifted x) c)

            testCase "normal quantile against the reference" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for row in normalQuantileCases do
                    let struct (p, q) = row
                    Expect.isLessThanOrEqual (rel (Continuous.quantile d p) q) 1e-12 ("quantile at " + string p)

                Expect.equal (Continuous.quantile d 0.5) 0.0 "the median")

            testCase "normal density" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for x in [ 0.0; 1.0; 5.0 ] do
                    let expected = exp (-x * x / 2.0) / sqrt (2.0 * pi)
                    Expect.isLessThanOrEqual (abs (Continuous.density d x - expected)) 1e-13 ("density at " + string x)

                Expect.isLessThanOrEqual
                    (abs (Continuous.logDensity d 30.0 - (-450.0 - 0.5 * log (2.0 * pi))))
                    1e-13
                    "logDensity at 30")

            testCase "quantile round trips" (fun () ->
                for d in families do
                    for p in lowerGrid do
                        roundTrip d p

                    for p in upperGrid do
                        if not (isUniform d && p = 1.0 - 1e-10) then
                            roundTrip d p

                for d in
                    [
                        Continuous.normal 0.0 1.0
                        Continuous.exponential 2.0
                        Continuous.logistic 1.0 2.0
                        Continuous.cauchy 0.0 1.0
                    ] do
                    roundTrip d 1e-300)

            testCase "quantile at 0 and 1 is the support's bounds" (fun () ->
                for d in families do
                    let struct (lower, upper) = Continuous.support d
                    Expect.equal (Continuous.quantile d 0.0) lower (Continuous.describe d + " at 0")
                    Expect.equal (Continuous.quantile d 1.0) upper (Continuous.describe d + " at 1")
                    Expect.isTrue (Double.IsNaN(Continuous.quantile d nan)) (Continuous.describe d + " at NaN")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.quantile d -0.1 |> ignore)
                        (Continuous.describe d + " at -0.1")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.quantile d 1.1 |> ignore)
                        (Continuous.describe d + " at 1.1"))

            testCase "logNormal against the reference" (fun () ->
                let d = Continuous.logNormal 0.5 0.8

                for row in logNormalCases do
                    let struct (x, c, s) = row
                    matches ("cdf at " + string x) (Continuous.cdf d x) c
                    matches ("survival at " + string x) (Continuous.survival d x) s

                Expect.isLessThanOrEqual (rel (Continuous.mean d) (exp (0.5 + 0.32))) 1e-14 "mean"

                Expect.isLessThanOrEqual
                    (rel (Continuous.variance d) ((exp 0.64 - 1.0) * exp (1.0 + 0.64)))
                    1e-14
                    "variance")

            testCase "exponential closed forms" (fun () ->
                let d = Continuous.exponential 2.0
                // cdf(x) = 1 - e^(-2x) = 2x (1 - x + ...), so at 1e-20 it is 2e-20 (1 - 1e-20) to 1e-40.
                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d 1e-20) (2e-20 * (1.0 - 1e-20)))
                    1e-14
                    "cdf near 0"

                Expect.isLessThanOrEqual (rel (Continuous.survival d 300.0) (exp -600.0)) 1e-13 "survival at 300"
                Expect.equal (Continuous.mean d) 0.5 "mean"
                Expect.equal (Continuous.variance d) 0.25 "variance")

            testCase "uniform closed forms" (fun () ->
                let d = Continuous.uniform 0.0 4.0
                Expect.equal (Continuous.cdf d 1.0) 0.25 "cdf at 1"

                for x in [ 0.0; 1.0; 4.0 ] do
                    Expect.equal (Continuous.density d x) 0.25 ("density at " + string x)

                for x in [ -1.0; 4.5 ] do
                    Expect.equal (Continuous.density d x) 0.0 ("density at " + string x)

                for p in [ 0.0; 1e-300; 0.1; 0.3; 0.5; 0.77; 1.0 ] do
                    Expect.equal (Continuous.quantile d p) (4.0 * p) ("quantile at " + string p)

                Expect.equal (Continuous.mean d) 2.0 "mean"
                Expect.equal (Continuous.variance d) (16.0 / 12.0) "variance")

            testCase "cauchy tails" (fun () ->
                let d = Continuous.cauchy 0.0 1.0

                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d -1e100) (1e-100 / pi))
                    1e-14
                    "cdf far in the lower tail"

                Expect.equal (Continuous.cdf d 0.0) 0.5 "cdf at 0"
                Expect.isLessThanOrEqual (abs (Continuous.cdf d 1.0 - 0.75)) 1e-15 "cdf at 1"
                // atan(1/z) / pi = 1 / (z pi) (1 - 1 / (3 z^2) + ...), the next term 3e-21 of the first at z = 1e10.
                Expect.isLessThanOrEqual (rel (Continuous.survival d 1e10) (1e-10 / pi)) 1e-14 "survival at 1e10"
                Expect.isLessThanOrEqual (abs (Continuous.quantile d 0.75 - 1.0)) 1e-15 "quantile at 0.75"
                Expect.isTrue (Double.IsNaN(Continuous.mean d)) "mean"
                Expect.isTrue (Double.IsNaN(Continuous.variance d)) "variance")

            testCase "weibull closed forms" (fun () ->
                let d = Continuous.weibull 1.5 2.0
                // 1 - e^(-t) = t (1 - t / 2 + ...) with t = (x / scale)^shape = 1e-14^1.5 at x = 2e-14.
                let t = 1e-14 ** 1.5

                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d 2e-14) (t * (1.0 - t / 2.0)))
                    1e-13
                    "cdf near 0"

                Expect.isLessThanOrEqual
                    (rel (Continuous.survival d 20.0) (exp (-(10.0 ** 1.5))))
                    1e-13
                    "survival at 20"

                let d2 = Continuous.weibull 2.0 3.0
                // mean = scale Gamma(3/2) = 3 sqrt(pi) / 2; variance = scale^2 (Gamma(2) - Gamma(3/2)^2) = 9 (1 - pi / 4).
                Expect.isLessThanOrEqual (rel (Continuous.mean d2) (3.0 * sqrt pi / 2.0)) 1e-14 "mean"
                Expect.isLessThanOrEqual (rel (Continuous.variance d2) (9.0 * (1.0 - pi / 4.0))) 1e-14 "variance"
                Expect.equal (Continuous.density (Continuous.weibull 0.5 1.0) 0.0) infinity "density at 0, shape 1/2")

            testCase "weibull variance against the reference at large shape" (fun () ->
                for row in weibullVarianceCases do
                    let struct (k, scale, v) = row

                    Expect.isLessThanOrEqual
                        (rel (Continuous.variance (Continuous.weibull k scale)) v)
                        1e-13
                        ("variance at shape " + string k + ", scale " + string scale)

                // The direct difference of gamma values goes negative from a shape of about 1e8.
                Expect.isTrue (Continuous.variance (Continuous.weibull 1e8 1.0) > 0.0) "positive at shape 1e8"
                Expect.isTrue (Continuous.standardDeviation (Continuous.weibull 1e8 1.0) > 0.0) "sd at shape 1e8")

            testCase "weibull survival at a large shape and a scale that is not a power of two" (fun () ->
                for row in weibullSurvivalCases do
                    let struct (k, scale, x, sf) = row
                    let d = Continuous.weibull k scale
                    // x / scale rounds to half an ulp and is raised to the shape: the error grows as shape * (x / scale)^shape.
                    let bound = if k <= 20.0 then 1e-12 else 1e-9
                    matchesWithin bound ("survival at shape " + string k + ", x " + string x) (Continuous.survival d x) sf)

            testCase "log-normal quantile round trip at a large mu" (fun () ->
                // exp (mu + sigma * z) rounds its argument to half an ulp of mu: the error grows as |mu| * |z| / sigma.
                for p in lowerGrid @ upperGrid @ [ 1e-300 ] do
                    roundTrip (Continuous.logNormal 50.0 0.1) p

                let d = Continuous.logNormal 300.0 0.1

                for p in [ 1e-300; 1e-100; 1e-10; 0.3; 0.975 ] do
                    let q = Continuous.quantile d p

                    let err =
                        if p <= 0.5 then
                            rel (Continuous.cdf d q) p
                        else
                            rel (Continuous.survival d q) (1.0 - p)

                    Expect.isLessThanOrEqual err 2e-11 ("round trip at " + string p))

            testCase "logistic closed forms" (fun () ->
                let d = Continuous.logistic 1.0 2.0
                let expected = 1.0 / (1.0 + exp 700.0)
                Expect.isLessThanOrEqual (rel (Continuous.cdf d -1399.0) expected) 1e-13 "cdf in the lower tail"
                Expect.isLessThanOrEqual (rel (Continuous.survival d 1401.0) expected) 1e-13 "survival in the upper tail"
                Expect.equal (Continuous.mean d) 1.0 "mean"
                Expect.isLessThanOrEqual (rel (Continuous.variance d) (4.0 * pi * pi / 3.0)) 1e-14 "variance")

            testCase "the density integrates to the cdf" (fun () ->
                for d in families do
                    let a = Continuous.quantile d 0.1
                    let b = Continuous.quantile d 0.9
                    let integral = Integrate.adaptive 1e-14 1e-12 (Continuous.density d) a b
                    Expect.isTrue integral.Converged (Continuous.describe d + " converged")

                    Expect.isLessThanOrEqual
                        (abs (integral.Value - (Continuous.cdf d b - Continuous.cdf d a)))
                        1e-10
                        (Continuous.describe d + " integral"))

            testCase "support" (fun () ->
                let expected =
                    [
                        Continuous.normal 0.0 1.0, struct (-infinity, infinity)
                        Continuous.logNormal 0.5 0.8, struct (0.0, infinity)
                        Continuous.uniform 0.0 4.0, struct (0.0, 4.0)
                        Continuous.exponential 2.0, struct (0.0, infinity)
                        Continuous.cauchy 0.0 1.0, struct (-infinity, infinity)
                        Continuous.weibull 1.5 2.0, struct (0.0, infinity)
                        Continuous.logistic 1.0 2.0, struct (-infinity, infinity)
                    ]

                for d, bounds in expected do
                    Expect.equal (Continuous.support d) bounds (Continuous.describe d)

                for d in families do
                    let sd = Continuous.standardDeviation d
                    let variance = Continuous.variance d

                    if Double.IsNaN variance then
                        Expect.isTrue (Double.IsNaN sd) (Continuous.describe d + " standard deviation")
                    else
                        Expect.equal sd (sqrt variance) (Continuous.describe d + " standard deviation"))

            testCase "NaN and infinite arguments" (fun () ->
                for d in families do
                    let name = Continuous.describe d
                    Expect.isTrue (Double.IsNaN(Continuous.cdf d nan)) (name + " cdf at NaN")
                    Expect.isTrue (Double.IsNaN(Continuous.survival d nan)) (name + " survival at NaN")
                    Expect.isTrue (Double.IsNaN(Continuous.density d nan)) (name + " density at NaN")
                    Expect.equal (Continuous.cdf d -infinity) 0.0 (name + " cdf at -infinity")
                    Expect.equal (Continuous.cdf d infinity) 1.0 (name + " cdf at infinity")
                    Expect.equal (Continuous.survival d -infinity) 1.0 (name + " survival at -infinity")
                    Expect.equal (Continuous.survival d infinity) 0.0 (name + " survival at infinity")
                    Expect.equal (Continuous.density d infinity) 0.0 (name + " density at infinity")
                    Expect.equal (Continuous.density d -infinity) 0.0 (name + " density at -infinity")
                    Expect.equal (Continuous.logDensity d infinity) -infinity (name + " logDensity at infinity")

                for d, outside in
                    [
                        Continuous.logNormal 0.5 0.8, [ -1.0; 0.0 ]
                        Continuous.uniform 0.0 4.0, [ -1.0; 4.5 ]
                        Continuous.exponential 2.0, [ -1.0 ]
                        Continuous.weibull 1.5 2.0, [ -1.0 ]
                    ] do
                    for x in outside do
                        let name = Continuous.describe d + " at " + string x
                        Expect.equal (Continuous.density d x) 0.0 (name + " density")
                        Expect.equal (Continuous.logDensity d x) -infinity (name + " logDensity"))

            testCase "describe" (fun () ->
                Expect.equal (Continuous.describe (Continuous.normal 0.0 1.0)) "Normal(mean = 0, sd = 1)" "normal"

                Expect.equal
                    (Continuous.describe (Continuous.logNormal 0.5 0.8))
                    "LogNormal(mu = 0.5, sigma = 0.8)"
                    "logNormal"

                Expect.equal
                    (Continuous.describe (Continuous.uniform 0.0 4.0))
                    "Uniform(lower = 0, upper = 4)"
                    "uniform"

                Expect.equal (Continuous.describe (Continuous.exponential 2.0)) "Exponential(rate = 2)" "exponential"

                Expect.equal
                    (Continuous.describe (Continuous.cauchy 0.0 1.0))
                    "Cauchy(location = 0, scale = 1)"
                    "cauchy"

                Expect.equal
                    (Continuous.describe (Continuous.weibull 1.5 2.0))
                    "Weibull(shape = 1.5, scale = 2)"
                    "weibull"

                Expect.equal
                    (Continuous.describe (Continuous.logistic 1.0 2.0))
                    "Logistic(location = 1, scale = 2)"
                    "logistic")

            testCase "constructors reject parameters outside their domain" (fun () ->
                let rejects (name: string) (build: unit -> Continuous) =
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> build () |> ignore) name

                rejects "normal 0 0" (fun () -> Continuous.normal 0.0 0.0)
                rejects "normal 0 -1" (fun () -> Continuous.normal 0.0 -1.0)
                rejects "normal 0 nan" (fun () -> Continuous.normal 0.0 nan)
                rejects "normal infinity 1" (fun () -> Continuous.normal infinity 1.0)
                rejects "logNormal 0 0" (fun () -> Continuous.logNormal 0.0 0.0)
                rejects "uniform 1 1" (fun () -> Continuous.uniform 1.0 1.0)
                rejects "uniform 2 1" (fun () -> Continuous.uniform 2.0 1.0)
                rejects "uniform -infinity 0" (fun () -> Continuous.uniform -infinity 0.0)
                rejects "exponential 0" (fun () -> Continuous.exponential 0.0)
                rejects "exponential infinity" (fun () -> Continuous.exponential infinity)
                rejects "cauchy 0 0" (fun () -> Continuous.cauchy 0.0 0.0)
                rejects "weibull 0 1" (fun () -> Continuous.weibull 0.0 1.0)
                rejects "weibull 1 0" (fun () -> Continuous.weibull 1.0 0.0)
                rejects "logistic 0 -1" (fun () -> Continuous.logistic 0.0 -1.0))

            testCase "equal parameters give equal distributions" (fun () ->
                Expect.equal (Continuous.normal 0.0 1.0) (Continuous.normal 0.0 1.0) "same parameters"
                Expect.notEqual (Continuous.normal 0.0 1.0) (Continuous.normal 0.0 2.0) "different parameters")

            testCase "samples follow the distribution" (fun () ->
                let count = 20000
                // The asymptotic Kolmogorov critical value at alpha = 1e-4, sqrt(-ln(alpha / 2) / 2) = 2.226
                // (general knowledge), divided by sqrt n.
                let critical = 2.226 / sqrt (float count)

                for d in families do
                    let name = Continuous.describe d
                    let struct (xs, final) = Continuous.samples d count (Generator.create 2026)
                    let ks = ksDistance (Continuous.cdf d) xs
                    Expect.isLessThanOrEqual ks critical (name + " Kolmogorov-Smirnov distance " + string ks)

                    let sd = Continuous.standardDeviation d

                    if not (Double.IsNaN sd) then
                        let mean = Array.sum xs / float count

                        Expect.isLessThanOrEqual
                            (abs (mean - Continuous.mean d))
                            (5.0 * sd / sqrt (float count))
                            (name + " sample mean")

                    let mutable g = Generator.create 2026

                    for i in 0 .. count - 1 do
                        let struct (x, next) = Continuous.sample d g
                        g <- next

                        if x <> xs[i] then
                            Expect.equal x xs[i] (name + " sample " + string i)

                    Expect.equal g final (name + " final generator")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.samples d -1 (Generator.create 1) |> ignore)
                        (name + " with a negative count"))
        ]
