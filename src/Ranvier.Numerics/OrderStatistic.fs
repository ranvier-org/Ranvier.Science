namespace Ranvier.Numerics

open System

module OrderStatistic =
    /// The type-7 quantile at the probability p, linear between the order statistics: with h = (n − 1)p and j = ⌊h⌋
    /// over the 0-based array of n values, x[j] when h is a whole number or x[j + 1] = x[j], and
    /// (1 − f)·x[j] + f·x[j + 1] with f = h − j otherwise. NaN for an empty array. Raises
    /// `ArgumentOutOfRangeException` unless 0 ≤ p ≤ 1, and so for a NaN p. The array is assumed sorted ascending and is
    /// not checked.
    let quantileSorted (p: float) (sorted: float[]) : float =
        if not (p >= 0.0 && p <= 1.0) then
            raise (ArgumentOutOfRangeException("p", "A probability must be from 0 to 1, and not NaN."))

        let n = sorted.Length

        if n = 0 then
            nan
        else
            let h = float (n - 1) * p
            let j = int (Math.Floor h)

            if j >= n - 1 then
                sorted[n - 1]
            else
                let f = h - float j
                let x = sorted[j]
                let y = sorted[j + 1]
                if f = 0.0 || x = y then x else (1.0 - f) * x + f * y
