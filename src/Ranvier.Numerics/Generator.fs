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

    /// Every seed, including 0, gives a distinct start: the seed enters all four words and 20 warm-up
    /// steps mix it through the state. The counter word makes every state, the all-zero one included,
    /// advance to a different state.
    let create (seed: int) : Generator =
        let s = uint32 seed

        let mutable g =
            {
                A = 0x9E3779B9u ^^^ s
                B = 0x243F6A88u + (s <<< 7)
                C = 0xB7E15162u ^^^ ((s <<< 16) ||| (s >>> 16))
                D = s
            }

        for _ in 1..20 do
            let struct (_, next) = step g
            g <- next

        g

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
