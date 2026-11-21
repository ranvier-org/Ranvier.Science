namespace Ranvier.Numerics

open System

/// The discrete Fourier transform of a complex sequence of any length, held as separate real and imaginary arrays,
/// with real spectra, frequencies and linear convolution. The forward transform is X_k = Σ_j x_j e^(−2πi jk/n); the
/// inverse carries the 1/n. Lengths that are powers of two use an iterative radix-2 transform whose twiddle factors
/// come from a table built with + − × ÷ and sqrt only (half-angle steps from the roots 1, −1 and −i), so those
/// transforms give the same bits on .NET and JavaScript; the table's error grows as log₂ n (general knowledge). Other
/// lengths use Bluestein's algorithm, which evaluates the transform as a convolution through power-of-two transforms
/// of the smallest length at least 2n − 1, and takes its chirp e^(−iπk²/n) from `Math.Cos` and `Math.Sin`, so those
/// results may differ in the last places between targets. Every function leaves its inputs unchanged and returns
/// fresh arrays. Cost O(n log n) (general knowledge); a length that is not a power of two costs three transforms of
/// the padded length, two forward and one inverse. Accuracy: each output of `forward` is within
/// 1e-13 · Σ_j (|Re x_j| + |Im x_j|) of the direct sum, checked at every bin for n ≤ 1024 and at the bins 0, n/2,
/// n − 1 and every 37th for n = 4095 and 4096, and each output of `inverse ∘ forward` within
/// 1e-13 · max_j max(|Re x_j|, |Im x_j|) of the input for n ≤ 4096, measured here by the "Fourier" test list
/// (`dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "Fourier"`).
module Fourier =
    let private checkLengths (re: float[]) (im: float[]) : unit =
        if re.Length <> im.Length then
            raise (
                ArgumentException(
                    sprintf "The real and imaginary parts must have the same length; they have %d and %d." re.Length im.Length
                )
            )

    let private isPowerOfTwo (n: int) : bool = n > 0 && (n &&& (n - 1)) = 0

    /// The smallest power of two that is at least `need`.
    let private powerOfTwoAtLeast (need: int) : int =
        let mutable m = 1

        while m < need do
            m <- m * 2

        m

    /// e^(−2πi k/n) for k below n/2, n a power of two. Each stage's table keeps the previous stage's entries at the even
    /// indices and puts the previous entry times the stage's principal root at the odd ones; the roots for 2, 4 are
    /// set exactly and the rest come from cos(θ/2) = √((1 + cos θ)/2), sin(θ/2) = sin θ/(2 cos(θ/2)), starting from
    /// −i (starting from −1 would divide 0 by 0).
    let private twiddles (n: int) : struct (float[] * float[]) =
        let half = n / 2
        let wr = Array.zeroCreate<float> half
        let wi = Array.zeroCreate<float> half

        if half > 0 then
            wr[0] <- 1.0
            let mutable count = 1
            let mutable rootRe = -1.0
            let mutable rootIm = 0.0

            while count < half do
                let nextRe, nextIm =
                    if count = 1 then
                        0.0, -1.0
                    else
                        let c = sqrt ((1.0 + rootRe) / 2.0)
                        c, rootIm / (2.0 * c)

                for k in count - 1 .. -1 .. 0 do
                    let ar = wr[k]
                    let ai = wi[k]
                    wr[2 * k] <- ar
                    wi[2 * k] <- ai
                    wr[2 * k + 1] <- ar * nextRe - ai * nextIm
                    wi[2 * k + 1] <- ar * nextIm + ai * nextRe

                count <- count * 2
                rootRe <- nextRe
                rootIm <- nextIm

        struct (wr, wi)

    /// The forward transform of a power-of-two length, in place, with the table from `twiddles` of that length.
    let private radix2 (re: float[]) (im: float[]) (wr: float[]) (wi: float[]) : unit =
        let n = re.Length
        let mutable j = 0

        for i in 1 .. n - 1 do
            let mutable bit = n >>> 1

            while j &&& bit <> 0 do
                j <- j ^^^ bit
                bit <- bit >>> 1

            j <- j ^^^ bit

            if i < j then
                let tr = re[i]
                re[i] <- re[j]
                re[j] <- tr
                let ti = im[i]
                im[i] <- im[j]
                im[j] <- ti

        let mutable length = 2

        while length <= n do
            let half = length / 2
            let stride = n / length
            let mutable start = 0

            while start < n do
                for k in 0 .. half - 1 do
                    let tr = wr[k * stride]
                    let ti = wi[k * stride]
                    let a = start + k
                    let b = a + half
                    let xr = re[b] * tr - im[b] * ti
                    let xi = re[b] * ti + im[b] * tr
                    re[b] <- re[a] - xr
                    im[b] <- im[a] - xi
                    re[a] <- re[a] + xr
                    im[a] <- im[a] + xi

                start <- start + length

            length <- length * 2

    /// The inverse of `radix2` including the 1/n, in place: conjugate, transform, conjugate, divide.
    let private radix2Inverse (re: float[]) (im: float[]) (wr: float[]) (wi: float[]) : unit =
        let n = re.Length

        for i in 0 .. n - 1 do
            im[i] <- -im[i]

        radix2 re im wr wi
        let scale = float n

        for i in 0 .. n - 1 do
            re[i] <- re[i] / scale
            im[i] <- -im[i] / scale

    /// The forward transform of any length n ≥ 2 by Bluestein's algorithm, as fresh arrays.
    let private bluestein (re: float[]) (im: float[]) : struct (float[] * float[]) =
        let n = re.Length
        let m = powerOfTwoAtLeast (2 * n - 1)
        let twoN = 2 * n
        let chirpRe = Array.zeroCreate<float> n
        let chirpIm = Array.zeroCreate<float> n
        // q = k² mod 2n, formed incrementally so that no product overflows an int.
        let mutable q = 0

        for k in 0 .. n - 1 do
            let angle = Math.PI * float q / float n
            chirpRe[k] <- Math.Cos angle
            chirpIm[k] <- -(Math.Sin angle)
            q <- (q + 2 * k + 1) % twoN

        let ar = Array.zeroCreate<float> m
        let ai = Array.zeroCreate<float> m
        let hr = Array.zeroCreate<float> m
        let hi = Array.zeroCreate<float> m

        for j in 0 .. n - 1 do
            ar[j] <- re[j] * chirpRe[j] - im[j] * chirpIm[j]
            ai[j] <- re[j] * chirpIm[j] + im[j] * chirpRe[j]
            hr[j] <- chirpRe[j]
            hi[j] <- -chirpIm[j]

            if j > 0 then
                hr[m - j] <- chirpRe[j]
                hi[m - j] <- -chirpIm[j]

        let struct (wr, wi) = twiddles m
        radix2 ar ai wr wi
        radix2 hr hi wr wi

        for i in 0 .. m - 1 do
            let pr = ar[i] * hr[i] - ai[i] * hi[i]
            let pi = ar[i] * hi[i] + ai[i] * hr[i]
            ar[i] <- pr
            ai[i] <- pi

        radix2Inverse ar ai wr wi
        let outRe = Array.zeroCreate<float> n
        let outIm = Array.zeroCreate<float> n

        for k in 0 .. n - 1 do
            outRe[k] <- ar[k] * chirpRe[k] - ai[k] * chirpIm[k]
            outIm[k] <- ar[k] * chirpIm[k] + ai[k] * chirpRe[k]

        struct (outRe, outIm)

    let private transform (re: float[]) (im: float[]) : struct (float[] * float[]) =
        let n = re.Length

        if n < 2 then
            struct (Array.copy re, Array.copy im)
        elif isPowerOfTwo n then
            let outRe = Array.copy re
            let outIm = Array.copy im
            let struct (wr, wi) = twiddles n
            radix2 outRe outIm wr wi
            struct (outRe, outIm)
        else
            bluestein re im

    /// The transform X_k = Σ_j x_j e^(−2πi jk/n) of the sequence with real parts `re` and imaginary parts `im`, as
    /// fresh real and imaginary arrays. Lengths 0 and 1 return copies. Raises `ArgumentException` naming both lengths
    /// when the two arrays differ in length. Cost O(n log n); the error bound is in the module summary.
    let forward (re: float[]) (im: float[]) : struct (float[] * float[]) =
        checkLengths re im
        transform re im

    /// The inverse transform x_j = (1/n) Σ_k X_k e^(2πi jk/n), so `inverse ∘ forward` returns the input to within the
    /// module's bound relative to the largest input magnitude, measured here by the "Fourier" test list. Raises
    /// `ArgumentException` naming both lengths when the two arrays differ in length. Cost as `forward`.
    let inverse (re: float[]) (im: float[]) : struct (float[] * float[]) =
        checkLengths re im
        let n = re.Length

        if n < 2 then
            struct (Array.copy re, Array.copy im)
        else
            let struct (fr, fi) = transform re (Array.map (fun v -> -v) im)
            let scale = float n
            struct (Array.map (fun v -> v / scale) fr, Array.map (fun v -> -v / scale) fi)

    /// The first ⌊n/2⌋ + 1 bins of the transform of the real sequence, for a sequence of length n ≥ 1; the remaining
    /// bins are their conjugate mirror. An empty sequence gives two empty arrays. Cost as `forward`.
    let realForward (x: float[]) : struct (float[] * float[]) =
        let n = x.Length
        let struct (re, im) = transform x (Array.zeroCreate n)
        let bins = if n = 0 then 0 else n / 2 + 1
        struct (Array.sub re 0 bins, Array.sub im 0 bins)

    /// re² + im² of each of the ⌊n/2⌋ + 1 bins of `realForward`, unnormalised. Cost as `forward`.
    let powerSpectrum (x: float[]) : float[] =
        let struct (re, im) = realForward x
        Array.init re.Length (fun k -> re[k] * re[k] + im[k] * im[k])

    /// The ⌊n/2⌋ + 1 frequencies k/(n · spacing) of the bins of `realForward` for a sequence of length n sampled
    /// `spacing` apart (NumPy's `rfftfreq`). Raises `ArgumentOutOfRangeException` when n is below 1 or the spacing is
    /// not above 0, NaN included.
    let frequencies (n: int) (spacing: float) : float[] =
        if n < 1 then
            raise (ArgumentOutOfRangeException("n", "The length must be at least 1."))

        if not (spacing > 0.0) then
            raise (ArgumentOutOfRangeException("spacing", "The sample spacing must be above 0."))

        Array.init (n / 2 + 1) (fun k -> float k / (float n * spacing))

    /// The linear convolution of the two sequences, of length n + m − 1, through power-of-two transforms of at least
    /// that length; empty when either input is empty. Cost O((n + m) log (n + m)). Error: within 1e-12 · Σ|a| · max|b| of
    /// the direct double sum for lengths 300 and 200, measured here by the "Fourier" test list.
    let convolve (a: float[]) (b: float[]) : float[] =
        if a.Length = 0 || b.Length = 0 then
            [||]
        else
            let length = a.Length + b.Length - 1
            let size = powerOfTwoAtLeast length
            let ar = Array.zeroCreate<float> size
            let ai = Array.zeroCreate<float> size
            let br = Array.zeroCreate<float> size
            let bi = Array.zeroCreate<float> size
            Array.blit a 0 ar 0 a.Length
            Array.blit b 0 br 0 b.Length
            let struct (wr, wi) = twiddles size
            radix2 ar ai wr wi
            radix2 br bi wr wi

            for i in 0 .. size - 1 do
                let pr = ar[i] * br[i] - ai[i] * bi[i]
                let pi = ar[i] * bi[i] + ai[i] * br[i]
                ar[i] <- pr
                ai[i] <- pi

            radix2Inverse ar ai wr wi
            Array.sub ar 0 length
