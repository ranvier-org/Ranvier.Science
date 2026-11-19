module Ranvier.Science.Tests.MonitorPartsTests

open Expecto
open Ranvier
open Ranvier.Numerics
open SequentialMonitor

/// Moments of `count` observations of 1.0, built by merging in O(log count) steps.
let rec private ofCount (count: float) : Moments =
    if count = 1.0 then
        Moments.add true Moments.zero 1.0
    else
        let half = ofCount (floor (count / 2.0))
        let doubled = Moments.merge true half half

        if count - 2.0 * floor (count / 2.0) = 1.0 then
            Moments.add true doubled 1.0
        else
            doubled

let private nextSamplingCount (c: float) = max (c + 1.0) (ceil (c * 1.05))

[<Tests>]
let tests =
    testList
        "MonitorParts"
        [
            test "fixed rounds half away from zero" {
                Expect.equal (Format.fixed' 2 0.84) "0.84" "0.84"
                Expect.equal (Format.fixed' 2 -0.016) "-0.02" "-0.016"
                Expect.equal (Format.fixed' 2 -0.004) "0.00" "-0.004 rounds to zero and loses its sign"
                Expect.equal (Format.fixed' 1 9.25) "9.3" "9.25"
                Expect.equal (Format.fixed' 0 2.5) "3" "2.5"
                Expect.equal (Format.fixed' 0 -2.5) "-3" "-2.5"
                Expect.equal (Format.fixed' 2 1000000000.125) "1000000000.13" "1000000000.125"
            }

            test "fixed names the values it cannot write" {
                Expect.equal (Format.fixed' 2 nan) "NaN" "NaN"
                Expect.equal (Format.fixed' 2 infinity) "∞" "infinity"
                Expect.equal (Format.fixed' 2 -infinity) "-∞" "negative infinity"
            }

            test "count groups thousands" {
                Expect.equal (Format.count 999.0) "999" "999"
                Expect.equal (Format.count 12400.0) "12,400" "12400"
                Expect.equal (Format.count 1234567.0) "1,234,567" "1234567"
                Expect.equal (Format.count 0.0) "0" "0"
            }

            test "tryNumber" {
                Expect.equal (Format.tryNumber "0.05") (ValueSome 0.05) "0.05"
                Expect.equal (Format.tryNumber "-1") (ValueSome -1.0) "-1"
                Expect.equal (Format.tryNumber " 1e3 ") (ValueSome 1000.0) "1e3 with spaces"
                Expect.equal (Format.tryNumber "") ValueNone "empty"
                Expect.equal (Format.tryNumber "abc") ValueNone "a name"
                Expect.equal (Format.tryNumber "1,5") ValueNone "a comma"
                Expect.equal (Format.tryNumber "0x10") ValueNone "hexadecimal"
            }

            test "history samples at spaced counts" {
                let mutable folded = Folded.zero

                for i in 1..100000 do
                    folded <- Folded.add folded (float i)

                let counts = History.samples folded.History |> Array.map (fun s -> s.Count)
                Expect.equal counts[0] 1.0 "the first sample is at a count of 1"

                for i in 1 .. counts.Length - 1 do
                    Expect.isTrue (counts[i] > counts[i - 1]) (sprintf "counts rise at %d" i)

                    Expect.isTrue
                        (counts[i] >= counts[i - 1] * 1.05 || counts[i] = counts[i - 1] + 1.0)
                        (sprintf "spacing at %d: %g after %g" i counts[i] counts[i - 1])

                Expect.equal counts.Length 186 "the number of samples on the .NET run"
            }

            test "history returns itself between samples" {
                let mutable m = Moments.zero
                let mutable h = History.empty

                let mutable between = 0

                for x in 1..200 do
                    m <- Moments.add true m (float x)
                    let samples = History.samples h

                    let due =
                        if samples.Length = 0 then 1.0 else nextSamplingCount samples[samples.Length - 1].Count

                    let next = History.add m h

                    if Moments.count m < due then
                        between <- between + 1
                        Expect.isTrue (obj.ReferenceEquals (next, h)) (sprintf "the same instance at %d" x)
                        Expect.isTrue (obj.ReferenceEquals (History.samples next, samples)) (sprintf "the same array at %d" x)
                    else
                        Expect.isFalse (obj.ReferenceEquals (next, h)) (sprintf "a new instance at %d" x)

                    h <- next

                Expect.isGreaterThan between 100 "most counts up to 200 fall between samples"
            }

            test "history stops growing at its capacity" {
                let mutable h = History.empty
                let mutable c = 1.0
                let mutable latest = 0.0

                while c <= 1e13 do
                    h <- History.add (ofCount c) h
                    latest <- c
                    c <- nextSamplingCount c

                let samples = History.samples h
                Expect.equal samples.Length History.Capacity "Capacity samples"
                Expect.equal samples[samples.Length - 1].Count latest "the last sample has the latest count"
            }

            test "a feed delivers a seeded sequence and restarts it" {
                withGraph (fun () ->
                    let feed = Feed()
                    let epoch = createSignal 0

                    let fold =
                        createStreamFold
                            (Folded.zero, [])
                            (fun (f, xs) x -> Folded.add f x, x :: xs)
                            (fun _ ->
                                epoch.Value |> ignore
                                feed.Source(0.0, 1.0, 7))

                    fold.TryValue |> ignore
                    Expect.equal feed.Starts 1 "the first read starts the feed"
                    Expect.equal (feed.EmitAtRate(1024.0, 0.015625)) 16 "16 items a call"
                    Expect.equal (feed.EmitAtRate(1024.0, 0.015625)) 16 "16 items a call"
                    let folded, values = fold.Value
                    Expect.equal (Moments.count folded.Moments) 32.0 "16 + 16"
                    Expect.equal feed.Delivered 32 "Delivered"

                    let first16 = values |> List.rev |> List.truncate 16
                    let struct (expected, _) = Generator.normals 0.0 1.0 16 (Generator.create 7)
                    Expect.sequenceEqual first16 expected "the deviates of seed 7"

                    epoch.Value <- 1
                    fold.TryValue |> ignore
                    Expect.equal feed.Starts 2 "a changed signal restarts the feed"
                    Expect.equal (feed.EmitAtRate(1024.0, 0.015625)) 16 "16 items a call"
                    let restarted, again = fold.Value
                    Expect.equal (Moments.count restarted.Moments) 16.0 "the restart folds from zero"
                    Expect.sequenceEqual (List.rev again) first16 "the same first 16 values")
            }

            test "a feed carries the fraction" {
                withGraph (fun () ->
                    let feed = Feed()
                    let fold = createStreamFold Folded.zero Folded.add (fun _ -> feed.Source(0.0, 1.0, 7))
                    fold.TryValue |> ignore

                    let delivered = [ for _ in 1..4 -> feed.EmitAtRate(96.0, 0.015625) ]
                    Expect.equal delivered [ 1; 2; 1; 2 ] "1.5 items a call"
                    Expect.equal (Moments.count fold.Value.Moments) 6.0 "the fold holds 6 items")
            }

            testCaseAsync "a failed feed fails its fold"
            <| async {
                use graph = new Graph()
                let feed = Feed()

                let fold =
                    graph.Run(fun () -> createStreamFold Folded.zero Folded.add (fun _ -> feed.Source(0.0, 1.0, 7)))

                fold.TryValue |> ignore
                feed.Fail(exn "boom")
                do! drained ()

                match fold.TryValue with
                | Failed error -> Expect.equal error.Message "boom" "TryValue reports the feed's failure"
                | other -> failtestf "expected Failed, got %A" other
            }
        ]
