// Reference values: the case tables below, printed by
//   python -I tests/reference/special.py gamma
//   python -I tests/reference/special.py erf
//   python -I tests/reference/special.py incgamma
//   python -I tests/reference/special.py incbeta
// and the identities and series written in each test.
module Ranvier.Science.Tests.SpecialTests

open System
open Expecto
open Ranvier.Numerics

// python -I tests/reference/special.py gamma
let private gammaLnCases: (float * float)[] =
    [|
        1e-300, 690.7755278982137
        1e-08, 18.42068073818021
        0.001, 6.907178885383853
        0.1, 2.252712651734206
        0.5, 0.5723649429247001
        0.9, 0.06637623973474295
        1.0, 0.0
        1.1, -0.049872441259839764
        1.5, -0.12078223763524522
        1.9, -0.03898427592308336
        2.0, 0.0
        2.5, 0.2846828704729192
        3.7, 1.428072326665388
        7.9, 8.32426586800881
        8.0, 8.525161361065415
        10.1, 13.027526738633236
        33.3, 82.60372358165495
        100.5, 361.4355404677776
        171.5, 709.1431630309282
        1000.25, 5906.9472682711175
        100000.5, 1051293.4654351394
        10000000000.0, 220258509288.81058
        1e+300, 6.897755278982137e+302
        -0.5, 1.2655121234846454
        -1.5, 0.860047015376481
        -2.75, 0.004487897535955773
        -10.3, -14.457515440024208
        -100.7, -365.61225576180533
        -170.4, -707.434114102398
        -1000.5, -5914.437701116852
        -10.999999976954987, 0.08350865668793016
        -11.999999997871052, -0.019576466288501845
        -12.99999999983939, -0.00011746708712988221
        -13.999999999988548, 0.0016204794186455906
        -14.000000000010768, 0.0631956070520921
        -15.00000000000072, 0.0610533964879175
        -1e-320, 736.8272408909739
        -5e-324, 744.4400719213812
    |]

let private gammaCases: (float * float)[] =
    [|
        1e-300, 9.999999999999999e+299
        1e-08, 99999999.42278434
        0.001, 999.4237724845955
        0.1, 9.51350769866873
        0.5, 1.772453850905516
        1.5, 0.886226925452758
        2.5, 1.329340388179137
        3.7, 4.170651783796604
        7.9, 4122.709484285445
        10.1, 454760.7514415856
        33.3, 7.487577596522633e+35
        100.5, 9.320963104082716e+156
        170.5, 5.56209241456e+305
        171.6, 1.5858969096672565e+308
        -0.5, -3.544907701811032
        -1.5, 2.363271801207355
        -2.75, -1.0044979832303123
        -10.3, -5.26236323953561e-07
        -100.7, -1.6467012169218231e-159
        -150.3, -1.5097598047749159e-263
    |]

let private digammaCases: (float * float)[] =
    [|
        1e-08, -100000000.57721564
        0.01, -100.56088545786868
        0.5, -1.9635100260214235
        1.0, -0.5772156649015329
        1.4616321449683622, -9.241265521729427e-17
        1.5, 0.03648997397857652
        2.0, 0.42278433509846713
        2.5, 0.7031566406452432
        7.9, 2.002238487563571
        10.0, 2.251752589066721
        100.5, 4.605174352581845
        1000000.0, 13.815510057964191
        1000000000000000.0, 34.538776394910684
        -0.5, 0.03648997397857652
        -2.3, 3.3173231575618227
        -10.7, 0.13374481244706443
        -1000000000.9521098, -4.831288283987657e-05
    |]

let private betaCases: (float * float * float * float)[] =
    [|
        0.5, 0.5, 3.141592653589793, 1.1447298858494002
        1.0, 1.0, 1.0, 0.0
        2.0, 3.0, 0.08333333333333333, -2.4849066497880004
        0.001, 0.001, 1999.996714935228, 7.600900817008347
        0.1, 10.0, 7.5913800009109895, 2.0270133931824375
        10.5, 20.25, 2.568137333897245e-09, -19.780084973616695
        8.0, 8.0, 1.9425019425019425e-05, -10.848948661710063
        7.9, 8.1, 1.9450899301260316e-05, -10.847617252400042
        100.0, 200.0, 3.607285449794666e-84, -192.13419227497894
        1000000.0, 4.0, 5.999964000149999e-24, -53.47028876262204
        3.5, 100000.0, 1.050889876096561e-17, -39.094309274611234
        1000.0, 0.01, 92.79634711761626, 4.530407276060692
    |]

let private betaLargeCases: (float * float * float * float)[] =
    [|
        285012.3312716716, 54.57821217759673, 8.179028401067405e-228, -522.887827836458
        56.66781809454644, 409868.9057467205, 1.6303372430918338e-244, -561.3419757995265
        221798.3284124938, 57.378267480692294, 5.8907725724773266e-232, -532.4263544187525
        163.29755153612564, 2693.0586087351535, 3.1757281850743475e-273, -627.4501934316967
        34.62098830767503, 32691.762932782338, 3.856283668053207e-119, -272.65792212688245
        90.80721825997759, 41402.73972602267, 3.108210272216323e-282, -648.1949491389507
        194.49704136848504, 1057.1279356594662, 3.1039681606970696e-236, -542.2774006018338
        338.87483434172145, 636.8072643768838, 3.904266834117713e-275, -631.8488305582443
    |]

