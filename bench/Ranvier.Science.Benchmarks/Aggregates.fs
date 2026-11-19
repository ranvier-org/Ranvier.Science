module Ranvier.Science.Benchmarks.Aggregates

open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open Ranvier
open Ranvier.Numerics
open Ranvier.Science.Tests

/// Rows of the benchmark collection: a key and a value.
type Row = (struct (int * float))

let private keyOf (struct (k: int, _: float)) = k
let private valueOf (struct (_: int, v: float)) = v

/// The value of a row before any revision. Not exactly representable, between 100 and 243.
let private baseValue (key: int) = 100.0 + float ((key * 37) % 1000) / 7.0

/// The 1,000 keys a revision replaces: every hundredth key of 100,000.
let private revisedKeys = Array.init 1_000 (fun j -> j * 100)

/// Two prepared sets of 1,000 values, one per revised key. Every value is at least 1,000, so differs from the row's
/// base value, and set B differs from set A at every key: each revision replaces a value with a different one.
let private setA = Array.init 1_000 (fun j -> 1_000.0 + float ((j * 13) % 997) / 7.0)
let private setB = setA |> Array.map (fun v -> v + 0.1)

/// One operation of every Revise1Percent benchmark: replaces the revised keys' values with set A, or with set B on
/// the next call, in one batch.
let private reviseWith (rows: KeyedCollection<int, Row>) (second: bool) =
    let values = if second then setB else setA

    batch (fun () ->
        for j in 0 .. revisedKeys.Length - 1 do
            rows.AddOrUpdate (struct (revisedKeys[j], values[j])))

/// A keyed collection of `rowCount` rows, a graph that lives until disposal, and the aggregate under test.
/// `build` receives the collection's rows with the graph active and returns how to read the aggregate and its re-folds.
type Scenario(rowCount: int, build: Projection<int, Row> -> (unit -> float) * (unit -> int)) =
    let graph = new Graph ()
    let activation = graph.Activate ()
    let rows = createKeyedCollection keyOf

    do
        batch (fun () ->
            for k in 0 .. rowCount - 1 do
                rows.AddOrUpdate (struct (k, baseValue k)))

    let read, refolds = build rows.Rows
    let mutable second = false
    let newKey = rowCount

    do read () |> ignore

    /// The aggregate's current value.
    member _.Read() = read ()

    /// The re-folds the policy has requested since creation; zero for a core aggregate.
    member _.Refolds() = refolds ()

    /// Replaces 1,000 rows' values with values that differ from the current ones, in one batch, then reads the aggregate.
    member _.Revise() =
        reviseWith rows second
        second <- not second
        read ()

    /// Adds one new key, reads the aggregate, removes the key, reads the aggregate again. Returns the second reading.
    member _.AddRemove() =
        rows.AddOrUpdate (struct (newKey, 1.5))
        read () |> ignore
        rows.Remove newKey |> ignore
        read ()

    /// The collection, for the drift mode.
    member _.Rows = rows

    interface IDisposable with
        member _.Dispose() =
            activation.Dispose ()
            (graph :> IDisposable).Dispose ()

/// A maintained sum under a policy.
let sumScenario (rowCount: int) (policy: FloatPolicy) =
    new Scenario (
        rowCount,
        fun rows ->
            let sum = rows |> Aggregate.sumBy policy valueOf
            (fun () -> sum.Value.Value), sum.Refolds
    )

/// The core's `Projection.sumBy`, which keeps no re-fold count.
let coreSumScenario (rowCount: int) =
    new Scenario (
        rowCount,
        fun rows ->
            let sum = rows |> Projection.sumBy valueOf
            (fun () -> sum.Value), (fun () -> 0)
    )

/// The core's `Projection.fold` with a plain `+`: every row folded on every read after a change.
let fromScratchScenario (rowCount: int) =
    new Scenario (
        rowCount,
        fun rows ->
            let sum = rows |> Projection.fold (fun s (struct (_: int, v: float)) -> s + v) 0.0
            (fun () -> sum.Value), (fun () -> 0)
    )

/// A maintained mean and variance under the standard policy, read as the mean.
let momentsScenario (rowCount: int) =
    new Scenario (
        rowCount,
        fun rows ->
            let m =
                rows
                |> Aggregate.momentsBy FloatPolicy.standard (fun (struct (_: int, v: float)) -> Moments.add true Moments.zero v)

            (fun () -> Moments.mean m.Value.Value), m.Refolds
    )

