# Ranvier.Science toolkit, Plan T3: statistics, docs and release — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Ranvier.Statistics` gains distributions, descriptive statistics, intervals, hypothesis tests, a sequential test, multiple-testing adjustments, regression, correlation, curve fitting, the bootstrap and density estimation (spec §7); the repository gains `docs/GUIDE.md`, `docs/measurements/accuracy.md` and a 0.2.0 README (spec §9); the three packages move to 0.2.0 and pack (spec goal 6).

**Architecture:** Every part this plan adds is a kernel: a plain function over numbers, arrays and the immutable accumulators of `Ranvier.Numerics`, which reads and creates no node (spec §3.3). The kernels build on Plan T2's special functions, linear algebra, comoments, least squares and variates and on Plan T1's sequence settings and moments additions, by the spec's names. Reference values come from one self-contained Python script, Plan T2's `tests/reference/special.py`, to which this plan adds sections; it computes in 60-digit `decimal` or exact `fractions`.

**Tech Stack:** F# on .NET 10 (`global.json`: SDK 10.0.100, `latestFeature`); core `Ranvier` 0.2.1; Expecto 11.0.0-alpha8; Fickl 0.10.1 with Fable.Mocha 2.17.0 and mocha 12; Python 3.14.4, standard library only.

**Spec:** `C:\wk\ranvier-release\Ranvier.Science\docs\superpowers\specs\2026-11-20-toolkit-design.md`. Section numbers below refer to it. Read §3 (conventions), §5 (accuracy and references) and the subsection of §7 or §9 a task names.

**Order.** Plans T1 (interfaces and quantities) and T2 (numerics kernels) run before this plan on the branch `toolkit` of `C:\wk\ranvier-release\Ranvier.Science`. This plan consumes their parts by the spec's names and signatures and redefines none of them. Tasks 1 to 12 are spec §7, Task 13 the accuracy record, Task 14 the guide (the last implementation task), Task 15 the release. The coordinator's steps (review, pull request, CI, merge, publish) follow Task 15 and are not implementer tasks.

## Global Constraints

