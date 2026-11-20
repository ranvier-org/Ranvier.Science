// Reference values: the direct sum X_k = sum_j x_j e^(-2 pi i jk/n) computed in the test with `exactSum` over the
// cosines and sines of 2 pi ((j k) mod n)/n; the closed forms of an impulse, a constant and a cosine; Parseval's
// identity; the linear convolution worked by hand and the direct double sum; the bins of NumPy's rfftfreq (k/(n d))
// worked by hand.
module Ranvier.Science.Tests.FourierTests

open System
open Expecto
open Ranvier.Numerics

/// The direct sum of the transform of (re, im) at the given bins, each from the correctly rounded sum of its terms.
let private dftAt (re: float[]) (im: float[]) (bins: int[]) : float[] * float[] =
    let n = re.Length
    let c = Array.init n (fun t -> Math.Cos (2.0 * Math.PI * float t / float n))
    let s = Array.init n (fun t -> Math.Sin (2.0 * Math.PI * float t / float n))

    let outRe =
        bins |> Array.map (fun k -> exactSum (Array.init n (fun j -> re[j] * c[(j * k) % n] + im[j] * s[(j * k) % n])))

    let outIm =
        bins |> Array.map (fun k -> exactSum (Array.init n (fun j -> im[j] * c[(j * k) % n] - re[j] * s[(j * k) % n])))

    outRe, outIm

/// Every bin up to n = 1024; above it the bins 0, n/2 and n − 1 and every 37th. The direct sum over every bin at 4095
/// and 4096, O(n²), was most of the mocha run's time (docs/measurements/gate.md).
let private checkedBins (n: int) : int[] =
    if n <= 1024 then
        Array.init n id
    else
        Array.append [| 0; n / 2; n - 1 |] [| 0..37 .. n - 1 |] |> Array.distinct |> Array.sort

let private normalsOf (seed: int) (n: int) : float[] =
    let struct (xs, _) = Generator.normals 0.0 1.0 n (Generator.create seed)
    xs

let private sizes = [ 1; 2; 3; 5; 8; 12; 17; 64; 100; 1000; 1024; 4095; 4096 ]

