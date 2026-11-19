# Maintained aggregates over 100,000 rows, and the cost of adding one key

This file holds three BenchmarkDotNet runs and one drift run. Every figure is **measured here**, on 2026-11-19, on the
machine below, with the command given beside each run. BenchmarkDotNet 0.15.8, default job (not shortened) unless a run
says `--launchCount 3`. Ratios are taken within one run only, against the baseline row of the same category (and, in
run 2, of the same row count). Nothing here is compared with a figure from another document.

Machine and runtime lines of the BenchmarkDotNet summary (the same for all three runs):

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9974/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9900X 4.40GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4 DEBUG
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

## What one operation is

The collection is a keyed collection of 100,000 rows `struct (key, value)`, keys 0 to 99,999, base values
`100 + ((key * 37) mod 1000) / 7` (not exactly representable). Each benchmark builds its own graph and collection in
its own `GlobalSetup`, so the aggregates of the other benchmarks do not run during its operations.

- **`*_Revise1Percent`**: one operation replaces the values of 1,000 rows (every hundredth key) in one `batch`, each
  with a value that differs from the row's current one, then reads the aggregate once. Two prepared value sets
  alternate between operations (set A: `1000 + ((j * 13) mod 997) / 7`; set B: A + 0.1), and both differ from the base
  values, so every edit is a real change.
- **`*_AddOneKey`**: one operation adds one new key (100,000, value 1.5), reads the sum, removes the key, reads the sum.
- **`CoreSumBy_*`**: `Projection.sumBy`. **`SumStandard_*`, `SumFast_*`**: `Aggregate.sumBy` under `FloatPolicy.standard`
  and `FloatPolicy.fast`. **`MomentsStandard_Revise1Percent`**: `Aggregate.momentsBy standard`, read as the mean.
  **`FromScratch_Revise1Percent`**: `Projection.fold (+) 0.0`, so every row is folded on the read after a revision.
- The benchmarks are in two categories, `Revise1Percent` and `AddOneKey`, grouped by category;
  `CoreSumBy_Revise1Percent` and `CoreSumBy_AddOneKey` are the two baselines.

### Re-folds per operation (from the policy's rule and the drift run, not counted in the benchmark runs)

Under `standard` a re-fold over all rows is requested once the removals since the last one reach the row count. A
revision of 1,000 rows is 1,000 removals, so `SumStandard_Revise1Percent` and `MomentsStandard_Revise1Percent` re-fold
once per 100 operations (0.01 per operation); the drift run below counted 10 re-folds in 1,000 passes, which agrees.
That O(N) re-fold is inside their means. `SumFast_Revise1Percent` and the core rows never re-fold on these finite
values. `SumStandard_AddOneKey` makes one removal per operation over about 100,001 rows, so about one re-fold per
100,001 operations (arithmetic only).

## Run 1: the seven benchmarks