- Work only in `C:\wk\ranvier-release\Ranvier.Science`, branch `toolkit`. Commit on that branch only. Implementers never push, merge, tag, publish or delete a remote branch.
- Library code compiles with Fable: no reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`, no `System.Numerics.Complex`. `Math.*` functions are fine.
- `FSharpCoreImplicitPackageVersion` 8.0.100, `Fable.Package.SDK` 1.4.1 with `FablePackageType=library`, MIT license (spec §3.1). The version is set by hand in `src/Directory.Build.props`; only the three `src/` libraries are packable.
- A source file is added to its library's `.fsproj` `Compile` list. A test file is added to `tests/Ranvier.Science.Tests/TestFiles.props` (never to one project alone) and its root to `tests/Ranvier.Science.Tests.Fable/Main.fs` as `testList "<File>.fs" [ Ranvier.Science.Tests.<Module>.tests ]`, in compile order.
- Shared test files use only what the Expecto shim (`tests/Ranvier.Science.Tests.Fable/Expecto.fs`) provides: `testList`, `test`, `testCase`, `testCaseAsync`, and `Expect.equal`, `notEqual`, `isTrue`, `isFalse`, `isLessThan`, `isLessThanOrEqual`, `isGreaterThan`, `throws`, `throwsT`, `sequenceEqual`, `all`, `exists`, `stringContains`, `stringStarts`, `isEmpty`, `isNonEmpty`. There is no `isGreaterThanOrEqual` and no `floatClose`: write `Expect.isLessThanOrEqual (rel a b) bound`.
- Error conventions (spec §3.2): an argument outside the domain raises `ArgumentOutOfRangeException` or `ArgumentException`; a NaN argument where the domain is otherwise satisfied returns NaN (or NaN fields) and raises nothing; a data-dependent failure of a direct method is `Result<'T, string>` naming the condition; an iterative method reports `Converged`. A function never returns a plausible number where it failed (spec §2, anti-goals).
- A kernel never reads or creates a node. No doc comment claims a cut-off from structural equality (spec §3.3).
- Each approximating kernel states its bound in its doc comment in the form "relative error at most E over D" (or "absolute error at most E over D" near zero), and a test pins it (spec §5.1). Targets: distribution `cdf` and `survival` relative 1e-12 where the value is at least 1e-300; `quantile` by round trip, the forward function at the result within 1e-12 relative of the target probability.
- Every reference value in a test carries its source beside it (spec §5.2): a closed form or identity written in the test; the script `tests/reference/special.py` (Plan T2's; this plan adds sections, named in each task), run as `python -I tests/reference/special.py <section>` with the standard library alone (never `pip install`), the command written beside the pasted literals; or a published table cited by name and edition. A value marked ⚠️ in this plan is used only after the implementer confirms it against its named source; otherwise the test cites the script alone.
- Every figure in a doc comment, record or commit carries a label: *measured here* (with the command), *from documentation* (with the file or page), or *general knowledge*.
- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`: the contract, the raise conditions, the cost where it is not O(1), the accuracy bound; no narration. British spelling in prose.
- Kernels that use `exp`, `log` or trigonometric functions meet their bound on each target separately; this plan claims no exact cross-target equality (spec §5.3).
- F# inner loop: SageFs when available, otherwise `rfs check --fail-on-error` (plain `rfs check` exits 0 with compile errors). A task is complete only when `dotnet build -c Release` shows 0 warnings and 0 errors and both suites pass unfiltered.
- Shell: write a script to a file and run the file; use absolute paths; run commands plainly. No window is opened and no app is launched.
- Commit messages are conventional (`feat(statistics): …`, `test: …`, `docs: …`, `build: …`) and end with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Builder's choices made here (spec §12)

Each is stated again in the task that owns it.

1. File layout: one file per §7 subsection (`Continuous.fs`, `Discrete.fs`, `Describe.fs`, `Intervals.fs`, `HypothesisTest.fs`, `Sequential.fs`, `Regression.fs`, `Resampling.fs`) plus the internal argument checks `Check` in `Check.fs` (Tasks 1 to 12), which compiles before `ConfidenceSequence.fs` so that `ConfidenceSequence.fs` calls `Check` and keeps no private copy; `log1p` and `expm1` are Plan T2's `Special.log1p` and `Special.expm1` (spec §6.1).
2. One reference script with sections, since `python -I` puts no script folder on `sys.path` and so a script cannot import a sibling: Plan T2's `tests/reference/special.py`, to which Tasks 1 to 11 add sections (`normal`, `lognormal`, `dist-gamma`, `dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`, `discrete`, `intervals`, `parametric`, `counts`, `ranks`, `regression`, `correlation`, named so that none collides with T2's `gamma`, `erf`, `incgamma`, `incbeta`). The sections call the script's `ln_gamma`, `gamma_p`, `gamma_q` and `beta_inc`, which Plan T2 writes as named functions; nothing re-implements ln Γ, Bernoulli numbers or the incomplete functions (Task 1).
3. Upper-tail quantiles (p > 0.5) solve `survival q = 1 − p` and are tested on `survival`, so upper critical values keep full relative precision (Tasks 1, 2).
4. Moments: +∞ where the defining integral diverges to +∞, NaN where the moment is undefined (Task 2).
5. `describe` prints parameters with F#'s `string`; text for magnitudes outside [1e-5, 1e15) may differ between targets (Task 1).
6. `Discrete.negativeBinomial r p` takes a real `r > 0` and counts failures before the r-th success; `Discrete.support` uses `Int32.MaxValue` for an unbounded upper end, and `quantile` saturates there; a NaN `p` to `Discrete.quantile` raises, since an `int` has no NaN (Task 3).
7. `Describe.binEdges` follows NumPy's `histogram_bin_edges` (Sturges, Scott, Freedman–Diaconis, edges by NumPy's `linspace`), `trimmedMean` cuts ⌊n·proportion⌋ from each end as R and SciPy do (Task 4).
8. Proportion intervals: `trials = 0` gives NaN bounds; Agresti–Coull is clipped to [0, 1]; Jeffreys takes Brown, Cai and DasGupta's modification at 0 and n (Task 5).
9. `HypothesisTest.chiSquaredGoodnessOfFit` rescales `expected` to the observed total, so it may hold counts or proportions (Task 7).
10. `HypothesisTest.fisherExact`'s `Statistic` is the sample odds ratio ad/(bc) (Task 7).
11. Rank tests follow R's `wilcox.test(exact = FALSE, correct = TRUE)`; Kolmogorov–Smirnov p-values follow R's `ks.test(exact = FALSE)`, the Kolmogorov distribution at √n·D (Task 8).
12. `Adjust` follows R's `p.adjust`: NaN entries stay NaN and the family size counts the others (Task 9).
13. `Regression.linear`'s covariance is σ²(RᵀR)⁻¹ = σ² R⁻¹R⁻ᵀ from Plan T2's `QrDecomposition.r` (the coordinator's ruling: a Cholesky of XᵀX squares the design's condition number); a constant column is one whose entries all equal its first, non-zero, entry; adjusted R² follows R's `summary.lm`; a non-finite input is an `Error` (Task 10).
14. `Correlation.pearsonTest` and `spearmanTest` report the t statistic with n − 2 degrees of freedom; Kendall's tau-b is Knight's O(n log n) algorithm and `kendallTest` is R's tie-corrected normal approximation without continuity correction (Task 11).
15. `Density.silvermanBandwidth` is R's `bw.nrd0` (with its fallbacks) and `scottBandwidth` R's `bw.nrd` (Task 12).
16. The guide's snippets are checked against `GuideTests.fs` by a committed standard-library script, `tests/guide-snippets.py` (Task 14).

## Review Focus

1. **Far-tail arguments** (a normal cdf at −37, a Student t survival at 1000, a Cauchy cdf at −1e100, a quantile at 1e-100 or 1 − 1e-10). Expected: the small probability to relative 1e-12, never 0 or a cancelled 1 − x. Tests: Task 1 `normal cdf and survival against the reference`, `cauchy tails`; Task 2 `Student t against the reference`; quantile round trips in Tasks 1 and 2.
2. **Empty, one-item and NaN-holding samples** reaching intervals, tests and descriptive statistics. Expected: NaN bounds or fields, nothing raised (except `Describe.ranks`, which the spec makes raise). Tests: Task 4 `summary of an empty array and of one item`, `NaN in the data`; Task 5 `small samples give NaN bounds`; Task 6 `fewer than two observations give NaN fields`; Task 8 `NaN or empty samples give NaN fields`.
3. **Ties and zeros in rank data.** Expected: tie-corrected variances, zero differences dropped, an all-tied sample gives a NaN p-value rather than 0 or 1. Tests: Task 4 `ranks with ties`; Task 8 `Wilcoxon on the sleep differences`, `all observations tied give a NaN p-value`; Task 11 `Kendall tau-b matches a direct count`.
4. **Degenerate regression and test data** (a duplicated design column, a constant x, two constant samples). Expected: an `Error` or NaN, never a large finite coefficient. Tests: Task 10 `a rank-deficient design is an Error`, `constant x gives a NaN slope`; Task 6 `zero spread`.
5. **Boundary probabilities and counts** (p-values of exactly 0 and 1 into `Adjust`, p = 0 and 1 into `quantile`, 0 or n successes into a proportion interval, a zero expected count). Expected: the documented bound or a raise, never NaN by accident. Tests: Task 9 `p-values of 0 and 1`; Tasks 1 and 3 `quantile at 0 and 1 is the support's bounds`; Task 5 `proportion intervals at 0 and n successes`; Task 7 `count arguments out of range raise`.

---

## File Structure

```
src/Ranvier.Statistics/
  NormalQuantile.fs        doc comment points to Continuous.quantile (Task 1)
  Check.fs                 internal: the argument checks `Check` (Task 1); before ConfidenceSequence.fs
  ConfidenceSequence.fs    Plan T1's (SequenceSettings, settings, tunedFor, standardBurnIn, interval); edited only in Task 1,
                           where its private `Arguments` gives way to `Check`
  Continuous.fs            Continuous (Tasks 1, 2)
  Discrete.fs              Discrete (Task 3)
  Describe.fs              Describe, Summary, BinRule (Task 4)
  Intervals.fs             MeanInterval, ProportionInterval, VarianceInterval, DifferenceInterval (Task 5)
  HypothesisTest.fs        Alternative, TestResult, HypothesisTest, internal PValue (Tasks 6, 7, 8)
  Sequential.fs            SequentialTest, Adjust (Task 9)
  Regression.fs            Regression, Correlation and their records (Tasks 10, 11)
  Resampling.fs            Bootstrap, Density (Task 12)
tests/reference/special.py        Plan T2's reference script; one section added per test area (Task 1 on)
tests/guide-snippets.py           guide snippet check (Task 14)
tests/Ranvier.Science.Tests/      ContinuousTests.fs DiscreteTests.fs DescribeTests.fs IntervalTests.fs
                                  HypothesisTests.fs SequentialTestTests.fs AdjustTests.fs RegressionTests.fs
                                  CorrelationTests.fs ResamplingTests.fs GuideTests.fs
docs/GUIDE.md  docs/measurements/accuracy.md  README.md                  (Tasks 13 to 15)
src/Directory.Build.props  src/*/Ranvier.*.fsproj                        (Task 15)
```

`Compile` order of `Ranvier.Statistics.fsproj` at the end of the plan: `NormalQuantile.fs`, `Check.fs`, `ConfidenceSequence.fs`, `Continuous.fs`, `Discrete.fs`, `Describe.fs`, `Intervals.fs`, `HypothesisTest.fs`, `Sequential.fs`, `Regression.fs`, `Resampling.fs`. It references `Ranvier.Numerics` only; nothing here needs `Ranvier.Quantities`. New test files go into `TestFiles.props` and the Fable `Main.fs` immediately before `CrossTargetTests.fs` (Plan T2's rule), in task order, each after the previous T3 file, and `GuideTests.fs` last before `CrossTargetTests.fs`.

Commands, from the repository root:

- **Build:** `dotnet build -c Release`
- **.NET suite:** `dotnet run -c Release --project tests/Ranvier.Science.Tests` (one list: `-- --filter "<list name>"`, red/green loop only)
- **JavaScript suite:** `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`
- **The gate** is the build with 0 warnings and 0 errors and both suites unfiltered, all passing.

---

### Task 1: Continuous distributions with closed forms, and the normal

**Files:**
- Create: `src/Ranvier.Statistics/Check.fs`, `src/Ranvier.Statistics/Continuous.fs`, `tests/Ranvier.Science.Tests/ContinuousTests.fs`
- Modify: `tests/reference/special.py` (sections `normal`, `lognormal`); `src/Ranvier.Statistics/ConfidenceSequence.fs` (its private `Arguments` module removed, its calls become `Check.alpha`, `Check.finitePositive`, `Check.notNegative`); `src/Ranvier.Statistics/Ranvier.Statistics.fsproj` (`Check.fs` immediately before `ConfidenceSequence.fs`, `Continuous.fs` after it); `src/Ranvier.Statistics/NormalQuantile.fs:22` (doc comment); `tests/Ranvier.Science.Tests/TestFiles.props`; `tests/Ranvier.Science.Tests.Fable/Main.fs`

**Interfaces:**
- Consumes (Plan T2, `Ranvier.Numerics`): `Special.erfc : float -> float`; `Special.log1p : float -> float` and `Special.expm1 : float -> float` (both public, spec §6.1; T2 Task 1); `Generator.exponential : rate: float -> Generator -> struct (float * Generator)`; `Generator.uniform` and `Generator.normal` (0.1.0, `Generator.fs:55,62`); in tests, `Integrate.adaptive : absolute: float -> relative: float -> f: (float -> float) -> a: float -> b: float -> Integral` with `Integral = { Value: float; ErrorEstimate: float; Evaluations: int; Converged: bool }`.
- Produces:

```fsharp
namespace Ranvier.Statistics

module internal Check =         // each raises ArgumentOutOfRangeException naming the parameter
    val alpha : float -> unit                        // unless 0 < alpha < 1
    val finite : name: string -> float -> unit       // unless finite
    val finitePositive : name: string -> float -> unit
    val notNegative : name: string -> float -> unit  // unless ≥ 0 (NaN raises)
    val probability : name: string -> float -> unit  // unless 0 ≤ p ≤ 1 (NaN raises)

type Continuous   // a union with private cases; structural equality

module Continuous =
    val normal : mean: float -> sd: float -> Continuous
    val logNormal : mu: float -> sigma: float -> Continuous
    val uniform : lower: float -> upper: float -> Continuous
    val exponential : rate: float -> Continuous
    val cauchy : location: float -> scale: float -> Continuous
    val weibull : shape: float -> scale: float -> Continuous
    val logistic : location: float -> scale: float -> Continuous
    val density : Continuous -> float -> float
    val logDensity : Continuous -> float -> float
    val cdf : Continuous -> float -> float
    val survival : Continuous -> float -> float
    val quantile : Continuous -> p: float -> float
    val mean : Continuous -> float
    val variance : Continuous -> float
    val standardDeviation : Continuous -> float
    val support : Continuous -> struct (float * float)
    val sample : Continuous -> Generator -> struct (float * Generator)
    val samples : Continuous -> count: int -> Generator -> struct (float[] * Generator)
    val describe : Continuous -> string
```

- Produces (script): sections of Plan T2's `tests/reference/special.py <section>`, with the new helpers `normal_cdf(z)`, `normal_sf(z)`, `invert(f, target, lo, hi)` and `emit(name, rows)`; later tasks add sections and helpers. Consumes from the same script (Plan T2, Tasks 1, 3 and 4): `pi()`, `ln_gamma(x)`, `gamma_p(a, x)`, `gamma_q(a, x)` and `beta_inc(a, b, x)`, which T2 writes as named functions behind its `incgamma` and `incbeta` sections.

- [ ] **Step 1: Add the sections `normal` and `lognormal` to `tests/reference/special.py`.** The script already sets `decimal.getcontext().prec = 60` and converts each input with `Decimal(x)` or `Fraction(x)` from the double the test passes (Plan T2's conventions), so a reference is exact at that double; this task writes no second copy of π, ln Γ, the Bernoulli numbers or P and Q. It adds: `normal_sf(z) = gamma_q(1/2, z²/2) / 2` for z ≥ 0 and `1 − normal_sf(−z)` below, `normal_cdf(z) = 1 − normal_sf(z)` through the same split; `invert` by 200 bisection steps on an increasing function. Before printing, the new sections assert to 1e-50: `normal_cdf(0) = 1/2`; `gamma_p(1, x) = 1 − e^−x` at x = 0.3 and 7; `exp(ln_gamma(10)) = 362880`; `ln_gamma(0.5) = ln √π`. `emit` prints `let private <name> = [| struct (x, v); ... |]` with `repr(float(v))` (the nearest double, shortest form), and the script prints its command as the first line, as an F# comment. Sections of this task:
  - `normal`: N(0, 1) `cdf` and `survival` at x ∈ {−37, −30, −10, −5, −1.96, −1, −0.5, 0, 0.3, 1, 1.96, 3, 5, 8, 10, 30, 37}; N(3, 2) `cdf` at {−5, 1, 3, 7.5}; N(0, 1) quantile at p ∈ {1e-300, 1e-100, 1e-10, 0.025, 0.3, 0.975, 1 − 1e-10}, bisecting the survival against `1 − Decimal(p)` for p > 0.5.
  - `lognormal`: LN(0.5, 0.8) `cdf` and `survival` at x ∈ {1e-10, 0.1, 1, 2, 10, 1000}.

  Run `python -I tests/reference/special.py normal` and `... lognormal`. Expected: the asserts pass and two blocks of literals print.

- [ ] **Step 2: Write the failing tests** in `ContinuousTests.fs`, list `"Continuous"`. Helpers: `rel a b = abs (a - b) / abs b`; `lowerGrid = [ 1e-100; 1e-10; 0.01; 0.3; 0.5 ]`; `upperGrid = [ 0.6; 0.975; 0.999; 1.0 - 1e-10 ]`; `roundTrip d p` asserts `rel (cdf d q) p <= 1e-12` for p ≤ 0.5 and `rel (survival d q) (1.0 - p) <= 1e-12` for p > 0.5, with `q = quantile d p`; `ksDistance (cdf: float -> float) (xs: float[])` is max over the sorted sample of max(i/n − F(x₍ᵢ₎), F(x₍ᵢ₎) − (i − 1)/n). Paste the script's tables above the list with their command.
  - `normal cdf and survival against the reference`: every row of the `normal` tables within relative 1e-12 (rows below 1e-300 are skipped by the assertion, not removed).
  - `normal quantile against the reference`: the quantile table within relative 1e-12; `quantile (normal 0 1) 0.5 = 0.0`.
  - `normal density`: `density (normal 0.0 1.0) 0.0` and at 1 and 5 within 1e-13 of `exp (-x*x/2) / sqrt (2π)`; `logDensity` at 30 within 1e-13 of `-450 - 0.5 * log (2π)` (closed forms).
  - `quantile round trips`: `roundTrip` over both grids for every family of this task with `uniform 0.0 4.0`, which takes `upperGrid` without `1 − 1e-10`; also p = 1e-300 for `normal 0 1`, `exponential 2`, `logistic 1 2`, `cauchy 0 1`.
  - `quantile at 0 and 1 is the support's bounds`: for every family, `quantile d 0.0` and `quantile d 1.0` equal the two parts of `support d`; `quantile d nan` is NaN; p = −0.1 and 1.1 raise `ArgumentOutOfRangeException`.
  - `logNormal against the reference`: the `lognormal` tables within relative 1e-12; `mean = exp (0.5 + 0.32)` and `variance = (exp 0.64 - 1.0) * exp (1.0 + 0.64)` within 1e-14 (closed forms).
  - `exponential closed forms`: for rate 2, `cdf` at 1e-20 within 1e-14 of `2e-20 * (1.0 - 1e-20)` (Taylor), `survival` at 300 within 1e-13 of `exp -600.0`; `mean = 0.5`, `variance = 0.25`.
  - `uniform closed forms`: for `uniform 0.0 4.0`, `cdf 1.0 = 0.25`, `density` 0.25 on [0, 4] and 0 outside, `quantile p = 4.0 * p` exactly; `mean = 2.0`, `variance = 16.0 / 12.0`.
  - `cauchy tails`: for `cauchy 0.0 1.0`, `cdf -1e100` within 1e-14 of `1e-100 / π`; `cdf 0.0 = 0.5`; `cdf 1.0` within 1e-15 of 0.75; `survival 1e10` within 1e-14 of `1e-10 / π` (the leading term of atan(1/z)/π; the next is 3e-21 of it); `quantile 0.75` within 1e-15 of 1; `mean` and `variance` NaN.
  - `weibull closed forms`: for `weibull 1.5 2.0`, `cdf` at 2e-14 within 1e-13 of `t * (1.0 - t / 2.0)` with `t = 1e-14 ** 1.5`; `survival 20.0` within 1e-13 of `exp (-(10.0 ** 1.5))`; for `weibull 2.0 3.0`, `mean = 3 * sqrt π / 2` and `variance = 9 * (1 - π / 4)` within 1e-14; `density (weibull 0.5 1.0) 0.0 = infinity`.
  - `logistic closed forms`: for `logistic 1.0 2.0`, `cdf -1399.0` within 1e-13 of `1.0 / (1.0 + exp 700.0)`; `survival 1401.0` the same; `mean = 1.0`; `variance = 4 * π² / 3` within 1e-14.
  - `the density integrates to the cdf`: for every family, `Integrate.adaptive 1e-14 1e-12 (density d) a b` with `a = quantile d 0.1`, `b = quantile d 0.9` has `Converged` and `Value` within 1e-10 of `cdf d b - cdf d a`.
  - `support`: `normal` (−∞, ∞), `logNormal` (0, ∞), `uniform 0 4` (0, 4), `exponential` (0, ∞), `cauchy` (−∞, ∞), `weibull` (0, ∞), `logistic` (−∞, ∞); `standardDeviation = sqrt variance` for each.
  - `NaN and infinite arguments`: `cdf`, `survival`, `density` at NaN are NaN; `cdf` at −∞ is 0 and at ∞ is 1; `survival` the reverse; `density` outside the support is 0 and `logDensity` −∞.
  - `describe`: `"Normal(mean = 0, sd = 1)"`, `"LogNormal(mu = 0.5, sigma = 0.8)"`, `"Uniform(lower = 0, upper = 4)"`, `"Exponential(rate = 2)"`, `"Cauchy(location = 0, scale = 1)"`, `"Weibull(shape = 1.5, scale = 2)"`, `"Logistic(location = 1, scale = 2)"`.
  - `constructors reject parameters outside their domain`: `throwsT<ArgumentOutOfRangeException>` for `normal 0 0`, `normal 0 -1`, `normal 0 nan`, `normal infinity 1`, `logNormal 0 0`, `uniform 1 1`, `uniform 2 1`, `uniform -infinity 0`, `exponential 0`, `exponential infinity`, `cauchy 0 0`, `weibull 0 1`, `weibull 1 0`, `logistic 0 -1`.
  - `equal parameters give equal distributions`: `normal 0.0 1.0 = normal 0.0 1.0`; `normal 0.0 1.0 <> normal 0.0 2.0`.
  - `samples follow the distribution`: for every family, `samples d 20000 (Generator.create 2026)` has `ksDistance (cdf d) xs <= 2.226 / sqrt 20000.0` (the asymptotic Kolmogorov critical value at α = 1e-4, √(−ln(α/2)/2); *general knowledge*) and, for families with finite variance, a sample mean within `5.0 * standardDeviation d / sqrt 20000.0` of `mean d`; the same values and final generator as 20000 successive `sample` calls; `samples d -1` raises `ArgumentOutOfRangeException`.

- [ ] **Step 3: Run** `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "Continuous"`. Expected: compile error, `Continuous` is not defined.

- [ ] **Step 4: Implement `Check.fs` and `Continuous.fs`, and move `ConfidenceSequence.fs` onto `Check`.** `Check.fs` compiles before `ConfidenceSequence.fs`; `ConfidenceSequence.fs` loses its private `Arguments` module and calls `Check.alpha`, `Check.finitePositive` and `Check.notNegative`, so no copy remains. `ConfidenceSequence.radius`'s existing rule that a NaN standard deviation gives NaN is kept by calling `Check.notNegative (nameof sd) sd` only when `sd` is not NaN; the existing `ConfidenceSequence` tests pass unchanged. Decisions:
  - `Continuous` is `type Continuous = private | Normal of mean: float * sd: float | ...`, one case per constructor; Task 2 adds five cases. Constructors raise `ArgumentOutOfRangeException` for a NaN or infinite parameter, a scale, rate or shape ≤ 0, and `lower ≥ upper`.
  - Normal: z = (x − mean)/sd; `cdf = 0.5 · erfc(−z/√2)`, `survival = 0.5 · erfc(z/√2)`. Quantile: Acklam's approximation (the coefficients of `NormalQuantile.fs`) at min(p, 1 − p), then one Halley step on the lower-tail cdf, e = cdf(x) − p, u = e·√(2π)·exp(x²/2), x ← x − u/(1 + x·u/2), skipped where `exp` overflows; for p > 0.5 the result is the negated lower quantile at 1 − p (exact for p ≥ 0.5).
  - Tails use `Special.log1p` and `Special.expm1` (no local copy): exponential `cdf = −Special.expm1(−rate·x)`; Weibull `cdf = −Special.expm1(−(x/scale)^shape)`; their quantiles use `−Special.log1p(−p)` for p ≤ 0.5 and `−ln(1 − p)` above. Cauchy `cdf(z) = atan(−1/z)/π` for z < 0, `survival(z) = cdf(−z)`, quantile `location − scale / tan(πp)` for p < 0.5, symmetric above. Logistic cdf `1/(1 + e^−z)`, quantile `ln p − log1p(−p)` below 0.5, symmetric above.
  - Sampling: normal `mean + sd·z`; log-normal `exp` of a normal; uniform `lower + (upper − lower)·u`; exponential `Generator.exponential`; Cauchy, Weibull and logistic by inversion of a uniform, redrawing u = 0 where the inversion needs u in (0, 1).
  - `describe` prints `Name(param = value, ...)` with F#'s `string` of each parameter and the constructor's parameter names. Its doc comment states that a parameter's text outside [1e-5, 1e15) in magnitude may differ between .NET and JavaScript.
  - Doc comments: each of `cdf`, `survival` and `quantile` states "relative error at most 1e-12 where the value is at least 1e-300" over the parameter values the tests cover, and the quantile's round-trip rule; `quantile` raises `ArgumentOutOfRangeException` for p outside [0, 1].
  - `NormalQuantile.ofProbability`'s doc comment gains one sentence: it is the faster approximation, and `Continuous.quantile` of `Continuous.normal 0.0 1.0` gives full precision.

- [ ] **Step 5: Run** the filtered list again. Expected: PASS.
- [ ] **Step 6: Run the gate.** `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then `npx mocha dist/tests/Main.fs.js`. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): continuous distributions with closed forms and a full-precision normal`.

### Task 2: Continuous distributions over the incomplete gamma and beta functions

**Files:**
- Modify: `src/Ranvier.Statistics/Continuous.fs`, `tests/Ranvier.Science.Tests/ContinuousTests.fs`, `tests/reference/special.py` (sections `dist-gamma`, `dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`)

**Interfaces:**
- Consumes (Task 1): `Continuous` and its module, `Check.*`; the script's `ln_gamma`, `gamma_p`, `gamma_q`, `invert`, `emit` (`special.py`). Plan T2: `Special.log1p`, `Special.expm1`, `Special.gammaLn : float -> float`, `Special.betaLn : float -> float -> float`, `Special.gammaLowerRegularized : float -> float -> float` (P(a, x)), `Special.gammaUpperRegularized : float -> float -> float` (Q(a, x)), `Special.gammaLowerRegularizedInverse : float -> float -> float` (x with P(a, x) = p), `Special.betaRegularized : float -> float -> float -> float` (I_x(a, b)), `Special.betaRegularizedInverse : float -> float -> float -> float`; `Generator.gamma : shape: float -> scale: float -> Generator -> struct (float * Generator)`, `Generator.beta : a: float -> b: float -> Generator -> struct (float * Generator)`. The spec fixes these names but not the parameter order of the beta functions: this plan reads them as `betaRegularized a b x` and `betaRegularizedInverse a b p` (spec §6: configuration first, data last). Before Step 4, read the doc comments in the `Ranvier.Numerics` file that defines `module Special` (`rg -n "module Special" src`) and use the order they state.
- Produces:

```fsharp
module Continuous =
    val gamma : shape: float -> scale: float -> Continuous
    val chiSquared : dof: float -> Continuous
    val beta : a: float -> b: float -> Continuous
    val studentT : dof: float -> Continuous
    val fisherF : d1: float -> d2: float -> Continuous
    // density, logDensity, cdf, survival, quantile, mean, variance, standardDeviation, support,
    // sample, samples, describe now cover these five families as well
```

- [ ] **Step 1: Add the sections to `tests/reference/special.py`.** The script's `beta_inc(a, b, x)` (Plan T2, the function behind its `incbeta` section), `gamma_p` and `gamma_q` give the incomplete functions; nothing is re-implemented here. Self-checks to 1e-50 in the new sections: `beta_inc(1, 1, x) = x`; `beta_inc(2.5, 3.5, 0.3) + beta_inc(3.5, 2.5, 0.7) = 1`. Sections, each printing `cdf`, `survival` and `density` at the points and the quantile at p ∈ {0.05, 0.975}:
  - `dist-gamma`: (shape 2.5, scale 2) at x ∈ {1e-5, 0.1, 1, 5, 20, 100}; (0.5, 1) at {1e-10, 0.01, 0.5, 3, 50}; (100, 1) at {60, 90, 100, 110, 150}.
  - `dist-chi-squared`: dof 3 at {0.01, 1, 3, 10, 50}; dof 10 at {1, 10, 30, 100}.
  - `dist-beta`: (2.5, 3.5) at {1e-6, 0.1, 0.4, 0.8, 0.999}.
  - `dist-student-t`: dof ∈ {2.5, 5, 30} at t ∈ {−1000, −10, −2.5, −1, 0.4, 1.96, 4, 100}, through `beta_inc(ν/2, 1/2, ν/(ν + t²))`.
  - `dist-fisher-f`: (5, 10) at {0.01, 0.5, 1, 3, 20, 1000}; (2, 30) at {0.1, 1, 5, 50}.

- [ ] **Step 2: Write the failing tests**, appended to the `"Continuous"` list:
  - `gamma against the reference`, `chi-squared against the reference`, `beta against the reference`, `Student t against the reference`, `F against the reference`: every row within relative 1e-12 (cdf, survival and density; quantiles too).
  - `chi-squared with 2 degrees of freedom is the exponential with rate 1/2`: `cdf`, `survival`, `density` at x ∈ {1e-8, 0.5, 2, 40, 1000} and `quantile` over both grids within relative 1e-12 of `exponential 0.5` (identity).
  - `gamma with shape 1 is the exponential`: `gamma 1.0 3.0` against `exponential (1.0/3.0)`, the same points.
  - `beta(1, 1) is the uniform and beta(1/2, 1/2) the arcsine`: `cdf (beta 1 1) x = x`; `cdf (beta 0.5 0.5) x` within 1e-13 of `2/π · asin (sqrt x)` at x ∈ {1e-12, 0.1, 0.5, 0.9} (closed forms).
  - `Student t with 1 degree of freedom is the Cauchy`: at t ∈ {−1e100, −1e10, −3, −1, 0, 0.5, 2, 1e10}, `cdf`, `survival` and `density` within relative 1e-12 of `cauchy 0.0 1.0`; quantiles over both grids (identity, spec §5.2).
  - `the square of a t is an F with 1 numerator degree of freedom`: `survival (fisherF 1.0 5.0) (t * t)` within relative 1e-12 of `2.0 * survival (studentT 5.0) t` at t ∈ {0.3, 1, 2.5, 10, 300} (identity).
  - `quantile round trips for the incomplete-function families`: `roundTrip` over both grids for `gamma 2.5 2.0`, `gamma 0.5 1.0`, `chiSquared 3.0`, `chiSquared 10.0`, `studentT 2.5`, `studentT 30.0`, `fisherF 5.0 10.0`, and for `beta 2.5 3.5` without `1 − 1e-10`.
  - `densities at the lower bound`: at 0, `gamma 0.5 1.0` +∞, `gamma 1.0 2.0` 0.5, `gamma 2.0 1.0` 0; `chiSquared 1.0` +∞, `chiSquared 2.0` 0.5; `beta 0.5 2.0` +∞, `beta 1.0 3.0` 3, `beta 2.0 2.0` 0; `fisherF 1.0 5.0` +∞, `fisherF 2.0 5.0` 1, `fisherF 5.0 5.0` 0.
  - `moments where they exist`: `studentT 1` mean and variance NaN; `studentT 2` mean 0, variance +∞; `studentT 5` variance 5/3; `fisherF 5 2` mean +∞, variance NaN; `fisherF 5 4` mean 2, variance +∞; `fisherF 5 10` mean 1.25, variance 1300/960; `gamma 2.5 2` mean 5, variance 10; `beta 2.5 3.5` mean 2.5/6, variance 8.75/252; `chiSquared 3` mean 3, variance 6 (closed forms, within 1e-14).
  - `the density integrates to the cdf` and `samples follow the distribution`: as in Task 1, for the five new families (mean check only where the variance is finite).
  - `describe`: `"Gamma(shape = 2.5, scale = 2)"`, `"ChiSquared(dof = 3)"`, `"Beta(a = 2.5, b = 3.5)"`, `"StudentT(dof = 5)"`, `"FisherF(d1 = 5, d2 = 10)"`.
  - `constructors reject parameters outside their domain`, extended: `gamma 0 1`, `gamma 1 0`, `chiSquared 0`, `chiSquared infinity`, `beta 0 1`, `beta 1 nan`, `studentT 0`, `studentT infinity`, `fisherF 0 1`, `fisherF 1 -1`.

- [ ] **Step 3: Run** the `"Continuous"` list. Expected: FAIL to compile (`Continuous.gamma` not defined).

- [ ] **Step 4: Implement** the five families. Decisions:
  - Gamma(k, θ): `cdf = P(k, x/θ)`, `survival = Q(k, x/θ)`; chi-squared(ν) is the case Gamma(ν/2, 2) and keeps its own `describe`.
  - Beta: `cdf = I_x(a, b)`, `survival = I_{1−x}(b, a)`. Student t: for t < 0, `cdf = I_{ν/(ν+t²)}(ν/2, 1/2)/2`, with x = ν/t² computed as (ν/|t|)/|t| where t² overflows; for t² < ν use `0.5 − I_{t²/(ν+t²)}(1/2, ν/2)/2`; symmetric above 0. F(d₁, d₂): `cdf = I_{d₁x/(d₁x+d₂)}(d₁/2, d₂/2)`, `survival = I_{d₂/(d₂+d₁x)}(d₂/2, d₁/2)`.
  - Quantiles start from T2's inverses: gamma `θ · gammaLowerRegularizedInverse k p`; beta `betaRegularizedInverse a b p`; t and F map from the beta inverse as their cdfs map to I_x. For p > 0.5 each solves `survival q = 1 − p` instead: t by symmetry, q(p) = −q(1 − p); beta and F through the reflected beta inverse; gamma by at most 8 Newton steps on Q from the P-inverse start, stopping when a step is below 4 ulps of q.
  - Densities: `exp` of the log density, which uses `Special.gammaLn` and `Special.betaLn`; at the lower bound, the limits the `densities at the lower bound` test lists.
  - Moments: +∞ where the defining integral diverges to +∞, NaN where the moment is undefined (stated in the doc comments of `mean` and `variance`).
  - Sampling: gamma `Generator.gamma`; chi-squared `Generator.gamma (ν/2) 2`; beta `Generator.beta`; t `Z / sqrt (V/ν)` with V a chi-squared draw; F `(V₁/d₁)/(V₂/d₂)`. All exact in distribution.
  - Doc comments: the bounds of Task 1, over the parameter values the tests cover (shapes and degrees of freedom from 0.5 to 100).

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): gamma, chi-squared, beta, Student t and F distributions`.

### Task 3: Discrete distributions

**Files:**
- Create: `src/Ranvier.Statistics/Discrete.fs`, `tests/Ranvier.Science.Tests/DiscreteTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Discrete.fs` after `Continuous.fs`), `tests/reference/special.py` (section `discrete`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: Task 1's `Check.*`, `Continuous.quantile (Continuous.normal 0.0 1.0)`; the script's helpers. Plan T2: `Special.log1p`, `Special.expm1`, `Special.logChoose : int -> int -> float` (ln C(n, k)), `Special.logFactorial : int -> float`, `Special.gammaLn`, `Special.betaRegularized`, `Special.gammaLowerRegularized`, `Special.gammaUpperRegularized` (read the parameter order in the doc comments of the file that defines `module Special`; this plan expects `betaRegularized a b x`); `Generator.binomial : trials: int -> p: float -> Generator -> struct (int * Generator)`, `Generator.poisson : mean: float -> Generator -> struct (int * Generator)`, `Generator.gamma`, `Generator.integer : lower: int -> upperExclusive: int -> Generator -> struct (int * Generator)`, `Generator.uniform`.
- Produces:

```fsharp
type Discrete   // a union with private cases; structural equality

module Discrete =
    val binomial : trials: int -> p: float -> Discrete
    val poisson : mean: float -> Discrete
    val geometric : p: float -> Discrete                       // failures before the first success
    val negativeBinomial : r: float -> p: float -> Discrete    // failures before the r-th success; real r > 0
    val hypergeometric : population: int -> successes: int -> draws: int -> Discrete
    val bernoulli : p: float -> Discrete
    val uniform : lower: int -> upper: int -> Discrete         // both included
    val mass : Discrete -> int -> float
    val logMass : Discrete -> int -> float
    val cdf : Discrete -> int -> float                         // P(X ≤ k)
    val survival : Discrete -> int -> float                    // P(X > k)
    val quantile : Discrete -> p: float -> int                 // the least k with cdf k ≥ p
    val mean : Discrete -> float
    val variance : Discrete -> float
    val support : Discrete -> struct (int * int)               // Int32.MaxValue: unbounded above
    val sample : Discrete -> Generator -> struct (int * Generator)
    val samples : Discrete -> count: int -> Generator -> struct (int[] * Generator)
    val describe : Discrete -> string
```

- [ ] **Step 1: Extend the script**, section `discrete`, exact in `fractions` where the masses are rational at the double's exact value, otherwise `decimal`. Print `mass`, `cdf`, `survival` per point:
  - binomial (10, 0.3) at k = −1..11; (1000, 0.5): `cdf` at 400 and 450, `survival` at 550 and 600; (100, 0.01): `mass` at 0 and 3, `cdf` at 5, `survival` at 10.
  - poisson 4 at k = 0..20 and 40; poisson 1000: `cdf` at 900, `survival` at 1100.
  - negativeBinomial (3, 0.4) at k = 0..15; (2.5, 0.4) at k ∈ {0, 3, 10}, with mass Π_{j<k}(r + j)/k! · p^r (1 − p)^k.
  - hypergeometric (50, 10, 5) at k = 0..5; (1000, 400, 100): `cdf` at 20 and 40, `survival` at 60.

- [ ] **Step 2: Write the failing tests**, list `"Discrete"`, with the script's tables and command:
  - `binomial against exact fractions`, `binomial tails`, `Poisson against the reference`, `negative binomial against the reference`, `hypergeometric against exact fractions`: each row within relative 1e-12 (rows below 1e-300 skipped by the assertion); outside the support `mass` is 0, `cdf` 0 below and 1 above.
  - `geometric closed forms`: `geometric 0.2`, `mass k` within 1e-14 of `0.8 ** k * 0.2`, `survival k` of `0.8 ** (k + 1)` for k ∈ {0, 1, 5, 50}; `cdf (geometric 1e-10) 0` within 1e-14 of 1e-10.
  - `Bernoulli and discrete uniform closed forms`: `bernoulli 0.3` mass 0.7 at 0 and 0.3 at 1; `uniform 1 6` mass 1/6 on 1..6, `cdf 3 = 0.5`, mean 3.5, variance 35/12.
  - `the mass sums to 1`: over the whole support for `binomial 50 0.37` and `hypergeometric 50 10 5`, and over 0..60 for `poisson 4`, within 1e-14 of 1.
  - `logMass is the log of mass`: within 1e-13 relative on the binomial table rows; `logMass` outside the support is −∞.
  - `quantile is the least k with cdf at least p`: for every family and p ∈ {1e-10, 0.01, 0.3, 0.5, 0.6, 0.975, 1 − 1e-10}, with q = `quantile d p`: `cdf d q >= p` and `cdf d (q - 1) < p` for p ≤ 0.5; `survival d q <= 1.0 - p` and `survival d (q - 1) > 1.0 - p` above; and for binomial (10, 0.3) at p ∈ {0.1, 0.5, 0.9}, q equals the least k whose reference `cdf` is at least p.
  - `quantile at 0 and 1 is the support's bounds`: `quantile d 0.0` and `quantile d 1.0` are the parts of `support d` (`Int32.MaxValue` above for Poisson, geometric and negative binomial); p = −0.1, 1.1 and NaN raise `ArgumentOutOfRangeException`.
  - `mean and variance`: closed forms for one parameter set of each family (binomial np and np(1 − p); Poisson λ, λ; geometric (1 − p)/p, (1 − p)/p²; negative binomial r(1 − p)/p, r(1 − p)/p²; hypergeometric nK/N and nK(N − K)(N − n)/(N²(N − 1)); Bernoulli p, p(1 − p)).
  - `describe`: `"Binomial(trials = 10, p = 0.3)"`, `"Poisson(mean = 4)"`, `"Geometric(p = 0.2)"`, `"NegativeBinomial(r = 3, p = 0.4)"`, `"Hypergeometric(population = 50, successes = 10, draws = 5)"`, `"Bernoulli(p = 0.3)"`, `"DiscreteUniform(lower = 1, upper = 6)"`.
  - `constructors reject parameters outside their domain`: `binomial -1 0.5`, `binomial 10 1.5`, `binomial 10 nan`, `poisson 0`, `poisson infinity`, `geometric 0`, `negativeBinomial 0 0.5`, `negativeBinomial 2 1.5`, `hypergeometric 10 11 3`, `hypergeometric 10 3 11`, `hypergeometric -1 0 0`, `bernoulli -0.1`, `uniform 3 2`, `uniform 0 Int32.MaxValue`.
  - `samples follow the distribution`: for each family, 20000 draws from `Generator.create 2027`: the frequency of each k in the five most likely values within `5 * sqrt (m * (1 - m) / 20000)` of its mass m, the sample mean within `5 * sqrt (variance / 20000)` of `mean`; `samples` equals successive `sample` calls; degenerate distributions (`binomial 0 0.5`, `binomial 5 1.0`, `uniform 4 4`) return their one value and the generator unchanged.

- [ ] **Step 3: Run** `-- --filter "Discrete"`. Expected: compile error, `Discrete` is not defined.

- [ ] **Step 4: Implement `Discrete.fs`.** Decisions:
  - Binomial `cdf k = I_{1−p}(n − k, k + 1)` and `survival k = I_p(k + 1, n − k)` for 0 ≤ k < n. Poisson `cdf k = Q(k + 1, λ)`, `survival k = P(k + 1, λ)`. Geometric by `Special.log1p`/`Special.expm1` closed forms. Negative binomial `cdf k = I_p(r, k + 1)`, `survival k = I_{1−p}(k + 1, r)`. Masses through `exp` of `logMass` (`logChoose`, `logFactorial`, `gammaLn`, `log1p`), with 0·ln 0 taken as 0 at p = 0 or 1.
  - Hypergeometric: `mass` from three `logChoose` terms; `cdf k` sums the masses from the lower support bound to k when k is below the mode, else is 1 − `survival k`, which sums from k + 1 to the upper bound; cost O(min(k − lower, upper − k)), stated in the doc comment.
  - Quantile: start at ⌊mean + sd · z_p⌋ clamped to the support (z_p the standard normal quantile), then step down while the condition holds at k − 1 and up while it fails at k; for p > 0.5 the condition is `survival k ≤ 1 − p`. p = 0 and 1 give the support's bounds; a result past `Int32.MaxValue` saturates there; a NaN p raises (an `int` has no NaN).
  - Sampling, exact in distribution: binomial and Poisson from the generator; geometric ⌊ln u / Special.log1p(−p)⌋ with u = 1 − uniform in (0, 1]; negative binomial as a Poisson draw whose mean is a `Generator.gamma r ((1 − p)/p)` draw; hypergeometric by `draws` sequential urn draws (O(draws)); Bernoulli `uniform < p`; uniform `Generator.integer lower (upper + 1)`. A degenerate distribution returns its value and the generator unchanged.
  - Doc comments: `mass`, `cdf` and `survival` state "relative error at most 1e-12 where the value is at least 1e-300" over the tests' parameters (trials and population up to 1000, means up to 1000).

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): discrete distributions`.

### Task 4: Descriptive statistics

**Files:**
- Create: `src/Ranvier.Statistics/Describe.fs`, `tests/Ranvier.Science.Tests/DescribeTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Describe.fs` after `Discrete.fs`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: Task 1's `Check.*`; Plan T1's `OrderStatistic.quantileSorted : p: float -> sorted: float[] -> float` (type 7; NaN for an empty array; raises `ArgumentOutOfRangeException` unless 0 ≤ p ≤ 1).
- Produces:

```fsharp
type Summary =
    { Count: int; Mean: float; StandardDeviation: float; Minimum: float; LowerQuartile: float
      Median: float; UpperQuartile: float; Maximum: float }
type BinRule = Sturges | Scott | FreedmanDiaconis | Count of int

module Describe =
    val quantile : p: float -> float[] -> float          // type 7; sorts a copy
    val quantileSorted : p: float -> float[] -> float
    val quantiles : ps: float[] -> float[] -> float[]
    val median : float[] -> float
    val interquartileRange : float[] -> float
    val medianAbsoluteDeviation : float[] -> float        // unscaled
    val trimmedMean : proportion: float -> float[] -> float
    val ranks : float[] -> float[]                        // 1-based, average ranks for ties; raises on NaN
    val autocorrelation : maxLag: int -> float[] -> float[]
    val summary : float[] -> Summary                      // StandardDeviation with divisor n − 1
    val binEdges : BinRule -> float[] -> float[]
```

- [ ] **Step 1: Write the failing tests**, list `"Describe"`. Every expected value is a closed form computed by hand from the definition named in the test:
  - `quantile type 7`: xs = `[| 10.0; 1.0; 4.0; 7.0; 3.0 |]`, h = (n − 1)p: p 0 → 1, 1 → 10, 0.5 → 4, 0.25 → 3, 0.1 → 1.8, 0.9 → 8.8 (within 1e-15); xs is unchanged afterwards.
  - `quantiles and quantileSorted agree with quantile`: `quantiles [| 0.1; 0.5; 0.9 |] xs` and `quantileSorted p (Array.sort xs)` give the same values.
  - `median, interquartile range and median absolute deviation`: xs = `[| 1.0; 2.0; 3.0; 4.0; 100.0 |]`: 3, 2, 1.
  - `trimmed mean`: xs = `[| 1.0 .. 9.0 |]` with 100 appended: proportion 0.1 → 5.5, 0.25 → 5.5, 0 → 14.5; 0.5 and −0.1 raise `ArgumentOutOfRangeException`.
  - `ranks with ties`: `ranks [| 3.0; 1.0; 4.0; 1.0; 5.0; 9.0; 2.0; 6.0; 5.0 |] = [| 4.0; 1.5; 5.0; 1.5; 6.5; 9.0; 3.0; 8.0; 6.5 |]`; `ranks [||]` is empty; a NaN raises `ArgumentException`.
  - `autocorrelation`: `autocorrelation 2 [| 1.0 .. 5.0 |] = [| 1.0; 0.4; -0.1 |]` within 1e-15; a constant series gives NaN at every lag; maxLag −1 and maxLag 5 (= n) raise `ArgumentOutOfRangeException`.
  - `summary`: xs = `[| 2.0; 4.0; 4.0; 4.0; 5.0; 5.0; 7.0; 9.0 |]`: Count 8, Mean 5, StandardDeviation `sqrt (32.0 / 7.0)`, Minimum 2, LowerQuartile 4, Median 4.5, UpperQuartile 5.5, Maximum 9.
  - `summary of an empty array and of one item`: `[||]` gives Count 0 and NaN in every other field; `[| 3.5 |]` gives StandardDeviation NaN and 3.5 in the others.
  - `NaN in the data`: with `nan` in xs, `quantile`, `median`, `interquartileRange`, `medianAbsoluteDeviation`, `trimmedMean` and `summary`'s Mean and order statistics are NaN, and Count is the array's length.
  - `quantile of an empty array is NaN`; `quantile` with p = 1.5 raises `ArgumentOutOfRangeException`; p = NaN gives NaN.
  - `bin edges follow NumPy's rules`: for xs = 0.0 .. 99.0 (100 values), with `edges lo hi bins = Array.init (bins + 1) (fun k -> if k = bins then hi else lo + float k * ((hi - lo) / float bins))` (NumPy's `linspace`): `Sturges` gives `edges 0 99 8` (⌈log₂ 100 + 1⌉); `Scott` `edges 0 99 5` (width (24√π/n)^{1/3}·σ with σ the population standard deviation, 21.71); `FreedmanDiaconis` `edges 0 99 5` (width 2·IQR·n^{−1/3}, 21.33); `Count 4` `[| 0.0; 24.75; 49.5; 74.25; 99.0 |]` (numpy 2.x `histogram_bin_edges`, *general knowledge* ⚠️: confirm the three rules against its documentation page and cite the page; if it cannot be read, cite the rules as written here).
  - `bin edges of constant data and bad input`: `[| 2.0; 2.0; 2.0 |]` gives `[| 1.5; 2.5 |]` for `Scott`, `FreedmanDiaconis` and `Sturges` and `edges 1.5 2.5 2` for `Count 2`; an empty array, a NaN, an infinity and `Count 0` raise `ArgumentException` or `ArgumentOutOfRangeException`.

