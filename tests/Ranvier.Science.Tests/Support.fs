[<AutoOpen>]
module Ranvier.Science.Tests.Support

open System
open Ranvier

/// <summary>Runs <c>body</c> with a fresh graph active, and disposes the graph afterwards.</summary>
let withGraph (body: unit -> unit) : unit =
    use g = new Graph ()
    use _ = g.Activate ()
    body ()

/// <summary>
/// The distance between <c>a</c> and <c>b</c> in units in the last place of the larger magnitude: <c>|a - b|</c>
/// over the spacing of doubles at <c>max |a| |b|</c>. Zero for equal values, NaN when either is NaN, and +infinity when the values differ and the larger magnitude is infinite.
/// </summary>
let ulps (a: float) (b: float) : float =
    let largest = max (abs a) (abs b)

    if Double.IsNaN a || Double.IsNaN b then
        nan
    elif a = b then
        0.0
    elif Double.IsInfinity largest then
        infinity
    else
        // The exponent of the binade holding `largest`, corrected for rounding of the logarithm.
        let mutable e = Math.Floor (Math.Log largest / Math.Log 2.0)

        while Math.Pow (2.0, e) > largest do
            e <- e - 1.0

        while Math.Pow (2.0, e + 1.0) <= largest do
            e <- e + 1.0

        abs (a - b) / Math.Pow (2.0, max (e - 52.0) -1074.0)
