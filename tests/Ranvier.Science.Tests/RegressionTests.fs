// Reference values: the tables below, printed by
//   python -I tests/reference/special.py regression
// (exact fractions of the integer data, the t p-values in decimal arithmetic), and the identities and closed forms
// written in each test. Data: x1 = 1..12, x2 = 3 1 4 1 5 9 2 6 5 3 5 8, y = 6 5 9 7 13 18 11 15 16 13 16 22.
module Ranvier.Science.Tests.RegressionTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py regression
// each model: coefficients, standard errors, t statistics, two-sided p-values, then R-squared, adjusted
// R-squared, residual standard error and degrees of freedom, then the covariance row by row
let private simpleCoefficients: float[] =
    [|
        5.066666666666666
        1.1333333333333333
    |]

let private simpleStandardErrors: float[] =
    [|
        2.0512868589687248
        0.33059481135209806
    |]

let private simpleT: float[] =
    [|
        2.469994210957852
        3.4281643099542882
    |]

let private simpleP: float[] =
    [|
        0.03871079989354851
        0.008979122165068586
    |]

let private simpleSummary: float[] =
    [|
        0.5949840913344563
        0.5443571027512634
        3.0027764929589194
        8.0
    |]

let private simpleCovariance: float[] =
    [|
        4.207777777777777
        -0.6011111111111112
        -0.6011111111111112
        0.10929292929292929
    |]

let private linearCoefficients: float[] =
    [|
        2.1784712482468445
        0.7859396914446003
        1.2222124824684433
    |]

let private linearStandardErrors: float[] =
    [|
        0.5029873409739876
        0.07159391635307279
        0.10184432534864239
    |]

let private linearT: float[] =
    [|
        4.331065756105193
        10.97774408049781
        12.000791190716406
    |]

let private linearP: float[] =
    [|
        0.0019021364130887392
        1.637661863886233e-06
        7.695563049835868e-07
    |]

let private linearSummary: float[] =
    [|
        0.9831655442639362
        0.9794245541003664
        0.742724830967962
        9.0
    |]

let private linearCovariance: float[] =
    [|
        0.25299626518008245
        -0.017601422118797786
        -0.02137315542996874
        -0.017601422118797786
        0.005125688858770783
        -0.0036266666453566862
        -0.02137315542996874
        -0.0036266666453566862
        0.010372266605720123
    |]

let private noConstantCoefficients: float[] =
    [|
        0.9375
        1.40625
    |]

let private noConstantStandardErrors: float[] =
    [|
        0.10406149124553543
        0.1542058565570166
    |]

let private noConstantT: float[] =
    [|
        9.009096340815908
        9.11930345187667
    |]

let private noConstantP: float[] =
    [|
        4.100629970818264e-06
        3.675411939020598e-06
    |]

let private noConstantSummary: float[] =
    [|
        0.9930239179954442
        0.991628701594533
        1.2374368670764582
        10.0
    |]

let private noConstantCovariance: float[] =
    [|
        0.01082879396024465
        -0.014194500191131498
        -0.014194500191131498
        0.02377944619648318
    |]

let private weightedCoefficients: float[] =
    [|
        5.87090367428004
        1.099304865938431
    |]

let private weightedStandardErrors: float[] =
    [|
        2.053978022641653
        0.30190639146122267
    |]

let private weightedT: float[] =
    [|
        2.8583089057249893
        3.6412109747588017
    |]

let private weightedP: float[] =
    [|
        0.018832252237614852
        0.005390719732681502
    |]

let private weightedSummary: float[] =
    [|
        0.5956585837463745
        0.5507317597181939
        4.181262089929375
        9.0
    |]

let private weightedCovariance: float[] =
    [|
        4.218825717494914
        -0.5555655265836924
        -0.5555655265836924
        0.09114746920513703
    |]

let private dominantCoefficients: float[] =
    [|
        4.798418972332016
        1.2015810276679841
    |]

let private dominantStandardErrors: float[] =
    [|
        0.12918009548968615
        0.12918009548968615
    |]

let private dominantT: float[] =
    [|
        37.14518830584954
        9.301595753688845
    |]

let private dominantP: float[] =
    [|
        4.761018419524502e-12
        3.0737555316669824e-06
    |]

let private dominantSummary: float[] =
    [|
        0.8963941899658091
        0.8860336089623899
        2.9058343926980013
        10.0
    |]

let private dominantCovariance: float[] =
    [|
        0.01668749707072443
        -0.01668749707072443
        -0.01668749707072443
        0.01668749707072443
    |]

let private x1: float[] = [| 1.0 .. 12.0 |]
let private x2: float[] = [| 3.0; 1.0; 4.0; 1.0; 5.0; 9.0; 2.0; 6.0; 5.0; 3.0; 5.0; 8.0 |]
let private y: float[] = [| 6.0; 5.0; 9.0; 7.0; 13.0; 18.0; 11.0; 15.0; 16.0; 13.0; 16.0; 22.0 |]
let private ones: float[] = Array.create 12 1.0
let private weights: float[] = [| 1.0; 2.0; 3.0; 1.0; 2.0; 3.0; 1.0; 2.0; 3.0; 1.0; 2.0; 0.0 |]

let private isNaN (x: float) : bool = Double.IsNaN x

let private designOf (columns: float[][]) : Matrix =
    Matrix.init columns[0].Length columns.Length (fun i j -> columns[j][i])

let private comoments (xs: float[]) (ys: float[]) : Comoments =
    Array.fold2 (Comoments.add true) Comoments.zero xs ys

let private scaleBy (factor: float) (xs: float[]) : float[] = Array.map (fun v -> v * factor) xs

// Five pairs at x = 3 and twelve at x = 3 + k · step added with compensation, then the twelve removed again.
let private leftAfterRemoving (step: float) : Comoments =
    let constantPairs = [| 3.0, 1.0; 3.0, 4.0; 3.0, 2.0; 3.0, 8.0; 3.0, 5.0 |]
    let movedPairs = [| for k in 1..12 -> 3.0 + step * float k, float (k * k % 7) |]
    let mutable m = Comoments.zero

    for (x, yv) in Array.append constantPairs movedPairs do
        m <- Comoments.add true m x yv

    for (x, yv) in movedPairs do
        m <- Comoments.remove true m x yv

    m

// 2^e by repeated doubling, exact where the result is a normal double.
let private power (e: int) : float =
    let mutable r = 1.0

    for _ in 1 .. abs e do
        r <- if e > 0 then r * 2.0 else r / 2.0

    r

let private fitOf (design: Matrix) (ys: float[]) : LinearRegression =
    expectOk "linear" (Regression.linear design ys)