- [ ] **Step 2: Run** `-- --filter "Describe"`. Expected: compile error, `Describe` is not defined.

- [ ] **Step 3: Implement `Describe.fs`.** Decisions: type 7, h = (n − 1)p, `(1 − f)·x[⌊h⌋] + f·x[⌊h⌋ + 1]` with f = h − ⌊h⌋, taken from Plan T1's `OrderStatistic.quantileSorted` (spec §6.13): `Describe.quantileSorted` delegates to it and no second copy of the interpolation is written. `quantile`, `quantiles` and the order statistics scan for NaN while copying (O(n)) and return NaN; `quantileSorted` is O(1), reads the array as sorted ascending and scans nothing. `trimmedMean` cuts ⌊n · proportion⌋ from each end (R's `mean(trim =)` and SciPy's `trim_mean`, *general knowledge*), 0 ≤ proportion < 0.5. `medianAbsoluteDeviation`'s doc comment gives 1.4826 as the factor for consistency with the normal. `autocorrelation` r_k = Σ_{t<n−k}(x_t − x̄)(x_{t+k} − x̄) / Σ(x_t − x̄)². `binEdges` follows NumPy's `histogram_bin_edges` (numpy 2.x, as confirmed in Step 1), with constant data widened to [x − 0.5, x + 0.5] and one bin for the width rules, as NumPy does. Doc comments state costs: O(n log n) for the sorting reads.

