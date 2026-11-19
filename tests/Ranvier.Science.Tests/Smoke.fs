module Ranvier.Science.Tests.Smoke

open Expecto
open Ranvier

[<Tests>]
let tests =
    testList "Smoke" [
        test "a memo follows its signal" {
            withGraph (fun () ->
                let a = createSignal 2.0
                let double = createMemo (fun _ -> a.Value * 2.0)
                Expect.equal double.Value 4.0 "first read"
                a.Value <- 5.0
                Expect.equal double.Value 10.0 "after a write")
        }
        test "ulps counts units in the last place" {
            let epsilon = 2.220446049250313e-16
            let big = System.Math.Pow (2.0, 100.0)
            Expect.equal (ulps 1.0 1.0) 0.0 "equal values"
            Expect.equal (ulps 1.0 (1.0 + epsilon)) 1.0 "one step above 1"
            Expect.equal (ulps big (big + 3.0 * System.Math.Pow (2.0, 48.0))) 3.0 "three steps at 2^100"
            Expect.equal (ulps 1.0 infinity) infinity "finite against infinity"
            Expect.equal (ulps infinity -infinity) infinity "opposite infinities"
            Expect.equal (ulps infinity infinity) 0.0 "equal infinities"
            Expect.isTrue (System.Double.IsNaN (ulps 1.0 nan)) "NaN against finite"
            Expect.isTrue (System.Double.IsNaN (ulps nan nan)) "NaN against NaN"
        }
    ]
