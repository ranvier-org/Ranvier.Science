# Maintained aggregates over 100,000 rows, and the cost of adding one key

This file holds four BenchmarkDotNet runs, one drift run and the drift test's printed lines. Every figure is
**measured here**, on 2026-11-19, on the machine below, with the command given beside each run. BenchmarkDotNet
0.15.8, default job (not shortened) unless a run says `--launchCount 3`. Each run's log (*from documentation*, under
`BenchmarkDotNet.Artifacts/`, git-ignored):

- run 1: `Ranvier.Science.Benchmarks.Aggregates.AggregatesBenchmarks-20261119-161313.log`;
- run 2: `Ranvier.Science.Benchmarks.KeyScaling.KeyScalingBenchmarks-20261119-162414.log`;
- run 3: `Ranvier.Science.Benchmarks.Aggregates.AggregatesBenchmarks-20261119-162755.log`;
- run 4: `Ranvier.Science.Benchmarks.KeyScaling.KeyScalingBenchmarks-20261119-164055.log`.

Ratios are taken within one run only, against the baseline row of the same category (and, in runs 2 and 4, of the
same row count). Differences between rows are taken within one run. Nothing here is compared with a figure from
another document.

Machine and runtime lines of the BenchmarkDotNet summary of runs 1, 2 and 4. Run 3 has the same machine and `[Host]`
lines, and its job line reads `Job-OEWQXS : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4`, followed
by `LaunchCount=3  Categories=AddOneKey` (*from documentation*, run 3's log):

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9974/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9900X 4.40GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4 DEBUG
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

## What one operation is

The collection is a keyed collection of 100,000 rows `struct (key, value)`, keys 0 to 99,999, base values
`100 + ((key * 37) mod 1000) / 7` (a multiple of 1/7, so mostly not exactly representable; exact where the numerator is
a multiple of 7, about one key in seven, key 0's value being exactly 100). Each benchmark builds its own graph and collection in
its own `GlobalSetup`, so the aggregates of the other benchmarks do not run during its operations.

- **`*_Revise1Percent`**: one operation replaces the values of 1,000 rows (every hundredth key) in one `batch`, each
  with a value that differs from the row's current one, then reads the aggregate once. Two prepared value sets
  alternate between operations (set A: `1000 + ((j * 13) mod 997) / 7`, mostly not exactly representable, with j = 0
exactly 1,000; set B: A + 0.1), and both differ from the base
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
the contradiction of run 1 does not appear here. This run has no control row: it cannot say how much of the growth
belongs to the collection and how much to the aggregate. Run 4 adds the control.

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
shown by these runs. The two 100,000-row `AddOneKey` rows of runs 1, 2 and 3 show Gen2 collections (the Gen2 column is
non-zero), while the two 10,000-row rows of run 2 show none. A key array of 100,000 `int`s is 400,000 B, above the
85,000 B large-object threshold, and one of 10,000 `int`s is 40,000 B, below it (*general knowledge*). That would fit
Gen2 at 100,000 rows only, and the spread between runs of the identical core benchmark at that size (728.0, 983.1 and
973.3 us in runs 2, 1 and 3). It is a candidate that was **not tested**.

## Run 4: `AddOneKey` at two row counts, with a control

Measured to separate the collection's cost of a key change from the aggregate's. `KeyScalingBenchmarks` now has three
methods at 10,000 and 100,000 rows. `Control_AddOneKey` is the baseline of each row count: the same operation (add one
new key, read, remove it, read) with the read being a `Projection.length` memo, which reads the projection's `Keys` and
takes the array's length, so it folds no row value. The read of `Keys` makes the collection process each key change
(see the code reference under "The two sentences"); the memo adds O(1) work on the array of its own. This is the
least work of its own among the public readers of the key set that were considered (`Count` and `Keys` read the same
cell; `length` and `isEmpty` wrap them in a memo), so the control is the collection's cost of the two key changes plus
the O(1) read, with the collection's key-array build and its key pass of the projection not separated from each other.
Command (run time 6 min 7 s; the full default job, not shortened):

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*KeyScaling*"
```

| Method                | Rows   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|---------------------- |------- |----------:|----------:|----------:|----------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| Control_AddOneKey     | 10000  |  28.33 us |  0.566 us |  1.231 us |  27.85 us |  1.00 |    0.06 |  4.8523 |  0.5798 |       - |  79.84 KB |        1.00 |
| CoreSumBy_AddOneKey   | 10000  |  30.05 us |  0.582 us |  0.905 us |  29.79 us |  1.06 |    0.05 |  5.0354 |  1.0071 |       - |  82.41 KB |        1.03 |
| SumStandard_AddOneKey | 10000  |  29.99 us |  0.485 us |  0.595 us |  29.91 us |  1.06 |    0.05 |  5.0354 |  1.0071 |       - |  82.41 KB |        1.03 |
|                       |        |           |           |           |           |       |         |         |         |         |           |             |
| Control_AddOneKey     | 100000 | 784.78 us | 15.653 us | 34.686 us | 783.77 us |  1.00 |    0.06 | 11.7188 | 11.7188 | 11.7188 | 782.98 KB |        1.00 |
| CoreSumBy_AddOneKey   | 100000 | 843.20 us | 18.025 us | 53.147 us | 834.73 us |  1.08 |    0.08 |  4.8828 |  4.8828 |  4.8828 | 785.54 KB |        1.00 |
| SumStandard_AddOneKey | 100000 | 725.87 us | 14.440 us | 23.318 us | 726.02 us |  0.93 |    0.05 |  4.8828 |  4.8828 |  4.8828 | 785.54 KB |        1.00 |

Increments over the control, differences of means within this run, with the 99.9% error intervals of the rows:

| Rows | Aggregate | Mean increment | As a share of the control's mean | Intervals | Allocation increment |
|---|---|---:|---:|---|---:|
| 10,000 | `CoreSumBy` | +1.72 us | 6.1% | 29.47 to 30.63 against 27.76 to 28.90: disjoint, resolved | +2.57 KB |
| 10,000 | `SumStandard` | +1.66 us | 5.9% | 29.51 to 30.48 against 27.76 to 28.90: disjoint, resolved | +2.57 KB |
| 100,000 | `CoreSumBy` | +58.42 us | 7.4% | 825.18 to 861.23 against 769.13 to 800.43: disjoint, resolved | +2.56 KB |
| 100,000 | `SumStandard` | -58.91 us | -7.5% | 711.43 to 740.31 against 769.13 to 800.43: disjoint, resolved | +2.56 KB |

At 100,000 rows the two aggregates have increments of opposite sign (+58.42 and -58.91 us), each outside the control's
interval. A path that does the control's work and a fold on top cannot be faster than the control, so the
`SumStandard` row contradicts the control at that size, as it contradicted the core row in runs 1 and 3. The time
increment of the aggregate at 100,000 rows is **undecided in this run**. The control row itself is 784.78 us here
against 725.87 us for `SumStandard`, and the Gen2 column shows the 100,000-row rows collecting in generation 2 (11.7
per 1,000 operations for the control, 4.9 for the two aggregates), at 10,000 rows none. At 10,000 rows no row
contradicts another.

What the control accounts for, from this table: at 10,000 rows 94.3% of `CoreSumBy`'s mean time (28.33 / 30.05) and
94.5% of `SumStandard`'s (28.33 / 29.99), and 96.9% of the allocation (79.84 / 82.41 KB); at 100,000 rows 99.7% of the
allocation (782.98 / 785.54 KB), and in time 93.1% of `CoreSumBy`'s mean (784.78 / 843.20) but more than all of
`SumStandard`'s (784.78 / 725.87 = 108.1%, the contradiction above). The control's own allocation rose 9.81 times
(79.84 to 782.98 KB) for 10 times the rows, and its mean time rose 27.7 times (28.33 to 784.78 us). The aggregates'
allocation increment over the control is the same 2.57 KB and 2.56 KB at the two sizes.

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
100003) / 7`, multiplied by 100,000 when the key is a multiple of 100, so the values are multiples of 1/7, mostly not
exactly representable (exact for about one in seven), and differ in magnitude by five orders. The maintained sum is compared with `exactSum` (the correctly
rounded sum, `tests/Ranvier.Science.Tests/Exact.fs`) of the current values after every pass, so the samples that fall
between re-folds count; the largest distance over the 1,000 samples is printed. This is one input sequence, not a bound.

## The drift test's lines, per target

The test `between re-folds the sum stays within 2 ulp` (`tests/Ranvier.Science.Tests/AggregateTests.fs`) edits 1,000
rows under `FloatPolicy.standard` and, beside it, under `FloatPolicy.fast`, and prints one line. *Measured here*,
2026-11-19, at commit 718f821: .NET with `dotnet run -c Release --project tests/Ranvier.Science.Tests`; JavaScript
with `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then
`npx mocha dist/tests/Main.fs.js`.

```
.NET:       Aggregate drift: largest distance 0 ulp before a re-fold, 0 ulp at every 997th edit, 196138 ulp under fast, over 1000000 edits, 1000 re-folds
JavaScript: Aggregate drift: largest distance 0 ulp before a re-fold, 0 ulp at every 997th edit, 14 ulp under fast, over 100000 edits, 100 re-folds
```

The two runs have different lengths (1,000,000 edits on .NET, 100,000 under JavaScript), so their `fast` figures are
not comparable with each other. The test asserts the `standard` distance only (at most 2 ulp); the `fast` figure is
printed, not asserted.

## The two sentences

**What the standard policy costs against the fast one.** In run 1, revising 1,000 of 100,000 rows and reading the sum
took 184.1 us under `standard` against 176.5 us under `fast`, a difference of 7.6 us, 4.3% of the `fast` mean, outside
both rows' 99.9% error intervals (180.4 to 187.8 us against 173.1 to 179.9 us), with the once-per-100-operations re-fold
inside the `standard` mean; in the drift run, whose revisions are contiguous blocks of 1,000 keys set to multiples of
1/7 (some scaled by 100,000) and not the benchmark's every-hundredth-key sets, `fast` ended at most 263 ulps from the
exact sum and `standard` at 0.00.

**What adding one key to 100,000 rows cost, and which description it agrees with.** One operation (add one key, read,
remove it, read) took 843.2 us for the core's `Projection.sumBy` and 725.9 us for `Aggregate.sumBy` under `standard` in
run 4 (983.1 and 881.7 us in run 1, 973.3 and 702.5 us in run 3, 728.0 and 725.1 us in run 2; the order of the two is
undecided); the collection's own processing of the key changes, measured by a control that reads the key count and folds
nothing, was 784.8 us of it in run 4, and allocated 782.98 of the 785.54 KB per operation (99.7%), which is two key
arrays of 100,000 `int`s (2 x 400,000 B = 781.25 KB) plus about 1.7 KB, and 79.84 of 82.41 KB (96.9%) at 10,000 rows
(two arrays of 40,000 B = 78.13 KB plus about 1.7 KB); the allocation of the collection grew 9.8 times for 10 times the
rows, so the clearest sign of linear growth is the collection's key array, built once per read that follows one or
more key changes in `Ranvier/src/Ranvier/Collections.fs` (`toArray`, lines 28 to 41, called from `ReadDelta`, lines
55 to 63, when changes are recorded since the last read; *from documentation*, read in the source; one operation
has two reads, each after a key change, hence two arrays), while the time grew 27.7 times, which mixes the row count with a
change of GC regime (Gen2 collections at 100,000 rows only) and is not a scaling law. What the aggregate adds over the
control is +1.7 us (about 6%) at 10,000 rows for both aggregates and a constant +2.6 KB at both sizes; at 100,000 rows
its time increment is undecided in these runs (+58.4 us for the core, -58.9 us for `standard`, each outside the
control's error interval). So the measurement does not settle between the two descriptions: the guide's "an O(N) key
diff" (`Ranvier/docs/content/guide/aggregates.fsx`, line 71) and Combinators.fs's "O(Δ) up to max(64, N) changes"
(`Ranvier/src/Ranvier/Combinators.fs`, line 1775) both predict the O(N) total seen here, because the collection builds
its key array in either case; it shows that the aggregate's own allocation does not grow between 10,000 and 100,000
rows, and it cannot show whether its time grows, as 10 times 1.7 us (about 17 us) lies within the 100,000-row spread.
It says nothing about bursts larger than one key.
