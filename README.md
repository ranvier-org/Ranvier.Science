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
- `examples/Monitor`: the sequential monitor, one page for a native window and a browser (below).
- `docs/`: the probe record and the measurements.

## Tests

Both commands run from the repository root.

.NET:

```
dotnet run -c Release --project tests/Ranvier.Science.Tests
```

JavaScript (Fickl compiles the same test files, then mocha runs them). On a fresh checkout, restore the local tools
and the Node packages first:

```
dotnet tool restore
npm install
dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release
npx mocha dist/tests/Main.fs.js
```

`examples/Monitor/model/Recorded.fs` holds one recorded stream of 1,000 doubles. Both targets fold the same
doubles, and `CrossTargetTests.fs` pins the moments (exact equality) and the confidence-sequence bounds (relative
`1e-12`).

## The sequential monitor

`examples/Monitor` is one page, a sequential monitor, built from one set of sources for a native window and a browser.
Each channel is a stream of observations folded into moments; the page shows a table of rows (count, mean, standard
error, a confidence sequence, the user's formula) and a chart of each channel's mean with its sequence band and its
fixed-sample interval, which the chart alone draws. The folds, the rows and the diagnostics are a graph of the core's
nodes.

- `examples/Monitor/model`: the page's graph (channels, feeds, history, the monitor).
- `examples/Monitor/src`: the view, the chart and the scripted runs, shared by both heads.
- `examples/Monitor/native`: the head that mounts the page in a window.
- `examples/Monitor/web`: the head that Fickl compiles to JavaScript, and the script that drives it in headless Chrome.

The commands below run from the repository root. The scripted runs hide the window and run Chrome headless; a run
without a script name (`dotnet run -c Release --project examples/Monitor/native`) opens a visible window, and no run of
that kind or of the `dev` host has been made in this slice.

1. .NET suite: `dotnet run -c Release --project tests/Ranvier.Science.Tests`
2. JavaScript suite: `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then
   `npx mocha dist/tests/Main.fs.js`
3. Native scripted run: `dotnet run -c Release --project examples/Monitor/native -- --script <name>`
4. Browser build: `dotnet fickl examples/Monitor/web -e .fs.js -o examples/Monitor/web/dist -c Release`. The first
   build took 1299 s (*measured here*, `docs/PROBES.md`, section B); a build with nothing changed took 0.7 s.
5. Browser scripted run: `node examples/Monitor/web/run.mjs <name>`

The scripts, each ending with the line `script done 0` on success (1 when a check fails, 2 for an unknown name):

- `table`: prints one line per row of the table (channel, count, mean, standard error, confidence sequence, formula),
  then whether the pinned text equals the DOM text.
- `counts`: prints the counts of scene builds, mark keys and node updates over a frame, a change of the confidence level
  and an added channel, then `all as expected`.
- `picture`: runs `table`, then saves a picture of the page. The native run writes `examples/Monitor/out/monitor.png`;
  the browser run takes `--picture <png>` and writes that file.
- `frames`: times the frame work of the page at four channels and over a sweep of rates, and prints each block's mean and
  95th percentile against a budget. It prints no frame rate.

Records of the page:

- [`docs/measurements/monitor.md`](docs/measurements/monitor.md): the `frames` figures on both targets, and the `table`
  and `counts` outputs.
- [`docs/pictures/monitor-native.png`](docs/pictures/monitor-native.png): the page in the native window, written by
  `dotnet run -c Release --project examples/Monitor/native -- --script picture` and copied from
  `examples/Monitor/out/monitor.png`, at commit `a5ce24c`.
- [`docs/pictures/monitor-browser.png`](docs/pictures/monitor-browser.png): the page in headless Chrome, written by
  `node examples/Monitor/web/run.mjs picture --picture docs/pictures/monitor-browser.png`, at commit `4adc36b`.
- [`docs/PROBES.md`](docs/PROBES.md): section B (the heads, packages and build), the workarounds the page needed, and
  hot reload.

Both pictures show the page in the state the `table` script leaves, two channels on the recorded stream, and not the
default page of four channels.

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

Figures in those files carry a label (measured here, from documentation, or general knowledge), and a statement about
the app that the slice has not checked is labelled an assumption; read the label before comparing two figures.
