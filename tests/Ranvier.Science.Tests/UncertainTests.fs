module Ranvier.Science.Tests.UncertainTests

open System
open Expecto
open Ranvier.Numerics

let private rel (a: float) (b: float) = abs (a - b) / abs b

let private within (tolerance: float) (actual: float) (expected: float) (message: string) =
    Expect.isTrue (abs (actual - expected) <= tolerance) (sprintf "%s: %.17g within %g of %.17g" message actual tolerance expected)

/// The id and the contribution of a value with exactly one source.
let private single (u: Uncertain) : struct (int * float) =
    match Uncertain.contributions u with
    | [| one |] -> one
    | other -> failtestf "expected one contribution, got %A" other

let private idOf (u: Uncertain) : int =
    let struct (id, _) = single u
    id

let private contributionOf (u: Uncertain) : float =
    let struct (_, c) = single u
    c

// Fable 5.27 miscompiles an array pattern over struct tuples, so the tests project the entries instead.
let private sourcesOf (u: Uncertain) : int[] =
    Uncertain.contributions u |> Array.map (fun struct (id, _) -> id)

let private amountsOf (u: Uncertain) : float[] =
    Uncertain.contributions u |> Array.map (fun struct (_, c) -> c)

#if !FABLE_COMPILER
let private dotNetTests =
    [
        test ".NET: F#'s abs gives Uncertain.abs" {
            for v in [ 0.5; 0.0; -0.0; -2.0 ] do
                let u = Uncertain.source 0.1 v
                Expect.equal (abs u) (Uncertain.abs u) (sprintf "at %g" v)
        }

        test ".NET: sources made on two threads are distinct" {
            let make () = Array.init 10_000 (fun _ -> Uncertain.source 1.0 0.0)
            let made = Array.zeroCreate<Uncertain[]> 3

            let others =
                [|
                    for k in 0..1 ->
                        Threading.Tasks.Task.Run(Action (fun () -> offThread (fun () -> made[k] <- make ())))
                |]

            made[2] <- make ()
            Threading.Tasks.Task.WaitAll others
            let ids = made |> Array.collect (Array.map idOf) |> Array.distinct
            Expect.equal ids.Length 30_000 "30,000 distinct ids"
        }
    ]
#else
let private dotNetTests = []
#endif

