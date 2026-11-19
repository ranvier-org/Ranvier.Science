module Ranvier.Science.Benchmarks.KeyScaling

open System
open BenchmarkDotNet.Attributes
open Ranvier
open Ranvier.Numerics
open Ranvier.Science.Benchmarks.Aggregates

/// The control: a memo of `Projection.length`, which reads `Keys` and takes its length and folds no row value. Reading
/// `Keys` makes the collection process each key change (it builds the key array of the new key set); the memo itself
/// does O(1) work on the array.
let private lengthScenario (rowCount: int) =
    new Scenario (
        rowCount,
        fun rows ->
            let length = rows |> Projection.length
            (fun () -> float length.Value), (fun () -> 0)
    )

/// What adding one key costs at two collection sizes, and how much of it is the aggregate's.
/// One operation is as in `AggregatesBenchmarks`: add one new key, read, remove the key, read.
/// `Control_AddOneKey` reads `Projection.length` instead of a sum, so it measures the collection's own processing of
/// the two key changes. `CoreSumBy_AddOneKey` (the core's `Projection.sumBy`) and `SumStandard_AddOneKey`
/// (`Aggregate.sumBy` under the standard policy) add their folds to that. The control is the baseline of each row count.
[<MemoryDiagnoser>]
type KeyScalingBenchmarks() =
    let mutable scenario: Scenario = Unchecked.defaultof<Scenario>

    [<Params(10_000, 100_000)>]
    member val Rows = 100_000 with get, set

    [<GlobalSetup(Target = "Control_AddOneKey")>]
    member this.SetupControl() = scenario <- lengthScenario this.Rows

    [<GlobalSetup(Target = "CoreSumBy_AddOneKey")>]
    member this.SetupCore() = scenario <- coreSumScenario this.Rows

    [<GlobalSetup(Target = "SumStandard_AddOneKey")>]
    member this.SetupStandard() = scenario <- sumScenario this.Rows FloatPolicy.standard

    [<GlobalCleanup>]
    member _.Cleanup() = (scenario :> IDisposable).Dispose ()

    [<Benchmark(Baseline = true)>]
    member _.Control_AddOneKey() = scenario.AddRemove ()

    [<Benchmark>]
    member _.CoreSumBy_AddOneKey() = scenario.AddRemove ()

    [<Benchmark>]
    member _.SumStandard_AddOneKey() = scenario.AddRemove ()