let private logFactorialCases: (int * float)[] =
    [|
        0, 0.0
        1, 0.0
        2, 0.6931471805599453
        10, 15.104412573075516
        20, 42.335616460753485
        21, 45.38013889847691
        100, 363.73937555556347
        1000, 5912.128178488163
        1000000, 12815518.384658169
        2147483647, 43996705655.378525
    |]

let private logChooseCases: (int * int * float)[] =
    [|
        0, 0, 0.0
        5, 0, 0.0
        5, 5, 0.0
        10, 3, 4.787491742782046
        50, 25, 32.47055650581199
        1000, 500, 689.4672615678512
        1000000, 3, 39.654769204662266
        1000000, 999997, 39.654769204662266
        2147483647, 2, 42.28197801275968
    |]

let private log1pCases: (float * float)[] =
    [|
        -0.5, -0.6931471805599453
        -0.9, -2.302585092994046
        -0.001, -0.0010005003335835335
    |]

let private expm1Cases: (float * float)[] =
    [|
        -0.5, -0.3934693402873666
        -5.0, -0.9932620530009145
    |]

// python -I tests/reference/special.py erf
let private erfCases: (float * float)[] =
    [|
        1e-300, 1.1283791670955126e-300
        1e-20, 1.1283791670955125e-20
        1e-08, 1.1283791670955126e-08
        0.1, 0.1124629160182849
        0.4, 0.42839235504666845
        0.5, 0.5204998778130465
        0.9, 0.7969082124228322
        1.0, 0.8427007929497149
        1.5, 0.9661051464753108
        1.99, 0.995111413199617
        2.0, 0.9953222650189527
        2.5, 0.999593047982555
        3.0, 0.9999779095030014
        4.0, 0.9999999845827421
        5.0, 0.9999999999984626
        6.0, 1.0
        -0.7, -0.6778011938374184
        -3.0, -0.9999779095030014
        1.4155672647003859, 0.9547060639511419
        1.3842121810791699, 0.9497197021106583
        -0.5754788416632675, -0.5842691027884962
    |]

let private erfcCases: (float * float)[] =
    [|
        -6.0, 2.0
        -3.0, 1.9999779095030015
        -1.0, 1.8427007929497148
        -0.5, 1.5204998778130465
        1e-10, 0.999999999887162
        0.3, 0.6713732405408726
        0.5, 0.4795001221869535
        0.9, 0.20309178757716786
        1.0, 0.15729920705028513
        1.5, 0.033894853524689274
        1.99, 0.004888586800383003
        2.0, 0.004677734981047266
        2.01, 0.004475150644751763
        2.5, 0.0004069520174449589
        3.0, 2.209049699858544e-05
        4.5, 1.9661604415428876e-10
        6.0, 2.1519736712498913e-17
        10.0, 2.088487583762545e-45
        15.0, 7.212994172451207e-100
        20.0, 5.395865611607901e-176
        26.0, 5.663192408856143e-296
        26.2, 1.643250792438946e-300
        1.9900586191485314, 0.004887326150968393
        1.9965492769326505, 0.004749545194316568
        1.993136684222406, 0.004821541735648791
        1.9984920195828624, 0.004708994549438264
        1.9999999999999993, 0.004677734981047279
        1.7971114942683344, 0.01103781204087212
        1.799206427704676, 0.010944617826032545
        2.097369509069908, 0.0030157455022655753
        13.556885625435964, 6.302005962203107e-82
        26.079080580576477, 9.186008752467783e-298
        26.209397193028817, 1.0038244562485365e-300
        1.9984023829417783, 0.004710858606266666
        1.9952152428717123, 0.004777573082879369
    |]

// python -I tests/reference/special.py incgamma
let private incompleteGammaCases: (float * float * float * float)[] =
    [|
        0.5, 0.01, 0.1124629160182849, 0.8875370839817152
        0.5, 0.1, 0.345279153981423, 0.654720846018577
        0.5, 2.0, 0.9544997361036416, 0.04550026389635842
        1.0, 1e-05, 9.999950000166666e-06, 0.9999900000499998
        1.0, 1.0, 0.6321205588285577, 0.36787944117144233
        1.0, 690.0, 1.0, 2.171738281389827e-300
        2.5, 3.0, 0.6937810815867216, 0.3062189184132784
        3.0, 0.5, 0.014387677966970687, 0.9856123220330293
        10.0, 10.0, 0.5420702855281478, 0.4579297144718522
        10.0, 30.0, 0.9999928782491372, 7.121750862815577e-06
        10.0, 100.0, 1.0, 1.1253473960842733e-31
        30.0, 10.0, 2.5099512015279076e-07, 0.9999997490048799
        100.0, 90.0, 0.15822098918643016, 0.8417790108135699
        100.0, 110.0, 0.8417213299399129, 0.15827867006008708
        1000.0, 1000.0, 0.5042052441802155, 0.4957947558197845
        1000.0, 900.0, 0.0005499022657117829, 0.9994500977342882
        0.01, 0.001, 0.938570652526129, 0.061429347473871014
        0.001, 5.0, 0.9999988490186603, 1.150981339730861e-06
        951.0231977761456, 272.1197103557075, 1.9335245240493914e-224, 1.0
        183.62946971865998, 3.2110236532743675, 1.377956710571754e-246, 1.0
        720.8248999971448, 169.1963751588507, 1.3867721104066327e-216, 1.0
        999.9999999999999, 2665.0, 1.0, 2.991878023048829e-300
        709.2095670657058, 1809.3012846853007, 1.0, 4.804928779504767e-192
        1e-200, 0.5615, 1.0, 4.914931248869244e-201
        0.2398004852923794, 1.2191387010096424, 0.953063746869373, 0.046936253130626965
        18.09467398327835, 2.9282007020591715e-15, 1.2487086434146043e-279, 1.0
        112.97305207532186, 0.21468962230833327, 1.3399078295016432e-260, 1.0
        1.691588477341068e-10, 0.8505543045792839, 0.9999999999520027, 4.799725146888966e-11
        0.5, 5e-324, 2.508114666398235e-162, 1.0
        5e-324, 0.5, 1.0, 0.0
        1e-12, 5e-324, 0.9999999992561371, 7.438628559798145e-10
    |]

