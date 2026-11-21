// Reference values: the tables below, printed by
//   python -I tests/reference/special.py correlation
// (Pearson's r and t from exact fractions, Spearman's from exact average ranks, Kendall's tau and z from a count over
// all pairs, the tails in decimal arithmetic), and the identities and counts written in each test.
// Data: x = 1..8, y = 2 1 4 3 7 8 6 5; the tied pair x = 1 2 2 3 4 5 5 6, y = 1 3 2 2 5 4 6 6.
module Ranvier.Science.Tests.CorrelationTests

open System
open Expecto
open Ranvier.Statistics

// python -I tests/reference/special.py correlation
// each row: the correlation, the statistic (t on n - 2 degrees of freedom, or z for Kendall), then the Less,
// Greater and TwoSided p-values
let private pearsonCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.7380952380952381, 2.6796590924604065, 0.9817236194735696, 0.01827638052643042, 0.03655276105286084)
    |]

let private spearmanTiedCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.8902439024390244, 4.787528601222856, 0.9984803522157556, 0.00151964778424441, 0.00303929556848882)
    |]

let private kendallCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.5, 1.7320508075688772, 0.9583677416682248, 0.0416322583317752, 0.0832645166635504)
    |]

let private kendallTiedCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.7692307692307693, 2.5508006671194883, 0.99462621120977, 0.005373788790230038, 0.010747577580460075)
    |]

let private x: float[] = [| 1.0 .. 8.0 |]
let private y: float[] = [| 2.0; 1.0; 4.0; 3.0; 7.0; 8.0; 6.0; 5.0 |]
let private tiedX: float[] = [| 1.0; 2.0; 2.0; 3.0; 4.0; 5.0; 5.0; 6.0 |]
let private tiedY: float[] = [| 1.0; 3.0; 2.0; 2.0; 5.0; 4.0; 6.0; 6.0 |]

let private isNaN (v: float) : bool = Double.IsNaN v

let private alternatives: (Alternative * string)[] =
    [| Less, "Less"; Greater, "Greater"; TwoSided, "TwoSided" |]

// The p-value of the row's column for an alternative: Less, Greater, TwoSided are columns 2, 3, 4.
let private expectedP (struct (_, _, less, greater, two): struct (float * float * float * float * float)) (a: Alternative) =
    match a with
    | Less -> less
    | Greater -> greater
    | TwoSided -> two

// The definition of Kendall's tau-b over every pair: (concordant − discordant, n0 − n1, n0 − n2), with n1 and n2 the
// pairs tied in x and in y. A pair tied in either is neither concordant nor discordant.
let private countPairs (xs: float[]) (ys: float[]) : struct (float * float * float) =
    let mutable s = 0.0
    let mutable n0 = 0.0
    let mutable n1 = 0.0
    let mutable n2 = 0.0

    for i in 0 .. xs.Length - 1 do
        for j in i + 1 .. xs.Length - 1 do
            n0 <- n0 + 1.0

            if xs[i] = xs[j] then
                n1 <- n1 + 1.0

            if ys[i] = ys[j] then
                n2 <- n2 + 1.0

            let product = (xs[i] - xs[j]) * (ys[i] - ys[j])

            if product > 0.0 then
                s <- s + 1.0
            elif product < 0.0 then
                s <- s - 1.0

    struct (s, n0 - n1, n0 - n2)

// R's tie-corrected variance of S, written from stats/R/cor.test.R lines 113 to 131 (read as the raw file at trunk).
let private varianceOfS (xs: float[]) (ys: float[]) : float =
    let n = float xs.Length
    let groups (values: float[]) = Array.countBy id values |> Array.map (fun (_, c) -> float c)
    let tx = groups xs
    let ty = groups ys
    let sum (f: float -> float) (ts: float[]) = Array.sumBy f ts
    let v0 = n * (n - 1.0) * (2.0 * n + 5.0)
    let vt = sum (fun t -> t * (t - 1.0) * (2.0 * t + 5.0)) tx
    let vu = sum (fun t -> t * (t - 1.0) * (2.0 * t + 5.0)) ty
    let v1 = sum (fun t -> t * (t - 1.0)) tx * sum (fun t -> t * (t - 1.0)) ty
    let v2 = sum (fun t -> t * (t - 1.0) * (t - 2.0)) tx * sum (fun t -> t * (t - 1.0) * (t - 2.0)) ty
    let third = if n > 2.0 then v2 / (9.0 * n * (n - 1.0) * (n - 2.0)) else 0.0
    (v0 - vt - vu) / 18.0 + v1 / (2.0 * n * (n - 1.0)) + third

let private raisesArgument (message: string) (f: unit -> unit) : unit =
    Expect.throwsT<ArgumentException> f message

