module Ranvier.Science.Tests.OrderStatisticTests

open System
open Expecto
open Ranvier.Numerics

// Reference values are closed forms of the type-7 quantile written in each test: h = (n − 1)p, f = h − ⌊h⌋, and the
// value (1 − f)·x[⌊h⌋] + f·x[⌊h⌋ + 1] over the 0-based sorted array.

[<Tests>]
let tests =
    testList
        "OrderStatistic"
        [
            test "the type-7 quantile" {
                let three = [| 1.0; 2.0; 3.0 |]
                // n = 3: h = 2p.
                Expect.equal (OrderStatistic.quantileSorted 0.0 three) 1.0 "p = 0: h = 0, x[0]"
                Expect.equal (OrderStatistic.quantileSorted 1.0 three) 3.0 "p = 1: h = 2, x[2]"
                Expect.equal (OrderStatistic.quantileSorted 0.5 three) 2.0 "p = 0.5: h = 1, x[1]"
                let four = [| 1.0; 2.0; 3.0; 4.0 |]
                // n = 4: h = 3p.
                Expect.equal (OrderStatistic.quantileSorted 0.5 four) 2.5 "p = 0.5: h = 1.5, 0.5 · 2 + 0.5 · 3"
                Expect.equal (OrderStatistic.quantileSorted 0.25 four) 1.75 "p = 0.25: h = 0.75, 0.25 · 1 + 0.75 · 2"
            }

            test "edge cases" {
                for p in [ 0.0; 0.3; 1.0 ] do
                    // n = 1: h = 0 for every p.
                    Expect.equal (OrderStatistic.quantileSorted p [| 7.5 |]) 7.5 (sprintf "one element at p = %g" p)

                Expect.isTrue (Double.IsNaN(OrderStatistic.quantileSorted 0.5 [||])) "the empty array gives NaN"
            }

            test "arguments" {
                for p in [ -0.1; 1.1; nan ] do
                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> OrderStatistic.quantileSorted p [| 1.0; 2.0 |] |> ignore)
                        (sprintf "p = %g" p)
            }
        ]
