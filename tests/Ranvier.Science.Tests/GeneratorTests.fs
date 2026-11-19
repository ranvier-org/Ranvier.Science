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
        ]
