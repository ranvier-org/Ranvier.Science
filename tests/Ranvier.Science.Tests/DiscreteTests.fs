// Reference values: the tables below, printed by
//   python -I tests/reference/special.py discrete
// and the closed forms and identities written in each test.
module Ranvier.Science.Tests.DiscreteTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py discrete
// trials, p, k, mass, cdf, survival
let private binomialCases: struct (int * float * int * float * float * float)[] =
    [|
        struct (10, 0.3, -1, 0.0, 0.0, 1.0)
        struct (10, 0.3, 0, 0.028247524900000005, 0.028247524900000005, 0.9717524751)
        struct (10, 0.3, 1, 0.12106082100000001, 0.14930834590000003, 0.8506916541)
        struct (10, 0.3, 2, 0.23347444050000002, 0.3827827864, 0.6172172135999999)
        struct (10, 0.3, 3, 0.266827932, 0.6496107184000001, 0.3503892816)
        struct (10, 0.3, 4, 0.200120949, 0.8497316674000001, 0.15026833259999997)
        struct (10, 0.3, 5, 0.10291934519999998, 0.9526510126, 0.047348987399999994)
        struct (10, 0.3, 6, 0.036756909, 0.9894079216, 0.010592078399999998)
        struct (10, 0.3, 7, 0.009001691999999999, 0.9984096136, 0.0015903863999999995)
        struct (10, 0.3, 8, 0.0014467004999999997, 0.9998563141, 0.00014368589999999996)
        struct (10, 0.3, 9, 0.00013778099999999996, 0.9999940951, 5.9048999999999975e-06)
        struct (10, 0.3, 10, 5.9048999999999975e-06, 1.0, 0.0)
        struct (10, 0.3, 11, 0.0, 1.0, 0.0)
    |]

// trials, p, k, mass, cdf, survival
let private binomialTailCases: struct (int * float * int * float * float * float)[] =
    [|
        struct (1000, 0.5, 400, 4.633908074049734e-11, 1.3642320780330092e-10, 0.9999999998635768)
        struct (1000, 0.5, 450, 0.00016939724527711568, 0.0008652680424881588, 0.9991347319575118)
        struct (1000, 0.5, 550, 0.00016939724527711568, 0.999304129202789, 0.0006958707972110431)
        struct (1000, 0.5, 600, 4.633908074049734e-11, 0.9999999999099158, 9.008412706280358e-11)
        struct (100, 0.01, 0, 0.3660323412732295, 0.3660323412732295, 0.6339676587267705)
        struct (100, 0.01, 3, 0.06099916580753068, 0.9816259635553504, 0.018374036444649657)
        struct (100, 0.01, 5, 0.002897787123761482, 0.999465465536007, 0.0005345344639930333)
        struct (100, 0.01, 10, 7.006035693977211e-08, 0.9999999937444816, 6.25551838283409e-09)
    |]

// mean, k, mass, cdf, survival
let private poissonCases: struct (float * int * float * float * float)[] =
    [|
        struct (4.0, 0, 0.01831563888873418, 0.01831563888873418, 0.9816843611112658)
        struct (4.0, 1, 0.07326255555493671, 0.09157819444367091, 0.9084218055563291)
        struct (4.0, 2, 0.14652511110987343, 0.23810330555354434, 0.7618966944464557)
        struct (4.0, 3, 0.1953668148131646, 0.43347012036670896, 0.566529879633291)
        struct (4.0, 4, 0.1953668148131646, 0.6288369351798735, 0.37116306482012645)
        struct (4.0, 5, 0.15629345185053167, 0.7851303870304052, 0.2148696129695948)
        struct (4.0, 6, 0.10419563456702112, 0.8893260215974264, 0.11067397840257369)
        struct (4.0, 7, 0.05954036260972635, 0.9488663842071526, 0.05113361579284734)
        struct (4.0, 8, 0.029770181304863176, 0.9786365655120158, 0.021363434487984164)
        struct (4.0, 9, 0.0132311916910503, 0.9918677572030662, 0.008132242796933864)
        struct (4.0, 10, 0.00529247667642012, 0.9971602338794863, 0.002839766120513743)
        struct (4.0, 11, 0.00192453697324368, 0.99908477085273, 0.000915229147270063)
        struct (4.0, 12, 0.00064151232441456, 0.9997262831771445, 0.000273716822855503)
        struct (4.0, 13, 0.0001973884075121723, 0.9999236715846567, 7.632841534333068e-05)
        struct (4.0, 14, 5.639668786062066e-05, 0.9999800682725173, 1.9931727482710028e-05)
        struct (4.0, 15, 1.5039116762832175e-05, 0.9999951073892801, 4.892610719877852e-06)
        struct (4.0, 16, 3.759779190708044e-06, 0.9999988671684709, 1.1328315291698082e-06)
        struct (4.0, 17, 8.846539272254222e-07, 0.9999997518223981, 2.48177601944386e-07)
        struct (4.0, 18, 1.9658976160564936e-07, 0.9999999484121597, 5.158784033873664e-08)
        struct (4.0, 19, 4.138731823276829e-08, 0.9999999897994779, 1.0200522105968353e-08)
        struct (4.0, 20, 8.277463646553658e-09, 0.9999999980769415, 1.9230584594146952e-09)
        struct (4.0, 40, 2.713792621604946e-26, 1.0, 2.9255511651945938e-27)
        struct (1000.0, 900, 7.516954352125952e-05, 0.0006977673277963068, 0.9993022326722036)
        struct (1000.0, 1100, 9.498944242299507e-05, 0.9991323590365564, 0.000867640963443562)
    |]

