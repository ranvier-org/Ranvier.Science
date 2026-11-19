namespace Ranvier.Numerics

open System

/// How far a value may move from the last published one and still count as unchanged.
type Tolerance = { Absolute: float; Relative: float }

module Tolerance =
    let private bound (name: string) (x: float) : float =
        if x >= 0.0 then
            x
        else
            raise (ArgumentOutOfRangeException(name, "A tolerance bound must be zero or more, and not NaN."))

    /// A tolerance of `x` in the units of the value. Raises `ArgumentOutOfRangeException` for a negative or NaN bound.
    let absolute (x: float) : Tolerance = { Absolute = bound "x" x; Relative = 0.0 }

    /// A tolerance of `x` times the magnitude of the previous value. Raises `ArgumentOutOfRangeException` for a negative or NaN bound.
    let relative (x: float) : Tolerance = { Absolute = 0.0; Relative = bound "x" x }

    /// `|next - previous| <= max t.Absolute (t.Relative * |previous|)`. Two NaNs are within, and so are equal infinities; one NaN, opposite infinities, or an infinity against a finite value are not.
    let within (t: Tolerance) (previous: float) (next: float) : bool =
        if Double.IsNaN previous || Double.IsNaN next then
            Double.IsNaN previous && Double.IsNaN next
        elif previous = next then
            true
        elif Double.IsInfinity previous || Double.IsInfinity next then
            false
        else
            let scaled = t.Relative * abs previous
            let limit = if scaled > t.Absolute then scaled else t.Absolute
            abs (next - previous) <= limit

    /// `previous` when there is one and `next` is within the tolerance of it; `next` otherwise.
    /// Applied inside a memo's computation, to the previous value the memo passes in.
    let hold (t: Tolerance) (previous: float voption) (next: float) : float =
        match previous with
        | ValueSome p when within t p next -> p
        | _ -> next

    /// The `previous` array instance when it has the length of `next` and every element is within the tolerance of its counterpart; `next` otherwise.
    /// Applied inside a memo's computation, to the previous array the memo passes in.
    let holdSeries (t: Tolerance) (previous: float[] voption) (next: float[]) : float[] =
        match previous with
        | ValueSome p when p.Length = next.Length ->
            let mutable same = true
            let mutable i = 0

            while same && i < p.Length do
                same <- within t p[i] next[i]
                i <- i + 1

            if same then p else next
        | _ -> next
