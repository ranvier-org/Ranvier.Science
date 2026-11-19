module Ranvier.Science.Tests.ToleranceTests

open Expecto
open Ranvier
open Ranvier.Numerics

[<Tests>]
let tests =
    testList
        "Tolerance"
        [
            test "within is measured from the previous value" {
                let abs1 = Tolerance.absolute 1.0
                Expect.isTrue (Tolerance.within abs1 0.0 1.0) "absolute: a step of exactly 1.0 is within"
                Expect.isFalse (Tolerance.within abs1 0.0 1.0000001) "absolute: a step beyond 1.0 is not within"
                let rel1 = Tolerance.relative 0.01
                Expect.isTrue (Tolerance.within rel1 100.0 101.0) "relative: 1% of 100 is within"
                Expect.isFalse (Tolerance.within rel1 100.0 101.1) "relative: beyond 1% of 100 is not within"
            }

            test "NaN and infinity" {
                let t = Tolerance.absolute 1.0
                Expect.isTrue (Tolerance.within t nan nan) "two NaNs are within"
                Expect.isTrue (Tolerance.within t infinity infinity) "equal infinities are within"
                Expect.isFalse (Tolerance.within t 1.0 nan) "a NaN against a number is not within"
                Expect.isFalse (Tolerance.within t nan 1.0) "a number against a NaN is not within"
                Expect.isFalse (Tolerance.within t infinity -infinity) "opposite infinities are not within"
                Expect.isFalse (Tolerance.within t 1.0 infinity) "a number against an infinity is not within"
            }

            test "small steps cannot accumulate unseen" {
                withGraph (fun () ->
                    let a = createSignal 0.0
                    let m = createMemo (fun prev -> Tolerance.hold (Tolerance.absolute 1.0) prev a.Value)
                    let seen = ResizeArray<float>()
                    createEffect (fun () -> seen.Add m.Value)

                    for x in [ 0.4; 0.8; 1.2; 1.6; 2.0; 2.4 ] do
                        a.Value <- x

                    Expect.sequenceEqual seen [ 0.0; 1.2; 2.4 ] "readers see a value only when it is more than 1.0 from the last one they saw")
            }

            test "holdSeries returns the same array instance" {
                let t = Tolerance.absolute 0.5
                let a = [| 1.0; 2.0; 3.0 |]
                let near = [| 1.2; 2.0; 2.8 |]
                let far = [| 1.2; 2.0; 3.6 |]
                let shorter = [| 1.0; 2.0 |]
                Expect.isTrue (obj.ReferenceEquals(Tolerance.holdSeries t (ValueSome a) near, a)) "every element within returns the previous array"
                Expect.isTrue (obj.ReferenceEquals(Tolerance.holdSeries t (ValueSome a) far, far)) "one element beyond returns the next array"
                Expect.isTrue (obj.ReferenceEquals(Tolerance.holdSeries t (ValueSome a) shorter, shorter)) "a different length returns the next array"
                Expect.isTrue (obj.ReferenceEquals(Tolerance.holdSeries t ValueNone near, near)) "no previous array returns the next array"
            }

            test "a negative bound is rejected" {
                Expect.throwsT<System.ArgumentOutOfRangeException> (fun () -> Tolerance.absolute -1.0 |> ignore) "negative absolute"
                Expect.throwsT<System.ArgumentOutOfRangeException> (fun () -> Tolerance.relative nan |> ignore) "NaN relative"
            }
        ]
