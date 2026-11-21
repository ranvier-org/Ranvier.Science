// Reference values: the tables below, printed by
//   python -I tests/reference/special.py intervals
// and the closed forms and identities written in each test.
module Ranvier.Science.Tests.IntervalTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py intervals
// alpha, lower, upper; the proportion tables add the successes and trials, the known-sigma table sigma
let private studentIntervalCases: struct (float * float * float)[] =
    [|
        struct (0.05, 7.788684321969954, 12.211315678030047)
        struct (0.01, 6.823203797541367, 13.176796202458632)
    |]

let private knownSigmaIntervalCases: struct (float * float * float * float)[] =
    [|
        struct (0.05, 3.0, 8.140614903086314, 11.859385096913686)
    |]

let private varianceIntervalCases: struct (float * float * float)[] =
    [|
        struct (0.05, 4.520898373481751, 31.84725759029412)
    |]

let private wilsonCases: struct (float * int * int * float * float)[] =
    [|
        struct (0.05, 81, 263, 0.2552885198782742, 0.36620957698280004)
        struct (0.05, 0, 20, 0.0, 0.16112515805281938)
        struct (0.05, 20, 20, 0.8388748419471806, 1.0)
        struct (0.05, 1, 29, 0.006113214292762661, 0.17175521879320288)
        struct (0.01, 81, 263, 0.24013690268760768, 0.38528250231822986)
    |]

let private clopperPearsonCases: struct (float * int * int * float * float)[] =
    [|
        struct (0.05, 81, 263, 0.25273674558527115, 0.36762192260135135)
        struct (0.05, 0, 20, 0.0, 0.16843347098308534)
        struct (0.05, 20, 20, 0.8315665290169146, 1.0)
        struct (0.05, 1, 29, 0.000872646883579922, 0.17764429548872293)
        struct (0.01, 81, 263, 0.23683735816553864, 0.386258325669745)
    |]

let private agrestiCoullCases: struct (float * int * int * float * float)[] =
    [|
        struct (0.05, 81, 263, 0.25522066518999675, 0.3662774316710775)
        struct (0.05, 0, 20, 0.0, 0.18980956054248885)
        struct (0.05, 20, 20, 0.8101904394575111, 1.0)
        struct (0.05, 1, 29, 0.0, 0.1862865085617061)
        struct (0.01, 81, 263, 0.23998703260147503, 0.3854323724043625)
    |]

let private jeffreysCases: struct (float * int * int * float * float)[] =
    [|
        struct (0.05, 81, 263, 0.2545219349910235, 0.3656474991740824)
        struct (0.05, 0, 20, 0.0, 0.11663898290487543)
        struct (0.05, 20, 20, 0.8833610170951246, 1.0)
        struct (0.05, 1, 29, 0.003746173649057074, 0.15007768600223176)
        struct (0.01, 81, 263, 0.238580047660932, 0.38426823916736547)
    |]

let private welchIntervalCases: struct (float * float * float)[] =
    [|
        struct (0.05, -3.365483230711711, 0.20548323071171096)
    |]

let private pooledIntervalCases: struct (float * float * float)[] =
    [|
        struct (0.05, -3.3638740322875984, 0.20387403228759854)
    |]

let private moments (xs: float[]) : Moments = Array.fold (Moments.add true) Moments.zero xs

let private dataA = [| 5.0; 7.0; 8.0; 9.0; 9.0; 10.0; 11.0; 12.0; 14.0; 15.0 |]

// R's datasets::sleep, extra by group.
let private sleepA = [| 0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0 |]
let private sleepB = [| 1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4 |]

let private isNaN (x: float) : bool = Double.IsNaN x

// Each bound within 1e-12 of the larger bound's magnitude, relative.
let private within (message: string) (actual: Interval) (lower: float) (upper: float) : unit =
    let tolerance = 1e-12 * max (abs lower) (abs upper)

    Expect.isLessThanOrEqual
        (abs (actual.Lower - lower))
        tolerance
        (message + " lower: " + string actual.Lower + " against " + string lower)

    Expect.isLessThanOrEqual
        (abs (actual.Upper - upper))
        tolerance
        (message + " upper: " + string actual.Upper + " against " + string upper)

