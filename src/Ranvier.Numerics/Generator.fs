namespace Ranvier.Numerics

open System

/// A pseudo-random generator with value semantics: the same state always gives the same next value.
/// The state is 128 bits held in four 32-bit words.
[<Struct; NoComparison>]
type Generator =
    private
        {
            A: uint32
            B: uint32
            C: uint32
            D: uint32
        }

module Generator =
    // sfc32 (Small Fast Counting generator, Chris Doty-Humphrey, PractRand). It uses only addition,
    // xor, shifts and rotations, so a JavaScript double holds every intermediate exactly once the
    // sum is reduced to 32 bits, and no multiplication has to be emulated.
    let private step (g: Generator) : struct (uint32 * Generator) =
        let t = g.A + g.B + g.D

        let next =
            {
                A = g.B ^^^ (g.B >>> 9)
                B = g.C + (g.C <<< 3)
                C = ((g.C <<< 21) ||| (g.C >>> 11)) + t
                D = g.D + 1u
            }

        struct (t, next)

    let private warmUp (state: Generator) : Generator =
        let mutable g = state

        for _ in 1..20 do
            let struct (_, next) = step g
            g <- next

        g

    /// Every seed, including 0, gives a distinct start: the seed enters all four words and 20 warm-up
    /// steps mix it through the state. The counter word makes every state, the all-zero one included,
    /// advance to a different state.
    let create (seed: int) : Generator =
        let s = uint32 seed

        warmUp
            {
                A = 0x9E3779B9u ^^^ s
                B = 0x243F6A88u + (s <<< 7)
                C = 0xB7E15162u ^^^ ((s <<< 16) ||| (s >>> 16))
                D = s
            }

    /// A uniform value in [0, 1) and the next generator.
    let uniform (g: Generator) : struct (float * Generator) =
        let struct (bits, next) = step g
        struct (float bits / 4294967296.0, next)

    // Box-Muller: two uniforms per deviate, the sine half is discarded so that no deviate is cached
    // in the generator.
    /// A standard normal deviate and the next generator.
    let normal (g: Generator) : struct (float * Generator) =
        let struct (u1, g1) = uniform g
        let struct (u2, g2) = uniform g1
        // 1 - u1 lies in (0, 1], so the logarithm is finite; the floor guards the argument anyway.
        let r = sqrt (-2.0 * log (Math.Max(1.0 - u1, Double.Epsilon)))
        struct (r * cos (2.0 * Math.PI * u2), g2)

    /// `count` normal deviates `mean + sd * z` and the next generator.
    let normals (mean: float) (sd: float) (count: int) (g: Generator) : struct (float[] * Generator) =
        let out: float[] = Array.zeroCreate count
        let mutable current = g

        for i in 0 .. count - 1 do
            let struct (z, next) = normal current
            out[i] <- mean + sd * z
            current <- next

        struct (out, current)

    let private outOfRange (name: string) (message: string) : 'a =
        raise (ArgumentOutOfRangeException(name, message))

    /// An integer uniform on [lower, upperExclusive) and the next generator, exactly uniform: a 32-bit output b is
    /// rejected when b ≥ 2³² − (2³² mod r) for r = upperExclusive − lower, so every residue is equally likely. It
    /// uses only exact float operations on the 32-bit output (general knowledge: a uint32 converts exactly, and `%` on
    /// floats is an exact fmod), so the sequence is equal on both targets, as the "Generator" test "the first integers
    /// of seed 1 are pinned" checks under mocha. Raises `ArgumentOutOfRangeException` unless
    /// lower < upperExclusive. Fewer than 2 outputs are drawn on average (general knowledge: the acceptance
    /// probability exceeds 1/2).
    let integer (lower: int) (upperExclusive: int) (g: Generator) : struct (int * Generator) =
        if lower >= upperExclusive then
            outOfRange
                "upperExclusive"
                (sprintf "integer needs lower < upperExclusive; lower = %d, upperExclusive = %d." lower upperExclusive)

        let r = float upperExclusive - float lower
        let limit = 4294967296.0 - 4294967296.0 % r
        let mutable current = g
        let mutable result = 0
        let mutable found = false

        while not found do
            let struct (bits, next) = step current
            current <- next
            let b = float bits

            if b < limit then
                result <- int (float lower + b % r)
                found <- true

        struct (result, current)

    /// An exponential deviate with the given rate, −ln(1 − u)/rate for a uniform u, and the next generator. Raises
    /// `ArgumentOutOfRangeException` unless rate > 0 (NaN included). Cost: one uniform and one logarithm.
    let exponential (rate: float) (g: Generator) : struct (float * Generator) =
        if not (rate > 0.0) then
            outOfRange "rate" (sprintf "exponential needs rate > 0; rate = %g." rate)

        let struct (u, next) = uniform g
        struct ((0.0 - log (1.0 - u)) / rate, next)

    // Marsaglia and Tsang, ACM Transactions on Mathematical Software 26(3), 2000, for shape >= 1: a squeezed
    // acceptance test on a cubed normal deviate. The result is a positive unit-scale deviate.
    let private gammaAtLeastOne (shape: float) (g: Generator) : struct (float * Generator) =
        let d = shape - 1.0 / 3.0
        let c = 1.0 / sqrt (9.0 * d)
        let mutable current = g
        let mutable result = 0.0
        let mutable found = false

        while not found do
            let struct (x, g1) = normal current
            let struct (u, g2) = uniform g1
            current <- g2
            let t = 1.0 + c * x

            if t > 0.0 then
                let v = t * t * t
                let x2 = x * x
                // The same u serves both tests; log 0 = -infinity accepts, as it should.
                if u < 1.0 - 0.0331 * x2 * x2 || log u < 0.5 * x2 + d * (1.0 - v + log v) then
                    result <- d * v
                    found <- true

        struct (result, current)

    // ln of a unit-scale gamma deviate, and ln(1 - u) of its boost (0 from shape 1). Below shape 1 it is
    // ln G(shape + 1) + ln(1 - u) / shape, which stays finite where (1 - u)^(1/shape) would underflow to zero, and is
    // -infinity only where ln(1 - u) / shape overflows (a subnormal shape).
    let private logGamma (shape: float) (g: Generator) : struct (float * float * Generator) =
        if shape >= 1.0 then
            let struct (x, next) = gammaAtLeastOne shape g
            struct (log x, 0.0, next)
        else
            let struct (x, g1) = gammaAtLeastOne (shape + 1.0) g
            let struct (u, g2) = uniform g1
            let boost = log (1.0 - u)
            struct (log x + boost / shape, boost, g2)

    /// A gamma deviate with the given shape and scale (mean shape · scale) and the next generator. The method of
    /// Marsaglia and Tsang (ACM Transactions on Mathematical Software 26(3), 2000, from documentation), with the
    /// boost Γ(shape + 1) · u^(1/shape) below shape 1. Below shape 1 the deviate is exactly 0 wherever the boost
    /// underflows, for about e^(−745·shape) of the draws, 47% at shape 1e-3 (general knowledge: u^(1/shape) underflows
    /// where u < 2^(−1075·shape)). Raises `ArgumentOutOfRangeException` unless shape > 0 and scale > 0 (NaN included).
    /// Expected cost: a few normal deviates (general knowledge).
    let gamma (shape: float) (scale: float) (g: Generator) : struct (float * Generator) =
        if not (shape > 0.0) then
            outOfRange "shape" (sprintf "gamma needs shape > 0; shape = %g." shape)

        if not (scale > 0.0) then
            outOfRange "scale" (sprintf "gamma needs scale > 0; scale = %g." scale)

        if shape >= 1.0 then
            let struct (x, next) = gammaAtLeastOne shape g
            struct (x * scale, next)
        else
            let struct (x, g1) = gammaAtLeastOne (shape + 1.0) g
            let struct (u, g2) = uniform g1
            struct (x * Math.Pow(1.0 - u, 1.0 / shape) * scale, g2)

    /// A beta deviate with parameters a and b and the next generator, X/(X + Y) for independent gamma deviates X and
    /// Y. The ratio is formed from the logarithms of X and Y, so the result lies in [0, 1] and is never NaN even
    /// where a gamma deviate underflows (a or b below 1). Where both logarithms overflow to −∞ (subnormal a and b),
    /// the result is 1 or 0 as ln(1 − u)/a exceeds ln(1 − u′)/b or not, compared multiplied out by a·b, with u and u′
    /// the uniforms of the two deviates' boosts. Raises `ArgumentOutOfRangeException` unless a > 0 and b > 0 (NaN
    /// included). Cost: two gamma deviates.
    let beta (a: float) (b: float) (g: Generator) : struct (float * Generator) =
        if not (a > 0.0) then
            outOfRange "a" (sprintf "beta needs a > 0; a = %g." a)

        if not (b > 0.0) then
            outOfRange "b" (sprintf "beta needs b > 0; b = %g." b)

        let struct (lx, boostX, g1) = logGamma a g
        let struct (ly, boostY, g2) = logGamma b g1

        if lx = -infinity && ly = -infinity then
            struct ((if boostX * b > boostY * a then 1.0 else 0.0), g2)
        else
            let m = Math.Max(lx, ly)
            let x = exp (lx - m)
            let y = exp (ly - m)
            struct (x / (x + y), g2)

    /// The number of successes in `trials` independent trials of probability p, and the next generator. Knuth's
    /// order-statistic split (The Art of Computer Programming vol. 2, 3rd edition, section 3.4.1, from
    /// documentation): while trials > 64, an order statistic X ~ Beta(a, trials + 1 − a), a = 1 + trials/2, halves
    /// the problem; at most 64 Bernoulli trials remain. Expected cost O(log trials) beta deviates and at most 64
    /// uniforms (general knowledge). Raises `ArgumentOutOfRangeException` unless trials ≥ 0 and 0 ≤ p ≤ 1 (NaN
    /// included).
    let binomial (trials: int) (p: float) (g: Generator) : struct (int * Generator) =
        if trials < 0 then
            outOfRange "trials" (sprintf "binomial needs trials >= 0; trials = %d." trials)

        if not (p >= 0.0 && p <= 1.0) then
            outOfRange "p" (sprintf "binomial needs 0 <= p <= 1; p = %g." p)

        let mutable current = g
        let mutable remaining = trials
        let mutable prob = p
        let mutable count = 0

        while remaining > 64 do
            let a = 1 + remaining / 2
            let struct (x, next) = beta (float a) (float (remaining + 1 - a)) current
            current <- next

            if x >= prob then
                remaining <- a - 1
                prob <- prob / x
            else
                count <- count + a
                remaining <- remaining - a
                prob <- (prob - x) / (1.0 - x)

        for _ in 1..remaining do
            let struct (u, next) = uniform current
            current <- next

            if u < prob then
                count <- count + 1

        struct (count, current)

    /// A Poisson deviate with the given mean and the next generator. Above mean 16, Knuth's gamma split (The Art of
    /// Computer Programming vol. 2, 3rd edition, section 3.4.1, from documentation): X ~ Gamma(m), m = ⌊7 · mean/8⌋,
    /// either adds m and continues on mean − X, or ends with a binomial draw of m − 1 trials; at or below 16 uniforms
    /// are multiplied until the product falls below e^(−mean). Expected cost O(log mean) gamma deviates then a
    /// product of about 16 uniforms (general knowledge). Raises `ArgumentOutOfRangeException` unless
    /// 0 ≤ mean ≤ 2³⁰ (NaN included).
    let poisson (mean: float) (g: Generator) : struct (int * Generator) =
        if not (mean >= 0.0 && mean <= 1073741824.0) then
            outOfRange "mean" (sprintf "poisson needs 0 <= mean <= 2^30; mean = %g." mean)

        let mutable current = g
        let mutable rest = mean
        let mutable count = 0
        let mutable finished = false

        while not finished && rest > 16.0 do
            let m = Math.Floor(7.0 * rest / 8.0)
            let struct (x, next) = gamma m 1.0 current
            current <- next

            if x < rest then
                count <- count + int m
                rest <- rest - x
            else
                let struct (k, g2) = binomial (int m - 1) (rest / x) current
                current <- g2
                count <- count + k
                finished <- true

        if not finished then
            let limit = exp (-rest)
            let mutable product = 1.0
            let mutable draws = 0

            while draws = 0 || product > limit do
                let struct (u, next) = uniform current
                current <- next
                product <- product * (1.0 - u)
                draws <- draws + 1

            count <- count + draws - 1

        struct (count, current)

    /// A uniformly random permutation of a copy of the array, by Fisher-Yates with `integer`, and the next
    /// generator. The input is not changed. Cost O(n) draws.
    let shuffle (items: 'T[]) (g: Generator) : struct ('T[] * Generator) =
        let out = Array.copy items
        let mutable current = g

        for i in out.Length - 1 .. -1 .. 1 do
            let struct (j, next) = integer 0 (i + 1) current
            current <- next
            let t = out[i]
            out[i] <- out[j]
            out[j] <- t

        struct (out, current)

    /// Two generators from one: eight outputs of the given generator become the four words of each, and each is then
    /// warmed by 20 steps as `create` does. The same state always splits into the same pair. No proof backs the
    /// independence of the two streams; the tests check that they differ and that their first 100 000 paired uniforms
    /// show a correlation within 0.01 of 0 (measured here: the `Generator` test list).
    let split (g: Generator) : struct (Generator * Generator) =
        let struct (w0, g1) = step g
        let struct (w1, g2) = step g1
        let struct (w2, g3) = step g2
        let struct (w3, g4) = step g3
        let struct (w4, g5) = step g4
        let struct (w5, g6) = step g5
        let struct (w6, g7) = step g6
        let struct (w7, _) = step g7
        struct (warmUp { A = w0; B = w1; C = w2; D = w3 }, warmUp { A = w4; B = w5; C = w6; D = w7 })
