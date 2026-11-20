// Reference values: the tables below, printed by
//   python -I tests/reference/special.py parametric
// and the closed forms and identities written in each test. The sleep data are R's datasets::sleep. The statistics
// of the sleep Welch and paired tests (-1.860813, 17.77647 and -4.062128) were also reproduced with Python's
// statistics module by the plan author (measured by the plan author, not re-run here); no test asserts R's own
// printed output.
module Ranvier.Science.Tests.HypothesisTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py parametric
// statistic, degrees of freedom (two for the F), then the Less, Greater and TwoSided p-values
let private oneSampleTCases: struct (float * float * float * float * float)[] =
    [|
        struct (1.3257101407138212, 9.0, 0.8912011099657755, 0.10879889003422448, 0.21759778006844896)
        struct (-4.062127683382037, 9.0, 0.0014164450986921357, 0.9985835549013079, 0.0028328901973842715)
    |]

let private oneSampleZCases: struct (float * float * float * float)[] =
    [|
        struct (0.5270462766947299, 0.7009192736582359, 0.2990807263417641, 0.5981614526835282)
    |]

let private welchCases: struct (float * float * float * float * float)[] =
    [|
        struct (-1.860813467486853, 17.77647351617849, 0.03969707009367907, 0.9603029299063209, 0.07939414018735814)
    |]

let private pooledCases: struct (float * float * float * float * float)[] =
    [|
        struct (-1.860813467486853, 18.0, 0.03959335710796905, 0.9604066428920309, 0.0791867142159381)
    |]

let private varianceRatioCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (0.7983426179983926, 9.0, 9.0, 0.3713599658630226, 0.6286400341369773, 0.7427199317260452)
    |]

let private anovaCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (9.264705882352942, 2.0, 15.0, 0.9976012226706071, 0.0023987773293929083, 0.004797554658785817)
    |]

// python -I tests/reference/special.py counts
// successes, trials, p0, then the Less, Greater and TwoSided p-values, as exact fractions
let private binomialTestCases: struct (int * int * float * float * float * float)[] =
    [|
        struct (7, 20, 0.5, 0.13158798217773438, 0.9423408508300781, 0.26317596435546875)
        struct (3, 20, 0.3, 0.10708680450373102, 0.9645168677015313, 0.2204182673807094)
        struct (0, 10, 0.2, 0.10737418239999999, 1.0, 0.228248064)
        struct (10, 10, 0.2, 1.0, 1.0240000000000006e-07, 1.0240000000000006e-07)
        struct (10, 20, 0.5, 0.5880985260009766, 0.5880985260009766, 1.0)
        struct (13, 20, 0.5, 0.9423408508300781, 0.13158798217773438, 0.26317596435546875)
        struct (9, 10, 0.7, 0.9717524751000001, 0.14930834589999994, 0.2995766785)
    |]

// Mendel's F2 seed counts against 9 : 3 : 3 : 1: the statistic, then the chi-squared survival at it
let private mendelCase: struct (float * float)[] =
    [|
        struct (0.47002398081534774, 0.925425895103616)
    |]

// the statistic, then the chi-squared survival at it, for the 2 x 3 and the 3 x 3 table
let private independenceCases: struct (float * float)[] =
    [|
        struct (3.111111111111111, 0.2110720877910902)
        struct (20.0, 0.0004993992273873333)
    |]

// a, b, c, d, then the Less, Greater and TwoSided p-values, as exact fractions
let private fisherCases: struct (int * int * int * int * float * float * float)[] =
    [|
        struct (10, 2, 3, 15, 0.9999845190186861, 0.00046518094336290503, 0.0005367241191434358)
    |]

let private independenceTables: float[][][] =
    [|
        [| [| 20.0; 30.0; 50.0 |]; [| 30.0; 30.0; 40.0 |] |]
        [| [| 10.0; 20.0; 30.0 |]; [| 20.0; 20.0; 20.0 |]; [| 30.0; 20.0; 10.0 |] |]
    |]

let private mendelObserved = [| 315.0; 108.0; 101.0; 32.0 |]

