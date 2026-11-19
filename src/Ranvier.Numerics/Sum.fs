namespace Ranvier.Numerics

/// <summary>A running sum with its rounding error carried beside it.</summary>
/// <remarks>
/// Every operation on one accumulator takes the same value of <c>compensated</c>. An operation with <c>false</c> on a
/// state built with <c>true</c> drops the state's carry.
/// </remarks>
[<Struct>]
type Sum = { Total: float; Carry: float }

module Sum =
    let zero: Sum = { Total = 0.0; Carry = 0.0 }

    /// <summary>
    /// Neumaier's compensated addition when <c>compensated</c>; plain addition, with a zero carry, otherwise.
    /// </summary>
    /// <remarks>
    /// Returns the given state itself when the total and the carry after the item equal those before: a zero of
    /// either sign equals the other, so the state keeps the sign of its zero, and a NaN equals nothing. A compensated
    /// total that overflows or receives an infinite term reads NaN; a plain one reads the infinity, or NaN once
    /// infinities of both signs have been added.
    /// </remarks>
    let add (compensated: bool) (s: Sum) (x: float) : Sum =
        let t = s.Total + x

        if compensated then
            let lost =
                if abs s.Total >= abs x then
                    (s.Total - t) + x
                else
                    (x - t) + s.Total

            let carry = s.Carry + lost

            if t = s.Total && carry = s.Carry then
                s
            else
                { Total = t; Carry = carry }
        elif t = s.Total && s.Carry = 0.0 then
            s
        else
            { Total = t; Carry = 0.0 }

    /// `add` of the negated value.
    let remove (compensated: bool) (s: Sum) (x: float) : Sum = add compensated s (-x)

    /// `Total + Carry`.
    let value (s: Sum) : float = s.Total + s.Carry