[<Tests>]
let tests =
    testList
        "Uncertain"
        ([
            test "an exact value has no uncertainty" {
                let e = Uncertain.exact 2.5
                Expect.equal (Uncertain.value e) 2.5 "value"
                Expect.equal (Uncertain.standardUncertainty e) 0.0 "standard uncertainty"
                Expect.isEmpty (Uncertain.contributions e) "contributions"
            }

            test "each source is new and independent" {
                let a = Uncertain.source 0.3 1.0
                let b = Uncertain.source 0.3 1.0
                // sqrt (fl (u * u)) = u for a binary float without overflow or underflow.
                Expect.equal (Uncertain.standardUncertainty a) 0.3 "a's standard uncertainty"
                Expect.equal (Uncertain.standardUncertainty b) 0.3 "b's standard uncertainty"
                Expect.equal (contributionOf a) 0.3 "a's one contribution"
                Expect.equal (contributionOf b) 0.3 "b's one contribution"
                Expect.notEqual (idOf a) (idOf b) "ids differ"
                Expect.notEqual a b "a <> b"
                Expect.equal (Uncertain.covariance a b) 0.0 "covariance"
                Expect.equal (Uncertain.correlation a b) 0.0 "correlation"
            }

            test "independent contributions add in quadrature" {
                let a = Uncertain.source 3.0 10.0
                let b = Uncertain.source 4.0 20.0
                // sqrt (3² + 4²) = 5.
                Expect.equal (Uncertain.value (a + b)) 30.0 "a + b value"
                Expect.equal (Uncertain.standardUncertainty (a + b)) 5.0 "a + b standard uncertainty"
                Expect.equal (Uncertain.value (a - b)) -10.0 "a - b value"
                Expect.equal (Uncertain.standardUncertainty (a - b)) 5.0 "a - b standard uncertainty"
                let idA = idOf a
                let idB = idOf b
                Expect.isTrue (idA < idB) "the earlier source has the smaller id"
                Expect.equal (Uncertain.contributions (a + b)) [| struct (idA, 3.0); struct (idB, 4.0) |] "contributions"
            }

            test "a shared source cancels" {
                let a = Uncertain.source 3.0 10.0
                Expect.equal (Uncertain.value (a - a)) 0.0 "a - a value"
                Expect.equal (Uncertain.standardUncertainty (a - a)) 0.0 "a - a standard uncertainty"
                Expect.equal (Uncertain.contributions (a - a)) [| struct (idOf a, 0.0) |] "the cancelled contribution stays listed"
                // 3 + 3 = 6.
                Expect.equal (Uncertain.standardUncertainty (a + a)) 6.0 "a + a standard uncertainty"
                within 1e-15 (Uncertain.correlation a (2.0 * a)) 1.0 "correlation with 2a"
                within 1e-15 (Uncertain.correlation a (-a)) -1.0 "correlation with -a"
            }

            test "covariance of combinations" {
                let a = Uncertain.source 3.0 10.0
                let b = Uncertain.source 4.0 20.0
                // (3)(3) + (4)(−4) = 3² − 4² = −7; each side has standard uncertainty 5.
                Expect.equal (Uncertain.covariance (a + b) (a - b)) -7.0 "covariance"
                within 1e-15 (Uncertain.correlation (a + b) (a - b)) (-7.0 / 25.0) "correlation"
            }

            test "a source held by both operands merges into one entry" {
                let a = Uncertain.source 3.0 10.0
                let b = Uncertain.source 4.0 20.0
                let c = Uncertain.source 5.0 30.0
                let idA, idB, idC = idOf a, idOf b, idOf c
                // ∂/∂a = 2 and ∂/∂b = 1: 2 · 3 = 6 and 4.
                Expect.equal (Uncertain.contributions ((a + b) + a)) [| struct (idA, 6.0); struct (idB, 4.0) |] "(a + b) + a"

                // ∂/∂a = 1 − 1 = 0 and ∂/∂b = 1 + 1 = 2: 0 and 2 · 4 = 8.
                Expect.equal
                    (Uncertain.contributions ((a + b) - (a - b)))
                    [| struct (idA, 0.0); struct (idB, 8.0) |]
                    "(a + b) - (a - b)"

                // ∂/∂a = 1, ∂/∂b = 1 and ∂/∂c = 2: 3, 4 and 2 · 5 = 10, in id order.
                Expect.equal
                    (Uncertain.contributions ((a + c) + (b + c)))
                    [| struct (idA, 3.0); struct (idB, 4.0); struct (idC, 10.0) |]
                    "(a + c) + (b + c)"

                // Both sides are a + b + c: value 60, contributions 3, 4 and 5 in id order.
                Expect.equal ((c + a) + b) (a + (b + c)) "(c + a) + b = a + (b + c)"
            }

            test "products and quotients" {
                let x = Uncertain.source 0.1 3.0
                let y = Uncertain.source 0.2 4.0
                // ∂(xy)/∂x = y, ∂(xy)/∂y = x; ∂(x/y)/∂x = 1/y, ∂(x/y)/∂y = −x/y².
                Expect.equal (Uncertain.value (x * y)) 12.0 "x * y value"
                // y · 0.1 = 0.4 and x · 0.2 = 0.6.
                Expect.equal (sourcesOf (x * y)) [| idOf x; idOf y |] "x * y sources"
                let product = amountsOf (x * y)
                within 1e-15 product[0] 0.4 "x * y: x's contribution"
                within 1e-15 product[1] 0.6 "x * y: y's contribution"

                Expect.isTrue
                    (rel (Uncertain.standardUncertainty (x * y)) (sqrt ((0.1 * 4.0) ** 2.0 + (0.2 * 3.0) ** 2.0))
                     <= 1e-15)
                    "x * y standard uncertainty within relative 1e-15"

                Expect.equal (Uncertain.value (x / y)) 0.75 "x / y value"
                // 0.1 / y = 0.025 and −(x / y²) · 0.2 = −(3 / 16) · 0.2 = −0.0375.
                Expect.equal (sourcesOf (x / y)) [| idOf x; idOf y |] "x / y sources"
                let quotient = amountsOf (x / y)
                within 1e-15 quotient[0] 0.025 "x / y: x's contribution"
                within 1e-15 quotient[1] -0.0375 "x / y: y's contribution"

                within
                    1e-15
                    (Uncertain.standardUncertainty (x / y))
                    (sqrt ((0.1 / 4.0) ** 2.0 + (0.2 * 3.0 / 16.0) ** 2.0))
                    "x / y standard uncertainty"
            }

            test "floats on either side" {
                let x = Uncertain.source 0.1 3.0
                Expect.equal (2.0 * x) (x * 2.0) "2.0 * x = x * 2.0"
                // 2 · 3 = 6 and 2 · 0.1 = 0.2.
                Expect.equal (Uncertain.value (2.0 * x)) 6.0 "2.0 * x value"
                Expect.equal (contributionOf (2.0 * x)) 0.2 "2.0 * x contribution"
                Expect.equal (Uncertain.value (x + 1.0)) 4.0 "x + 1.0 value"
                Expect.equal (Uncertain.contributions (x + 1.0)) (Uncertain.contributions x) "x + 1.0 keeps x's contribution"
                Expect.equal (Uncertain.value (1.0 + x)) 4.0 "1.0 + x value"
                Expect.equal (Uncertain.contributions (1.0 + x)) (Uncertain.contributions x) "1.0 + x keeps x's contribution"
                Expect.equal (Uncertain.value (x - 1.0)) 2.0 "x - 1.0 value"
                Expect.equal (Uncertain.contributions (x - 1.0)) (Uncertain.contributions x) "x - 1.0 keeps x's contribution"
                Expect.equal (Uncertain.value (1.0 - x)) -2.0 "1.0 - x value"
                Expect.equal (contributionOf (1.0 - x)) -0.1 "1.0 - x contribution"
                // ∂(1/x)/∂x = −1/x².
                Expect.equal (Uncertain.value (1.0 / x)) (1.0 / 3.0) "1.0 / x value"
                within 1e-15 (contributionOf (1.0 / x)) (-0.1 / 9.0) "1.0 / x contribution"
                Expect.equal (contributionOf (x / 2.0)) 0.05 "x / 2.0 contribution"
                Expect.equal (Uncertain.value (-x)) -3.0 "-x value"
                Expect.equal (contributionOf (-x)) -0.1 "-x contribution"
                Expect.equal (idOf (-x)) (idOf x) "-x keeps x's source"
            }

            test "the F# functions apply" {
                let z = Uncertain.source 0.01 0.5

                // Each derivative is the closed form the member's doc comment states.
                let cases: (string * (Uncertain -> Uncertain) * (float -> float) * (float -> float)) list =
                    [
                        "sqrt", (fun (u: Uncertain) -> sqrt u), sqrt, (fun x -> 1.0 / (2.0 * sqrt x))
                        "exp", (fun (u: Uncertain) -> exp u), exp, exp
                        "log", (fun (u: Uncertain) -> log u), log, (fun x -> 1.0 / x)
                        "log10", (fun (u: Uncertain) -> log10 u), log10, (fun x -> 1.0 / (x * log 10.0))
                        "sin", (fun (u: Uncertain) -> sin u), sin, cos
                        "cos", (fun (u: Uncertain) -> cos u), cos, (fun x -> -(sin x))
                        "tan", (fun (u: Uncertain) -> tan u), tan, (fun x -> 1.0 + tan x * tan x)
                        "asin", (fun (u: Uncertain) -> asin u), asin, (fun x -> 1.0 / sqrt (1.0 - x * x))
                        "acos", (fun (u: Uncertain) -> acos u), acos, (fun x -> -1.0 / sqrt (1.0 - x * x))
                        "atan", (fun (u: Uncertain) -> atan u), atan, (fun x -> 1.0 / (1.0 + x * x))
                        "sinh", (fun (u: Uncertain) -> sinh u), sinh, cosh
                        "cosh", (fun (u: Uncertain) -> cosh u), cosh, sinh
                        "tanh", (fun (u: Uncertain) -> tanh u), tanh, (fun x -> 1.0 - tanh x * tanh x)
                        "Uncertain.abs", Uncertain.abs, abs, (fun x -> if x < 0.0 then -1.0 else 1.0)
                    ]

                for name, f, g, dg in cases do
                    let r = f z
                    Expect.equal (Uncertain.value r) (g 0.5) (sprintf "%s value" name)
                    Expect.equal (idOf r) (idOf z) (sprintf "%s keeps z's source" name)
                    let expected = dg 0.5 * 0.01

                    Expect.isTrue
                        (rel (contributionOf r) expected <= 1e-14)
                        (sprintf "%s contribution %.17g within relative 1e-14 of %.17g" name (contributionOf r) expected)
            }

            test "abs at zero keeps the magnitude" {
                Expect.equal (contributionOf (Uncertain.abs (Uncertain.source 0.1 0.0))) 0.1 "at 0"
                Expect.equal (contributionOf (Uncertain.abs (Uncertain.source 0.1 -0.0))) 0.1 "at -0"
                Expect.equal (contributionOf (Uncertain.abs (Uncertain.source 0.1 -2.0))) -0.1 "at -2"
            }

            test "powers" {
                let p = Uncertain.source 0.1 2.0
                let q = Uncertain.source 0.2 3.0
                // ∂(x^y)/∂x = y·x^(y−1), ∂(x^y)/∂y = x^y·ln x.
                Expect.equal (Uncertain.value (p ** 3.0)) 8.0 "p ** 3.0 value"
                within 1e-14 (contributionOf (p ** 3.0)) (3.0 * 2.0 ** 2.0 * 0.1) "p ** 3.0 contribution"
                let pq = p ** q
                Expect.equal (Uncertain.value pq) 8.0 "p ** q value"
                Expect.equal (sourcesOf pq) [| idOf p; idOf q |] "p's source, then q's"
                let c = amountsOf pq
                within 1e-14 c[0] (3.0 * 2.0 ** 2.0 * 0.1) "p's contribution"
                within 1e-14 c[1] (8.0 * log 2.0 * 0.2) "q's contribution"
            }

            test "map and map2" {
                let x = Uncertain.source 0.1 3.0
                let y = Uncertain.source 0.2 4.0
                // d(v²)/dv = 2v: 2 · 3 · 0.1 = 0.6.
                let squared = Uncertain.map (fun v -> v * v) (fun v -> 2.0 * v) x
                Expect.equal (Uncertain.value squared) 9.0 "map value"
                within 1e-15 (contributionOf squared) 0.6 "map contribution"
                let product = Uncertain.map2 (*) (fun _ b -> b) (fun a _ -> a) x y

                within
                    1e-15
                    (Uncertain.standardUncertainty product)
                    (Uncertain.standardUncertainty (x * y))
                    "map2 standard uncertainty"
            }

            test "a NaN standard uncertainty propagates" {
                let n = Uncertain.source nan 1.0
                let x = Uncertain.source 0.1 3.0
                Expect.isTrue (Double.IsNaN (Uncertain.standardUncertainty n)) "n's standard uncertainty"
                Expect.equal (Uncertain.value (n + x)) 4.0 "n + x value"
                Expect.isTrue (Double.IsNaN (Uncertain.standardUncertainty (n + x))) "n + x standard uncertainty"
                Expect.isTrue (Double.IsNaN (Uncertain.correlation n x)) "correlation"
            }

            test "a negative standard uncertainty is rejected" {
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Uncertain.source -0.1 1.0 |> ignore) "source -0.1"
            }

            test "equal arithmetic gives equal values" {
                let x = Uncertain.source 0.1 3.0
                Expect.equal (x + 1.0) (x + 1.0) "x + 1.0 = x + 1.0"
                Expect.notEqual (Uncertain.source 0.1 1.0) (Uncertain.source 0.1 1.0) "two sources differ"
            }
        ]
         @ dotNetTests)
