namespace Ranvier.Quantities

open System
open Ranvier.Numerics

/// One line of the formula notation, parsed.
type Expr =
    | Number of float
    | Name of string
    | Negate of Expr
    | Add of Expr * Expr
    | Subtract of Expr * Expr
    | Multiply of Expr * Expr
    | Divide of Expr * Expr
    /// The base to an integer exponent from -1000 to 1000.
    | Power of Expr * int
    | Call of string * Expr list

exception internal FormulaSyntax of string

/// A one-line arithmetic notation over the identifiers of a `Model`: `+ - * /`, an integer power `^`, parentheses,
/// the one-argument functions `sqrt abs exp ln log10` and the two-argument functions `min max`. `−`, `×`, `·` and
/// `÷` read as `-`, `*`, `*` and `/`. Spaces are ignored. A `^` takes an integer exponent from -1000 to 1000.
module Formula =
    let private arity (name: string) : int voption =
        match name with
        | "sqrt"
        | "abs"
        | "exp"
        | "ln"
        | "log10" -> ValueSome 1
        | "min"
        | "max" -> ValueSome 2
        | _ -> ValueNone

    /// The largest exponent magnitude of `^`; a larger one is a syntax error.
    let private maxExponent = 1000

    let private isDigit (c: char) = c >= '0' && c <= '9'

    let private isLetter (c: char) =
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c = '_'

    /// The tree of `text`, or a message with the 1-based character where reading stopped.
    let parse (text: string) : Result<Expr, string> =
        let length = text.Length
        let pos = ref 0

        let fail (at: int) (what: string) : 'a =
            raise (FormulaSyntax(what + " at character " + string (at + 1) + "."))

        let describeAt (at: int) : string =
            if at >= length then "Unexpected end of the text" else "Unexpected \"" + string text[at] + "\""

        let skip () =
            while pos.Value < length && text[pos.Value] = ' ' do
                pos.Value <- pos.Value + 1

        /// The next significant character with the typographic operators normalised; `ValueNone` at the end.
        let peek () : char voption =
            skip ()

            if pos.Value >= length then
                ValueNone
            else
                match text[pos.Value] with
                | '−' -> ValueSome '-'
                | '×'
                | '·' -> ValueSome '*'
                | '÷' -> ValueSome '/'
                | c -> ValueSome c

        let advance () = pos.Value <- pos.Value + 1

        let readDigits () : unit =
            while pos.Value < length && isDigit text[pos.Value] do
                pos.Value <- pos.Value + 1

        let number () : Expr =
            let start = pos.Value
            readDigits ()

            if pos.Value < length && text[pos.Value] = '.' then
                advance ()

                if pos.Value >= length || not (isDigit text[pos.Value]) then
                    fail pos.Value "A digit is expected after the decimal point"

                readDigits ()

            if pos.Value < length && (text[pos.Value] = 'e' || text[pos.Value] = 'E') then
                advance ()

                if pos.Value < length && (text[pos.Value] = '+' || text[pos.Value] = '-') then
                    advance ()

                if pos.Value >= length || not (isDigit text[pos.Value]) then
                    fail pos.Value "A digit is expected in the exponent"

                readDigits ()

            Number(float (text.Substring(start, pos.Value - start)))

        let integer () : int =
            let negative =
                match peek () with
                | ValueSome '-' ->
                    advance ()
                    true
                | ValueSome '+' ->
                    advance ()
                    false
                | _ -> false

            skip ()
            let start = pos.Value
            readDigits ()

            if pos.Value = start then
                fail pos.Value (describeAt pos.Value + ": an integer exponent is expected")

            let digits = text.Substring(start, pos.Value - start).TrimStart('0')

            if digits.Length > 4 || (digits.Length > 0 && int digits > maxExponent) then
                fail start ("An exponent must be from -" + string maxExponent + " to " + string maxExponent)

            let magnitude = if digits.Length = 0 then 0 else int digits
            if negative then -magnitude else magnitude

        let rec expr () : Expr =
            let mutable left = term ()
            let mutable more = true

            while more do
                match peek () with
                | ValueSome '+' ->
                    advance ()
                    left <- Add(left, term ())
                | ValueSome '-' ->
                    advance ()
                    left <- Subtract(left, term ())
                | _ -> more <- false

            left

        and term () : Expr =
            let mutable left = unary ()
            let mutable more = true

            while more do
                match peek () with
                | ValueSome '*' ->
                    advance ()
                    left <- Multiply(left, unary ())
                | ValueSome '/' ->
                    advance ()
                    left <- Divide(left, unary ())
                | _ -> more <- false

            left

        and unary () : Expr =
            match peek () with
            | ValueSome '-' ->
                advance ()
                Negate(unary ())
            | _ -> power ()

        and power () : Expr =
            let baseExpr = atom ()

            match peek () with
            | ValueSome '^' ->
                advance ()
                Power(baseExpr, integer ())
            | _ -> baseExpr

        and atom () : Expr =
            match peek () with
            | ValueNone -> fail pos.Value (describeAt pos.Value + ": a number, a name or \"(\" is expected")
            | ValueSome c when isDigit c -> number ()
            | ValueSome c when isLetter c -> nameOrCall ()
            | ValueSome '(' ->
                advance ()
                let inner = expr ()
                close ()
                inner
            | ValueSome _ -> fail pos.Value (describeAt pos.Value + ": a number, a name or \"(\" is expected")

        and close () : unit =
            match peek () with
            | ValueSome ')' -> advance ()
            | _ -> fail pos.Value (describeAt pos.Value + ": \")\" is expected")

        and nameOrCall () : Expr =
            let start = pos.Value

            while pos.Value < length && (isLetter text[pos.Value] || isDigit text[pos.Value]) do
                pos.Value <- pos.Value + 1

            let name = text.Substring(start, pos.Value - start)

            match peek () with
            | ValueSome '(' ->
                advance ()
                let arguments = ResizeArray<Expr>()
                arguments.Add(expr ())

                while peek () = ValueSome ',' do
                    advance ()
                    arguments.Add(expr ())

                close ()

                match arity name with
                | ValueNone -> fail start ("\"" + name + "\" is not a function of the notation")
                | ValueSome n when n <> arguments.Count ->
                    fail start ("\"" + name + "\" takes " + string n + " argument(s), not " + string arguments.Count)
                | ValueSome _ -> Call(name, List.ofSeq arguments)
            | _ -> Name name

        try
            let tree = expr ()

            match peek () with
            | ValueNone -> Result.Ok tree
            | ValueSome _ -> fail pos.Value (describeAt pos.Value + ": an operator or the end of the text is expected")
        with FormulaSyntax message ->
            Result.Error message

    /// The unit's symbol; "1" for a unit without factors.
    let internal describe (u: UnitInfo) : string =
        let symbol = UnitInfo.symbol u
        if symbol = "" then "1" else symbol

    let private both (a: Result<UnitInfo, string>) (b: Result<UnitInfo, string>) (f: UnitInfo -> UnitInfo -> Result<UnitInfo, string>) =
        match a, b with
        | Result.Error m, _
        | _, Result.Error m -> Result.Error m
        | Result.Ok x, Result.Ok y -> f x y

    let private oneUnit (operation: string) (a: UnitInfo) (b: UnitInfo) : Result<UnitInfo, string> =
        if UnitInfo.sameUnit a b then
            Result.Ok a
        elif UnitInfo.sameDimension a b then
            Result.Error(
                operation
                + " needs operands of one unit, but met "
                + describe a
                + " and "
                + describe b
                + ": they differ in size only, and the notation does not convert."
            )
        else
            Result.Error(operation + " needs operands of one unit, but met " + describe a + " and " + describe b + ".")

    let private pureNumber (name: string) (u: UnitInfo) : Result<UnitInfo, string> =
        if UnitInfo.sameUnit u UnitInfo.dimensionless then
            Result.Ok UnitInfo.dimensionless
        else
            Result.Error(name + " needs a dimensionless operand, but met " + describe u + ".")

    /// The result of `operation`, or `Error` with the message of the exception it raises.
    let private guarded (operation: unit -> Result<UnitInfo, string>) : Result<UnitInfo, string> =
        try
            operation ()
        with e ->
            Result.Error e.Message

    /// The unit of `expr` over the identifiers of `model`, or a message naming the operation and the units it met.
    /// A product, quotient, power or root whose scale is not finite or is 0 is an `Error` stating that unit's
    /// symbol, as "km^103".
    let rec check (model: Model) (expr: Expr) : Result<UnitInfo, string> =
        let unitOf = check model

        match expr with
        | Number _ -> Result.Ok UnitInfo.dimensionless
        | Name id ->
            match model.TryFind id with
            | ValueSome entry -> Result.Ok entry.Unit
            | ValueNone -> Result.Error("The name \"" + id + "\" is not in the model.")
        | Negate a -> unitOf a
        | Add(a, b) -> both (unitOf a) (unitOf b) (oneUnit "+")
        | Subtract(a, b) -> both (unitOf a) (unitOf b) (oneUnit "-")
        | Multiply(a, b) -> both (unitOf a) (unitOf b) (fun x y -> guarded (fun () -> Result.Ok(UnitInfo.multiply x y)))
        | Divide(a, b) -> both (unitOf a) (unitOf b) (fun x y -> guarded (fun () -> Result.Ok(UnitInfo.divide x y)))
        | Power(a, n) -> unitOf a |> Result.bind (fun u -> guarded (fun () -> Result.Ok(UnitInfo.power u n)))
        | Call(name, arguments) ->
            match name, arguments with
            | "sqrt", [ a ] -> unitOf a |> Result.bind (fun u -> guarded (fun () -> UnitInfo.sqrt u))
            | "abs", [ a ] -> unitOf a
            | ("exp" | "ln" | "log10"), [ a ] -> unitOf a |> Result.bind (pureNumber name)
            | ("min" | "max"), [ a; b ] -> both (unitOf a) (unitOf b) (oneUnit name)
            | _ -> Result.Error("\"" + name + "\" with " + string arguments.Length + " argument(s) is not in the notation.")

    /// `x` to the integer power `n`, by repeated multiplication so both targets round alike.
    let private integerPower (x: float) (n: int) : float =
        let mutable product = 1.0

        for _ in 1 .. abs n do
            product <- product * x

        if n < 0 then 1.0 / product else product

    /// A function that evaluates `expr` with tracked reads of the entries of `model`.
    /// Raises ArgumentException for an identifier `model` lacks.
    let evaluator (model: Model) (expr: Expr) : unit -> float =
        let rec build (e: Expr) : unit -> float =
            match e with
            | Number x -> fun () -> x
            | Name id ->
                match model.TryFind id with
                | ValueSome entry -> entry.Read
                | ValueNone -> raise (ArgumentException("The name \"" + id + "\" is not in the model.", "expr"))
            | Negate a ->
                let f = build a
                fun () -> -(f ())
            | Add(a, b) ->
                let f, g = build a, build b
                fun () -> f () + g ()
            | Subtract(a, b) ->
                let f, g = build a, build b
                fun () -> f () - g ()
            | Multiply(a, b) ->
                let f, g = build a, build b
                fun () -> f () * g ()
            | Divide(a, b) ->
                let f, g = build a, build b
                fun () -> f () / g ()
            | Power(a, n) ->
                let f = build a
                fun () -> integerPower (f ()) n
            | Call(name, arguments) ->
                match name, arguments with
                | "sqrt", [ a ] ->
                    let f = build a
                    fun () -> Math.Sqrt(f ())
                | "abs", [ a ] ->
                    let f = build a
                    fun () -> Math.Abs(f ())
                | "exp", [ a ] ->
                    let f = build a
                    fun () -> Math.Exp(f ())
                | "ln", [ a ] ->
                    let f = build a
                    fun () -> Math.Log(f ())
                | "log10", [ a ] ->
                    let f = build a
                    fun () -> Math.Log10(f ())
                | "min", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> Math.Min(f (), g ())
                | "max", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> Math.Max(f (), g ())
                | _ ->
                    raise (
                        ArgumentException(
                            "\"" + name + "\" with " + string arguments.Length + " argument(s) is not in the notation.",
                            "expr"
                        )
                    )

        build expr

    /// True when the two floats are equal and, at zero, of one sign.
    let private identical (x: float) (y: float) : bool = x = y && 1.0 / x = 1.0 / y

    /// The operand whose value `pick` gives, the first when both have it; an exact NaN when either value is NaN.
    let private operandOf (pick: float -> float -> float) (a: Uncertain) (b: Uncertain) : Uncertain =
        let x = Uncertain.value a
        let y = Uncertain.value b

        if Double.IsNaN x || Double.IsNaN y then Uncertain.exact nan
        elif identical (pick x y) x then a
        else b

    /// A function that evaluates `expr` as `evaluator` does, over tracked reads of each entry's `Entry.Measured`: the
    /// value has the evaluator's bits, with first-order contributions by source (`Uncertain`). A number is exact;
    /// `min` and `max` give the operand whose value `Math.Min` and `Math.Max` give, the first when both have it, and an
    /// exact NaN when either value is NaN. Each operation costs O(m + n) in its operands' numbers of sources.
    /// Raises ArgumentException for an identifier `model` lacks.
    let measuredEvaluator (model: Model) (expr: Expr) : unit -> Uncertain =
        let rec build (e: Expr) : unit -> Uncertain =
            match e with
            | Number x ->
                let exact = Uncertain.exact x
                fun () -> exact
            | Name id ->
                match model.TryFind id with
                | ValueSome entry -> entry.Measured
                | ValueNone -> raise (ArgumentException("The name \"" + id + "\" is not in the model.", "expr"))
            | Negate a ->
                let f = build a
                fun () -> -(f ())
            | Add(a, b) ->
                let f, g = build a, build b
                fun () -> f () + g ()
            | Subtract(a, b) ->
                let f, g = build a, build b
                fun () -> f () - g ()
            | Multiply(a, b) ->
                let f, g = build a, build b
                fun () -> f () * g ()
            | Divide(a, b) ->
                let f, g = build a, build b
                fun () -> f () / g ()
            | Power(a, n) ->
                let f = build a
                let power (v: float) = integerPower v n
                let derivative (v: float) = float n * integerPower v (n - 1)
                fun () -> Uncertain.map power derivative (f ())
            | Call(name, arguments) ->
                match name, arguments with
                | "sqrt", [ a ] ->
                    let f = build a
                    fun () -> Uncertain.Sqrt(f ())
                | "abs", [ a ] ->
                    let f = build a
                    fun () -> Uncertain.abs (f ())
                | "exp", [ a ] ->
                    let f = build a
                    fun () -> Uncertain.Exp(f ())
                | "ln", [ a ] ->
                    let f = build a
                    fun () -> Uncertain.Log(f ())
                | "log10", [ a ] ->
                    let f = build a
                    fun () -> Uncertain.Log10(f ())
                | "min", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> operandOf (fun x y -> Math.Min(x, y)) (f ()) (g ())
                | "max", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> operandOf (fun x y -> Math.Max(x, y)) (f ()) (g ())
                | _ ->
                    raise (
                        ArgumentException(
                            "\"" + name + "\" with " + string arguments.Length + " argument(s) is not in the notation.",
                            "expr"
                        )
                    )

        build expr