[<Tests>]
let tests =
    testList
        "Fourier"
        [
            test "an impulse transforms to ones" {
                let struct (re, im) = Fourier.forward [| 1.; 0.; 0.; 0.; 0.; 0.; 0.; 0. |] (Array.zeroCreate 8)
                Expect.equal re (Array.create 8 1.0) "re"
                Expect.equal im (Array.create 8 0.0) "im"
            }

            test "a constant and a cosine" {
                let struct (re, im) = Fourier.forward (Array.create 16 1.0) (Array.zeroCreate 16)
                Expect.equal re[0] 16.0 "DC"

                for k in 0..15 do
                    if k > 0 then
                        Expect.isTrue (abs re[k] <= 1e-14) (sprintf "re[%d] = %g" k re[k])

                    Expect.isTrue (abs im[k] <= 1e-14) (sprintf "im[%d] = %g" k im[k])

                let x = Array.init 16 (fun j -> Math.Cos (2.0 * Math.PI * 3.0 * float j / 16.0))
                let struct (cr, ci) = Fourier.forward x (Array.zeroCreate 16)

                for k in 0..15 do
                    let expected = if k = 3 || k = 13 then 8.0 else 0.0
                    Expect.isTrue (abs (cr[k] - expected) <= 1e-13) (sprintf "re[%d] = %.17g" k cr[k])
                    Expect.isTrue (abs ci[k] <= 1e-13) (sprintf "im[%d] = %.17g" k ci[k])
            }

            test "forward matches the direct sum" {
                for n in sizes do
                    let re = normalsOf n n
                    let im = normalsOf (n + 1) n
                    let struct (fr, fi) = Fourier.forward re im
                    let bins = checkedBins n
                    let er, ei = dftAt re im bins
                    let scale = Array.sum (Array.map2 (fun a b -> abs a + abs b) re im)
                    Expect.equal fr.Length n (sprintf "n = %d: re length" n)
                    Expect.equal fi.Length n (sprintf "n = %d: im length" n)

                    for i in 0 .. bins.Length - 1 do
                        let k = bins[i]
                        expectClose 0.0 (1e-13 * scale) er[i] fr[k] (sprintf "n = %d: re[%d]" n k)
                        expectClose 0.0 (1e-13 * scale) ei[i] fi[k] (sprintf "n = %d: im[%d]" n k)
            }

            test "inverse undoes forward" {
                for n in sizes do
                    let re = normalsOf n n
                    let im = normalsOf (n + 1) n
                    let struct (fr, fi) = Fourier.forward re im
                    let struct (br, bi) = Fourier.inverse fr fi
                    let top = Array.max (Array.map2 (fun a b -> max (abs a) (abs b)) re im)

                    for j in 0 .. n - 1 do
                        expectClose 0.0 (1e-13 * top) re[j] br[j] (sprintf "n = %d: re[%d]" n j)
                        expectClose 0.0 (1e-13 * top) im[j] bi[j] (sprintf "n = %d: im[%d]" n j)
            }

            test "Parseval" {
                for n in [ 1000; 1024 ] do
                    let re = normalsOf n n
                    let im = normalsOf (n + 1) n
                    let struct (fr, fi) = Fourier.forward re im
                    let time = Array.sum (Array.map2 (fun a b -> a * a + b * b) re im)
                    let freq = Array.sum (Array.map2 (fun a b -> a * a + b * b) fr fi) / float n
                    expectClose 1e-13 0.0 time freq (sprintf "n = %d" n)
            }

            test "real transforms and frequencies" {
                for n in [ 7; 8 ] do
                    let x = normalsOf n n
                    let struct (fr, fi) = Fourier.forward x (Array.zeroCreate n)
                    let bins = n / 2 + 1
                    let struct (rr, ri) = Fourier.realForward x
                    Expect.equal rr (Array.sub fr 0 bins) (sprintf "n = %d: re" n)
                    Expect.equal ri (Array.sub fi 0 bins) (sprintf "n = %d: im" n)

                    Expect.equal
                        (Fourier.powerSpectrum x)
                        (Array.init bins (fun k -> fr[k] * fr[k] + fi[k] * fi[k]))
                        (sprintf "n = %d: power" n)

                Expect.equal (Fourier.frequencies 8 0.5) [| 0.; 0.25; 0.5; 0.75; 1.0 |] "frequencies 8 0.5"
                Expect.equal (Fourier.frequencies 7 1.0).Length 4 "frequencies 7"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Fourier.frequencies 0 1.0 |> ignore) "n = 0"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Fourier.frequencies 8 0.0 |> ignore) "spacing 0"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Fourier.frequencies 8 -1.0 |> ignore) "spacing < 0"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Fourier.frequencies 8 nan |> ignore) "NaN spacing"
            }

            test "convolve" {
                let c = Fourier.convolve [| 1.; 2.; 3. |] [| 0.; 1.; 0.5 |]
                let expected = [| 0.; 1.; 2.5; 4.; 1.5 |]
                Expect.equal c.Length 5 "length"

                for k in 0..4 do
                    Expect.isTrue (abs (c[k] - expected[k]) <= 1e-14) (sprintf "c[%d] = %.17g" k c[k])

                let a = normalsOf 300 300
                let b = normalsOf 200 200
                let big = Fourier.convolve a b
                Expect.equal big.Length 499 "length 499"
                let bound = 1e-12 * Array.sum (Array.map abs a) * Array.max (Array.map abs b)

                for k in 0..498 do
                    let terms = [| for i in 0..299 do if k - i >= 0 && k - i < 200 then yield a[i] * b[k - i] |]
                    let direct = exactSum terms
                    Expect.isTrue (abs (big[k] - direct) <= bound) (sprintf "c[%d]: %.17g against %.17g" k big[k] direct)

                Expect.equal (Fourier.convolve [||] [| 1. |]) [||] "empty first"
                Expect.equal (Fourier.convolve [| 1. |] [||]) [||] "empty second"
            }

            test "lengths" {
                Expect.throwsT<ArgumentException> (fun () -> Fourier.forward [| 1.; 2. |] [| 0. |] |> ignore) "mismatch"
                Expect.throwsT<ArgumentException> (fun () -> Fourier.inverse [| 1. |] [| 0.; 0. |] |> ignore) "mismatch inverse"
                let struct (er, ei) = Fourier.forward [||] [||]
                Expect.equal er [||] "empty re"
                Expect.equal ei [||] "empty im"

                for n in [ 1; 2; 3; 8 ] do
                    let re = Array.init n (fun j -> float (j + 1))
                    let im = Array.init n (fun j -> float (j * j) - 0.5)
                    let re0 = Array.copy re
                    let im0 = Array.copy im
                    let struct (fr, fi) = Fourier.forward re im
                    Expect.equal re re0 (sprintf "n = %d: forward leaves re" n)
                    Expect.equal im im0 (sprintf "n = %d: forward leaves im" n)
                    Expect.isFalse (obj.ReferenceEquals (fr, re)) "fresh re"
                    Expect.isFalse (obj.ReferenceEquals (fi, im)) "fresh im"
                    Fourier.inverse re im |> ignore
                    Expect.equal re re0 (sprintf "n = %d: inverse leaves re" n)
                    Expect.equal im im0 (sprintf "n = %d: inverse leaves im" n)
            }
        ]