// r, p, k, mass, cdf, survival
let private negativeBinomialCases: struct (float * float * int * float * float * float)[] =
    [|
        struct (3.0, 0.4, 0, 0.06400000000000002, 0.06400000000000002, 0.9359999999999999)
        struct (3.0, 0.4, 1, 0.11520000000000001, 0.17920000000000003, 0.8208)
        struct (3.0, 0.4, 2, 0.13824, 0.31744000000000006, 0.68256)
        struct (3.0, 0.4, 3, 0.13824, 0.45568000000000003, 0.5443199999999999)
        struct (3.0, 0.4, 4, 0.124416, 0.5800960000000001, 0.41990399999999994)
        struct (3.0, 0.4, 5, 0.10450944, 0.68460544, 0.31539455999999993)
        struct (3.0, 0.4, 6, 0.083607552, 0.768212992, 0.23178700799999996)
        struct (3.0, 0.4, 7, 0.0644972544, 0.8327102464, 0.16728975359999995)
        struct (3.0, 0.4, 8, 0.04837294079999999, 0.8810831872, 0.11891681279999997)
        struct (3.0, 0.4, 9, 0.03547348992, 0.91655667712, 0.08344332287999998)
        struct (3.0, 0.4, 10, 0.025540912742399995, 0.9420975898624, 0.05790241013759998)
        struct (3.0, 0.4, 11, 0.018110829035519995, 0.96020841889792, 0.03979158110207998)
        struct (3.0, 0.4, 12, 0.012677580324863996, 0.972885999222784, 0.02711400077721599)
        struct (3.0, 0.4, 13, 0.008776786378751997, 0.981662785601536, 0.018337214398463992)
        struct (3.0, 0.4, 14, 0.006018367802572798, 0.9876811534041088, 0.012318846595891194)
        struct (3.0, 0.4, 15, 0.004092490105749503, 0.9917736435098583, 0.008226356490141692)
        struct (2.5, 0.4, 0, 0.10119288512538815, 0.10119288512538815, 0.8988071148746118)
        struct (2.5, 0.4, 3, 0.1434409146652377, 0.5558019215511943, 0.4441980784488056)
        struct (2.5, 0.4, 10, 0.017357492890014706, 0.9645608619098024, 0.03543913809019764)
    |]

// population, successes, draws, k, mass, cdf, survival
let private hypergeometricCases: struct (int * int * int * int * float * float * float)[] =
    [|
        struct (50, 10, 5, 0, 0.3105627820045687, 0.3105627820045687, 0.6894372179954313)
        struct (50, 10, 5, 1, 0.43133719722856767, 0.7418999792331363, 0.2581000207668636)
        struct (50, 10, 5, 2, 0.20983971757065453, 0.9517396968037909, 0.0482603031962091)
        struct (50, 10, 5, 3, 0.04417678264645358, 0.9959164794502445, 0.004083520549755517)
        struct (50, 10, 5, 4, 0.003964583058015066, 0.9998810625082596, 0.00011893749174045196)
        struct (50, 10, 5, 5, 0.00011893749174045196, 1.0, 0.0)
        struct (1000, 400, 100, 20, 4.385050028283944e-06, 6.5137576345423785e-06, 0.9999934862423655)
        struct (1000, 400, 100, 40, 0.08560998572862107, 0.5450660029793728, 0.4549339970206272)
        struct (1000, 400, 100, 60, 1.024328427852979e-05, 0.999993540073939, 6.459926060987788e-06)
    |]

