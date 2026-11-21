// Reference values: the identities and closed forms written in each test (the bandwidths are those of R's bw.nrd0 and
// bw.nrd, bandwidths.R in wch/r-source trunk, src/library/stats/R, lines 23-37: from documentation), the normal
// density, and the plug-in standard error of a mean. No committed script is needed: every expected value is an
// expression in the test.
module Ranvier.Science.Tests.ResamplingTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private isNaN (v: float) : bool = Double.IsNaN v

let private sample: float[] =
    let struct (xs, _) = Generator.normals 10.0 2.0 200 (Generator.create 3)
    xs

let private mean (xs: float[]) : float = Array.sum xs / float xs.Length

// The standard error of the sample mean, sqrt (population variance / n): the value the bootstrap converges to.
let private plugInSe (xs: float[]) : float =
    let m = mean xs
    sqrt ((xs |> Array.sumBy (fun x -> (x - m) * (x - m))) / float xs.Length / float xs.Length)

let private raisesArgument (message: string) (f: unit -> unit) : unit = Expect.throwsT<ArgumentException> f message

let private raisesOutOfRange (message: string) (f: unit -> unit) : unit =
    Expect.throwsT<ArgumentOutOfRangeException> f message

// The normal density with mean 2 and deviation 0.5; the parentheses matter: prefix minus binds before **.
let private normalDensity (x: float) : float =
    exp (-(((x - 2.0) / 0.5) ** 2.0) / 2.0) / (0.5 * sqrt (2.0 * Math.PI))