let private allNaN (message: string) (r: TestResult) : unit =
    Expect.isTrue (isNaN r.Statistic) (message + ": statistic")
    Expect.isTrue (isNaN r.DegreesOfFreedom) (message + ": degrees of freedom")
    Expect.isTrue (isNaN r.DenominatorDegreesOfFreedom) (message + ": denominator degrees of freedom")
    Expect.isTrue (isNaN r.PValue) (message + ": p-value")

// A small deterministic generator (the linear congruence 75 s + 74 mod 65537, no System.Random under Fable).
let private congruence (count: int) (modulus: int) (seed: int) : float[] =
    let mutable s = seed
    [|
        for _ in 1..count do
            s <- (s * 75 + 74) % 65537
            float (s % modulus)
    |]

[<Tests>]
let tests =
    testList
        "Correlation"
        [
            testCase "Pearson, Spearman and Kendall against exact fractions" (fun () ->
                let struct (r, t, _, _, _) = pearsonCases[0]
                expectClose 1e-12 0.0 r (Correlation.pearson x y) "Pearson r"
                // x and y are permutations of 1..8, so their ranks are their values (identity): Spearman equals Pearson.
                expectClose 1e-12 0.0 r (Correlation.spearman x y) "Spearman r on untied data"
                let struct (tau, z, _, _, _) = kendallCases[0]
                expectClose 1e-12 0.0 tau (Correlation.kendall x y) "Kendall tau"

                for (a, name) in alternatives do
                    let p = Correlation.pearsonTest a x y
                    expectClose 1e-12 0.0 t p.Statistic (name + " Pearson t")
                    Expect.equal p.DegreesOfFreedom 6.0 (name + " Pearson degrees of freedom is n - 2")
                    expectClose 1e-12 0.0 (expectedP pearsonCases[0] a) p.PValue (name + " Pearson p-value")
                    let s = Correlation.spearmanTest a x y
                    expectClose 1e-12 0.0 t s.Statistic (name + " Spearman t on untied data")
                    expectClose 1e-12 0.0 (expectedP pearsonCases[0] a) s.PValue (name + " Spearman p-value on untied data")
                    let k = Correlation.kendallTest a x y
                    expectClose 1e-12 0.0 z k.Statistic (name + " Kendall z")
                    expectClose 1e-12 0.0 (expectedP kendallCases[0] a) k.PValue (name + " Kendall p-value")
                    Expect.isTrue (isNaN k.DegreesOfFreedom && isNaN k.DenominatorDegreesOfFreedom) (name + " Kendall has no degrees of freedom"))

            testCase "Spearman with ties against the reference" (fun () ->
                let struct (r, t, _, _, _) = spearmanTiedCases[0]
                // python -I tests/reference/special.py correlation: the Spearman r, from exact average ranks.
                expectClose 1e-12 0.0 r (Correlation.spearman tiedX tiedY) "Spearman r with ties"

                for (a, name) in alternatives do
                    let s = Correlation.spearmanTest a tiedX tiedY
                    expectClose 1e-12 0.0 t s.Statistic (name + " Spearman t with ties")
                    Expect.equal s.DegreesOfFreedom 6.0 (name + " degrees of freedom")
                    expectClose 1e-12 0.0 (expectedP spearmanTiedCases[0] a) s.PValue (name + " Spearman p-value with ties")

                // Identical ranks by definition: a monotone, non-linear relation has Spearman r = 1.
                let cube = Array.map (fun v -> v * v * v) [| 1.0 .. 6.0 |]
                expectClose 1e-14 0.0 1.0 (Correlation.spearman [| 1.0 .. 6.0 |] cube) "monotone y = x^3"
                expectClose 1e-14 0.0 -1.0 (Correlation.spearman [| 1.0 .. 6.0 |] (Array.rev cube)) "reversed")

            testCase "Kendall tau-b matches a direct count" (fun () ->
                for (name, xs, ys) in [| "untied", x, y; "tied", tiedX, tiedY |] do
                    let struct (s, d1, d2) = countPairs xs ys
                    expectClose 1e-14 0.0 (s / sqrt (d1 * d2)) (Correlation.kendall xs ys) (name + " tau-b")

                // 7 concordant and 3 discordant pairs among the 10 of (1..5, 3 1 2 5 4), counted by hand: (7 - 3) / 10.
                Expect.equal (Correlation.kendall [| 1.0 .. 5.0 |] [| 3.0; 1.0; 2.0; 5.0; 4.0 |]) 0.4 "hand count"

                let struct (tiedTau, tiedZ, _, _, _) = kendallTiedCases[0]
                expectClose 1e-12 0.0 tiedTau (Correlation.kendall tiedX tiedY) "tau-b with ties against the reference"

                for (a, name) in alternatives do
                    let k = Correlation.kendallTest a tiedX tiedY
                    expectClose 1e-12 0.0 tiedZ k.Statistic (name + " z with ties")
                    expectClose 1e-12 0.0 (expectedP kendallTiedCases[0] a) k.PValue (name + " p-value with ties"))

            testCase "Kendall over generated samples with ties matches the direct count and variance" (fun () ->
                // Sizes 2 to 40 exercise every run length of the merge sort, ties in x, in y and in both.
                for n in 2..40 do
                    let xs = congruence n 7 n
                    let ys = congruence n 9 (n + 100)
                    let struct (s, d1, d2) = countPairs xs ys
                    let tau = Correlation.kendall xs ys

                    if d1 = 0.0 || d2 = 0.0 then
                        Expect.isTrue (isNaN tau) (sprintf "n = %d: all x or all y tied" n)
                    else
                        expectClose 1e-14 0.0 (s / sqrt (d1 * d2)) tau (sprintf "n = %d tau-b" n)
                        let k = Correlation.kendallTest TwoSided xs ys

                        if n < 4 then
                            allNaN (sprintf "n = %d test" n) k
                        else
                            expectClose 1e-12 0.0 (s / sqrt (varianceOfS xs ys)) k.Statistic (sprintf "n = %d z" n))

            testCase "perfect correlation gives an infinite t and a p-value of 0" (fun () ->
                // Exactly linear data give r = 1 or a value a few ulps below it: the integers 1 to 5 against 2x + 1
                // give 0.9999999999999998 (a finite t of about 1e8), the integers 1 to 8 give 1 (an infinite t).
                let five = [| 1.0 .. 5.0 |]
                expectClose 1e-15 0.0 1.0 (Correlation.pearson five (Array.map (fun v -> 2.0 * v + 1.0) five)) "r on 5 points"
                let up = [| 1.0 .. 8.0 |]
                let line = Array.map (fun v -> 2.0 * v + 1.0) up
                Expect.equal (Correlation.pearson up line) 1.0 "r"
                Expect.equal (Correlation.pearson up (Array.rev line)) -1.0 "r reversed"
                let t = Correlation.pearsonTest TwoSided up line
                Expect.equal t.Statistic infinity "t"
                Expect.equal t.DegreesOfFreedom 6.0 "degrees of freedom"
                Expect.equal t.PValue 0.0 "two-sided p-value"
                Expect.equal (Correlation.pearsonTest Greater up line).PValue 0.0 "Greater"
                Expect.equal (Correlation.pearsonTest Less up line).PValue 1.0 "Less"
                let down = Correlation.pearsonTest Less up (Array.rev line)
                Expect.equal down.Statistic -infinity "t reversed"
                Expect.equal down.PValue 0.0 "Less reversed"
                Expect.equal (Correlation.spearmanTest TwoSided up line).Statistic infinity "Spearman t")

            testCase "tests on two and three points" (fun () ->
                // Two points give r = ±1 but no degrees of freedom: the t tests are NaN. Kendall's normal approximation
                // gives p = 0.317 at n = 2 and 0.117 at n = 3 for perfect agreement, where the exact values are 1 and
                // 1/3 (all 2 and 6 orderings counted), so kendallTest is NaN below 4 pairs.
                expectClose 1e-15 0.0 1.0 (Correlation.pearson [| 1.0; 2.0 |] [| 5.0; 9.0 |]) "r"
                allNaN "Pearson" (Correlation.pearsonTest TwoSided [| 1.0; 2.0 |] [| 5.0; 9.0 |])
                allNaN "Spearman" (Correlation.spearmanTest TwoSided [| 1.0; 2.0 |] [| 5.0; 9.0 |])
                allNaN "Kendall, 2 pairs" (Correlation.kendallTest TwoSided [| 1.0; 2.0 |] [| 5.0; 9.0 |])
                expectClose 1e-15 0.0 1.0 (Correlation.kendall [| 1.0; 2.0 |] [| 5.0; 9.0 |]) "tau at 2 pairs"
                allNaN "Kendall, 3 pairs" (Correlation.kendallTest TwoSided [| 1.0; 2.0; 3.0 |] [| 5.0; 9.0; 10.0 |])
                // From 3 pairs the t tests exist (1 degree of freedom), and from 4 Kendall's.
                Expect.equal (Correlation.pearsonTest TwoSided [| 1.0; 2.0; 3.0 |] [| 5.0; 9.0; 4.0 |]).DegreesOfFreedom 1.0 "3 pairs"
                Expect.isFalse (isNaN (Correlation.kendallTest TwoSided [| 1.0; 2.0; 3.0; 4.0 |] [| 5.0; 9.0; 4.0; 6.0 |]).Statistic) "4 pairs")

            testCase "Pearson at the scales of 1e200 and 1e-200" (fun () ->
                // r does not change when a sample is multiplied by a positive number, but the squares in the comoments
                // would overflow at 1e200 and underflow at 1e-200 without the power-of-two scaling.
                let plain = Correlation.pearson x y
                let scaleBy (f: float) (xs: float[]) = Array.map (fun v -> v * f) xs

                for (name, fx, fy) in [| "1e200, 1e-200", 1e200, 1e-200; "1e-200, 1e200", 1e-200, 1e200; "1e200, 1e200", 1e200, 1e200 |] do
                    expectClose 1e-12 0.0 plain (Correlation.pearson (scaleBy fx x) (scaleBy fy y)) (name + ": pearson")
                    let t = Correlation.pearsonTest TwoSided (scaleBy fx x) (scaleBy fy y)
                    expectClose 1e-12 0.0 (Correlation.pearsonTest TwoSided x y).Statistic t.Statistic (name + ": t"))

            testCase "degenerate inputs" (fun () ->
                let constant = Array.create 6 3.0
                let varying = [| 1.0; 4.0; 2.0; 8.0; 5.0; 7.0 |]

                for (name, xs, ys) in [| "constant x", constant, varying; "constant y", varying, constant; "both constant", constant, constant |] do
                    Expect.isTrue (isNaN (Correlation.pearson xs ys)) (name + ": pearson")
                    Expect.isTrue (isNaN (Correlation.spearman xs ys)) (name + ": spearman")
                    Expect.isTrue (isNaN (Correlation.kendall xs ys)) (name + ": kendall")

                    for (a, aName) in alternatives do
                        allNaN (name + " " + aName + " pearsonTest") (Correlation.pearsonTest a xs ys)
                        allNaN (name + " " + aName + " spearmanTest") (Correlation.spearmanTest a xs ys)
                        allNaN (name + " " + aName + " kendallTest") (Correlation.kendallTest a xs ys)

                // Fewer than 2 points.
                for (name, xs, ys) in [| "empty", [||], [||]; "one point", [| 1.0 |], [| 2.0 |] |] do
                    Expect.isTrue (isNaN (Correlation.pearson xs ys)) (name + ": pearson")
                    Expect.isTrue (isNaN (Correlation.spearman xs ys)) (name + ": spearman")
                    Expect.isTrue (isNaN (Correlation.kendall xs ys)) (name + ": kendall")
                    allNaN (name + " pearsonTest") (Correlation.pearsonTest TwoSided xs ys)
                    allNaN (name + " spearmanTest") (Correlation.spearmanTest TwoSided xs ys)
                    allNaN (name + " kendallTest") (Correlation.kendallTest TwoSided xs ys)

                // A NaN or an infinity anywhere gives NaN, and raises nothing (Describe.ranks raises on NaN, so it is checked first).
                let bad (v: float) = Array.mapi (fun i e -> if i = 3 then v else e) varying

                for (name, xs, ys) in
                    [|
                        "NaN in x", bad nan, varying
                        "NaN in y", varying, bad nan
                        "infinity in x", bad infinity, varying
                        "infinity in y", varying, bad -infinity
                    |] do
                    Expect.isTrue (isNaN (Correlation.pearson xs ys)) (name + ": pearson")
                    Expect.isTrue (isNaN (Correlation.spearman xs ys)) (name + ": spearman")
                    Expect.isTrue (isNaN (Correlation.kendall xs ys)) (name + ": kendall")
                    allNaN (name + " pearsonTest") (Correlation.pearsonTest TwoSided xs ys)
                    allNaN (name + " spearmanTest") (Correlation.spearmanTest TwoSided xs ys)
                    allNaN (name + " kendallTest") (Correlation.kendallTest TwoSided xs ys)

                // Lengths that differ raise.
                let short = varying[.. 4]
                raisesArgument "pearson" (fun () -> Correlation.pearson varying short |> ignore)
                raisesArgument "spearman" (fun () -> Correlation.spearman varying short |> ignore)
                raisesArgument "kendall" (fun () -> Correlation.kendall varying short |> ignore)
                raisesArgument "pearsonTest" (fun () -> Correlation.pearsonTest TwoSided varying short |> ignore)
                raisesArgument "spearmanTest" (fun () -> Correlation.spearmanTest TwoSided varying short |> ignore)
                raisesArgument "kendallTest" (fun () -> Correlation.kendallTest TwoSided varying short |> ignore))
        ]
