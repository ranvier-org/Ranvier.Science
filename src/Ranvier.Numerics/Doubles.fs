namespace Ranvier.Numerics

open System

/// Constants of the doubles and their error-free transformations, shared by the numerics.
module internal Doubles =
    /// ε = 2⁻⁵², the spacing of doubles at 1.
    let epsilon = 2.220446049250313e-16

    /// Whether x is neither NaN nor infinite.
    let isFinite (x: float) : bool = not (Double.IsNaN x || Double.IsInfinity x)

    /// Whether every entry is neither NaN nor infinite.
    let allFinite (xs: float[]) : bool = Array.forall isFinite xs

    /// a + b = s + e exactly, with s the rounded sum (Knuth's TwoSum).
    let twoSum (a: float) (b: float) : struct (float * float) =
        let s = a + b
        let v = s - a
        struct (s, (a - (s - v)) + (b - v))

    /// Veltkamp's split of a into high + low, each half of the significand.
    let split (a: float) : struct (float * float) =
        let c = 134217729.0 * a // 2²⁷ + 1
        let high = c - (c - a)
        struct (high, a - high)

    /// a · b = p + e exactly, with p the rounded product (Dekker's product), where no partial product overflows or
    /// underflows.
    let twoProduct (a: float) (b: float) : struct (float * float) =
        let p = a * b
        let struct (ah, al) = split a
        let struct (bh, bl) = split b
        struct (p, ((ah * bh - p) + ah * bl + al * bh) + al * bl)
