// Reference values: R's p.adjust definitions, worked by hand beside each expected array (Holm: step-down running
// maximum of (m - i + 1) p(i); Hochberg: step-up running minimum of the same; Benjamini–Hochberg: step-up running
// minimum of m / i * p(i); Benjamini–Yekutieli: the same times the sum of 1 / i; all capped at 1). The definitions
// are from documentation: lines 40 to 82 of R's src/library/stats/R/p.adjust.R, read here on 2026-11-21 from
// https://raw.githubusercontent.com/wch/r-source/trunk/src/library/stats/R/p.adjust.R.
module Ranvier.Science.Tests.AdjustTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

let private p = [| 0.04; 0.001; 0.03; 0.2; 0.01 |]

let private methods: (string * (float[] -> float[]))[] =
    [|
        "bonferroni", Adjust.bonferroni
        "holm", Adjust.holm
        "hochberg", Adjust.hochberg
        "benjaminiHochberg", Adjust.benjaminiHochberg
        "benjaminiYekutieli", Adjust.benjaminiYekutieli
    |]

let private close (name: string) (expected: float[]) (actual: float[]) =
    Expect.equal actual.Length expected.Length (name + " length")

    for i in 0 .. expected.Length - 1 do
        if Double.IsNaN expected[i] then
            Expect.isTrue (Double.IsNaN actual[i]) (name + " " + string i + " is NaN")
        else
            Expect.isLessThanOrEqual (ulps expected[i] actual[i]) 4.0 (name + " " + string i + ": " + string actual[i])

