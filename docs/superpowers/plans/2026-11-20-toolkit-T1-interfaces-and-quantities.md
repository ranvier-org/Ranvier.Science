# Ranvier.Science toolkit, Plan T1: interfaces and quantities — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The interface changes I1 to I9, first-order uncertainty (`Uncertain`), the `Moments` reads, and the quantities additions of spec §8.1 to §8.6 (unit catalogue, unit text and conversion, the model's catalogue and the formula notation, entries with uncertainty and formula entries, entry lifetime, physical constants and Monte Carlo propagation), on core `Ranvier` 0.2.1.

**Architecture:** Every change stays inside the three libraries under `src/` and their callers in this repository. Ranvier.Numerics gains `Uncertain`, three `Moments` reads and a re-fold floor in `FloatPolicy.standard`; Ranvier.Statistics gains `SequenceSettings`; Ranvier.Quantities gains a unit catalogue, unit text, an uncertainty on every entry, formula entries, entries scoped to their owner, the CODATA constants and a Monte Carlo reader. Tasks 1 to 8 make the interface changes, Tasks 9 to 14 build the quantities features on them, Task 15 records the dispositions of the findings.

**Tech Stack:** F# on .NET 10 (`global.json` 10.0.100); `Ranvier` 0.2.1 from nuget.org; Expecto 11.0.0-alpha8; Fickl 0.10.1 (Fable 5.27.0) with Fable.Core 5.3.0 and Fable.Mocha 2.17.0; mocha 12; Python 3.14's standard library for reference scripts.

**Spec:** `C:\wk\ranvier-release\Ranvier.Science\docs\superpowers\specs\2026-11-20-toolkit-design.md`. A § below is a section of it. Plans T2 (numerics kernels, §6.1 to §6.10 and §8.7) and T3 (statistics, docs and release) run after this plan on the same branch and use the names produced here exactly as the spec spells them.

## Global Constraints

Repository and process:

- Work in `C:\wk\ranvier-release\Ranvier.Science`, branch `toolkit` (checked out). Read anything under `C:\wk\ranvier-release`; edit only this repository. Nothing under `C:\wk\ranvier-release\Ranvier` changes (§2).
- Commit on `toolkit` only. Conventional messages (`build: …`, `feat(numerics): …`, `feat(quantities): …`, `feat(statistics): …`, `test: …`, `docs: …`), each ending with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never merge, push, tag or publish.
- Shell: write a multi-line script to a file and run the file; use absolute paths; run commands plainly.
- Read core 0.2.1 at its tag, `git -C C:/wk/ranvier-release/Ranvier show v0.2.1:src/Ranvier/<file>`, or under `~/.nuget/packages/ranvier/0.2.1/fable/` once restored; never in the core's working tree, which is at 0.2.0.
- Inner loop: `rfs check --fail-on-error` (rfs 0.1.1; `rfs status --reload` after an `.fsproj` edit) or SageFs. A filtered test run is for the red/green loop only.

**The gate.** Run from the repository root. A task is complete only when all four pass in full, unfiltered, and the build shows 0 warnings and 0 errors:

1. `dotnet build -c Release`
2. `dotnet run -c Release --project tests/Ranvier.Science.Tests` (filter for the loop: `-- --filter "<test list name>"`)
3. `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`
4. `npx mocha dist/tests/Main.fs.js` (filter for the loop: `--grep "<text>"`)