Command (run time 10 min 50 s, BenchmarkDotNet's figure; 11 min 1 s wall):

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*Aggregates*"
```

| Method                         | Categories     | Mean     | Error    | StdDev    | Median     | Ratio | RatioSD | Gen0    | Gen1    | Gen2   | Allocated | Alloc Ratio |
|------------------------------- |--------------- |---------:|---------:|----------:|-----------:|------:|--------:|--------:|--------:|-------:|----------:|------------:|
| CoreSumBy_AddOneKey            | AddOneKey      | 983.1 us | 69.12 us | 197.19 us | 1,031.3 us |  1.05 |    0.34 |  9.7656 |  9.7656 | 4.8828 | 785.54 KB |        1.00 |
| SumStandard_AddOneKey          | AddOneKey      | 881.7 us | 24.09 us |  65.53 us |   865.5 us |  0.94 |    0.25 | 11.7188 | 11.7188 | 6.8359 | 785.57 KB |        1.00 |
|                                |                |          |          |           |            |       |         |         |         |        |           |             |
| CoreSumBy_Revise1Percent       | Revise1Percent | 173.1 us |  5.75 us |  16.96 us |   178.7 us |  1.01 |    0.14 | 10.9863 |       - |      - | 179.72 KB |        1.00 |
| SumStandard_Revise1Percent     | Revise1Percent | 184.1 us |  3.66 us |   9.46 us |   182.6 us |  1.07 |    0.13 | 10.9863 |       - |      - | 179.72 KB |        1.00 |
| SumFast_Revise1Percent         | Revise1Percent | 176.5 us |  3.39 us |   5.28 us |   175.4 us |  1.03 |    0.11 | 10.9863 |       - |      - | 179.72 KB |        1.00 |
| MomentsStandard_Revise1Percent | Revise1Percent | 259.8 us |  4.77 us |   8.22 us |   258.5 us |  1.52 |    0.17 | 14.6484 |       - |      - | 242.22 KB |        1.35 |
| FromScratch_Revise1Percent     | Revise1Percent | 515.4 us | 10.18 us |  17.55 us |   515.5 us |  3.01 |    0.33 |  8.3008 |       - |      - | 140.66 KB |        0.78 |

The baseline rows read 1.05 and 1.01, not 1.00, because BenchmarkDotNet reports the mean of the ratio distribution,
not the ratio of means. BenchmarkDotNet flagged `CoreSumBy_Revise1Percent` and `SumStandard_Revise1Percent` as possibly
multimodal, and `CoreSumBy_AddOneKey` had 26 outliers detected (6 removed).

### Sanity check of run 1

- `Revise1Percent`: the rows order as the work does. `SumStandard` (184.1 us, interval 180.4 to 187.8) is above
  `SumFast` (176.5 us, interval 173.1 to 179.9), as a path doing more work should be; `MomentsStandard` and
  `FromScratch` do more work and are above all three sum rows. `CoreSumBy` (173.1 us) and `SumFast` (176.5 us) do the
  same work apart from the policy's wrapper and their intervals overlap. No contradiction.
- `AddOneKey`: `SumStandard_AddOneKey` (881.7 us, interval 857.6 to 905.8) is below `CoreSumBy_AddOneKey` (983.1 us,
  interval 914.0 to 1,052.2), but the standard path does at least the core's work (it adds a policy wrapper around the
  same operation). The intervals do not overlap, so this is a contradiction. Run 3 re-runs these two rows.

## Run 2: `AddOneKey` at two row counts

Measured to see how the add-one-key cost moves with the number of rows. This is a second class,
`KeyScalingBenchmarks` (`bench/Ranvier.Science.Benchmarks/KeyScaling.fs`), whose operation is the `AddOneKey` operation
above, at 10,000 and at 100,000 rows. Baseline: `CoreSumBy_AddOneKey` of the same row count. Command (run time
3 min 22 s):

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*KeyScaling*"
```

| Method                | Rows   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|---------------------- |------- |----------:|----------:|----------:|----------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| CoreSumBy_AddOneKey   | 10000  |  30.38 us |  0.601 us |  1.241 us |  29.82 us |  1.00 |    0.06 | 5.0354 | 1.0071 |      - |  82.41 KB |        1.00 |
| SumStandard_AddOneKey | 10000  |  29.54 us |  0.217 us |  0.203 us |  29.53 us |  0.97 |    0.04 | 5.0354 | 1.0071 |      - |  82.41 KB |        1.00 |
|                       |        |           |           |           |           |       |         |        |        |        |           |             |
| CoreSumBy_AddOneKey   | 100000 | 728.00 us | 14.507 us | 31.538 us | 732.49 us |  1.00 |    0.06 | 4.8828 | 4.8828 | 4.8828 | 785.54 KB |        1.00 |
| SumStandard_AddOneKey | 100000 | 725.14 us | 14.125 us | 20.704 us | 724.69 us |  1.00 |    0.05 | 4.8828 | 4.8828 | 4.8828 | 785.53 KB |        1.00 |

Within this run the core and the standard rows agree at each size (ratios 0.97 and 1.00, intervals overlapping), so
the contradiction of run 1 does not appear here.

## Run 3: the two `AddOneKey` rows of run 1, three launches each

Measured to re-measure the rows that contradicted each other in run 1. The same class and the same two benchmarks as
run 1's `AddOneKey` category, with three launches of the benchmark process per row (run time 6 min 7 s):

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*Aggregates*" --anyCategories AddOneKey --launchCount 3
```

| Method                | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|----------:|---------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| CoreSumBy_AddOneKey   | 973.3 us | 74.77 us | 260.21 us | 819.2 us |  1.07 |    0.40 | 3.9063 | 3.9063 | 3.9063 | 785.54 KB |        1.00 |
| SumStandard_AddOneKey | 702.5 us |  8.73 us |  25.20 us | 707.1 us |  0.77 |    0.20 | 3.9063 | 3.9063 | 3.9063 | 785.53 KB |        1.00 |

The contradiction persists: `SumStandard_AddOneKey` (interval 693.8 to 711.2 us) is below `CoreSumBy_AddOneKey`
(interval 898.5 to 1,048.1 us). The core row's StdDev is 260 us and its median (819.2 us) is 154 us below its mean, so
its distribution is wide and skewed. The comparison of the core's and the standard policy's `AddOneKey` cost is
**undecided in these runs**: runs 1 and 3 put the standard row below the core row, run 2 puts them level, and a path
that does more work cannot be faster. Both rows allocate the same 785.5 KB in every run. No cause of the spread is
shown by these runs; the Gen2 collections in every `AddOneKey` row (Gen0, Gen1 and Gen2 counts per 1,000 operations
all non-zero) are a candidate that was not tested.

## Drift run

Command (about 3 s wall, one process):

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --drift
```

Output:

```
drift standard: 1000 passes x 1000 rows over 100000 rows, sampled after every pass: largest distance 0.00 ulps, 10 re-folds
drift fast: 1000 passes x 1000 rows over 100000 rows, sampled after every pass: largest distance 263.00 ulps, 0 re-folds
```

Each pass revises keys `pass * 1000 .. pass * 1000 + 999` (modulo 100,000) to `((pass * 7919 + key * 104729) mod
100003) / 7`, multiplied by 100,000 when the key is a multiple of 100, so the values are fractions that are not exactly
representable and differ in magnitude by five orders. The maintained sum is compared with `exactSum` (the correctly
rounded sum, `tests/Ranvier.Science.Tests/Exact.fs`) of the current values after every pass, so the samples that fall
between re-folds count; the largest distance over the 1,000 samples is printed. This is one input sequence, not a bound.

## The two sentences

**What the standard policy costs against the fast one.** In run 1, revising 1,000 of 100,000 rows and reading the sum
took 184.1 us under `standard` against 176.5 us under `fast`, a difference of 7.6 us, 4.3% of the `fast` mean, with the
once-per-100-operations re-fold inside the `standard` mean; that difference is small against the table's own spread
(ratio columns 1.07 +/- 0.13 and 1.03 +/- 0.11, one run of the default job), and in the drift run the same revisions left
`fast` as far as 263 ulps from the exact sum and `standard` at 0.00.

**What adding one key to 100,000 rows cost, and which description it agrees with.** One operation (add one key, read,
remove it, read) took 881.7 us under `standard` and 983.1 us for the core's `Projection.sumBy` in run 1 (702.5 and
973.3 us in run 3, 725.1 and 728.0 us in run 2; the order of the two is undecided, see run 3), and in run 2 the cost
rose from 29.54 to 725.14 us (24.5 times) under `standard`, and from 30.38 to 728.00 us (24.0 times) for the core, as
the rows rose from 10,000 to 100,000 (10 times), with the allocation per operation rising 9.5 times (82.41 to
785.53 KB), so at these two sizes the cost of one key grows at least in proportion to the rows. That agrees with the
guide's description of an O(N) key diff (`Ranvier/docs/content/guide/aggregates.fsx`, line 71) and not with an O(Δ)
cost for a single key (`Ranvier/src/Ranvier/Combinators.fs`, line 1775, "O(Δ) up to max(64, N) changes"); two sizes
show a growth, not its exact order, and the measurement says nothing about bursts larger than one key.
