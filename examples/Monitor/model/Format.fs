namespace SequentialMonitor

open System
open Ranvier.Quantities

/// Number text built from IEEE double arithmetic alone, so .NET and JavaScript give the same characters.
module Format =
    /// 2^53, the bound of the range in which doubles hold every whole number.
    let private exactWhole = 9007199254740992.0

    let private maxDecimals = 22

    /// The decimal digits of a whole number from 0 to 2^53.
    let private digitsOf (n: float) : string =
        if n = 0.0 then
            "0"
        else
            let mutable rest = n
            let mutable text = ""

            while rest > 0.0 do
                let quotient = floor (rest / 10.0)
                text <- string (int (rest - 10.0 * quotient)) + text
                rest <- quotient

            text

    /// `a` split into two halves of 26 bits whose sum is `a` (Veltkamp); `a` must be below about 1e300 in magnitude.
    let private split (a: float) : struct (float * float) =
        let c = 134217729.0 * a
        let high = c - (c - a)
        struct (high, a - high)

    /// `a * b` as a rounded product and its exact error (Dekker): `product + error` equals `a * b` exactly.
    let private twoProduct (a: float) (b: float) : struct (float * float) =
        let product = a * b
        let struct (aHigh, aLow) = split a
        let struct (bHigh, bLow) = split b
        let error = ((aHigh * bHigh - product) + aHigh * bLow + aLow * bHigh) + aLow * bLow
        struct (product, error)

    /// <summary>
    /// <c>x</c> to <c>decimals</c> places: digits, ".", and a leading "-" unless every digit is 0. "NaN", "∞" and "-∞"
    /// for the rest. The same text on .NET and in JavaScript.
    /// </summary>
    /// <remarks>
    /// <c>decimals</c> is from 0 to 22, the powers of ten a double holds exactly; raises ArgumentOutOfRangeException
    /// for any other value. The double's own value is rounded half away from zero, exactly, while |x|·10^decimals is
    /// below 2^53 (about 9.007e15), and from 2^53 up its digits past about the sixteenth are truncated to zeros.
    /// </remarks>
    let fixed' (decimals: int) (x: float) : string =
        if decimals < 0 || decimals > maxDecimals then
            raise (
                ArgumentOutOfRangeException(
                    nameof decimals,
                    "The number of decimals must be from 0 to " + string maxDecimals + "."
                )
            )

        if Double.IsNaN x then
            "NaN"
        elif Double.IsPositiveInfinity x then
            "∞"
        elif Double.IsNegativeInfinity x then
            "-∞"
        else
            let mutable scale = 1.0

            for _ in 1..decimals do
                scale <- scale * 10.0

            let magnitude = abs x
            let scaled = magnitude * scale

            let digits =
                if scaled < exactWhole then
                    let struct (product, error) = twoProduct magnitude scale
                    let whole = floor product
                    let rounded = if (product - whole - 0.5) + error >= 0.0 then whole + 1.0 else whole
                    digitsOf rounded
                else
                    let mutable reduced = scaled
                    let mutable zeros = ""

                    while reduced >= exactWhole do
                        reduced <- reduced / 10.0
                        zeros <- zeros + "0"

                    digitsOf (floor reduced) + zeros

            let padded = String.replicate (max 0 (decimals + 1 - digits.Length)) "0" + digits

            let body =
                if decimals = 0 then
                    padded
                else
                    let point = padded.Length - decimals
                    padded.Substring(0, point) + "." + padded.Substring point

            if x < 0.0 && digits.TrimStart('0') <> "" then "-" + body else body

    /// A whole number with "," between thousands.
    let count (n: float) : string =
        let text = fixed' 0 n

        if text = "NaN" || text.EndsWith "∞" then
            text
        else
            let negative = text.StartsWith "-"
            let digits = if negative then text.Substring 1 else text
            let mutable grouped = ""
            let mutable fromRight = 0

            for i in digits.Length - 1 .. -1 .. 0 do
                if fromRight > 0 && fromRight % 3 = 0 then
                    grouped <- "," + grouped

                grouped <- string digits[i] + grouped
                fromRight <- fromRight + 1

            if negative then "-" + grouped else grouped

    /// The number written by `text`, read as the formula notation reads a number with an optional sign.
    let tryNumber (text: string) : float voption =
        match Formula.parse text with
        | Result.Ok(Number x) -> ValueSome x
        | Result.Ok(Negate(Number x)) -> ValueSome -x
        | _ -> ValueNone