- [ ] **Step 4: Run** the filtered list. Expected: PASS.
- [ ] **Step 5: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 6: Commit** `feat(statistics): descriptive statistics`.

### Task 5: Confidence intervals

**Files:**
- Create: `src/Ranvier.Statistics/Intervals.fs`, `tests/Ranvier.Science.Tests/IntervalTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Intervals.fs` after `Describe.fs`), `tests/reference/special.py` (section `intervals`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: `Interval = { Lower: float; Upper: float }` (struct, `ConfidenceSequence.fs:7`); Task 1's `Check.alpha`, `Check.finitePositive`; `Continuous.quantile`, `Continuous.normal`, `Continuous.studentT`, `Continuous.chiSquared`, `Continuous.beta` (Tasks 1, 2); `Moments.count`, `Moments.mean`; Plan T1's `Moments.sampleVariance : Moments -> float` (divisor n − 1, NaN below 2) and `Moments.sampleStandardDeviation : Moments -> float`.
- Produces:

```fsharp
module MeanInterval =
    val student : alpha: float -> Moments -> Interval                  // t with n − 1, sample standard deviation
    val knownSigma : alpha: float -> sigma: float -> Moments -> Interval
module ProportionInterval =
    val wilson : alpha: float -> successes: int -> trials: int -> Interval
    val clopperPearson : alpha: float -> successes: int -> trials: int -> Interval
    val agrestiCoull : alpha: float -> successes: int -> trials: int -> Interval
    val jeffreys : alpha: float -> successes: int -> trials: int -> Interval
module VarianceInterval =
    val chiSquared : alpha: float -> Moments -> Interval
module DifferenceInterval =                                              // mean a − mean b
    val welch : alpha: float -> Moments -> Moments -> Interval
    val pooled : alpha: float -> Moments -> Moments -> Interval
```

- [ ] **Step 1: Extend the script**, section `intervals`, printing the bounds (Decimal; quantiles by `invert` on the Decimal cdfs):
  - data A = `[5; 7; 8; 9; 9; 10; 11; 12; 14; 15]`: `student` at α ∈ {0.05, 0.01}; `knownSigma` with σ = 3 at α = 0.05; `chiSquared` at α = 0.05.
  - proportions (81, 263), (0, 20), (20, 20), (1, 29) at α = 0.05, and (81, 263) at α = 0.01: the four methods as Step 3 defines them.
  - the sleep data (R's `datasets::sleep`, `extra` by `group`): a = `[0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0]`, b = `[1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4]`: `welch` and `pooled` at α = 0.05.

- [ ] **Step 2: Write the failing tests**, list `"Intervals"`, with the script's tables and command; moments fold with `Array.fold (Moments.add true) Moments.zero`:
  - `the Student interval against the reference`, `the known-sigma interval against the reference`, `the variance interval against the reference`, `proportion intervals against the reference`, `difference intervals against the reference`: each bound within `1e-12 * max (abs lower) (abs upper)` of the reference. ⚠️ Cross-check, used only if confirmed from R's `?t.test` example output: R prints the sleep Welch interval as −3.3654832 to 0.2054832; if confirmed, the test cites it beside the script's literal and asserts agreement to its printed digits.
  - `proportion intervals at 0 and n successes`: at (0, 20) the Clopper–Pearson, Jeffreys and Agresti–Coull lower bounds are 0; at (20, 20) their upper bounds are 1; every bound of every method lies in [0, 1].
  - `small samples give NaN bounds`: `student` and `chiSquared` at count 1, `knownSigma` at count 0, `welch` with one count of 1, `pooled` with counts 1 and 1, every proportion method at trials 0.
  - `zero spread gives a zero-width interval`: `welch` and `pooled` of two constant samples (`[| 2.0; 2.0; 2.0 |]`, `[| 5.0; 5.0 |]`) give Lower = Upper = −3.
  - `arguments out of range raise`: α ∈ {0, 1, NaN} for every function; σ ∈ {0, −1, ∞}; successes −1, successes > trials, trials −1 (`ArgumentOutOfRangeException`).
  - `the Student interval covers at its level`: for seeds 1..2000, 10 standard normal deviates each (`Generator.normals 0.0 1.0 10`), the fraction of `student 0.05` intervals holding 0 lies in [0.925, 0.975] (5 standard errors of a binomial fraction at 0.95).

- [ ] **Step 3: Run** `-- --filter "Intervals"`. Expected: compile error.

- [ ] **Step 4: Implement `Intervals.fs`.** Decisions, each stated in the doc comment: z = the standard normal quantile at 1 − α/2. `student`: mean ± t_{1−α/2, n−1} · s/√n. `knownSigma`: mean ± z·σ/√n. `wilson`: (p̂ + z²/2n)/(1 + z²/n) ± z/(1 + z²/n) · √(p̂(1 − p̂)/n + z²/4n²). `clopperPearson`: the Beta(x, n − x + 1) quantile at α/2 and the Beta(x + 1, n − x) quantile at 1 − α/2, with 0 at x = 0 and 1 at x = n. `agrestiCoull`: ñ = n + z², p̃ = (x + z²/2)/ñ, p̃ ± z√(p̃(1 − p̃)/ñ), clipped to [0, 1]. `jeffreys`: Beta(x + ½, n − x + ½) quantiles at α/2 and 1 − α/2, with 0 at x = 0 and 1 at x = n (Brown, Cai and DasGupta, "Interval estimation for a binomial proportion", *Statistical Science* 16(2), 2001; *general knowledge*). `chiSquared`: [(n − 1)s²/χ²_{1−α/2}, (n − 1)s²/χ²_{α/2}] with n − 1 degrees of freedom. `welch`: d ± t_{1−α/2, ν}·√(s²_a/n_a + s²_b/n_b), ν by Welch–Satterthwaite; a zero standard error gives [d, d]. `pooled`: s²_p = ((n_a − 1)s²_a + (n_b − 1)s²_b)/(n_a + n_b − 2), d ± t_{1−α/2, n_a+n_b−2}·s_p√(1/n_a + 1/n_b). Each bound has relative error at most 1e-12 over the tests' samples (from the quantiles' bounds).

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): confidence intervals for means, proportions, variances and differences`.

### Task 6: Hypothesis tests on moments

**Files:**
- Create: `src/Ranvier.Statistics/HypothesisTest.fs`, `tests/Ranvier.Science.Tests/HypothesisTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`HypothesisTest.fs` after `Intervals.fs`), `tests/reference/special.py` (section `parametric`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: `Continuous.normal`, `studentT`, `fisherF`, `cdf`, `survival` (Tasks 1, 2); `Moments.count`, `Moments.mean`, `Moments.variance`; Plan T1's `Moments.sampleVariance`, `Moments.sampleStandardDeviation`.
- Produces:

```fsharp
type Alternative = TwoSided | Less | Greater   // Less: the parameter is below the null value
type TestResult = { Statistic: float; DegreesOfFreedom: float; DenominatorDegreesOfFreedom: float; PValue: float }
                                               // NaN where a field does not apply
module internal PValue =
    /// Less: cdf; Greater: survival; TwoSided: min 1 (2 · min cdf survival). NaN statistic → NaN.
    val continuous : Alternative -> Continuous -> statistic: float -> float
module HypothesisTest =
    val oneSampleT : Alternative -> mu0: float -> Moments -> TestResult
    val oneSampleZ : Alternative -> mu0: float -> sigma: float -> Moments -> TestResult
    val welchT : Alternative -> Moments -> Moments -> TestResult
    val pooledT : Alternative -> Moments -> Moments -> TestResult
    val varianceRatio : Alternative -> Moments -> Moments -> TestResult
    val oneWayAnova : Moments[] -> TestResult
```

The module is `HypothesisTest`, not the spec's first name `Test`, which collides with Expecto's `Test` type in any file that opens Expecto (the coordinator's ruling, spec §7.4).

- [ ] **Step 1: Extend the script**, section `parametric` (statistics exact in `fractions` where rational, p-values from the Decimal cdfs), printing statistic, degrees of freedom and the p-value for each alternative:
  - `oneSampleT` of sleep group a = `[0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0]` (R's `datasets::sleep`, `extra` of group 1) against 0, and of the paired differences `[-1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4]` against 0.
  - `oneSampleZ` of `[1.0 .. 10.0]` against 5 with σ = 3.
  - `welchT`, `pooledT` and `varianceRatio` of sleep a against b = `[1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4]` (group 2).
  - `oneWayAnova` of `[6; 8; 4; 5; 3; 4]`, `[8; 12; 9; 11; 6; 8]`, `[13; 9; 11; 8; 7; 12]` (F with 2 and 15 degrees of freedom).

- [ ] **Step 2: Write the failing tests**, list `"HypothesisTest"`, with the script's tables and command:
  - `one-sample t against the reference`, `one-sample z against the reference`, `Welch and pooled t against the reference`, `variance ratio against the reference`, `one-way ANOVA against the reference`: Statistic, degrees of freedom and PValue within relative 1e-12; unused fields NaN. ⚠️ Cross-check, used only if confirmed from R's documentation or output: R prints for the sleep data Welch t = −1.8608, df = 17.776, p-value = 0.07939, and for the paired test t = −4.0621, df = 9, p-value = 0.002833 (the statistics and Welch df were reproduced here with Python's `statistics` module, *measured here*: −1.860813, 17.77647, −4.062128).
  - `the alternatives are consistent`: for each test above, `Less.PValue + Greater.PValue` within 1e-15 of 1 and `TwoSided.PValue = min 1 (2 * min less greater)`.
  - `fewer than two observations give NaN fields`: `oneSampleT` at count 1, `welchT`, `pooledT` and `varianceRatio` with one sample of count 1: every field NaN, nothing raised; `oneSampleZ` at count 0 likewise.
  - `zero spread`: `oneSampleT TwoSided 0.0` of `[| 2.0; 2.0; 2.0 |]` gives Statistic +∞ and PValue 0, and of `[| 0.0; 0.0 |]` NaN Statistic and PValue; `welchT TwoSided` and `pooledT TwoSided` of `[| 2.0; 2.0; 2.0 |]` and `[| 5.0; 5.0 |]` give Statistic −∞ and PValue 0 (Welch's DegreesOfFreedom NaN), and of two constant samples with equal means NaN Statistic and PValue — consistent with Task 5's zero-width interval at the difference.
  - `an empty group gives NaN fields`: `oneWayAnova` with one group `Moments.zero`; fewer than two groups raises `ArgumentException`.
  - `arguments out of range raise`: σ ∈ {0, −1, NaN} for `oneSampleZ`; `mu0` infinite.

- [ ] **Step 3: Run** `-- --filter "HypothesisTest"`. Expected: compile error.

- [ ] **Step 4: Implement `HypothesisTest.fs`.** Decisions, each in the doc comment: t = (x̄ − μ₀)/(s/√n) with n − 1 degrees of freedom; z = (x̄ − μ₀)/(σ/√n); Welch's t with Welch–Satterthwaite degrees of freedom and the pooled t with n_a + n_b − 2, the parameter being mean a − mean b; `varianceRatio` F = s²_a/s²_b with (n_a − 1, n_b − 1), the parameter σ²_a/σ²_b against 1; ANOVA F = (SSB/(k − 1))/(SSW/(N − k)) with SSW = Σ count·variance, p from the F survival. A count below 2 gives NaN fields; a NaN degrees of freedom gives a NaN p-value without building a distribution, except that Welch's t with a zero standard error takes PValue 0 when the means differ (the statistic is ±∞). PValue's relative error is at most 1e-12 where it is at least 1e-300 (from the cdf bounds).

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): t, z, variance-ratio and one-way ANOVA tests`.

### Task 7: Exact and contingency-table tests

**Files:**
- Modify: `src/Ranvier.Statistics/HypothesisTest.fs`, `tests/Ranvier.Science.Tests/HypothesisTests.fs`, `tests/reference/special.py` (section `counts`)

**Interfaces:**
- Consumes: Task 6's `Alternative = TwoSided | Less | Greater`, `TestResult = { Statistic; DegreesOfFreedom; DenominatorDegreesOfFreedom; PValue }` (NaN where a field does not apply) and the internal `PValue.continuous : Alternative -> Continuous -> statistic: float -> float` (Less: cdf; Greater: survival; TwoSided: min 1 (2 · min cdf survival); NaN statistic → NaN); `Discrete.binomial`, `Discrete.hypergeometric`, `mass`, `cdf`, `survival` (Task 3); `Continuous.chiSquared` (Task 2).
- Produces:

```fsharp
module HypothesisTest =
    val binomial : Alternative -> successes: int -> trials: int -> p0: float -> TestResult   // exact
    val chiSquaredGoodnessOfFit : observed: float[] -> expected: float[] -> TestResult
    val chiSquaredIndependence : table: float[][] -> TestResult                            // no continuity correction
    val fisherExact : Alternative -> a: int -> b: int -> c: int -> d: int -> TestResult    // 2 × 2, [[a, b], [c, d]]
```

- [ ] **Step 0: Confirm R's two-sided rule** (spec §7.4 ⚠️): read R's `src/library/stats/R/binom.test.R` and `fisher.test.R` (r-source on GitHub) and record the URL in the doc comments. The rule this plan expects: the two-sided p-value sums the probabilities of every outcome whose probability is at most the observed one times 1 + 1e-7, capped at 1. If the source differs, follow the source and say so in the commit message; if it cannot be read, keep this rule and label it *general knowledge* in the doc comment.

- [ ] **Step 1: Extend the script**, section `counts`, exact in `fractions` (the slack as `Fraction(1) + Fraction(1, 10**7)`):
  - binomial (7, 20, 0.5) and (3, 20, 0.3), three alternatives each; (0, 10, 0.2) and (10, 10, 0.2) two-sided.
  - goodness of fit, Mendel's F₂ seed counts (315, 108, 101, 32) against 9 : 3 : 3 : 1 (Mendel 1866, as tabulated in any genetics text; *general knowledge*): statistic exact, p from the χ² survival with 3 degrees of freedom.
  - independence: `[[20; 30; 50]; [30; 30; 40]]` and `[[10; 20; 30]; [20; 20; 20]; [30; 20; 10]]`.
  - Fisher: `[[10; 2]; [3; 15]]`, three alternatives.

- [ ] **Step 2: Write the failing tests**, appended to `"HypothesisTest"`:
  - `exact binomial against exact fractions`: every row within relative 1e-12; Statistic is the successes as a float; degrees-of-freedom fields NaN.
  - `the binomial two-sided rule counts tied outcomes`: (7, 20, 0.5) two-sided equals `2 * cdf (binomial 20 0.5) 7` within 1e-15 relative (the symmetric masses tie in exact arithmetic).
  - `binomial at p0 of 0 and 1`: p0 = 0 gives PValue 1 at 0 successes and 0 otherwise; p0 = 1 the same at n.
  - `goodness of fit against Mendel's counts`: Statistic within relative 1e-13 of 196/417 (exact, by `fractions`: Σ(o − e)²/e with e = 556 · (9, 3, 3, 1)/16), DegreesOfFreedom 3, PValue against the script; `expected = [| 9.0; 3.0; 3.0; 1.0 |]` and the expected counts `[| 312.75; 104.25; 104.25; 34.75 |]` give equal results within 1e-15.
  - `independence against the reference`: statistic and p within relative 1e-12; for the 2 × 3 table, PValue also within 1e-13 of `exp (-statistic / 2.0)` (the χ² survival with 2 degrees of freedom, closed form); DegreesOfFreedom (r − 1)(c − 1).
  - `Fisher's tea-tasting table`: `fisherExact Greater 3 1 1 3` PValue 17/70, `Less` 69/70, `TwoSided` 34/70 (Fisher, *The Design of Experiments*, 1935, chapter 2; the hypergeometric masses 1, 16, 36, 16, 1 over 70 are exact), Statistic 9.
  - `Fisher against exact fractions`: the `[[10; 2]; [3; 15]]` rows within relative 1e-12.
  - `count arguments out of range raise`: successes < 0 or > trials, p0 outside [0, 1]; observed and expected of different lengths or fewer than 2 cells; a negative observed count; an expected value ≤ 0; a table with fewer than 2 rows or columns, ragged rows or a negative entry; a negative Fisher cell (`ArgumentException` or `ArgumentOutOfRangeException`).
  - `a zero row of a table gives NaN fields`: `chiSquaredIndependence [| [| 0.0; 0.0 |]; [| 3.0; 4.0 |] |]`.

- [ ] **Step 3: Run** `-- --filter "HypothesisTest"`. Expected: the new tests fail to compile.

- [ ] **Step 4: Implement.** Decisions, each in the doc comment: `binomial` Less P(X ≤ x), Greater P(X ≥ x), TwoSided by the confirmed rule. `chiSquaredGoodnessOfFit` rescales `expected` to the observed total, χ² = Σ(o − e)²/e, k − 1 degrees of freedom. `chiSquaredIndependence` e = row · column / total, (r − 1)(c − 1) degrees of freedom, a zero row or column total gives NaN fields. `fisherExact`: X ~ Hypergeometric(a + b + c + d, a + b, a + c) at a; Less P(X ≤ a), Greater P(X ≥ a), TwoSided by the confirmed rule; Statistic the sample odds ratio ad/(bc) (+∞ when bc = 0 < ad, NaN when both are 0). PValue's bound as in Task 6.

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): exact binomial, chi-squared and Fisher's exact tests`.

### Task 8: Rank and Kolmogorov–Smirnov tests

**Files:**
- Modify: `src/Ranvier.Statistics/HypothesisTest.fs`, `tests/Ranvier.Science.Tests/HypothesisTests.fs`, `tests/reference/special.py` (section `ranks`)

**Interfaces:**
- Consumes: Task 6's `Alternative = TwoSided | Less | Greater`, `TestResult = { Statistic; DegreesOfFreedom; DenominatorDegreesOfFreedom; PValue }` (NaN where a field does not apply) and the internal `PValue.continuous : Alternative -> Continuous -> statistic: float -> float` (Less: cdf; Greater: survival; TwoSided: min 1 (2 · min cdf survival); NaN statistic → NaN); `Describe.ranks` (Task 4); `Continuous.normal`.
- Produces:

```fsharp
module HypothesisTest =
    val mannWhitney : Alternative -> float[] -> float[] -> TestResult             // normal approximation
    val wilcoxonSignedRank : Alternative -> differences: float[] -> TestResult    // normal approximation
    val kolmogorovSmirnov : cdf: (float -> float) -> float[] -> TestResult        // asymptotic, two-sided
    val kolmogorovSmirnovTwoSample : float[] -> float[] -> TestResult             // asymptotic, two-sided
```

- [ ] **Step 1: Extend the script**, section `ranks` (U, V, σ² exact in `fractions`, z and p in `decimal`):
  - Mann–Whitney: x = `[1.1; 2.2; 2.2; 3.5; 4.0]`, y = `[2.2; 3.0; 5.1; 6.3; 6.3; 7.0]`, three alternatives.
  - Wilcoxon: the sleep differences a − b, written as the literals `[-1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4]`; and d = `[1.5; -0.5; 2.0; 3.0; -1.0; 2.5; 4.0; 0.0; 3.5]`, three alternatives.
  - Q(λ) = 2Σ_{k≥1}(−1)^{k−1}e^{−2k²λ²}, summed until the terms fall below 1e-60, at the λ of the three Kolmogorov–Smirnov data sets of Step 2: √3·0.3, √(20/9)·0.6 and √3 (the first two below the series switch at 1.18, the third above it).

- [ ] **Step 2: Write the failing tests**, appended to `"HypothesisTest"`:
  - `Mann–Whitney against the reference`: Statistic (U of the first sample), PValue within relative 1e-12; degrees-of-freedom fields NaN.
  - `Wilcoxon on the sleep differences`: Statistic 0 (V, the sum of positive ranks, the zero dropped), PValue against the script; ⚠️ cross-check, used only if confirmed: R's `wilcox.test(extra ~ group, data = sleep, paired = TRUE)` prints V = 0, p-value = 0.009091 (*measured here* with Python's `statistics.NormalDist` from the tie-corrected formula: 0.0090907).
  - `Wilcoxon against the reference`: the second data set, three alternatives.
  - `one-sample Kolmogorov–Smirnov`: xs = `[| 0.1; 0.4; 0.7 |]` against `fun x -> max 0.0 (min 1.0 x)`: Statistic 0.3 within 1e-15 (by hand: max over i of i/n − F and F − (i − 1)/n), PValue Q(√3 · 0.3) from the script within relative 1e-12.
  - `two-sample Kolmogorov–Smirnov`: `[| 1.0; 2.0; 3.0; 4.0 |]` against `[| 3.0; 4.0; 5.0; 6.0; 7.0 |]`: Statistic 0.6 (by hand, at the tied value 4), PValue Q(√(20/9) · 0.6) from the script; `[| 1.0 .. 6.0 |]` against `[| 7.0 .. 12.0 |]`: Statistic 1, PValue Q(√3) from the script (the large-λ series); both within relative 1e-12. The series stays internal; these data sets are its tests.
  - `all observations tied give a NaN p-value`: `mannWhitney TwoSided [| 1.0; 1.0 |] [| 1.0; 1.0; 1.0 |]` and `wilcoxonSignedRank TwoSided [| 0.0; 0.0 |]`.
  - `NaN or empty samples give NaN fields`: a NaN in any argument of the four tests, and an empty sample, give NaN in every field and raise nothing.

