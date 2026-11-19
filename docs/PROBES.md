# Probes

Each section records one probe: what was run and what it showed. Every figure carries a label.

## Core package through Fickl

Question: does Fickl compile a project that reaches `Ranvier` 0.2.0 as a NuGet package?

Result: yes. The Fable test project reaches the package through `Ranvier.Numerics`
(`PackageReference Include="Ranvier" Version="0.2.0"`), so the `ProjectReference` fallback to the core clone was
not needed.

Command (*measured here*), from the repository root:

```
dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release
npx mocha dist/tests/Main.fs.js
```

Relevant output: `Project and references (26 source files) parsed`, `Fable compilation finished`, then
`2 passing` under Mocha, including `a memo follows its signal`.

## Commands for probes A, C, D and E

The probes are the list `"Probes"` in `tests/Ranvier.Science.Tests/Probes.fs`, one test per probe. Every outcome
below is *measured here* on 2026-11-19, with `Ranvier` 0.2.0 and Fickl 0.10.1 (Fable 5.27.0), from the repository
root:

- .NET: `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter-test-list "Probes"`, reporting
  `6 tests run ... 6 passed, 0 ignored, 0 failed, 0 errored` (E3 added in a later commit; the first run had 5).
- JavaScript: `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then
  `npx mocha dist/tests/Main.fs.js`, reporting `8 passing` (the six probes and the two smoke tests).

Each test asserts the observed behaviour. Where the targets differ, the test asserts each target's behaviour behind
`#if FABLE_COMPILER`.

## A

Question: do F# units of measure compile and erase on both targets?

Test: `units of measure compile and erase`. With `[<Measure>] type m` and `[<Measure>] type s`, it asserts
`(6.0<m> / 2.0<s>) = 3.0<m/s>`, `float 3.0<m/s> = 3.0`, `LanguagePrimitives.FloatWithMeasure<m> 2.0 = 2.0<m>`, and
that `let twice (x: float<'u>) = x + x` gives `4.0<m>` for `2.0<m>`.

- .NET (*measured here*): passes.
- JavaScript (*measured here*): Fickl compiles the file without an error or a warning, and the test passes. The
  emitted `dist/tests/Ranvier.Science.Tests/Probes.fs.js` holds plain numbers: the assertions compile to
  `Expect_equal(6 / 2, 3, ...)`, `Expect_equal(3, 3, ...)`, `Expect_equal(2, 2, ...)` and
  `Expect_equal(twice(2), 4, ...)`, and `twice` compiles to `function twice(x) { return x + x; }`. A measure leaves
  no wrapper, conversion call or type tag in the output.

Consequence: Task 9 can type quantities with F# units of measure on both targets. Its fallback for a compiler that
rejects measures is not needed.

## B

Not yet run. Probe B (an empty View page through Fickl) is the first task of Plan 2.

## C

Question: can a projection row factory create a stream fold that the row owns, so that removing the key stops the
stream?

Test: `a stream fold can be created in a projection row factory`. A keyed collection holds key 1. A
`createProjectionWith` over the collection's keys has a factory that creates a `ManualStream<int>`, calls
`createStreamFold 0 (+) start` (where `start` records its `CancellationToken`) and returns `fun () -> total.Value`.

- .NET (*measured here*): passes. The first `rows.Get 1` raises `NotReadyException` and starts the row's stream
  (one start). After pushing 1, 2 and 3 into the row's stream, `rows.Get 1` is `6`. The token is not cancelled while
  the key is present. After `keys.Remove 1` and a read of `rows.Keys` (empty), both the token passed to `start` and
  the token passed to the source's `Run` report `IsCancellationRequested = true`.
- JavaScript (*measured here*): passes with the same assertions. Every step is synchronous: pushes on the graph
  thread publish at once, and the removal cancels the tokens during the pass, with no microtask wait.

Consequence: Plan 2 can create each channel's stream fold in its projection row factory. The per-channel
`createRoot` fallback is not needed.

## D

Question: does a memo that reads a pending stream report pending without raising, and which member reports a memo's
status?