// The ends of the domains Discrete.fs states, and its worst points there.
// trials, p, k, mass, cdf, survival
let private binomialEdgeCases: struct (int * float * int * float * float * float)[] =
    [|
        struct (1, 0.3, 0, 0.7, 0.7, 0.3)
        struct (1, 0.3, 1, 0.3, 1.0, 0.0)
        struct (1000, 1e-10, 0, 0.999999900000005, 0.999999900000005, 9.999999500500017e-08)
        struct (1000, 1e-10, 1, 9.999999001000051e-08, 0.999999999999995, 4.9949996676660124e-15)
        struct (1000, 1e-10, 2, 4.994999501499025e-15, 1.0, 1.6616698757486309e-22)
        struct (1000, 0.3, 300, 0.027521003821268385, 0.5155935198141203, 0.48440648018587973)
        struct (1000, 0.3, 794, 2.014778448194188e-228, 1.0, 2.5147911311274567e-229)
        struct (1000, 0.999, 990, 9.782838349942127e-08, 1.0742833868464958e-07, 0.9999998925716613)
        struct (1000, 0.999, 999, 0.36806348825922325, 0.6323045752290363, 0.36769542477096373)
        struct (1000, 0.999, 1000, 0.36769542477096373, 1.0, 0.0)
    |]

// mean, k, mass, cdf, survival
let private poissonEdgeCases: struct (float * int * float * float * float)[] =
    [|
        struct (0.001, 0, 0.999000499833375, 0.999000499833375, 0.0009995001666250085)
        struct (0.001, 1, 0.000999000499833375, 0.9999995003332084, 4.996667916333403e-07)
        struct (0.001, 10, 2.7529775678829786e-37, 1.0, 2.502915454844413e-41)
        struct (1000.0, 1000, 0.012614611348721499, 0.508409367168506, 0.491590632831494)
        struct (1000.0, 2374, 1.7480590303290592e-297, 1.0, 1.2706447773035888e-297)
    |]

// p, k, mass, cdf, survival
let private geometricEdgeCases: struct (float * int * float * float * float)[] =
    [|
        struct (1e-10, 0, 1e-10, 1e-10, 0.9999999999)
        struct (1e-10, 2000000000, 8.187307530697945e-11, 0.18126924701207853, 0.8187307529879214)
        struct (0.999, 0, 0.999, 0.999, 0.0010000000000000009)
        struct (0.999, 99, 9.990000000000878e-298, 1.0, 1.0000000000000889e-300)
    |]

// r, p, k, mass, cdf, survival
let private negativeBinomialEdgeCases: struct (float * float * int * float * float * float)[] =
    [|
        struct (0.5, 0.002, 0, 0.044721359549995794, 0.044721359549995794, 0.9552786404500042)
        struct (0.5, 0.002, 100, 0.0020627724593253924, 0.4746639811605532, 0.5253360188394468)
        struct (0.5, 0.002, 270000, 8.558655950962791e-240, 1.0, 4.266825830213119e-237)
        struct (100.0, 0.0909091, 1000, 0.003800568130464639, 0.5152142883609708, 0.4847857116390291)
        struct (100.0, 0.0909091, 5000, 3.568146387060175e-101, 1.0, 4.5338605617518097e-100)
        struct (0.5, 0.999, 0, 0.999499874937461, 0.999499874937461, 0.0005001250625390904)
        struct (0.5, 0.999, 1, 0.0004997499374687309, 0.9999996248749297, 3.7512507035940985e-07)
        struct (0.5, 0.999, 5, 2.4597067234789185e-16, 1.0, 2.2568268068058026e-19)
        struct (100.0, 0.999, 0, 0.9047921471137089, 0.9047921471137089, 0.09520785288629104)
        struct (100.0, 0.999, 1, 0.09047921471137098, 0.9952713618250799, 0.004728638174920062)
        struct (100.0, 0.999, 5, 8.320696592478777e-08, 0.9999999985215298, 1.4784702443084372e-09)
    |]