- [ ] **Step 3: Run** `-- --filter "HypothesisTest"`. Expected: the new tests fail to compile.

- [ ] **Step 4: Implement.** Decisions, each in the doc comment: Mann–Whitney follows R's `wilcox.test(exact = FALSE, correct = TRUE)`: U = R_x − n_x(n_x + 1)/2 from average ranks of the pooled sample; σ² = (n_x n_y/12)((N + 1) − Σ(t³ − t)/(N(N − 1))); z = (U − n_x n_y/2 − c)/σ with c = sign·0.5 two-sided, 0.5 for Greater, −0.5 for Less; Less means the first sample's location is below the second's. Wilcoxon: zeros dropped, ranks of |d|, V the sum of positive ranks, μ = n(n + 1)/4, σ² = n(n + 1)(2n + 1)/24 − Σ(t³ − t)/48, the same correction. Kolmogorov–Smirnov follows R's `ks.test(exact = FALSE)`: D the largest gap, p = Q(√n·D) one-sample and Q(√(nm/(n + m))·D) two-sample, with Q by the alternating series for λ ≥ 1.18 and by 1 − (√(2π)/λ)Σ_{k≥1}e^{−(2k−1)²π²/(8λ²)} below (both forms of the Kolmogorov distribution, *general knowledge*). The doc comments say the p-values are approximations: R switches to exact distributions below 50 observations (rank tests) and 100 (Kolmogorov–Smirnov) (*general knowledge*), and the bound stated is relative 1e-12 to the approximating formula's value. NaN anywhere or an empty sample gives NaN fields before `Describe.ranks` is called.

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): Mann–Whitney, Wilcoxon signed-rank and Kolmogorov–Smirnov tests`.

### Task 9: The sequential test and multiple-testing adjustments

**Files:**
- Create: `src/Ranvier.Statistics/Sequential.fs`, `tests/Ranvier.Science.Tests/SequentialTestTests.fs`, `tests/Ranvier.Science.Tests/AdjustTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Sequential.fs` after `HypothesisTest.fs`), `TestFiles.props`, the Fable `Main.fs` (two roots)

**Interfaces:**
- Consumes (Plan T1, `ConfidenceSequence.fs`): `type SequenceSettings = { Alpha: float; Rho: float; BurnIn: float }`; `ConfidenceSequence.settings : alpha: float -> rho: float -> burnIn: float -> SequenceSettings`; `ConfidenceSequence.tunedFor : alpha: float -> plannedCount: float -> burnIn: float -> SequenceSettings`; `ConfidenceSequence.standardBurnIn : float` (30); `ConfidenceSequence.interval : SequenceSettings -> Moments -> Interval` (NaN bounds below `BurnIn`). Read `interval` before Step 3 and use the standard deviation it uses (today's `ofMoments` uses `sqrt (Moments.variance m)`). `Moments.add`, `count`, `mean`, `variance`.
- Produces:

```fsharp
[<NoComparison>]
type SequentialTest   // private record; structural equality