[<Tests>]
let tests =
    testList
        "Resampling"
        [
            testCase "the bootstrap standard error of the mean" (fun () ->
                let se = plugInSe sample
                let struct (boot, _) = Bootstrap.standardError 2000 Array.average sample (Generator.create 4)
                Expect.isTrue (abs (boot - se) <= 0.1 * se) (sprintf "bootstrap %g against plug-in %g" boot se))

            testCase "the percentile interval of the mean" (fun () ->
                let se = plugInSe sample
                let m = mean sample
                let struct (ci, _) = Bootstrap.percentile 0.05 2000 Array.average sample (Generator.create 4)
                Expect.isTrue (abs (ci.Lower - (m - 1.96 * se)) <= 0.35 * se) (sprintf "lower %g" ci.Lower)
                Expect.isTrue (abs (ci.Upper - (m + 1.96 * se)) <= 0.35 * se) (sprintf "upper %g" ci.Upper))

            testCase "the same generator gives the same result" (fun () ->
                let g = Generator.create 9
                let struct (a, ga) = Bootstrap.percentile 0.1 300 Array.average sample g
                let struct (b, gb) = Bootstrap.percentile 0.1 300 Array.average sample g
                Expect.equal a b "equal intervals"
                Expect.equal ga gb "equal returned generators"
                Expect.notEqual ga g "the returned generator moved on"
                let struct (e1, ge1) = Bootstrap.standardError 300 Array.average sample g
                let struct (e2, ge2) = Bootstrap.standardError 300 Array.average sample g
                Expect.equal e1 e2 "equal standard errors"
                Expect.equal ge1 ge2 "equal returned generators"
                Expect.notEqual ge1 g "the returned generator moved on")

            testCase "the statistic sees resamples of the sample" (fun () ->
                let seen = ResizeArray<float[]>()

                let record (xs: float[]) : float =
                    seen.Add xs
                    xs[0]

                Bootstrap.percentile 0.1 50 record sample (Generator.create 5) |> ignore
                Expect.equal seen.Count 50 "one call per resample"
                let members = Set.ofArray sample

                for r in seen do
                    Expect.equal r.Length 200 "length of the sample"
                    Expect.isTrue (r |> Array.forall (fun v -> members.Contains v)) "every element belongs to the sample"

                // Each resample is a fresh array: a statistic that keeps its argument sees it unchanged afterwards.
                for i in 0 .. seen.Count - 1 do
                    for j in i + 1 .. seen.Count - 1 do
                        Expect.isFalse (LanguagePrimitives.PhysicalEquality seen[i] seen[j]) "distinct arrays"

                Expect.notEqual seen[0] seen[1] "different draws"
                seen.Clear()
                Bootstrap.standardError 7 record sample (Generator.create 5) |> ignore
                Expect.equal seen.Count 7 "one call per resample for standardError")

            testCase "the bootstrap standard error of statistics with a large offset" (fun () ->
                // The resample means of offset + i · 1e-3, offset 1e9: each recorded mean minus 1e9 is exact (Sterbenz),
                // so the spread of the differences is the reference. A one-pass fold of the means can lose up to about
                // ε · 1e9 / sd relative, 1e-4 here; it read 2.9e-7 off, more than this test allows, at f8a6caf.
                let xs = Array.init 50 (fun i -> 1e9 + float i * 1e-3)
                let recorded = ResizeArray<float>()

                let recordMean (resample: float[]) : float =
                    let m = Array.average resample
                    recorded.Add m
                    m

                let struct (se, _) = Bootstrap.standardError 400 recordMean xs (Generator.create 6)
                let differences = recorded.ToArray() |> Array.map (fun m -> m - 1e9)
                let m = Array.fold (Moments.add true) Moments.zero differences
                expectClose 1e-12 0.0 (Moments.sampleStandardDeviation m) se "standard error")

            testCase "a constant sample gives a zero-width interval at the constant" (fun () ->
                // 0.1 three times has a mean that rounds (0.30000000000000004 / 3), so the deviations are not 0.
                let struct (three, _) = Bootstrap.standardError 50 Array.average [| 0.1; 0.1; 0.1 |] (Generator.create 1)
                Expect.equal three 0.0 "standard error of 0.1 three times"

                for c in [ 0.5; 0.1 ] do
                    let xs = Array.create 200 c
                    // Every resample equals the sample, so the bounds are the statistic of the sample itself.
                    let expected = Array.average xs
                    let struct (ci, _) = Bootstrap.percentile 0.05 100 Array.average xs (Generator.create 1)
                    Expect.equal ci.Lower expected "lower"
                    Expect.equal ci.Upper expected "upper"
                    let struct (se, _) = Bootstrap.standardError 100 Array.average xs (Generator.create 1)
                    Expect.equal se 0.0 "standard error")

            testCase "a statistic that is NaN gives NaN results" (fun () ->
                let mutable calls = 0

                let sometimes (xs: float[]) : float =
                    calls <- calls + 1
                    if calls = 3 then nan else xs[0]

                let struct (ci, _) = Bootstrap.percentile 0.1 20 sometimes sample (Generator.create 2)
                Expect.isTrue (isNaN ci.Lower && isNaN ci.Upper) "NaN interval"
                calls <- 0
                let struct (se, _) = Bootstrap.standardError 20 sometimes sample (Generator.create 2)
                Expect.isTrue (isNaN se) "NaN standard error")

            testCase "bootstrap arguments out of range raise" (fun () ->
                let g = Generator.create 1
                let stat = Array.average

                raisesOutOfRange "percentile resamples 0" (fun () ->
                    Bootstrap.percentile 0.05 0 stat sample g |> ignore)

                raisesOutOfRange "standardError resamples 1" (fun () ->
                    Bootstrap.standardError 1 stat sample g |> ignore)

                raisesOutOfRange "standardError resamples 0" (fun () ->
                    Bootstrap.standardError 0 stat sample g |> ignore)

                for a in [ 0.0; 1.0; nan; -0.1; 1.5 ] do
                    raisesOutOfRange (sprintf "alpha %g" a) (fun () ->
                        Bootstrap.percentile a 10 stat sample g |> ignore)

                raisesArgument "percentile of an empty sample" (fun () ->
                    Bootstrap.percentile 0.05 10 stat [||] g |> ignore)

                raisesArgument "standardError of an empty sample" (fun () ->
                    Bootstrap.standardError 10 stat [||] g |> ignore))

            testCase "Silverman and Scott bandwidths" (fun () ->
                let xs = [| 1.0 .. 10.0 |]
                // s = sqrt (55/6) and the type-7 quartiles 3.25 and 7.75, so the IQR is 4.5 (R: bw.nrd0, bw.nrd).
                let expected (factor: float) = factor * min (sqrt (55.0 / 6.0)) (4.5 / 1.34) * 10.0 ** -0.2
                expectClose 1e-14 0.0 (expected 0.9) (Density.silvermanBandwidth xs) "Silverman"
                expectClose 1e-14 0.0 (expected 1.06) (Density.scottBandwidth xs) "Scott"

                // The other branch of the minimum: n = 9, quartiles 0 and 1 (positions 2 and 6), so IQR/1.34 is
                // far below s.
                let heavy = [| 0.0; 0.0; 0.0; 0.0; 1.0; 1.0; 1.0; 1.0; 100.0 |]
                Expect.equal (Describe.interquartileRange heavy) 1.0 "the IQR"
                expectClose 1e-14 0.0 (0.9 * (1.0 / 1.34) * 9.0 ** -0.2) (Density.silvermanBandwidth heavy) "Silverman, IQR branch"
                expectClose 1e-14 0.0 (1.06 * (1.0 / 1.34) * 9.0 ** -0.2) (Density.scottBandwidth heavy) "Scott, IQR branch")

            testCase "Silverman's fallbacks at zero spread" (fun () ->
                // R's bw.nrd0: the minimum is 0, so s, then |x[1]|, then 1.
                expectClose 1e-14 0.0 (0.9 * 5.0 * 4.0 ** -0.2) (Density.silvermanBandwidth [| 5.0; 5.0; 5.0; 5.0 |]) "constant 5"
                expectClose 1e-14 0.0 (0.9 * 5.0 * 4.0 ** -0.2) (Density.silvermanBandwidth [| -5.0; -5.0; -5.0; -5.0 |]) "constant -5 uses |x1|"
                expectClose 1e-14 0.0 (0.9 * 4.0 ** -0.2) (Density.silvermanBandwidth [| 0.0; 0.0; 0.0; 0.0 |]) "zeros"
                Expect.equal (Density.scottBandwidth [| 5.0; 5.0; 5.0; 5.0 |]) 0.0 "Scott of a constant"
                Expect.equal (Density.scottBandwidth [| 0.0; 0.0; 0.0; 0.0 |]) 0.0 "Scott of zeros"
                // A constant whose mean rounds: s is still exactly 0, so Silverman falls back to |x1| and Scott is 0.
                expectClose 1e-14 0.0 (0.9 * 0.1 * 3.0 ** -0.2) (Density.silvermanBandwidth [| 0.1; 0.1; 0.1 |]) "constant 0.1"
                Expect.equal (Density.scottBandwidth [| 0.1; 0.1; 0.1 |]) 0.0 "Scott of a constant 0.1"
                // The IQR is 0 but s > 0: nine zeros and a 10 have mean 1, variance 90/9 = 10, so s = sqrt 10.
                let spike = Array.append (Array.zeroCreate 9) [| 10.0 |]
                Expect.equal (Describe.interquartileRange spike) 0.0 "the IQR is 0"
                expectClose 1e-14 0.0 (0.9 * sqrt 10.0 * 10.0 ** -0.2) (Density.silvermanBandwidth spike) "Silverman falls back to s"
                Expect.equal (Density.scottBandwidth spike) 0.0 "Scott has no fallback")

            testCase "bandwidths of fewer than two observations raise" (fun () ->
                raisesArgument "Silverman of one point" (fun () -> Density.silvermanBandwidth [| 3.0 |] |> ignore)
                raisesArgument "Scott of one point" (fun () -> Density.scottBandwidth [| 3.0 |] |> ignore)
                raisesArgument "Silverman of none" (fun () -> Density.silvermanBandwidth [||] |> ignore)
                raisesArgument "Scott of none" (fun () -> Density.scottBandwidth [||] |> ignore))

            testCase "a NaN or infinity in the data gives NaN bandwidths" (fun () ->
                for xs in [ [| 1.0; nan; 3.0 |]; [| 1.0; infinity; 3.0 |]; [| 1.0; -infinity; 3.0 |] ] do
                    Expect.isTrue (isNaN (Density.silvermanBandwidth xs)) "Silverman"
                    Expect.isTrue (isNaN (Density.scottBandwidth xs)) "Scott")

            testCase "bandwidths scale with the data without overflow or underflow" (fun () ->
                let xs = [| 1.0 .. 10.0 |]
                let silver = Density.silvermanBandwidth xs
                let scott = Density.scottBandwidth xs

                // Scaling the data by c scales the bandwidth by c (an identity). At 1e-300 the squared deviations
                // underflow to 0 if the variance is computed unscaled.
                for c in [ 1e300; 1e-300; 1e-150 ] do
                    let scaled = xs |> Array.map (fun x -> x * c)
                    expectClose 1e-13 0.0 (silver * c) (Density.silvermanBandwidth scaled) (sprintf "Silverman at %g" c)
                    expectClose 1e-13 0.0 (scott * c) (Density.scottBandwidth scaled) (sprintf "Scott at %g" c)

                // The IQR is 3e308 here, and the sum of squares overflows, if computed unscaled.
                let wide = [| -1.5e308; 1.5e308; -1.5e308; 1.5e308 |]
                let unitData = [| -1.0; 1.0; -1.0; 1.0 |]
                let w = Density.silvermanBandwidth wide
                Expect.isFalse (Double.IsInfinity w || isNaN w) "finite"
                expectClose 1e-13 0.0 (Density.silvermanBandwidth unitData * 1.5e308) w "Silverman at 1.5e308"
                expectClose 1e-13 0.0 (Density.scottBandwidth unitData * 1.5e308) (Density.scottBandwidth wide) "Scott at 1.5e308"
                // The constant-sample fallback at the extremes.
                expectClose 1e-14 0.0 (0.9 * 1e-300 * 4.0 ** -0.2) (Density.silvermanBandwidth (Array.create 4 1e-300)) "constant 1e-300"
                expectClose 1e-14 0.0 (0.9 * 1e300 * 4.0 ** -0.2) (Density.silvermanBandwidth (Array.create 4 1e300)) "constant 1e300")

            testCase "bandwidths of offset data match exact rational arithmetic" (fun () ->
                // python -I tests/reference/special.py resampling: the exact bandwidths of the 50 doubles
                // offset + i * spread (rationals and 60-digit decimals, rounded once). Scaling by a power of two is
                // exact, so each is within 1e-13 relative; a scale that rounds each value loses of the order of
                // ε · offset / s relative, from about 1e-5 to 1e-2 on the last three rows (general knowledge, not
                // measured here).
                let cases: struct (float * float * float * float)[] =
                    [|
                        struct (1.0, 0.001, 0.0059996784578804415, 0.007066287961503631)
                        struct (1000000000.0, 0.001, 0.005999678643355716, 0.007066288179952288)
                        struct (1000000000.0, 1e-06, 6.000046719992836e-06, 7.066721692436007e-06)
                        struct (1000000.0, 1e-09, 5.999614148126862e-09, 7.0662122189049706e-09)
                    |]

                for struct (offset, spread, silverman, scott) in cases do
                    let xs = Array.init 50 (fun i -> offset + float i * spread)
                    let label = sprintf "offset %g spread %g" offset spread
                    expectClose 1e-13 0.0 silverman (Density.silvermanBandwidth xs) ("Silverman, " + label)
                    expectClose 1e-13 0.0 scott (Density.scottBandwidth xs) ("Scott, " + label))

            testCase "seeded resamples are equal on both targets" (fun () ->
                // The sample is the integers 0 to 199 in the order i * 37 mod 200 (37 is coprime to 200): exact on every
                // platform, with no exp, log or trigonometry, so the literals do not depend on the libm. The bootstrap
                // itself uses Generator.integer (exact), type-7 quantiles, sums and a square root. The literals were
                // printed on .NET (net10.0, Release, Windows) for 500 resamples and Generator.create 4, the standard
                // error again after its fold became the corrected two-pass formula (it moved by one ulp); the mocha run
                // must reproduce them bit for bit.
                let exact = Array.init 200 (fun i -> float ((i * 37) % 200))
                let struct (ci, _) = Bootstrap.percentile 0.05 500 Array.average exact (Generator.create 4)
                Expect.equal ci.Lower 91.055125 "lower bound"
                Expect.equal ci.Upper 107.255 "upper bound"
                let struct (se, _) = Bootstrap.standardError 500 Array.average exact (Generator.create 4)
                Expect.equal se 4.240561452445176 "standard error")

            testCase "the estimate of one point is a normal density" (fun () ->
                let f = Density.gaussianKernel 0.5 [| 2.0 |]

                for x in [ 2.0; 2.7; -1.0 ] do
                    expectClose 1e-14 0.0 (normalDensity x) (f x) (sprintf "x = %g" x))

            testCase "the estimate averages the kernels of its points" (fun () ->
                let pts = [| 0.0; 1.0; 3.0 |]
                let f = Density.gaussianKernel 0.7 pts

                for x in [ -1.0; 0.4; 2.0 ] do
                    let expected =
                        (pts |> Array.sumBy (fun p -> exp (-(((x - p) / 0.7) ** 2.0) / 2.0) / (0.7 * sqrt (2.0 * Math.PI))))
                        / 3.0

                    expectClose 1e-14 0.0 expected (f x) (sprintf "x = %g" x))

            testCase "the estimate integrates to 1" (fun () ->
                let h = Density.silvermanBandwidth sample
                let f = Density.gaussianKernel h sample
                let lo = Array.min sample - 10.0 * h
                let hi = Array.max sample + 10.0 * h
                let r = Integrate.adaptive 1e-12 1e-10 f lo hi
                Expect.isTrue r.Converged "converged"
                expectClose 0.0 1e-9 1.0 r.Value "integral")

            testCase "the estimate copies its sample" (fun () ->
                let xs = [| 1.0; 2.0; 4.0 |]
                let f = Density.gaussianKernel 0.5 xs
                let before = [ 0.0; 1.5; 4.0 ] |> List.map f
                xs[0] <- 100.0
                xs[2] <- -50.0
                Expect.equal ([ 0.0; 1.5; 4.0 ] |> List.map f) before "unchanged")

            testCase "density arguments out of range raise" (fun () ->
                for h in [ 0.0; -1.0; nan; infinity ] do
                    raisesOutOfRange (sprintf "bandwidth %g" h) (fun () ->
                        Density.gaussianKernel h [| 1.0 |] |> ignore)

                raisesArgument "an empty sample" (fun () -> Density.gaussianKernel 1.0 [||] |> ignore)

                // A NaN in the data gives a NaN estimate and raises nothing.
                let f = Density.gaussianKernel 1.0 [| 1.0; nan |]
                Expect.isTrue (isNaN (f 1.0)) "NaN estimate")

            testCase "the estimate at extreme scales is exact or honestly zero" (fun () ->
                // One point at 1e300 with bandwidth 1e299: the peak is 1/(1e299 sqrt (2 pi)), a closed form.
                let big = Density.gaussianKernel 1e299 [| 1e300 |]
                expectClose 1e-14 0.0 (1.0 / (1e299 * sqrt (2.0 * Math.PI))) (big 1e300) "peak at 1e300"
                expectClose 1e-13 0.0 (exp -0.5 / (1e299 * sqrt (2.0 * Math.PI))) (big 1.1e300) "one deviation at 1e300"
                // Tiny scales: the peak 1/(1e-300 sqrt (2 pi)) is 3.99e299, finite.
                let tiny = Density.gaussianKernel 1e-300 [| 1e-300 |]
                expectClose 1e-14 0.0 (1.0 / (1e-300 * sqrt (2.0 * Math.PI))) (tiny 1e-300) "peak at 1e-300"
                // x - xi overflows (2e308): the true value is below 1e-307, so a zero is the honest answer.
                let far = Density.gaussianKernel 1e308 [| -1e308 |]
                let v = far 1e308
                Expect.isFalse (isNaN v) "not NaN"
                Expect.isTrue (v >= 0.0 && v < 1e-307) "below 1e-307"
                // A subnormal bandwidth whose peak is finite: 1 / h overflows but 1 / (h sqrt (2 pi)) = 9.97e307.
                let subnormal = Density.gaussianKernel 4e-309 [| 0.0 |]
                expectClose 1e-9 0.0 (1.0 / (4e-309 * sqrt (2.0 * Math.PI))) (subnormal 0.0) "peak at h = 4e-309"
                // A peak above the largest double is infinity, not a finite wrong number.
                let narrow = Density.gaussianKernel 1e-310 [| 0.0 |]
                Expect.equal (narrow 0.0) infinity "overflowing peak")
        ]
