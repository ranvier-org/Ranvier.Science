// Reference values: closed forms computed by hand from the definition named in each test; the bin-edge rules are those
// of numpy 2.x histogram_bin_edges (from documentation:
// https://numpy.org/doc/stable/reference/generated/numpy.histogram_bin_edges.html).
module Ranvier.Science.Tests.DescribeTests

open System
open Expecto
open Ranvier.Statistics

let private close (tolerance: float) (actual: float) (expected: float) (message: string) : unit =
    Expect.isLessThanOrEqual
        (abs (actual - expected))
        tolerance
        (message + ": " + string actual + " against " + string expected)

let private closeAll (tolerance: float) (actual: float[]) (expected: float[]) (message: string) : unit =
    Expect.equal actual.Length expected.Length (message + " length")

    for i in 0 .. expected.Length - 1 do
        close tolerance actual[i] expected[i] (message + " at " + string i)

let private isNaN (x: float) : bool = Double.IsNaN x

// NumPy's linspace: lo + k * step, with the last edge exactly hi.
let private edges (lo: float) (hi: float) (bins: int) : float[] =
    Array.init (bins + 1) (fun k -> if k = bins then hi else lo + float k * ((hi - lo) / float bins))

[<Tests>]
let tests =
    testList
        "Describe"
        [
            testCase "quantile type 7" (fun () ->
                // h = (n - 1) p over the sorted 1, 3, 4, 7, 10: (1 - f) x[j] + f x[j + 1].
                let xs = [| 10.0; 1.0; 4.0; 7.0; 3.0 |]

                for p, expected in [ 0.0, 1.0; 1.0, 10.0; 0.5, 4.0; 0.25, 3.0; 0.1, 1.8; 0.9, 8.8 ] do
                    close 1e-15 (Describe.quantile p xs) expected ("p " + string p)

                Expect.equal xs [| 10.0; 1.0; 4.0; 7.0; 3.0 |] "xs is unchanged")

            testCase "quantiles and quantileSorted agree with quantile" (fun () ->
                let xs = [| 10.0; 1.0; 4.0; 7.0; 3.0 |]
                let ps = [| 0.1; 0.5; 0.9 |]
                let sorted = Array.sort xs
                let many = Describe.quantiles ps xs

                for i in 0 .. ps.Length - 1 do
                    Expect.equal many[i] (Describe.quantile ps[i] xs) "quantiles"
                    Expect.equal (Describe.quantileSorted ps[i] sorted) (Describe.quantile ps[i] xs) "quantileSorted")

            testCase "median, interquartile range and median absolute deviation" (fun () ->
                // Sorted 1, 2, 3, 4, 100: median 3; quartiles 2 and 4; |x - 3| = 2, 1, 0, 1, 97 has median 1.
                let xs = [| 1.0; 2.0; 3.0; 4.0; 100.0 |]
                Expect.equal (Describe.median xs) 3.0 "median"
                Expect.equal (Describe.interquartileRange xs) 2.0 "IQR"
                Expect.equal (Describe.medianAbsoluteDeviation xs) 1.0 "MAD")

            testCase "trimmed mean" (fun () ->
                // 1..9 and 100, n = 10: cutting 1 each end leaves 2..9 (mean 5.5); cutting 2 leaves 3..8 (mean 5.5);
                // cutting 0 gives (45 + 100) / 10.
                let xs = Array.append [| 1.0 .. 9.0 |] [| 100.0 |]
                Expect.equal (Describe.trimmedMean 0.1 xs) 5.5 "0.1"
                Expect.equal (Describe.trimmedMean 0.25 xs) 5.5 "0.25"
                Expect.equal (Describe.trimmedMean 0.0 xs) 14.5 "0"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Describe.trimmedMean 0.5 xs |> ignore) "0.5"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Describe.trimmedMean -0.1 xs |> ignore) "-0.1"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Describe.trimmedMean nan xs |> ignore) "NaN"
                Expect.isTrue (isNaN (Describe.trimmedMean 0.1 [||])) "an empty array gives NaN")

            testCase "ranks with ties" (fun () ->
                // The average of the 1-based positions a tied group occupies in sorted order.
                Expect.equal
                    (Describe.ranks [| 3.0; 1.0; 4.0; 1.0; 5.0; 9.0; 2.0; 6.0; 5.0 |])
                    [| 4.0; 1.5; 5.0; 1.5; 6.5; 9.0; 3.0; 8.0; 6.5 |]
                    "ranks"

                Expect.isEmpty (Describe.ranks [||]) "empty"
                Expect.throwsT<ArgumentException> (fun () -> Describe.ranks [| 1.0; nan |] |> ignore) "NaN")

            testCase "autocorrelation" (fun () ->
                // 1..5: mean 3, sum of squares 10; lag 1 sum (-2)(-1) + (-1)(0) + 0(1) + 1(2) = 4;
                // lag 2 sum (-2)(0) + (-1)(1) + 0(2) = -1.
                closeAll 1e-15 (Describe.autocorrelation 2 [| 1.0 .. 5.0 |]) [| 1.0; 0.4; -0.1 |] "1..5"
                Expect.all (Describe.autocorrelation 2 [| 2.0; 2.0; 2.0; 2.0 |]) isNaN "constant series"
                // 0.1 is not exact, so the computed mean differs from it; the rule is equality with the first value.
                Expect.all (Describe.autocorrelation 2 [| 0.1; 0.1; 0.1 |]) isNaN "constant series, inexact mean"
                Expect.all (Describe.autocorrelation 2 [| 1.0; 2.0; nan; 4.0; 5.0 |]) isNaN "a NaN in the series"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Describe.autocorrelation -1 [| 1.0 .. 5.0 |] |> ignore)
                    "-1"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Describe.autocorrelation 5 [| 1.0 .. 5.0 |] |> ignore)
                    "n")

            testCase "summary" (fun () ->
                // Mean 40 / 8 = 5; squared deviations sum to 32, so the sample variance is 32 / 7.
                // Sorted, h = 1.75, 3.5, 5.25 give 4, 4.5, 5.5.
                let s = Describe.summary [| 2.0; 4.0; 4.0; 4.0; 5.0; 5.0; 7.0; 9.0 |]
                Expect.equal s.Count 8 "count"
                Expect.equal s.Mean 5.0 "mean"
                close 1e-15 s.StandardDeviation (sqrt (32.0 / 7.0)) "standard deviation"
                Expect.equal s.Minimum 2.0 "minimum"
                Expect.equal s.LowerQuartile 4.0 "lower quartile"
                Expect.equal s.Median 4.5 "median"
                Expect.equal s.UpperQuartile 5.5 "upper quartile"
                Expect.equal s.Maximum 9.0 "maximum")

            testCase "summary of an empty array and of one item" (fun () ->
                let e = Describe.summary [||]
                Expect.equal e.Count 0 "empty count"

                Expect.all
                    [ e.Mean; e.StandardDeviation; e.Minimum; e.LowerQuartile; e.Median; e.UpperQuartile; e.Maximum ]
                    isNaN
                    "empty fields"

                let one = Describe.summary [| 3.5 |]
                Expect.equal one.Count 1 "one count"
                Expect.isTrue (isNaN one.StandardDeviation) "one standard deviation"

                Expect.all
                    [ one.Mean; one.Minimum; one.LowerQuartile; one.Median; one.UpperQuartile; one.Maximum ]
                    (fun x -> x = 3.5)
                    "one fields")

            testCase "NaN in the data" (fun () ->
                let xs = [| 1.0; nan; 3.0 |]
                Expect.isTrue (isNaN (Describe.quantile 0.5 xs)) "quantile"
                Expect.isTrue (isNaN (Describe.median xs)) "median"
                Expect.isTrue (isNaN (Describe.interquartileRange xs)) "IQR"
                Expect.isTrue (isNaN (Describe.medianAbsoluteDeviation xs)) "MAD"
                Expect.isTrue (isNaN (Describe.trimmedMean 0.1 xs)) "trimmed mean"
                Expect.all (Describe.quantiles [| 0.2; 0.8 |] xs) isNaN "quantiles"
                let s = Describe.summary xs
                Expect.equal s.Count 3 "count"

                Expect.all
                    [ s.Mean; s.StandardDeviation; s.Minimum; s.LowerQuartile; s.Median; s.UpperQuartile; s.Maximum ]
                    isNaN
                    "fields")

            testCase "quantile of an empty array is NaN" (fun () ->
                Expect.isTrue (isNaN (Describe.quantile 0.5 [||])) "empty"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Describe.quantile 1.5 [| 1.0 |] |> ignore)
                    "p = 1.5"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Describe.quantile -0.5 [| 1.0 |] |> ignore)
                    "p = -0.5"

                Expect.isTrue (isNaN (Describe.quantile nan [| 1.0; 2.0 |])) "p = NaN")

            testCase "bin edges follow NumPy's rules" (fun () ->
                // Sturges: ceil (log2 100 + 1) = 8. Scott: (24 sqrt pi / 100)^(1/3) * 28.866 = 21.71 and
                // ceil (99 / 21.71) = 5. Freedman-Diaconis: IQR 49.5, 2 * 49.5 / 100^(1/3) = 21.33, ceil (99 / 21.33) = 5.
                // Count 4 is linspace.
                let xs = Array.init 100 float
                Expect.equal (Describe.binEdges Sturges xs) (edges 0.0 99.0 8) "Sturges"
                Expect.equal (Describe.binEdges Scott xs) (edges 0.0 99.0 5) "Scott"
                Expect.equal (Describe.binEdges FreedmanDiaconis xs) (edges 0.0 99.0 5) "Freedman-Diaconis"
                Expect.equal (Describe.binEdges (Count 4) xs) [| 0.0; 24.75; 49.5; 74.25; 99.0 |] "Count 4"

                // NumPy takes width = range / (log2 n + 1) and then ceil (range / width): for n = 64 (7) and range 37.1
                // that quotient rounds above 7, so NumPy gives 8 bins where ceil (log2 64 + 1) is 7.
                let sixtyFour = Array.init 64 (fun i -> if i = 63 then 37.1 else float i * 0.5)
                Expect.equal (Describe.binEdges Sturges sixtyFour).Length 9 "Sturges at 64, range 37.1"

                // log2 128 + 1 = 8 exactly, so 8 bins and 9 edges.
                Expect.equal (Describe.binEdges Sturges (Array.init 128 float)).Length 9 "Sturges at 128"

                // NumPy's Freedman-Diaconis width is 2 · IQR · n^(-1/3). For 0..25 and 130 (n = 27, IQR 13, range 130)
                // that is 2 · 13 · 0.33333333333333337 = 8.666666666666668 and 130 over it 14.999999999999998: 15 bins.
                // The form 2 · IQR / n^(1/3) gives 26 / 3 = 8.666666666666666 and 16 bins (Python 3.14's pow, by
                // python -I: 27 ** (-1/3) and 27 ** (1/3); in real arithmetic the quotient is exactly 15).
                let fd = Array.init 27 (fun i -> if i = 26 then 130.0 else float i)
                Expect.equal (Describe.binEdges FreedmanDiaconis fd).Length 16 "Freedman-Diaconis at 27, range 130")

            testCase "a width rule that asks for too many bins raises" (fun () ->
                // 99 values within 1e-7 of 0 and one at 1000: the IQR is about 5e-8, so the width is about 2e-8 and
                // 1000 / width is about 5e10 bins, over the limit of 16777216.
                let xs = Array.init 100 (fun i -> if i = 99 then 1000.0 else float i * 1e-9)
                Expect.throwsT<ArgumentException> (fun () -> Describe.binEdges FreedmanDiaconis xs |> ignore) "FD")

            testCase "bin edges of constant data and bad input" (fun () ->
                let c = [| 2.0; 2.0; 2.0 |]

                for rule in [ Scott; FreedmanDiaconis; Sturges ] do
                    Expect.equal (Describe.binEdges rule c) [| 1.5; 2.5 |] (string rule)

                Expect.equal (Describe.binEdges (Count 2) c) (edges 1.5 2.5 2) "Count 2"
                Expect.throwsT<ArgumentException> (fun () -> Describe.binEdges Sturges [||] |> ignore) "empty"
                Expect.throwsT<ArgumentException> (fun () -> Describe.binEdges Sturges [| 1.0; nan |] |> ignore) "NaN"

                Expect.throwsT<ArgumentException>
                    (fun () -> Describe.binEdges Scott [| 1.0; infinity |] |> ignore)
                    "infinity"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Describe.binEdges (Count 0) [| 1.0; 2.0 |] |> ignore)
                    "Count 0")
        ]