module SequentialTest =
    val create : SequenceSettings -> theta0: float -> SequentialTest
    val add : compensated: bool -> SequentialTest -> float -> SequentialTest
    val pValue : SequentialTest -> float      // 1 before the burn-in; non-increasing after
    val rejects : SequentialTest -> bool      // pValue ≤ Alpha
    val moments : SequentialTest -> Moments

module Adjust =                               // adjusted p-values in input order, capped at 1
    val bonferroni : float[] -> float[]
    val holm : float[] -> float[]
    val hochberg : float[] -> float[]
    val benjaminiHochberg : float[] -> float[]
    val benjaminiYekutieli : float[] -> float[]
```

- [ ] **Step 1: Write the failing tests.** List `"SequentialTest"`, with `fold xs = Array.fold (SequentialTest.add true) (SequentialTest.create s 0.0) xs` and `s = ConfidenceSequence.tunedFor 0.05 1000.0 ConfidenceSequence.standardBurnIn`:
  - `the p-value is 1 before the burn-in`: after 29 observations of a stream at mean 1, `pValue = 1.0` and `rejects` false.
  - `the p-value is the running minimum of the inverse mixture likelihood ratio`: over 2000 deviates (`Generator.normals 0.2 1.0 2000 (Generator.create 5)`), at every t the `pValue` is within relative 1e-12 of min(1, min_{30≤u≤t} 1/Λ_u), computed in the test from the closed form ln Λ_u = −½ ln(uρ² + 1) + ρ²(u(x̄_u − θ₀))²/(2σ²_u(uρ² + 1)) with x̄_u, σ²_u from `Moments.mean` and `Moments.variance` of the prefix (the normal mixture of Robbins; spec §7.5).
  - `the p-value never increases after the burn-in`: the same stream.
  - `rejects exactly when an interval from the burn-in on excluded theta0`: for seeds 1..50, 3000 deviates each at mean 0.15, at every t `rejects` equals whether some `ConfidenceSequence.interval s` of the moments so far, from count 30 on, excluded 0 (`0 < Lower || 0 > Upper`) (the duality, spec §7.5).
  - `a NaN observation gives a NaN p-value and no rejection`: from then on, `pValue` is NaN and `rejects` false.
  - `zero spread at theta0 keeps the p-value at 1`: 40 observations of 0.0 with θ₀ = 0.
  - `moments are the folded observations`: `count`, `mean` and `variance` of `moments (fold xs)` equal those of `Array.fold (Moments.add true) Moments.zero xs`.
  - `equal histories give equal tests`: two folds of one array are `=`.
  - `create rejects invalid settings and theta0`: `{ s with Alpha = 0.0 }`, `{ s with Rho = -1.0 }`, `{ s with BurnIn = 0.5 }` and θ₀ ∈ {NaN, ∞} raise `ArgumentOutOfRangeException`.

  List `"Adjust"`, with p = `[| 0.04; 0.001; 0.03; 0.2; 0.01 |]`; expected values by R's `p.adjust` definitions computed by hand, each element within 4 ulps (`ulps` from `Exact.fs`):
  - `Bonferroni`: `[| 0.2; 0.005; 0.15; 1.0; 0.05 |]`.
  - `Holm`: `[| 0.09; 0.005; 0.09; 0.2; 0.04 |]`.
  - `Hochberg`: `[| 0.08; 0.005; 0.08; 0.2; 0.04 |]`.
  - `Benjamini–Hochberg`: `[| 0.05; 0.005; 0.05; 0.2; 0.025 |]`.
  - `Benjamini–Yekutieli`: the Benjamini–Hochberg values times 137/60 (Σ_{i≤5} 1/i).
  - `adjusted p-values are ordered as the raw ones and never below them`: for 200 p-values from `Generator.uniform` (seed 9), each method's output is at least its input, at most 1, and non-decreasing in the raw order.
  - `p-values of 0 and 1`: `[| 0.0; 1.0; 0.5 |]` gives 0 and 1 at those positions under every method.
  - `NaN entries stay NaN and are not counted`: `bonferroni [| 0.01; nan; 0.04 |]` is `[| 0.02; nan; 0.08 |]`.
  - `the empty family`: every method returns an empty array.
  - `p-values outside [0, 1] raise`: `[| -0.1 |]` and `[| 1.5 |]` raise `ArgumentOutOfRangeException`.

- [ ] **Step 2: Run** `-- --filter "SequentialTest"` and `-- --filter "Adjust"`. Expected: compile errors.

- [ ] **Step 3: Implement `Sequential.fs`.** Decisions: `SequentialTest` is `private { Settings; Theta0; Moments; PValue }`; `create` validates the settings by rebuilding them with `ConfidenceSequence.settings s.Alpha s.Rho s.BurnIn` and checks θ₀ finite. `add` folds the observation, and from count ≥ BurnIn takes PValue ← min(PValue, exp(−ln Λ)) with ln Λ as the test writes it, the exponent taken as 0 when both the deviation and σ are 0. A NaN observation makes the moments, and so the p-value, NaN for good; `rejects` is `pValue ≤ Alpha`, false at NaN. The doc comment states the duality with `ConfidenceSequence.interval` (Johari, Pekelis and Walsh, "Always valid inference"; Waudby-Smith et al., arXiv 2103.06476; *general knowledge*), that its validity is the sequence's asymptotic one, that a paired two-sample test folds the differences, and that the record publishes a new instance on every `add`. `Adjust` follows R's `p.adjust` (*general knowledge*): Holm step-down max_{j≤i}((m − j + 1)p₍ⱼ₎), Hochberg step-up min_{j≥i}((m − j + 1)p₍ⱼ₎), Benjamini–Hochberg min_{j≥i}(m/j · p₍ⱼ₎), Benjamini–Yekutieli the same times Σ_{i≤m} 1/i, each capped at 1; m counts the non-NaN entries. Cost O(m log m).

- [ ] **Step 4: Run** both filtered lists. Expected: PASS.
- [ ] **Step 5: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 6: Commit** `feat(statistics): the sequential test and multiple-testing adjustments`.

### Task 10: Linear regression

**Files:**
- Create: `src/Ranvier.Statistics/Regression.fs`, `tests/Ranvier.Science.Tests/RegressionTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Regression.fs` after `Sequential.fs`), `tests/reference/special.py` (section `regression`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: Task 6's `Alternative = TwoSided | Less | Greater`, `TestResult = { Statistic; DegreesOfFreedom; DenominatorDegreesOfFreedom; PValue }` (NaN where a field does not apply) and the internal `PValue.continuous : Alternative -> Continuous -> statistic: float -> float` (Less: cdf; Greater: survival; TwoSided: min 1 (2 · min cdf survival); NaN statistic → NaN); `Continuous.studentT`. Plan T2 (`Ranvier.Numerics`): `type Matrix` with `Matrix.ofRows`, `Matrix.init`, `Matrix.rowCount`, `Matrix.columnCount`, `Matrix.get`, `Matrix.column`, `Matrix.transpose`, `Matrix.multiply`, `Matrix.multiplyVector`, `Matrix.identity`; `Matrix.qr : Matrix -> QrDecomposition` (rows ≥ columns, else raises); `QrDecomposition.leastSquares : QrDecomposition -> float[] -> Result<float[], string>` (Error when rank-deficient); `QrDecomposition.r : QrDecomposition -> Matrix` (upper-triangular factor R); `Matrix.inverse : Matrix -> Result<Matrix, string>`. The `Comoments` reads `count`, `meanX`, `meanY`, `varianceX`, `varianceY`, `covariance`, `slope`, `intercept` (spec §6.8; `varianceX`, `varianceY` and `covariance` with divisor n, as `Moments.variance`). Read T2's `Comoments` doc comments before Step 3 to confirm the divisors and that `count` is a `float`.
- Produces:

```fsharp
type SimpleRegression =
    { Slope: float; Intercept: float; SlopeStandardError: float; InterceptStandardError: float
      RSquared: float; ResidualStandardError: float; DegreesOfFreedom: float; Count: float }
type LinearRegression =
    { Coefficients: float[]; StandardErrors: float[]; TStatistics: float[]; PValues: float[]
      RSquared: float; AdjustedRSquared: float; ResidualStandardError: float
      DegreesOfFreedom: float; Covariance: Matrix; Residuals: float[] }

module Regression =
    val simple : Comoments -> SimpleRegression
    val linear : design: Matrix -> y: float[] -> Result<LinearRegression, string>
    val weighted : design: Matrix -> y: float[] -> weights: float[] -> Result<LinearRegression, string>
    val polynomial : degree: int -> xs: float[] -> ys: float[] -> Result<LinearRegression, string>   // ascending powers
```

- [ ] **Step 1: Extend the script**, section `regression`, exact by Gaussian elimination on XᵀX in `fractions` (square roots and t p-values in `decimal`). Data: x₁ = `[1..12]`, x₂ = `[3; 1; 4; 1; 5; 9; 2; 6; 5; 3; 5; 8]`, y = `[6; 5; 9; 7; 13; 18; 11; 15; 16; 13; 16; 22]`. Print:
  - simple regression of y on x₁ (first 10 rows): slope, intercept, both standard errors, R², residual standard error.
  - `linear` on the design (1, x₁, x₂): coefficients, standard errors, t, two-sided p, R², adjusted R², residual standard error, the 3 × 3 covariance.
  - `linear` on (x₁, x₂) without a constant: coefficients and R² about zero.
  - `weighted` on (1, x₁) with weights `[1; 2; 3; 1; 2; 3; 1; 2; 3; 1; 2; 0]`: coefficients, standard errors, R², residual standard error.

- [ ] **Step 2: Write the failing tests**, list `"Regression"`, with the script's tables and command:
  - `simple regression against exact fractions`: every field within relative 1e-12; DegreesOfFreedom 8, Count 10.
  - `simple regression equals linear regression on a constant and x`: Slope, Intercept and their standard errors within relative 1e-12 of `linear` on (1, x₁) (identity).
  - `linear regression against exact fractions`, `regression without a constant against exact fractions`, `weighted regression against exact fractions`: every field within relative 1e-12 (p-values where at least 1e-300); `Residuals` within 1e-12 absolute of y − Xβ; the weighted DegreesOfFreedom is 9 (11 positive weights − 2).
  - `a constant column of 2 counts as an intercept`: the design (2, x₁, x₂) gives the same RSquared as (1, x₁, x₂).
  - `polynomial regression recovers exact coefficients`: `polynomial 2 [| 0.0 .. 7.0 |] ys` with ys = 1 + 2x − 0.5x²: Coefficients within 1e-12 of `[| 1.0; 2.0; -0.5 |]`, every residual within 1e-12 of 0.
  - `a rank-deficient design is an Error`: a design with x₁ twice; `polynomial 3` over two distinct x values repeated.
  - `a non-finite input is an Error`: a NaN in y, an infinity in the design, a NaN weight.
  - `constant x gives a NaN slope`: `Regression.simple` of comoments over x = 3 everywhere.
  - `as many rows as columns gives NaN standard errors`: a 2 × 2 design: Coefficients within 1e-14 of the exact solution, StandardErrors and PValues NaN (not +∞), DegreesOfFreedom 0.
  - `arguments out of range raise`: y of the wrong length, fewer rows than columns, weights of the wrong length or negative, `polynomial -1`, xs and ys of different lengths (`ArgumentException` or `ArgumentOutOfRangeException`).

- [ ] **Step 3: Run** `-- --filter "Regression"`. Expected: compile error.

- [ ] **Step 4: Implement** the records and `Regression.simple`, `linear`, `weighted`, `polynomial` in `Regression.fs`. Decisions, each in the doc comment: `simple` from the comoments: slope cov/var_x, residual variance n(var_y − cov²/var_x)/(n − 2), slope standard error s/√(n·var_x), intercept standard error s·√(1/n + x̄²/(n·var_x)), R² = cov²/(var_x·var_y). `linear` solves by `Matrix.qr` and `QrDecomposition.leastSquares`, passing its `Error` on; σ² = eᵀe/(n − p), NaN when n = p (no division by 0); the covariance σ² R⁻¹R⁻ᵀ, with R from `QrDecomposition.r` of the same decomposition and R⁻¹ by back-substitution or `Matrix.inverse` (an `Error` if R is singular, which `leastSquares` has already reported as rank-deficient), computed in one private helper of `Regression.fs`, `unscaledCovariance : r: Matrix -> Result<Matrix, string>` (R⁻¹R⁻ᵀ), which Task 11's `curve` calls too; two-sided p-values from Student t with n − p degrees of freedom. A constant column is one whose entries all equal its first entry, which is non-zero; with one, R² is taken about the mean, otherwise about zero; adjusted R² = 1 − (1 − R²)(n − c)/(n − p), c = 1 with a constant column, else 0 (R's `summary.lm`, *general knowledge*). `weighted` scales each row and observation by √w, reports `Residuals` y − Xβ unscaled, counts only positive weights in n, and uses the weighted mean for R²; fewer positive weights than columns is an `Error`. A NaN or infinite input is an `Error` naming it. `polynomial` builds the raw Vandermonde design; its doc comment says its conditioning grows with the degree. Coefficients have relative error at most 1e-12 over well-conditioned designs (the tests' integer designs of up to 3 columns).

- [ ] **Step 5: Run** the filtered list. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): simple, multiple, weighted and polynomial regression`.

### Task 11: Curve fitting and correlation

