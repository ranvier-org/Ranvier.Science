[<AutoOpen>]
module Ranvier.Science.Tests.Bounds

open System
open Expecto

/// Passes when actual = expected (equal infinities included), when both are NaN, or when both are finite and
/// |actual − expected| ≤ max(relative · |expected|, absolute). The failure message prints both values with %.17g.
let expectClose (relative: float) (absolute: float) (expected: float) (actual: float) (message: string) : unit =
    let close =
        if actual = expected then
            true
        elif Double.IsNaN expected || Double.IsNaN actual then
            Double.IsNaN expected && Double.IsNaN actual
        elif Double.IsInfinity expected || Double.IsInfinity actual then
            false
        else
            abs (actual - expected) <= max (relative * abs expected) absolute

    if not close then
        failtestf
            "%s. Expected %.17g, actual %.17g: |difference| %.3g, bound max(%g · |expected|, %g)"
            message
            expected
            actual
            (abs (actual - expected))
            relative
            absolute

/// The Ok value; fails the test with the Error's text otherwise.
let expectOk (message: string) (result: Result<'T, string>) : 'T =
    match result with
    | Ok value -> value
    | Error text -> failtestf "%s. Expected Ok, got Error: %s" message text

/// Passes when the result is an Error whose text contains `part`.
let expectError (part: string) (message: string) (result: Result<'T, string>) : unit =
    match result with
    | Error text ->
        if not (text.Contains part) then
            failtestf "%s. The error %A does not contain %A" message text part
    | Ok value -> failtestf "%s. Expected an Error containing %A, got Ok %A" message part value