// python -I tests/reference/special.py incbeta
let private incompleteBetaCases: (float * float * float * float)[] =
    [|
        1.0, 1.0, 0.3, 0.3
        0.5, 0.5, 0.2, 0.2951672353008666
        2.0, 3.0, 0.4, 0.5248
        5.0, 10.0, 0.1, 0.009230212455850002
        10.0, 5.0, 0.9, 0.99076978754415
        0.1, 10.0, 0.01, 0.8244896709066988
        50.0, 50.0, 0.5, 0.5
        50.0, 60.0, 0.4, 0.12470685834310036
        200.0, 300.0, 0.4, 0.502428616319932
        0.001, 0.001, 0.5, 0.5
        30.0, 2.0, 0.01, 3.0700000000000017e-59
        1000.0, 1000.0, 0.49, 0.18555265943151145
        0.5, 1000.0, 0.0001, 0.3452468691021009
        7.5, 0.25, 0.999, 0.6798199172452066
    |]

let private incompleteBetaScanCases: (float * float * float * float)[] =
    [|
        26.47726446673045, 122.94025167092182, 1.0975185646430612e-11, 9.509433838750919e-262
        38.04870023325283, 530.2670642127772, 3.682337338018041e-09, 3.4181834998388033e-262
        526.934562703818, 377.3827200800417, 0.11432183348485359, 5.952196805299492e-252
        648.9439616655302, 0.13591441112284136, 0.3901655144016795, 4.5684369660301066e-269
        1.33887962351026, 3.0156634547318433, 3.393673180998573e-171, 2.2727489071285554e-228
        187.03445169233888, 676.1498087962242, 0.21758279285097282, 0.5318529746991384
        96.77412815163682, 5.399004386837298, 0.9463164204577453, 0.43248441136500787
        6.729458999421565, 1.0893450340321005, 0.7964308989536404, 0.2422456528613247
        689.6875600619811, 1.525515418979068e-10, 0.9987418730036892, 4.200060598935009e-11
        0.06574199246585886, 0.01713668015682447, 0.524342739714624, 0.20753207703665882
        802.5335441586208, 364.46238711754455, 0.31261349811999317, 1.7924595508915173e-152
        12.160964710731095, 36.5970243760941, 7.663753456911437e-19, 3.839137684992898e-210
        2.0, 1e-300, 0.9, 1.4025850929940459e-300
        0.5, 0.5, 5e-324, 1.41505216912524e-162
        0.25, 1000.0, 5e-324, 9.248778146509729e-81
        1000.0, 1e-10, 0.9999999999999999, 2.9252329666424127e-09
        1e-10, 1000.0, 1.1102230246251565e-16, 0.999999997074767
        5e-324, 1.0, 0.5, 1.0
        999.9999999999999, 1000.0, 0.5, 0.500000000000001
    |]

let private raises (name: string) (f: unit -> float) : unit =
    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> f () |> ignore) name