Baseline (*measured here*, 2026-11-20, as the coordinator's brief records): build 9 s incremental, .NET suite 2 s (174 tests), Fable compile 4 s warm, mocha 5 s (171 tests).

Code:

- Library code compiles under Fable: no reflection, no `System.Random`, no thread API outside `#if !FABLE_COMPILER`, no `System.Numerics.Complex`. `Math.*` is fine.
- Library code never type-tests a System exception (`:? ArithmeticException`): under Fable those are plain errors (`tests/Ranvier.Science.Tests.Fable/Expecto.fs`, the remark on `throwsT`). Catch with `with e ->` where a catch is needed, or raise an F# `exception`, as `Formula.parse` does with `FormulaSyntax`.
- Errors (§3.2): an argument outside the domain raises `ArgumentOutOfRangeException` or `ArgumentException`; a NaN argument where the domain is otherwise satisfied gives NaN and raises nothing; a data-dependent failure of a direct method returns `Result<'T, string>` whose message names the condition.
- Graph (§3.3): a kernel never reads or creates a node, and a graph-aware part states in its doc comment how many nodes it creates. The core's default equality compares a `float` by value and a record, union, array or class by reference, so a memo that returns one of those notifies its readers on every recomputation. No doc comment claims a cut-off from structural equality, and no test expects a memo of a record, union, array or struct value to cut off under the default policy. No node per arithmetic operation or array element; no dependency tracker, cache or scheduler beside the core's.
- Names and signatures the spec fixes are used exactly. Where the spec leaves a choice, the task states the choice in one line.
- A task that removes or renames a public function migrates every caller in `examples/Monitor`, `bench/Ranvier.Science.Benchmarks` and `tests` in the same task.
- A new library file goes in its project's `<Compile>` list. A new test file goes in `tests/Ranvier.Science.Tests/TestFiles.props` **and** gets a line `testList "<File>.fs" [ Ranvier.Science.Tests.<Module>.tests ]` at the same position in `tests/Ranvier.Science.Tests.Fable/Main.fs`, which lists every test module by hand. Each test file is a module `Ranvier.Science.Tests.<File name>` with one `[<Tests>] let tests`.
- Test code uses only what the Fable shim `tests/Ranvier.Science.Tests.Fable/Expecto.fs` defines (`test`, `testList`, `testCase`, `failtest`, `failtestf`, `Expect.equal`, `notEqual`, `isTrue`, `isFalse`, `isLessThanOrEqual`, `isGreaterThan`, `isEmpty`, `isNonEmpty`, `stringContains`, `throws`, `throwsT`, `throwsC` and the rest listed there). A missing function is added to the shim with Expecto's signature. `Exact.fs` gives `ulps` and `exactSum`; `Support.fs` gives `withGraph`, `ManualStream` and, on .NET, `offThread`.

Docs, figures and references:

- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`: the contract, the raise conditions, the cost where it is not O(1), the accuracy bound; no narration. British spelling. Each task writes the doc comments of what it adds or changes.
- Every figure in a doc comment, record or commit message carries a label: *measured here* (with the command), *from documentation* (with the file or page), or *general knowledge*.
- Every reference value in a test carries its source beside it (§5.2): a closed form or identity written in the test; a script under `tests/reference/`, run as `python -I tests/reference/<name>.py` with Python 3.14's standard library alone (never `pip install`), with the command named beside the pasted literals; or a published table or standard cited by name and edition.
- CODATA values (Tasks 9 and 13) are fetched from NIST in the task that uses them, `https://physics.nist.gov/cuu/Constants/` and the ASCII table of all values linked from it, and the page and the access date are written in the test file. No value is typed from memory. If the fetch fails, the task stops and reports.

## Review Focus

1. **`open Ranvier.Quantities` hides an FSharp.Core function.** A measure type named like an FSharp.Core value shadows it: `[<Measure>] type min` makes `min 3 4` fail with FS1133 "No constructors are available for the type 'min'" (*measured here*: `dotnet fsi` on a five-line script, 2026-11-20). A user expects `min` and `max` to keep working after the open. Test in Task 9.
2. **Unit text typed with look-alike characters**: `μ` (U+03BC) for `µ` (U+00B5), `Ω` (U+2126) for `Ω` (U+03A9), `−` (U+2212) for `-` in an exponent. A user expects the same unit. Tests in Tasks 9 and 10.
3. **Unit text with extra spaces**: leading, trailing, doubled, and around `/` and `^` (`" kg  m / s ^ 2 "`). A user expects the unit without the spaces. Test in Task 10.
4. **A change of uncertainty alone** (`SetUncertainty`) reaching a value reader. A user expects a chart of a formula's value not to redraw when only an input's uncertainty moves. Tests in Tasks 6 and 7.
5. **A declaration in a pure memo, or a constant declared with no active graph.** A user expects the first to raise at once with nothing declared, and the second to keep working as in 0.1.0. Tests in Task 8.

---

## File Structure

```
src/Ranvier.Numerics/
  FloatPolicy.fs, Moments.fs        Task 3
  Uncertain.fs                      new, Task 5 (last in the Compile list)
  Ranvier.Numerics.fsproj           Tasks 1, 5
src/Ranvier.Statistics/
  ConfidenceSequence.fs             Task 4
src/Ranvier.Quantities/             Compile order at the end of this plan:
  Units.fs        Dimension, UnitInfo (equality, checked scales, prefixed, parse, conversion), Prefix,
                  the UnitCatalogue type, UnitOf                                       Tasks 2, 9, 10
  Catalogue.fs    new, Task 9: every measure type, `Units`, the `UnitCatalogue` functions
  Expr.fs         new, Task 14: the `Expr` union, moved out of Formula.fs unchanged
  Model.fs        Entry, Model (catalogue, lifetime, formula records)                  Tasks 6, 8, 11, 14
  Formula.fs      notation, check, evaluators, Monte Carlo; before Quantity.fs from Task 7 on
  Quantity.fs     declarations, formula entries, views                                Tasks 6, 7, 8, 14
  Constants.fs    new, Task 13
  Run.fs          unchanged (Plan T2 edits it)
tests/Ranvier.Science.Tests/        new files: UncertainTests.fs (5), FormulaEntryTests.fs (7), LifetimeTests.fs (8),
                                    CatalogueTests.fs (9), UnitTextTests.fs (10), NotationTests.fs (11, 12),
                                    ConstantsTests.fs (13), MonteCarloTests.fs (14)
tests/reference/normal_quantile.py  new, Task 14
examples/Monitor/model/Monitor.fs   Tasks 3, 4
docs/FINDINGS.md                    Task 15; docs/measurements/gate.md new, Task 15
```

---

### Task 1: Core `Ranvier` 0.2.1 (I7)

**Files:**
- Modify: `src/Ranvier.Numerics/Ranvier.Numerics.fsproj:19`, `examples/Monitor/model/Monitor.Model.fsproj:17`, `examples/Monitor/native/Monitor.Native.fsproj:22`: `<PackageReference Include="Ranvier" Version="0.2.0" />` becomes `Version="0.2.1"`.
- Modify: `src/Directory.Build.props:7`, the comment, to "the one Ranvier 0.2.1 depends on" (core 0.2.1 depends on FSharp.Core 8.0.100, *from documentation*: §3.1).
- Unchanged: `README.md` (it describes the published 0.1.0 packages, which depend on 0.2.0; Plan T3 rewrites it), `docs/PROBES.md` and `docs/measurements/*` (dated records).

**Interfaces:**
- Consumes: nothing.
- Produces: every project restores `Ranvier` 0.2.1. No API changes; core 0.2.0 to 0.2.1 changed collection views only (`git -C C:/wk/ranvier-release/Ranvier log --oneline v0.2.0..v0.2.1`).

- [ ] **Step 1: Confirm the pins.** `git grep -n 'Include="Ranvier" Version' -- '*.fsproj'` lists exactly the three files above. `examples/Monitor/web/Monitor.Web.fsproj` pins no `Ranvier`: it reaches it through `Monitor.Model` and the D5 clone (`../../../../Ranvier.D5`, which states `Ranvier` 0.2.0 as a minimum), so restore resolves 0.2.1 for its whole graph.
- [ ] **Step 2: Edit** the three pins and the comment.
- [ ] **Step 3: Run the gate.** Expected: all pass with the counts unchanged (174 .NET, 171 JavaScript); `dist/tests/fable_modules` holds `Ranvier.0.2.1`, and `grep -rlF "Ranvier.0.2.0/" dist/tests --include=*.js | grep -v "/fable_modules/Ranvier.0.2.0/"` prints nothing. A test that fails after the bump is a finding: stop and report the test, its output and the core commit it points to. Do not edit its expectation.
- [ ] **Step 4: Browser build** (not in CI; needs the `Ranvier.D5` clone beside this repository): `dotnet fickl examples/Monitor/web -e .fs.js -o examples/Monitor/web/dist -c Release`, in the background: it may recompile every package that depends on `Ranvier` (the first build took 1299 s, *from documentation*: `README.md`, "The sequential monitor"). Expected: exit 0; `examples/Monitor/web/dist/fable_modules/project_cracked.json` holds `Ranvier.0.2.1/` source paths and no `Ranvier.0.2.0/` path; `grep -rlF "Ranvier.0.2.0/" examples/Monitor/web/dist --include=*.js | grep -v "/fable_modules/Ranvier.0.2.0/"` prints nothing.
  If a check fails: confirm no `dotnet fickl` process is running, delete `examples/Monitor/web/dist/fable_modules` (Fickl's git-ignored cache) and build again. If two `Ranvier` versions remain, add `<PackageReference Include="Ranvier" Version="0.2.1" />` to `Monitor.Web.fsproj`, build again and say so in the commit message. If they still remain, stop and report.
- [ ] **Step 5: Scripted runs**, which hide their window: `dotnet run -c Release --project examples/Monitor/native -- --script table` and `node examples/Monitor/web/run.mjs table`. Expected: each prints the table and ends with `script done 0`.
- [ ] **Step 6: Commit** `build: depend on core Ranvier 0.2.1`.

### Task 2: One unit-equality rule, checked unit scales, FSharp.Core's SI measures (I1, I2, I8)

**Files:**
- Modify: `src/Ranvier.Quantities/Units.fs`: `multiply`, `divide`, `power` (79-103), `sqrt` (122-135), `sameUnit` (145-148), `equivalent` (150-153, removed), measures `m kg s K` (177-193).
- Modify: `src/Ranvier.Quantities/Formula.fs`: the private `sameUnit` (240-253) is renamed `oneUnit` and tests with `UnitInfo.sameUnit`; `pureNumber` (255-259) tests with `UnitInfo.sameUnit`; `check` (262-283) turns a scale failure into `Error`.
- Modify tests: `UnitsTests.fs`, `FormulaTests.fs` (39 and 94: `equivalent` becomes `sameUnit`), `MonitorTests.fs` (541 and 550: the same).

**Interfaces:**
- Consumes: nothing new.
- Produces:

```fsharp
module UnitInfo =
    /// True when the dimensions are equal and |a.Scale − b.Scale| ≤ 1e-12 · max(|a.Scale|, |b.Scale|); symmetric.
    val sameUnit : UnitInfo -> UnitInfo -> bool
    // removed: UnitInfo.equivalent
    // multiply, divide, power and sqrt raise ArithmeticException when the resulting scale is not finite or is 0.
    // The message names the result: "The scale of km^103 is ∞; a unit's scale must be positive and finite."

[<Measure>] type m = Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols.m     // and kg, s, K the same way
// km and mK stay measures of their own.
```

`UnitOf`'s `*`, `/` and `UnitOf.squared` raise as `multiply`, `divide` and `power` do. `Formula.check` returns `Error` with the exception's message when a unit's scale leaves the positive finite numbers.

- [ ] **Step 1: Write the failing tests.** In `UnitsTests.fs` (`"Units"`): replace the two tests at 75-101 by the first row below, rename the test at 103-112 to the second row, and add the rest.

| Test | Asserts |
|---|---|
| `sameUnit joins two routes that differ in the last place` | `u = UnitOf.define<m> "u" Dimension.length 0.7`; `root` = `UnitInfo.sqrt (UnitInfo.power u.Info -2)`, `inverse` = `UnitInfo.power u.Info -1`: `root.Scale <> inverse.Scale`, `sameUnit root inverse` and `sameUnit inverse root` |
| `sameUnit separates sizes and dimensions` | the four assertions of the old test with `sameUnit`: mK and K false; `(km / m).Info` and `dimensionless` false; m and s false; m and m true |
| `sameUnit at the relative bound, in both orders` | `spacing = 1.0 / 4503599627370496.0` (2^-52, the spacing of doubles at 1); `at x = { Units.metre.Info with Scale = x }`; 1e-12 / 2^-52 = 4503.6 (closed form), so `sameUnit (at 1.0) (at (1.0 + 4503.0 * spacing))` and its reverse are true, and `sameUnit (at 1.0) (at (1.0 + 4504.0 * spacing))` and its reverse are false |
| `the SI measures are FSharp.Core's` | compiles and holds: `let x: float<UnitSymbols.m> = 2.0<m>`, the same for `kg`, `s`, `K`; `let metre: UnitOf<UnitSymbols.m> = Units.metre`; `Expect.equal (float x) 2.0` (open `Microsoft.FSharp.Data.UnitSystems.SI`) |
| `a scale that overflows or vanishes raises` | `Expect.throwsT<ArithmeticException>` and `Expect.throwsC` with `stringContains e.Message "km^103"` for `UnitInfo.power Units.kilometre.Info 103` (1000^103 = 1e309 > 1.797e308, closed form); throwsT for `power Units.kilometre.Info -108` (scale 0); for `multiply big.Info big.Info` with `big = UnitOf.define<m> "big" Dimension.length 1e200` (1e400); for `divide tiny.Info big.Info` with `tiny` at 1e-200 (1e-400 rounds to 0); for `UnitInfo.sqrt` of `{ Factors = [ "z", 2 ]; Dimension = { Dimension.none with Length = 2 }; Scale = 0.0 }` and of the same with `Scale = infinity`. `power Units.kilometre.Info 102` raises nothing |

In `FormulaTests.fs` (`"Formula"`), add:

| Test | Asserts |
|---|---|
| `a unit whose scale overflows is an error` | model with `Quantity.input model "x" "X" Units.kilometre 1.0<km>`: `check "x^103"` is `Error` containing `"km^103"`; `check "x^60 * x^60"` is `Error` containing `"km^120"`; `check "x^102"` is `Ok` |

- [ ] **Step 2: Run** `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "Units"`. Expected: the build fails (`UnitSymbols.m` and `m` are distinct measures) or the new tests fail.
- [ ] **Step 3: Implement.** One private check after each of the four operations raises the `ArithmeticException` above (`UnitInfo.symbol` of the result, `"1"` when it is empty). `Formula.check` wraps only its calls of `multiply`, `divide`, `power` and `sqrt` in `try … with e -> Result.Error e.Message`. Update the doc comments of the four operations, `sameUnit`, `UnitOf`'s operators, `UnitOf.squared` and `Formula.check`.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): one unit-equality rule and checked unit scales`.

### Task 3: A re-fold floor in `FloatPolicy.standard`, and the `Moments` reads (I3, I9, §6.12)

**Files:**
- Modify: `src/Ranvier.Numerics/FloatPolicy.fs:17-24`, `src/Ranvier.Numerics/Moments.fs` (after `variance`, line 108), `examples/Monitor/model/Monitor.fs:454` (`FloatPolicy.standard |> FloatPolicy.withRefoldFloor 64` becomes `FloatPolicy.standard`).
- Test: `tests/Ranvier.Science.Tests/AggregateTests.fs`, `tests/Ranvier.Science.Tests/MomentsTests.fs`.

**Interfaces:**
- Consumes: `Aggregate.sumBy`, `Sum.add`, `ulps` (existing).
- Produces:

```fsharp
FloatPolicy.standard : FloatPolicy     // { Compensated = true; RefoldAfterRemovals = true; RefoldFloor = 64 }
Moments.sampleVariance : Moments -> float          // M2 / (count − 1), never negative; NaN when the count is below 2
Moments.standardDeviation : Moments -> float       // sqrt (variance m); NaN when the count is 0
Moments.sampleStandardDeviation : Moments -> float // sqrt (sampleVariance m); NaN when the count is below 2
```

- [ ] **Step 1: Write the failing tests.** `MomentsTests.fs` (`"Moments"`):

| Test | Asserts |
|---|---|
| `the sample variance divides by n − 1` | over `0 .. 999`: `sampleVariance` within relative 1e-15 of `83333250.0 / 999.0` (closed form: Σ(k − 499.5)² = n(n² − 1)/12 = 83 333 250 for n = 1000); `sampleStandardDeviation` within relative 1e-15 of `sqrt (83333250.0 / 999.0)`; `standardDeviation` equals `sqrt 83333.25` |
| `two observations` | `[| 1.0; 3.0 |]`: `standardDeviation = 1.0`, `sampleVariance = 2.0`, `sampleStandardDeviation = sqrt 2.0`, all exact (the fold's mean is 2 and M2 is 2 exactly) |
| `fewer than two observations` | `Moments.zero`: all three NaN; `[| 3.5 |]`: `sampleVariance` and `sampleStandardDeviation` NaN, `standardDeviation = 0.0` |

`AggregateTests.fs` (`"Aggregate"`):

| Test | Asserts |
|---|---|
| `standard carries a floor of 64` | `FloatPolicy.standard.RefoldFloor = 64` |
| `standard waits for 64 removals on a small aggregate` | 10 rows `0.0 .. 9.0`, `sumBy FloatPolicy.standard`; after 63 edits (`rows.AddOrUpdate (struct (i % 10, float (100 + i)))`, each read) `Refolds () = 0`; after the 64th, `1` |
| `between re-folds under the standard floor a small sum stays within 2 ulp of the from-scratch fold` | 8 rows from `valueAt i = 1.0 + (float i * 3.14159) % 999_999.0`; edits `e = 1 .. N` (N = 1,000,000 on .NET, 100,000 under `FABLE_COMPILER`), each setting row `e % 8` to `valueAt (8 + e)`; at every edit `ulps maintained scratch <= 2.0`, with `scratch = values |> Array.fold (Sum.add true) Sum.zero |> Sum.value` (key order); at the end `Refolds () = N / 64`; prints `Re-fold floor drift: largest distance %g ulp over %d edits of 8 rows, %d re-folds` |

These existing tests count re-folds of fewer than 64 rows under `FloatPolicy.standard`; each takes `FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0` and keeps its expected counts, since its subject is the row-count rule or edit detection: `standard re-folds after as many removals as rows` (91-105; rename it `with no floor, re-folds after as many removals as rows`), the "no floor" case of `a floor delays the re-fold of a small aggregate` (369; add `Expect.equal (refoldsUnder FloatPolicy.standard) 4 "standard"`), and `a momentsBy row republished with an equal Moments` (214-240). Any other test that then fails on a re-fold count gets the same treatment; report which.

- [ ] **Step 2: Run** `-- --filter "Aggregate"` and `-- --filter "Moments"`. Expected: FAIL (`sampleVariance` is not defined; the floor is 0).
- [ ] **Step 3: Implement** the floor and the three reads; `sampleVariance` clamps a negative M2 to 0 as `variance` does.
- [ ] **Step 4: Measure.** Run the drift test on both targets (`-- --filter "Aggregate"`; `npx mocha dist/tests/Main.fs.js --grep "standard floor"`) and write its printed largest distances into `FloatPolicy.standard`'s doc comment: "Compensated, and re-folded once the removals since the last re-fold reach the larger of the row count and 64. Between re-folds a maintained sum of 8 rows stayed within D ulp of the from-scratch compensated fold over 1,000,000 edits on .NET and D′ ulp over 100,000 under JavaScript (*measured here*, <date>: the test `between re-folds under the standard floor …` in `tests/Ranvier.Science.Tests/AggregateTests.fs`, with the two commands)." The assertion stays at 2 ulp; a run that measures more stops the task: report it.
- [ ] **Step 5: Run the gate.** Expected: PASS.
- [ ] **Step 6: Commit** `feat(numerics): a re-fold floor of 64 in the standard policy, and sample variance and standard deviations`.

### Task 4: The burn-in moves into the confidence sequence (I4)

**Files:**
- Modify: `src/Ranvier.Statistics/ConfidenceSequence.fs` (`ofMomentsAfter`, 73-86, removed; the module's remarks, 27-35).
- Modify: `examples/Monitor/model/Monitor.fs:121` (`let BurnIn = ConfidenceSequence.standardBurnIn`) and `:348` (`ConfidenceSequence.interval (ConfidenceSequence.tunedFor a tightestAt.Value BurnIn) m`).
- Test: `tests/Ranvier.Science.Tests/ConfidenceSequenceTests.fs`.

**Interfaces:**
- Consumes: `ConfidenceSequence.radius`, `ofMoments`, `rhoTightestAt` (unchanged).
- Produces (namespace `Ranvier.Statistics`; Plan T3's `SequentialTest.create` takes `SequenceSettings`):

```fsharp
type SequenceSettings = { Alpha: float; Rho: float; BurnIn: float }   // BurnIn a count, as Moments.count is a float
module ConfidenceSequence =
    val settings : alpha: float -> rho: float -> burnIn: float -> SequenceSettings
    val tunedFor : alpha: float -> plannedCount: float -> burnIn: float -> SequenceSettings  // Rho = rhoTightestAt alpha plannedCount
    val standardBurnIn : float                                           // 30.0
    val interval : SequenceSettings -> Moments -> Interval               // NaN bounds while the count is below BurnIn
    // removed: ofMomentsAfter
```

`settings` raises `ArgumentOutOfRangeException` unless 0 < alpha < 1, rho is finite and above 0, and burnIn is finite and at least 1; `tunedFor` raises as `rhoTightestAt` and `settings` do; `interval` checks the record's fields by the same rules.

- [ ] **Step 1: Write the failing tests** (`"ConfidenceSequence"`). Migrate the four tests that read `ofMomentsAfter` or a manual `t >= 30`:

| Test | Asserts |
|---|---|
| `settings and tunedFor` | `settings 0.05 0.3 30.0 = { Alpha = 0.05; Rho = 0.3; BurnIn = 30.0 }`; `(tunedFor 0.05 1000.0 30.0).Rho = rhoTightestAt 0.05 1000.0`; `standardBurnIn = 30.0` |
| `no interval before the burn-in` (migrated, 154-165) | `interval (settings alpha rho 30.0)`: NaN bounds at 29 observations; at 30 the bounds of `ofMoments alpha rho` |
| `a burn-in out of range is rejected` (migrated, 167-174) | `settings` and `tunedFor` raise for burnIn 0.5, nan, infinity; `interval { Alpha = 0.05; Rho = 1.0; BurnIn = 0.5 } m` raises |
| `settings out of range are rejected` | `settings` raises for alpha 0, 1, nan and for rho 0, -1, infinity, nan; `tunedFor` raises for plannedCount 0, infinity, nan; `interval` raises for records with `Alpha = 1.0` and with `Rho = 0.0` |
| `the sequence covers under continuous monitoring and the fixed-sample interval does not` (migrated, 177-200) | `misses` reads the sequence through `interval (tunedFor alpha 1000.0 standardBurnIn)` and keeps `t >= 30` for `FixedSample.ofMoments`; bounds as today. Its printed fraction stays 0.0415 (*from documentation*: `docs/PROBES.md`, "Coverage"); a different figure means the migration changed the arithmetic: stop and report |
| `the sequence covers at the page's setting` (migrated, 202-228) | reads through `interval (tunedFor (0.05 / 4.0) 1000.0 standardBurnIn)`; bound as today |

- [ ] **Step 2: Run** `-- --filter "ConfidenceSequence"`. Expected: the build fails (`settings` is not defined).
- [ ] **Step 3: Implement** and migrate `Monitor.fs`. `interval` returns `{ Lower = nan; Upper = nan }` below the burn-in and `ofMoments s.Alpha s.Rho m` from it on.
- [ ] **Step 4: Measure and document.** Run the two coverage tests on both targets and write the printed fractions into the module's remarks, one sentence per setting: α = 0.05 and α = 0.0125 (0.05 over four channels), each with ρ = `rhoTightestAt α 1000`, burn-in 30, 2,000 standard normal streams of 10,000 observations, *measured here* with the date and both commands; name both targets' figures when they differ. Point the remarks of `radius` and `ofMoments` to `interval` for the burn-in.
- [ ] **Step 5: Run the gate.** Expected: PASS.
- [ ] **Step 6: Commit** `feat(statistics): the burn-in belongs to the confidence sequence's settings`.

### Task 5: `Uncertain`, first-order uncertainty (§6.11)

**Files:**
- Create: `src/Ranvier.Numerics/Uncertain.fs` (last in `Ranvier.Numerics.fsproj`'s Compile list), `tests/Ranvier.Science.Tests/UncertainTests.fs`.

**Interfaces:**
- Consumes: nothing.
- Produces (namespace `Ranvier.Numerics`):

```fsharp
[<NoComparison>]
type Uncertain          // a value with its contributions by source; structural equality
    // operators: + - * / between two Uncertains and with a float on either side; unary minus
    // static members Sqrt Exp Log Log10 Sin Cos Tan Asin Acos Atan Sinh Cosh Tanh Abs, and
    // Pow (Uncertain, float) and Pow (Uncertain, Uncertain), so F#'s sqrt … abs and ** apply
module Uncertain =
    val exact : float -> Uncertain
    val source : standardUncertainty: float -> value: float -> Uncertain    // a new, independent source
    val value : Uncertain -> float
    val standardUncertainty : Uncertain -> float
    val covariance : Uncertain -> Uncertain -> float
    val correlation : Uncertain -> Uncertain -> float
    val contributions : Uncertain -> struct (int * float)[]                 // source id, signed contribution
    val map : f: (float -> float) -> df: (float -> float) -> Uncertain -> Uncertain
    val map2 : f: (float -> float -> float) -> dfx: (float -> float -> float) -> dfy: (float -> float -> float)
               -> Uncertain -> Uncertain -> Uncertain
```

Choices: the representation is a private record `{ Value: float; Sources: int[]; Contributions: float[] }`, sources ascending, so the F# record equality is structural; a contribution that cancels to 0 stays listed; `contributions` returns ascending ids. `standardUncertainty` is `sqrt` of the sum of squared contributions in id order (it overflows to +∞ when a contribution exceeds about 1e154 in magnitude, *general knowledge*); `covariance` sums products over shared sources; `correlation` is covariance over the product of the standard uncertainties, NaN when either is 0. `source` raises `ArgumentOutOfRangeException` for a negative standard uncertainty and accepts NaN and +∞. Ids come from a counter starting at 1: `System.Threading.Interlocked.Increment` on .NET, a plain increment under `FABLE_COMPILER`. The derivatives, stated in each member's doc comment: √x → 1/(2√x); eˣ → eˣ; ln x → 1/x; log₁₀ x → 1/(x ln 10); sin → cos; cos → −sin; tan → 1 + tan²; asin → 1/√(1 − x²); acos → −1/√(1 − x²); atan → 1/(1 + x²); sinh → cosh; cosh → sinh; tanh → 1 − tanh²; |x| → the sign of x, +1 at 0 and −0 (first-order propagation does not hold at 0; the contribution keeps its magnitude); x ** y (y a float) → y·x^(y−1); x ** y (both uncertain) → y·x^(y−1) and x^y·ln x.

- [ ] **Step 1: Write the failing tests** (`"Uncertain"`; closed forms written in each test):

| Test | Asserts |
|---|---|
| `an exact value has no uncertainty` | `exact 2.5`: value 2.5, standard uncertainty 0.0, `contributions` empty |
| `each source is new and independent` | `a`, `b` = `source 0.3 1.0` twice: each standard uncertainty 0.3; one contribution each; ids differ; `a <> b`; `covariance a b = 0.0`; `correlation a b = 0.0` |
| `independent contributions add in quadrature` | `a = source 3.0 10.0`, `b = source 4.0 20.0`: `a + b` value 30, standard uncertainty 5.0 exactly; `a - b` 5.0; `contributions (a + b) = [| struct (idA, 3.0); struct (idB, 4.0) |]` with idA < idB |
| `a shared source cancels` | `a - a`: value 0, standard uncertainty 0.0; `a + a`: 6.0; `correlation a (2.0 * a)` within 1e-15 of 1; `correlation a (-a)` within 1e-15 of −1 |
| `covariance of combinations` | `covariance (a + b) (a - b) = -7.0` (3² − 4²); `correlation` within 1e-15 of −7/25 |
| `products and quotients` | `x = source 0.1 3.0`, `y = source 0.2 4.0`: `x * y` value 12, standard uncertainty within relative 1e-15 of `sqrt ((0.1 * 4.0) ** 2.0 + (0.2 * 3.0) ** 2.0)`; `x / y` value 0.75, within 1e-15 of `sqrt ((0.1 / 4.0) ** 2.0 + (0.2 * 3.0 / 16.0) ** 2.0)` |
| `floats on either side` | `2.0 * x = x * 2.0`; `x + 1.0` and `1.0 + x` value 4 with x's contribution; `1.0 - x` contribution −0.1; `1.0 / x` value `1.0 / 3.0`, contribution within 1e-15 of `-0.1 / 9.0`; `x / 2.0` contribution 0.05; `-x` value −3, contribution −0.1 |
| `the F# functions apply` | `z = source 0.01 0.5`; for each of sqrt, exp, log, log10, sin, cos, tan, asin, acos, atan, sinh, cosh, tanh, abs with its float function `g` and the derivative `dg` listed above: `value (f z) = g 0.5` exactly; its one contribution within relative 1e-14 of `dg 0.5 * 0.01`, sign included |
| `abs at zero keeps the magnitude` | `abs (source 0.1 0.0)`: contribution +0.1; `abs (source 0.1 -2.0)`: −0.1 |
| `powers` | `p = source 0.1 2.0`, `q = source 0.2 3.0`: `p ** 3.0` value 8, contribution within 1e-14 of 3·2²·0.1; `p ** q` value 8, contributions within 1e-14 of 3·2²·0.1 and 8·ln 2·0.2 |
| `map and map2` | `map (fun v -> v * v) (fun v -> 2.0 * v) x`: value 9, contribution within 1e-15 of 0.6; `map2 (*) (fun _ b -> b) (fun a _ -> a) x y`: standard uncertainty within 1e-15 of that of `x * y` |
| `a NaN standard uncertainty propagates` | `n = source nan 1.0`: standard uncertainty NaN; `n + x`: value 4, standard uncertainty NaN; `correlation n x` NaN |
| `a negative standard uncertainty is rejected` | `throwsT<ArgumentOutOfRangeException>` for `source -0.1 1.0` |
| `equal arithmetic gives equal values` | `x + 1.0 = x + 1.0`; `source 0.1 1.0 <> source 0.1 1.0` |
| `.NET: sources made on two threads are distinct` (`#if !FABLE_COMPILER`) | 10,000 sources on each of two `offThread` calls and 10,000 on the caller: 30,000 distinct ids |

- [ ] **Step 2: Run** `-- --filter "Uncertain"`. Expected: the build fails (`Uncertain` is not defined).
- [ ] **Step 3: Implement** `Uncertain.fs`. Binary operations merge the two sorted source arrays in one pass.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(numerics): Uncertain, first-order propagation through shared sources`.

### Task 6: Entries with uncertainty (I5, first part; §8.4)

**Files:**
- Modify: `src/Ranvier.Quantities/Model.fs` (`Entry`, 18-31), `src/Ranvier.Quantities/Quantity.fs`.
- Test: `tests/Ranvier.Science.Tests/QuantityTests.fs`.

**Interfaces:**
- Consumes: `Uncertain.source`, `Uncertain.map`, `Uncertain.value`, `Uncertain.standardUncertainty` (Task 5).
- Produces:

```fsharp
type Entry =
    { Id: string; Name: string; Unit: UnitInfo; Kind: QuantityKind; Source: string
      Read: unit -> float; Status: unit -> QuantityStatus
      /// A tracked read of the standard uncertainty in Unit: 0 for an exact value, NaN when unknown.
      Uncertainty: unit -> float
      /// A tracked read of the value with its contributions by source.
      Measured: unit -> Uncertain
      /// The formula text of an entry declared by Quantity.formula; "" otherwise.
      Formula: string }

module Quantity =
    val inputWithUncertainty : Model -> id: string -> name: string -> unit: UnitOf<'u> -> initial: float<'u>
                               -> uncertainty: float<'u> -> Input<'u>
    val measuredConstant : Model -> id: string -> name: string -> unit: UnitOf<'u> -> value: float<'u>
                           -> uncertainty: float<'u> -> source: string -> Quantity<'u>
    val deriveMeasured : Model -> id: string -> name: string -> unit: UnitOf<'u> -> compute: (unit -> Uncertain)
                         -> Quantity<'u>
type Input<'u> with
    member SetUncertainty : float<'u> -> unit
```

Choices: every input, constant and derived entry owns one `Uncertain` source, allocated at declaration as `unitSource = Uncertain.source 1.0 0.0`; its `Measured` is `Uncertain.map (fun _ -> value) (fun _ -> u) unitSource` with the current value and standard uncertainty, so every reader shares the source. An input holds its uncertainty in a second signal (0 for `input`). `derive` and `deriveWithin` entries have `Uncertainty = NaN` and a NaN contribution, and their doc comment points to `deriveMeasured`. `deriveMeasured` creates two memos: the `Uncertain` (it publishes on every recomputation) and the value read from it (a `float`, which cuts off); `Read` and `Status` follow the second, `Uncertainty` and `Measured` the first. `measuredConstant` is `QuantityKind.Constant`; `deriveMeasured` is `Derived`. A negative uncertainty raises `ArgumentOutOfRangeException` in `inputWithUncertainty`, `SetUncertainty` and `measuredConstant`; NaN is accepted and reads NaN.

- [ ] **Step 1: Write the failing tests** (`"Quantity"`, inside `withGraph`):

| Test | Asserts |
|---|---|
| `an input carries a source of uncertainty 0` | `input … 2.0`: `Uncertainty () = 0.0`; `Measured ()` value 2.0, standard uncertainty 0.0, one contribution; `Formula = ""` |
| `inputWithUncertainty and SetUncertainty` | `b` = 5.0 ± 0.25 m: `Uncertainty () = 0.25`; `Measured ()` standard uncertainty 0.25; a memo over `b.Entry.Uncertainty ()` counts runs: `SetUncertainty 0.5<m>` gives 0.5 and one more run; a second `SetUncertainty 0.5<m>` no run; after `b.Set 6.0<m>` the contribution's id is the one before |
| `an uncertainty must not be negative` | `throwsT<ArgumentOutOfRangeException>` for `inputWithUncertainty … -0.1`, `SetUncertainty -0.1`, `measuredConstant … -0.1 …`; `inputWithUncertainty … nan` reads `Uncertainty ()` NaN |
| `exact and measured constants` | `constant`: `Uncertainty () = 0.0`, standard uncertainty 0; `measuredConstant model "g" "G" Units.one 9.8 0.1 "a source"`: `Kind = Constant`, `Source = "a source"`, `Read () = 9.8`, `Uncertainty () = 0.1`, `Measured ()` standard uncertainty 0.1 |
| `derived entries have an unknown uncertainty` | `derive` and `deriveWithin`: `Uncertainty ()` NaN; `Measured ()` value equals `Read ()`, standard uncertainty NaN |
| `deriveMeasured propagates from the entries it reads` | `a` = 3 ± 0.1 m, `b` = 4 ± 0.2 m, `area = deriveMeasured model "area" "Area" (UnitOf.squared Units.metre) (fun () -> a.Entry.Measured () * b.Entry.Measured ())`: `area.Value = 12.0<m^2>`; `Uncertainty ()` within relative 1e-15 of `sqrt ((0.1 * 4.0) ** 2.0 + (0.2 * 3.0) ** 2.0)`; `Kind = Derived` |
| `two entries that read one input are correlated` | `d1` = `deriveMeasured` of `2.0 * a`, `d2` of `-a`: `Uncertain.correlation (d1.Entry.Measured ()) (d2.Entry.Measured ())` within 1e-15 of −1 |
| `an uncertainty change leaves deriveMeasured's value readers unnotified` | an effect over `area.Value` counts runs: `a.SetUncertainty 0.2<m>` adds no run and changes `area.Entry.Uncertainty ()`; `a.Set 5.0<m>` adds one run |

- [ ] **Step 2: Run** `-- --filter "Quantity"`. Expected: the build fails (`inputWithUncertainty` is not defined).
- [ ] **Step 3: Implement.** `Input<'u>`'s internal constructor gains the uncertainty signal; the private `entry` helper of `Quantity.fs` gains the three fields. Doc comments: each new function; `input` says "uncertainty 0"; `derive` and `deriveWithin` point to `deriveMeasured`; `Entry`'s three fields as above.
- [ ] **Step 4: Run the gate.** Expected: PASS. Nothing outside `Quantity.fs` builds an `Entry` (`git grep -n "Status = " -- '*.fs'` finds `Quantity.fs` alone).
- [ ] **Step 5: Commit** `feat(quantities): an uncertainty on every entry, measured constants and derived values`.

### Task 7: Formula entries and typed views (I5, second part; §8.4)

**Files:**
- Modify: `src/Ranvier.Quantities/Ranvier.Quantities.fsproj` (Compile order `Units.fs Model.fs Formula.fs Quantity.fs Run.fs`: `Quantity.formula` calls `Formula`, and `Formula` uses only `Model`, `Entry` and `UnitInfo`), `src/Ranvier.Quantities/Formula.fs`, `src/Ranvier.Quantities/Quantity.fs`.
- Create: `tests/Ranvier.Science.Tests/FormulaEntryTests.fs` (after `FormulaTests.fs` in `TestFiles.props` and the Fable `Main.fs`).

**Interfaces:**
- Consumes: `Entry.Measured`, `Entry.Uncertainty`, `Entry.Formula`, `Input.SetUncertainty`, `Quantity.inputWithUncertainty` (Task 6); the `Uncertain` operators and static members (Task 5); `UnitInfo.sameUnit` (Task 2).
- Produces:

```fsharp
module Formula =
    /// Raises ArgumentException for an identifier the model lacks.
    val measuredEvaluator : Model -> Expr -> (unit -> Uncertain)
module Quantity =
    /// Raises ArgumentException as input does; Error for text that does not parse or check, and then declares nothing.
    val formula : Model -> id: string -> name: string -> text: string -> Result<Entry, string>
    /// Error unless UnitInfo.sameUnit unit.Info entry.Unit.
    val view : UnitOf<'u> -> Entry -> Result<Quantity<'u>, string>
```

Choices: `measuredEvaluator` resolves names once, at creation, as `evaluator` does, and its value has the evaluator's bits: `Number` is `Uncertain.exact`; `+ − * /` and unary minus are `Uncertain`'s; `Power (a, n)` is `Uncertain.map` of the evaluator's private `integerPower` with derivative `float n * integerPower v (n − 1)`; `sqrt abs exp ln log10` are the static members; `min` and `max` return the operand whose value `Math.Min`/`Math.Max` returns (the first on a tie), and `Uncertain.exact nan` when either value is NaN. A formula entry is `QuantityKind.Derived` with `Source = ""`, its `Unit` the unit `Formula.check` gives, and two memos: the value (`float`, cuts off) and the measured value (an `Uncertain`, publishes on every recomputation, computed only when read); `Read` and `Status` follow the first, `Uncertainty` and `Measured` the second. The identifier is checked (and raises) before the text is parsed.

- [ ] **Step 1: Write the failing tests** (`"FormulaEntry"`, inside `withGraph`; a private page model copied from `FormulaTests.fs`'s `pageWithMean` (19-29: `mean`, `se`, `target`, `variance`, `n`, `alpha`, `rate`, `temp_k`), with `mean` = 7.3 ± 0.2 mK and `target` = 2.1 ± 0.1 mK declared through `inputWithUncertainty`):

| Test | Asserts |
|---|---|
| `measuredEvaluator gives the evaluator's value` | for each of `(mean - target) / se`, `mean * 2 + target`, `sqrt(variance)`, `exp(mean / se)`, `min(mean, target)`, `max(mean, target)`, `mean^3`, `mean^-2`, `-mean`, `abs(mean - target)`, `ln(n)`, `log10(n)`: `Uncertain.value (measuredEvaluator …) ()` equals `(evaluator …) ()` exactly |
| `measuredEvaluator propagates` | inputs `a` = 10 ± 3 and `b` = 20 ± 4 (dimensionless): `a + b` standard uncertainty 5.0 exactly; `a - a` 0.0; `2 * a` 6.0; `a^2` within relative 1e-15 of 60 (2·10·3) |
| `measuredEvaluator raises for a name the model lacks` | `throwsT<ArgumentException>` for `mean + nope` |
| `a formula entry` | `Quantity.formula model "excess" "Excess" "mean - target"` is `Ok e`: `e.Formula = "mean - target"`, `e.Kind = Derived`, `e.Id`, `e.Name`, `sameUnit e.Unit Units.millikelvin.Info`, `e.Read () = 7.3 - 2.1`, `e.Status () = Ready`, `(model.TryFind "excess").Value.Id = "excess"` |
| `a formula entry tracks its reads and cuts off` | an effect over `e.Read ()` counts runs: `mean.Set 9.9<mK>` adds one run and reads `9.9 - 2.1`; a write of `target`'s current value adds none |
| `an uncertainty change leaves a formula entry's value readers unnotified` | the same effect: `mean.SetUncertainty 0.4<mK>` adds no run; `e.Uncertainty ()` becomes `sqrt (0.4 ** 2.0 + 0.1 ** 2.0)` within relative 1e-15 |
| `a formula can read a formula entry` | `Quantity.formula model "doubled" "Doubled" "excess * 2"`: `Read () = (7.3 - 2.1) * 2.0` |
| `a formula that does not parse or check declares nothing` | `"mean +"` and `"mean + rate"` give `Error` (the first containing `"character"`); `model.TryFind "bad"` is `ValueNone`; a later `Quantity.formula model "bad" … "mean"` is `Ok` |
| `a formula entry's identifier follows input's rules` | `throwsT<ArgumentException>` for a second `"excess"` and for `"2x"` |
| `a pending leaf leaves a formula entry pending` | over a stream fold with no item: `Status () = Pending`; after an item, `Ready` and the value |
| `view` | `Quantity.view Units.millikelvin e` is `Ok q` with `q.Value = (7.3 - 2.1) * 1.0<mK>`; `view Units.kelvin e` is `Error` containing `"mK"` and `"K"`; `view Units.metre e` is `Error`; `view (UnitOf.squared Units.millikelvin)` of a formula entry `"mean^2"` is `Ok`; `view Units.millikelvin` of `"mean * mean / mean"` is `Ok` (its scale can differ in the last place) |

- [ ] **Step 2: Run** `-- --filter "FormulaEntry"`. Expected: the build fails (`Quantity.formula` is not defined).
- [ ] **Step 3: Implement.** Move `Formula.fs` before `Quantity.fs`; add `measuredEvaluator` beside `evaluator`; add `formula` and `view` to `Quantity`.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): formula entries, typed views and the measured evaluator`.

### Task 8: An entry leaves its model with its owner (I6; §8.5)

**Files:**
- Modify: `src/Ranvier.Quantities/Model.fs` (removal; the type's doc comment), `src/Ranvier.Quantities/Quantity.fs` (each declaration registers one cleanup), `src/Ranvier.Quantities/Formula.fs` (doc comments of `evaluator` and `measuredEvaluator`).
- Create: `tests/Ranvier.Science.Tests/LifetimeTests.fs` (after `FormulaEntryTests.fs`). Modify: `tests/Ranvier.Science.Tests/MonitorTests.fs` (one test).

**Interfaces:**
- Consumes: every declaration of `Quantity` (Tasks 6 and 7: `input`, `inputWithUncertainty`, `constant`, `measuredConstant`, `derive`, `deriveWithin`, `deriveMeasured`, `formula`, `block`, `blockFrom`); core `onCleanup`, `createRoot`, `createEffect`, `Graph.TryCurrent` (core v0.2.1 `Api.fs`, `Core.fs`).
- Produces: no new signature. Each declaration registers, through `onCleanup`, one cleanup with the current owner that removes the entry (or the block's reserved identifier) from its model; after it runs, `TryFind` no longer finds the entry and the identifier may be declared again.

Choices, from core v0.2.1 (read `Api.fs` `onCleanup`, and `Core.fs` `currentOwner` (2449), `AddCleanup` (2825-2834), `Graph.Current` (2565), `Dispose` (2861-2865)):
- The cleanup is registered before the entry is added, so a registration that raises leaves the model unchanged.
- With no active graph (`Graph.TryCurrent` is `ValueNone`), a declaration registers nothing and stays for good, as in 0.1.0; only `constant` and `measuredConstant` can be declared then, since the others create nodes.
- Under a graph and outside any `createRoot`, effect, owning memo or projection factory, the owner is the graph's root scope: the entry leaves its model when the graph is disposed.
- Inside a pure body (a `createMemo` computation, a projection row's reader), `onCleanup` raises `InvalidOperationException`, and so does every declaration.
- Inside an effect, the declarations are removed before each re-run (the core runs an effect's cleanups then).
- A cleanup removes only the declaration that registered it.

Doc comments: `Model`'s remarks state the lifetime rules above and that names resolve once, when an entry or evaluator is created, so a formula entry or evaluator that outlives the owner of an entry it reads keeps reading that entry's last value and does not follow a later declaration of the identifier; the same sentence goes on `Quantity.formula`, `Formula.evaluator` and `Formula.measuredEvaluator`. A read through a removed entry's functions behaves as the core's disposed node does.

- [ ] **Step 1: Write the failing tests** (`"Lifetime"`):

| Test | Asserts |
|---|---|
| `each declaration leaves its model with its owner` | for each of the ten declarations, as `(string -> unit)` closures declaring `"x"`: `let owner = createRoot (fun o -> declare "x"; o)`; before disposal, `TryFind "x"` finds it (for `block` and `blockFrom`: a second `Quantity.input model "x"` raises `ArgumentException`); after `owner.Dispose ()`, `TryFind "x"` is `ValueNone`, `model.Entries` holds no `"x"`, and `Quantity.input model "x" …` succeeds |
| `an entry outside any scope leaves with the graph` | a graph created and activated in the test (`use g = new Graph ()`, `use _ = g.Activate ()`) declares an input; after the graph is disposed, `TryFind` is `ValueNone` |
| `a constant declared with no active graph stays` | outside any graph: `Quantity.constant model "k" "K" Units.one 2.0 "a source"`; `TryFind "k"` finds it |
| `a declaration inside a pure memo raises` | `createMemo (fun _ -> Quantity.constant model "k" … |> ignore; 1.0)`: reading it raises (`throwsT<InvalidOperationException>`); `TryFind "k"` is `ValueNone` |
| `declarations inside an effect are replaced on each re-run` | `trigger = createSignal 1.0`; an effect declares `Quantity.input model "x" "X" Units.one trigger.Value`; `trigger.Value <- 2.0` re-runs it without raising; `(model.TryFind "x").Value.Read () = 2.0`; `model.Entries` ids are `[ "x" ]` |
| `a re-declared identifier is not followed` | `a` = 1.0 declared under a root; the formula entry `"f"` (`"a * 2"`), an `evaluator` and a `measuredEvaluator` of `"a * 2"` are built outside it; `a.Set 3.0`; the root is disposed and `"a"` declared again at 10.0: the formula entry, the evaluator and the measured value still read 6.0; an evaluator built now reads 20.0 |

`MonitorTests.fs`: add `a removed channel's entries leave its row's model`: `monitorWith 2`, `let a = rowOf m "A"`, `Monitor.removeChannel m "A"`, `readPage m`: `Expect.isEmpty a.Model.Entries`.

- [ ] **Step 2: Run** `-- --filter "Lifetime"`. Expected: FAIL (an entry stays after its owner is disposed; the effect's re-run raises "already declares").
- [ ] **Step 3: Implement** the removal in `Model` (from its entries, its identifier index and its reserved identifiers, keeping the order of the rest) and the registration in each declaration; write the doc comments.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): an entry leaves its model when its owner is cleaned up`.

### Task 9: The unit catalogue (§8.1)

**Files:**
- Modify: `src/Ranvier.Quantities/Units.fs`: add the `Prefix` record and module, the `UnitCatalogue` class, `UnitInfo.prefixed`, `UnitOf.prefixed`; move the measure types and `module Units` out.
- Create: `src/Ranvier.Quantities/Catalogue.fs` (after `Units.fs`): every measure type, `module Units`, and `[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>] module UnitCatalogue`. Create `tests/Ranvier.Science.Tests/CatalogueTests.fs` (after `UnitsTests.fs`).

**Interfaces:**
- Consumes: `UnitOf.define`, `UnitInfo.multiply`, `sameUnit` (Task 2).
- Produces:

```fsharp
type Prefix = { Symbol: string; Factor: float }
module Prefix =     // the 24 SI prefixes
    val quecto, ronto, yocto, zepto, atto, femto, pico, nano, micro, milli, centi, deci,
        deca, hecto, kilo, mega, giga, tera, peta, exa, zetta, yotta, ronna, quetta : Prefix
module UnitInfo =
    /// Raises ArgumentException unless u has one factor with exponent 1; the factor's name gains the prefix.
    val prefixed : Prefix -> UnitInfo -> UnitInfo
module UnitOf =
    val prefixed : Prefix -> UnitOf<'u> -> UnitOf<'v>        // the caller's annotation fixes 'v, as in define
[<Sealed>]
type UnitCatalogue                                           // immutable
    // internal member TryFind : symbol: string -> UnitInfo voption  — used by UnitInfo.parse (Task 10)
module UnitCatalogue =
    val si : UnitCatalogue
    val add : symbol: string -> UnitInfo -> prefixable: bool -> UnitCatalogue -> UnitCatalogue
    val tryFind : symbol: string -> UnitCatalogue -> UnitInfo voption
```

Layout of `Units.fs`: types `Dimension`, `UnitInfo`, `Prefix`; `module Dimension`; `module Prefix`; an internal function applying a prefix to a one-factor unit; the class `UnitCatalogue` over a `Map<string, struct (UnitInfo * bool)>` with the internal `TryFind`; `module UnitInfo`; `UnitOf`. `UnitInfo.parse` (Task 10) belongs to `module UnitInfo` and reads the catalogue through the class's member, which is why the class precedes the module and `si` lives in `Catalogue.fs`.

Rules: `TryFind` reads an exact symbol first, then a prefix followed by a prefixable symbol, trying `"da"` before `"d"`; a prefixed result's factor is the prefix's symbol followed by the unit's, with scale `Factor · Scale`. Before lookup, `TryFind` and `add` replace `μ` (U+03BC) by `µ` (U+00B5) and `Ω` (U+2126) by `Ω` (U+03A9), and a result carries the replaced symbol. `add` raises `ArgumentException` for an empty symbol, one the catalogue holds, one with a space or any of `·*./()^[],-−×÷`, an ASCII digit or a superscript digit or `⁻`, and for `prefixable = true` on a unit that is not one factor with exponent 1; `ArgumentOutOfRangeException` unless the scale is positive and finite. `prefixed` raises `ArithmeticException` as `multiply` does when the scale overflows.

The catalogue, one `Units` value and measure type per row. Dimensions: L length, M mass, T time, I current, Θ temperature, N amount, J luminosity. A measure marked "=" is an abbreviation of `Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols`' measure of that name (I8); the others are measures of their own. Prefixable: **P**.

| Symbol | Measure | `Units.` | Dimension | Scale | |
|---|---|---|---|---|---|
| m, kg, s, K | = (Task 2) | metre, kilogram, second, kelvin | L; M; T; Θ | 1 | P except kg |
| A, mol, cd | = | ampere, mole, candela | I; N; J | 1 | P |
| rad, sr | rad, sr | radian, steradian | none | 1 | P |
| Hz, N, Pa, J, W | = | hertz, newton, pascal, joule, watt | T⁻¹; M L T⁻²; M L⁻¹ T⁻²; M L² T⁻²; M L² T⁻³ | 1 | P |
| C, V, F, Ω, S | = (`ohm` for Ω) | coulomb, volt, farad, ohm, siemens | T I; M L² T⁻³ I⁻¹; M⁻¹ L⁻² T⁴ I²; M L² T⁻³ I⁻²; M⁻¹ L⁻² T³ I² | 1 | P |
| Wb, T, H, lm, lx | = | weber, tesla, henry, lumen, lux | M L² T⁻² I⁻¹; M T⁻² I⁻¹; M L² T⁻² I⁻²; J; J L⁻² | 1 | P |
| Bq, Gy, Sv, kat | = | becquerel, gray, sievert, katal | T⁻¹; L² T⁻²; L² T⁻²; N T⁻¹ | 1 | P |
| km, cm, mm, µm, nm | km (exists), cm, mm, um, nm | kilometre, centimetre, millimetre, micrometre, nanometre | L | 1e3, 1e-2, 1e-3, 1e-6, 1e-9 | |
| g, mg | g, mg | gram, milligram | M | 1e-3, 1e-6 | P for g |
| ms, µs, ns | ms, us, ns | millisecond, microsecond, nanosecond | T | 1e-3, 1e-6, 1e-9 | |
| mK | mK (exists) | millikelvin | Θ | 1e-3 | |
| kPa, MPa, kJ, kW, MW | kPa, MPa, kJ, kW, MW | kilopascal, megapascal, kilojoule, kilowatt, megawatt | as Pa, Pa, J, W, W | 1e3, 1e6, 1e3, 1e3, 1e6 | |
| mA, mV, kV | mA, mV, kV | milliampere, millivolt, kilovolt | as A, V, V | 1e-3, 1e-3, 1e3 | |
| min, h, d | **minute**, h, d | minute, hour, day | T | 60, 3600, 86400 | |
| au | au | astronomicalUnit | L | §8.1's value, IAU 2012 Resolution B2 | |
| L, t | L, t | litre, tonne | L³; M | 1e-3, 1e3 | P |
| eV, Da | eV, Da | electronvolt, dalton | M L² T⁻²; M | e in J; m_u in kg (CODATA 2022, NIST) | P |
| ° | deg | degree | none | `Math.PI / 180.0` | |
| a, ly, pc | a, ly, pc | julianYear, lightYear, parsec | T; L; L | 31 557 600; c (exact, CODATA 2022) × 31 557 600; 648 000/π au (IAU 2015 Resolution B2) | |
| bar, atm | bar, atm | bar, atmosphere | as Pa | 1e5, 101 325 | |

The minute's measure is `minute`, not `min`: a measure type named `min` hides FSharp.Core's `min` after `open Ranvier.Quantities` (Review Focus 1), and breaks this repository's own `min dt 0.1` (`examples/Monitor/model/Monitor.fs:510`) and `min maxStep …` (`src/Ranvier.Quantities/Run.fs:85`). Before writing the au, eV, Da, ly and pc scales, confirm each against its source (SI Brochure 9th edition Table 8, CODATA 2022 from NIST, IAU 2012 B2, IAU 2015 B2, and the IAU's definitions of the Julian year and the light-year) and cite it in the value's doc comment; fetch e and m_u from NIST as the Global Constraints say.

- [ ] **Step 1: Write the failing tests** (`"Catalogue"`; the file opens `Ranvier.Quantities`):

| Test | Asserts |
|---|---|
| `every unit of the table` | a list of `(UnitInfo, symbol, Dimension, scale)` for every row: `UnitInfo.symbol` equals the symbol, the dimension and the scale equal the table's (scales from the SI Brochure, 9th edition, Tables 2, 4 and 8; eV and Da from CODATA 2022 with the NIST page and access date; au, pc, a and ly from the IAU sources above); `UnitCatalogue.tryFind symbol UnitCatalogue.si` gives the same symbol and `sameUnit` |
| `the 24 prefixes` | symbol and factor of each, `1e-30 … 1e30` (SI Brochure, 9th edition, Table 7, with the four prefixes of the 27th CGPM, 2022, Resolution 3) |
| `prefixed units` | `UnitInfo.prefixed Prefix.kilo Units.metre.Info`: symbol `"km"`, `sameUnit` with `Units.kilometre.Info`; `prefixed` of `(Units.metre / Units.second).Info` and of `(UnitOf.squared Units.metre).Info` raise `ArgumentException`; `(UnitOf.prefixed Prefix.micro Units.second : UnitOf<us>).Info` is `sameUnit` with `Units.microsecond.Info` |
| `tryFind takes an exact symbol first` | `"min"`, `"Pa"`, `"cd"`, `"mm"`, `"Gy"`, `"T"`, `"h"`, `"kg"` give minute, pascal, candela, millimetre, gray, tesla, hour, kilogram |
| `tryFind then a prefix and a prefixable symbol` | `"hPa"` symbol `"hPa"`, scale 100; `"dam"` scale 10 (`"da"` before `"d"`); `"dL"` scale within relative 1e-15 of 1e-4; `"keV"` 1000 × eV's scale; `"GHz"` 1e9; `"µg"` within 1e-15 of 1e-9 |
| `look-alike characters read the catalogue's symbols` | `"μg"` (U+03BC) gives symbol `"µg"` (U+00B5) and the scale of `"µg"`; `"kΩ"` (U+2126) gives `"kΩ"` (U+03A9) |
| `prefixes attach to g, not to kg, and only to prefixable units` | `tryFind ("Q" + s)` is `ValueSome` exactly for the **P** symbols of the table (`Q` matches no exact symbol); `"kkg"`, `"mkg"`, `"kmin"`, `"kau"`, `"Mbar"`, `"Mpc"`, `"kkm"` give `ValueNone` |
| `add` | `widget = { Factors = [ "widget", 1 ]; Dimension = Dimension.length; Scale = 2.5 }` (a made-up unit): `add "widget" widget false si` finds `"widget"` and not `"kwidget"`; with `true`, `"kwidget"` has scale 2500; `si` still lacks `"widget"`; `add` raises for `"m"`, `""`, `"a b"`, `"x/y"`, `"x2"`, for `true` on a two-factor unit, and (`ArgumentOutOfRangeException`) for scale 0 |
| `opening the namespace keeps FSharp.Core's min and max` | `Expect.equal (min 2 3) 2`; `Expect.equal (max 2.0 3.0) 3.0` |

- [ ] **Step 2: Run** `-- --filter "Catalogue"`. Expected: the build fails (`UnitCatalogue` is not defined).
- [ ] **Step 3: Implement** in the layout above, with a doc comment on each `Units` value whose scale has a source beyond the SI Brochure, and on `UnitCatalogue.si` listing the prefixable units.
- [ ] **Step 4: Run the gate.** Expected: PASS; the existing `UnitsTests.fs` and every caller of `Units.*` compile unchanged.
- [ ] **Step 5: Commit** `feat(quantities): a catalogue of units with the SI prefixes`.

### Task 10: Unit text and conversion (§8.2)

**Files:**
- Modify: `src/Ranvier.Quantities/Units.fs` (`module UnitInfo`, `module UnitOf`).
- Create: `tests/Ranvier.Science.Tests/UnitTextTests.fs` (after `CatalogueTests.fs`).

**Interfaces:**
- Consumes: `UnitCatalogue` and its internal `TryFind`, `UnitCatalogue.si` (Task 9); `multiply`, `divide`, `power` and their `ArithmeticException` (Task 2).
- Produces:

```fsharp
module UnitInfo =
    val parse : UnitCatalogue -> text: string -> Result<UnitInfo, string>
    val conversionFactor : from: UnitInfo -> target: UnitInfo -> Result<float, string>   // Error across dimensions
    val convert : from: UnitInfo -> target: UnitInfo -> float -> Result<float, string>
module UnitOf =
    val convert : from: UnitOf<'u> -> target: UnitOf<'v> -> float<'u> -> float<'v>     // ArgumentException across dimensions
```

The grammar (§8.2), with the choices in it:

```
unit     := product
product  := factor (sep factor | '/' factor)*     '/' associates left: J/kg/K is J·kg⁻¹·K⁻¹
factor   := atom exponent?
atom     := symbol | '1' | '(' product ')'
exponent := '^' ('-' | '−')? digits               magnitude 0 to 1000, as Formula's '^'
          | superscript-minus? superscript-digits  ⁻ ⁰ ¹ ² ³ ⁴ ⁵ ⁶ ⁷ ⁸ ⁹
sep      := '·' | '*' | '.' | one or more spaces
symbol   := the longest run of characters that are none of: space · * . / ( ) ^ [ ] , - − × ÷,
            ASCII digits, superscript digits, ⁻
```

Spaces around `/`, `^`, parentheses and at either end are ignored. Each catalogue hit becomes a one-factor unit named by the symbol as written (after Task 9's look-alike replacement) with the catalogue entry's dimension and scale; so `UnitInfo.symbol` of the parse of `"km/h"` is `"km/h"`. An error message names the 1-based character position, as `Formula.parse` does, and the symbol or character met; a scale failure of Task 2 is an `Error` with its message. `conversionFactor` is `from.Scale / target.Scale`; its `Error` names both units. `convert` multiplies by the factor.

- [ ] **Step 1: Write the failing tests** (`"UnitText"`; `p = UnitInfo.parse UnitCatalogue.si`):

| Test | Asserts |
|---|---|
| `a quotient of prefixed units` | `p "km/h"`: `Factors = [ "km", 1; "h", -1 ]`, `symbol = "km/h"`, dimension L T⁻¹, `Scale = 1000.0 / 3600.0` |
| `separators and spaces` | `"kg·m/s^2"`, `"kg*m/s^2"`, `"kg.m/s^2"`, `"kg m/s^2"`, `" kg  m / s ^ 2 "` all give `Factors = [ "kg", 1; "m", 1; "s", -2 ]` |
| `division associates left` | `"J/kg/K"`: `Factors = [ "J", 1; "kg", -1; "K", -1 ]`, `symbol = "J/(kg·K)"` |
| `exponents` | `"m^2"` = `"m²"`; `"s^-1"` = `"s^−1"` (U+2212) = `"s⁻¹"`; `"(m/s)^2"` = `"(m/s)²"` = `[ "m", 2; "s", -2 ]`; `"m²·s⁻²"` the same |
| `one as a numerator` | `"1/s"`: `[ "s", -1 ]`; `"1/(m·s)"`: `[ "m", -1; "s", -1 ]` |
| `prefixes and look-alike characters` | `"hPa"` scale 100; `"μm"` and `"µm"` both `Factors = [ "µm", 1 ]`; `"kΩ"` (U+2126) symbol `"kΩ"` (U+03A9) |
| `errors name the position` | `""`, `"m/"`, `"m^"`, `"(m"`, `"2m"` are `Error`; `"m^x"` contains `"3"`; `"m)"` contains `"2"`; `"m/xyz"` contains `"xyz"` and `"3"`; `"kkg"` contains `"kkg"`; `"m^1001"` contains `"1000"`; `"km^200"` contains `"km^200"` |
| `conversion factors` | `conversionFactor` km→m `Ok 1000.0`; m→km `Ok (1.0 / 1000.0)`; `p "km/h"`→`p "m/s"` `Ok ((1000.0 / 3600.0) / 1.0)`; m→s `Error` containing `"m"` and `"s"`; `convert` km m 2.5 `Ok 2500.0`; `convert` (km/h) (m/s) 72.0 `Ok (72.0 * ((1000.0 / 3600.0) / 1.0))` |
| `UnitOf.convert` | `UnitOf.convert Units.kilometre Units.metre 2.5<km> = 2500.0<m>`; `UnitOf.convert Units.hour Units.second 1.5<h> = 5400.0<s>`; `throwsT<ArgumentException>` for `UnitOf.convert Units.metre Units.second 1.0<m>` |

- [ ] **Step 2: Run** `-- --filter "UnitText"`. Expected: the build fails (`UnitInfo.parse` is not defined).
- [ ] **Step 3: Implement** a recursive-descent parser over the grammar, with a private F# exception for its failures, as `Formula.parse` uses `FormulaSyntax`.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): unit text and conversion`.

### Task 11: The model's catalogue, unit literals and conversion in the notation (§8.3, first part)

**Files:**
- Modify: `src/Ranvier.Quantities/Model.fs` (catalogue), `src/Ranvier.Quantities/Formula.fs` (`Expr`, `parse`, `check`, `evaluator`, `measuredEvaluator`).
- Create: `tests/Ranvier.Science.Tests/NotationTests.fs` (after `FormulaEntryTests.fs`).

**Interfaces:**
- Consumes: `UnitInfo.parse`, `UnitInfo.conversionFactor` (Task 10); `UnitCatalogue.si`, `UnitCatalogue.add` (Task 9); `Formula.measuredEvaluator` (Task 7).
- Produces:

```fsharp
type Expr =
    | … // the 0.1.0 cases
    | UnitLiteral of string        // [unit text]: one of that unit
    | Convert of Expr * string     // convert(expr, [unit text])
type Model with
    member Catalogue : UnitCatalogue
module Model =
    val create : name: string -> Model                               // UnitCatalogue.si
    val createWith : catalogue: UnitCatalogue -> name: string -> Model
    val child : parent: Model -> name: string -> Model               // the parent's catalogue
```

Rules, with the choices:
- `[` starts a literal; its text runs to the next `]` and is kept trimmed of surrounding spaces. A number followed by a literal is their product, and a `^` after the literal applies to the literal alone: `2 [m]^2` is `Multiply (Number 2.0, Power (UnitLiteral "m", 2))`. A literal elsewhere stands alone (`[m]^2`); a literal after a name or `)` is a syntax error.
- `convert` takes exactly an expression and a bracketed literal, else a syntax error naming `convert`.
- `check`: a literal's unit is `UnitInfo.parse model.Catalogue text`, its `Error` prefixed with the literal (`"The unit literal [xyz]: …"`); `Convert (e, text)` has the literal's unit, `Error` across dimensions naming both units. `+` and `−` keep needing one unit and do not convert (the slice's D3).
- Evaluation: a literal's value is 1.0; `Convert (e, text)` multiplies `e`'s value by `UnitInfo.conversionFactor`, computed once when the evaluator is built; `measuredEvaluator` multiplies the `Uncertain` by the same float.
- `evaluator` and `measuredEvaluator` run `check` when they are built and raise `ArgumentException` with its message when it is an `Error`: conversion factors and the scale rule of Task 12 come from the same unit computation (§8.3). An unknown identifier still raises `ArgumentException` at creation.
- Write the recursion of `check` and of both evaluators over a private context `{ Find: string -> Entry voption; Catalogue: UnitCatalogue }` built from the model, so Task 14 can run it over a formula entry's recorded names.

- [ ] **Step 1: Write the failing tests** (`"Notation"`; models inside `withGraph`):

| Test | Asserts |
|---|---|
| `unit literals parse` | `"9.81 [m/s^2]"` = `Multiply (Number 9.81, UnitLiteral "m/s^2")`; `"[km]"` = `UnitLiteral "km"`; `"2 [m]^2"` and `"[m]^2"` as above; `"-3 [m]"` = `Negate (Multiply (Number 3.0, UnitLiteral "m"))`; `"[ m / s ]"` = `UnitLiteral "m / s"` |
| `convert parses` | `"convert(x, [m])"` = `Convert (Name "x", "m")`; `"convert(x + y, [km/h])"` = `Convert (Add (Name "x", Name "y"), "km/h")` |
| `malformed literals and conversions` | `"[m"` is `Error` containing `"]"`; `"convert(x, m)"`, `"convert(x)"`, `"convert(x, [m], [s])"`, `"x [m]"`, `"(x) [m]"` are `Error` |
| `a literal has its unit` | `check "9.81 [m/s^2]"` is `sameUnit` with metre per second squared; `check "[furlong]"` is `Error` containing `"furlong"`; with the made-up `widget = { Factors = [ "widget", 1 ]; Dimension = Dimension.length; Scale = 2.5 }`, over `Model.createWith (UnitCatalogue.si |> UnitCatalogue.add "widget" widget false) "w"`, `check "[widget]"` is `Ok` |
| `the model's catalogue` | `(Model.create "m").Catalogue` finds `"m"`; `(Model.createWith c "w").Catalogue` is `c` (`obj.ReferenceEquals`); `(Model.child parent "c").Catalogue` is the parent's |
| `conversion` | `x` = 2 km: `check "convert(x, [m])"` is `sameUnit` with metre, value 2000.0; `"convert(x, [s])"` is `Error` containing `"km"` and `"s"`; `"x + 3 [m]"` is `Error` containing `"convert"`; `"convert(x, [m]) + 3 [m]"` is 2003.0; `"x + 3 [km]"` is 5.0 |
| `a literal is one of its unit` | `"[km]"` evaluates to 1.0; `"9.81 [m/s^2]"` to 9.81 |
| `measured conversion` | `x` = 2 ± 0.1 km: `measuredEvaluator "convert(x, [m])"`: value 2000.0, standard uncertainty within relative 1e-15 of 100.0 |
| `the evaluators raise for a formula that does not check` | `evaluator` and `measuredEvaluator` of `"x + [s]"` raise `ArgumentException`; the existing `an unknown name raises when the evaluator is built` passes unchanged |

- [ ] **Step 2: Run** `-- --filter "Notation"`. Expected: the build fails (`UnitLiteral` is not defined).
- [ ] **Step 3: Implement**, and add the two cases to the doc comment of `Expr` and `Formula`'s module comment.
- [ ] **Step 4: Run the gate.** Expected: PASS, the Monitor's formula tests included (`Format.tryNumber` reads `Number` and `Negate (Number _)` only).
- [ ] **Step 5: Commit** `feat(quantities): unit literals and conversion in the formula notation`.

### Task 12: Functions, π and dimensionless arguments at scale 1 (§8.3, second part)

**Files:**
- Modify: `src/Ranvier.Quantities/Formula.fs` (`arity`, `check`, `pureNumber`, both evaluators, the module comment).
- Test: `tests/Ranvier.Science.Tests/NotationTests.fs`.

**Interfaces:**
- Consumes: Task 11's notation, context and rule that evaluators run `check` at creation; the `Uncertain` static members and `map2` (Task 5); `Units.degree`, `Units.kilometre` (Task 9).
- Produces: the one-argument functions `sin cos tan asin acos atan sinh cosh tanh` and the two-argument `atan2(y, x)` and `hypot(x, y)`; the name `pi`.

Rules:
- `exp ln log10` and the nine new one-argument functions take a dimensionless argument of any scale and give a dimensionless result of scale 1; both evaluators multiply the argument's value (or `Uncertain`) by its unit's scale first, so `sin(30 [deg])` is sin(π/6). This replaces 0.1.0's rule that a dimensionless argument had scale 1 (`pureNumber`). The inverse functions return radians.
- `atan2(y, x)` needs one unit for both and is dimensionless; `hypot(x, y)` needs one unit and has it. The choice for `hypot`: `m · sqrt(1 + (n/m)²)` with `m = max(|x|, |y|)`, `n = min(|x|, |y|)`; 0 when `m = 0`; +∞ when either is infinite; NaN when either is NaN otherwise. Measured derivatives: `atan2` x/(x² + y²) for y and −y/(x² + y²) for x; `hypot` x/h and y/h (NaN at the origin, which its doc comment states).
- `pi` reads π, dimensionless, when `TryFind "pi"` (the model and its ancestors) finds no entry; otherwise the entry.

- [ ] **Step 1: Write the failing tests** (`"Notation"`):

| Test | Asserts |
|---|---|
| `the new functions parse` | `"sin(x)"` = `Call ("sin", [ Name "x" ])`, and each of the nine; `"atan2(y, x)"` and `"hypot(x, y)"` have two arguments; `"sin(1, 2)"` and `"atan2(1)"` are `Error` |
| `dimensionless arguments are taken at scale 1` | `"sin(30 [deg])"` evaluates to `Math.Sin (30.0 * (Math.PI / 180.0))` exactly; `r` = 0.001 in `Units.kilometre / Units.metre`: `check "exp(r)"` is dimensionless and `"exp(r)"` evaluates to `Math.Exp (0.001 * 1000.0)`, `"ln(r)"` to `Math.Log (0.001 * 1000.0)`; `x` in m: `check "exp(x)"` and `"sin(x)"` are `Error` |
| `the inverse functions return radians` | `"asin(1)"` evaluates to `Math.Asin 1.0`, dimensionless at scale 1; `"convert(asin(1), [deg])"` to `Math.Asin 1.0 * (1.0 / (Math.PI / 180.0))` |
| `atan2 and hypot` | `y` = 3 m, `x` = 4 m, `t` = 1 s: `"atan2(y, x)"` is `Math.Atan2 (3.0, 4.0)`, dimensionless; `"hypot(x, y)"` is 5.0 exactly (4·sqrt(1 + 0.75²) = 4·1.25), unit m; `"atan2(y, t)"` and `"hypot(x, 3 [km])"` are `Error` |
| `hypot keeps large and zero arguments` | `"hypot(1e200 [m], 1e200 [m])"` is `1e200 * sqrt 2.0`; `"hypot(0 [m], 0 [m])"` is 0.0 |
| `pi` | `"2 * pi"` is `2.0 * Math.PI`; a model declaring `pi` = 3.0 reads 3.0, and so does its child |
| `0.1.0's pure-number rule is replaced` | `x` = 2 km, `y` = 500 m: `check "exp(x / y)"` is dimensionless and `"exp(x / y)"` evaluates to `Math.Exp ((2.0 / 500.0) * 1000.0)`; `"exp(mean / se)"` of `FormulaTests.fs` keeps its value |
| `measured trigonometry` | `a` = 30 ± 1 deg (`inputWithUncertainty`): `measuredEvaluator "sin(a)"`: value `Math.Sin (30.0 * s)`, its one contribution within relative 1e-14 of `Math.Cos (30.0 * s) * s` with `s = Math.PI / 180.0` |

- [ ] **Step 2: Run** `-- --filter "Notation"`. Expected: FAIL (`"sin"` is not a function of the notation).
- [ ] **Step 3: Implement**, and update `Formula`'s module comment with the functions, `pi` and the scale rule.
- [ ] **Step 4: Run the gate.** Expected: PASS; `FormulaTests.fs`'s `transcendental functions need a pure number` still passes (`exp(mean)` with mean in mK is still an error).
- [ ] **Step 5: Commit** `feat(quantities): trigonometric functions, pi and dimensionless arguments at scale 1`.

### Task 13: Physical constants (§8.6, first part)

**Files:**
- Create: `src/Ranvier.Quantities/Constants.fs` (after `Quantity.fs`), `tests/Ranvier.Science.Tests/ConstantsTests.fs` (after `NotationTests.fs`).

**Interfaces:**
- Consumes: `Quantity.constant`, `measuredConstant`, `deriveMeasured`, `Entry.Measured`, `Entry.Uncertainty` (Task 6); `Quantity.formula` (Task 7); the `Units` values and measures (Task 9); `Uncertain.correlation`, `standardUncertainty` (Task 5).
- Produces:

```fsharp
type PhysicalConstants =
    { SpeedOfLight: Quantity<m/s>; Planck: Quantity<J s>; ReducedPlanck: Quantity<J s>
      ElementaryCharge: Quantity<C>; Boltzmann: Quantity<J/K>; Avogadro: Quantity<1/mol>
      Gravitation: Quantity<m^3/(kg s^2)>; VacuumPermittivity: Quantity<F/m>; VacuumPermeability: Quantity<N/A^2>
      ElectronMass: Quantity<kg>; ProtonMass: Quantity<kg>; NeutronMass: Quantity<kg>; AtomicMassConstant: Quantity<kg>
      FineStructure: Quantity<1>; MolarGas: Quantity<J/(mol K)>; StefanBoltzmann: Quantity<W/(m^2 K^4)>
      BohrRadius: Quantity<m>; StandardGravity: Quantity<m/s^2>; AstronomicalUnit: Quantity<m> }
module Constants =
    /// Raises ArgumentException as Quantity.input does, for an identifier the model already declares.
    val declare : Model -> PhysicalConstants
```

Identifiers, in the record's order: `c h hbar e k_B N_A G epsilon_0 mu_0 m_e m_p m_n m_u alpha R sigma a_0 g_n au`. Each entry's `Name` is NIST's name of the quantity and its `Source` names its origin (CODATA 2022 with the NIST page; "computed from …" for the derived ones; the 3rd CGPM, 1901, for g_n; IAU 2012 Resolution B2 for au). Correlations (§8.6):
- c, h, e, k_B, N_A: `Quantity.constant`, exact, NIST's values. ħ = h/(2π), R = N_A·k_B, σ = 2π⁵k_B⁴/(15h³c²): `Quantity.constant` with the value computed in F# from the exact ones.
- α, G, m_e, m_p, m_n, m_u, a_0: `Quantity.measuredConstant` with NIST's value and standard uncertainty, each its own source. `declare`'s doc comment says that CODATA publishes correlations among several of them and that 0.2.0 does not keep them.
- μ0 = 2αh/(e²c) and ε0 = 1/(μ0c²): `Quantity.deriveMeasured` over α's and μ0's `Measured`, so both share α's source.
- g_n: `constant`, NIST's "standard acceleration of gravity" (exact); au: `constant` at `Units.astronomicalUnit`'s scale.

Fetch the NIST ASCII table first; write the page, the access date and each pasted value and uncertainty (as printed) at the top of `ConstantsTests.fs`; take `Constants.fs`'s values from the same fetch.

- [ ] **Step 1: Write the failing tests** (`"Constants"`, inside `withGraph`; `pc = Constants.declare model`):

| Test | Asserts |
|---|---|
| `every constant is declared` | for each identifier, `model.TryFind id` finds an entry with a non-empty `Source`, the same entry as the record's field (`Entry.Id`), and `sameUnit` with the field's unit |
| `the defining constants are exact` | c, h, e, k_B, N_A, g_n: `Read ()` equals the NIST literal; `Uncertainty () = 0.0`; `Kind = Constant` |
| `the measured constants` | α, G, m_e, m_p, m_n, m_u, a_0: `Read ()` equals the NIST value; `Uncertainty ()` equals the NIST standard uncertainty |
| `constants computed from exact ones` | ħ, R, σ: `Read ()` within half a unit of the last digit NIST prints; `Uncertainty () = 0.0` |
| `mu_0 and epsilon_0 come from alpha` | values within half a unit of NIST's last printed digit; uncertainties within half a unit of the last digit of NIST's printed uncertainty; `correlation` of α and μ0 within 1e-12 of 1, of μ0 and ε0 within 1e-12 of −1; μ0's relative uncertainty within relative 1e-12 of α's |
| `epsilon_0 times mu_0 is 1/c² with no propagated uncertainty` | `p = ε0.Measured () * μ0.Measured ()`: `value p * c * c` within 1e-15 of 1; `standardUncertainty p <= 1e-6 * value p * ur` with `ur` α's relative standard uncertainty |
| `units agree with the constants` | `Units.electronvolt.Info.Scale = pc.ElementaryCharge.Entry.Read ()`; `Units.dalton.Info.Scale = pc.AtomicMassConstant.Entry.Read ()`; `pc.AstronomicalUnit.Entry.Read () = Units.astronomicalUnit.Info.Scale` |
| `independent measured constants are uncorrelated` | `correlation` of G and m_e, and of m_p and m_n, is 0.0 |
| `declaring twice in one model raises` | a second `Constants.declare model` raises `ArgumentException` |
| `a formula over the constants` | `Quantity.formula model "rest" "Electron rest energy" "m_e * c^2"`: `sameUnit` with `Units.joule.Info`; `Read ()` equals m_e·c·c as the evaluator computes it (`m_e * (c * c)`); `Uncertainty ()` within relative 1e-14 of `c * c * u(m_e)` |

- [ ] **Step 2: Run** `-- --filter "Constants"`. Expected: the build fails (`Constants` is not defined).
- [ ] **Step 3: Implement** `Constants.fs`.
- [ ] **Step 4: Run the gate.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): the CODATA 2022 constants with their uncertainties`.

### Task 14: Monte Carlo propagation (§8.6, second part)

**Files:**
- Create: `src/Ranvier.Quantities/Expr.fs` (before `Model.fs`): the `Expr` union moved out of `Formula.fs`, unchanged, same namespace.
- Modify: `src/Ranvier.Quantities/Model.fs` (an internal record per formula entry), `src/Ranvier.Quantities/Quantity.fs` (`formula` records it), `src/Ranvier.Quantities/Formula.fs` (`monteCarlo`), `src/Ranvier.Quantities/Ranvier.Quantities.fsproj`.
- Create: `tests/reference/normal_quantile.py`, `tests/Ranvier.Science.Tests/MonteCarloTests.fs` (after `ConstantsTests.fs`).

**Interfaces:**
- Consumes: Task 11's context and `check`; `Entry.Measured` (Task 6); `Quantity.formula` (Task 7); Task 8's cleanup; `Uncertain.value`, `contributions` (Task 5); `Generator.create`, `Generator.normal`, `Moments.add`, `Moments.mean`, `Moments.sampleStandardDeviation` (Task 3).
- Produces:

```fsharp
type MonteCarloResult = { Mean: float; StandardDeviation: float; Lower: float; Upper: float; Draws: int }
module Formula =
    val monteCarlo : draws: int -> seed: int -> coverage: float -> Model -> Expr -> (unit -> MonteCarloResult)
```

Behaviour (§8.6) and choices:
- At creation, `check` runs (`ArgumentException` with its message on `Error`, and for an unknown identifier); `draws` must be at least 2 and `coverage` strictly between 0 and 1, else `ArgumentOutOfRangeException`. Names resolve once, at creation.
- The expression's formula entries are inlined down to leaves that are not formula entries. Inlining uses each formula entry's names as they resolved at its declaration: `Quantity.formula` records, internal to the assembly, `{ Expr; Names; Catalogue }` with `Names` mapping each identifier of its text to the entry found then and, for a formula entry, that entry's own record; `Model` keeps these records, finds them as `TryFind` finds entries (own, then ancestors), and Task 8's cleanup removes a record with its entry. A leaf named twice is one leaf.
- Each read reads every leaf's `Measured ()` (tracked). With any NaN leaf value or contribution, every float field is NaN and `Draws = draws`. Otherwise each draw takes one standard normal deviate zᵢ per source reached by the leaves, in ascending id order, from `Generator.create seed` threaded through the draws; sets each leaf to its value plus Σᵢ cᵢzᵢ over its contributions; and evaluates the inlined expression with the evaluator's arithmetic, conversion factors and scale rule.
- `Mean` and `StandardDeviation` (divisor n − 1) come from `Moments` over the draws; `Lower` and `Upper` are the type-7 quantiles of the sorted draws at (1 − coverage)/2 and (1 + coverage)/2, the probabilistically symmetric interval. The doc comment states the cost, O(draws · (sources + Σ leaf contributions + expression size)) plus a sort of the draws; that the reader creates no node and evaluates inside the node that reads it; that its record publishes on every recomputation; and the resolve-once rule of Task 8.

- [ ] **Step 1: Write the reference script** `tests/reference/normal_quantile.py`: with `statistics.NormalDist`, print `repr(NormalDist().inv_cdf(0.975))`, and in a header comment the command `python -I tests/reference/normal_quantile.py`. Run it and paste the printed literal into the test file beside that command.
- [ ] **Step 2: Write the failing tests** (`"MonteCarlo"`, inside `withGraph`; N = 20,000 draws, seed 1, coverage 0.95 unless stated; dimensionless inputs through `inputWithUncertainty`):

| Test | Asserts |
|---|---|
| `a linear formula meets first-order propagation` | `a` = 1 ± 0.1, `b` = 2 ± 0.2, `"a + b"`, σ = sqrt 0.05 (closed form): `Draws = N`; `abs (Mean - 3.0) <= 4.0 * σ / sqrt N`; `abs (StandardDeviation - σ) <= 4.0 * σ / sqrt (2.0 * (N - 1))`; `Lower` and `Upper` within 0.1σ of 3 ∓ zσ, z from the reference script |
| `leaves that share a source move together` | `x` = 1 ± 0.1; `y1 = deriveMeasured (2.0 * x)`, `y2 = deriveMeasured (-2.0 * x)`; `"y1 + y2"`: `Mean = 0.0` and `StandardDeviation = 0.0` exactly; independent `p`, `q` at 0 ± 0.2: `"p + q"` has `StandardDeviation > 0.2` |
| `formula entries are inlined` | `a` = 1 ± 0.5, `f = Quantity.formula … "a^2"`, `"f"`: `abs (Mean - 1.25) <= 4.0 * sqrt 1.125 / sqrt N` (closed form: E[(1 + 0.5Z)²] = 1.25, Var = 4·0.25 + 2·0.0625 = 1.125); `Mean - 1.0 > 0.1` (a leaf at first order would centre on 1) |
| `inlining uses the names a formula entry resolved` | parent model with `a` = 1 ± 0.1 and formula `f` = `"a * 2"`; a child declaring its own `a` = 100 ± 0.1: `monteCarlo` over the child's `"f"` has `abs (Mean - 2.0) <= 0.01` |
| `a fixed seed gives fixed results` | two readers with seed 1 give equal records; seeds 1 and 2 give different `Mean` |
| `a NaN contribution gives NaN fields` | `d = derive …` (NaN uncertainty), `"d * 2"`: `Mean`, `StandardDeviation`, `Lower`, `Upper` NaN; `Draws = N` |
| `a reader is tracked` | a memo over the reader counts runs: `a.Set` and `a.SetUncertainty` each add one run |
| `units and conversions` | `a` = 2 ± 0.1 km, `"convert(a, [m])"`: `abs (Mean - 2000.0) <= 4.0 * 100.0 / sqrt N` |
| `arguments` | `ArgumentOutOfRangeException` for draws 1 and 0 and for coverage 0, 1, nan; `ArgumentException` for `"nope"` and `"a + [s]"` |
| `a reader keeps the entries it resolved` | `a` = 1 ± 0.1 under a root; a reader of `"a"`; the root disposed and `"a"` declared again at 10 ± 0.1: the reader's `Mean` stays within 0.01 of 1 |

- [ ] **Step 3: Run** `-- --filter "MonteCarlo"`. Expected: the build fails (`Formula.monteCarlo` is not defined).
- [ ] **Step 4: Implement**: the `Expr.fs` move, the records, `monteCarlo`.
- [ ] **Step 5: Run the gate.** Expected: PASS.
- [ ] **Step 6: Commit** `feat(quantities): Monte Carlo propagation of a formula's distributions`.

### Task 15: The dispositions of the findings, and the gate's figures

**Files:**
- Modify: `docs/FINDINGS.md` §3 (lines 337-384).
- Create: `docs/measurements/gate.md`.

**Interfaces:**
- Consumes: Tasks 1 to 14 (their tests by name).
- Produces: §3 of the findings with a disposition on every item and every bullet of item 7 (§2 goal 4); a dated record of the gate's figures (§11, "measured at each plan's end").

- [ ] **Step 1: Mark each item of §3.** After each numbered item (and each bullet of item 7) add one line, `**0.2.0:** resolved (…)`, `**0.2.0:** not applicable (…)` or `**0.2.0:** deferred (…)`, keeping the finding's text as written:
  - 1 resolved: I1, `UnitInfo.sameUnit`, `equivalent` removed; test `sameUnit at the relative bound, in both orders` (`UnitsTests.fs`).
  - 2 resolved: I4, `SequenceSettings`, `ConfidenceSequence.interval`; coverage per setting in the module's remarks; the two coverage tests (`ConfidenceSequenceTests.fs`).
  - 3 resolved: I3, `FloatPolicy.standard` with `RefoldFloor = 64` and its measured bound; test `between re-folds under the standard floor …` (`AggregateTests.fs`).
  - 4 resolved: I5, `Quantity.formula` and `Quantity.view`; `FormulaEntryTests.fs`.
  - 5 resolved: I6, entries scoped to their owner; `LifetimeTests.fs`.
  - 6 not applicable: the AstroShowcase pages left this repository on 2026-11-20.
  - 7, the page-record fields (`Internals`, `Schema`, `Session.Graph`): deferred, the delivery layer is a non-goal of 0.2.0.
  - 7, the view's `each` over a projection: deferred, the same reason.
  - 7, `Ranvier.D5.View`'s listeners: deferred, the same reason.
  - 7, `Run` and `Flow.Reset ()`: deferred; `Run.ode` (§8.7) adds no reset.
  - 7, unit scales unchecked for overflow: resolved, I2; test `a scale that overflows or vanishes raises` (`UnitsTests.fs`).
  - 7, the tracked Attitude page's chart frame: not applicable, the AstroShowcase pages left on 2026-11-20.
- [ ] **Step 2: Record the gate.** Time each of the four gate commands once from a warm state, and write `docs/measurements/gate.md`: a table with one row per plan end (date, plan, build, .NET suite with its test count, Fable compile, mocha with its test count), *measured here*, with the commands and the machine (`[System.Environment]::ProcessorCount`, OS); add the baseline row from the Global Constraints, labelled *measured here* by the coordinator on 2026-11-20.
- [ ] **Step 3: Run the gate.** Expected: PASS.
- [ ] **Step 4: Commit** `docs: the dispositions of the findings' section 3, and the gate's figures at the end of Plan T1`.