/// What keeping an aggregate current costs over a keyed collection of 100,000 rows.
/// One operation of a `Revise1Percent` benchmark replaces the values of 1,000 rows (every hundredth key), each with a
/// value that differs from its current one (two prepared value sets alternate), inside one `batch`, then reads the
/// aggregate once. One operation of an `AddOneKey` benchmark adds one new key, reads the sum, removes the key and
/// reads the sum again. Each benchmark builds its own graph and collection, so the aggregates of the other benchmarks
/// do not run during its operations. Each method returns the aggregate's value so that the read cannot be removed.
[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<CategoriesColumn>]
type AggregatesBenchmarks() =
    let rowCount = 100_000
    let mutable scenario: Scenario = Unchecked.defaultof<Scenario>

    [<GlobalSetup(Target = "SumStandard_Revise1Percent")>]
    member _.SetupSumStandardRevise() = scenario <- sumScenario rowCount FloatPolicy.standard

    [<GlobalSetup(Target = "SumFast_Revise1Percent")>]
    member _.SetupSumFastRevise() = scenario <- sumScenario rowCount FloatPolicy.fast

    [<GlobalSetup(Target = "CoreSumBy_Revise1Percent")>]
    member _.SetupCoreRevise() = scenario <- coreSumScenario rowCount

    [<GlobalSetup(Target = "MomentsStandard_Revise1Percent")>]
    member _.SetupMomentsRevise() = scenario <- momentsScenario rowCount

    [<GlobalSetup(Target = "FromScratch_Revise1Percent")>]
    member _.SetupFromScratchRevise() = scenario <- fromScratchScenario rowCount

    [<GlobalSetup(Target = "SumStandard_AddOneKey")>]
    member _.SetupSumStandardAdd() = scenario <- sumScenario rowCount FloatPolicy.standard

    [<GlobalSetup(Target = "CoreSumBy_AddOneKey")>]
    member _.SetupCoreAdd() = scenario <- coreSumScenario rowCount

    [<GlobalCleanup>]
    member _.Cleanup() = (scenario :> IDisposable).Dispose ()

    [<Benchmark(Baseline = true); BenchmarkCategory("Revise1Percent")>]
    member _.CoreSumBy_Revise1Percent() = scenario.Revise ()

    [<Benchmark; BenchmarkCategory("Revise1Percent")>]
    member _.SumStandard_Revise1Percent() = scenario.Revise ()

    [<Benchmark; BenchmarkCategory("Revise1Percent")>]
    member _.SumFast_Revise1Percent() = scenario.Revise ()

    [<Benchmark; BenchmarkCategory("Revise1Percent")>]
    member _.MomentsStandard_Revise1Percent() = scenario.Revise ()

    [<Benchmark; BenchmarkCategory("Revise1Percent")>]
    member _.FromScratch_Revise1Percent() = scenario.Revise ()

    [<Benchmark(Baseline = true); BenchmarkCategory("AddOneKey")>]
    member _.CoreSumBy_AddOneKey() = scenario.AddRemove ()

    [<Benchmark; BenchmarkCategory("AddOneKey")>]
    member _.SumStandard_AddOneKey() = scenario.AddRemove ()

/// A deterministic value for pass `pass` and key `key`: a fraction that is not exactly representable, scaled by 1e5
/// at every hundredth key so that the rows differ in magnitude.
let private driftValue (pass: int) (key: int) =
    let raw = float ((pass * 7919 + key * 104729) % 100_003) / 7.0
    if key % 100 = 0 then raw * 1e5 else raw

/// Runs `passes` revision passes of 1,000 rows over 100,000 rows under `policy`. A pass replaces the values of keys
/// `pass * 1000 .. pass * 1000 + 999` (modulo 100,000), then reads the maintained sum and compares it with the
/// correctly rounded sum of the current values. Returns the largest distance in ulps over all passes, and the re-folds.
let drift (policy: FloatPolicy) (passes: int) =
    let rowCount = 100_000
    let current = Array.init rowCount baseValue
    use scenario = sumScenario rowCount policy
    let mutable worst = 0.0

    for pass in 0 .. passes - 1 do
        batch (fun () ->
            for j in 0..999 do
                let k = (pass * 1000 + j) % rowCount
                let v = driftValue pass k
                current[k] <- v
                scenario.Rows.AddOrUpdate (struct (k, v)))

        worst <- max worst (ulps (scenario.Read ()) (exactSum current))

    worst, scenario.Refolds ()