// population, successes, draws, k, mass, cdf, survival
let private hypergeometricEdgeCases: struct (int * int * int * int * float * float * float)[] =
    [|
        struct (1000, 400, 500, 149, 1.7293544024761138e-11, 2.9474899044635164e-11, 0.9999999999705251)
        struct (1000, 400, 500, 200, 0.05146246972492867, 0.5257312348624643, 0.4742687651375357)
        struct (1000, 400, 500, 284, 4.08581361262343e-28, 1.0, 1.2003827252202334e-28)
        struct (1000, 10, 999, 9, 0.01, 0.01, 0.99)
        struct (1000, 10, 999, 10, 0.99, 1.0, 0.0)
    |]

// Where e^-mean mean^k / k! from its logarithm loses more than 1e-12, and where the rounding of 1 - p does in
// I_(1 - p)(k + 1, r): p = 0.00272 and 0.00938 round 1 - p by 2^-54, and 1 - 1e-17 rounds to 1.
// mean, k, mass, cdf, survival
let private poissonRegimeCases: struct (float * int * float * float * float)[] =
    [|
        struct (300.0, 858, 8.098204102883457e-152, 1.0, 4.341931337371497e-152)
        struct (300.0, 1040, 9.156030760565385e-243, 1.0, 3.7048757433106905e-243)
        struct (1000.0, 2374, 1.7480590303290592e-297, 1.0, 1.2706447773035888e-297)
    |]

// r, p, k, mass, cdf, survival
let private negativeBinomialRegimeCases: struct (float * float * int * float * float * float)[] =
    [|
        struct (0.5, 0.00272, 200000, 1.7382163516694174e-241, 1.0, 6.367277546488354e-239)
        struct (0.5, 0.00938, 60000, 5.942403452603542e-250, 1.0, 6.270200150881548e-248)
        struct (0.5, 1e-17, 5, 7.782167679320621e-10, 8.560384447252683e-09, 0.9999999914396156)
    |]

let private rel (a: float) (b: float) : float = abs (a - b) / abs b

/// Within relative 1e-12 of a reference of at least 1e-300, exactly 0 at a reference of 0, and unchecked between.
let private matches (name: string) (actual: float) (reference: float) =
    if reference = 0.0 then
        Expect.equal actual 0.0 (name + ": " + string actual + " against 0")
    elif reference >= 1e-300 then
        Expect.isLessThanOrEqual
            (rel actual reference)
            1e-12
            (name + ": " + string actual + " against " + string reference)

/// Checks mass, cdf and survival at k against a reference row.
let private matchesRow (d: Discrete) (k: int) (mass: float) (cdf: float) (survival: float) =
    let name = Discrete.describe d + " at " + string k
    matches (name + " mass") (Discrete.mass d k) mass
    matches (name + " cdf") (Discrete.cdf d k) cdf
    matches (name + " survival") (Discrete.survival d k) survival

/// matchesRow, and logMass within 1e-12 of the log of a reference mass of at least 1e-300.
let private matchesRowAndLog (d: Discrete) (k: int) (mass: float) (cdf: float) (survival: float) =
    matchesRow d k mass cdf survival

    if mass >= 1e-300 then
        Expect.isLessThanOrEqual
            (abs (Discrete.logMass d k - log mass))
            1e-12
            (Discrete.describe d + " logMass at " + string k)

/// Mass 0, cdf 0 and survival 1 at k below the support.
let private belowSupport (d: Discrete) (k: int) =
    let name = Discrete.describe d + " at " + string k
    Expect.equal (Discrete.mass d k) 0.0 (name + " mass")
    Expect.equal (Discrete.cdf d k) 0.0 (name + " cdf")
    Expect.equal (Discrete.survival d k) 1.0 (name + " survival")

/// Mass 0, cdf 1 and survival 0 at k above the support.
let private aboveSupport (d: Discrete) (k: int) =
    let name = Discrete.describe d + " at " + string k
    Expect.equal (Discrete.mass d k) 0.0 (name + " mass")
    Expect.equal (Discrete.cdf d k) 1.0 (name + " cdf")
    Expect.equal (Discrete.survival d k) 0.0 (name + " survival")

let private families: Discrete list =
    [
        Discrete.binomial 10 0.3
        Discrete.binomial 1000 0.5
        Discrete.poisson 4.0
        Discrete.poisson 1000.0
        Discrete.geometric 0.2
        Discrete.negativeBinomial 3.0 0.4
        Discrete.negativeBinomial 2.5 0.4
        Discrete.hypergeometric 50 10 5
        Discrete.hypergeometric 1000 400 100
        Discrete.bernoulli 0.3
        Discrete.uniform 1 6
    ]

