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
    /// `[unit text]`: one of the unit written, read by `UnitInfo.parse` against the model's catalogue when checked.
    | UnitLiteral of string
    /// `convert(expr, [unit text])`: the expression's value in the unit written.
    | Convert of Expr * string

exception internal FormulaSyntax of string

/// A one-line arithmetic notation over the identifiers of a `Model`: `+ - * /`, an integer power `^`, parentheses,
/// the one-argument functions `sqrt abs exp ln log10`, the two-argument functions `min max`, unit literals
/// `[unit text]` and `convert(expr, [unit text])`. A number followed by a literal is their product (`9.81 [m/s^2]`),
/// and a `^` after that literal applies to the literal alone (`2 [m]^2` is 2 m²). A literal's text runs to the next
/// `]`, is trimmed of spaces at either end, and is read by `UnitInfo.parse` against `Model.Catalogue`. `+`, `-`, `min`
/// and `max` need operands of one unit and do not convert; `convert` does. `−`, `×`, `·` and `÷` read as `-`, `*`, `*`
/// and `/`. Spaces outside literals are ignored. A `^` takes an integer exponent from -1000 to 1000.
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
            match peek () with
            | ValueSome c when isDigit c ->
                let n = number ()

                match peek () with
                | ValueSome '[' -> Multiply(n, raised (UnitLiteral(literal ())))
                | _ -> raised n
            | _ -> raised (atom ())

        /// The base, to the exponent of a `^` that follows it.
        and raised (baseExpr: Expr) : Expr =
            match peek () with
            | ValueSome '^' ->
                advance ()
                Power(baseExpr, integer ())
            | _ -> baseExpr

        and atom () : Expr =
            match peek () with
            | ValueSome c when isLetter c -> nameOrCall ()
            | ValueSome '(' ->
                advance ()
                let inner = expr ()
                close ()
                inner
            | ValueSome '[' -> UnitLiteral(literal ())
            | _ -> fail pos.Value (describeAt pos.Value + ": a number, a name, \"(\" or \"[\" is expected")

        /// The text from the `[` at the current position to the next `]`, trimmed of spaces at either end.
        and literal () : string =
            advance ()
            let mutable first = pos.Value
            let mutable closing = pos.Value

            while closing < length && text[closing] <> ']' do
                closing <- closing + 1

            if closing >= length then
                fail length (describeAt length + ": \"]\" is expected")

            let mutable last = closing

            while first < last && text[first] = ' ' do
                first <- first + 1

            while last > first && text[last - 1] = ' ' do
                last <- last - 1

            pos.Value <- closing + 1
            text.Substring(first, last - first)

        /// `convert(expr, [unit text])`, read from the `(` at the current position.
        and conversion () : Expr =
            advance ()

            let malformed () : unit =
                fail
                    pos.Value
                    (describeAt pos.Value + ": \"convert\" takes an expression, a comma and a unit literal in brackets")

            match peek () with
            | ValueNone
            | ValueSome ','
            | ValueSome ')' -> malformed ()
            | ValueSome _ -> ()

            let operand = expr ()

            if peek () <> ValueSome ',' then
                malformed ()

            advance ()

            if peek () <> ValueSome '[' then
                malformed ()

            let unitText = literal ()

            if peek () <> ValueSome ')' then
                malformed ()

            advance ()
            Convert(operand, unitText)

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
            | ValueSome '(' when name = "convert" -> conversion ()
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
                + ": they differ in size only, and "
                + operation
                + " does not convert; convert(expr, [unit]) does."
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

    /// The entries by identifier, and the catalogue that unit literals are read against.
    type private Context =
        {
            Find: string -> Entry voption
            Catalogue: UnitCatalogue
        }

    let private contextOf (model: Model) : Context =
        {
            Find = fun id -> model.TryFind id
            Catalogue = model.Catalogue
        }

    let private missingName (id: string) : string = "The name \"" + id + "\" is not in the model."

    let private outsideNotation (name: string) (arguments: Expr list) : string =
        "\"" + name + "\" with " + string arguments.Length + " argument(s) is not in the notation."

    /// The unit written in a literal; an `Error` of `UnitInfo.parse` is prefixed with the literal.
    let private literalUnit (catalogue: UnitCatalogue) (text: string) : Result<UnitInfo, string> =
        UnitInfo.parse catalogue text
        |> Result.mapError (fun message -> "The unit literal [" + text + "]: " + message)

    /// The unit written in a literal, and the factor from `from` to it.
    let private conversion (catalogue: UnitCatalogue) (from: UnitInfo) (text: string) : Result<UnitInfo * float, string> =
        literalUnit catalogue text
        |> Result.bind (fun target -> UnitInfo.conversionFactor from target |> Result.map (fun factor -> target, factor))

    let rec private checkIn (context: Context) (expr: Expr) : Result<UnitInfo, string> =
        let unitOf = checkIn context

        match expr with
        | Number _ -> Result.Ok UnitInfo.dimensionless
        | Name id ->
            match context.Find id with
            | ValueSome entry -> Result.Ok entry.Unit
            | ValueNone -> Result.Error(missingName id)
        | UnitLiteral text -> literalUnit context.Catalogue text
        | Convert(a, text) -> unitOf a |> Result.bind (fun from -> conversion context.Catalogue from text) |> Result.map fst
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
            | _ -> Result.Error(outsideNotation name arguments)

    /// The unit of `expr` over the identifiers of `model`, or a message naming the operation and the units it met.
    /// A product, quotient, power or root whose scale is not finite or is 0 is an `Error` stating that unit's
    /// symbol, as "km^103". A unit literal has the unit `UnitInfo.parse` gives for its text over `model.Catalogue`, and
    /// an `Error` of that parse is prefixed with the literal ("The unit literal [xyz]: …"). `convert(e, [u])` has the
    /// unit u, or the `Error` of `UnitInfo.conversionFactor` from e's unit to u, which names both units: across
    /// dimensions, and when the quotient of the scales is not finite or is 0. `+`, `-`, `min` and `max` of two units
    /// that differ in scale alone are an `Error` that names both. Cost: one unit operation per node and one
    /// `UnitInfo.parse` per unit literal.
    let check (model: Model) (expr: Expr) : Result<UnitInfo, string> = checkIn (contextOf model) expr

    /// The value of `outcome`; raises `ArgumentException` with its message when it is an `Error`.
    let private orRaise (outcome: Result<'T, string>) : 'T =
        match outcome with
        | Result.Ok value -> value
        | Result.Error message -> raise (ArgumentException(message, "expr"))

    /// The factor a conversion multiplies its operand's value by, from the units `check` gives.
    let private factorIn (context: Context) (operand: Expr) (text: string) : float =
        checkIn context operand
        |> Result.bind (fun from -> conversion context.Catalogue from text)
        |> Result.map snd
        |> orRaise

    /// `x` to the integer power `n`, by repeated multiplication so both targets round alike.
    let private integerPower (x: float) (n: int) : float =
        let mutable product = 1.0

        for _ in 1 .. abs n do
            product <- product * x

        if n < 0 then 1.0 / product else product

    let private evaluatorIn (context: Context) (expr: Expr) : unit -> float =
        checkIn context expr |> orRaise |> ignore

        let rec build (e: Expr) : unit -> float =
            match e with
            | Number x -> fun () -> x
            | Name id ->
                match context.Find id with
                | ValueSome entry -> entry.Read
                | ValueNone -> orRaise (Result.Error(missingName id))
            | UnitLiteral _ -> fun () -> 1.0
            | Convert(a, text) ->
                let f = build a
                let factor = factorIn context a text
                fun () -> f () * factor
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
                | _ -> orRaise (Result.Error(outsideNotation name arguments))

        build expr

    /// A function that evaluates `expr` with tracked reads of the entries of `model`. A unit literal evaluates to 1,
    /// and `convert` multiplies its operand's value by `UnitInfo.conversionFactor` from the operand's unit to the
    /// literal's, computed when the evaluator is built. Creates no node. Names resolve once, when the evaluator is
    /// created: an evaluator that outlives the owner of an entry it reads keeps reading that entry's last value and
    /// does not follow a later declaration of the identifier. Raises `ArgumentException` with the message of `check`
    /// when `check` of `expr` is an `Error`: for an identifier `model` lacks, for units that do not combine, for a unit
    /// literal that does not parse over `model.Catalogue`, and for a `Call` outside the notation (only a hand-built
    /// `Expr` holds one).
    /// Building costs one `check` of `expr` and one of each conversion's operand.
    let evaluator (model: Model) (expr: Expr) : unit -> float = evaluatorIn (contextOf model) expr

    /// True when the two floats are equal and, at zero, of one sign.
    let private identical (x: float) (y: float) : bool = x = y && 1.0 / x = 1.0 / y

    /// The operand whose value `pick` gives, the first when both have it; an exact NaN when either value is NaN.
    let private operandOf (pick: float -> float -> float) (a: Uncertain) (b: Uncertain) : Uncertain =
        let x = Uncertain.value a
        let y = Uncertain.value b

        if Double.IsNaN x || Double.IsNaN y then Uncertain.exact nan
        elif identical (pick x y) x then a
        else b

    let private measuredEvaluatorIn (context: Context) (expr: Expr) : unit -> Uncertain =
        checkIn context expr |> orRaise |> ignore

        let rec build (e: Expr) : unit -> Uncertain =
            match e with
            | Number x ->
                let exact = Uncertain.exact x
                fun () -> exact
            | Name id ->
                match context.Find id with
                | ValueSome entry -> entry.Measured
                | ValueNone -> orRaise (Result.Error(missingName id))
            | UnitLiteral _ ->
                let one = Uncertain.exact 1.0
                fun () -> one
            | Convert(a, text) ->
                let f = build a
                let factor = factorIn context a text
                fun () -> f () * factor
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
                let derivative (v: float) = if n = 0 then 0.0 else float n * integerPower v (n - 1)
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
                | _ -> orRaise (Result.Error(outsideNotation name arguments))

        build expr

    /// A function that evaluates `expr` as `evaluator` does, over tracked reads of each entry's `Entry.Measured`: the
    /// value has the evaluator's bits, with first-order contributions by source (`Uncertain`). A number and a unit
    /// literal are exact; `convert` multiplies its operand by the evaluator's conversion factor, which carries no
    /// uncertainty; `x^0` is 1 with derivative 0 at every x, 0 included; `min` and `max` give the operand whose value
    /// `Math.Min` and `Math.Max` give, the first when both have it, and an exact NaN when either value is NaN. Each
    /// operation costs O(m + n) in its operands' numbers of sources. Creates no node. Names resolve once, when the
    /// evaluator is created: an evaluator that outlives the owner of an entry it reads keeps reading that entry's last
    /// value and does not follow a later declaration of the identifier. Raises `ArgumentException` as `evaluator` does,
    /// and building costs what it does there.
    let measuredEvaluator (model: Model) (expr: Expr) : unit -> Uncertain = measuredEvaluatorIn (contextOf model) expr