[<Tests>]
let tests =
    testList
        "Adjust"
        [
            testCase "Bonferroni" (fun () ->
                close "bonferroni" [| 0.2; 0.005; 0.15; 1.0; 0.05 |] (Adjust.bonferroni p))

            // ascending 0.001 0.01 0.03 0.04 0.2: terms 5*0.001, 4*0.01, 3*0.03, 2*0.04, 1*0.2 = 0.005 0.04 0.09 0.08 0.2;
            // running maximum 0.005 0.04 0.09 0.09 0.2
            testCase "Holm" (fun () -> close "holm" [| 0.09; 0.005; 0.09; 0.2; 0.04 |] (Adjust.holm p))

            // the same terms; running minimum from the top 0.005 0.04 0.08 0.08 0.2
            testCase "Hochberg" (fun () -> close "hochberg" [| 0.08; 0.005; 0.08; 0.2; 0.04 |] (Adjust.hochberg p))

            // terms 5/i * p(i) = 0.005 0.025 0.05 0.05 0.2; running minimum from the top is the same
            testCase "Benjamini–Hochberg" (fun () ->
                close "bh" [| 0.05; 0.005; 0.05; 0.2; 0.025 |] (Adjust.benjaminiHochberg p))

            testCase "Benjamini–Yekutieli" (fun () ->
                // 1 + 1/2 + 1/3 + 1/4 + 1/5 = 137/60
                let expected = [| 0.05; 0.005; 0.05; 0.2; 0.025 |] |> Array.map (fun x -> x * (137.0 / 60.0))
                close "by" expected (Adjust.benjaminiYekutieli p))

            testCase "tied p-values give tied results, in every order of the input" (fun () ->
                // ascending 0.02 0.02 0.5, m = 3; Holm terms 0.06 0.04 0.5, running maximum 0.06 0.06 0.5
                close "holm" [| 0.06; 0.06; 0.5 |] (Adjust.holm [| 0.02; 0.02; 0.5 |])
                close "holm" [| 0.5; 0.06; 0.06 |] (Adjust.holm [| 0.5; 0.02; 0.02 |])
                // Hochberg terms 0.06 0.04 0.5, running minimum from the top 0.04 0.04 0.5
                close "hochberg" [| 0.04; 0.5; 0.04 |] (Adjust.hochberg [| 0.02; 0.5; 0.02 |])
                // Benjamini–Hochberg terms 3/i * p(i) = 0.06 0.03 0.5, running minimum from the top 0.03 0.03 0.5
                close "bh" [| 0.03; 0.5; 0.03 |] (Adjust.benjaminiHochberg [| 0.02; 0.5; 0.02 |])
                close "bonferroni" [| 0.06; 0.06; 1.0 |] (Adjust.bonferroni [| 0.02; 0.02; 0.5 |]))

            testCase "the cap at 1 and the step boundaries" (fun () ->
                // ascending 0.4 0.5 0.9, m = 3: Holm terms 1.2 1.0 0.9, running maximum 1.2 1.2 1.2, capped at 1
                let q = [| 0.4; 0.5; 0.9 |]
                close "holm" [| 1.0; 1.0; 1.0 |] (Adjust.holm q)
                // Hochberg terms 1.2 1.0 0.9, running minimum from the top 0.9 0.9 0.9
                close "hochberg" [| 0.9; 0.9; 0.9 |] (Adjust.hochberg q)
                // Benjamini–Hochberg terms 3/i * p(i) = 1.2 0.75 0.9, running minimum from the top 0.75 0.75 0.9
                close "bh" [| 0.75; 0.75; 0.9 |] (Adjust.benjaminiHochberg q)
                close "bonferroni" [| 1.0; 1.0; 1.0 |] (Adjust.bonferroni q)
                // Benjamini–Yekutieli: the BH terms times 11/6 are 2.2 1.375 1.65, running minimum 1.375 1.375 1.65, capped
                close "by" [| 1.0; 1.0; 1.0 |] (Adjust.benjaminiYekutieli q))

            testCase "one p-value is unchanged by every method" (fun () ->
                for (name, f) in methods do
                    close name [| 0.3 |] (f [| 0.3 |]))

            testCase "adjusted p-values are ordered as the raw ones and never below them" (fun () ->
                let mutable g = Generator.create 9

                let raw =
                    Array.init 200 (fun _ ->
                        let struct (u, next) = Generator.uniform g
                        g <- next
                        u)

                let byRaw = Array.sortBy (fun i -> raw[i]) [| 0 .. raw.Length - 1 |]

                for (name, f) in methods do
                    let adjusted = f raw

                    for i in 0 .. raw.Length - 1 do
                        Expect.isLessThanOrEqual raw[i] adjusted[i] (name + " is not below the raw value")
                        Expect.isLessThanOrEqual adjusted[i] 1.0 (name + " is at most 1")

                    for k in 1 .. byRaw.Length - 1 do
                        Expect.isLessThanOrEqual adjusted[byRaw[k - 1]] adjusted[byRaw[k]] (name + " keeps the order"))

            testCase "p-values of 0 and 1" (fun () ->
                for (name, f) in methods do
                    let a = f [| 0.0; 1.0; 0.5 |]
                    Expect.equal a[0] 0.0 (name + " keeps 0")
                    Expect.equal a[1] 1.0 (name + " keeps 1"))

            testCase "NaN entries stay NaN and are not counted" (fun () ->
                close "bonferroni" [| 0.02; nan; 0.08 |] (Adjust.bonferroni [| 0.01; nan; 0.04 |])
                // m = 2, ascending 0.01 0.04: Holm and Hochberg terms 2*0.01 and 1*0.04
                close "holm" [| 0.04; nan; 0.02 |] (Adjust.holm [| 0.04; nan; 0.01 |])
                close "hochberg" [| 0.04; nan; 0.02 |] (Adjust.hochberg [| 0.04; nan; 0.01 |])
                // Benjamini–Hochberg terms 2/1 * 0.01 and 2/2 * 0.04
                close "bh" [| 0.04; nan; 0.02 |] (Adjust.benjaminiHochberg [| 0.04; nan; 0.01 |])
                // Benjamini–Yekutieli multiplies by 1 + 1/2
                close "by" [| 0.06; nan; 0.03 |] (Adjust.benjaminiYekutieli [| 0.04; nan; 0.01 |])

                for (name, f) in methods do
                    close name [| nan; nan |] (f [| nan; nan |]))

            testCase "the empty family" (fun () ->
                for (name, f) in methods do
                    Expect.isEmpty (f [||]) name)

            testCase "p-values outside [0, 1] raise" (fun () ->
                for (name, f) in methods do
                    for bad in [| [| -0.1 |]; [| 1.5 |]; [| 0.2; infinity |]; [| 0.2; -0.0001 |] |] do
                        Expect.throwsT<ArgumentOutOfRangeException>
                            (fun () -> f bad |> ignore)
                            (name + " " + string bad.Length + " " + string bad[bad.Length - 1]))
        ]
