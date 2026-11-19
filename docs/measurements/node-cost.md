# Node cost around four AstroShowcase formula bodies

All figures below are **measured here**: one run of the command below on 2026-11-19, BenchmarkDotNet 0.15.8
default job (not shortened), 21 min 41 s in total. Ratios are taken within this one run only, against the
`Direct` row of the same body. Nothing here is compared with a figure from another document.

## Command

```
dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*NodeCost*"
```

Machine and runtime lines of the BenchmarkDotNet summary:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9974/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9900X 4.40GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4 DEBUG
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

## What one operation is

One operation is one call of the path, which returns the body's `float`. The four bodies are copies of
AstroShowcase code (`bench/Ranvier.Science.Benchmarks/Bodies.fs` names each source line), each with one input
varied and the rest at the page's default controls: `rate` (decay rate, altitude varied), `kepler`
(`keplerToState`, true anomaly varied, x of the position returned), `rigidStep` (one `rk4` step of the rigid-body
derivative, initial tilt varied, first element returned) and `decay` (the drag decay table, initial altitude varied,
lifetime in days returned).

| Path | One operation |
|---|---|
| `Direct` | the body called directly, alternating between two inputs on successive calls |
| `KitUnchanged` | the `Kit.memo` copy called with the key it already holds (a cache hit), one fixed input |
| `KitChanged` | the `Kit.memo` copy called with a key that differs from the previous call's (a miss that recomputes and copies the key), alternating two inputs |
| `NodeCached` | a `createMemo` node read while its value is cached, one fixed input |
| `NodeWritten` | a `createSignal` written a value different from its current one, then the `createMemo` node read (one body run), alternating two inputs |

A signal ignores a write of an equal value, so `NodeWritten` alternates between two inputs; `KitChanged` and
`Direct` alternate the same two inputs so that their ratios compare like with like. `KitUnchanged` and `NodeCached`
use a fixed input by definition, so their ratios compare a cache hit with a computation.

## Results (20 rows)

| Method       | Body      | Mean           | Error       | StdDev        | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------- |---------- |---------------:|------------:|--------------:|------:|--------:|-------:|-------:|----------:|------------:|
| Direct       | decay     | 18,518.2175 ns | 661.5654 ns | 1,950.6407 ns | 1.011 |    0.15 | 3.8452 | 0.4120 |   64344 B |        1.00 |
| KitUnchanged | decay     |      7.5785 ns |   0.2932 ns |     0.8646 ns | 0.000 |    0.00 |      - |      - |         - |        0.00 |
| KitChanged   | decay     | 13,321.5180 ns | 260.8869 ns |   508.8399 ns | 0.728 |    0.08 | 3.8452 | 0.4272 |   64376 B |        1.00 |
| NodeCached   | decay     |      0.6178 ns |   0.0351 ns |     0.0826 ns | 0.000 |    0.00 |      - |      - |         - |        0.00 |
| NodeWritten  | decay     | 15,644.6833 ns | 305.9366 ns |   574.6231 ns | 0.855 |    0.10 | 3.8452 | 0.4120 |   64344 B |        1.00 |
|              |           |                |             |               |       |         |        |        |           |             |
| Direct       | kepler    |      9.6593 ns |   0.2560 ns |     0.7468 ns |  1.01 |    0.11 |      - |      - |         - |          NA |
| KitUnchanged | kepler    |      5.3142 ns |   0.0451 ns |     0.0422 ns |  0.55 |    0.04 |      - |      - |         - |          NA |
| KitChanged   | kepler    |     53.0456 ns |   0.6663 ns |     0.6232 ns |  5.52 |    0.42 | 0.0019 |      - |      32 B |          NA |
| NodeCached   | kepler    |      0.7083 ns |   0.0552 ns |     0.1628 ns |  0.07 |    0.02 |      - |      - |         - |          NA |
| NodeWritten  | kepler    |     34.0620 ns |   0.6896 ns |     0.8469 ns |  3.55 |    0.28 |      - |      - |         - |          NA |
|              |           |                |             |               |       |         |        |        |           |             |
| Direct       | rate      |      9.5865 ns |   0.2072 ns |     0.3037 ns |  1.00 |    0.04 | 0.0014 |      - |      24 B |        1.00 |
| KitUnchanged | rate      |      6.3246 ns |   0.1458 ns |     0.2666 ns |  0.66 |    0.03 |      - |      - |         - |        0.00 |
| KitChanged   | rate      |     44.9333 ns |   0.9214 ns |     2.1537 ns |  4.69 |    0.27 | 0.0033 |      - |      56 B |        2.33 |
| NodeCached   | rate      |      0.6484 ns |   0.0322 ns |     0.0301 ns |  0.07 |    0.00 |      - |      - |         - |        0.00 |
| NodeWritten  | rate      |     36.0353 ns |   0.7165 ns |     0.9807 ns |  3.76 |    0.15 | 0.0014 |      - |      24 B |        1.00 |
|              |           |                |             |               |       |         |        |        |           |             |
| Direct       | rigidStep |     60.9587 ns |   1.2445 ns |     2.5422 ns | 1.002 |    0.06 | 0.0430 |      - |     720 B |        1.00 |
| KitUnchanged | rigidStep |      5.3236 ns |   0.0906 ns |     0.1872 ns | 0.087 |    0.00 |      - |      - |         - |        0.00 |
| KitChanged   | rigidStep |     90.4263 ns |   1.7862 ns |     4.9196 ns | 1.486 |    0.10 | 0.0449 |      - |     752 B |        1.04 |
| NodeCached   | rigidStep |      0.6019 ns |   0.0260 ns |     0.0231 ns | 0.010 |    0.00 |      - |      - |         - |        0.00 |
| NodeWritten  | rigidStep |     88.7269 ns |   1.7898 ns |     4.0031 ns | 1.458 |    0.09 | 0.0430 |      - |     720 B |        1.00 |

The `Direct` rows are the ratio baseline and read 1.00 up to run noise (the `decay` baseline has a StdDev of
1,950 ns and a bimodal distribution warning). Ratios of 0.000 are below the table's precision.

## Grain

Of the four bodies, only `decay` (about 18.5 us per direct call, mean as measured here) has a write-then-read node
under 10% more than its direct call (ratio 0.855 within this run, which is inside the noise of the `Direct`
baseline: the node adds roughly 25 to 28 ns to the cheaper three bodies, which is negligible only against a body
that takes microseconds); `rigidStep` (ratio 1.458), `kepler` (3.55) and `rate` (3.76) do not qualify.