let private residualsOf (design: Matrix) (ys: float[]) (coefficients: float[]) : float[] =
    Array.map2 (-) ys (Matrix.multiplyVector design coefficients)

// Every field of a fit against the script's tables: relative 1e-12 (the p-values are all at least 1e-300).
let private checkFit
    (name: string)
    (design: Matrix)
    (ys: float[])
    (r: LinearRegression)
    (coefficients: float[])
    (errors: float[])
    (t: float[])
    (p: float[])
    (summary: float[])
    (covariance: float[])
    : unit =
    let columns = coefficients.Length
    Expect.equal r.Coefficients.Length columns (name + " coefficient count")

    for j in 0 .. columns - 1 do
        expectClose 1e-12 0.0 coefficients[j] r.Coefficients[j] (sprintf "%s coefficient %d" name j)
        expectClose 1e-12 0.0 errors[j] r.StandardErrors[j] (sprintf "%s standard error %d" name j)
        expectClose 1e-12 0.0 t[j] r.TStatistics[j] (sprintf "%s t %d" name j)
        expectClose 1e-12 0.0 p[j] r.PValues[j] (sprintf "%s p-value %d" name j)

        for k in 0 .. columns - 1 do
            expectClose
                1e-12
                0.0
                covariance[j * columns + k]
                (Matrix.get r.Covariance j k)
                (sprintf "%s covariance %d %d" name j k)

    expectClose 1e-12 0.0 summary[0] r.RSquared (name + " R squared")
    expectClose 1e-12 0.0 summary[1] r.AdjustedRSquared (name + " adjusted R squared")
    expectClose 1e-12 0.0 summary[2] r.ResidualStandardError (name + " residual standard error")
    Expect.equal r.DegreesOfFreedom summary[3] (name + " degrees of freedom")
    Expect.equal r.Residuals.Length ys.Length (name + " residual count")
    let expected = residualsOf design ys r.Coefficients

    for i in 0 .. ys.Length - 1 do
        expectClose 0.0 1e-12 expected[i] r.Residuals[i] (sprintf "%s residual %d" name i)

let private raisesArgument (message: string) (f: unit -> unit) : unit =
    Expect.throwsT<ArgumentException> f message

let private raisesRange (message: string) (f: unit -> unit) : unit =
    Expect.throwsT<ArgumentOutOfRangeException> f message

