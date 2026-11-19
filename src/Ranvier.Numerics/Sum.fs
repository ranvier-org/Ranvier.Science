namespace Ranvier.Numerics

/// A running sum with its rounding error carried beside it.
[<Struct>]
type Sum = { Total: float; Carry: float }

module Sum =
    let zero: Sum = { Total = 0.0; Carry = 0.0 }

    /// Neumaier's compensated addition when `compensated`; plain addition, with a zero carry, otherwise.
    let add (compensated: bool) (s: Sum) (x: float) : Sum =
        if compensated then
            let t = s.Total + x

            let lost =
                if abs s.Total >= abs x then
                    (s.Total - t) + x
                else
                    (x - t) + s.Total

            { Total = t; Carry = s.Carry + lost }
        else
            { Total = s.Total + x; Carry = 0.0 }

    /// `add` of the negated value.
    let remove (compensated: bool) (s: Sum) (x: float) : Sum = add compensated s (-x)

    /// `Total + Carry`.
    let value (s: Sum) : float = s.Total + s.Carry