let private bothNaN (message: string) (actual: Interval) : unit =
    Expect.isTrue (isNaN actual.Lower) (message + " lower is NaN")
    Expect.isTrue (isNaN actual.Upper) (message + " upper is NaN")

let private proportionMethods: (string * (float -> int -> int -> Interval))[] =
    [|
        "wilson", ProportionInterval.wilson
        "clopperPearson", ProportionInterval.clopperPearson
        "agrestiCoull", ProportionInterval.agrestiCoull
        "jeffreys", ProportionInterval.jeffreys
    |]

let private proportionReference
    (name: string)
    (f: float -> int -> int -> Interval)
    (cases: struct (float * int * int * float * float)[])
    : unit =
    for struct (alpha, successes, trials, lower, upper) in cases do
        within
            (name + " " + string successes + "/" + string trials + " at " + string alpha)
            (f alpha successes trials)
            lower
            upper

[<Tests>]
let tests =
    testList
        "Intervals"
        [
            testCase "the Student interval against the reference" (fun () ->
                for struct (alpha, lower, upper) in studentIntervalCases do
                    within ("alpha " + string alpha) (MeanInterval.student alpha (moments dataA)) lower upper)

            testCase "the known-sigma interval against the reference" (fun () ->
                for struct (alpha, sigma, lower, upper) in knownSigmaIntervalCases do
                    within ("alpha " + string alpha) (MeanInterval.knownSigma alpha sigma (moments dataA)) lower upper)

            testCase "the variance interval against the reference" (fun () ->
                for struct (alpha, lower, upper) in varianceIntervalCases do
                    within ("alpha " + string alpha) (VarianceInterval.chiSquared alpha (moments dataA)) lower upper)

            testCase "proportion intervals against the reference" (fun () ->
                proportionReference "wilson" ProportionInterval.wilson wilsonCases
                proportionReference "clopperPearson" ProportionInterval.clopperPearson clopperPearsonCases
                proportionReference "agrestiCoull" ProportionInterval.agrestiCoull agrestiCoullCases
                proportionReference "jeffreys" ProportionInterval.jeffreys jeffreysCases)

            testCase "difference intervals against the reference" (fun () ->
                let a = moments sleepA
                let b = moments sleepB

                for struct (alpha, lower, upper) in welchIntervalCases do
                    within "welch" (DifferenceInterval.welch alpha a b) lower upper

                for struct (alpha, lower, upper) in pooledIntervalCases do
                    within "pooled" (DifferenceInterval.pooled alpha a b) lower upper)

            testCase "Clopper-Pearson at one trial and at zero successes has a closed form" (fun () ->
                // Beta(1, n) has cdf 1 - (1 - x)^n, so the upper bound at 0 successes is 1 - (alpha/2)^(1/n); Beta(1, 1)
                // is uniform, so at (0, 1) the bounds are 0 and 1 - alpha/2, and at (1, 1) they are alpha/2 and 1.
                for alpha in [ 0.05; 0.01 ] do
                    for n in [ 1; 5; 20; 100 ] do
                        within
                            ("0/" + string n)
                            (ProportionInterval.clopperPearson alpha 0 n)
                            0.0
                            (1.0 - Math.Pow(alpha / 2.0, 1.0 / float n))

                    within "0/1" (ProportionInterval.clopperPearson alpha 0 1) 0.0 (1.0 - alpha / 2.0)
                    within "1/1" (ProportionInterval.clopperPearson alpha 1 1) (alpha / 2.0) 1.0)

            testCase "proportion intervals at 0 and n successes" (fun () ->
                let alpha = 0.05

                for name, f in proportionMethods do
                    Expect.equal (f alpha 0 20).Lower 0.0 (name + " lower at 0/20")
                    Expect.equal (f alpha 20 20).Upper 1.0 (name + " upper at 20/20")

                for name, f in proportionMethods do
                    for successes, trials in
                        [ 0, 1; 1, 1; 0, 2; 1, 2; 2, 2; 0, 20; 1, 20; 19, 20; 20, 20; 81, 263; 1, 29; 0, 1000; 1000, 1000 ] do
                        for a in [ 0.05; 0.01; 0.5 ] do
                            let interval = f a successes trials
                            let where = name + " " + string successes + "/" + string trials + " at " + string a
                            Expect.isTrue (interval.Lower >= 0.0 && interval.Lower <= 1.0) (where + " lower in [0, 1]")
                            Expect.isTrue (interval.Upper >= 0.0 && interval.Upper <= 1.0) (where + " upper in [0, 1]")
                            Expect.isTrue (interval.Lower <= interval.Upper) (where + " ordered"))

            testCase "small samples give NaN bounds" (fun () ->
                let one = moments [| 3.0 |]
                let two = moments [| 3.0; 4.0 |]
                bothNaN "student at count 1" (MeanInterval.student 0.05 one)
                bothNaN "student at count 0" (MeanInterval.student 0.05 Moments.zero)
                bothNaN "chiSquared at count 1" (VarianceInterval.chiSquared 0.05 one)
                bothNaN "knownSigma at count 0" (MeanInterval.knownSigma 0.05 1.0 Moments.zero)
                bothNaN "welch, first count 1" (DifferenceInterval.welch 0.05 one two)
                bothNaN "welch, second count 1" (DifferenceInterval.welch 0.05 two one)
                bothNaN "welch, counts 1 and 1" (DifferenceInterval.welch 0.05 one one)
                bothNaN "pooled, counts 1 and 1" (DifferenceInterval.pooled 0.05 one one)
                bothNaN "pooled, count 0" (DifferenceInterval.pooled 0.05 Moments.zero two)

                for name, f in proportionMethods do
                    bothNaN (name + " at 0 trials") (f 0.05 0 0))

            testCase "pooled with one observation in a sample uses the other sample's variance" (fun () ->
                // a = [x], b of count m: the pooled variance is s²_b, with m - 1 degrees of freedom, and the half width
                // t · s_b · sqrt(1 + 1/m). At 2 degrees of freedom the t quantile at p has the closed form
                // (2p - 1) / sqrt(2p(1 - p)).
                let x = 10.0
                let b = [| 1.0; 2.0; 4.0 |]
                let p = 0.975
                let t = (2.0 * p - 1.0) / sqrt (2.0 * p * (1.0 - p))
                let mean = 7.0 / 3.0
                let variance = ((1.0 - mean) ** 2.0 + (2.0 - mean) ** 2.0 + (4.0 - mean) ** 2.0) / 2.0
                let half = t * sqrt variance * sqrt (1.0 + 1.0 / 3.0)
                let d = x - mean
                let interval = DifferenceInterval.pooled 0.05 (moments [| x |]) (moments b)
                within "single observation first" interval (d - half) (d + half)
                let reversed = DifferenceInterval.pooled 0.05 (moments b) (moments [| x |])
                within "single observation second" reversed (-d - half) (-d + half))

            testCase "zero spread gives a zero-width interval" (fun () ->
                let a = moments [| 2.0; 2.0; 2.0 |]
                let b = moments [| 5.0; 5.0 |]
                let welch = DifferenceInterval.welch 0.05 a b
                let pooled = DifferenceInterval.pooled 0.05 a b
                Expect.equal welch.Lower (-3.0) "welch lower"
                Expect.equal welch.Upper (-3.0) "welch upper"
                Expect.equal pooled.Lower (-3.0) "pooled lower"
                Expect.equal pooled.Upper (-3.0) "pooled upper"
                let student = MeanInterval.student 0.05 a
                Expect.equal student.Lower 2.0 "student lower"
                Expect.equal student.Upper 2.0 "student upper")

            testCase "a spread the moments cannot resolve gives NaN bounds, not a zero-width interval" (fun () ->
                // The sleep groups scaled by 1e-200 (the squared deviations underflow to 0) and 1e-162 (a subnormal
                // sum of squares), and shifted by 10 and scaled by 1e155 without compensation (it overflows).
                let overflowed (xs: float[]) =
                    Array.fold (Moments.add false) Moments.zero (Array.map (fun x -> (x + 10.0) * 1e155) xs)

                let samples =
                    [|
                        "1e-200", moments (Array.map (fun x -> x * 1e-200) sleepA), moments (Array.map (fun x -> x * 1e-200) sleepB)
                        "1e-162", moments (Array.map (fun x -> x * 1e-162) sleepA), moments (Array.map (fun x -> x * 1e-162) sleepB)
                        "1e155 uncompensated", overflowed sleepA, overflowed sleepB
                    |]

                for (label, a, b) in samples do
                    bothNaN ("student " + label) (MeanInterval.student 0.05 a)
                    bothNaN ("chiSquared " + label) (VarianceInterval.chiSquared 0.05 a)
                    bothNaN ("welch " + label) (DifferenceInterval.welch 0.05 a b)
                    bothNaN ("pooled " + label) (DifferenceInterval.pooled 0.05 a b))

            testCase "arguments out of range raise" (fun () ->
                let a = moments dataA
                let b = moments sleepB

                let raises (message: string) (f: unit -> Interval) : unit =
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> f () |> ignore) message

                for alpha in [ 0.0; 1.0; nan; -0.1; 1.5 ] do
                    let where = " at alpha " + string alpha
                    raises ("student" + where) (fun () -> MeanInterval.student alpha a)
                    raises ("knownSigma" + where) (fun () -> MeanInterval.knownSigma alpha 3.0 a)
                    raises ("chiSquared" + where) (fun () -> VarianceInterval.chiSquared alpha a)
                    raises ("welch" + where) (fun () -> DifferenceInterval.welch alpha a b)
                    raises ("pooled" + where) (fun () -> DifferenceInterval.pooled alpha a b)
                    // The alpha check precedes the small-count return.
                    raises ("student at count 1" + where) (fun () -> MeanInterval.student alpha (moments [| 1.0 |]))

                    for name, f in proportionMethods do
                        raises (name + where) (fun () -> f alpha 5 10)
                        raises (name + " at 0 trials" + where) (fun () -> f alpha 0 0)

                for sigma in [ 0.0; -1.0; infinity; nan ] do
                    raises ("knownSigma at sigma " + string sigma) (fun () -> MeanInterval.knownSigma 0.05 sigma a)

                for name, f in proportionMethods do
                    raises (name + " with successes -1") (fun () -> f 0.05 (-1) 10)
                    raises (name + " with successes above trials") (fun () -> f 0.05 11 10)
                    raises (name + " with trials -1") (fun () -> f 0.05 0 (-1))
                    raises (name + " with successes 1 at 0 trials") (fun () -> f 0.05 1 0))

            testCase "the Student interval covers at its level" (fun () ->
                // 2,000 seeded samples of 10 standard normal deviates: the fraction of 95% intervals holding 0 has
                // standard error sqrt(0.95 * 0.05 / 2000) = 0.0049, so [0.925, 0.975] is 5 standard errors either side.
                let mutable covered = 0

                for seed in 1..2000 do
                    let struct (xs, _) = Generator.normals 0.0 1.0 10 (Generator.create seed)
                    let interval = MeanInterval.student 0.05 (moments xs)

                    if interval.Lower <= 0.0 && 0.0 <= interval.Upper then
                        covered <- covered + 1

                let fraction = float covered / 2000.0
                Expect.isLessThanOrEqual 0.925 fraction ("the fraction " + string fraction + " is at least 0.925")
                Expect.isLessThanOrEqual fraction 0.975 ("the fraction " + string fraction + " is at most 0.975"))
        ]