let private moments (xs: float[]) : Moments = Array.fold (Moments.add true) Moments.zero xs

// R's datasets::sleep, extra by group, and the paired differences a - b.
let private sleepA = [| 0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0 |]
let private sleepB = [| 1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4 |]
let private sleepDifferences = [| -1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4 |]
let private zData = [| 1.0 .. 10.0 |]

let private anovaGroups: float[][] =
    [|
        [| 6.0; 8.0; 4.0; 5.0; 3.0; 4.0 |]
        [| 8.0; 12.0; 9.0; 11.0; 6.0; 8.0 |]
        [| 13.0; 9.0; 11.0; 8.0; 7.0; 12.0 |]
    |]

let private isNaN (x: float) : bool = Double.IsNaN x

// Within 1e-12 of the expected value, relative.
let private close (message: string) (actual: float) (expected: float) : unit =
    Expect.isLessThanOrEqual
        (abs (actual - expected))
        (1e-12 * abs expected)
        (message + ": " + string actual + " against " + string expected)

let private pValues
    (message: string)
    (run: Alternative -> TestResult)
    (less: float)
    (greater: float)
    (twoSided: float)
    : unit =
    close (message + " Less") (run Less).PValue less
    close (message + " Greater") (run Greater).PValue greater
    close (message + " TwoSided") (run TwoSided).PValue twoSided

let private checkT (run: Alternative -> TestResult) (statistic: float) (dof: float) : unit =
    let r = run TwoSided
    close "statistic" r.Statistic statistic
    close "degrees of freedom" r.DegreesOfFreedom dof
    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) "denominator degrees of freedom is NaN"

// Every test of the file on the data above, by name, for the checks that hold for all.
let private allTests: (string * (Alternative -> TestResult))[] =
    [|
        "oneSampleT sleep a", (fun alt -> HypothesisTest.oneSampleT alt 0.0 (moments sleepA))
        "oneSampleT differences", (fun alt -> HypothesisTest.oneSampleT alt 0.0 (moments sleepDifferences))
        "oneSampleZ", (fun alt -> HypothesisTest.oneSampleZ alt 5.0 3.0 (moments zData))
        "welchT", (fun alt -> HypothesisTest.welchT alt (moments sleepA) (moments sleepB))
        "pooledT", (fun alt -> HypothesisTest.pooledT alt (moments sleepA) (moments sleepB))
        "varianceRatio", (fun alt -> HypothesisTest.varianceRatio alt (moments sleepA) (moments sleepB))
    |]

// Minimal-standard LCG (16807 mod 2^31 - 1): exact in doubles, so both targets see the same samples.
let private seededSamples (seed: float) (pairs: int) : (float[] * float[])[] =
    let mutable state = seed

    let next () =
        state <- (state * 16807.0) % 2147483647.0
        state / 2147483647.0 * 10.0

    [|
        for i in 0 .. pairs - 1 do
            let sample (size: int) = Array.init size (fun _ -> next ())
            let a = sample (3 + i % 10)
            let b = sample (3 + (i * 7) % 10)
            a, b
    |]

let private allNaN (message: string) (r: TestResult) : unit =
    Expect.isTrue (isNaN r.Statistic) (message + " Statistic is NaN")
    Expect.isTrue (isNaN r.DegreesOfFreedom) (message + " DegreesOfFreedom is NaN")
    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) (message + " DenominatorDegreesOfFreedom is NaN")
    Expect.isTrue (isNaN r.PValue) (message + " PValue is NaN")

let private constant (n: int) (x: float) : Moments = moments (Array.create n x)

