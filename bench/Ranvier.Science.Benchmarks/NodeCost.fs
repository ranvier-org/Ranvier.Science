module Ranvier.Science.Benchmarks.NodeCost

open System
open BenchmarkDotNet.Attributes
open Ranvier

/// A formula body and the two inputs its alternating paths switch between. The first input is the page default.
type Case =
    { Name: string
      Body: float -> float
      First: float
      Second: float }

let cases =
    [ { Name = "rate"; Body = Bodies.rate; First = 400.0; Second = 425.0 }
      { Name = "kepler"; Body = Bodies.kepler; First = 1.0; Second = 1.5 }
      { Name = "rigidStep"; Body = Bodies.rigidStep; First = 2.0; Second = 2.5 }
      { Name = "decay"; Body = Bodies.decay; First = 400.0; Second = 425.0 } ]

/// The body names the benchmark class runs; `Program.fs` narrows it to one body for `--body <name>`.
let mutable selectedBodies = cases |> List.map (fun c -> c.Name)

/// The five call paths around one body, in an active graph that lives until disposal.
/// The alternating paths (Direct, KitChanged, NodeWritten) use First at their first call, then Second, then First
/// again: a signal ignores a write of an equal value and KitMemo ignores an equal key, so every call after the first
/// differs from the one before it. KitUnchanged and NodeCached use First throughout.
type Paths(case: Case) =
    let graph = new Graph ()
    let activation = graph.Activate ()
    let body = case.Body
    let keyFirst = [| case.First |]
    let keySecond = [| case.Second |]
    let kitUnchanged = KitMemo.memo (fun p -> body p[0])
    let kitChanged = KitMemo.memo (fun p -> body p[0])
    let cachedSource = createSignal case.First
    let cachedNode = createMemo (fun _ -> body cachedSource.Value)
    let writtenSource = createSignal case.First
    let writtenNode = createMemo (fun _ -> body writtenSource.Value)
    let mutable directSecond = false
    let mutable changedSecond = false
    let mutable writtenSecond = false

    do
        // Prime the caches so that KitUnchanged and NodeCached measure a hit from the first call.
        kitUnchanged keyFirst |> ignore
        cachedNode.Value |> ignore
        writtenNode.Value |> ignore

    /// The body, called directly.
    member _.Direct() =
        let x = if directSecond then case.Second else case.First
        directSecond <- not directSecond
        body x

    /// The KitMemo copy, called with the key it already holds.
    member _.KitUnchanged() = kitUnchanged keyFirst

    /// The KitMemo copy, called with a key that differs from the previous call's.
    member _.KitChanged() =
        let key = if changedSecond then keySecond else keyFirst
        changedSecond <- not changedSecond
        kitChanged key

    /// A createMemo node read while cached.
    member _.NodeCached() = cachedNode.Value

    /// A createMemo node read after its input signal is written a value different from the current one.
    member _.NodeWritten() =
        writtenSource.Value <- if writtenSecond then case.Second else case.First
        writtenSecond <- not writtenSecond
        writtenNode.Value

    interface IDisposable with
        member _.Dispose() =
            activation.Dispose ()
            (graph :> IDisposable).Dispose ()

/// What wrapping a body in a graph node costs, per call path.
/// One operation is one call of the path. Direct, KitChanged and NodeWritten alternate between two inputs so that
/// every call does the work of a changed input; KitUnchanged and NodeCached use one fixed input and so measure a
/// cache hit. Each method returns the body's float so that the call cannot be removed.
[<MemoryDiagnoser>]
type NodeCostBenchmarks() =
    let mutable paths: Paths = Unchecked.defaultof<Paths>

    [<ParamsSource("Bodies")>]
    member val Body = "rate" with get, set

    /// The bodies to measure: all four, or the one named by `--body`.
    member _.Bodies = selectedBodies


    [<GlobalSetup>]
    member this.Setup() =
        paths <- new Paths (cases |> List.find (fun c -> c.Name = this.Body))

    [<GlobalCleanup>]
    member _.Cleanup() = (paths :> IDisposable).Dispose ()

    [<Benchmark(Baseline = true)>]
    member _.Direct() = paths.Direct ()

    [<Benchmark>]
    member _.KitUnchanged() = paths.KitUnchanged ()

    [<Benchmark>]
    member _.KitChanged() = paths.KitChanged ()

    [<Benchmark>]
    member _.NodeCached() = paths.NodeCached ()

    [<Benchmark>]
    member _.NodeWritten() = paths.NodeWritten ()