[<Tests>]
let tests =
    testList
        "Special"
        [
            test "gammaLn matches the reference" {
                for x, expected in gammaLnCases do
                    expectClose 1e-13 1e-14 expected (Special.gammaLn x) (sprintf "gammaLn %.17g" x)
            }

            test "gamma matches the reference" {
                for x, expected in gammaCases do
                    expectClose 1e-13 0.0 expected (Special.gamma x) (sprintf "gamma %g" x)
            }

            test "gamma of n is (n - 1)!" {
                // Γ(n) = (n − 1)!; the float product 1 · 2 · … · (n − 1) is exact for n ≤ 23.
                let mutable factorial = 1.0

                for n in 1..23 do
                    expectClose 1e-13 0.0 factorial (Special.gamma (float n)) (sprintf "gamma %d" n)
                    factorial <- factorial * float n
            }

            test "gamma of one half is the root of pi" {
                // Γ(½) = √π.
                expectClose 1e-13 0.0 (sqrt Math.PI) (Special.gamma 0.5) "gamma 0.5"
            }

            test "gamma satisfies its recurrence" {
                // Γ(x + 1) = x Γ(x).
                let xs =
                    [ for k in 1..400 -> 0.05 * float k ] @ [ for k in 0..100 -> -0.05 * float k - 0.025 ]

                for x in xs do
                    expectClose 3e-13 0.0 (x * Special.gamma x) (Special.gamma (x + 1.0)) (sprintf "x = %g" x)
            }

            test "gamma overflows from 171.63" {
                Expect.isLessThan (Special.gamma 171.62) infinity "gamma 171.62 is finite"
                Expect.equal (Special.gamma 171.63) infinity "gamma 171.63"
                Expect.equal (Special.gamma 1e10) infinity "gamma 1e10"
            }

            test "digamma matches the reference" {
                for x, expected in digammaCases do
                    expectClose 1e-13 1e-14 expected (Special.digamma x) (sprintf "digamma %.17g" x)
            }

            test "beta and betaLn match the reference" {
                for a, b, expectedBeta, expectedLn in betaCases do
                    expectClose 1e-13 0.0 expectedBeta (Special.beta a b) (sprintf "beta %g %g" a b)
                    expectClose 1e-13 1e-14 expectedLn (Special.betaLn a b) (sprintf "betaLn %g %g" a b)
            }

            test "beta where ln B is at least 256 in magnitude" {
                for a, b, expectedBeta, expectedLn in betaLargeCases do
                    let at = sprintf "at %g %g" a b
                    Expect.isTrue (abs expectedLn >= 256.0 && expectedBeta >= 1e-300) ("the case is in the regime " + at)
                    expectClose 5e-13 0.0 expectedBeta (Special.beta a b) ("beta " + at)
                    expectClose 1e-13 1e-14 expectedLn (Special.betaLn a b) ("betaLn " + at)

                for _, _, _, expectedLn in betaCases do
                    Expect.isLessThan (abs expectedLn) 256.0 "the brief's cases hold beta to 1e-13, below |ln B| = 256"
            }

            test "beta is symmetric" {
                for a, b, _, _ in Array.append betaCases betaLargeCases do
                    Expect.equal (Special.beta b a) (Special.beta a b) (sprintf "beta %g %g" a b)
                    Expect.equal (Special.betaLn b a) (Special.betaLn a b) (sprintf "betaLn %g %g" a b)
            }

            test "logFactorial matches the reference" {
                for n, expected in logFactorialCases do
                    expectClose 1e-13 1e-14 expected (Special.logFactorial n) (sprintf "logFactorial %d" n)

                Expect.equal (Special.logFactorial 0) 0.0 "ln 0! = 0"
                Expect.equal (Special.logFactorial 1) 0.0 "ln 1! = 0"
            }

            test "logChoose matches the reference" {
                for n, k, expected in logChooseCases do
                    expectClose 1e-13 1e-14 expected (Special.logChoose n k) (sprintf "logChoose %d %d" n k)
                    Expect.equal (Special.logChoose n (n - k)) (Special.logChoose n k) (sprintf "symmetry at %d %d" n k)
            }

            test "log1p and expm1 at small arguments" {
                // ln(1 + x) = x − x²/2 + x³/3 − …, eˣ − 1 = x + x²/2 + x³/6 + …; at x = 1e-10 the x³ term, 3.3e-31, is
                // below an ulp of the result.
                Expect.isLessThanOrEqual (ulps (Special.log1p 1e-10) (1e-10 - 5e-21)) 1.0 "log1p 1e-10"
                Expect.isLessThanOrEqual (ulps (Special.expm1 1e-10) (1e-10 + 5e-21)) 1.0 "expm1 1e-10"
                Expect.equal (Special.log1p 1e-300) 1e-300 "log1p 1e-300"
                Expect.equal (Special.expm1 1e-300) 1e-300 "expm1 1e-300"

                for x in [ 1e-5; -1e-5 ] do
                    // The series to the x⁶ term; the x⁷ term is below 1e-31 relative.
                    let log1pSeries =
                        x * (1.0 - x * (1.0 / 2.0 - x * (1.0 / 3.0 - x * (1.0 / 4.0 - x * (1.0 / 5.0 - x / 6.0)))))

                    let expm1Series =
                        x
                        * (1.0 + x * (1.0 / 2.0 + x * (1.0 / 6.0 + x * (1.0 / 24.0 + x * (1.0 / 120.0 + x / 720.0)))))

                    expectClose 1e-15 0.0 log1pSeries (Special.log1p x) (sprintf "log1p %g" x)
                    expectClose 1e-15 0.0 expm1Series (Special.expm1 x) (sprintf "expm1 %g" x)

                Expect.equal (Special.log1p 0.0) 0.0 "log1p 0"
                Expect.equal (Special.expm1 0.0) 0.0 "expm1 0"
            }

            test "log1p and expm1 match the reference at negative arguments" {
                for x, expected in log1pCases do
                    expectClose 1e-15 0.0 expected (Special.log1p x) (sprintf "log1p %g" x)

                for x, expected in expm1Cases do
                    expectClose 1e-15 0.0 expected (Special.expm1 x) (sprintf "expm1 %g" x)
            }

            test "log1p and expm1 inverses and ranges" {
                for x in [ 0.5; 3.0; 1e5 ] do
                    expectClose 1e-15 0.0 x (Special.expm1 (Special.log1p x)) (sprintf "expm1 (log1p %g)" x)

                Expect.equal (Special.log1p -1.0) (-infinity) "log1p -1"
                Expect.equal (Special.log1p infinity) infinity "log1p infinity"
                Expect.equal (Special.expm1 (-infinity)) -1.0 "expm1 -infinity"
                Expect.equal (Special.expm1 1000.0) infinity "expm1 1000"
                Expect.isTrue (Double.IsNaN (Special.log1p nan)) "log1p nan"
                Expect.isTrue (Double.IsNaN (Special.expm1 nan)) "expm1 nan"
                raises "log1p -1.5" (fun () -> Special.log1p -1.5)
            }

            test "erf matches the reference" {
                for x, expected in erfCases do
                    expectClose 1e-13 0.0 expected (Special.erf x) (sprintf "erf %.17g" x)
            }

            test "erfc matches the reference" {
                for x, expected in erfcCases do
                    Expect.isTrue (expected >= 1e-300) (sprintf "erfc %.17g is at least 1e-300" x)
                    expectClose 1e-13 0.0 expected (Special.erfc x) (sprintf "erfc %.17g" x)
            }

            test "erf is odd and erfc reflects" {
                let xs = Array.append (Array.map fst erfCases) (Array.map fst erfcCases)

                for x in xs do
                    Expect.equal (Special.erf (-x)) (-(Special.erf x)) (sprintf "erf %.17g" (-x))

                for x in xs |> Array.filter (fun x -> x >= 0.0) do
                    Expect.equal (Special.erfc (-x)) (2.0 - Special.erfc x) (sprintf "erfc %.17g" (-x))
            }

            test "erf and erfc sum to one" {
                // erf x + erfc x = 1.
                for k in -100 .. 100 do
                    let x = float k / 100.0
                    expectClose 0.0 3e-16 1.0 (Special.erf x + Special.erfc x) (sprintf "x = %g" x)
            }

            test "limits" {
                Expect.equal (Special.erf 0.0) 0.0 "erf 0"
                Expect.equal (1.0 / Special.erf -0.0) (-infinity) "erf -0 is -0"
                Expect.equal (Special.erfc 0.0) 1.0 "erfc 0"
                Expect.equal (Special.erf infinity) 1.0 "erf infinity"
                Expect.equal (Special.erf (-infinity)) -1.0 "erf -infinity"
                Expect.equal (Special.erfc infinity) 0.0 "erfc infinity"
                Expect.equal (Special.erfc (-infinity)) 2.0 "erfc -infinity"
                Expect.equal (Special.erfc 40.0) 0.0 "erfc 40"
                Expect.isTrue (Double.IsNaN (Special.erf nan)) "erf nan"
                Expect.isTrue (Double.IsNaN (Special.erfc nan)) "erfc nan"
            }

            test "infinity and NaN" {
                for name, f in [ "gammaLn", Special.gammaLn; "gamma", Special.gamma; "digamma", Special.digamma ] do
                    Expect.equal (f infinity) infinity (name + " infinity")
                    Expect.isTrue (Double.IsNaN (f nan)) (name + " nan")

                Expect.isTrue (Double.IsNaN (Special.beta nan 1.0)) "beta nan 1"
                Expect.isTrue (Double.IsNaN (Special.betaLn 1.0 nan)) "betaLn 1 nan"
                // B(a, b) → 0 as a → ∞ for a fixed b > 0.
                Expect.equal (Special.betaLn infinity 2.0) (-infinity) "betaLn infinity 2"
                Expect.equal (Special.beta 2.0 infinity) 0.0 "beta 2 infinity"
            }

            test "arguments outside the domain raise" {
                raises "gammaLn 0" (fun () -> Special.gammaLn 0.0)
                raises "gammaLn -3" (fun () -> Special.gammaLn -3.0)
                raises "gammaLn -infinity" (fun () -> Special.gammaLn (-infinity))
                raises "gamma -1" (fun () -> Special.gamma -1.0)
                raises "digamma 0" (fun () -> Special.digamma 0.0)
                raises "digamma -2" (fun () -> Special.digamma -2.0)
                raises "beta 0 1" (fun () -> Special.beta 0.0 1.0)
                raises "betaLn 1 -1" (fun () -> Special.betaLn 1.0 -1.0)
                raises "logFactorial -1" (fun () -> Special.logFactorial -1)
                raises "logChoose 5 6" (fun () -> Special.logChoose 5 6)
                raises "logChoose 5 -1" (fun () -> Special.logChoose 5 (-1))
            }

            test "incomplete gamma matches the reference" {
                for a, x, expectedP, expectedQ in incompleteGammaCases do
                    let at = sprintf "at a = %.17g, x = %.17g" a x

                    if expectedP >= 1e-300 then
                        expectClose 1e-12 0.0 expectedP (Special.gammaLowerRegularized a x) ("P " + at)

                    if expectedQ >= 1e-300 then
                        expectClose 1e-12 0.0 expectedQ (Special.gammaUpperRegularized a x) ("Q " + at)
            }

            test "P of one half is erf of the root" {
                // P(½, x) = erf √x.
                for x in [ 0.01; 0.5; 2.0; 10.0 ] do
                    let expected = Special.erf (sqrt x)
                    expectClose 1e-12 0.0 expected (Special.gammaLowerRegularized 0.5 x) (sprintf "x = %g" x)
            }

            test "Q of an integer shape is a Poisson sum" {
                // Q(n, x) = e⁻ˣ Σₖ₌₀ⁿ⁻¹ xᵏ/k!.
                let expected = exp -0.5 * (1.0 + 0.5 + 0.125)
                expectClose 1e-12 0.0 expected (Special.gammaUpperRegularized 3.0 0.5) "Q(3, 0.5)"

                for x in [ 5.0; 50.0; 500.0 ] do
                    expectClose 1e-12 0.0 (exp (-x)) (Special.gammaUpperRegularized 1.0 x) (sprintf "Q(1, %g)" x)
            }

            test "P and Q sum to one" {
                for a, x, _, _ in incompleteGammaCases do
                    let sum = Special.gammaLowerRegularized a x + Special.gammaUpperRegularized a x
                    expectClose 0.0 4e-16 1.0 sum (sprintf "a = %.17g, x = %.17g" a x)
            }

            test "incomplete gamma limits" {
                for a in [ 0.5; 3.0; 100.0 ] do
                    Expect.equal (Special.gammaLowerRegularized a 0.0) 0.0 (sprintf "P(%g, 0)" a)
                    Expect.equal (Special.gammaUpperRegularized a 0.0) 1.0 (sprintf "Q(%g, 0)" a)
                    Expect.equal (Special.gammaLowerRegularized a infinity) 1.0 (sprintf "P(%g, infinity)" a)
                    Expect.equal (Special.gammaUpperRegularized a infinity) 0.0 (sprintf "Q(%g, infinity)" a)
            }

            test "the inverse round-trips" {
                for a in [ 0.1; 0.5; 1.0; 2.5; 10.0; 100.0; 1000.0 ] do
                    for p in [ 1e-10; 1e-3; 0.05; 0.5; 0.95; 0.999; 1.0 - 1e-10 ] do
                        let x = Special.gammaLowerRegularizedInverse a p
                        let at = sprintf "a = %g, p = %.17g, x = %.17g" a p x
                        expectClose 1e-12 0.0 p (Special.gammaLowerRegularized a x) at

                // Deep in the lower tail, where the starting point's Wilson–Hilferty cube is negative, and the worst
                // case of a dense seeded scan of 3,156 pairs on .NET and JavaScript.
                for a, p in
                    [
                        2.5, 1e-300
                        3.922067060095062, 1.3028626123441362e-276
                        100.0, 1e-300
                        100.0, 1e-200
                        717.0392350199576, 2.6218221520418342e-295
                    ] do
                    let x = Special.gammaLowerRegularizedInverse a p
                    let at = sprintf "a = %.17g, p = %.17g, x = %.17g" a p x
                    expectClose 1e-12 0.0 p (Special.gammaLowerRegularized a x) at
            }

            test "the inverse at 0 and 1" {
                for a in [ 0.5; 10.0 ] do
                    Expect.equal (Special.gammaLowerRegularizedInverse a 0.0) 0.0 (sprintf "a = %g, p = 0" a)
                    Expect.equal (Special.gammaLowerRegularizedInverse a 1.0) infinity (sprintf "a = %g, p = 1" a)

                // P(0.01, x) exceeds 1e-10 at every positive double x, so the root of P(0.01, x) = 1e-10 is below
                // them all.
                Expect.isGreaterThan (Special.gammaLowerRegularized 0.01 5e-324) 1e-10 "P(0.01, 5e-324)"
                let belowAll = Special.gammaLowerRegularizedInverse 0.01 1e-10
                Expect.equal belowAll 0.0 "a root below the least positive double"
            }

            test "domain and NaN" {
                for a in [ 0.0; -1.0; infinity ] do
                    raises (sprintf "P with a = %g" a) (fun () -> Special.gammaLowerRegularized a 1.0)
                    raises (sprintf "Q with a = %g" a) (fun () -> Special.gammaUpperRegularized a 1.0)
                    raises (sprintf "inverse with a = %g" a) (fun () -> Special.gammaLowerRegularizedInverse a 0.5)

                raises "P with x = -1" (fun () -> Special.gammaLowerRegularized 1.0 -1.0)
                raises "Q with x = -1" (fun () -> Special.gammaUpperRegularized 1.0 -1.0)
                raises "inverse with p = -0.1" (fun () -> Special.gammaLowerRegularizedInverse 1.0 -0.1)
                raises "inverse with p = 1.1" (fun () -> Special.gammaLowerRegularizedInverse 1.0 1.1)
                // An argument outside the domain raises even beside a NaN.
                raises "P with a = NaN and x = -1" (fun () -> Special.gammaLowerRegularized nan -1.0)
                raises "inverse with a = -1 and p = NaN" (fun () -> Special.gammaLowerRegularizedInverse -1.0 nan)

                for name, value in
                    [
                        "P(NaN, 1)", Special.gammaLowerRegularized nan 1.0
                        "P(1, NaN)", Special.gammaLowerRegularized 1.0 nan
                        "Q(NaN, 1)", Special.gammaUpperRegularized nan 1.0
                        "Q(1, NaN)", Special.gammaUpperRegularized 1.0 nan
                        "inverse(NaN, 0.5)", Special.gammaLowerRegularizedInverse nan 0.5
                        "inverse(1, NaN)", Special.gammaLowerRegularizedInverse 1.0 nan
                    ] do
                    Expect.isTrue (Double.IsNaN value) name
            }

            test "incomplete beta matches the reference" {
                for a, b, x, expected in Array.append incompleteBetaCases incompleteBetaScanCases do
                    let at = sprintf "I_x(a, b) at a = %.17g, b = %.17g, x = %.17g" a b x
                    Expect.isTrue (expected >= 1e-300) ("the value is at least 1e-300 " + at)
                    expectClose 1e-12 0.0 expected (Special.betaRegularized a b x) at
            }

            test "closed forms" {
                // I_x(½, ½) = (2/π) asin √x.
                let arcsine = 2.0 / Math.PI * asin (sqrt 0.2)
                expectClose 1e-12 0.0 arcsine (Special.betaRegularized 0.5 0.5 0.2) "I_0.2(0.5, 0.5)"

                // I_x(a, 1) = xᵃ and I_x(1, b) = 1 − (1 − x)ᵇ.
                for s in [ 0.5; 3.0; 20.0 ] do
                    for x in [ 0.1; 0.5; 0.9 ] do
                        expectClose 1e-12 0.0 (x ** s) (Special.betaRegularized s 1.0 x) (sprintf "I_%g(%g, 1)" x s)
                        let expected = 1.0 - (1.0 - x) ** s
                        expectClose 1e-12 0.0 expected (Special.betaRegularized 1.0 s x) (sprintf "I_%g(1, %g)" x s)
            }

            test "symmetry" {
                // I_u(a, b) + I_(1 − u)(b, a) = 1, at u = x where 1 − x is exact (x ≥ 0.5), and otherwise at
                // u = 1 − (1 − x), whose complement 1 − x is exact because it is at least 0.5.
                for a, b, x, _ in incompleteBetaCases do
                    let u = if x >= 0.5 then x else 1.0 - (1.0 - x)
                    let sum = Special.betaRegularized a b u + Special.betaRegularized b a (1.0 - u)
                    expectClose 0.0 1e-15 1.0 sum (sprintf "a = %.17g, b = %.17g, u = %.17g" a b u)
            }

            test "incomplete beta limits" {
                for a, b in [ 0.5, 2.0; 30.0, 40.0 ] do
                    Expect.equal (Special.betaRegularized a b 0.0) 0.0 (sprintf "I_0(%g, %g)" a b)
                    Expect.equal (Special.betaRegularized a b 1.0) 1.0 (sprintf "I_1(%g, %g)" a b)
            }

            test "the beta inverse round-trips" {
                for a, b in [ 0.5, 0.5; 1.0, 1.0; 2.0, 5.0; 10.0, 10.0; 0.1, 3.0; 100.0, 200.0 ] do
                    for p in [ 1e-10; 1e-3; 0.05; 0.5; 0.95; 0.999; 1.0 - 1e-10 ] do
                        // No double meets the bound at (½, ½) and p = 1 − 1e-10: `the beta inverse at 0 and 1`.
                        if not (a = 0.5 && b = 0.5 && p = 1.0 - 1e-10) then
                            let x = Special.betaRegularizedInverse a b p
                            let at = sprintf "a = %g, b = %g, p = %.17g, x = %.17g" a b p x
                            expectClose 1e-12 0.0 p (Special.betaRegularized a b x) at

                // The worst cases of a dense seeded scan of 3,532 triples on .NET and JavaScript, in the lower tail,
                // between the tails and in the upper tail; the deep lower tail, where the method as first written gave
                // NaN or stopped far from the root; and a tiny a with p near 1, where I_x(a, b) is flat.
                for a, b, p in
                    [
                        520.5272457067133, 482.40015784157526, 9.421765318857333e-291
                        907.0707331834819, 179.18759978641594, 1.2022354659975348e-248
                        151.14317283317897, 0.5817356088616344, 3.042247392967044e-284
                        32.53993451221309, 0.21281835074738098, 0.8879382163125997
                        1000.0, 1.0, 0.95
                        1000.0, 1.0, 0.9999999999
                        388.118816571457, 14.38984189291441, 4.9997291934613676e-161
                        1000.0, 1.0, 1e-300
                        1000.0, 1000.0, 1e-300
                        10.0, 10.0, 1e-300
                        100.0, 200.0, 1e-300
                        6.157292559366567e-07, 10.68992791033839, 0.9996490099838193
                    ] do
                    let x = Special.betaRegularizedInverse a b p
                    let at = sprintf "a = %.17g, b = %.17g, p = %.17g, x = %.17g" a b p x
                    expectClose 1e-12 0.0 p (Special.betaRegularized a b x) at

                // Tiny shapes, where the starting point as first written cancelled to 0 or fell below 0 (the review of
                // this task). The roots, by mpmath bisection, are normal doubles: 0.9, 0.9, 0.2264, 0.00244, ½ and
                // 1.35e-48. For a tiny a with p near 1, I_x(a, b) is so flat that doubles far from the root meet the
                // bound as well.
                for a, b, p in
                    [
                        0.3, 7e-19, 3.6083087287046146e-18
                        0.3, 6.999999999999999e-300, 3.608308728704614e-299
                        1e-17, 0.1, 0.9999999999999999
                        7e-18, 0.1, 0.9999999999999999
                        6.999999999999999e-17, 0.001, 0.99999999999993
                        1e-19, 0.001, 0.9999999999999999
                    ] do
                    let x = Special.betaRegularizedInverse a b p
                    let at = sprintf "a = %.17g, b = %.17g, p = %.17g, x = %.17g" a b p x
                    Expect.isTrue (x >= 2.2250738585072014e-308) ("the result is a normal double " + at)
                    expectClose 1e-12 0.0 p (Special.betaRegularized a b x) at
            }

            test "the beta inverse near 1 for b below 1" {
                // There I_x(a, b) steps by more than 1e-12 p between adjacent doubles, spaced 2⁻⁵³ in [½, 1), so no
                // double meets the bound; the root lies within one double of the result. Cases from the scan.
                let spacing = Math.Pow (2.0, -53.0)

                for a, b, p in
                    [
                        1.8616811097043242, 0.005844168300205897, 0.1799916473362808
                        351.6137553093089, 0.018377542727907845, 0.23162757870093953
                        1000.0, 0.5, 0.9999999999
                        // The review's case where the starting point as first written cancelled to 0; the root,
                        // 0.99999989633 by mpmath bisection, lies where adjacent doubles step I by 80 times the bound.
                        7.236969982398896, 1.3670246232982652e-19, 1.858652750036213e-18
                    ] do
                    let x = Special.betaRegularizedInverse a b p
                    let at = sprintf "a = %.17g, b = %.17g, p = %.17g, x = %.17g" a b p x
                    let below = Special.betaRegularized a b (x - spacing)
                    let above = if x < 1.0 then Special.betaRegularized a b (x + spacing) else 1.0
                    Expect.isTrue (x >= 0.5) ("the result is in [1/2, 1] " + at)
                    Expect.isTrue (below <= p && p <= above) ("the root lies within one double " + at)

                    for neighbour in [ below; Special.betaRegularized a b x; above ] do
                        Expect.isGreaterThan (abs (neighbour - p)) (1e-12 * p) ("no double meets the bound " + at)
            }

            test "the beta inverse at 0 and 1" {
                Expect.equal (Special.betaRegularizedInverse 2.0 3.0 0.0) 0.0 "p = 0"
                Expect.equal (Special.betaRegularizedInverse 2.0 3.0 1.0) 1.0 "p = 1"

                // 1 − I_x(½, ½) = I_(1 − x)(½, ½) = (2/π) asin √(1 − x) is 6.7e-9 at x = 1 − 2⁻⁵³, the largest double
                // below 1, so the root of I_x(½, ½) = 1 − 1e-10, near 1 − 2.5e-20, lies above it: 1 is the double
                // nearest the root.
                let gapBelowOne = 2.0 / Math.PI * asin (sqrt (Math.Pow (2.0, -53.0)))
                Expect.isGreaterThan gapBelowOne (1.0 - (1.0 - 1e-10)) "1 - I below 1, against 1 - p"
                let nearOne = Special.betaRegularizedInverse 0.5 0.5 (1.0 - 1e-10)
                Expect.equal nearOne 1.0 "a root nearer 1 than any other double"

                // I_x(0.01, 1) = x^0.01 exceeds 1e-10 at every positive double x, so the root of I_x(0.01, 1) = 1e-10
                // is below them all.
                Expect.isGreaterThan (Special.betaRegularized 0.01 1.0 5e-324) 1e-10 "I at 5e-324"
                let belowAll = Special.betaRegularizedInverse 0.01 1.0 1e-10
                Expect.equal belowAll 0.0 "a root below the least positive double"
            }

            test "incomplete beta domain and NaN" {
                raises "I with a = 0" (fun () -> Special.betaRegularized 0.0 1.0 0.5)
                raises "I with b = -1" (fun () -> Special.betaRegularized 1.0 -1.0 0.5)
                raises "I with a = infinity" (fun () -> Special.betaRegularized infinity 1.0 0.5)
                raises "I with x = -0.1" (fun () -> Special.betaRegularized 1.0 1.0 -0.1)
                raises "I with x = 1.1" (fun () -> Special.betaRegularized 1.0 1.0 1.1)
                raises "inverse with a = 0" (fun () -> Special.betaRegularizedInverse 0.0 1.0 0.5)
                raises "inverse with b = -1" (fun () -> Special.betaRegularizedInverse 1.0 -1.0 0.5)
                raises "inverse with b = infinity" (fun () -> Special.betaRegularizedInverse 1.0 infinity 0.5)
                raises "inverse with p = 2" (fun () -> Special.betaRegularizedInverse 1.0 1.0 2.0)
                raises "inverse with p = -0.1" (fun () -> Special.betaRegularizedInverse 1.0 1.0 -0.1)
                // An argument outside the domain raises even beside a NaN.
                raises "I with a = NaN and x = 1.1" (fun () -> Special.betaRegularized nan 1.0 1.1)
                raises "inverse with a = -1 and p = NaN" (fun () -> Special.betaRegularizedInverse -1.0 1.0 nan)

                for name, value in
                    [
                        "I(NaN, 1, 0.5)", Special.betaRegularized nan 1.0 0.5
                        "I(1, NaN, 0.5)", Special.betaRegularized 1.0 nan 0.5
                        "I(1, 1, NaN)", Special.betaRegularized 1.0 1.0 nan
                        "inverse(NaN, 1, 0.5)", Special.betaRegularizedInverse nan 1.0 0.5
                        "inverse(1, NaN, 0.5)", Special.betaRegularizedInverse 1.0 nan 0.5
                        "inverse(1, 1, NaN)", Special.betaRegularizedInverse 1.0 1.0 nan
                    ] do
                    Expect.isTrue (Double.IsNaN value) name
            }
        ]
