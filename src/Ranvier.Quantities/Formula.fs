namespace Ranvier.Quantities

open System
open System.Collections.Generic
open Ranvier.Numerics

exception internal FormulaSyntax of string

/// The statistics of `Formula.monteCarlo` over its draws of an expression.
type MonteCarloResult =
    {
        Mean: float
        /// The sample standard deviation, divisor n − 1.
        StandardDeviation: float
        /// The type-7 quantile of the draws at (1 − coverage)/2.
        Lower: float
        /// The type-7 quantile of the draws at (1 + coverage)/2.
        Upper: float
        Draws: int
    }

/// A one-line arithmetic notation over the identifiers of a `Model`: `+ - * /`, an integer power `^`, parentheses,
/// the one-argument functions `sqrt abs exp ln log10 sin cos tan asin acos atan sinh cosh tanh`, the two-argument
/// functions `min max atan2 hypot`, the name `pi`, unit literals `[unit text]` and `convert(expr, [unit text])`. A
/// number followed by a literal is their product (`9.81 [m/s^2]`), and a `^` after that literal applies to the literal
/// alone (`2 [m]^2` is 2 m²). A literal's text runs to the next `]`, is trimmed of spaces at either end, and is read by
/// `UnitInfo.parse` against `Model.Catalogue`. `+`, `-`, `min`, `max`, `atan2(y, x)` and `hypot(x, y)` need operands
/// of one unit and do not convert; `convert` does. `atan2` is dimensionless and `hypot` has its operands' unit.
/// `exp ln log10` and the trigonometric and hyperbolic functions take a dimensionless argument of any scale, read at
/// scale 1 as its value times its unit's scale (`sin(30 [deg])` is sin(π/6)), and give a dimensionless result of
/// scale 1; the inverse functions give radians. `pi` reads π, dimensionless, unless the model or an ancestor declares
/// an entry `pi`, which it then reads. `−`, `×`, `·` and `÷` read as `-`, `*`, `*` and `/`. Spaces outside literals
/// are ignored. A `^` takes an integer exponent from -1000 to 1000.
module Formula =
    /// The float function and its `Uncertain` counterpart of a one-argument function that takes a dimensionless
    /// argument at scale 1 and gives a dimensionless result of scale 1.
    let private pureFunction (name: string) : ((float -> float) * (Uncertain -> Uncertain)) voption =
        match name with
        | "exp" -> ValueSome((fun x -> Math.Exp x), (fun u -> Uncertain.Exp u))
        | "ln" -> ValueSome((fun x -> Math.Log x), (fun u -> Uncertain.Log u))
        | "log10" -> ValueSome((fun x -> Math.Log10 x), (fun u -> Uncertain.Log10 u))
        | "sin" -> ValueSome((fun x -> Math.Sin x), (fun u -> Uncertain.Sin u))
        | "cos" -> ValueSome((fun x -> Math.Cos x), (fun u -> Uncertain.Cos u))
        | "tan" -> ValueSome((fun x -> Math.Tan x), (fun u -> Uncertain.Tan u))
        | "asin" -> ValueSome((fun x -> Math.Asin x), (fun u -> Uncertain.Asin u))
        | "acos" -> ValueSome((fun x -> Math.Acos x), (fun u -> Uncertain.Acos u))
        | "atan" -> ValueSome((fun x -> Math.Atan x), (fun u -> Uncertain.Atan u))
        | "sinh" -> ValueSome((fun x -> Math.Sinh x), (fun u -> Uncertain.Sinh u))
        | "cosh" -> ValueSome((fun x -> Math.Cosh x), (fun u -> Uncertain.Cosh u))
        | "tanh" -> ValueSome((fun x -> Math.Tanh x), (fun u -> Uncertain.Tanh u))
        | _ -> ValueNone

    let private arity (name: string) : int voption =
        match name with
        | "sqrt"
        | "abs" -> ValueSome 1
        | "min"
        | "max"
        | "atan2"
        | "hypot" -> ValueSome 2
        | _ when (pureFunction name).IsSome -> ValueSome 1
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
            Expr.Convert(operand, unitText)

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

    /// `UnitInfo.dimensionless` for a dimensionless operand of any scale; `Error` naming the function and the unit
    /// otherwise.
    let private pureNumber (name: string) (u: UnitInfo) : Result<UnitInfo, string> =
        if UnitInfo.sameDimension u UnitInfo.dimensionless then
            Result.Ok UnitInfo.dimensionless
        else
            Result.Error(name + " needs a dimensionless operand, but met " + describe u + ".")

    /// The result of `operation`, or `Error` with the message of the exception it raises.
    let private guarded (operation: unit -> Result<UnitInfo, string>) : Result<UnitInfo, string> =
        try
            operation ()
        with e ->
            Result.Error e.Message

    /// The entries by identifier, the formula records of those that are formula entries, and the catalogue that unit
    /// literals are read against.
    type private Context =
        {
            Find: string -> Entry voption
            Record: string -> FormulaRecord voption
            Catalogue: UnitCatalogue
        }

    let private contextOf (model: Model) : Context =
        {
            Find = fun id -> model.TryFind id
            Record =
                fun id ->
                    match model.TryResolve id with
                    | ValueSome(_, record) -> record
                    | ValueNone -> ValueNone
            Catalogue = model.Catalogue
        }

    /// The identifiers and catalogue of a formula entry as they resolved at its declaration.
    let private contextOfRecord (record: FormulaRecord) : Context =
        {
            Find =
                fun id ->
                    match record.Names.TryFind id with
                    | Some(entry, _) -> ValueSome entry
                    | None -> ValueNone
            Record =
                fun id ->
                    match record.Names.TryFind id with
                    | Some(_, inner) -> inner
                    | None -> ValueNone
            Catalogue = record.Catalogue
        }

    /// The record of a formula entry of `expr` declared now in `model`: each identifier of the tree with the entry and
    /// record `Model.TryResolve` gives for it.
    let internal recordOf (model: Model) (expr: Expr) : FormulaRecord =
        let rec collect (names: Map<string, Entry * FormulaRecord voption>) (e: Expr) =
            match e with
            | Name id when not (names.ContainsKey id) ->
                match model.TryResolve id with
                | ValueSome resolved -> names.Add(id, resolved)
                | ValueNone -> names
            | Number _
            | Name _
            | UnitLiteral _ -> names
            | Negate a
            | Power(a, _)
            | Convert(a, _) -> collect names a
            | Add(a, b)
            | Subtract(a, b)
            | Multiply(a, b)
            | Divide(a, b) -> collect (collect names a) b
            | Call(_, arguments) -> List.fold collect names arguments

        {
            Expr = expr
            Names = collect Map.empty expr
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
            | ValueNone when id = "pi" -> Result.Ok UnitInfo.dimensionless
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
            | _, [ a ] when (pureFunction name).IsSome -> unitOf a |> Result.bind (pureNumber name)
            | ("min" | "max" | "hypot"), [ a; b ] -> both (unitOf a) (unitOf b) (oneUnit name)
            | "atan2", [ y; x ] ->
                both (unitOf y) (unitOf x) (oneUnit name) |> Result.map (fun _ -> UnitInfo.dimensionless)
            | _ -> Result.Error(outsideNotation name arguments)

    /// The unit of `expr` over the identifiers of `model`, or a message naming the operation and the units it met.
    /// A product, quotient, power or root whose scale is not finite or is 0 is an `Error` stating that unit's
    /// symbol, as "km^103". A unit literal has the unit `UnitInfo.parse` gives for its text over `model.Catalogue`, and
    /// an `Error` of that parse is prefixed with the literal ("The unit literal [xyz]: …"). `convert(e, [u])` has the
    /// unit u, or the `Error` of `UnitInfo.conversionFactor` from e's unit to u, which names both units: across
    /// dimensions, and when the quotient of the scales is not finite or is 0. `+`, `-`, `min`, `max`, `atan2` and
    /// `hypot` of two units that differ in scale alone are an `Error` that names both. `exp ln log10` and the
    /// trigonometric and hyperbolic functions give `UnitInfo.dimensionless` for a dimensionless argument of any scale,
    /// and an `Error` naming the function and the unit for an argument with a dimension. `pi` is
    /// `UnitInfo.dimensionless` when `model.TryFind "pi"` is `ValueNone`. Cost: one unit operation per node and one
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

    /// The scale of the operand's unit, from the units `check` gives.
    let private scaleIn (context: Context) (operand: Expr) : float = (checkIn context operand |> orRaise).Scale

    /// √(x² + y²) without the squares of the operands: m·√(1 + (n/m)²) with m = max(|x|, |y|) and n = min(|x|, |y|);
    /// 0 when m = 0, +∞ when either operand is infinite, NaN when either is NaN otherwise.
    let private hypot (x: float) (y: float) : float =
        let ax = Math.Abs x
        let ay = Math.Abs y

        if Double.IsInfinity ax || Double.IsInfinity ay then
            Double.PositiveInfinity
        elif Double.IsNaN ax || Double.IsNaN ay then
            nan
        else
            let m = Math.Max(ax, ay)

            if m = 0.0 then
                0.0
            else
                let r = Math.Min(ax, ay) / m
                m * Math.Sqrt(1.0 + r * r)

    /// ∂atan2(y, x)/∂y = x/(x² + y²), as (x/h)/h with h = hypot(x, y); NaN at the origin.
    let private atan2ByY (y: float) (x: float) : float =
        let h = hypot x y
        x / h / h

    /// ∂atan2(y, x)/∂x = −y/(x² + y²), as (−y/h)/h with h = hypot(x, y); NaN at the origin.
    let private atan2ByX (y: float) (x: float) : float =
        let h = hypot x y
        -y / h / h

    /// ∂hypot(x, y)/∂x = x/h with h = hypot(x, y); NaN at the origin.
    let private hypotByX (x: float) (y: float) : float = x / hypot x y

    /// ∂hypot(x, y)/∂y = y/h with h = hypot(x, y); NaN at the origin.
    let private hypotByY (x: float) (y: float) : float = y / hypot x y

    /// `x` to the integer power `n`, by repeated multiplication so both targets round alike.
    let private integerPower (x: float) (n: int) : float =
        let mutable product = 1.0

        for _ in 1 .. abs n do
            product <- product * x

        if n < 0 then 1.0 / product else product

    /// The evaluator's function of `expr` in `context`, with `reader` giving the function of each identifier that
    /// resolves to an entry, from the context, the identifier and the entry. `expr` is assumed to check.
    let private compile
        (reader: Context -> string -> Entry -> (unit -> float))
        (context: Context)
        (expr: Expr)
        : unit -> float =
        let rec build (e: Expr) : unit -> float =
            match e with
            | Number x -> fun () -> x
            | Name id ->
                match context.Find id with
                | ValueSome entry -> reader context id entry
                | ValueNone when id = "pi" -> fun () -> Math.PI
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
                | _, [ a ] when (pureFunction name).IsSome ->
                    let apply, _ = (pureFunction name).Value
                    let f = build a
                    let scale = scaleIn context a
                    fun () -> apply (f () * scale)
                | "min", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> Math.Min(f (), g ())
                | "max", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> Math.Max(f (), g ())
                | "atan2", [ y; x ] ->
                    let f, g = build y, build x
                    fun () -> Math.Atan2(f (), g ())
                | "hypot", [ x; y ] ->
                    let f, g = build x, build y
                    fun () -> hypot (f ()) (g ())
                | _ -> orRaise (Result.Error(outsideNotation name arguments))

        build expr

    let private evaluatorIn (context: Context) (expr: Expr) : unit -> float =
        checkIn context expr |> orRaise |> ignore
        compile (fun _ _ entry -> entry.Read) context expr

    /// A function that evaluates `expr` with tracked reads of the entries of `model`. A unit literal evaluates to 1,
    /// and `convert` multiplies its operand's value by `UnitInfo.conversionFactor` from the operand's unit to the
    /// literal's, computed when the evaluator is built. `exp ln log10` and the trigonometric and hyperbolic functions
    /// apply `Math.Exp`, `Math.Sin` and the rest to the argument's value times the scale `check` gives its unit. `pi`
    /// without an entry evaluates to `Math.PI`. `hypot(x, y)` is m·√(1 + (n/m)²) with m = max(|x|, |y|) and
    /// n = min(|x|, |y|): 0 when m = 0, +∞ when either operand is infinite, and NaN when either is NaN otherwise.
    /// Creates no node. Names resolve once, when the evaluator is created: an evaluator that outlives the owner of an
    /// entry it reads reads that entry's last value, or raises `InvalidOperationException` when the entry was removed
    /// before it was computed (`Model`'s remarks), and does not follow a later declaration of the identifier.
    /// Raises `ArgumentException` with the message of `check` when `check` of `expr` is an `Error`: for an identifier
    /// `model` lacks, for units that do not combine, for a unit literal that does not parse over `model.Catalogue`,
    /// and for a `Call` outside the notation (only a hand-built `Expr` holds one).
    /// Building costs one `check` of `expr`, one of each conversion's operand and one of each argument of `exp ln
    /// log10` and the trigonometric and hyperbolic functions.
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
                | ValueNone when id = "pi" ->
                    let pi = Uncertain.exact Math.PI
                    fun () -> pi
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
                | _, [ a ] when (pureFunction name).IsSome ->
                    let _, apply = (pureFunction name).Value
                    let f = build a
                    let scale = scaleIn context a
                    // u * 1.0 has u's bits; skipping it spares a copy of the contributions.
                    if scale = 1.0 then fun () -> apply (f ()) else fun () -> apply (f () * scale)
                | "min", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> operandOf (fun x y -> Math.Min(x, y)) (f ()) (g ())
                | "max", [ a; b ] ->
                    let f, g = build a, build b
                    fun () -> operandOf (fun x y -> Math.Max(x, y)) (f ()) (g ())
                | "atan2", [ y; x ] ->
                    let f, g = build y, build x
                    fun () -> Uncertain.map2 (fun y x -> Math.Atan2(y, x)) atan2ByY atan2ByX (f ()) (g ())
                | "hypot", [ x; y ] ->
                    let f, g = build x, build y
                    fun () -> Uncertain.map2 hypot hypotByX hypotByY (f ()) (g ())
                | _ -> orRaise (Result.Error(outsideNotation name arguments))

        build expr

    /// A function that evaluates `expr` as `evaluator` does, over tracked reads of each entry's `Entry.Measured`: the
    /// value has the evaluator's bits, with first-order contributions by source (`Uncertain`). A number and a unit
    /// literal are exact; `convert` multiplies its operand by the evaluator's conversion factor, which carries no
    /// uncertainty; `x^0` is 1 with derivative 0 at every x, 0 included; `min` and `max` give the operand whose value
    /// `Math.Min` and `Math.Max` give, the first when both have it, and an exact NaN when either value is NaN. A
    /// function of a dimensionless argument multiplies the argument by its unit's scale and applies the function's
    /// `Uncertain` member (`Uncertain.Sin` and the rest); `pi` without an entry is exact. `atan2(y, x)` has the
    /// derivatives x/(x² + y²) in y and −y/(x² + y²) in x, and `hypot(x, y)` x/h in x and y/h in y with
    /// h = hypot(x, y); all four are NaN at the origin. Each operation costs O(m + n) in its operands' numbers of
    /// sources. Creates no node. Names resolve once, when the evaluator is created: an evaluator that outlives the
    /// owner of an entry it reads reads that entry's last measured value, or raises as `evaluator` does for an entry
    /// removed before it was computed, and does not follow a later declaration of the identifier. Raises
    /// `ArgumentException` as `evaluator` does, and building costs what it does there.
    let measuredEvaluator (model: Model) (expr: Expr) : unit -> Uncertain = measuredEvaluatorIn (contextOf model) expr

    let private undefined (draws: int) : MonteCarloResult =
        {
            Mean = nan
            StandardDeviation = nan
            Lower = nan
            Upper = nan
            Draws = draws
        }

    /// A reader of the distribution of `expr` over the entries of `model`, by propagation of distributions (the GUM's
    /// Supplement 1, general knowledge). The formula entries `expr` reaches are inlined, each with the names it
    /// resolved at its declaration, down to leaves that are not formula entries; a leaf reached twice is one leaf.
    /// Each read takes a tracked read of every leaf's `Entry.Measured`, and with a NaN value or contribution among them
    /// gives NaN in every float field. Otherwise each of the `draws` draws takes one standard normal deviate zᵢ per
    /// source the leaves hold, in ascending id order, from `Generator.create seed` threaded through the draws; sets
    /// each leaf to its value plus Σᵢ cᵢzᵢ over its contributions cᵢ; and evaluates the inlined expression as
    /// `evaluator` does, with its conversion factors and scales. An input or constant leaf is then normal at its value
    /// and standard uncertainty, and leaves that share a source move together, linearly in it. `Mean` and
    /// `StandardDeviation` (divisor n − 1) are those of the compensated `Moments` of the draws; `Lower` and `Upper`
    /// are `OrderStatistic.quantileSorted` of the sorted draws at (1 − coverage)/2 and (1 + coverage)/2, the
    /// probabilistically symmetric coverage interval. A draw that evaluates to NaN gives NaN in every float field.
    /// `Draws` is `draws`. Two reads with equal leaf values and contributions give equal records.
    ///
    /// The reader creates no node and evaluates inside the node that reads it. A memo over it publishes its record on
    /// every recomputation, since the core's default equality compares a record by reference. The reader holds its
    /// leaves' values of the current draw, so reads of one reader run one at a time. Names resolve once, when
    /// the reader is created: a reader that outlives the owner of a leaf reads that leaf's last measured value, or
    /// raises as `evaluator` does for a leaf removed before it was computed, and evaluates a removed formula entry it
    /// inlines from its leaves, not from the entry's last value; it does not follow a later declaration of the
    /// identifier. A read costs O(draws · (sources + Σ leaf
    /// contributions + size of the inlined expression)) plus a sort of the draws, and allocates an array of the draws.
    /// Creating the reader costs what building `evaluator` does for `expr` and for each inlined formula, and O(l²) in
    /// the number l of leaves. Raises `ArgumentOutOfRangeException` for fewer than 2 draws and for a coverage outside
    /// (0, 1), NaN included; then `ArgumentException` as `evaluator` does. A read raises as a leaf's `Entry.Measured`
    /// does.
    let monteCarlo (draws: int) (seed: int) (coverage: float) (model: Model) (expr: Expr) : unit -> MonteCarloResult =
        if draws < 2 then
            raise (ArgumentOutOfRangeException("draws", "A Monte Carlo propagation takes at least 2 draws."))

        if not (coverage > 0.0 && coverage < 1.0) then
            raise (ArgumentOutOfRangeException("coverage", "A coverage must be strictly between 0 and 1, and not NaN."))

        let context = contextOf model
        checkIn context expr |> orRaise |> ignore
        let leaves = ResizeArray<Entry>()
        // The value of each leaf in the current draw, at the leaf's index in `leaves`.
        let slots = ResizeArray<float>()

        let slotOf (entry: Entry) : int =
            let mutable k = 0

            while k < leaves.Count && not (obj.ReferenceEquals(leaves[k], entry)) do
                k <- k + 1

            if k = leaves.Count then
                leaves.Add entry
                slots.Add 0.0

            k

        let rec leafOrInlined (c: Context) (id: string) (entry: Entry) : unit -> float =
            match c.Record id with
            | ValueSome record -> compile leafOrInlined (contextOfRecord record) record.Expr
            | ValueNone ->
                let k = slotOf entry
                fun () -> slots[k]

        let evaluate = compile leafOrInlined context expr
        let leafEntries = leaves.ToArray()
        let count = leafEntries.Length

        fun () ->
            let values: float[] = Array.zeroCreate count
            let contributions: struct (int * float)[][] = Array.zeroCreate count
            let mutable known = true

            for k in 0 .. count - 1 do
                let u = leafEntries[k].Measured()
                let cs = Uncertain.contributions u
                values[k] <- Uncertain.value u
                contributions[k] <- cs

                if Double.IsNaN values[k] || cs |> Array.exists (fun struct (_, c) -> Double.IsNaN c) then
                    known <- false

            if not known then
                undefined draws
            else
                let ids =
                    contributions
                    |> Array.collect (Array.map (fun struct (id, _) -> id))
                    |> Array.distinct
                    |> Array.sort

                let position = Dictionary<int, int>()
                ids |> Array.iteri (fun i id -> position[id] <- i)
                let positions = contributions |> Array.map (Array.map (fun struct (id, _) -> position[id]))
                let weights = contributions |> Array.map (Array.map (fun struct (_, c) -> c))
                let z: float[] = Array.zeroCreate ids.Length
                let results: float[] = Array.zeroCreate draws
                let mutable generator = Generator.create seed
                let mutable moments = Moments.zero
                let mutable defined = true

                for d in 0 .. draws - 1 do
                    for i in 0 .. z.Length - 1 do
                        let struct (deviate, next) = Generator.normal generator
                        z[i] <- deviate
                        generator <- next

                    for k in 0 .. count - 1 do
                        let p = positions[k]
                        let w = weights[k]
                        let mutable shift = 0.0

                        for j in 0 .. p.Length - 1 do
                            shift <- shift + w[j] * z[p[j]]

                        slots[k] <- values[k] + shift

                    let y = evaluate ()

                    if Double.IsNaN y then
                        defined <- false

                    results[d] <- y
                    moments <- Moments.add true moments y

                if not defined then
                    undefined draws
                else
                    Array.sortInPlace results

                    {
                        Mean = Moments.mean moments
                        StandardDeviation = Moments.sampleStandardDeviation moments
                        Lower = OrderStatistic.quantileSorted ((1.0 - coverage) / 2.0) results
                        Upper = OrderStatistic.quantileSorted ((1.0 + coverage) / 2.0) results
                        Draws = draws
                    }
