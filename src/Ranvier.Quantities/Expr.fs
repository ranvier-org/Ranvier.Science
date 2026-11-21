namespace Ranvier.Quantities

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
