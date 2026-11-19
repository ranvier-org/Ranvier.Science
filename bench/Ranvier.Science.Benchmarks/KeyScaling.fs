module Ranvier.Science.Benchmarks.KeyScaling

open System
open BenchmarkDotNet.Attributes
open Ranvier.Numerics
open Ranvier.Science.Benchmarks.Aggregates

/// What adding one key costs at two collection sizes, to show how the cost moves with the number of rows.
/// One operation is as in `AggregatesBenchmarks`: add one new key, read the sum, remove the key, read the sum.
/// The two benchmarks are the core's `Projection.sumBy` (the baseline) and `Aggregate.sumBy` under the standard policy.
[<MemoryDiagnoser>]
type KeyScalingBenchmarks() =
    let mutable scenario: Scenario = Unchecked.defaultof<Scenario>

    [<Params(10_000, 100_000)>]
    member val Rows = 100_000 with get, set

    [<GlobalSetup(Target = "CoreSumBy_AddOneKey")>]
    member this.SetupCore() = scenario <- coreSumScenario this.Rows

    [<GlobalSetup(Target = "SumStandard_AddOneKey")>]
    member this.SetupStandard() = scenario <- sumScenario this.Rows FloatPolicy.standard

    [<GlobalCleanup>]
    member _.Cleanup() = (scenario :> IDisposable).Dispose ()

    [<Benchmark(Baseline = true)>]
    member _.CoreSumBy_AddOneKey() = scenario.AddRemove ()

    [<Benchmark>]
    member _.SumStandard_AddOneKey() = scenario.AddRemove ()
