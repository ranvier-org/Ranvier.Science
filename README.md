# Ranvier.Science

Numerics, quantities and statistics libraries built on the Ranvier reactive graph.

This is a local slice built to answer design questions. It has no remote, and nothing in it is published or
released. It builds on the published `Ranvier` 0.2.0 package and is not a finished product or a stable API.

## What it holds

- `src/Ranvier.Numerics`: compensated sum, moments, float policy and maintained aggregates, tolerance, generator.
- `src/Ranvier.Quantities`: units, quantities and models, runs.
- `src/Ranvier.Statistics`: normal quantile, confidence sequence, fixed-sample interval.
- `tests/`: one test suite that compiles to two targets, .NET and, through the Fickl compiler, JavaScript.
- `bench/Ranvier.Science.Benchmarks`: the benchmark project.
- `docs/`: the probe record and the measurements.

## Tests

Both commands run from the repository root.

.NET:

```
dotnet run -c Release --project tests/Ranvier.Science.Tests
```

JavaScript (Fickl compiles the same test files, then mocha runs them):

```
dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release
npx mocha dist/tests/Main.fs.js
```

`tests/Ranvier.Science.Tests/Recorded.fs` holds one recorded stream of 1,000 doubles. Both targets fold the same
doubles, and `CrossTargetTests.fs` pins the moments (exact equality) and the confidence-sequence bounds (relative
`1e-12`).

## Benchmarks

Run from the repository root, in Release.

- `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --check`: confirms that the four node-cost
  bodies give equal results over ten inputs on each of five paths. It times nothing.
- `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*NodeCost*"`: times the cost of a
  node against the same work done directly. Add `--body <name>` to run one body only.
- `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*Aggregates*"`: times maintained
  aggregates against re-folding.
- `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter "*KeyScaling*"`: times how an
  aggregate's update cost changes with the number of keys.
- `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --drift`: measures how far a maintained
  aggregate drifts from a fresh fold, in ulps, under the standard and fast float policies.

## Records

- [`docs/PROBES.md`](docs/PROBES.md): what each probe ran and what it showed.
- [`docs/measurements/node-cost.md`](docs/measurements/node-cost.md): the node-cost measurements.
- [`docs/measurements/aggregates.md`](docs/measurements/aggregates.md): the aggregate and key-scaling measurements.

Every figure in those files carries a label (measured here, from documentation, or general knowledge); read the
label before comparing two figures.