Test: `a memo reading a pending stream is pending` (a `testCaseAsync`, because the failure step waits for the
stream's task to settle). `memo = createMemo (fun _ -> stream.Value + 1)` over `createStream` of a `ManualStream<int>`
with no item.

- .NET (*measured here*): passes. Observed values:
  - while pending: `memo.TryValue = Pending`, without raising; then `memo.Status = Status.Pending`;
    `memo.Peek = 0`;
  - after one item 4: `memo.TryValue = Ready 5`; `memo.Status = Status.None`;
  - after the stream fails with `exn "boom"` (and `settled` on its start): `memo.TryValue = Failed error` with
    `error.Message = "boom"`; `memo.Status = Status.Error`; `memo.Peek = 5`.
- JavaScript (*measured here*): the same, except `memo.Peek` before the first value. The hoped-for assertion
  `memo.Peek = 0` failed under Mocha with `Expected: 0`, `Actual: <null>`. The test now asserts
  `isNull (box memo.Peek)` under `FABLE_COMPILER` and `memo.Peek = 0` on .NET.

The member that reports a memo's status without raising is `Memo<'T>.TryValue : Reading<'T>`, a tracked read that
brings the memo current first. `Reading<'T>` (`Ranvier/src/Ranvier/Types.fs`, a `[<Struct>]` union) has the cases
`Ready of value: 'T`, `Pending` and `Failed of error: exn`; its doc comment states that `error` is non-null on both
targets. `Memo<'T>.Status : Status` returns the stored `[<Flags>]` enum of the last computation and runs nothing
(`Ranvier/src/Ranvier/Core.fs`, `member _.Status = status`), so it can lag a source that changed since; the test
reads it only after `TryValue`. `Memo<'T>.Peek` is
the cached value without recomputing; before the first value it is `0` for `int` on .NET and `null` under Fable.

Consequence: Task 10 builds `Status: unit -> QuantityStatus` on `TryValue`, mapping `Ready _` to `Ready`, `Pending`
to `Pending` and `Failed e` to `Failed e`. It cannot use `Peek` as a value before the first `Ready`: the result
differs by target.

## E

Question: does an effect that reads a stream fold re-run when the fold returns state equal to the state it was given?

Each test is a `createStreamFold` over a `ManualStream<int>` and a `createEffect` that reads `tally.TryValue` and
counts its runs in a mutable counter. A push on the graph thread outside a batch flushes the effect before `Push`
returns, on both targets (the core's test `an effect sees one run per item on the graph thread, and one per batch`
relies on the same). Every zero item is followed by a non-zero item, and the count is read after each push.

### E1: the same instance

Test: `a fold that returns its previous state notifies no reader`. State is a record `{ Total: int }`; the fold is
`fun s x -> if x = 0 then s else { s with Total = s.Total + x }`. Items 1, 0, 2.

- .NET (*measured here*): runs 1 at creation, 2 after item 1, 2 after item 0, 3 after item 2; final `Total = 3`.
- JavaScript (*measured here*): the same counts.

### E2: an equal rebuilt struct

Test: `a fold that returns a new equal struct`. State is a `[<Struct>]` record `{ Total: int }`, rebuilt on every
item by `fun s x -> { Total = s.Total + x }`. Items 1, 0.

- .NET (*measured here*): runs 1, 2, then 2 after item 0. The zero item does not re-run the effect.
- JavaScript (*measured here*): runs 1, 2, then 3 after item 0. The zero item re-runs the effect. Fickl compiles the
  struct record to a class (`class StructTally extends Record` in the emitted `Probes.fs.js`).
- Final `Total = 1` on both targets. The test asserts 3 under `FABLE_COMPILER` and 2 otherwise.

### E3: an equal rebuilt record

Test: `a fold that returns a new equal record notifies its reader`. State is the record `{ Total: int }` of E1,
rebuilt on every item by `fun s x -> { Total = s.Total + x }`. Items 1, 0, 2.

- .NET (*measured here*): runs 1, 2, 3 after item 0, 4 after item 2; final `Total = 3`. The zero item re-runs the
  effect, although the new record is equal to the old one under F# structural equality.
- JavaScript (*measured here*): the same counts.

### The rule

A fold publication notifies readers when the status changes or `equal.Equals (value, state)` is false, where
`equal` is `graph.Options.Equality.Comparer<'S>()` (*from documentation*: `Ranvier/src/Ranvier/Streams.fs`,
`Publish`). The default `Equality` is `JsIdentityPolicy` (`Types.fs:539`), whose comparer comes from
`ComparerResolution.resolve` (*from documentation*: `Ranvier/src/Ranvier/Types.fs`):

- .NET (`Types.fs:290-307`): `float` and `float32` compare by IEEE value; other value types (structs) and `string`
  compare with `EqualityComparer<'T>.Default`, by value; every other type, records and classes included, compares by
  reference (`ReferenceComparer`).
- Fable (`Types.fs:279-289`): `===` at every type. Primitives compare by value; everything else compares by
  reference, including structs, struct tuples, `DateTime`, `decimal` and `KeyValuePair`.

| State returned by the fold | .NET | JavaScript | Known by |
|---|---|---|---|
| the same instance it received (record) | no notification | no notification | E1, *measured here* |
| an equal rebuilt struct | no notification | notification | E2, *measured here* |
| an equal rebuilt record | notification | notification | E3, *measured here* |

Consequence: on both targets, a stream fold skips an unchanged item only when it returns the state it received, for
a record state and for a struct state alike. A rebuilt record notifies readers on both targets; a rebuilt equal
struct is skipped on .NET only. `createStreamFold` in 0.2.0 has no comparer overload; the only other lever is the
graph's `GraphOptions.Equality`. This bounds the fold functions of Tasks 9 and 10 and of the later plans'
stream-fed quantities.

## Workarounds

### Raising from `subtract` to request a re-fold

What the slice does: `Aggregate.sumBy` and `Aggregate.momentsBy` (`src/Ranvier.Numerics/Aggregate.fs`) keep their
state, an accumulator with the row count and the removals since the last re-fold, in a `Projection.foldGroupBy`.
Their `subtract` raises `ArithmeticException` when the state it is given is not finite, or, under
`RefoldAfterRemovals`, when the removals including this one reach the row count before it. `Refolds` counts these
raises in a counter the closure owns.

Core lines it relies on (*from documentation*: `Ranvier/src/Ranvier/Combinators.fs`, package 0.2.0 source):

- `ProjectionFold.Step` (1058-1064) runs a step in `try ... with _ -> full <- true`; `Sync` then calls `Recompute`
  (1294-1295), so one raise gives one re-fold in the same read.
- A row edit is one step, `add (subtract s old) v` (1121), with `old` the row's cached value; `foldGroupBy` (1856)
  folds over `map projection upstream`, so `old` is the cached projected value and `projection` runs once per edit
  (*measured here*: test `a row edit makes one subtract and one add`, 1 call on .NET and JavaScript).
- A removed row is `Step (fun s -> subtract s old)` (1146), except the last valued row: `valued = 0` sets the state
  to `zero` without calling `subtract` (1142). `removing the last row returns zero` therefore needs no re-fold.
- `Recompute` (1242-1254) folds `add` from `zero` over the cached row values in `Keys` order (`lastKeys`). A keyed
  collection's `Keys` are in insertion order, and `AddOrUpdate` of an existing key keeps its position
  (*from documentation*: `Ranvier/src/Ranvier/Collections.fs`, `KeyedCollection.AddOrUpdate`). With keys inserted
  `0 .. N-1`, the re-fold order is key order: the two "from-scratch bits" tests compare against a fold in key order
  and pass on both targets (*measured here*).
- The core's own `sumBy` uses the same device: `subtractFinite` (1860-1865) raises `ArithmeticException` on an
  infinite or NaN difference.

What a direct request would replace: a step result or a callback in `foldGroupBy` that asks for a re-fold (for
example a `subtract` returning `ValueNone`) would replace the raise, the exception cost per re-fold on .NET, and
the reliance on `Step` treating every exception alike. A re-fold the core starts for its own reasons (a moved read
of the fold functions, 1267-1268) is not counted in `Refolds`.

## Differences from Kit.Flow

`Run` (`src/Ranvier.Quantities/Run.fs`) replaces `Flow` (`AstroShowcase/Kit.fs`, the type `Flow` at line 157).
It differs from `Flow` in two cases.

### A control written away and back between reads

`Flow.At` computes `keyOf p` at each call and restarts when `key <> k`, a structural comparison of two `float[]`
(*from documentation*: `AstroShowcase/Kit.fs`, line 172). A control moved away and back between two calls gives an
equal key, so `Flow` does not restart.

`Run` restarts when a quantity `start` read received a write of a different value since the last read of `State`.
A write away and a write back are two writes of a different value, so the run restarts. Test
`a quantity written away and back between reads restarts the run` (*measured here*, on .NET and JavaScript):

- unread writes `x0: 4 → 7 → 4`, then a read: a new state object, `Steps = 0`, `X = 4`, `start` called a second time;
- `batch (fun () -> x0.Value <- 9.0; x0.Value <- 4.0)`, then a read: a new state object, `Steps = 0`, `X = 4`,
  `start` called a third time.

Consequence for the port: its bit-for-bit comparison with `Flow` holds when the run is read between distinct writes
of a control, which is how the app writes controls (one write per control per frame, and a read every frame).

### A NaN clock at a start

`Flow.At` records the clock at a start (`last <- t`, line 175) even when it is NaN; every later comparison with NaN
is false, so `Flow` then neither advances nor restarts until its key changes.

`Run` leaves the last value unset after a start at a NaN clock; the first later read at a clock other than NaN
records that clock and does not advance, and the ordinary rules follow. Test
`a NaN clock at the start does not stick the run` (*measured here*, on .NET and JavaScript). No stepped clock in
the app is NaN, so the port is unaffected.

## Sources read

### The confidence sequence (`src/Ranvier.Statistics/ConfidenceSequence.fs`)

Read on 2026-11-19: Waudby-Smith, Arbour, Sinha, Kennedy and Ramdas, "Time-uniform central limit theory and
asymptotic confidence sequences", arXiv 2103.06476, version 9 (14 March 2024; the abstract page lists v1 to v9).
Theorem 2.2 with equation 13, Definition 2.1, Section 2.2.1 (equation 11) and appendix B.2 (equations 302 to 311).

Routes, each read as the paper's own LaTeX (the `alttext` of each formula), not as a summary:

1. `https://arxiv.org/html/2103.06476v9` in the browser pane: Theorem 2.2 and appendix B.2 extracted as text.
2. `https://ar5iv.labs.arxiv.org/html/2103.06476` in the browser pane, the same passages. ar5iv shows no version
   number. Both routes are LaTeXML renderings of the authors' source, so they share that source.
3. The authors' reference implementation, `R/asymptotic_confseq.R` and `R/misc.R` of the GitHub repository
   `WannabeSmith/drconfseq`, branch `main`, read as raw text: functions `std_conjmix_margin`, `best_rho2_exact`,
   `best_rho2_approx`, `asymptotic_confseq` and `cumul_var`.

A fetch of the PDF returned only compressed bytes; the fetch tool stored a copy outside the repository, which was
deleted unread.

The formulas, in this repository's notation (*from documentation*: arXiv 2103.06476 v9, the sections named):

- **Radius (Theorem 2.2, equation 13).** `radius α ρ t sd = sd · sqrt( (2(tρ² + 1) / (t²ρ²)) · ln( sqrt(tρ² + 1) / α ) )`,
  and the interval is `mean_t ± radius`. Routes 1 and 2 give the same LaTeX; it equals the brief's formula.
- **Standard deviation.** `sd_t² = (1/t)·Σy² − mean_t²` over the first t observations, with `mean_t` the mean of
  the first t: the population variance, `Moments.variance`. The brief's choice is confirmed.
- **ρ.** A constant ρ > 0, fixed before the data are seen, "which dictates the time at which (13) is tightest". In
  version 9, ρ enters only as `tρ²`, with sd outside the square root: the brief's scaling. The reference
  implementation's `lyapunov_asympcs` uses `t·var·ρ²` instead, a different scaling of ρ; the paper's Theorem 2.2
  and the implementation's `asymptotic_confseq` (unit margin times `sqrt(var)`) use the brief's.
- **Guarantee (Theorem 2.2, Definition 2.1).** Observations independent and identically distributed, with finite
  variance (the conditions of the central limit theorem). The intervals over all t form a (1 − α) asymptotic
  confidence sequence: there is a (generally unknown) sequence of intervals that holds the mean at every t
  simultaneously with probability at least 1 − α, and the ratios of its bounds to these bounds tend to 1 almost
  surely. The guarantee is asymptotic and time-uniform, as the design document reads it.
- **`rhoTightestAt` (appendix B.2, equation 302).** `ρ = sqrt( (−W₋₁(−α²·e⁻¹) − 1) / n )`, with `W₋₁` the lower
  branch of the Lambert W function; equivalently `ρ = sqrt(u / n)` with `u > 0` the root of `α²·exp u = u + 1`
  (equation 307, with `u = n·ρ²`). Implemented by Newton's method on `u − ln(u + 1) + 2 ln α = 0`, started from the
  closed-form approximation of equation 311, `u ≈ −2 ln α + ln(−2 ln α + 1)`.

Correction to the brief's formula: none.

Differences in the reference implementation, recorded and not followed: `cumul_var` divides by t − 1
(`(cumul_mean(x^2) - cumul_mean(x)^2) * t / (t - 1)`), where Theorem 2.2 divides by t; the brief and this
repository follow the paper.

Comparison with the reference implementation. R is not installed on this machine, so the R code was not run. The
expression of `std_conjmix_margin(t, rho2 = ρ², alpha)` was transcribed into an F# script and evaluated beside
`ConfidenceSequence.radius α ρ t 1.0` (*measured here*: `dotnet fsi` on a scratch script against the Release build
of `Ranvier.Statistics`, 2026-11-19):

| α | ρ | t | `radius` | transcribed reference expression |
|---|---|---|---|---|
| 0.05 | 1 | 100 | 0.32730186242349329 | 0.32730186242349335 |
| 0.1 | 0.05 | 1000 | 0.090559960337208867 | 0.090559960337208867 |
| 0.01 | 0.3 | 37 | 0.61252437936126281 | 0.61252437936126281 |

The same script gives `rhoTightestAt 0.05 1000 = 0.090619909854668543`, for which `α²·exp u − (u + 1) = −5.3e-15`,
and equation 311's approximation `0.089085097528922116` (*measured here*, same run).

**The unconfirmed mark on the radius formula is cleared.** The equation itself was read as LaTeX by two routes
that agree (routes 1 and 2), and the authors' reference implementation (route 3) has the same expression.
`rhoTightestAt` is taken from equation 302, read by the same two routes and matching `best_rho2_exact`.

### The normal quantile (`src/Ranvier.Statistics/NormalQuantile.fs`)

Acklam's rational approximation. Coefficients and break-point 0.02425 checked against the copy in Debian's
`imms` source, `immscore/ltqnorm.h` (*from documentation*), which matches the widely reproduced listing; stated
relative error below 1.15e-9 (*from documentation*: the same algorithm's description).

## Coverage

Test `the sequence covers under continuous monitoring and the fixed-sample interval does not`: 2,000 streams
(seeds 1 to 2,000) of 10,000 standard normal deviates each, α = 0.05, ρ = `rhoTightestAt 0.05 1000.0`; a stream
misses when its interval excludes 0 at any t ≥ 30.

| Target | Confidence sequence miss fraction | Fixed-sample interval miss fraction |
|---|---|---|
| .NET | 0.0415 | 0.5735 |
| JavaScript | 0.0415 | 0.5735 |

*Measured here*, 2026-11-19: .NET with `dotnet run -c Release --project tests/Ranvier.Science.Tests`; JavaScript
with `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then
`npx mocha dist/tests/Main.fs.js`. Each figure is the line the test prints, counted over the 2,000 streams of one
run. The two targets print the same fractions.

Checked from t = 1 instead of t ≥ 30, the same 2,000 streams give a confidence-sequence miss fraction of 1.0, and
the same script gives 0.0415 from t ≥ 30 (*measured here*, 2026-11-19, .NET: `dotnet fsi` on a scratch script
against the Release build of `Ranvier.Statistics`, the coverage test's loop with the first checked t as a
parameter). At t = 1 the interval has zero width at the observation, and the theorem's guarantee is asymptotic
only. Consequence for the later page: it applies a burn-in before showing or acting on the sequence.

After the fix of `FixedSample.ofMoments` to `-(NormalQuantile.ofProbability (α/2))`, the coverage test prints the
same fractions, 0.0415 and 0.5735, on both targets (*measured here*, by the commands above).

## One recorded stream on both targets

Test `the recorded stream gives pinned numbers` (`tests/Ranvier.Science.Tests/CrossTargetTests.fs`) folds the 1,000
doubles of `Recorded.fs` with `Moments.add true`. The stream was made once on .NET with
`Generator.normals 0.84 3.0 1000 (Generator.create 20261119)` and is checked in as data.

| Value | .NET | JavaScript |
|---|---|---|
| count | 1000 | 1000 |
| mean | 0.9199618814567002 | 0.9199618814567002 |
| variance | 8.34324586928672 | 8.34324586928672 |
| confidence sequence lower bound (α 0.05, `rhoTightestAt 0.05 1000.0`) | 0.642729642202583 | 0.642729642202583 |
| confidence sequence upper bound | 1.1971941207108174 | 1.1971941207108174 |

*Measured here*, 2026-11-19: .NET with `dotnet run -c Release --project tests/Ranvier.Science.Tests` (the test's
pinned literals were taken from a `dotnet fsi` run against the Release build and the test passes on them);
JavaScript by importing the compiled `dist/tests` modules (`Recorded`, `Moments`, `ConfidenceSequence`) into a scratch
`node` script that ran the same fold and printed the values. Count, mean and variance are exactly equal, and both bounds
agree to a relative difference of 0 (printed with round-trip formatting). The test asserts the moments with `=` and the
bounds within relative 1e-12.