**Files:**
- Create: `tests/Ranvier.Science.Tests/CorrelationTests.fs`
- Modify: `src/Ranvier.Statistics/Regression.fs`, `tests/Ranvier.Science.Tests/RegressionTests.fs`, `tests/reference/special.py` (section `correlation`), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: Task 10's `Regression.linear : design: Matrix -> y: float[] -> Result<LinearRegression, string>` (fields `Coefficients`, `StandardErrors`, `DegreesOfFreedom` among others) in `Regression.fs`, where this task adds its code; Task 6's `Alternative = TwoSided | Less | Greater`, `TestResult = { Statistic; DegreesOfFreedom; DenominatorDegreesOfFreedom; PValue }` (NaN where a field does not apply) and the internal `PValue.continuous : Alternative -> Continuous -> statistic: float -> float` (Less: cdf; Greater: survival; TwoSided: min 1 (2 · min cdf survival); NaN statistic → NaN); `Describe.ranks` (Task 4); `Continuous.studentT`, `Continuous.normal`. Plan T2: `LeastSquaresOptions.standard`; `LeastSquares.levenbergMarquardt : LeastSquaresOptions -> residuals: (float[] -> float[]) -> initial: float[] -> LeastSquaresResult` with `LeastSquaresResult = { Parameters: float[]; Residuals: float[]; Jacobian: Matrix; Cost: float; Iterations: int; Converged: bool }` (`Cost` is half the sum of squared residuals; the Jacobian is by forward differences); `Matrix.qr`, `QrDecomposition.r`, `Matrix.inverse`; `Comoments.zero`, `Comoments.add : compensated: bool -> Comoments -> float -> float -> Comoments`, `Comoments.correlation`.
- Produces:

```fsharp
type CurveFit = { Parameters: float[]; StandardErrors: float[]; Covariance: Matrix; ResidualStandardError: float
                  DegreesOfFreedom: float; Iterations: int; Converged: bool }
module Regression =
    val curve : model: (float[] -> float -> float) -> initial: float[] -> xs: float[] -> ys: float[] -> CurveFit
module Correlation =
    val pearson : float[] -> float[] -> float
    val spearman : float[] -> float[] -> float
    val kendall : float[] -> float[] -> float          // tau-b
    val pearsonTest : Alternative -> float[] -> float[] -> TestResult
    val spearmanTest : Alternative -> float[] -> float[] -> TestResult
    val kendallTest : Alternative -> float[] -> float[] -> TestResult
```

- [ ] **Step 1: Extend the script**, section `correlation` (exact sums in `fractions`, roots and p-values in `decimal`): for x = `[1; 2; 3; 4; 5; 6; 7; 8]`, y = `[2; 1; 4; 3; 7; 8; 6; 5]`: Pearson r, its t and p for three alternatives; Kendall's tau and z; and for the tied pair x = `[1; 2; 2; 3; 4; 5; 5; 6]`, y = `[1; 3; 2; 2; 5; 4; 6; 6]`: Spearman r and Kendall tau-b, with R's tie-corrected variance of S (`cor.test(method = "kendall", exact = FALSE)`).

- [ ] **Step 2: Write the failing tests.** Appended to `"Regression"`:
  - `curve fitting recovers exact parameters`: `curve (fun p x -> p[0] * exp (-p[1] * x)) [| 1.0; 1.0 |] xs ys` with xs = `0.0, 0.25 .. 5.0` and ys = 2.5·e^{−1.3x}: Parameters within relative 1e-8 of `[| 2.5; 1.3 |]`, Converged true, ResidualStandardError below 1e-8.
  - `a model linear in its parameters gives the linear regression`: `curve (fun p x -> p[0] + p[1] * x)` on x = `[1.0 .. 10.0]`, y = `[| 6.0; 5.0; 9.0; 7.0; 13.0; 18.0; 11.0; 15.0; 16.0; 13.0 |]`, against `Regression.linear` on the design (1, x): Parameters within relative 1e-8 of `linear`'s Coefficients, StandardErrors within relative 1e-6 (forward-difference Jacobian), DegreesOfFreedom n − 2 (identity).
  - `curve arguments out of range raise`: xs and ys of different lengths; fewer points than parameters.

  List `"Correlation"`, with the script's tables:
  - `Pearson, Spearman and Kendall against exact fractions`: each value within relative 1e-12; `pearsonTest` and `spearmanTest` Statistic (t, n − 2 degrees of freedom) and PValue within relative 1e-12; `kendallTest` Statistic (z) and PValue.
  - `Spearman with ties against the reference`: on the tied pair x = `[| 1.0; 2.0; 2.0; 3.0; 4.0; 5.0; 5.0; 6.0 |]`, y = `[| 1.0; 3.0; 2.0; 2.0; 5.0; 4.0; 6.0; 6.0 |]`, `spearman x y` within relative 1e-12 of the Spearman r printed by `python -I tests/reference/special.py correlation` (the script computes it from average ranks in exact `fractions`, independently of `Describe.ranks`; the command is written beside the literal); and `spearman` of x = `[| 1.0 .. 6.0 |]` and y = x³ (monotone, non-linear) within 1e-14 of 1.0 (by definition: identical ranks).
  - `Kendall tau-b matches a direct count`: on the same points, `kendall` within 1e-14 of (C − D)/√((n₀ − n₁)(n₀ − n₂)) counted over all pairs in the test (O(n²), the definition); and `kendall [|1..5|] [|3;1;2;5;4|] = 0.4` (7 concordant, 3 discordant pairs, by hand).
  - `degenerate inputs`: a constant series gives NaN from all three and NaN fields from the tests; fewer than 2 points gives NaN; a NaN gives NaN; lengths that differ raise `ArgumentException`.

- [ ] **Step 3: Run** `-- --filter "Regression"` and `-- --filter "Correlation"`. Expected: compile errors.