[<Tests>]
let tests =
    testList
        "HypothesisTest"
        [
            testCase "one-sample t against the reference" (fun () ->
                let data = [| sleepA; sleepDifferences |]

                for i in 0 .. oneSampleTCases.Length - 1 do
                    let struct (t, dof, less, greater, two) = oneSampleTCases[i]
                    let run alt = HypothesisTest.oneSampleT alt 0.0 (moments data[i])
                    checkT run t dof
                    pValues ("sample " + string i) run less greater two)

            testCase "one-sample z against the reference" (fun () ->
                for struct (z, less, greater, two) in oneSampleZCases do
                    let run alt = HypothesisTest.oneSampleZ alt 5.0 3.0 (moments zData)
                    let r = run TwoSided
                    close "statistic" r.Statistic z
                    Expect.isTrue (isNaN r.DegreesOfFreedom) "degrees of freedom is NaN"
                    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) "denominator degrees of freedom is NaN"
                    pValues "z" run less greater two)

            testCase "Welch and pooled t against the reference" (fun () ->
                for struct (t, dof, less, greater, two) in welchCases do
                    let run alt = HypothesisTest.welchT alt (moments sleepA) (moments sleepB)
                    checkT run t dof
                    pValues "welch" run less greater two

                for struct (t, dof, less, greater, two) in pooledCases do
                    let run alt = HypothesisTest.pooledT alt (moments sleepA) (moments sleepB)
                    checkT run t dof
                    pValues "pooled" run less greater two)

            testCase "variance ratio against the reference" (fun () ->
                for struct (f, d1, d2, less, greater, two) in varianceRatioCases do
                    let run alt = HypothesisTest.varianceRatio alt (moments sleepA) (moments sleepB)
                    let r = run TwoSided
                    close "statistic" r.Statistic f
                    close "numerator degrees of freedom" r.DegreesOfFreedom d1
                    close "denominator degrees of freedom" r.DenominatorDegreesOfFreedom d2
                    pValues "F" run less greater two)

            testCase "one-way ANOVA against the reference" (fun () ->
                for struct (f, d1, d2, _, greater, _) in anovaCases do
                    let r = HypothesisTest.oneWayAnova (Array.map moments anovaGroups)
                    close "statistic" r.Statistic f
                    close "numerator degrees of freedom" r.DegreesOfFreedom d1
                    close "denominator degrees of freedom" r.DenominatorDegreesOfFreedom d2
                    close "p-value" r.PValue greater)

            testCase "the alternatives are consistent" (fun () ->
                for (name, run) in allTests do
                    let less = (run Less).PValue
                    let greater = (run Greater).PValue
                    let two = (run TwoSided).PValue
                    Expect.isLessThanOrEqual (abs (less + greater - 1.0)) 1e-15 (name + ": Less + Greater is 1")
                    Expect.equal two (min 1.0 (2.0 * min less greater)) (name + ": TwoSided is min 1 (2 min)"))

            testCase "Less + Greater is 1 on seeded samples" (fun () ->
                // t, z, Welch and pooled measured 0 here; the F tails are separate incomplete-beta evaluations, whose
                // errors add: measured here on .NET (this test, 5000 pairs) the worst F deviation is 1.4e-15, and
                // 2.2e-15 by the reviewer over 20000 pairs. The bounds are 1e-15 and 4e-15.
                let mutable worstF = 0.0
                let mutable worstOther = 0.0

                for (a, b) in seededSamples 20260401.0 5000 do
                    let ma = moments a
                    let mb = moments b

                    let runs: (bool * (Alternative -> TestResult))[] =
                        [|
                            false, (fun alt -> HypothesisTest.oneSampleT alt 5.0 ma)
                            false, (fun alt -> HypothesisTest.oneSampleZ alt 5.0 3.0 ma)
                            false, (fun alt -> HypothesisTest.welchT alt ma mb)
                            false, (fun alt -> HypothesisTest.pooledT alt ma mb)
                            true, (fun alt -> HypothesisTest.varianceRatio alt ma mb)
                        |]

                    for (isF, run) in runs do
                        let err = abs ((run Less).PValue + (run Greater).PValue - 1.0)

                        if isF then
                            worstF <- max worstF err
                        else
                            worstOther <- max worstOther err

                Expect.isLessThanOrEqual worstOther 1e-15 ("t, z, Welch and pooled: worst " + string worstOther)
                Expect.isLessThanOrEqual worstF 4e-15 ("variance ratio: worst " + string worstF))

            testCase "fewer than two observations give NaN fields" (fun () ->
                let one = moments [| 3.0 |]
                let many = moments sleepA

                for alt in [| TwoSided; Less; Greater |] do
                    allNaN "oneSampleT" (HypothesisTest.oneSampleT alt 0.0 one)
                    allNaN "welchT a" (HypothesisTest.welchT alt one many)
                    allNaN "welchT b" (HypothesisTest.welchT alt many one)
                    allNaN "pooledT (1, 1)" (HypothesisTest.pooledT alt one one)
                    allNaN "pooledT (0, 3)" (HypothesisTest.pooledT alt Moments.zero many)
                    allNaN "pooledT (3, 0)" (HypothesisTest.pooledT alt many Moments.zero)
                    allNaN "varianceRatio a" (HypothesisTest.varianceRatio alt one many)
                    allNaN "varianceRatio b" (HypothesisTest.varianceRatio alt many one)
                    allNaN "oneSampleZ" (HypothesisTest.oneSampleZ alt 0.0 1.0 Moments.zero))

            testCase "pooledT with a single observation agrees with its interval" (fun () ->
                // Counts (1, 3) and (3, 1): the pooled variance and the n - 2 degrees of freedom exist. The interval
                // at alpha 0.05 excludes 0 exactly when the two-sided p-value is at most 0.05.
                let singles = [| 1.0; 9.0; 4.0 |]
                let triple = [| 3.0; 4.0; 5.0 |]

                for single in singles do
                    for (a, b) in [| [| single |], triple; triple, [| single |] |] do
                        let r = HypothesisTest.pooledT TwoSided (moments a) (moments b)
                        Expect.isTrue (not (isNaN r.Statistic || Double.IsInfinity r.Statistic)) "finite statistic"
                        Expect.equal r.DegreesOfFreedom 2.0 "degrees of freedom"
                        Expect.isTrue (r.PValue > 0.0 && r.PValue <= 1.0) "p-value in (0, 1]"
                        let ci = DifferenceInterval.pooled 0.05 (moments a) (moments b)
                        let excludesZero = ci.Lower > 0.0 || ci.Upper < 0.0
                        Expect.equal excludesZero (r.PValue <= 0.05) ("interval and p-value agree at single " + string single))

            testCase "zero spread" (fun () ->
                let t = HypothesisTest.oneSampleT TwoSided 0.0 (constant 3 2.0)
                Expect.equal t.Statistic Double.PositiveInfinity "constant 2 against 0: +infinity"
                Expect.equal t.PValue 0.0 "constant 2 against 0: p-value 0"
                Expect.equal t.DegreesOfFreedom 2.0 "constant 2 against 0: degrees of freedom"

                let zero = HypothesisTest.oneSampleT TwoSided 0.0 (constant 2 0.0)
                Expect.isTrue (isNaN zero.Statistic) "constant 0 against 0: NaN statistic"
                Expect.isTrue (isNaN zero.PValue) "constant 0 against 0: NaN p-value"

                let a = constant 3 2.0
                let b = constant 2 5.0

                let welch = HypothesisTest.welchT TwoSided a b
                Expect.equal welch.Statistic Double.NegativeInfinity "Welch: -infinity"
                Expect.equal welch.PValue 0.0 "Welch: p-value 0"
                Expect.isTrue (isNaN welch.DegreesOfFreedom) "Welch: degrees of freedom NaN"

                let pooled = HypothesisTest.pooledT TwoSided a b
                Expect.equal pooled.Statistic Double.NegativeInfinity "pooled: -infinity"
                Expect.equal pooled.PValue 0.0 "pooled: p-value 0"
                Expect.equal pooled.DegreesOfFreedom 3.0 "pooled: degrees of freedom"

                // One-sided at an infinite statistic: all the mass is on one side.
                Expect.equal (HypothesisTest.welchT Less a b).PValue 0.0 "Welch Less at -infinity"
                Expect.equal (HypothesisTest.welchT Greater a b).PValue 1.0 "Welch Greater at -infinity"
                Expect.equal (HypothesisTest.welchT Less b a).PValue 1.0 "Welch Less at +infinity"
                Expect.equal (HypothesisTest.pooledT Greater b a).PValue 0.0 "pooled Greater at +infinity"

                let equalA = constant 3 4.0
                let equalB = constant 2 4.0
                let welchEqual = HypothesisTest.welchT TwoSided equalA equalB
                let pooledEqual = HypothesisTest.pooledT TwoSided equalA equalB
                Expect.isTrue (isNaN welchEqual.Statistic) "Welch equal means: NaN statistic"
                Expect.isTrue (isNaN welchEqual.PValue) "Welch equal means: NaN p-value"
                Expect.isTrue (isNaN pooledEqual.Statistic) "pooled equal means: NaN statistic"
                Expect.isTrue (isNaN pooledEqual.PValue) "pooled equal means: NaN p-value")

            testCase "an empty group gives NaN fields" (fun () ->
                allNaN "empty group" (HypothesisTest.oneWayAnova [| moments anovaGroups[0]; Moments.zero |])
                allNaN "all singletons" (HypothesisTest.oneWayAnova [| moments [| 1.0 |]; moments [| 2.0 |] |])

                Expect.throwsT<ArgumentException>
                    (fun () -> HypothesisTest.oneWayAnova [| moments sleepA |] |> ignore)
                    "one group raises"

                Expect.throwsT<ArgumentException>
                    (fun () -> HypothesisTest.oneWayAnova [||] |> ignore)
                    "no group raises")

            testCase "arguments out of range raise" (fun () ->
                let m = moments sleepA

                for sigma in [| 0.0; -1.0; nan; infinity |] do
                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> HypothesisTest.oneSampleZ TwoSided 0.0 sigma m |> ignore)
                        ("sigma " + string sigma)

                for mu0 in [| infinity; -infinity; nan |] do
                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> HypothesisTest.oneSampleT TwoSided mu0 m |> ignore)
                        ("oneSampleT mu0 " + string mu0)

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> HypothesisTest.oneSampleZ TwoSided mu0 1.0 m |> ignore)
                        ("oneSampleZ mu0 " + string mu0))

            testCase "exact binomial against exact fractions" (fun () ->
                for struct (x, n, p0, less, greater, two) in binomialTestCases do
                    let run alt = HypothesisTest.binomial alt x n p0
                    let r = run TwoSided
                    Expect.equal r.Statistic (float x) "Statistic is the successes"
                    Expect.isTrue (isNaN r.DegreesOfFreedom) "DegreesOfFreedom is NaN"
                    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) "DenominatorDegreesOfFreedom is NaN"
                    pValues ("binomial " + string x + " of " + string n + " at " + string p0) run less greater two)

            testCase "the binomial two-sided rule counts tied outcomes" (fun () ->
                // The masses at 7 and 13 of Binomial(20, 1/2) tie in exact arithmetic.
                let expected = 2.0 * Discrete.cdf (Discrete.binomial 20 0.5) 7
                let actual = (HypothesisTest.binomial TwoSided 7 20 0.5).PValue
                Expect.isLessThanOrEqual (abs (actual - expected)) (1e-15 * expected) "7 of 20 at 1/2")

            testCase "goodness of fit at counts near the double maximum" (fun () ->
                // The totals overflow, the statistic does not: observed (9e307, 1.1e308) against equal expected
                // values has d = 1e307 on each cell, so the statistic is 2 d^2 / e = 2e306 with e = 1e308.
                let big = HypothesisTest.chiSquaredGoodnessOfFit [| 9e307; 1.1e308 |] [| 1e308; 1e308 |]
                Expect.isLessThanOrEqual (abs (big.Statistic - 2e306)) (1e-12 * 2e306) "statistic 2e306"
                Expect.equal big.DegreesOfFreedom 1.0 "degrees of freedom"
                Expect.equal big.PValue 0.0 "p-value of a statistic of 2e306 underflows to 0"

                // Observed (1, 2) against equal expected values near the maximum: e = 3/2, statistic 1/3.
                let small = HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; 2.0 |] [| 1e308; 1e308 |]
                close "statistic 1/3" small.Statistic (1.0 / 3.0)
                let plain = HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; 2.0 |] [| 1.0; 1.0 |]
                close "p-value as at expected 1" small.PValue plain.PValue

                // A statistic that cannot be represented, or a cell that underflows, gives NaN and never an infinity.
                allNaN "unrepresentable" (HypothesisTest.chiSquaredGoodnessOfFit [| 1e308; 1e308 |] [| 1.0; 1e-300 |])
                allNaN "zero total" (HypothesisTest.chiSquaredGoodnessOfFit [| 0.0; 0.0 |] [| 1.0; 1.0 |]))

            testCase "binomial at p0 of 0 and 1" (fun () ->
                Expect.equal (HypothesisTest.binomial TwoSided 0 10 0.0).PValue 1.0 "p0 0, 0 successes"
                Expect.equal (HypothesisTest.binomial TwoSided 1 10 0.0).PValue 0.0 "p0 0, 1 success"
                Expect.equal (HypothesisTest.binomial TwoSided 10 10 0.0).PValue 0.0 "p0 0, 10 successes"
                Expect.equal (HypothesisTest.binomial TwoSided 10 10 1.0).PValue 1.0 "p0 1, 10 successes"
                Expect.equal (HypothesisTest.binomial TwoSided 9 10 1.0).PValue 0.0 "p0 1, 9 successes"
                Expect.equal (HypothesisTest.binomial TwoSided 0 10 1.0).PValue 0.0 "p0 1, 0 successes")

            testCase "goodness of fit against Mendel's counts" (fun () ->
                let struct (statistic, p) = mendelCase[0]
                let r = HypothesisTest.chiSquaredGoodnessOfFit mendelObserved [| 9.0; 3.0; 3.0; 1.0 |]
                // 196/417, exact by fractions: sum of (o - e)^2 / e with e = 556 * (9, 3, 3, 1) / 16.
                Expect.isLessThanOrEqual (abs (r.Statistic - 196.0 / 417.0)) (1e-13 * (196.0 / 417.0)) "statistic is 196/417"
                close "statistic against the script" r.Statistic statistic
                Expect.equal r.DegreesOfFreedom 3.0 "degrees of freedom"
                Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) "DenominatorDegreesOfFreedom is NaN"
                close "p-value" r.PValue p

                let counts = HypothesisTest.chiSquaredGoodnessOfFit mendelObserved [| 312.75; 104.25; 104.25; 34.75 |]
                Expect.isLessThanOrEqual (abs (counts.Statistic - r.Statistic)) (1e-15 * r.Statistic) "statistic, expected counts"
                Expect.isLessThanOrEqual (abs (counts.PValue - r.PValue)) (1e-15 * r.PValue) "p-value, expected counts")

            testCase "independence against the reference" (fun () ->
                for i in 0 .. independenceCases.Length - 1 do
                    let struct (statistic, p) = independenceCases[i]
                    let table = independenceTables[i]
                    let r = HypothesisTest.chiSquaredIndependence table
                    close "statistic" r.Statistic statistic
                    close "p-value" r.PValue p
                    Expect.equal r.DegreesOfFreedom (float ((table.Length - 1) * (table[0].Length - 1))) "degrees of freedom"
                    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) "DenominatorDegreesOfFreedom is NaN"

                // The chi-squared survival with 2 degrees of freedom is exp(-x/2).
                let two = HypothesisTest.chiSquaredIndependence independenceTables[0]
                let closed = exp (-two.Statistic / 2.0)
                Expect.isLessThanOrEqual (abs (two.PValue - closed)) (1e-13 * closed) "2 x 3 against exp(-x/2)")

            testCase "Fisher's tea-tasting table" (fun () ->
                // Fisher, The Design of Experiments, 1935, chapter 2: masses 1, 16, 36, 16, 1 over 70.
                let run alt = HypothesisTest.fisherExact alt 3 1 1 3
                pValues "tea" run (69.0 / 70.0) (17.0 / 70.0) (34.0 / 70.0)
                close "odds ratio" (run TwoSided).Statistic 9.0
                Expect.isTrue (isNaN (run TwoSided).DegreesOfFreedom) "DegreesOfFreedom is NaN"
                Expect.isTrue (isNaN (run TwoSided).DenominatorDegreesOfFreedom) "DenominatorDegreesOfFreedom is NaN")

            testCase "Fisher against exact fractions" (fun () ->
                for struct (a, b, c, d, less, greater, two) in fisherCases do
                    let run alt = HypothesisTest.fisherExact alt a b c d
                    pValues "Fisher" run less greater two
                    close "odds ratio" (run TwoSided).Statistic (float (a * d) / float (b * c)))

            testCase "Fisher's odds ratio at a zero cell" (fun () ->
                Expect.equal (HypothesisTest.fisherExact TwoSided 3 0 1 2).Statistic infinity "bc = 0 < ad"
                Expect.equal (HypothesisTest.fisherExact TwoSided 0 3 2 0).Statistic 0.0 "ad = 0 < bc"
                Expect.isTrue (isNaN (HypothesisTest.fisherExact TwoSided 0 0 2 0).Statistic) "ad = bc = 0")

            testCase "count arguments out of range raise" (fun () ->
                let range (message: string) (f: unit -> TestResult) =
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> f () |> ignore) message

                let shape (message: string) (f: unit -> TestResult) =
                    Expect.throwsT<ArgumentException> (fun () -> f () |> ignore) message

                range "negative successes" (fun () -> HypothesisTest.binomial TwoSided -1 10 0.5)
                range "successes above trials" (fun () -> HypothesisTest.binomial TwoSided 11 10 0.5)
                range "negative trials" (fun () -> HypothesisTest.binomial TwoSided 0 -1 0.5)
                range "p0 below 0" (fun () -> HypothesisTest.binomial TwoSided 1 10 -0.1)
                range "p0 above 1" (fun () -> HypothesisTest.binomial TwoSided 1 10 1.1)
                range "p0 NaN" (fun () -> HypothesisTest.binomial TwoSided 1 10 nan)

                shape "lengths differ" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; 2.0; 3.0 |] [| 1.0; 2.0 |])
                shape "one cell" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0 |] [| 1.0 |])
                range "negative observed" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; -2.0 |] [| 1.0; 1.0 |])
                range "NaN observed" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; nan |] [| 1.0; 1.0 |])
                range "zero expected" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; 2.0 |] [| 1.0; 0.0 |])
                range "negative expected" (fun () -> HypothesisTest.chiSquaredGoodnessOfFit [| 1.0; 2.0 |] [| 1.0; -1.0 |])

                shape "one row" (fun () -> HypothesisTest.chiSquaredIndependence [| [| 1.0; 2.0 |] |])
                shape "one column" (fun () -> HypothesisTest.chiSquaredIndependence [| [| 1.0 |]; [| 2.0 |] |])
                shape "ragged" (fun () -> HypothesisTest.chiSquaredIndependence [| [| 1.0; 2.0 |]; [| 3.0 |] |])
                range "negative entry" (fun () -> HypothesisTest.chiSquaredIndependence [| [| 1.0; -2.0 |]; [| 3.0; 4.0 |] |])
                range "infinite entry" (fun () -> HypothesisTest.chiSquaredIndependence [| [| 1.0; infinity |]; [| 3.0; 4.0 |] |])

                range "negative a" (fun () -> HypothesisTest.fisherExact TwoSided -1 1 1 1)
                range "negative b" (fun () -> HypothesisTest.fisherExact TwoSided 1 -1 1 1)
                range "negative c" (fun () -> HypothesisTest.fisherExact TwoSided 1 1 -1 1)
                range "negative d" (fun () -> HypothesisTest.fisherExact TwoSided 1 1 1 -1))

            testCase "a zero row of a table gives NaN fields" (fun () ->
                allNaN "zero row" (HypothesisTest.chiSquaredIndependence [| [| 0.0; 0.0 |]; [| 3.0; 4.0 |] |])
                allNaN "zero column" (HypothesisTest.chiSquaredIndependence [| [| 0.0; 2.0 |]; [| 0.0; 4.0 |] |]))
        ]