/// One distribution of each family, for the sampling test.
let private samplingFamilies: Discrete list =
    [
        Discrete.binomial 10 0.3
        Discrete.poisson 4.0
        Discrete.geometric 0.2
        Discrete.negativeBinomial 2.5 0.4
        Discrete.hypergeometric 50 10 5
        Discrete.bernoulli 0.3
        Discrete.uniform 1 6
    ]

[<Tests>]
let tests =
    testList
        "Discrete"
        [
            testCase "binomial against exact fractions" (fun () ->
                for row in binomialCases do
                    let struct (n, p, k, m, c, s) = row
                    matchesRow (Discrete.binomial n p) k m c s

                let d = Discrete.binomial 10 0.3
                belowSupport d -1
                aboveSupport d 11)

            testCase "binomial tails" (fun () ->
                for row in binomialTailCases do
                    let struct (n, p, k, m, c, s) = row
                    matchesRow (Discrete.binomial n p) k m c s

                belowSupport (Discrete.binomial 1000 0.5) -1
                aboveSupport (Discrete.binomial 1000 0.5) 1001)

            testCase "Poisson against the reference" (fun () ->
                for row in poissonCases do
                    let struct (mean, k, m, c, s) = row
                    matchesRow (Discrete.poisson mean) k m c s

                belowSupport (Discrete.poisson 4.0) -1)

            testCase "negative binomial against the reference" (fun () ->
                for row in negativeBinomialCases do
                    let struct (r, p, k, m, c, s) = row
                    matchesRow (Discrete.negativeBinomial r p) k m c s

                belowSupport (Discrete.negativeBinomial 2.5 0.4) -1)

            testCase "hypergeometric against exact fractions" (fun () ->
                for row in hypergeometricCases do
                    let struct (n, successes, draws, k, m, c, s) = row
                    matchesRow (Discrete.hypergeometric n successes draws) k m c s

                belowSupport (Discrete.hypergeometric 50 10 5) -1
                aboveSupport (Discrete.hypergeometric 50 10 5) 6
                // The support of 5 draws from 10 with 8 successes starts at 3.
                belowSupport (Discrete.hypergeometric 10 8 5) 2)

            testCase "geometric closed forms" (fun () ->
                let d = Discrete.geometric 0.2

                for k in [ 0; 1; 5; 50 ] do
                    let name = "geometric 0.2 at " + string k
                    Expect.isLessThanOrEqual (rel (Discrete.mass d k) (0.8 ** float k * 0.2)) 1e-14 (name + " mass")

                    Expect.isLessThanOrEqual
                        (rel (Discrete.survival d k) (0.8 ** float (k + 1)))
                        1e-14
                        (name + " survival")

                belowSupport d -1

                Expect.isLessThanOrEqual
                    (rel (Discrete.cdf (Discrete.geometric 1e-10) 0) 1e-10)
                    1e-14
                    "geometric 1e-10 cdf at 0")

            testCase "Bernoulli and discrete uniform closed forms" (fun () ->
                let b = Discrete.bernoulli 0.3
                Expect.equal (Discrete.mass b 0) 0.7 "Bernoulli mass at 0"
                Expect.equal (Discrete.mass b 1) 0.3 "Bernoulli mass at 1"
                Expect.equal (Discrete.cdf b 0) 0.7 "Bernoulli cdf at 0"
                Expect.equal (Discrete.survival b 0) 0.3 "Bernoulli survival at 0"
                belowSupport b -1
                aboveSupport b 2

                let u = Discrete.uniform 1 6

                for k in 1..6 do
                    Expect.equal (Discrete.mass u k) (1.0 / 6.0) ("uniform mass at " + string k)

                Expect.equal (Discrete.cdf u 3) 0.5 "uniform cdf at 3"
                Expect.equal (Discrete.mean u) 3.5 "uniform mean"
                Expect.equal (Discrete.variance u) (35.0 / 12.0) "uniform variance"
                belowSupport u 0
                aboveSupport u 7)

            testCase "the mass sums to 1" (fun () ->
                let total (d: Discrete) (ks: int list) = ks |> List.sumBy (Discrete.mass d)

                Expect.isLessThanOrEqual
                    (abs (total (Discrete.binomial 50 0.37) [ 0..50 ] - 1.0))
                    1e-14
                    "binomial 50 0.37"

                Expect.isLessThanOrEqual
                    (abs (total (Discrete.hypergeometric 50 10 5) [ 0..5 ] - 1.0))
                    1e-14
                    "hypergeometric 50 10 5"

                Expect.isLessThanOrEqual
                    (abs (total (Discrete.poisson 4.0) [ 0..60 ] - 1.0))
                    1e-14
                    "poisson 4 over 0..60")

            testCase "logMass is the log of mass" (fun () ->
                for row in Array.append binomialCases binomialTailCases do
                    let struct (n, p, k, _, _, _) = row
                    let d = Discrete.binomial n p
                    let m = Discrete.mass d k

                    if m > 0.0 then
                        Expect.isLessThanOrEqual
                            (rel (Discrete.logMass d k) (log m))
                            1e-13
                            (Discrete.describe d + " logMass at " + string k)

                for (d, k) in
                    [
                        Discrete.binomial 10 0.3, -1
                        Discrete.binomial 10 0.3, 11
                        Discrete.poisson 4.0, -1
                        Discrete.geometric 0.2, -1
                        Discrete.negativeBinomial 2.5 0.4, -1
                        Discrete.hypergeometric 50 10 5, 6
                        Discrete.bernoulli 0.3, 2
                        Discrete.uniform 1 6, 0
                    ] do
                    Expect.equal (Discrete.logMass d k) -infinity (Discrete.describe d + " logMass at " + string k))

            testCase "quantile is the least k with cdf at least p" (fun () ->
                for d in families do
                    for p in [ 1e-10; 0.01; 0.3; 0.5; 0.6; 0.975; 1.0 - 1e-10 ] do
                        let q = Discrete.quantile d p
                        let name = Discrete.describe d + " quantile of " + string p + " = " + string q

                        if p <= 0.5 then
                            Expect.isTrue (Discrete.cdf d q >= p) (name + ": cdf at q")
                            Expect.isTrue (Discrete.cdf d (q - 1) < p) (name + ": cdf at q - 1")
                        else
                            Expect.isTrue (Discrete.survival d q <= 1.0 - p) (name + ": survival at q")
                            Expect.isTrue (Discrete.survival d (q - 1) > 1.0 - p) (name + ": survival at q - 1")

                let d = Discrete.binomial 10 0.3
                let cdfs = binomialCases |> Array.map (fun struct (_, _, k, _, c, _) -> struct (k, c))

                for p in [ 0.1; 0.5; 0.9 ] do
                    let expected =
                        cdfs |> Array.filter (fun struct (_, c) -> c >= p) |> Array.map (fun struct (k, _) -> k)

                    Expect.equal (Discrete.quantile d p) expected[0] ("binomial 10 0.3 quantile of " + string p))

            testCase "quantile at 0 and 1 is the support's bounds" (fun () ->
                for d in families do
                    let name = Discrete.describe d
                    let struct (lower, upper) = Discrete.support d
                    Expect.equal (Discrete.quantile d 0.0) lower (name + " at 0")
                    Expect.equal (Discrete.quantile d 1.0) upper (name + " at 1")

                    for p in [ -0.1; 1.1; nan ] do
                        Expect.throwsT<ArgumentOutOfRangeException>
                            (fun () -> Discrete.quantile d p |> ignore)
                            (name + " at " + string p)

                for d in [ Discrete.poisson 4.0; Discrete.geometric 0.2; Discrete.negativeBinomial 2.5 0.4 ] do
                    Expect.equal (Discrete.support d) (struct (0, Int32.MaxValue)) (Discrete.describe d + " support")

                Expect.equal (Discrete.support (Discrete.binomial 10 0.3)) (struct (0, 10)) "binomial support"
                Expect.equal
                    (Discrete.support (Discrete.hypergeometric 10 8 5))
                    (struct (3, 5))
                    "hypergeometric support"
                Expect.equal (Discrete.support (Discrete.bernoulli 0.3)) (struct (0, 1)) "Bernoulli support"
                Expect.equal (Discrete.support (Discrete.uniform 1 6)) (struct (1, 6)) "uniform support")

            testCase "mean and variance" (fun () ->
                let check (d: Discrete) (mean: float) (variance: float) =
                    let name = Discrete.describe d
                    Expect.isLessThanOrEqual (rel (Discrete.mean d) mean) 1e-15 (name + " mean")
                    Expect.isLessThanOrEqual (rel (Discrete.variance d) variance) 1e-15 (name + " variance")

                check (Discrete.binomial 10 0.3) (10.0 * 0.3) (10.0 * 0.3 * 0.7)
                check (Discrete.poisson 4.0) 4.0 4.0
                check (Discrete.geometric 0.2) (0.8 / 0.2) (0.8 / (0.2 * 0.2))
                check (Discrete.negativeBinomial 2.5 0.4) (2.5 * 0.6 / 0.4) (2.5 * 0.6 / (0.4 * 0.4))

                check
                    (Discrete.hypergeometric 50 10 5)
                    (5.0 * 10.0 / 50.0)
                    (5.0 * 10.0 * 40.0 * 45.0 / (50.0 * 50.0 * 49.0))

                check (Discrete.bernoulli 0.3) 0.3 (0.3 * 0.7))

            testCase "describe" (fun () ->
                Expect.equal (Discrete.describe (Discrete.binomial 10 0.3)) "Binomial(trials = 10, p = 0.3)" "binomial"
                Expect.equal (Discrete.describe (Discrete.poisson 4.0)) "Poisson(mean = 4)" "poisson"
                Expect.equal (Discrete.describe (Discrete.geometric 0.2)) "Geometric(p = 0.2)" "geometric"

                Expect.equal
                    (Discrete.describe (Discrete.negativeBinomial 3.0 0.4))
                    "NegativeBinomial(r = 3, p = 0.4)"
                    "negativeBinomial"

                Expect.equal
                    (Discrete.describe (Discrete.hypergeometric 50 10 5))
                    "Hypergeometric(population = 50, successes = 10, draws = 5)"
                    "hypergeometric"

                Expect.equal (Discrete.describe (Discrete.bernoulli 0.3)) "Bernoulli(p = 0.3)" "bernoulli"

                Expect.equal
                    (Discrete.describe (Discrete.uniform 1 6))
                    "DiscreteUniform(lower = 1, upper = 6)"
                    "uniform")

            testCase "constructors reject parameters outside their domain" (fun () ->
                let rejects (name: string) (build: unit -> Discrete) =
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> build () |> ignore) name

                rejects "binomial -1 0.5" (fun () -> Discrete.binomial -1 0.5)
                rejects "binomial 10 1.5" (fun () -> Discrete.binomial 10 1.5)
                rejects "binomial 10 nan" (fun () -> Discrete.binomial 10 nan)
                rejects "poisson 0" (fun () -> Discrete.poisson 0.0)
                rejects "poisson infinity" (fun () -> Discrete.poisson infinity)
                rejects "geometric 0" (fun () -> Discrete.geometric 0.0)
                rejects "negativeBinomial 0 0.5" (fun () -> Discrete.negativeBinomial 0.0 0.5)
                rejects "negativeBinomial 2 1.5" (fun () -> Discrete.negativeBinomial 2.0 1.5)
                rejects "hypergeometric 10 11 3" (fun () -> Discrete.hypergeometric 10 11 3)
                rejects "hypergeometric 10 3 11" (fun () -> Discrete.hypergeometric 10 3 11)
                rejects "hypergeometric -1 0 0" (fun () -> Discrete.hypergeometric -1 0 0)
                rejects "bernoulli -0.1" (fun () -> Discrete.bernoulli -0.1)
                rejects "uniform 3 2" (fun () -> Discrete.uniform 3 2)
                rejects "uniform 0 Int32.MaxValue" (fun () -> Discrete.uniform 0 Int32.MaxValue))

            testCase "samples follow the distribution" (fun () ->
                let count = 20000

                for d in samplingFamilies do
                    let name = Discrete.describe d
                    let struct (xs, final) = Discrete.samples d count (Generator.create 2027)
                    let struct (lower, upper) = Discrete.support d

                    let likely =
                        [ lower .. min upper (lower + 200) ]
                        |> List.sortByDescending (Discrete.mass d)
                        |> List.truncate 5

                    for k in likely do
                        let m = Discrete.mass d k
                        let frequency = float (xs |> Array.filter (fun x -> x = k) |> Array.length) / float count

                        Expect.isLessThanOrEqual
                            (abs (frequency - m))
                            (5.0 * sqrt (m * (1.0 - m) / float count))
                            (name + " frequency of " + string k + ": " + string frequency + " against " + string m)

                    let mean = (xs |> Array.sumBy float) / float count

                    Expect.isLessThanOrEqual
                        (abs (mean - Discrete.mean d))
                        (5.0 * sqrt (Discrete.variance d / float count))
                        (name + " sample mean " + string mean)

                    let mutable g = Generator.create 2027

                    for i in 0 .. count - 1 do
                        let struct (x, next) = Discrete.sample d g
                        g <- next

                        if x <> xs[i] then
                            Expect.equal x xs[i] (name + " sample " + string i)

                    Expect.equal g final (name + " final generator")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Discrete.samples d -1 (Generator.create 1) |> ignore)
                        (name + " with a negative count")

                for (d, value) in [ Discrete.binomial 0 0.5, 0; Discrete.binomial 5 1.0, 5; Discrete.uniform 4 4, 4 ] do
                    let g = Generator.create 2027
                    let struct (x, next) = Discrete.sample d g
                    Expect.equal x value (Discrete.describe d + " value")
                    Expect.equal next g (Discrete.describe d + " generator unchanged"))

            testCase "the stated domains at their edges against the reference" (fun () ->
                for row in binomialEdgeCases do
                    let struct (n, p, k, m, c, s) = row
                    matchesRowAndLog (Discrete.binomial n p) k m c s

                for row in poissonEdgeCases do
                    let struct (mean, k, m, c, s) = row
                    matchesRowAndLog (Discrete.poisson mean) k m c s

                for row in geometricEdgeCases do
                    let struct (p, k, m, c, s) = row
                    matchesRowAndLog (Discrete.geometric p) k m c s

                for row in negativeBinomialEdgeCases do
                    let struct (r, p, k, m, c, s) = row
                    matchesRowAndLog (Discrete.negativeBinomial r p) k m c s

                for row in hypergeometricEdgeCases do
                    let struct (n, successes, draws, k, m, c, s) = row
                    matchesRowAndLog (Discrete.hypergeometric n successes draws) k m c s)

            testCase "where the direct mass and the rounded 1 - p lose accuracy, against the reference" (fun () ->
                for row in poissonRegimeCases do
                    let struct (mean, k, m, c, s) = row
                    matchesRowAndLog (Discrete.poisson mean) k m c s

                for row in negativeBinomialRegimeCases do
                    let struct (r, p, k, m, c, s) = row
                    matchesRowAndLog (Discrete.negativeBinomial r p) k m c s)

            testCase "quantile far from the normal start" (fun () ->
                // The least k with 1 - (1 - p)^(k + 1) >= 1/2 is ceil(ln(1/2) / ln(1 - p)) - 1, with
                // ln(1 - p) = -(p + p^2 / 2) to 3e-19 of itself at p = 1e-9; the normal start, the mean 1e9, is
                // 3e8 away.
                let x = log 0.5 / -(1e-9 + 1e-18 / 2.0)
                let geometric = Discrete.geometric 1e-9
                let q = Discrete.quantile geometric 0.5
                Expect.equal q (int (Math.Ceiling x) - 1) "geometric 1e-9 median"
                Expect.isTrue (Discrete.cdf geometric q >= 0.5) "geometric 1e-9: cdf at the median"
                Expect.isTrue (Discrete.cdf geometric (q - 1) < 0.5) "geometric 1e-9: cdf below the median"

                let d = Discrete.negativeBinomial 0.5 0.002
                let p = 1.0 - 1e-10
                let q = Discrete.quantile d p
                Expect.isTrue (Discrete.survival d q <= 1.0 - p) "negative binomial: survival at q"
                Expect.isTrue (Discrete.survival d (q - 1) > 1.0 - p) "negative binomial: survival at q - 1"

                // ln(1e-10) / ln(1 - 1e-10) is about 2.3e11, beyond Int32.MaxValue.
                Expect.equal
                    (Discrete.quantile (Discrete.geometric 1e-10) p)
                    Int32.MaxValue
                    "a quantile beyond Int32.MaxValue saturates")

            testCase "Poisson draws above 2^30" (fun () ->
                let count = 200
                let struct (xs, _) = Discrete.samples (Discrete.poisson 1.5e9) count (Generator.create 2027)
                let mean = (xs |> Array.sumBy float) / float count

                Expect.isLessThanOrEqual
                    (abs (mean - 1.5e9))
                    (5.0 * sqrt (1.5e9 / float count))
                    ("Poisson 1.5e9 sample mean " + string mean)

                // The mean 3e9 lies about 15 000 standard deviations, sqrt(3e9), above Int32.MaxValue.
                let struct (ys, _) = Discrete.samples (Discrete.poisson 3e9) 20 (Generator.create 2027)
                Expect.all ys (fun y -> y = Int32.MaxValue) "Poisson 3e9 draws saturate at Int32.MaxValue")
        ]