- [ ] **Step 4: Implement.** Decisions, each in the doc comment: `curve` runs `LeastSquares.levenbergMarquardt LeastSquaresOptions.standard` on residuals y_i − model p x_i; ResidualStandardError √(2·Cost/(n − p)); Covariance σ²(RᵀR)⁻¹ = σ² R⁻¹R⁻ᵀ from `Matrix.qr` of the result's Jacobian and `QrDecomposition.r`, through Task 10's private helper `unscaledCovariance` (no Cholesky of JᵀJ, which squares the Jacobian's condition number), NaN entries (and NaN StandardErrors) when the helper gives `Error` or the Jacobian has fewer rows than columns; Iterations and Converged from the result. `pearson` is `Comoments.correlation` of the fold; `spearman` the Pearson of `Describe.ranks`; `kendall` tau-b by Knight's O(n log n) merge-sort algorithm (Knight, "A computer method for calculating Kendall's tau with ungrouped data", *JASA* 61, 1966; *general knowledge*). `pearsonTest` and `spearmanTest`: t = r√((n − 2)/(1 − r²)) with n − 2 degrees of freedom; `kendallTest`: z = S/√var(S) with R's tie-corrected variance, no continuity correction. A NaN is checked before `Describe.ranks`, which raises on NaN, so these return NaN.

- [ ] **Step 5: Run** both filtered lists. Expected: PASS.
- [ ] **Step 6: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 7: Commit** `feat(statistics): curve fitting and Pearson, Spearman and Kendall correlation`.

### Task 12: The bootstrap and kernel density estimation

**Files:**
- Create: `src/Ranvier.Statistics/Resampling.fs`, `tests/Ranvier.Science.Tests/ResamplingTests.fs`
- Modify: `Ranvier.Statistics.fsproj` (`Resampling.fs` last), `TestFiles.props`, the Fable `Main.fs`

**Interfaces:**
- Consumes: `Interval`; `Describe.quantile`, `Describe.interquartileRange` (Task 4); Task 1's `Check.*`; Plan T2's `Generator.integer : lower: int -> upperExclusive: int -> Generator -> struct (int * Generator)`; in tests `Integrate.adaptive`.
- Produces:

```fsharp
module Bootstrap =
    val percentile : alpha: float -> resamples: int -> statistic: (float[] -> float) -> float[] -> Generator
                     -> struct (Interval * Generator)
    val standardError : resamples: int -> statistic: (float[] -> float) -> float[] -> Generator
                        -> struct (float * Generator)
module Density =
    val silvermanBandwidth : float[] -> float
    val scottBandwidth : float[] -> float
    val gaussianKernel : bandwidth: float -> float[] -> (float -> float)
```

- [ ] **Step 1: Write the failing tests**, list `"Resampling"`; xs = `Generator.normals 10.0 2.0 200 (Generator.create 3)` and se = √(population variance/200):
  - `the bootstrap standard error of the mean`: `standardError 2000 Array.average xs (Generator.create 4)` within 10% of se (the bootstrap's standard error of the mean is the plug-in √(σ̂²/n); the Monte Carlo spread at 2000 resamples is about 1.6%, *general knowledge*).
  - `the percentile interval of the mean`: `percentile 0.05 2000 Array.average xs g`: each bound within 0.35·se of mean ∓ 1.96·se (about 5 Monte Carlo standard errors of a 2.5% quantile).
  - `the same generator gives the same result`: two calls with one generator give equal intervals and equal returned generators, which differ from the input generator.
  - `the statistic sees resamples of the sample`: a statistic that records its argument sees arrays of length 200 whose elements all belong to xs, `resamples` times.
  - `a constant sample gives a zero-width interval at the constant` and a standard error of 0.
  - `bootstrap arguments out of range raise`: resamples 0 (`percentile`) and 1 (`standardError`); α ∈ {0, 1}; an empty sample (`ArgumentException`).
  - `Silverman and Scott bandwidths`: for `[| 1.0 .. 10.0 |]`, `silvermanBandwidth` within 1e-14 of `0.9 * min (sqrt (55.0 / 6.0)) (4.5 / 1.34) * 10.0 ** -0.2` and `scottBandwidth` of the same with 1.06 (R's `bw.nrd0` and `bw.nrd`, closed forms).
  - `Silverman's fallbacks at zero spread`: `[| 5.0; 5.0; 5.0; 5.0 |]` gives `0.9 * 5.0 * 4.0 ** -0.2`; four zeros give `0.9 * 4.0 ** -0.2` (R's `bw.nrd0`); `scottBandwidth` of either is 0.
  - `the estimate of one point is a normal density`: `gaussianKernel 0.5 [| 2.0 |]` at x ∈ {2, 2.7, −1} within 1e-14 of `exp (-(((x - 2.0) / 0.5) ** 2.0) / 2.0) / (0.5 * sqrt (2.0 * Math.PI))` (the parentheses matter: F#'s prefix minus binds before `**`, so `-((x - 2.0) / 0.5) ** 2.0` is +u²; *measured here* by the pre-flight scan with `dotnet fsi`: the unparenthesised text gives 2.66446 at x = 2.7 against 0.375311).
  - `the estimate integrates to 1`: `Integrate.adaptive 1e-12 1e-10 f (min − 10h) (max + 10h)` for xs with Silverman's h: Value within 1e-9 of 1, Converged.
  - `the estimate copies its sample`: writing to xs after `gaussianKernel` leaves f's values unchanged.
  - `density arguments out of range raise`: bandwidth ∈ {0, −1, NaN, ∞}, an empty sample; a NaN in the data gives NaN bandwidths.

- [ ] **Step 2: Run** `-- --filter "Resampling"`. Expected: compile error.

- [ ] **Step 3: Implement `Resampling.fs`.** Decisions, each in the doc comment: a resample draws n indices with `Generator.integer 0 n`; `percentile` takes the type-7 quantiles at α/2 and 1 − α/2 of the resampled statistics; `standardError` their standard deviation with divisor n − 1; cost `resamples` calls of the statistic plus O(resamples · n). `silvermanBandwidth` is R's `bw.nrd0`: 0.9·min(s, IQR/1.34)·n^{−1/5}, falling back to s, then |x₁|, then 1 when the minimum is 0; `scottBandwidth` is R's `bw.nrd`: 1.06·min(s, IQR/1.34)·n^{−1/5} (*general knowledge*). `gaussianKernel` copies the sample and returns f(x) = (1/(nh))Σφ((x − xᵢ)/h), O(n) per evaluation.

- [ ] **Step 4: Run** the filtered list. Expected: PASS.
- [ ] **Step 5: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 6: Commit** `feat(statistics): the bootstrap and kernel density estimation`.

### Task 13: The accuracy record

**Files:**
- Create: `docs/measurements/accuracy.md` (or extend it, if an earlier plan created it)

**Interfaces:**
- Consumes: the doc comments of `src/Ranvier.Numerics`, `src/Ranvier.Quantities` and `src/Ranvier.Statistics` and the tests that pin them.
- Produces: `docs/measurements/accuracy.md`, linked from the README in Task 15.

- [ ] **Step 1: List the stated bounds.** Search `src/**/*.fs` for `error at most` and for `round trip` in doc comments (`rg -n "error at most|round trip" src`). Record each match: package, kernel (module and function), bound E, domain D.
- [ ] **Step 2: Find the pinning test for each.** For each bound, find the test that asserts it (`rg` the kernel's name under `tests/Ranvier.Science.Tests`) and its reference source: a closed form or identity (name it), a section of a script under `tests/reference/` (with the command), or a published table (name and edition).
- [ ] **Step 3: Write `docs/measurements/accuracy.md`**: a title; one paragraph saying that every bound below is pinned by a test that runs on .NET and, through Fickl, on JavaScript, *measured here* by the two suite commands (written out), and that a kernel using `exp`, `log` or trigonometric functions meets its bound on each target separately (spec §5.3); then one table per package with the columns Kernel, Bound, Domain, Test (file and name), Reference. The spec's target table (§5.1) is cited for the targets, not copied.
- [ ] **Step 4: Check coverage.** Expected: one row per match of Step 1 and none without a test. A bound with no pinning test in `Ranvier.Statistics` gets its test now, in that task's test file; one in another package is reported to the coordinator in the hand-off, with file and line, and listed in the record as "not pinned".
- [ ] **Step 5: Run the gate** if Step 4 added a test: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered.
- [ ] **Step 6: Commit** `docs: the accuracy record of every approximating kernel`.

### Task 14: The guide

**Files:**
- Create: `docs/GUIDE.md`, `tests/Ranvier.Science.Tests/GuideTests.fs`, `tests/guide-snippets.py`
- Modify: `TestFiles.props` (`GuideTests.fs` last), the Fable `Main.fs`

**Interfaces:**
- Consumes, by the spec's names: Plan T1: `UnitCatalogue.si`, `UnitInfo.parse : UnitCatalogue -> text: string -> Result<UnitInfo, string>`, `UnitInfo.convert : from: UnitInfo -> target: UnitInfo -> float -> Result<float, string>`, `UnitInfo.symbol`; `Model.create`; `Quantity.inputWithUncertainty model id name unit initial uncertainty : Input<'u>`; `Quantity.formula : Model -> id: string -> name: string -> text: string -> Result<Entry, string>`; `Entry.Read`, `Entry.Uncertainty : unit -> float`, `Entry.Measured : unit -> Uncertain`; `Uncertain.standardUncertainty`; `Formula.parse`; `Formula.monteCarlo : draws: int -> seed: int -> coverage: float -> Model -> Expr -> (unit -> MonteCarloResult)`; `Constants.declare : Model -> PhysicalConstants` with fields `ElectronMass`, `SpeedOfLight`; `Units.metre`, `Units.second` (0.1.0). Plan T2: `Run.ode : OdeOptions -> clock: (unit -> float) -> start: (unit -> float[]) -> rhs: (float -> float[] -> float[]) -> Run<OdeState>` with `OdeState.Time`, `OdeState.State`; `OdeOptions.standard`; `Fourier.powerSpectrum`, `Fourier.frequencies`; `Matrix.ofRows`, `Matrix.solve`, `Matrix.qr`, `QrDecomposition.leastSquares`; `Window.moments : compensated: bool -> capacity: int -> Window<float, Moments>`, `Window.push`, `Window.value`, `Window.items`; `Comoments.add`, `Comoments.zero`; `Aggregate.comomentsBy : FloatPolicy -> ('V -> Comoments) -> Projection<'K, 'V> -> Maintained<Comoments>`. This plan: `Regression.curve`, `Regression.simple`, `HypothesisTest.welchT`, `HypothesisTest.oneSampleT`, `Adjust.holm`, `Adjust.benjaminiHochberg`, `SequentialTest.*`, `ConfidenceSequence.tunedFor`, `standardBurnIn`. Core: `createSignal`, `createKeyedCollection`, `createStreamFold`. Test support: `withGraph`, `ManualStream` (`Support.fs`).
- Produces: `docs/GUIDE.md`; `GuideTests.fs` with list `"Guide"`, one test per section; `tests/guide-snippets.py`.

- [ ] **Step 1: Write `GuideTests.fs`**, opening `Ranvier`, `Ranvier.Numerics`, `Ranvier.Quantities`, `Ranvier.Statistics`. Each test body is a snippet: short, self-contained, using the public API and the support helpers, with `Expect` lines as its checks. A snippet that reads a node runs inside `withGraph`; a stream is started by a read of `TryValue` before the first push. The twelve tests (spec §9's list):
  - `fit a curve to data`: `Regression.curve` of p₀e^{−p₁x} to exact data at (2.5, 1.3); expects the parameters within relative 1e-8 and `Converged`.
  - `integrate an ODE in a Run`: `Run.ode OdeOptions.standard` of y′ = −y from `[| 1.0 |]` on a clock signal moved from 0 to 1; expects `State.[0]` within `100.0 * (OdeOptions.standard.AbsoluteTolerance + OdeOptions.standard.RelativeTolerance)` of e^{−1} and `Time` 1.
  - `propagate uncertainty through a formula`: a pendulum model with inputs L = 0.9945 ± 0.0005 m and T = 2.001 ± 0.002 s and the formula entry `"4 * pi^2 * L / T^2"`; expects the first-order uncertainty within relative 1e-12 of g·√((u_L/L)² + (2u_T/T)²), and the standard deviation of `Formula.monteCarlo 20000 1 0.95` within 5% of it.
  - `parse and convert units`: `UnitInfo.parse UnitCatalogue.si "km/h"` and `"m/s"`; expects `UnitInfo.symbol` "km/h" and 36 km/h converted to within 1e-15 relative of 10 m/s; the parse of `"m/"` is an `Error`.
  - `use the physical constants in a formula`: `Constants.declare`, then the formula entry `"m_e * c^2"`; expects its value within 1e-15 relative of m_e·c² from the record's fields, and its relative uncertainty within 1e-12 of m_e's (c is exact).
  - `test two samples`: the sleep data, a = `[0.7; -1.6; -0.2; -1.2; -0.1; 3.4; 3.7; 0.8; 0.0; 2.0]` and b = `[1.9; 0.8; 1.1; 0.1; -0.1; 4.4; 5.5; 1.6; 4.6; 3.4]`, and the paired differences `[-1.2; -2.4; -1.3; -1.3; 0.0; -1.0; -1.8; -0.8; -4.6; -1.4]`; expects `HypothesisTest.welchT TwoSided` p above 0.05 and `HypothesisTest.oneSampleT TwoSided 0.0` of the paired differences below 0.01 (pairing matters).
  - `adjust a family of p-values`: `[| 0.04; 0.001; 0.03; 0.2; 0.01 |]`; expects `Adjust.holm` to give `[| 0.09; 0.005; 0.09; 0.2; 0.04 |]` and `Adjust.benjaminiHochberg` `[| 0.05; 0.005; 0.05; 0.2; 0.025 |]`, each within 4 ulps (R's `p.adjust` definitions, by hand).
  - `monitor a stream with a sequential test`: `createStreamFold (SequentialTest.create (ConfidenceSequence.tunedFor 0.05 1000.0 ConfidenceSequence.standardBurnIn) 0.0) (SequentialTest.add true)` over a `ManualStream<float>` fed `Generator.normals 0.3 1.0 2000 (Generator.create 7)`; expects `rejects` true and `pValue` at most 0.05.
  - `keep a regression current over a projection`: a keyed collection of `struct (k, x, y)` rows with y = 1 + 2x, `Aggregate.comomentsBy FloatPolicy.standard` and `Regression.simple` of its value; expects slope 2 and intercept 1 within 1e-12, then after one row edit a fit within 1e-12 of `Regression.simple` of a fresh fold of the rows.
  - `take the spectrum of a signal`: 64 samples of sin(2π·5t) at 64 Hz; expects the largest bin of `Fourier.powerSpectrum` at index 5 and `(Fourier.frequencies 64 (1.0 / 64.0)).[5]` within 1e-12 of 5.
  - `solve a linear system and a least-squares problem`: `Matrix.solve` of [[4, 1], [1, 3]] x = [1, 2]; expects x within 1e-15 of [1/11, 7/11]; `QrDecomposition.leastSquares` of the line through (0, 1), (1, 3), (2, 2), (3, 5); expects [1.1, 1.1] within 1e-14 (the normal equations, by hand).
  - `window a stream`: `createStreamFold (Window.moments true 3) Window.push` over a `ManualStream<float>` fed 1..5; expects the mean of `Window.value` within 1e-12 of 4 and `Seq.toList (Window.items w) = [ 3.0; 4.0; 5.0 ]`.
- [ ] **Step 2: Run** `-- --filter "Guide"` on .NET, then the JavaScript suite. Expected: 12 tests pass on both targets. A failure is fixed in the snippet, or reported to the coordinator when it shows a defect in an earlier plan's part.
- [ ] **Step 3: Write `docs/GUIDE.md`**: a title and an introduction (what the three packages hold; the opens every snippet assumes, named in prose; `withGraph` and `ManualStream` described in prose with a link to `tests/Ranvier.Science.Tests/Support.fs`); then one `##` section per test, in the order above: two to four sentences of context (what the task is, which functions do it, what the graph does with it where it does), a line `Test: "<name>" in tests/Ranvier.Science.Tests/GuideTests.fs`, and one fenced `fsharp` block holding the test's body, dedented. The only code blocks are the snippets.
- [ ] **Step 4: Write `tests/guide-snippets.py`** (standard library only): for each `Test: "<name>"` line of `docs/GUIDE.md`, the following `fsharp` block must equal, after dedenting and stripping trailing spaces, the body of `test "<name>" {` … `}` in `GuideTests.fs`; it exits 1 and names each mismatch, and exits 0 printing `12 snippets match`. Run `python -I tests/guide-snippets.py`. Expected: `12 snippets match`.
- [ ] **Step 5: Run the gate**: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests`; `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then `npx mocha dist/tests/Main.fs.js`; all unfiltered. Expected: every test passes on both targets.
- [ ] **Step 6: Commit** `docs: the guide, twelve tasks each pinned by a test`.

### Task 15: Release 0.2.0

**Files:**
- Modify: `src/Directory.Build.props` (`Version`, `PackageReleaseNotes`, the FSharp.Core comment at line 7), `src/Ranvier.Numerics/Ranvier.Numerics.fsproj`, `src/Ranvier.Quantities/Ranvier.Quantities.fsproj`, `src/Ranvier.Statistics/Ranvier.Statistics.fsproj` (`Description`, `PackageTags`), `README.md`, `docs/measurements/gate.md` (a row appended)

**Interfaces:**
- Consumes: the branch as Tasks 1 to 14 and Plans T1 and T2 left it.
- Produces: packages at 0.2.0 that build, test and pack; the coordinator's steps below.

- [ ] **Step 1: Check the spec's goals** (§2, goals 1 to 6) against the branch and write the outcome per goal in the hand-off report, not in a file: goal 3's exact cases in `CrossTargetTests.fs` (one per kernel it names), goal 4's `docs/FINDINGS.md` §3 dispositions, goal 6's `PackageReference Include="Ranvier" Version="0.2.1"`. A missing item is reported to the coordinator with its location; this task does not build it.
- [ ] **Step 2: Set the version and notes** in `src/Directory.Build.props`: `<Version>0.2.0</Version>`; the FSharp.Core comment names Ranvier 0.2.1; `PackageReleaseNotes`: "0.2.0, the toolkit. Numerics: special functions, roots, minimisation and least squares, quadrature and differentiation, ODE integration, interpolation, dense linear algebra, the Fourier transform, streaming and windowed accumulators, random variates. Quantities: an SI unit catalogue with prefixes, parsing and conversion, CODATA 2022 physical constants, first-order and Monte Carlo propagation of uncertainty, formula entries, runs with an ODE integrator. Statistics: distributions, descriptive statistics, intervals, hypothesis tests, a sequential test, multiple-testing adjustments, regression, correlation, curve fitting, the bootstrap and density estimation. Breaking changes from 0.1.0 are listed in docs/FINDINGS.md §3. Built on Ranvier 0.2.1. The interfaces will change before 1.0." Before writing it, confirm each named area exists on the branch (`git diff --stat f329053 -- src`).
- [ ] **Step 3: Set descriptions and tags.** `Ranvier.Numerics`: "Numerics for the Ranvier reactive graph: special functions, root finding, minimisation and least squares, quadrature, ODE integration, interpolation, dense linear algebra, the Fourier transform, compensated sums, moments, streaming and windowed accumulators, maintained aggregates, tolerances and random variates." Tags `numerics;special-functions;linear-algebra;least-squares;root-finding;quadrature;ode;interpolation;fft;moments;aggregates;random;ranvier;reactive;fsharp`. `Ranvier.Quantities`: "Units, quantities and models on the Ranvier reactive graph: an SI unit catalogue with prefixes, parsing and conversion, physical constants with their uncertainty, first-order and Monte Carlo propagation of uncertainty, a one-line formula notation whose results are model entries, and runs: simulations advanced with a clock, with an ODE integrator." Tags `units;units-of-measure;quantities;si;physical-constants;uncertainty;formula;simulation;ranvier;reactive;fsharp`. `Ranvier.Statistics`: "Statistics for the Ranvier reactive graph: continuous and discrete distributions, descriptive statistics, confidence intervals, hypothesis tests, a sequential test with its confidence sequence, multiple-testing adjustments, regression, correlation, curve fitting, the bootstrap and kernel density estimation." Tags `statistics;distributions;hypothesis-testing;confidence-interval;confidence-sequence;sequential-testing;regression;correlation;bootstrap;kernel-density;ranvier;fsharp`. (`Directory.Build.targets` appends the Fable tags.)
- [ ] **Step 4: Rewrite the README's opening and package sections** for 0.2.0: the first paragraph (packages built on the reactive graph, 0.2.0 on `Ranvier` 0.2.1, interfaces that will change before 1.0); "Packages" by area, one short list per package naming its modules, with `dotnet add package … --version 0.2.0`; links (absolute `https://github.com/ranvier-org/Ranvier.Science/blob/main/…`, as the README's other links are, since nuget.org shows the packed copy) to `docs/GUIDE.md` and `docs/measurements/accuracy.md`; "What it holds" and "Records" list the guide and the accuracy record and keep the AstroTracked note. The Tests, Monitor and Benchmarks sections stay as they are unless a command in them changed.
- [ ] **Step 5: Run the gate and time it.** `dotnet build -c Release` (0 warnings, 0 errors), both suites unfiltered. Record the four wall-clock times (*measured here*, with the commands) and the test counts in the commit message and the hand-off, and append them as a dated row "After Plan T3" to `docs/measurements/gate.md` (the one gate record, created by Plan T1, Task 15, and appended to by Plan T2, Task 20; spec §11); the baseline is the brief's 2026-11-20 gate (build 9 s incremental, .NET suite 2 s for 174 tests, Fable compile 4 s warm, mocha 5 s for 171 tests), the first row of that file.
- [ ] **Step 6: Pack into a scratch folder** (`artifacts/` is git-ignored): `dotnet pack src/Ranvier.Numerics -c Release -o artifacts/pack-0.2.0`, and the same for `src/Ranvier.Quantities` and `src/Ranvier.Statistics`. Write a check script to the session scratchpad and run it with `python -I`: each of the three `.nupkg` files is version 0.2.0; its nuspec's dependencies are FSharp.Core 8.0.100 and Ranvier 0.2.1 (Numerics) or Ranvier.Numerics 0.2.0 (the other two); its `releaseNotes`, `description` and `tags` are Steps 2 and 3's; its `README.md` is byte-for-byte the repository's `README.md`; its `fable/` folder holds every `.fs` of its project's `Compile` list. Expected: every check passes. Delete `artifacts/pack-0.2.0` afterwards (this task created it).
- [ ] **Step 7: Commit** `build: version 0.2.0, release notes, descriptions, tags and README`, with the gate times in the body. Do not push.

---

## Coordinator steps after Task 15

These are not implementer tasks. The coordinator session runs them after checking the diff, the checks and the branch protection (`C:\wk\ranvier-release\AGENTS.md`, "Working with other agents"). `RELEASE-RUNBOOK.md` has no Ranvier.Science section; the mechanics are those of `.github/workflows/ci.yml` and `publish.yml`.

1. **Whole-branch review** of `f329053..toolkit` against the spec, by a fresh reviewer on the most capable model; its findings become implementer tasks on the branch, each with the gate.
2. **Push** `toolkit` to `origin` (ranvier-org/Ranvier.Science) and confirm with `git ls-remote origin toolkit`.
3. **Open the pull request** from `toolkit` to `main` (`gh pr create --base main --head toolkit`), its body summarising the three plans and ending with the attribution line.
4. **Wait for CI**: `gh pr checks <number> --watch --fail-fast` (build, both suites, pack). A failure is diagnosed and fixed on the branch.
5. **Merge** once CI passes and the protection allows it, keeping the plans' commits (a merge commit, or a rebase merge if the protection requires linear history; not a squash).
6. **Publish.** The push to `main` runs CI, whose success triggers `publish.yml`. Expected: its `resolve` job lists all three packages as new and the `publish` job fails at `NuGet/login` (the token exchange answers "No match": the trusted-publishing policy still names the repository that became the private archive, spec goal 6, run 44667750478). Confirm the failure is at that step (`gh run watch <run-id> --compact --exit-status`, then the job log), and ask the owner to re-save the nuget.org trusted-publishing policy for ranvier-org/Ranvier.Science and `publish.yml`. After the owner confirms, re-run the failed job (`gh run rerun <run-id> --failed`, or dispatch `publish.yml` on `main` with `dry_run=false`) and confirm each of the three packages at `https://api.nuget.org/v3-flatcontainer/<id>/0.2.0/<id>.nuspec` answers 200.
7. **Clean up**: delete the merged remote branch `toolkit`, then the local branch (`git branch -d toolkit`) and any worktree the plans created.
