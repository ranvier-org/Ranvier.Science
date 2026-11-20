module Ranvier.Science.Tests.GeneratorTests

open Expecto
open Ranvier.Numerics

let private uniforms (count: int) (g0: Generator) : float[] =
    let out = Array.zeroCreate count
    let mutable g = g0

    for i in 0 .. count - 1 do
        let struct (u, next) = Generator.uniform g
        out[i] <- u
        g <- next

    out

let private normalMoments (count: int) (g0: Generator) : Moments =
    let mutable g = g0
    let mutable m = Moments.zero

    for _ in 1..count do
        let struct (z, next) = Generator.normal g
        m <- Moments.add true m z
        g <- next

    m

let private sample (count: int) (draw: Generator -> struct ('T * Generator)) (g0: Generator) : 'T[] =
    let out = Array.zeroCreate count
    let mutable g = g0

    for i in 0 .. count - 1 do
        let struct (x, next) = draw g
        out[i] <- x
        g <- next

    out

/// The Kolmogorov-Smirnov statistic of the sample against a distribution function.
let private ks (cdf: float -> float) (xs: float[]) : float =
    let sorted = Array.sort xs
    let n = float sorted.Length
    let mutable d = 0.0

    for i in 0 .. sorted.Length - 1 do
        let f = cdf sorted[i]
        d <- max d (max (float (i + 1) / n - f) (f - float i / n))

    d

/// p-value of the Pearson chi-squared statistic over the cells, df = cells - 1 (general knowledge).
let private pValue (observed: float[]) (expected: float[]) : float =
    let mutable chi2 = 0.0

    for i in 0 .. observed.Length - 1 do
        let d = observed[i] - expected[i]
        chi2 <- chi2 + d * d / expected[i]

    Special.gammaUpperRegularized (float (observed.Length - 1) / 2.0) (chi2 / 2.0)

/// Cells k = 0 .. counts.Length - 1 plus a tail cell for every larger value, pooled upwards until each expects at
/// least 5; a short last pool joins the cell before it.
let private pooledPValue (n: int) (counts: int[]) (probability: int -> float) : float =
    let observed = ResizeArray<float>()
    let expected = ResizeArray<float>()
    let mutable o = 0.0
    let mutable e = 0.0
    let mutable seenObserved = 0.0
    let mutable seenExpected = 0.0

    for k in 0 .. counts.Length - 1 do
        o <- o + float counts[k]
        e <- e + float n * probability k
        seenObserved <- seenObserved + float counts[k]
        seenExpected <- seenExpected + float n * probability k

        if e >= 5.0 then
            observed.Add o
            expected.Add e
            o <- 0.0
            e <- 0.0

    o <- o + (float n - seenObserved)
    e <- e + (float n - seenExpected)

    if e >= 5.0 then
        observed.Add o
        expected.Add e
    else
        observed[observed.Count - 1] <- observed[observed.Count - 1] + o
        expected[expected.Count - 1] <- expected[expected.Count - 1] + e

    pValue (observed.ToArray()) (expected.ToArray())

let private tally (size: int) (xs: int[]) : int[] =
    let counts = Array.zeroCreate size

    for x in xs do
        if x >= 0 && x < size then
            counts[x] <- counts[x] + 1

    counts

let private n = 20_000
let private ksBound = 1.95 / sqrt (float n)

let private expectPoisson (mean: float) (seed: int) =
    let xs = sample n (Generator.poisson mean) (Generator.create seed)
    let kmax = int (mean + 10.0 * sqrt mean) + 20
    let p = pooledPValue n (tally (kmax + 1) xs) (fun k -> exp (float k * log mean - mean - Special.logFactorial k))
    Expect.isTrue (p >= 0.001) (sprintf "poisson %g: chi-squared p-value %g >= 0.001" mean p)

let private expectBinomial (trials: int) (prob: float) (seed: int) =
    let xs = sample n (Generator.binomial trials prob) (Generator.create seed)

    let p =
        pooledPValue n (tally (trials + 1) xs) (fun k ->
            exp (Special.logChoose trials k + float k * log prob + float (trials - k) * log (1.0 - prob)))

    Expect.isTrue (p >= 0.001) (sprintf "binomial %d %g: chi-squared p-value %g >= 0.001" trials prob p)

let private expectGamma (shape: float) (scale: float) (seed: int) =
    let xs = sample n (Generator.gamma shape scale) (Generator.create seed)
    let d = ks (fun x -> Special.gammaLowerRegularized shape (x / scale)) xs
    Expect.isTrue (d <= ksBound) (sprintf "gamma %g %g: KS %g <= %g" shape scale d ksBound)

let private expectBeta (a: float) (b: float) (seed: int) =
    let xs = sample n (Generator.beta a b) (Generator.create seed)
    let d = ks (fun x -> Special.betaRegularized a b x) xs
    Expect.isTrue (d <= ksBound) (sprintf "beta %g %g: KS %g <= %g" a b d ksBound)

let private expectOutOfRange (message: string) (f: unit -> unit) =
    Expect.throwsT<System.ArgumentOutOfRangeException> f message

[<Tests>]
let tests =
    testList
        "Generator"
        [
            // Seed 7.
            test "the same seed gives the same sequence" {
                let a = uniforms 1000 (Generator.create 7)
                let b = uniforms 1000 (Generator.create 7)
                Expect.isTrue (a = b) "sequences equal"
            }

            // Seeds 1 and 2.
            test "different seeds differ" {
                let a = uniforms 4 (Generator.create 1)
                let b = uniforms 4 (Generator.create 2)
                Expect.isTrue (a <> b) "sequences differ"
            }

            // Seed 3.
            test "uniform stays in [0, 1)" {
                let xs = uniforms 100_000 (Generator.create 3)
                Expect.isTrue (Array.min xs >= 0.0) "min >= 0"
                Expect.isTrue (Array.max xs < 1.0) "max < 1"
                Expect.isTrue (abs (Array.average xs - 0.5) <= 0.005) "mean within 0.005 of 0.5"
            }

            // Seed 4.
            test "normal has mean 0 and variance 1" {
                let m = normalMoments 100_000 (Generator.create 4)
                Expect.isTrue (abs (Moments.mean m) <= 0.02) "abs mean <= 0.02"
                Expect.isTrue (abs (Moments.variance m - 1.0) <= 0.03) "abs (variance - 1) <= 0.03"
            }

            // Seed 5.
            test "normals scales and shifts" {
                let g = Generator.create 5
                let struct (xs, next) = Generator.normals 10.0 2.0 50_000 g
                let m = Array.fold (Moments.add true) Moments.zero xs
                Expect.equal xs.Length 50_000 "count"
                Expect.isTrue (abs (Moments.mean m - 10.0) <= 0.05) "mean within 0.05 of 10"
                Expect.isTrue (abs (Moments.variance m - 4.0) <= 0.15) "variance within 0.15 of 4"

                // The returned generator is the one a step-by-step run reaches.
                let mutable stepped = g

                for _ in 1..50_000 do
                    let struct (_, n) = Generator.normal stepped
                    stepped <- n

                let struct (a, _) = Generator.normal next
                let struct (b, _) = Generator.normal stepped
                Expect.equal a b "the returned generator continues the sequence"
            }

            // Seed 1.
            test "the first uniform values of seed 1 are pinned" {
                let expected: float[] =
                    [|
                        0.6121158804744482
                        0.4441434277687222
                        0.6781873514410108
                        0.49892908753827214
                        0.3183279244694859
                        0.5603306379634887
                        0.3533998727798462
                        0.4709854475222528
                    |]
                Expect.equal (uniforms 8 (Generator.create 1)) expected "first eight values"
            }

            // Seed 201.
            test "integer is uniform on its range" {
                let xs = sample 60_000 (Generator.integer 0 6) (Generator.create 201)
                Expect.isTrue (xs |> Array.forall (fun x -> x >= 0 && x < 6)) "all in [0, 6)"
                let counts = tally 6 xs
                let p = pValue (counts |> Array.map float) (Array.create 6 10_000.0)
                Expect.isTrue (p >= 0.001) (sprintf "chi-squared p-value %g >= 0.001" p)
            }

            // Seed 202.
            test "integer covers the whole int range" {
                let xs =
                    sample 1000 (Generator.integer System.Int32.MinValue System.Int32.MaxValue) (Generator.create 202)

                Expect.isTrue
                    (xs |> Array.forall (fun x -> x >= System.Int32.MinValue && x < System.Int32.MaxValue))
                    "in range"

                Expect.isTrue (xs |> Array.exists (fun x -> x < 0)) "some negative"
                Expect.isTrue (xs |> Array.exists (fun x -> x > 0)) "some positive"
            }

            // Seed 1. Printed by the .NET run of this test with placeholders, then checked under mocha.
            test "the first integers of seed 1 are pinned" {
                let expected: int[] = [| 688; 497; 495; 114; 25; 765; 896; 94 |]
                Expect.equal (sample 8 (Generator.integer 0 1000) (Generator.create 1)) expected "first eight integers"
            }

            // Seed 203. Closed form 1 - e^(-2x).
            test "exponential" {
                let xs = sample n (Generator.exponential 2.0) (Generator.create 203)
                let d = ks (fun x -> 1.0 - exp (-2.0 * x)) xs
                Expect.isTrue (d <= ksBound) (sprintf "KS %g <= %g" d ksBound)
            }

            // Seeds 211 to 214.
            test "gamma" {
                expectGamma 0.3 2.0 211
                expectGamma 1.0 2.0 212
                expectGamma 2.5 2.0 213
                expectGamma 30.0 2.0 214
            }

            // Seeds 221 and 222.
            test "beta" {
                expectBeta 0.5 0.5 221
                expectBeta 2.0 5.0 222
            }

            // Seeds 231 to 236.
            test "poisson" {
                expectPoisson 0.5 231
                expectPoisson 5.0 232
                expectPoisson 15.9 233
                expectPoisson 16.0 234
                expectPoisson 100.0 235

                let xs = sample n (Generator.poisson 1e4) (Generator.create 236) |> Array.map float
                let m = Array.fold (Moments.add true) Moments.zero xs
                let bound = 4.0 * sqrt (1e4 / float n)
                Expect.isTrue (abs (Moments.mean m - 1e4) <= bound) (sprintf "mean %g within %g of 1e4" (Moments.mean m) bound)
                Expect.isTrue (abs (Moments.variance m / 1e4 - 1.0) <= 0.05) (sprintf "variance %g within 5%% of 1e4" (Moments.variance m))
            }

            // Seeds 241 to 243.
            test "binomial" {
                expectBinomial 20 0.3 241
                expectBinomial 100 0.5 242

                let xs = sample n (Generator.binomial 1_000_000 0.4) (Generator.create 243) |> Array.map float
                let m = Array.fold (Moments.add true) Moments.zero xs
                let sigma = sqrt (1e6 * 0.4 * 0.6)
                let bound = 4.0 * sigma / sqrt (float n)
                Expect.isTrue (abs (Moments.mean m - 400_000.0) <= bound) (sprintf "mean %g within %g of 400000" (Moments.mean m) bound)
            }

            // Seed 251 for the distribution, seed 1 for the pinned shuffle.
            test "shuffle is a uniform permutation" {
                let input = [| 0; 1; 2 |]
                let results = sample 60_000 (Generator.shuffle input) (Generator.create 251)
                Expect.equal input [| 0; 1; 2 |] "the input is unchanged"

                Expect.isTrue
                    (results |> Array.forall (fun r -> Array.sort r = [| 0; 1; 2 |]))
                    "each result is a permutation"

                let codes = Array.zeroCreate 27

                for r in results do
                    let c = r[0] * 9 + r[1] * 3 + r[2]
                    codes[c] <- codes[c] + 1

                let observed =
                    [| 0 * 9 + 1 * 3 + 2; 0 * 9 + 2 * 3 + 1; 1 * 9 + 0 * 3 + 2; 1 * 9 + 2 * 3 + 0; 2 * 9 + 0 * 3 + 1; 2 * 9 + 1 * 3 + 0 |]
                    |> Array.map (fun c -> float codes[c])

                let p = pValue observed (Array.create 6 10_000.0)
                Expect.isTrue (p >= 0.001) (sprintf "chi-squared p-value %g >= 0.001" p)

                let big = [| 0..9 |]
                let struct (shuffled, _) = Generator.shuffle big (Generator.create 1)
                Expect.equal big [| 0..9 |] "the input is unchanged"
                let pinned: int[] = [| 3; 2; 5; 4; 9; 1; 0; 7; 6; 8 |]
                Expect.equal shuffled pinned "the first shuffle of 0..9 from seed 1"
            }

            // Seed 261.
            test "split gives two other streams" {
                let parent = Generator.create 261
                let struct (a, b) = Generator.split parent
                let ua = uniforms 4 a
                let ub = uniforms 4 b
                let up = uniforms 4 parent
                Expect.isTrue (ua <> ub) "the two children differ"
                Expect.isTrue (ua <> up) "the first child differs from the parent"
                Expect.isTrue (ub <> up) "the second child differs from the parent"

                let struct (a2, b2) = Generator.split parent
                Expect.equal (uniforms 4 a2) ua "splitting twice gives the same first child"
                Expect.equal (uniforms 4 b2) ub "splitting twice gives the same second child"

                let xs = uniforms 100_000 a
                let ys = uniforms 100_000 b
                let mutable c = Comoments.zero

                for i in 0 .. xs.Length - 1 do
                    c <- Comoments.add true c xs[i] ys[i]

                Expect.isTrue (abs (Comoments.correlation c) <= 0.01) (sprintf "correlation %g within 0.01 of 0" (Comoments.correlation c))
            }

            // Seeds 271 to 276.
            test "variates at their edges" {
                let gs = sample 10_000 (Generator.gamma 1e-3 1.0) (Generator.create 271)
                Expect.isTrue (gs |> Array.forall (fun x -> not (System.Double.IsNaN x || System.Double.IsInfinity x) && x >= 0.0)) "gamma 1e-3 finite and >= 0"

                let bs = sample 10_000 (Generator.beta 1e-3 1e-3) (Generator.create 272)
                Expect.isTrue (bs |> Array.forall (fun x -> not (System.Double.IsNaN x) && x >= 0.0 && x <= 1.0)) "beta 1e-3 in [0, 1], none NaN"
                Expect.isTrue (abs (Array.average bs - 0.5) <= 0.02) "beta 1e-3 mean within 0.02 of 0.5"

                // Subnormal shapes: both log-gamma deviates are -infinity, so each draw is 0 or 1. Seed 277.
                let tiny = sample 1000 (Generator.beta 1e-310 1e-310) (Generator.create 277)
                Expect.isTrue (tiny |> Array.forall (fun x -> x = 0.0 || x = 1.0)) "beta 1e-310 gives 0 or 1, none NaN"
                Expect.isTrue (tiny |> Array.exists (fun x -> x = 0.0) && tiny |> Array.exists (fun x -> x = 1.0)) "beta 1e-310 gives both"

                let struct (b0, _) = Generator.binomial 10 0.0 (Generator.create 273)
                let struct (b1, _) = Generator.binomial 10 1.0 (Generator.create 273)
                let struct (bz, _) = Generator.binomial 0 0.5 (Generator.create 273)
                Expect.equal b0 0 "binomial 10 0.0"
                Expect.equal b1 10 "binomial 10 1.0"
                Expect.equal bz 0 "binomial 0 0.5"

                let struct (p0, _) = Generator.poisson 0.0 (Generator.create 274)
                Expect.equal p0 0 "poisson 0.0"

                let struct (i, _) = Generator.integer 5 6 (Generator.create 275)
                Expect.equal i 5 "integer 5 6"

                let struct (empty, _) = Generator.shuffle [||] (Generator.create 276)
                Expect.equal empty.Length 0 "shuffle of an empty array"
            }

            // Seed 281.
            test "domains" {
                let g = Generator.create 281
                expectOutOfRange "integer 3 3" (fun () -> Generator.integer 3 3 g |> ignore)
                expectOutOfRange "exponential 0.0" (fun () -> Generator.exponential 0.0 g |> ignore)
                expectOutOfRange "gamma -1.0 1.0" (fun () -> Generator.gamma -1.0 1.0 g |> ignore)
                expectOutOfRange "beta 1.0 0.0" (fun () -> Generator.beta 1.0 0.0 g |> ignore)
                expectOutOfRange "binomial -1 0.5" (fun () -> Generator.binomial -1 0.5 g |> ignore)
                expectOutOfRange "binomial 5 1.5" (fun () -> Generator.binomial 5 1.5 g |> ignore)
                expectOutOfRange "binomial 5 nan" (fun () -> Generator.binomial 5 nan g |> ignore)
                expectOutOfRange "poisson -1.0" (fun () -> Generator.poisson -1.0 g |> ignore)
                expectOutOfRange "poisson nan" (fun () -> Generator.poisson nan g |> ignore)
                expectOutOfRange "poisson 2e9" (fun () -> Generator.poisson 2e9 g |> ignore)
                expectOutOfRange "exponential nan" (fun () -> Generator.exponential nan g |> ignore)
                expectOutOfRange "gamma nan 1.0" (fun () -> Generator.gamma nan 1.0 g |> ignore)
                expectOutOfRange "gamma 1.0 nan" (fun () -> Generator.gamma 1.0 nan g |> ignore)
                expectOutOfRange "beta nan 1.0" (fun () -> Generator.beta nan 1.0 g |> ignore)
                expectOutOfRange "beta 1.0 nan" (fun () -> Generator.beta 1.0 nan g |> ignore)
            }
        ]
