module Ranvier.Science.Tests.HistogramTests

open System
open Expecto
open Ranvier.Numerics

let private fill (h: Histogram) (xs: float[]) = Array.fold Histogram.add h xs

let private feed (e: QuantileEstimate) (xs: float[]) = Array.fold QuantileEstimate.add e xs

[<Tests>]
let tests =
    testList
        "Histogram"
        [
            test "NumPy's bin rule" {
                let h = fill (Histogram.create [| 0.0; 1.0; 2.0; 3.0 |]) [| 0.0; 1.0; 3.0; 3.0000001; -0.1; nan |]
                Expect.equal (Histogram.counts h) [| 1; 1; 1 |] "counts"
                Expect.equal (Histogram.overflow h) 1 "overflow"
                Expect.equal (Histogram.underflow h) 1 "underflow"
                Expect.equal (Histogram.missing h) 1 "missing"
                Expect.equal (Histogram.total h) 6 "total"
            }

            test "uniform edges" {
                Expect.equal (Histogram.edges (Histogram.uniform 0.0 1.0 4)) [| 0.0; 0.25; 0.5; 0.75; 1.0 |] "quarters"
                let e = Histogram.edges (Histogram.uniform 0.0 0.3 3)
                Expect.equal e[3] 0.3 "last edge is exactly upper"
            }

            test "uniform over a wide or narrow interval" {
                let e = Histogram.edges (Histogram.uniform -1e308 1e308 4)
                Expect.equal e.Length 5 "five edges"
                Expect.equal e[0] -1e308 "first edge"
                Expect.equal e[4] 1e308 "last edge"
                Expect.isTrue (Array.forall2 (fun (a: float) b -> a < b) e[0..3] e[1..4]) "strictly increasing"
                // Finite widths whose product with the bin index would overflow.
                for (lo, hi) in [ (0.0, 1e308); (-1e308, 1e307); (1e308, 1.7e308) ] do
                    let w = Histogram.edges (Histogram.uniform lo hi 10)
                    Expect.equal w.Length 11 $"eleven edges over {lo}..{hi}"
                    Expect.equal w[0] lo "first edge exact"
                    Expect.equal w[10] hi "last edge exact"
                    Expect.isTrue (Array.forall2 (fun (a: float) b -> a < b) w[0..9] w[1..10]) "strictly increasing"
                    Expect.isTrue (Array.forall (fun (x: float) -> not (Double.IsInfinity x)) w) "finite"

                // An interval three doubles wide (3 * 2^-52 above 1.0) cannot hold ten bins.
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Histogram.uniform 1.0 (1.0 + 3.0 * 2.220446049250313e-16) 10 |> ignore) "too many bins"
            }

            test "remove, merge and unmerge" {
                let a = fill (Histogram.uniform 0.0 4.0 4) [| 0.5; 1.5; 1.7; 9.0; -1.0; nan |]
                let b = fill (Histogram.uniform 0.0 4.0 4) [| 0.1; 3.9; 4.0; 9.0; nan |]

                for x in [| 2.5; 9.0; -3.0; nan; 4.0 |] do
                    Expect.equal (Histogram.remove (Histogram.add a x) x) a $"remove {x} restores"

                let m = Histogram.merge a b
                Expect.equal (Histogram.counts m) [| 2; 2; 0; 2 |] "bin sums"
                Expect.equal (Histogram.underflow m) 1 "underflow sum"
                Expect.equal (Histogram.overflow m) 2 "overflow sum"
                Expect.equal (Histogram.missing m) 2 "missing sum"
                Expect.equal (Histogram.total m) (Histogram.total a + Histogram.total b) "total sum"
                Expect.equal (Histogram.unmerge m b) a "unmerge restores"
            }

            test "invalid use raises" {
                Expect.throwsT<ArgumentException> (fun () -> Histogram.create [| 1.0; 1.0 |] |> ignore) "equal edges"
                Expect.throwsT<ArgumentException> (fun () -> Histogram.create [| 0.0 |] |> ignore) "one edge"
                Expect.throwsT<ArgumentException> (fun () -> Histogram.create [| 0.0; nan |] |> ignore) "NaN edge"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Histogram.uniform 1.0 0.0 3 |> ignore) "reversed"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Histogram.uniform 0.0 1.0 0 |> ignore) "no bins"
                let empty = Histogram.uniform 0.0 1.0 2
                Expect.throwsT<InvalidOperationException> (fun () -> Histogram.remove empty 0.2 |> ignore) "empty bin"
                Expect.throwsT<InvalidOperationException> (fun () -> Histogram.remove empty nan |> ignore) "no missing"
                Expect.throwsT<InvalidOperationException> (fun () -> Histogram.unmerge empty (Histogram.add empty 0.2) |> ignore) "unmerge below zero"
                Expect.throwsT<ArgumentException> (fun () -> Histogram.merge empty (Histogram.uniform 0.0 2.0 2) |> ignore) "different edges"
                Expect.throwsT<ArgumentException> (fun () -> Histogram.unmerge empty (Histogram.uniform 0.0 2.0 2) |> ignore) "different edges, unmerge"
            }

            test "copies in and out" {
                let edges = [| 0.0; 1.0; 2.0 |]
                let h = Histogram.add (Histogram.create edges) 0.5
                edges[1] <- 100.0
                (Histogram.edges h)[1] <- 100.0
                (Histogram.counts h)[0] <- 100
                Expect.equal (Histogram.edges h) [| 0.0; 1.0; 2.0 |] "edges unchanged"
                Expect.equal (Histogram.counts h) [| 1; 0 |] "counts unchanged"
                Expect.equal (Histogram.add h 1.5 |> Histogram.counts) [| 1; 1 |] "still bins at 1.0"
            }

            test "the quantile estimate is exact below five" {
                let e = feed (QuantileEstimate.create 0.25) [| 3.0; 1.0; 2.0 |]
                // Type 7: h = (3 - 1) * 0.25 = 0.5, between 1 and 2.
                Expect.equal (QuantileEstimate.value e) 1.5 "type 7"
                Expect.equal (QuantileEstimate.count e) 3.0 "count"
                Expect.isTrue (Double.IsNaN(QuantileEstimate.value (QuantileEstimate.create 0.5))) "empty is NaN"
            }

            test "the quantile estimate at five" {
                let e = feed (QuantileEstimate.create 0.5) [| 5.0; 1.0; 4.0; 2.0; 3.0 |]
                Expect.equal (QuantileEstimate.value e) 3.0 "the markers are the sorted observations"
            }

            test "the quantile estimate converges" {
                let mutable g = Generator.create 11
                let mutable e5 = QuantileEstimate.create 0.5
                let mutable e9 = QuantileEstimate.create 0.9

                for _ in 1..100000 do
                    let struct (u, next) = Generator.uniform g
                    g <- next
                    e5 <- QuantileEstimate.add e5 u
                    e9 <- QuantileEstimate.add e9 u

                Expect.isTrue (abs (QuantileEstimate.value e5 - 0.5) < 0.01) "uniform median"
                Expect.isTrue (abs (QuantileEstimate.value e9 - 0.9) < 0.01) "uniform 0.9"

                // python -I tests/reference/normal_quantile.py  (Plan T1, Task 14) prints 1.9599639845400538
                let z975 = 1.9599639845400538
                let mutable h = Generator.create 12
                let mutable en = QuantileEstimate.create 0.975

                for _ in 1..100000 do
                    let struct (z, next) = Generator.normal h
                    h <- next
                    en <- QuantileEstimate.add en z

                Expect.isTrue (abs (QuantileEstimate.value en - z975) < 0.03) "normal 0.975"
            }

            test "the quantile estimate's domain" {
                for p in [| 0.0; 1.0; nan |] do
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> QuantileEstimate.create p |> ignore) $"p = {p}"

                let e = feed (QuantileEstimate.create 0.5) [| 1.0; nan |]
                let e = feed e (Array.init 10 float)
                Expect.isTrue (Double.IsNaN(QuantileEstimate.value e)) "NaN observation poisons"
                Expect.equal (QuantileEstimate.count e) 12.0 "count includes it"
            }

            test "an infinite observation makes the estimate NaN" {
                let finite = Array.init 10 (fun i -> float i + 0.5)
                let early = feed (feed (QuantileEstimate.create 0.5) [| 1.0; 2.0; infinity; 3.0 |]) finite
                Expect.isTrue (Double.IsNaN(QuantileEstimate.value early)) "+infinity among the first five"
                let afterFive = feed (feed (QuantileEstimate.create 0.5) [| 1.0; 2.0; 3.0; 4.0; 5.0; 6.0; -infinity |]) finite
                Expect.isTrue (Double.IsNaN(QuantileEstimate.value afterFive)) "-infinity after the fifth"
                let late = feed (feed (QuantileEstimate.create 0.5) (Array.init 1000 float)) (Array.append [| infinity |] finite)
                Expect.isTrue (Double.IsNaN(QuantileEstimate.value late)) "+infinity after 1000 finite values"
            }
        ]
