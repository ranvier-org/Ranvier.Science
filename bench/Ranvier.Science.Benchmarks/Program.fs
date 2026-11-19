module Ranvier.Science.Benchmarks.Program

open System
open BenchmarkDotNet.Running

let private bits (x: float) = BitConverter.DoubleToInt64Bits x

/// Ten inputs per body, spread over the range of the page's control.
let private checkInputs (name: string) =
    match name with
    | "rate" -> [ for i in 0..9 -> 150.0 + 50.0 * float i ]
    | "kepler" -> [ for i in 0..9 -> 0.6 * float i ]
    | "rigidStep" -> [ for i in 0..9 -> 0.5 + 1.0 * float i ]
    | _ -> [ for i in 0..9 -> 250.0 + 35.0 * float i ]

/// Calls the five paths of every body on ten inputs, twice each, and returns the mismatches against the direct call.
/// The first call of every path is on x. The second call of the alternating paths is on x + the case's spacing, so a
/// KitMemo key and a signal value really change.
let private check () =
    let failures = ResizeArray<string>()

    for case in NodeCost.cases do
        for x in checkInputs case.Name do
            let y = x + (case.Second - case.First)
            use paths = new NodeCost.Paths ({ case with First = x; Second = y })
            let expectX = case.Body x
            let expectY = case.Body y

            let expect (label: string) (expected: float) (actual: float) =
                if bits expected <> bits actual then
                    failures.Add $"{case.Name} x={x} {label}: expected {expected:R}, got {actual:R}"

            expect "Direct" expectX (paths.Direct ())
            expect "KitUnchanged" expectX (paths.KitUnchanged ())
            expect "KitChanged" expectX (paths.KitChanged ())
            expect "NodeCached" expectX (paths.NodeCached ())
            expect "NodeWritten" expectX (paths.NodeWritten ())
            // Second round: the alternating paths change input, the fixed ones repeat a hit.
            expect "Direct (changed)" expectY (paths.Direct ())
            expect "KitUnchanged (hit)" expectX (paths.KitUnchanged ())
            expect "KitChanged (changed key)" expectY (paths.KitChanged ())
            expect "NodeCached (hit)" expectX (paths.NodeCached ())
            expect "NodeWritten (changed write)" expectY (paths.NodeWritten ())

    failures

[<EntryPoint>]
let main argv =
    match argv |> Array.toList with
    | "--check" :: _ ->
        let failures = check ()

        if failures.Count = 0 then
            printfn "check: 4 bodies x 10 inputs x 5 paths equal"
            0
        else
            for f in failures do
                eprintfn "%s" f

            1
    | args ->
        // `--body <name>` restricts the run to one body's five rows; the default is all four bodies.
        let args =
            match args |> List.tryFindIndex ((=) "--body") with
            | Some i when i + 1 < args.Length ->
                NodeCost.selectedBodies <- [ args[i + 1] ]
                args |> List.indexed |> List.filter (fun (j, _) -> j <> i && j <> i + 1) |> List.map snd
            | _ -> args

        BenchmarkSwitcher.FromAssembly(typeof<NodeCost.NodeCostBenchmarks>.Assembly).Run(List.toArray args)
        |> ignore

        0