[<Tests>]
let tests =
    testList
        "Regression"
        [
            testCase "simple regression against exact fractions" (fun () ->
                let r = Regression.simple (comoments (x1[.. 9]) (y[.. 9]))
                expectClose 1e-12 0.0 simpleCoefficients[1] r.Slope "slope"
                expectClose 1e-12 0.0 simpleCoefficients[0] r.Intercept "intercept"
                expectClose 1e-12 0.0 simpleStandardErrors[1] r.SlopeStandardError "slope standard error"
                expectClose 1e-12 0.0 simpleStandardErrors[0] r.InterceptStandardError "intercept standard error"
                expectClose 1e-12 0.0 simpleSummary[0] r.RSquared "R squared"
                expectClose 1e-12 0.0 simpleSummary[2] r.ResidualStandardError "residual standard error"
                Expect.equal r.DegreesOfFreedom 8.0 "degrees of freedom"
                Expect.equal r.Count 10.0 "count")

            testCase "simple regression equals linear regression on a constant and x" (fun () ->
                // Identity: the fit of y on (1, x) is the simple regression of y on x.
                let s = Regression.simple (comoments x1 y)
                let l = fitOf (designOf [| ones; x1 |]) y
                expectClose 1e-12 0.0 l.Coefficients[0] s.Intercept "intercept"
                expectClose 1e-12 0.0 l.Coefficients[1] s.Slope "slope"
                expectClose 1e-12 0.0 l.StandardErrors[0] s.InterceptStandardError "intercept standard error"
                expectClose 1e-12 0.0 l.StandardErrors[1] s.SlopeStandardError "slope standard error"
                expectClose 1e-12 0.0 l.RSquared s.RSquared "R squared"
                expectClose 1e-12 0.0 l.ResidualStandardError s.ResidualStandardError "residual standard error"
                Expect.equal s.DegreesOfFreedom l.DegreesOfFreedom "degrees of freedom")

            testCase "linear regression against exact fractions" (fun () ->
                let design = designOf [| ones; x1; x2 |]

                checkFit
                    "linear"
                    design
                    y
                    (fitOf design y)
                    linearCoefficients
                    linearStandardErrors
                    linearT
                    linearP
                    linearSummary
                    linearCovariance)

            testCase "regression without a constant against exact fractions" (fun () ->
                let design = designOf [| x1; x2 |]

                checkFit
                    "no constant"
                    design
                    y
                    (fitOf design y)
                    noConstantCoefficients
                    noConstantStandardErrors
                    noConstantT
                    noConstantP
                    noConstantSummary
                    noConstantCovariance)

            testCase "weighted regression against exact fractions" (fun () ->
                let design = designOf [| ones; x1 |]
                let r = expectOk "weighted" (Regression.weighted design y weights)

                checkFit
                    "weighted"
                    design
                    y
                    r
                    weightedCoefficients
                    weightedStandardErrors
                    weightedT
                    weightedP
                    weightedSummary
                    weightedCovariance

                Expect.equal r.DegreesOfFreedom 9.0 "11 positive weights less 2 columns")

            testCase "a constant column of 2 counts as an intercept" (fun () ->
                let a = fitOf (designOf [| ones; x1; x2 |]) y
                let b = fitOf (designOf [| scaleBy 2.0 ones; x1; x2 |]) y
                expectClose 1e-12 0.0 a.RSquared b.RSquared "R squared"
                expectClose 1e-12 0.0 a.AdjustedRSquared b.AdjustedRSquared "adjusted R squared"
                expectClose 1e-12 0.0 (a.Coefficients[0] / 2.0) b.Coefficients[0] "the intercept halves"
                expectClose 1e-12 0.0 a.Coefficients[1] b.Coefficients[1] "x1 coefficient")

            testCase "a design without a constant column takes R squared about zero" (fun () ->
                // Closed form: y = (1, 2, 3, 5) on the column (1, 2, 3, 4): beta = Σxy / Σx² = 34 / 30, RSS = Σy² − 34² / 30
                // with Σy² = 39, so R squared = 1 − RSS / Σy² = 34² / (30 · 39), about zero and not about the mean 2.75.
                let r = fitOf (designOf [| [| 1.0; 2.0; 3.0; 4.0 |] |]) [| 1.0; 2.0; 3.0; 5.0 |]
                expectClose 1e-14 0.0 (34.0 / 30.0) r.Coefficients[0] "coefficient"
                expectClose 1e-14 0.0 (1156.0 / 1170.0) r.RSquared "R squared about zero"
                expectClose 1e-14 0.0 (1.0 - (1.0 - 1156.0 / 1170.0) * 4.0 / 3.0) r.AdjustedRSquared "adjusted, n over n - p")

            testCase "polynomial regression recovers exact coefficients" (fun () ->
                let xs = [| 0.0 .. 7.0 |]
                let ys = Array.map (fun x -> 1.0 + 2.0 * x - 0.5 * x * x) xs
                let r = expectOk "polynomial" (Regression.polynomial 2 xs ys)
                let expected = [| 1.0; 2.0; -0.5 |]

                for j in 0 .. 2 do
                    expectClose 0.0 1e-12 expected[j] r.Coefficients[j] (sprintf "coefficient %d" j)

                for i in 0 .. 7 do
                    expectClose 0.0 1e-12 0.0 r.Residuals[i] (sprintf "residual %d" i)

                expectClose 1e-12 0.0 1.0 r.RSquared "a perfect fit has R squared 1")

            testCase "a perfect fit gives R squared 1, standard errors of 0 and NaN t and p" (fun () ->
                // Columns e1 and e2 of a 3-row design need no reflection, so y = (2, 3, 0) leaves residuals of exactly 0.
                let design = designOf [| [| 1.0; 0.0; 0.0 |]; [| 0.0; 1.0; 0.0 |] |]
                let r = fitOf design [| 2.0; 3.0; 0.0 |]
                Expect.equal r.Coefficients[0] 2.0 "first coefficient"
                Expect.equal r.Coefficients[1] 3.0 "second coefficient"
                Expect.equal r.RSquared 1.0 "R squared"
                Expect.equal r.DegreesOfFreedom 1.0 "degrees of freedom"
                Expect.equal r.ResidualStandardError 0.0 "residual standard error"
                Expect.equal r.StandardErrors[0] 0.0 "first standard error"
                Expect.equal r.StandardErrors[1] 0.0 "second standard error"
                // R's summary.lm calls this an essentially perfect fit: the t statistics and p-values are NaN.
                Expect.isTrue (isNaN r.TStatistics[0]) "t of an essentially perfect fit is NaN"
                Expect.isTrue (isNaN r.PValues[0]) "p-value of an essentially perfect fit is NaN"
                Expect.isTrue (isNaN r.PValues[1]) "second p-value")

            testCase "a zero coefficient with a zero standard error has a NaN t statistic and p-value" (fun () ->
                let design = designOf [| [| 1.0; 0.0; 0.0 |]; [| 0.0; 1.0; 0.0 |] |]
                let r = fitOf design [| 0.0; 3.0; 0.0 |]
                Expect.equal r.StandardErrors[0] 0.0 "standard error"
                Expect.isTrue (isNaN r.TStatistics[0]) "0 / 0"
                Expect.isTrue (isNaN r.PValues[0]) "p-value")

            testCase "a rank-deficient design is an Error" (fun () ->
                expectError "rank-deficient" "x1 twice" (Regression.linear (designOf [| ones; x1; x1 |]) y)
                expectError "rank-deficient" "a zero column" (Regression.linear (designOf [| ones; Array.zeroCreate 12 |]) y)
                let xs = [| 1.0; 2.0; 1.0; 2.0; 1.0; 2.0 |]

                expectError
                    "rank-deficient"
                    "cubic over two x values"
                    (Regression.polynomial 3 xs (Array.map ((*) 2.0) xs))

                expectError
                    "rank-deficient"
                    "weighted, x1 twice"
                    (Regression.weighted (designOf [| ones; x1; x1 |]) y weights)

                // The zero weight leaves two rows with one x value.
                let design = designOf [| [| 1.0; 1.0; 1.0 |]; [| 4.0; 4.0; 9.0 |] |]

                expectError
                    "rank-deficient"
                    "positive weights on one x"
                    (Regression.weighted design [| 1.0; 2.0; 3.0 |] [| 1.0; 1.0; 0.0 |]))

            testCase "fewer positive weights than columns is an Error" (fun () ->
                let design = designOf [| ones; x1; x2 |]
                let w = Array.init 12 (fun i -> if i < 2 then 1.0 else 0.0)
                expectError "positive weights" "two positive weights, three columns" (Regression.weighted design y w))

            testCase "a non-finite input gives NaN fields" (fun () ->
                let design = designOf [| ones; x1 |]
                let withY (v: float) = Array.mapi (fun i e -> if i = 3 then v else e) y

                let allNaN (message: string) (columns: int) (rows: int) (result: Result<LinearRegression, string>) =
                    let r = expectOk message result
                    Expect.equal r.Coefficients.Length columns (message + " coefficient count")
                    Expect.equal r.Residuals.Length rows (message + " residual count")

                    for part, values in
                        [ "coefficient", r.Coefficients
                          "standard error", r.StandardErrors
                          "t", r.TStatistics
                          "p-value", r.PValues
                          "residual", r.Residuals ] do
                        Expect.isTrue (Array.forall isNaN values) (message + ": every " + part + " is NaN")

                    for j in 0 .. columns - 1 do
                        for k in 0 .. columns - 1 do
                            Expect.isTrue (isNaN (Matrix.get r.Covariance j k)) (message + ": covariance is NaN")

                    for part, v in
                        [ "R squared", r.RSquared
                          "adjusted R squared", r.AdjustedRSquared
                          "residual standard error", r.ResidualStandardError
                          "degrees of freedom", r.DegreesOfFreedom ] do
                        Expect.isTrue (isNaN v) (message + ": " + part + " is NaN")

                allNaN "NaN in y" 2 12 (Regression.linear design (withY nan))
                allNaN "infinity in y" 2 12 (Regression.linear design (withY infinity))

                let infiniteDesign =
                    Matrix.init 12 2 (fun i j -> if i = 5 && j = 1 then infinity else 1.0 + float (i * j))

                allNaN "infinity in the design" 2 12 (Regression.linear infiniteDesign y)
                let nanDesign = Matrix.init 12 2 (fun i j -> if i = 5 && j = 0 then nan else 1.0 + float (i * j))
                allNaN "NaN in the design" 2 12 (Regression.linear nanDesign y)
                let nanWeights = Array.mapi (fun i w -> if i = 2 then nan else w) weights
                allNaN "NaN weight" 2 12 (Regression.weighted design y nanWeights)
                let infiniteWeights = Array.mapi (fun i w -> if i = 2 then infinity else w) weights
                allNaN "infinite weight" 2 12 (Regression.weighted design y infiniteWeights)
                allNaN "NaN in polynomial ys" 2 12 (Regression.polynomial 1 x1 (withY nan))

                allNaN
                    "NaN in polynomial xs"
                    2
                    12
                    (Regression.polynomial 1 (Array.mapi (fun i e -> if i = 0 then nan else e) x1) y))

            testCase "NaN comoments give a simple regression of NaN fields" (fun () ->
                let nanX = Array.mapi (fun i e -> if i = 3 then nan else e) x1
                let nanY = Array.mapi (fun i e -> if i = 3 then nan else e) y

                for message, m in [ "NaN in x", comoments nanX y; "NaN in y", comoments x1 nanY ] do
                    let r = Regression.simple m
                    Expect.isTrue (isNaN r.Slope && isNaN r.Intercept) (message + ": line")

                    Expect.isTrue
                        (isNaN r.SlopeStandardError && isNaN r.InterceptStandardError)
                        (message + ": standard errors")

                    Expect.isTrue (isNaN r.RSquared && isNaN r.ResidualStandardError) (message + ": R squared and sigma"))

            testCase "a constant y gives a NaN R squared, linear and weighted" (fun () ->
                // The total sum of squares about the mean of a constant is exactly 0, not a rounding residue.
                let design = designOf [| ones; x1 |]

                for c in [ 2.9; 7.3; 0.7; 1e-3; -4.1; 1e10 ] do
                    let ys = Array.create 12 c
                    let l = fitOf design ys
                    Expect.isTrue (isNaN l.RSquared) (sprintf "linear R squared at %g" c)
                    Expect.isTrue (isNaN l.AdjustedRSquared) (sprintf "linear adjusted R squared at %g" c)
                    let w = expectOk "weighted" (Regression.weighted design ys weights)
                    Expect.isTrue (isNaN w.RSquared) (sprintf "weighted R squared at %g" c)
                    Expect.isTrue (isNaN w.AdjustedRSquared) (sprintf "weighted adjusted R squared at %g" c)
                    // The fit is perfect, so no slope is significant.
                    Expect.isTrue (isNaN w.PValues[1]) (sprintf "weighted slope p-value at %g" c))

            testCase "a dominant weight leaves R squared at the exact fraction" (fun () ->
                // Weights 1e30, 1, ..., 1 on (1, x1): the table "dominant" of the script.
                let design = designOf [| ones; x1 |]
                let w = Array.mapi (fun i _ -> if i = 0 then 1e30 else 1.0) ones
                let r = expectOk "weighted" (Regression.weighted design y w)
                expectClose 1e-9 0.0 dominantSummary[0] r.RSquared "R squared"
                expectClose 1e-9 0.0 dominantSummary[1] r.AdjustedRSquared "adjusted R squared"
                expectClose 1e-9 0.0 dominantCoefficients[0] r.Coefficients[0] "intercept"
                expectClose 1e-9 0.0 dominantCoefficients[1] r.Coefficients[1] "slope")

            testCase "a quadratic fitted to exactly linear data gives NaN t and p, and a noisy fit keeps its t and p" (fun () ->
                // y = 1 + 2x is exactly linear, so the quadratic's residual variance is rounding noise: essentially perfect.
                let xs = [| 1.0 .. 8.0 |]
                let exact = Array.map (fun x -> 1.0 + 2.0 * x) xs
                let r = expectOk "polynomial" (Regression.polynomial 2 xs exact)

                for j in 0..2 do
                    Expect.isTrue (isNaN r.TStatistics[j]) (sprintf "t %d" j)
                    Expect.isTrue (isNaN r.PValues[j]) (sprintf "p-value %d" j)
                    Expect.isFalse (isNaN r.StandardErrors[j]) (sprintf "the standard error %d is kept" j)

                expectClose 0.0 1e-9 1.0 r.Coefficients[0] "intercept"
                expectClose 0.0 1e-9 0.0 r.Coefficients[2] "quadratic coefficient"
                let noisy = fitOf (designOf [| ones; x1; x2 |]) y

                for j in 0..2 do
                    Expect.isFalse (isNaN noisy.TStatistics[j]) (sprintf "noisy t %d" j)
                    Expect.isFalse (isNaN noisy.PValues[j]) (sprintf "noisy p-value %d" j))

            testCase "constant x gives a NaN slope" (fun () ->
                let r = Regression.simple (comoments (Array.create 6 3.0) [| 1.0; 2.0; 4.0; 3.0; 5.0; 9.0 |])
                Expect.isTrue (isNaN r.Slope) "slope"
                Expect.isTrue (isNaN r.Intercept) "intercept"
                Expect.isTrue (isNaN r.SlopeStandardError) "slope standard error"
                Expect.isTrue (isNaN r.InterceptStandardError) "intercept standard error"
                Expect.isTrue (isNaN r.RSquared) "R squared"
                Expect.isTrue (isNaN r.ResidualStandardError) "residual standard error"
                Expect.equal r.DegreesOfFreedom 4.0 "degrees of freedom"
                Expect.equal r.Count 6.0 "count")

            testCase "x emptied back to a constant by removals gives a NaN slope" (fun () ->
                // Pairs at x = 3 plus twelve pairs at x = 3 + k · 1e-14, the latter removed again: M2x keeps a residue
                // that the premise below shows, so Comoments.slope reads a finite number where a fresh fold of the x = 3
                // pairs reads NaN; the residue's variance is under the limit (8 ε · 3)².
                let m = leftAfterRemoving 1e-14
                Expect.equal (Comoments.count m) 5.0 "five pairs remain"
                Expect.isFalse (isNaN (Comoments.slope m)) "the premise: the residue makes Comoments.slope finite"
                let r = Regression.simple m
                Expect.isTrue (isNaN r.Slope) "slope"
                Expect.isTrue (isNaN r.Intercept) "intercept"
                Expect.isTrue (isNaN r.InterceptStandardError) "intercept standard error"
                Expect.equal r.Count 5.0 "count"
                Expect.equal r.DegreesOfFreedom 3.0 "degrees of freedom")

            testCase "a residue left by removals from far away is not detected" (fun () ->
                // The documented limit: twelve pairs at x = 3 + k · 0.1 removed again leave a variance far above the limit
                // (8 ε · 3)², so the slope is the finite number Comoments.slope reads.
                let m = leftAfterRemoving 0.1
                Expect.isFalse (isNaN (Comoments.slope m)) "Comoments.slope is finite"
                Expect.isTrue (Comoments.varianceX m > 1e-20) "the residue is far above the limit"
                expectClose 0.0 0.0 (Comoments.slope m) (Regression.simple m).Slope "the slope is Comoments.slope")

            testCase "simple regression with fewer than three pairs has NaN standard errors" (fun () ->
                let two = Regression.simple (comoments [| 1.0; 3.0 |] [| 2.0; 8.0 |])
                expectClose 1e-14 0.0 3.0 two.Slope "slope through two points"
                expectClose 1e-14 0.0 -1.0 two.Intercept "intercept through two points"
                Expect.isTrue (isNaN two.SlopeStandardError) "slope standard error"
                Expect.isTrue (isNaN two.InterceptStandardError) "intercept standard error"
                Expect.isTrue (isNaN two.ResidualStandardError) "residual standard error"
                Expect.equal two.DegreesOfFreedom 0.0 "degrees of freedom"
                let none = Regression.simple Comoments.zero
                Expect.isTrue (isNaN none.Slope) "empty slope"
                Expect.equal none.Count 0.0 "empty count"
                Expect.equal none.DegreesOfFreedom 0.0 "empty degrees of freedom")

            testCase "a constant y gives a zero slope, zero standard errors and a NaN R squared" (fun () ->
                let r = Regression.simple (comoments x1 (Array.create 12 4.0))
                Expect.equal r.Slope 0.0 "slope"
                expectClose 1e-14 0.0 4.0 r.Intercept "intercept"
                Expect.equal r.ResidualStandardError 0.0 "residual standard error"
                Expect.equal r.SlopeStandardError 0.0 "slope standard error"
                Expect.isTrue (isNaN r.RSquared) "R squared")

            testCase "as many rows as columns gives NaN standard errors" (fun () ->
                // Closed form: [[2, 1], [1, 3]] b = (5, 10) gives b = (1, 3).
                let r = fitOf (designOf [| [| 2.0; 1.0 |]; [| 1.0; 3.0 |] |]) [| 5.0; 10.0 |]
                expectClose 1e-14 0.0 1.0 r.Coefficients[0] "first coefficient"
                expectClose 1e-14 0.0 3.0 r.Coefficients[1] "second coefficient"

                for j in 0..1 do
                    Expect.isTrue (isNaN r.StandardErrors[j]) (sprintf "standard error %d is NaN, not infinite" j)
                    Expect.isTrue (isNaN r.TStatistics[j]) (sprintf "t %d" j)
                    Expect.isTrue (isNaN r.PValues[j]) (sprintf "p-value %d is NaN" j)

                Expect.isTrue (isNaN r.ResidualStandardError) "residual standard error"
                Expect.isTrue (isNaN r.AdjustedRSquared) "adjusted R squared"
                Expect.isTrue (isNaN (Matrix.get r.Covariance 0 1)) "covariance"
                Expect.equal r.DegreesOfFreedom 0.0 "degrees of freedom")

            testCase "arguments out of range raise" (fun () ->
                let design = designOf [| ones; x1 |]
                raisesArgument "y too short" (fun () -> Regression.linear design y[.. 10] |> ignore)
                raisesArgument "y too long" (fun () -> Regression.linear design (Array.append y [| 1.0 |]) |> ignore)

                raisesArgument "fewer rows than columns" (fun () ->
                    Regression.linear (designOf [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |]; [| 5.0; 6.0 |] |]) [| 1.0; 2.0 |]
                    |> ignore)

                raisesArgument "no columns" (fun () ->
                    Regression.linear (Matrix.init 3 0 (fun _ _ -> 0.0)) [| 1.0; 2.0; 3.0 |] |> ignore)

                raisesArgument "weights too short" (fun () -> Regression.weighted design y weights[.. 10] |> ignore)

                raisesArgument "weights too long" (fun () ->
                    Regression.weighted design y (Array.append weights [| 1.0 |]) |> ignore)

                raisesRange "negative weight" (fun () ->
                    Regression.weighted design y (Array.mapi (fun i w -> if i = 4 then -1.0 else w) weights)
                    |> ignore)

                raisesArgument "weighted, fewer rows than columns" (fun () ->
                    Regression.weighted (designOf [| [| 1.0 |]; [| 2.0 |] |]) [| 1.0 |] [| 1.0 |] |> ignore)

                raisesRange "negative degree" (fun () -> Regression.polynomial -1 x1 y |> ignore)
                raisesArgument "xs and ys of different lengths" (fun () -> Regression.polynomial 1 x1 y[.. 10] |> ignore)

                raisesArgument "fewer points than coefficients" (fun () ->
                    Regression.polynomial 3 [| 1.0; 2.0 |] [| 1.0; 2.0 |] |> ignore))

            testCase "a design whose columns differ in scale by 2^80 gives the unscaled fit" (fun () ->
                // The columns (1, x1 · 2^40, x2 · 2^-40) have 2-norms √12, 2^40 √650 and 2^-40 √290. Without equilibration
                // the rank threshold max(m, n) ε (largest norm), about 12 · 2.2e-16 · 2.8e13, exceeds the third column.
                let big = power 40
                let small = power -40
                let plain = fitOf (designOf [| ones; x1; x2 |]) y
                let skewed = designOf [| ones; scaleBy big x1; scaleBy small x2 |]
                let r = fitOf skewed y
                expectClose 1e-12 0.0 plain.RSquared r.RSquared "R squared"
                expectClose 1e-12 0.0 plain.AdjustedRSquared r.AdjustedRSquared "adjusted R squared"
                expectClose 1e-12 0.0 plain.ResidualStandardError r.ResidualStandardError "residual standard error"
                expectClose 1e-12 0.0 plain.Coefficients[0] r.Coefficients[0] "intercept"
                expectClose 1e-12 0.0 (plain.Coefficients[1] * small) r.Coefficients[1] "x1 coefficient scaled by 2^-40"
                expectClose 1e-12 0.0 (plain.Coefficients[2] * big) r.Coefficients[2] "x2 coefficient scaled by 2^40"
                expectClose 1e-12 0.0 plain.StandardErrors[0] r.StandardErrors[0] "intercept standard error"
                expectClose 1e-12 0.0 (plain.StandardErrors[1] * small) r.StandardErrors[1] "x1 standard error"
                expectClose 1e-12 0.0 (plain.StandardErrors[2] * big) r.StandardErrors[2] "x2 standard error"

                expectClose
                    1e-12
                    0.0
                    (Matrix.get plain.Covariance 1 2 * small * big)
                    (Matrix.get r.Covariance 1 2)
                    "covariance of x1 and x2"

                for j in 0..2 do
                    expectClose 1e-12 0.0 plain.PValues[j] r.PValues[j] (sprintf "p-value %d" j)

                for i in 0..11 do
                    expectClose 0.0 1e-12 plain.Residuals[i] r.Residuals[i] (sprintf "residual %d" i)

                // The same through the weighted fit.
                let w = expectOk "weighted" (Regression.weighted skewed y ones)
                expectClose 1e-12 0.0 plain.RSquared w.RSquared "weighted R squared")

            testCase "data at the scale of 1e200 and 1e-200 give the unscaled fit, not an overflow or an underflow" (fun () ->
                // Scaling y by 2^664 (about 1e200) is exact: the coefficients, the standard errors and the residual
                // standard error scale by it, t, p and R squared do not. A sum of squares formed at that scale would
                // overflow to infinity (and report p = 1); the covariance, a square of it, rounds to +infinity there and
                // to 0 at 2^-664.
                let design = designOf [| ones; x1; x2 |]
                let plain = fitOf design y

                for e, covariance in [ 664, infinity; -664, 0.0 ] do
                    let s = power e
                    let r = fitOf design (scaleBy s y)
                    expectClose 1e-12 0.0 plain.RSquared r.RSquared (sprintf "R squared at 2^%d" e)

                    for j in 0..2 do
                        expectClose
                            1e-12
                            0.0
                            (plain.Coefficients[j] * s)
                            r.Coefficients[j]
                            (sprintf "coefficient %d at 2^%d" j e)

                        expectClose
                            1e-12
                            0.0
                            (plain.StandardErrors[j] * s)
                            r.StandardErrors[j]
                            (sprintf "standard error %d at 2^%d" j e)

                        expectClose 1e-12 0.0 plain.TStatistics[j] r.TStatistics[j] (sprintf "t %d at 2^%d" j e)
                        expectClose 1e-12 0.0 plain.PValues[j] r.PValues[j] (sprintf "p-value %d at 2^%d" j e)

                    expectClose
                        1e-12
                        0.0
                        (plain.ResidualStandardError * s)
                        r.ResidualStandardError
                        (sprintf "residual standard error at 2^%d" e)

                    Expect.equal
                        (Matrix.get r.Covariance 0 0)
                        covariance
                        (sprintf "the covariance rounds to %g at 2^%d" covariance e)

                    // The design at the same scale: the coefficients scale by the inverse, t and p do not move.
                    let wide = designOf [| ones; scaleBy s x1; scaleBy s x2 |]
                    let q = fitOf wide y
                    expectClose 1e-12 0.0 plain.PValues[1] q.PValues[1] (sprintf "p-value with the design at 2^%d" e)

                    expectClose
                        1e-12
                        0.0
                        (plain.Coefficients[1] / s)
                        q.Coefficients[1]
                        (sprintf "coefficient with the design at 2^%d" e))

            testCase "weights at 2^1000 and 2^-1000 give the same fit, with the residual standard error rescaled" (fun () ->
                // Scaling every weight by c leaves the coefficients, the covariance, t and p unchanged and scales the
                // residual standard error by sqrt c: sigma^2 = Σ w e^2 / (n − p) scales by c, (X'WX)^-1 by 1 / c.
                let design = designOf [| ones; x1 |]
                let plain = expectOk "weighted" (Regression.weighted design y weights)

                for e in [ 1000; -1000 ] do
                    let r = expectOk "weighted" (Regression.weighted design y (scaleBy (power e) weights))

                    expectClose
                        1e-12
                        0.0
                        (plain.ResidualStandardError * power (e / 2))
                        r.ResidualStandardError
                        (sprintf "residual standard error at 2^%d" e)

                    expectClose 1e-12 0.0 plain.RSquared r.RSquared (sprintf "R squared at 2^%d" e)

                    for j in 0..1 do
                        expectClose 1e-12 0.0 plain.Coefficients[j] r.Coefficients[j] (sprintf "coefficient %d at 2^%d" j e)

                        expectClose
                            1e-12
                            0.0
                            plain.StandardErrors[j]
                            r.StandardErrors[j]
                            (sprintf "standard error %d at 2^%d" j e)

                        expectClose 1e-12 0.0 plain.PValues[j] r.PValues[j] (sprintf "p-value %d at 2^%d" j e))

            testCase "a zero weight drops its row" (fun () ->
                // Identity: weights 1 with one zero equal the unweighted fit of the other rows; the dropped row keeps a residual.
                let rows (v: float[]) = v[.. 10]
                let design = designOf [| ones; x1 |]
                let reduced = designOf [| rows ones; rows x1 |]
                let w = expectOk "weighted" (Regression.weighted design y (Array.append (Array.create 11 1.0) [| 0.0 |]))
                let l = fitOf reduced (rows y)

                for j in 0..1 do
                    expectClose 1e-12 0.0 l.Coefficients[j] w.Coefficients[j] (sprintf "coefficient %d" j)
                    expectClose 1e-12 0.0 l.StandardErrors[j] w.StandardErrors[j] (sprintf "standard error %d" j)

                expectClose 1e-12 0.0 l.RSquared w.RSquared "R squared"
                Expect.equal w.DegreesOfFreedom 9.0 "degrees of freedom"
                Expect.equal w.Residuals.Length 12 "the dropped row keeps a residual"

                expectClose
                    0.0
                    1e-12
                    (y[11] - w.Coefficients[0] - w.Coefficients[1] * x1[11])
                    w.Residuals[11]
                    "residual of the dropped row")

            testCase "weights of 1 equal the unweighted fit" (fun () ->
                let design = designOf [| ones; x1; x2 |]
                let a = fitOf design y
                let b = expectOk "weighted" (Regression.weighted design y ones)

                for j in 0..2 do
                    Expect.equal b.Coefficients[j] a.Coefficients[j] (sprintf "coefficient %d" j)
                    Expect.equal b.StandardErrors[j] a.StandardErrors[j] (sprintf "standard error %d" j))

            testCase "the covariance is symmetric with the squared standard errors on its diagonal" (fun () ->
                let r = fitOf (designOf [| ones; x1; x2 |]) y

                for j in 0..2 do
                    expectClose
                        1e-14
                        0.0
                        (r.StandardErrors[j] * r.StandardErrors[j])
                        (Matrix.get r.Covariance j j)
                        (sprintf "diagonal %d" j)

                    for k in 0..2 do
                        Expect.equal (Matrix.get r.Covariance j k) (Matrix.get r.Covariance k j) (sprintf "symmetry %d %d" j k))

            testCase "simple regression at a scale of 2^332 scales its fields exactly" (fun () ->
                // Scaling x and y by 2^332 (about 1e100) is exact in the comoments: the slope and R squared do not change,
                // the intercept, the standard errors and the residual standard error scale. cov^2 formed first (about
                // 1e400) would overflow.
                let s = power 332
                let plain = Regression.simple (comoments x1 y)
                let r = Regression.simple (comoments (scaleBy s x1) (scaleBy s y))
                expectClose 1e-12 0.0 plain.Slope r.Slope "slope"
                expectClose 1e-12 0.0 plain.RSquared r.RSquared "R squared"
                expectClose 1e-12 0.0 (plain.Intercept * s) r.Intercept "intercept"
                expectClose 1e-12 0.0 plain.SlopeStandardError r.SlopeStandardError "slope standard error"
                expectClose 1e-12 0.0 (plain.InterceptStandardError * s) r.InterceptStandardError "intercept standard error"
                expectClose 1e-12 0.0 (plain.ResidualStandardError * s) r.ResidualStandardError "residual standard error")

            testCase "simple regression at the edges of the spreads Comoments can hold" (fun () ->
                // The comoments store squares: x · 1e155 overflows M2x without compensation and x · 1e-160 leaves a
                // subnormal variance. Each gives NaN fields where it gave a wrong line.
                let unrounded (xs: float[]) (ys: float[]) =
                    Array.fold2 (Comoments.add false) Comoments.zero xs ys

                let allNaN (message: string) (r: SimpleRegression) =
                    Expect.isTrue
                        (isNaN r.Slope
                         && isNaN r.Intercept
                         && isNaN r.SlopeStandardError
                         && isNaN r.InterceptStandardError
                         && isNaN r.RSquared
                         && isNaN r.ResidualStandardError)
                        message

                    Expect.equal r.Count 12.0 (message + ": count")

                allNaN "x · 1e155" (Regression.simple (unrounded (scaleBy 1e155 x1) y))
                allNaN "y · 1e155" (Regression.simple (unrounded x1 (scaleBy 1e155 y)))
                allNaN "x · 1e-160" (Regression.simple (unrounded (scaleBy 1e-160 x1) y))
                allNaN "y · 1e-160" (Regression.simple (unrounded x1 (scaleBy 1e-160 y)))
                // Inside the domain (spreads about 1e150) the line is the scaled one.
                let inside = Regression.simple (unrounded (scaleBy 1e150 x1) (scaleBy 1e150 y))
                let plain = Regression.simple (comoments x1 y)
                expectClose 1e-12 0.0 plain.Slope inside.Slope "slope at 1e150"
                expectClose 1e-12 0.0 plain.RSquared inside.RSquared "R squared at 1e150")

            testCase "curve fitting recovers exact parameters" (fun () ->
                // ys = 2.5 e^(-1.3 x) exactly, so the minimum is at (2.5, 1.3) with zero residuals.
                let xs = Array.init 21 (fun i -> 0.25 * float i)
                let ys = Array.map (fun x -> 2.5 * exp (-1.3 * x)) xs
                let fit = Regression.curve (fun p x -> p[0] * exp (-p[1] * x)) [| 1.0; 1.0 |] xs ys
                expectClose 1e-8 0.0 2.5 fit.Parameters[0] "amplitude"
                expectClose 1e-8 0.0 1.3 fit.Parameters[1] "rate"
                Expect.isTrue fit.Converged "converged"
                Expect.isLessThan fit.ResidualStandardError 1e-8 "residual standard error"
                Expect.equal fit.DegreesOfFreedom 19.0 "degrees of freedom is n - p"
                Expect.isGreaterThan fit.Iterations 0 "iterations")

            testCase "a model linear in its parameters gives the linear regression" (fun () ->
                let xs = [| 1.0 .. 10.0 |]
                let ys = [| 6.0; 5.0; 9.0; 7.0; 13.0; 18.0; 11.0; 15.0; 16.0; 13.0 |]
                let fit = Regression.curve (fun p x -> p[0] + p[1] * x) [| 0.0; 0.0 |] xs ys
                let reference = fitOf (designOf [| Array.create 10 1.0; xs |]) ys
                Expect.isTrue fit.Converged "converged"

                for j in 0..1 do
                    expectClose 1e-8 0.0 reference.Coefficients[j] fit.Parameters[j] (sprintf "parameter %d" j)
                    // The forward-difference Jacobian costs about 1e-8 of relative accuracy in the covariance.
                    expectClose 1e-6 0.0 reference.StandardErrors[j] fit.StandardErrors[j] (sprintf "standard error %d" j)

                    for k in 0..1 do
                        expectClose
                            1e-6
                            0.0
                            (Matrix.get reference.Covariance j k)
                            (Matrix.get fit.Covariance j k)
                            (sprintf "covariance %d %d" j k)

                expectClose 1e-8 0.0 reference.ResidualStandardError fit.ResidualStandardError "residual standard error"
                Expect.equal fit.DegreesOfFreedom reference.DegreesOfFreedom "degrees of freedom is n - 2 (identity)"
                Expect.equal fit.DegreesOfFreedom 8.0 "n - 2")

            testCase "curve arguments out of range raise" (fun () ->
                let line (p: float[]) (x: float) = p[0] + p[1] * x

                raisesArgument "xs and ys of different lengths" (fun () ->
                    Regression.curve line [| 0.0; 0.0 |] [| 1.0; 2.0; 3.0 |] [| 1.0; 2.0 |] |> ignore)

                raisesArgument "fewer points than parameters" (fun () ->
                    Regression.curve line [| 0.0; 0.0 |] [| 1.0 |] [| 1.0 |] |> ignore)

                raisesArgument "no parameters" (fun () ->
                    Regression.curve (fun _ x -> x) [||] [| 1.0; 2.0 |] [| 1.0; 2.0 |] |> ignore))

            testCase "a NaN or infinite observation gives NaN fields and raises nothing" (fun () ->
                let line (p: float[]) (x: float) = p[0] + p[1] * x
                let xs = [| 1.0; 2.0; 3.0; 4.0 |]
                let ys = [| 2.0; 4.0; 5.0; 9.0 |]

                let cases =
                    [|
                        "NaN x", Array.mapi (fun i v -> if i = 1 then nan else v) xs, ys
                        "infinite x", Array.mapi (fun i v -> if i = 2 then infinity else v) xs, ys
                        "NaN y", xs, Array.mapi (fun i v -> if i = 0 then nan else v) ys
                        "infinite y", xs, Array.mapi (fun i v -> if i = 3 then -infinity else v) ys
                    |]

                for (name, badX, badY) in cases do
                    let fit = Regression.curve line [| 0.0; 0.0 |] badX badY
                    Expect.isTrue (Array.forall isNaN fit.Parameters) (name + ": parameters")
                    Expect.isTrue (Array.forall isNaN fit.StandardErrors) (name + ": standard errors")
                    Expect.isTrue (isNaN (Matrix.get fit.Covariance 0 1)) (name + ": covariance")
                    Expect.isTrue (isNaN fit.ResidualStandardError) (name + ": residual standard error")
                    Expect.isFalse fit.Converged (name + ": converged")
                    Expect.equal fit.Iterations 0 (name + ": iterations")
                    Expect.equal fit.DegreesOfFreedom 2.0 (name + ": degrees of freedom"))

            testCase "as many points as parameters fit exactly with NaN errors" (fun () ->
                let fit = Regression.curve (fun p x -> p[0] + p[1] * x) [| 0.0; 0.0 |] [| 1.0; 3.0 |] [| 5.0; 11.0 |]
                Expect.equal fit.DegreesOfFreedom 0.0 "degrees of freedom"
                expectClose 1e-8 0.0 2.0 fit.Parameters[0] "intercept"
                expectClose 1e-8 0.0 3.0 fit.Parameters[1] "slope"
                Expect.isTrue (isNaN fit.ResidualStandardError) "residual standard error"
                Expect.isTrue (Array.forall isNaN fit.StandardErrors) "standard errors"
                Expect.isTrue (isNaN (Matrix.get fit.Covariance 1 1)) "covariance")

            testCase "a parameter the model ignores gives NaN errors and keeps the others" (fun () ->
                // The second Jacobian column is exactly 0, so R has a zero on its diagonal: no covariance exists.
                let xs = [| 1.0 .. 6.0 |]
                let ys = [| 3.1; 4.9; 7.2; 8.8; 11.1; 13.0 |]
                let fit = Regression.curve (fun p x -> p[0] * x + 0.0 * p[1]) [| 1.0; 1.0 |] xs ys
                Expect.isTrue (isNaN fit.StandardErrors[0] && isNaN fit.StandardErrors[1]) "standard errors"
                Expect.isTrue (isNaN (Matrix.get fit.Covariance 0 0)) "covariance"
                Expect.isFalse (isNaN fit.ResidualStandardError) "residual standard error is still defined"
                // Least squares through the origin: Σxy / Σx² (the normal equation of one parameter).
                let slope = Array.sum (Array.map2 (*) xs ys) / Array.sumBy (fun x -> x * x) xs
                expectClose 1e-6 0.0 slope fit.Parameters[0] "the slope through the origin")

            testCase "a Jacobian that is not finite gives NaN errors and keeps the residual standard error" (fun () ->
                // The model is NaN for any p above its start, so every forward-difference column is NaN, while the
                // residuals at the start are finite: the fit stops there, short of convergence.
                let fit =
                    Regression.curve
                        (fun p x -> if p[0] > 1.0 then nan else p[0] * x)
                        [| 1.0 |]
                        [| 1.0; 2.0; 3.0 |]
                        [| 2.0; 3.0; 7.0 |]

                Expect.isFalse fit.Converged "converged"
                Expect.equal fit.Iterations 0 "iterations"
                Expect.equal fit.Parameters [| 1.0 |] "parameters stay at the start"
                Expect.isTrue (isNaN fit.StandardErrors[0]) "standard error"
                Expect.isTrue (isNaN (Matrix.get fit.Covariance 0 0)) "covariance"
                // Residuals 1, 1, 4: cost 9, σ = √(2 · 9 / 2) = 3.
                expectClose 1e-14 0.0 3.0 fit.ResidualStandardError "residual standard error")

            testCase "residuals whose squares leave the double range give NaN errors and no convergence" (fun () ->
                let line (p: float[]) (x: float) = p[0] * x
                let xs = [| 1.0 .. 8.0 |]
                let shape = [| 0.3; -0.2; 0.5; -0.4; 0.1; 0.2; -0.3; 0.4 |]
                let noisy (scale: float) = Array.mapi (fun i x -> scale * (2.0 * x + shape[i])) xs

                let allNaN (message: string) (fit: CurveFit) =
                    Expect.isTrue (isNaN fit.ResidualStandardError) (message + ": residual standard error")
                    Expect.isTrue (isNaN fit.StandardErrors[0]) (message + ": standard error")
                    Expect.isTrue (isNaN (Matrix.get fit.Covariance 0 0)) (message + ": covariance")
                    Expect.isFalse fit.Converged (message + ": converged")

                // Squares of 1e200 overflow: the cost is infinite and no step lowers it.
                allNaN "1e200" (Regression.curve line [| 1.0 |] xs (noisy 1e200))
                // From the start p = 2e-170, the residuals are about 1e-171 and their squares underflow: the cost reads 0
                // with non-zero residuals.
                allNaN "1e-170" (Regression.curve line [| 2e-170 |] xs (noisy 1e-170))
                // A model that is NaN at the start has no cost at all.
                allNaN "NaN model" (Regression.curve (fun _ _ -> nan) [| 1.0 |] xs (noisy 1.0)))

            testCase "columns scaled by 2^15 and 2^-15 give the unscaled fit with the errors rescaled" (fun () ->
                // The model p0 · 2^-15 + p1 · x · 2^15 is the line with p0 and p1 multiplied by 2^15 and 2^-15 (powers of
                // two, exact), so its parameters and standard errors are the line's times 2^15 and 2^-15 and its
                // covariance entry the line's (the factors cancel). The Jacobian columns differ in scale by 2^30.
                let xs = [| 1.0 .. 10.0 |]
                let ys = [| 6.0; 5.0; 9.0; 7.0; 13.0; 18.0; 11.0; 15.0; 16.0; 13.0 |]
                let low = power -15
                let high = power 15
                let plain = Regression.curve (fun p x -> p[0] + p[1] * x) [| 1.0; 1.0 |] xs ys

                let scaled =
                    Regression.curve (fun p x -> p[0] * low + p[1] * (x * high)) [| high; low |] xs ys

                Expect.isTrue (plain.Converged && scaled.Converged) "converged"
                expectClose 1e-6 0.0 plain.Parameters[0] (scaled.Parameters[0] * low) "intercept"
                expectClose 1e-6 0.0 plain.Parameters[1] (scaled.Parameters[1] * high) "slope"
                expectClose 1e-6 0.0 plain.StandardErrors[0] (scaled.StandardErrors[0] * low) "intercept standard error"
                expectClose 1e-6 0.0 plain.StandardErrors[1] (scaled.StandardErrors[1] * high) "slope standard error"
                expectClose 1e-6 0.0 (Matrix.get plain.Covariance 0 1) (Matrix.get scaled.Covariance 0 1) "covariance"
                expectClose 1e-6 0.0 plain.ResidualStandardError scaled.ResidualStandardError "residual standard error")
        ]
