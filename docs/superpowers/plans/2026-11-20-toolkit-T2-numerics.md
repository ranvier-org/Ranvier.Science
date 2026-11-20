# Ranvier.Science 0.2.0, Plan T2: Numerics Kernels — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The numerics kernels of the 0.2.0 toolkit in `Ranvier.Numerics` (special functions, roots and minimisation, least squares, quadrature and differentiation, ODE integration, interpolation, dense linear algebra, the Fourier transform, accumulators, maintained aggregates, random variates) and `Run.ode` in `Ranvier.Quantities`, each tested on .NET and JavaScript against a named reference.

**Architecture:** Kernels are plain functions over `float` and `float[]` in new files of `src/Ranvier.Numerics`, one file per area; none reads or creates a node. The graph-aware parts are the additions to `Aggregate.fs` and `Run.ode`. Every test lives in the shared suite, so it runs on both targets; the exact cross-target cases of goal 3 go into `CrossTargetTests.fs`. Reference values come from closed forms written in the tests, from one Python standard-library script, `tests/reference/special.py`, or from a cited published table.

**Tech Stack:** F# on .NET 10 and Fable (through Fickl); core `Ranvier` 0.2.1 (Plan T1's first task moves the packages to it); Expecto 11 with the Mocha shim; Python 3.14's standard library (`decimal`, `fractions`, `math`) for references.

**Spec:** `C:\wk\ranvier-release\Ranvier.Science\docs\superpowers\specs\2026-11-20-toolkit-design.md`: sections 3.2, 3.3, 5, 6.1 to 6.10 and 8.7. Section numbers below refer to it. Plan T1 (sections 4, 6.11, 6.12, 8 but 8.7) runs before this plan and Plan T3 (sections 7 and 9) after it, on the same branch. This plan consumes from T1 only `Moments.sampleVariance`, `Moments.standardDeviation`, `Moments.sampleStandardDeviation`, `FloatPolicy.standard` with `RefoldFloor = 64`, `OrderStatistic.quantileSorted` (Plan T1, Task 14; spec section 6.13) and the core 0.2.1 dependency.

## Global Constraints

The controller passes this section with every task.

**Repository and gate**

- Work in `C:\wk\ranvier-release\Ranvier.Science` on branch `toolkit`; commit there only. Never merge, push, tag or publish. No file under `C:\wk\ranvier-release\Ranvier\src` changes. Read core 0.2.1 at its tag: `git -C C:/wk/ranvier-release/Ranvier show v0.2.1:src/Ranvier/<file>`.
- The gate, from the repository root: `dotnet build -c Release` (0 warnings, 0 errors); `dotnet run -c Release --project tests/Ranvier.Science.Tests` (0 failed); `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then `npx mocha dist/tests/Main.fs.js` (0 failing). A task is complete only after the whole gate passes unfiltered. For the red/green loop: `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter "<list name>"`, and `rfs check --fail-on-error` for a compile check (plain `rfs check` exits 0 with errors).
- Registering files: a library file goes into its project's `<Compile>` list where the task says. A new test file goes into `tests/Ranvier.Science.Tests/TestFiles.props` immediately before `CrossTargetTests.fs`, and its root into `tests/Ranvier.Science.Tests.Fable/Main.fs` immediately before the `CrossTargetTests.fs` line, as `testList "<File>.fs" [ Ranvier.Science.Tests.<Module>.tests ]`. A test file is never registered in one project alone.
- Commit messages are conventional (`feat(numerics): …`, `feat(quantities): …`, `test: …`) and end with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Shell: write a multi-line script to a file and run the file; use absolute paths.

**Library code**

- Fable-compatible: no reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`, no `System.Numerics.Complex`, no BCL type Fable lacks (`PriorityQueue`, `BitConverter`). `Math.*`, arrays, lists and `ResizeArray` are fine.
- The names and signatures in each task's Produces block are the spec's and are fixed. Plan T3 consumes `Special`, `Matrix` and its decompositions, `LeastSquares.levenbergMarquardt`, `Comoments`, the `Generator` variates, `Integrate.adaptive` and `Roots.brent` by them. Parameter order: configuration first, data last.
- Errors (section 3.2): an argument outside the domain raises `ArgumentOutOfRangeException`, and a dimension or length mismatch raises `ArgumentException` naming both lengths; a NaN data argument otherwise in the domain returns NaN (or NaN fields) and raises nothing, while a NaN configuration parameter (a distribution's or variate's parameter, a rate, a half-life, a tolerance, settings) raises `ArgumentOutOfRangeException`; a data-dependent failure of a direct method returns `Result<'T, string>` whose message names the condition; an iterative method that may stop short returns a record with `Converged: bool`. No function returns a plausible number where it failed. An exception raised by a caller's function propagates unchanged.
- A kernel never reads or creates a node. Inputs are never mutated, and a returned array is fresh: never an input, and never the array a value holds.
- Accumulators (sections 3.3, 6.8): immutable values, `compensated: bool` first, each running total a `Sum` added with `Sum.add compensated` as `Moments` does; `remove` and `unmerge` raise `InvalidOperationException` on an empty accumulator.
- Cut-off (section 3.3): the core's default equality compares reference types by reference on .NET, and everything but primitives by reference under Fable. No doc comment claims a cut-off from structural equality, a memo returning anything but `float` says in its doc comment that it publishes on every recomputation, and no test expects a memo of a record, union, array or struct value to cut off under the default policy.
- Exact kernels (section 5.3): LU, Cholesky, QR, `Vector.norm`, the moments family, interpolation, `Ode.rungeKutta4Step` and `Ode.rungeKutta4`, and the power-of-two Fourier path use only `+ − × ÷`, `sqrt`, `abs` and comparisons: no `**`, `pown`, `Math.Pow`, `exp`, `log` or trigonometric call.
- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`: the contract, the raise conditions, the cost where it is not O(1), the accuracy bound; no narration; British spelling. A bound reads "relative error at most E over D" (or absolute, near zero), names the test list that pins it, and is labelled *measured here*. A constant copied from a paper or table is *from documentation*, with the citation; a cost or rate not checked here is *general knowledge*. A limit the code defines (an iteration cap) is a definition and carries no label.
- Bounds (section 5.1) are floors for the tests: `gammaLn`, `gamma`, `beta`, `erf`, `erfc`, `digamma` relative 1e-13 where the value is at least 1e-300; the regularised incomplete gamma and beta relative 1e-12 where the value is at least 1e-300; their inverses round-trip, the forward function at the result within 1e-12 relative of the target probability; adaptive quadrature and the ODE solve meet the requested tolerance on closed-form problems. A task may tighten a bound its tests show; it never loosens one except where a task states a ruling (Task 1). A bound that fails is reported to the controller with the measured error, and the task stops.

**Tests and references**

- Shared test code uses only the Expecto shim (`testList`, `test`, `testCase`, `testCaseAsync`, the `Expect.*` of `tests/Ranvier.Science.Tests.Fable/Expecto.fs`) and the helpers of `Exact.fs` (`ulps`, `exactSum`), `Support.fs` (`withGraph`) and, from Task 1, `Bounds.fs` (`expectClose`, `expectOk`, `expectError`).
- Every reference value carries its source beside it (section 5.2): a closed form or identity written in the test; a literal printed by a script under `tests/reference/`, pasted below a comment holding the command that printed it; or a published table cited by name and edition. A script uses Python 3.14's standard library only, runs as `python -I tests/reference/<name>.py <section>`, computes in `decimal` at 60 significant digits or more (or in exact `fractions`), takes each input as the exact double the test passes (`Decimal(0.1)`, never `Decimal("0.1")`), checks each value by computing it again at 20 more digits and exiting non-zero unless both round to the same double, and prints `repr(float(value))`. Never `pip install`.
- **Exact cross-target procedure** (goal 3): append the case to the `"CrossTarget"` list of `tests/Ranvier.Science.Tests/CrossTargetTests.fs`, after `the recorded stream gives pinned numbers`, with `Expect.equal` against literals. Write `nan` placeholders; run the .NET suite with a temporary `printfn` of each value as `v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)`; paste the printed values; remove the print; run both suites. If JavaScript gives other bits, leave the case failing, put both values in the report, and stop for the controller: section 5.3 claims equality.
- Statistical tests use fixed seeds and thresholds at significance 0.001: a Kolmogorov–Smirnov statistic at most 1.95/√n, or a chi-squared p-value, `Special.gammaUpperRegularized (df / 2.0) (chi2 / 2.0)`, of at least 0.001 (*general knowledge*: 1.95 is the asymptotic Kolmogorov critical value at 0.001).

## Review Focus

1. **A matrix scaled far from 1** (entries near 1e-150 or 1e150) reaches `lu`, `cholesky`, `qr` or `Vector.norm`. Expected: the verdict and the solution of the unscaled matrix, within the bound; `norm` neither overflows nor underflows. Tests in Tasks 5, 6, 7.
2. **A caller mutates an array after handing it over or after reading it** (an `ofRows` row, the `xs` of an interpolant, histogram edges, an ODE start state, `Matrix.row`, `Histogram.counts`, `Window.items`). Expected: the value already built is unchanged. Tests in Tasks 5, 11, 12, 13, 17, 18.
3. **A caller's function raises inside an iterative kernel** (`Roots`, `Minimize`, `Integrate`, `Ode`, `LeastSquares`). Expected: the exception reaches the caller unchanged, not an `Error` or `Converged = false`. Tests in Tasks 8, 9, 10, 11.
4. **A variate at the edge of its parameters** (gamma shape 1e-3, beta(1e-3, 1e-3), binomial with p 0 or 1 or no trials, Poisson mean 0, an integer range of width 1, an empty shuffle). Expected: values in the support, never NaN, no hang. Tests in Task 20.
5. **A length that does not match** (`ys` against `xs`, `im` against `re`, a residual or right-hand side whose length changes between calls, a vector against a matrix). Expected: `ArgumentException`. Tests in Tasks 5, 9, 10, 11, 13, 14.

---

## File Structure

```
src/Ranvier.Numerics/            new, in compile order, each inserted immediately before FloatPolicy.fs:
  Special.fs                     Special (Tasks 1–4)
  LinearAlgebra.fs               Vector, Matrix, the decompositions (Tasks 5–7)
  Roots.fs                       Roots, Minimize (Task 8)
  LeastSquares.fs                LeastSquares (Task 9)
  Calculus.fs                    Integrate, Differentiate (Task 10)
  Ode.fs                         Ode (Task 11)
  Interpolate.fs                 Interpolant, Interpolate (Task 13)
  Fourier.fs                     Fourier (Task 14)
  Comoments.fs                   (Task 15)
  HigherMoments.fs  WeightedMoments.fs   (Task 16)
  Histogram.fs                   Histogram, QuantileEstimate (Task 17)
  Windows.fs                     DecayedMoments, Window, TimeWindow (Task 18)
  modified: Moments.fs (Task 16), Aggregate.fs (Task 19), Generator.fs (Task 20)
src/Ranvier.Quantities/Run.fs    OdeState, Run.ode (Task 12)
tests/Ranvier.Science.Tests/     Bounds.fs (after Support.fs); SpecialTests.fs LinearAlgebraTests.fs RootsTests.fs
                                 LeastSquaresTests.fs CalculusTests.fs OdeTests.fs InterpolateTests.fs FourierTests.fs
                                 ComomentsTests.fs HigherMomentsTests.fs WeightedMomentsTests.fs HistogramTests.fs
                                 WindowTests.fs (each before CrossTargetTests.fs); modified: RunTests.fs,
                                 AggregateTests.fs, GeneratorTests.fs, CrossTargetTests.fs
tests/reference/special.py       references for Special (Tasks 1–4)
docs/measurements/gate.md        created by Plan T1, Task 15; a row of the gate's times appended at the plan's end (Task 20)
```

Choices the spec leaves to the builder (section 12) are stated in the task that makes them, each on a line starting **Choice.**

---

### Task 1: Gamma family, test helpers and the reference script (spec 6.1)

**Files:**
- Create: `src/Ranvier.Numerics/Special.fs`, `tests/Ranvier.Science.Tests/Bounds.fs`, `tests/Ranvier.Science.Tests/SpecialTests.fs`, `tests/reference/special.py`
- Modify: `src/Ranvier.Numerics/Ranvier.Numerics.fsproj` (`Special.fs` immediately before `FloatPolicy.fs`); `tests/Ranvier.Science.Tests/TestFiles.props` (`Bounds.fs` immediately after `Support.fs`; `SpecialTests.fs` as Global Constraints say); `tests/Ranvier.Science.Tests.Fable/Main.fs` (`SpecialTests.fs` only: `Bounds.fs` holds no tests)

**Interfaces:**
- Consumes: nothing new.
- Produces:

```fsharp
// tests/Ranvier.Science.Tests/Bounds.fs
[<AutoOpen>]
module Ranvier.Science.Tests.Bounds
/// Passes when actual = expected (equal infinities included), when both are NaN, or when
/// |actual − expected| ≤ max(relative · |expected|, absolute). The failure message prints both values with %.17g.
val expectClose : relative: float -> absolute: float -> expected: float -> actual: float -> message: string -> unit
/// The Ok value; fails the test with the Error's text otherwise.
val expectOk : message: string -> result: Result<'T, string> -> 'T
/// Passes when the result is an Error whose text contains `part`.
val expectError : part: string -> message: string -> result: Result<'T, string> -> unit

// src/Ranvier.Numerics/Special.fs
namespace Ranvier.Numerics
module Special =
    val gammaLn : x: float -> float            // ln |Γ(x)|
    val gamma : x: float -> float
    val digamma : x: float -> float
    val beta : a: float -> b: float -> float
    val betaLn : a: float -> b: float -> float
    val logFactorial : n: int -> float
    val logChoose : n: int -> k: int -> float
    val log1p : x: float -> float               // ln(1 + x): 1 + x rounded to u, then x when u = 1, else ln u · x / (u − 1)
    val expm1 : x: float -> float               // eˣ − 1: u = eˣ, then x when u = 1, −1 when u − 1 = −1, else (u − 1) · x / ln u
    // internal helpers, used by Tasks 2 to 4
    val internal log1pmx : x: float -> float             // ln(1 + x) − x: its Taylor series for |x| < 0.5, log1p x − x otherwise
    val internal stirlingRemainder : x: float -> float   // lnΓ(x) − ((x − ½) ln x − x + ½ ln 2π), for x ≥ 8
    val internal sinPi : x: float -> float               // sin(πx), x reduced exactly to [−½, ½] before the sine
```

**Choice.** Domains: `gammaLn`, `gamma` and `digamma` take x > 0 or non-integer x < 0; 0, the negative integers and −∞ raise `ArgumentOutOfRangeException`; +∞ gives +∞; NaN gives NaN. `gamma` gives +∞ from 171.63 on. `beta` and `betaLn` take a, b > 0; `logFactorial` n ≥ 0; `logChoose` 0 ≤ k ≤ n.

**Choice.** Bounds: `gamma` and `beta` relative 1e-13 where the value is at least 1e-300. `gammaLn`, `digamma`, `betaLn`, `logFactorial` and `logChoose`, whose values pass through zero, |error| ≤ max(1e-13 · |value|, 1e-14): relative 1e-13, and absolute 1e-14 where the value is below 0.1 in magnitude. **Ruling (pre-flight, 2026-11-20):** this is a recorded loosening of spec section 5.1, accepted because relative accuracy is unattainable at the zeros of these functions (x = 1 and 2 for `gammaLn`; ψ's root near 1.4616); each of the five doc comments states the absolute floor and why. `log1p` and `expm1` are public (spec section 6.1) at relative 1e-15 or the bound this task's tests show, the larger of the two reported to the controller.

**Choice.** `log1p` takes x ≥ −1: −1 gives −∞, x < −1 raises `ArgumentOutOfRangeException`, +∞ gives +∞, NaN gives NaN. `expm1` takes every x: +∞ and an overflowing x give +∞, −∞ gives −1, NaN gives NaN. Both use Kahan's trick as the Produces block states (Goldberg 1991, Theorem 4, *from documentation*); Plan T3 uses them in place of any copy.

**Choice.** Methods (the spec offers Lanczos or Stirling with shifts; this plan takes Stirling, whose coefficients the script derives):
- lnΓ for x ≥ 8: (x − ½) ln x − x + ½ ln 2π + Σₖ₌₁¹⁰ B₂ₖ / (2k(2k − 1) x^(2k−1)); for 0 < x < 8, lnΓ(x + N) − ln(x(x + 1)…(x + N − 1)) with the least N giving x + N ≥ 8; for x < 0, ln π − ln |sinPi x| − lnΓ(1 − x).
- Γ for x ≥ 8 as √(2π) · (q · e^(−x)) · q · e^(S(x)) with q = x^((x − ½)/2) and S the series above (the naive `exp (gammaLn x)` loses about 3e-13 relative near 171); for 0.5 ≤ x < 8 by the shift as a quotient; below 0.5 by reflection, π / (sinPi x · Γ(1 − x)).
- ψ for x ≥ 8: ln x − 1/(2x) − Σₖ₌₁¹⁰ B₂ₖ / (2k x^(2k)); below, ψ(x) = ψ(x + 1) − 1/x; for x < 0, ψ(1 − x) − π cos(πr)/sin(πr) with the exact reduction of `sinPi`.
- betaLn with a ≥ b (swap first, so `beta a b` and `beta b a` give the same bits): both ≥ 8, (a − ½) log1p(−b/(a + b)) + (b − ½) ln(b/(a + b)) − ½ ln(a + b) + ½ ln 2π + δ(a) + δ(b) − δ(a + b), with δ = `stirlingRemainder`; b < 8 ≤ a, lnΓ(b) + (a − ½) log1p(−b/(a + b)) − b ln(a + b) + b + δ(a) − δ(a + b); both < 8, lnΓ(a) + lnΓ(b) − lnΓ(a + b). `beta` is Γ(a)Γ(b)/Γ(a + b) when a + b < 8, `exp (betaLn a b)` otherwise.
- `logFactorial n = gammaLn (n + 1)`; `logChoose n k = −ln(n + 1) − betaLn (n − m + 1) (m + 1)` with m = min k (n − k).

- [ ] **Step 1: Write `tests/reference/special.py` with its `gamma` section.** A docstring giving the command line `python -I tests/reference/special.py <section>`; the section is the one argument. Helpers, all in `decimal` at a precision the caller sets: π by Machin's formula; Bernoulli numbers B₀…B₆₀ as exact `Fraction`s (Akiyama–Tanigawa); `ln_gamma(z)` for z > 0 by shifting to z + N ≥ 40 and summing 30 terms of Stirling's series; `gamma_value(x)` (`exp(ln_gamma)` for x > 0, reflection for x < 0 with `sin` by its Taylor series after reducing x modulo 2); `digamma(z)` by shifting to ≥ 40 and 30 terms of the asymptotic series, reflection for x < 0; `value(f, *args)` as Global Constraints describe (60 digits, then 80, same double or exit 1). The `gamma` section prints, under the comment `// python -I tests/reference/special.py gamma`, F# literals:
  - `gammaLnCases: (float * float)[]` at x = 1e-300, 1e-8, 0.001, 0.1, 0.5, 0.9, 1.0, 1.1, 1.5, 1.9, 2.0, 2.5, 3.7, 7.9, 8.0, 10.1, 33.3, 100.5, 171.5, 1000.25, 100000.5, 1e10, 1e300, −0.5, −1.5, −2.75, −10.3, −100.7, −170.4, −1000.5;
  - `gammaCases: (float * float)[]` at x = 1e-300, 1e-8, 0.001, 0.1, 0.5, 1.5, 2.5, 3.7, 7.9, 10.1, 33.3, 100.5, 170.5, 171.6, −0.5, −1.5, −2.75, −10.3, −100.7, −150.3;
  - `digammaCases: (float * float)[]` at x = 1e-8, 0.01, 0.5, 1.0, 1.4616321449683622, 1.5, 2.0, 2.5, 7.9, 10.0, 100.5, 1e6, 1e15, −0.5, −2.3, −10.7;
  - `betaCases: (float * float * float * float)[]` (a, b, B(a, b), ln B(a, b)) at (0.5, 0.5), (1, 1), (2, 3), (0.001, 0.001), (0.1, 10), (10.5, 20.25), (8, 8), (7.9, 8.1), (100, 200), (1e6, 4), (3.5, 1e5), (1000, 0.01);
  - `logFactorialCases: (int * float)[]` at n = 0, 1, 2, 10, 20, 21, 100, 1000, 1000000, 2147483647 (`math.factorial` then `ln` for n ≤ 10⁵, `ln_gamma(n + 1)` above);
  - `logChooseCases: (int * int * float)[]` at (0, 0), (5, 0), (5, 5), (10, 3), (50, 25), (1000, 500), (1000000, 3), (1000000, 999997), (2147483647, 2) (`math.comb`, then `ln`);
  - comment lines giving B₂ₖ/(2k(2k − 1)) and B₂ₖ/(2k) for k = 1..12 as exact fractions, for `Special.fs` to transcribe as `numerator / denominator`.
- [ ] **Step 2: Run** `python -I tests/reference/special.py gamma`. Expected: exit 0 and the blocks above.
- [ ] **Step 3: Write `Bounds.fs`** as the Produces block states, and **the failing tests** in `SpecialTests.fs` (module `Ranvier.Science.Tests.SpecialTests`, list `"Special"`), the printed blocks pasted at the top of the file under their command comment:

| Test name | Asserts |
|---|---|
| `gammaLn matches the reference` | every `gammaLnCases` pair: `expectClose 1e-13 1e-14` (script) |
| `gamma matches the reference` | every `gammaCases` pair: `expectClose 1e-13 0.0` (script) |
| `gamma of n is (n - 1)!` | n = 1..23 against the float product `1 · 2 · … · (n − 1)` (exact for n ≤ 23): `expectClose 1e-13 0.0` (identity Γ(n) = (n − 1)!) |
| `gamma of one half is the root of pi` | `expectClose 1e-13 0.0 (sqrt Math.PI) (Special.gamma 0.5)` (identity) |
| `gamma satisfies its recurrence` | x = 0.05k for k = 1..400 and x = −0.05k − 0.025 for k = 0..100: `expectClose 3e-13 0.0 (x * gamma x) (gamma (x + 1.0))` (Γ(x + 1) = xΓ(x)) |
| `gamma overflows from 171.63` | `gamma 171.62` is finite; `gamma 171.63` and `gamma 1e10` are `infinity` |
| `digamma matches the reference` | every `digammaCases` pair: `expectClose 1e-13 1e-14` (script) |
| `beta and betaLn match the reference` | every `betaCases` row: `beta` with `expectClose 1e-13 0.0`, `betaLn` with `expectClose 1e-13 1e-14` (script) |
| `beta is symmetric` | for every `betaCases` pair, `beta a b = beta b a` and `betaLn a b = betaLn b a` (`Expect.equal`) |
| `logFactorial matches the reference` | every `logFactorialCases` pair: `expectClose 1e-13 1e-14` (script); `logFactorial 0` and `logFactorial 1` equal `0.0` |
| `logChoose matches the reference` | every `logChooseCases` row: `expectClose 1e-13 1e-14` (script); `logChoose n k = logChoose n (n - k)` (`Expect.equal`) |
| `log1p and expm1 at small arguments` | x = 1e-10: `log1p x` within 1 ulp of 1e-10 − 5e-21 (series x − x²/2 + x³/3, with the x³ term 3.3e-31 below an ulp) and `expm1 x` within 1 ulp of 1e-10 + 5e-21 (series x + x²/2); x = 1e-300: both equal x; x = ±1e-5: `expectClose 1e-15 0.0` against the series to the x⁶ term written in the test; `log1p` and `expm1` of 0.0 are 0.0 |
| `log1p and expm1 inverses and ranges` | x = 0.5, 3.0, 1e5: `expectClose 1e-15 0.0 x (expm1 (log1p x))`; `log1p -1.0 = -infinity`, `log1p infinity = infinity`, `expm1 -infinity = -1.0`, `expm1 1000.0 = infinity`; both of `nan` are NaN; `Expect.throwsT<ArgumentOutOfRangeException>` for `log1p -1.5` |
| `infinity and NaN` | `gammaLn`, `gamma` and `digamma` of `infinity` are `infinity`; of `nan` are NaN; `beta nan 1.0` and `betaLn 1.0 nan` are NaN |
| `arguments outside the domain raise` | `Expect.throwsT<ArgumentOutOfRangeException>` for `gammaLn 0.0`, `gammaLn -3.0`, `gammaLn -infinity`, `gamma -1.0`, `digamma 0.0`, `digamma -2.0`, `beta 0.0 1.0`, `betaLn 1.0 -1.0`, `logFactorial -1`, `logChoose 5 6`, `logChoose 5 -1` |

- [ ] **Step 4: Run** the `"Special"` filter. Expected: the build fails, `Special` is not defined.
- [ ] **Step 5: Implement `Special.fs`** by the methods above. Each public function's doc comment states its domain, its raise conditions and its bound ("… pinned by the `Special` tests against `tests/reference/special.py` (*measured here*)").
- [ ] **Step 6: Run** the `"Special"` filter. Expected: all pass. A case outside its bound is reported with its error (Global Constraints).
- [ ] **Step 7: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 8: Commit** `feat(numerics): gamma, digamma, beta and log-factorial with a decimal reference script`.

### Task 2: `erf` and `erfc` (spec 6.1)

**Files:**
- Modify: `src/Ranvier.Numerics/Special.fs`, `tests/reference/special.py` (section `erf`), `tests/Ranvier.Science.Tests/SpecialTests.fs`

**Interfaces:**
- Consumes: `Special` and its internal helpers (Task 1); `expectClose` (Task 1).
- Produces: `Special.erf : x: float -> float`, `Special.erfc : x: float -> float`.

**Choice.** Method: for 0 ≤ x < 2, erf by the positive-term series (2/√π) e^(−x²) Σ 2ⁿ x^(2n+1) / (1 · 3 · … · (2n + 1)) and erfc = 1 − erf; for x ≥ 2, erfc by Laplace's continued fraction e^(−x²)/√π · 1/(x + ½/(x + 1/(x + (3/2)/(x + …)))), modified Lentz, and erf = 1 − erfc; in that branch e^(−x²) is e^(−h) · e^(−l) with x² = h + l split exactly (Dekker's product), since a rounded x² costs up to 6e-14 relative at x = 26. erf(−x) = −erf(x); erfc(−x) is computed as 2 − erfc(x). Iteration cap 500 for each; NaN past it.

- [ ] **Step 1: Add the `erf` section** to `special.py`: erf by its Maclaurin series at precision 60 + ⌈x²/ln 10⌉ + 10, erfc as 1 − erf at precision 60 + ⌈2x²/ln 10⌉ + 10, both through `value`. It prints `erfCases: (float * float)[]` at x = 1e-300, 1e-20, 1e-8, 0.1, 0.4, 0.5, 0.9, 1.0, 1.5, 1.99, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, −0.7, −3.0, and `erfcCases: (float * float)[]` at x = −6.0, −3.0, −1.0, −0.5, 1e-10, 0.3, 0.5, 0.9, 1.0, 1.5, 1.99, 2.0, 2.01, 2.5, 3.0, 4.5, 6.0, 10.0, 15.0, 20.0, 26.0, 26.2. Run `python -I tests/reference/special.py erf`. Expected: exit 0.
- [ ] **Step 2: Write the failing tests** (in the `"Special"` list):

| Test name | Asserts |
|---|---|
| `erf matches the reference` | every `erfCases` pair: `expectClose 1e-13 0.0` (script) |
| `erfc matches the reference` | every `erfcCases` pair: `expectClose 1e-13 0.0` (script; every value is at least 1e-300) |
| `erf is odd and erfc reflects` | for every x of both grids, `erf -x = -(erf x)`; for every x ≥ 0 of both grids, `erfc -x = 2.0 - erfc x` (`Expect.equal`) |
| `erf and erfc sum to one` | x = −1 to 1 in steps of 0.01: `expectClose 0.0 3e-16 1.0 (erf x + erfc x)` (identity) |
| `limits` | `erf 0.0 = 0.0`, `erfc 0.0 = 1.0`, `erf infinity = 1.0`, `erf -infinity = -1.0`, `erfc infinity = 0.0`, `erfc -infinity = 2.0`, `erfc 40.0 = 0.0`; `erf nan` and `erfc nan` are NaN |

- [ ] **Step 3: Run** the `"Special"` filter. Expected: the build fails, `Special.erf` is not defined.
- [ ] **Step 4: Implement** `erf` and `erfc`; the doc comment states the bound (relative 1e-13 where the value is at least 1e-300, so erfc up to x ≈ 26.2) with its label.
- [ ] **Step 5: Run** the filter. Expected: all pass.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): erf and erfc`.

### Task 3: Regularised incomplete gamma and its inverse (spec 6.1)

**Files:**
- Modify: `src/Ranvier.Numerics/Special.fs`, `tests/reference/special.py` (section `incgamma`), `tests/Ranvier.Science.Tests/SpecialTests.fs`

**Interfaces:**
- Consumes: `Special.gammaLn`, `Special.erf`, `log1p`, internal `log1pmx`, `stirlingRemainder` (Tasks 1, 2).
- Produces:

```fsharp
val gammaLowerRegularized : a: float -> x: float -> float          // P(a, x)
val gammaUpperRegularized : a: float -> x: float -> float          // Q(a, x), accurate where P is near 1
val gammaLowerRegularizedInverse : a: float -> p: float -> float   // the x with P(a, x) = p
```

**Choice.** Domain: a > 0 and finite, x ≥ 0 (x = +∞ allowed), p in [0, 1]; otherwise `ArgumentOutOfRangeException`; NaN gives NaN. P(a, 0) = 0, P(a, ∞) = 1; the inverse gives 0 at p = 0 and +∞ at p = 1.

**Choice.** Method: P by its power series when x < a + 1, Q by Legendre's continued fraction (modified Lentz) otherwise, the other as 1 minus. The prefactor xᵃe⁻ˣ/Γ(a) is `exp (a * log1pmx ((x - a) / a) - stirlingRemainder a) * sqrt (a / (2π))` for a ≥ 8 and `exp (a * log x - x - gammaLn a)` below: the naive form loses about 1e-11 relative at a = 1000. Iteration cap 10 000; NaN past it. The inverse starts from the guess of Numerical Recipes, 3rd edition, §6.2.1 (`invgammp`; *from documentation*) and takes Halley steps on P(a, x) − p, kept positive, until the step is below 1e-15 relative or 100 steps. The bound is pinned for a ≤ 1000, and the doc comment states that domain.

- [ ] **Step 1: Add the `incgamma` section** to `special.py`: P(a, x) = xᵃe⁻ˣ/Γ(a + 1) · Σₙ xⁿ/((a + 1)…(a + n)) and Q = 1 − P, both through `value`, with the working precision raised by ⌈x/ln 10⌉ + 20 digits for Q; they are the functions `gamma_p(a, x)` and `gamma_q(a, x)` of `special.py`, which Plan T3's sections call (no second copy). It prints `incompleteGammaCases: (float * float * float * float)[]` (a, x, P, Q) at (0.5, 0.01), (0.5, 0.1), (0.5, 2.0), (1.0, 1e-5), (1.0, 1.0), (1.0, 690.0), (2.5, 3.0), (3.0, 0.5), (10.0, 10.0), (10.0, 30.0), (10.0, 100.0), (30.0, 10.0), (100.0, 90.0), (100.0, 110.0), (1000.0, 1000.0), (1000.0, 900.0), (0.01, 0.001), (0.001, 5.0), with 0.0 printed for a value below 1e-300. Run `python -I tests/reference/special.py incgamma`. Expected: exit 0.
- [ ] **Step 2: Write the failing tests:**

| Test name | Asserts |
|---|---|
| `incomplete gamma matches the reference` | every row: P and Q with `expectClose 1e-12 0.0` wherever the reference is at least 1e-300 (script) |
| `P of one half is erf of the root` | x = 0.01, 0.5, 2.0, 10.0: `expectClose 1e-12 0.0 (erf (sqrt x)) (gammaLowerRegularized 0.5 x)` (identity P(½, x) = erf √x) |
| `Q of an integer shape is a Poisson sum` | `expectClose 1e-12 0.0 (exp -0.5 * (1.0 + 0.5 + 0.125)) (gammaUpperRegularized 3.0 0.5)`; x = 5, 50, 500: `expectClose 1e-12 0.0 (exp -x) (gammaUpperRegularized 1.0 x)` (identity Q(n, x) = e⁻ˣ Σₖ₌₀ⁿ⁻¹ xᵏ/k!) |
| `P and Q sum to one` | every row: `expectClose 0.0 4e-16 1.0 (P + Q)` |
| `limits` | P(a, 0) = 0, Q(a, 0) = 1, P(a, ∞) = 1, Q(a, ∞) = 0 for a = 0.5, 3, 100 |
| `the inverse round-trips` | a in 0.1, 0.5, 1, 2.5, 10, 100, 1000 and p in 1e-10, 1e-3, 0.05, 0.5, 0.95, 0.999, 1 − 1e-10: `expectClose 1e-12 0.0 p (gammaLowerRegularized a (gammaLowerRegularizedInverse a p))` (spec 5.1) |
| `the inverse at 0 and 1` | 0.0 and `infinity` for a = 0.5 and 10 |
| `domain and NaN` | `ArgumentOutOfRangeException` for a = 0, a = −1, a = ∞, x = −1, p = −0.1, p = 1.1; NaN a, x or p gives NaN |

- [ ] **Step 3: Run** the `"Special"` filter. Expected: the build fails, `gammaLowerRegularized` is not defined.
- [ ] **Step 4: Implement** the three functions; doc comments state the domain, the bound with its label and the pinned domain a ≤ 1000.
- [ ] **Step 5: Run** the filter. Expected: all pass.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): regularised incomplete gamma and its inverse`.

### Task 4: Regularised incomplete beta and its inverse (spec 6.1)

**Files:**
- Modify: `src/Ranvier.Numerics/Special.fs`, `tests/reference/special.py` (section `incbeta`), `tests/Ranvier.Science.Tests/SpecialTests.fs`

**Interfaces:**
- Consumes: `Special.betaLn`, `log1p`, internal `log1pmx`, `stirlingRemainder` (Task 1).
- Produces:

```fsharp
val betaRegularized : a: float -> b: float -> x: float -> float          // I_x(a, b)
val betaRegularizedInverse : a: float -> b: float -> p: float -> float   // the x with I_x(a, b) = p
```

**Choice.** Domain: a, b > 0 and finite, x and p in [0, 1]; otherwise `ArgumentOutOfRangeException`; NaN gives NaN. I₀ = 0, I₁ = 1; the inverse gives 0 at p = 0 and 1 at p = 1.

**Choice.** Method: the continued fraction of Numerical Recipes, 3rd edition, §6.4 (`betacf`, modified Lentz; *from documentation*) when x < (a + 1)/(a + b + 2), and 1 − I₁₋ₓ(b, a) otherwise. The prefactor xᵃ(1 − x)ᵇ/B(a, b), for a, b ≥ 8, with c = a + b, x₀ = a/c, y₀ = b/c and y = 1 − x, is `sqrt (a * b / (2π * c)) * exp (a * log1pmx ((x - x0) / x0) + b * log1pmx ((y - y0) / y0) - δ a - δ b + δ c)` (δ = `stirlingRemainder`); below 8, `exp (a * log x + b * log1p (-x) - betaLn a b)`. Iteration cap 10 000; NaN past it. The inverse starts from the guess of Numerical Recipes, 3rd edition, §6.14.1 (`invbetai`), and takes Halley steps kept inside (0, 1) by bisection, until the step is below 1e-15 relative or 100 steps. The bound is pinned for a, b ≤ 1000.

- [ ] **Step 1: Add the `incbeta` section** to `special.py`: B(x; a, b) = xᵃ(1 − x)ᵇ/a · Σₙ (a + b)ₙ/(a + 1)ₙ · xⁿ for x ≤ ½, and 1 − I₁₋ₓ(b, a) above at 30 more digits, divided by B(a, b) from `ln_gamma`; through `value`. The computation is the function `beta_inc(a, b, x)` of `special.py`, which Plan T3's sections call (no second copy). For integer a and b with a + b ≤ 200 it also evaluates I_x(a, b) = Σⱼ₌ₐᵃ⁺ᵇ⁻¹ C(a + b − 1, j) xʲ(1 − x)^(a+b−1−j) in exact `Fraction`s and exits non-zero unless both give the same double. It prints `incompleteBetaCases: (float * float * float * float)[]` (a, b, x, I) at (1, 1, 0.3), (0.5, 0.5, 0.2), (2, 3, 0.4), (5, 10, 0.1), (10, 5, 0.9), (0.1, 10, 0.01), (50, 50, 0.5), (50, 60, 0.4), (200, 300, 0.4), (0.001, 0.001, 0.5), (30, 2, 0.01), (1000, 1000, 0.49), (0.5, 1000, 1e-4), (7.5, 0.25, 0.999). Run `python -I tests/reference/special.py incbeta`. Expected: exit 0.
- [ ] **Step 2: Write the failing tests:**

| Test name | Asserts |
|---|---|
| `incomplete beta matches the reference` | every row: `expectClose 1e-12 0.0` (script) |
| `closed forms` | `betaRegularized 0.5 0.5 0.2` against `2.0 / Math.PI * asin (sqrt 0.2)`; `betaRegularized a 1.0 x` against `x ** a` and `betaRegularized 1.0 b x` against `1.0 - (1.0 - x) ** b` for a, b in 0.5, 3, 20 and x in 0.1, 0.5, 0.9: `expectClose 1e-12 0.0` (identities of I_x) |
| `symmetry` | every row: `expectClose 0.0 1e-15 1.0 (betaRegularized a b x + betaRegularized b a (1.0 - x))` where `1.0 - x` is exact (x ≥ 0.5) |
| `limits` | `betaRegularized a b 0.0 = 0.0` and `… 1.0 = 1.0` for (0.5, 2) and (30, 40) |
| `the inverse round-trips` | (a, b) in (0.5, 0.5), (1, 1), (2, 5), (10, 10), (0.1, 3), (100, 200) and p in 1e-10, 1e-3, 0.05, 0.5, 0.95, 0.999, 1 − 1e-10: `expectClose 1e-12 0.0 p (betaRegularized a b (betaRegularizedInverse a b p))` |
| `the inverse at 0 and 1` | 0.0 and 1.0 for (2, 3) |
| `domain and NaN` | `ArgumentOutOfRangeException` for a = 0, b = −1, x = −0.1, x = 1.1, p = 2; NaN arguments give NaN |

- [ ] **Step 3: Run** the `"Special"` filter. Expected: the build fails, `betaRegularized` is not defined.
- [ ] **Step 4: Implement** both functions with their doc comments (domain, bound, label, pinned domain a, b ≤ 1000).
- [ ] **Step 5: Run** the filter. Expected: all pass.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): regularised incomplete beta and its inverse`.

### Task 5: `Vector` and `Matrix` (spec 6.6, first part)

**Files:**
- Create: `src/Ranvier.Numerics/LinearAlgebra.fs`, `tests/Ranvier.Science.Tests/LinearAlgebraTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`LinearAlgebra.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Produces:

```fsharp
module Vector =
    val dot : float[] -> float[] -> float
    val add : float[] -> float[] -> float[]
    val subtract : float[] -> float[] -> float[]
    val scale : float -> float[] -> float[]
    val norm : float[] -> float                 // Euclidean, scaled against overflow
/// Dense, row-major, immutable; structural equality.
type Matrix = internal { RowCount: int; ColumnCount: int; Data: float[] }
module Matrix =
    val ofRows : float[][] -> Matrix            // copies; rows of one length
    val init : rows: int -> columns: int -> (int -> int -> float) -> Matrix
    val identity : int -> Matrix
    val zero : rows: int -> columns: int -> Matrix
    val diagonal : float[] -> Matrix
    val rowCount : Matrix -> int
    val columnCount : Matrix -> int
    val get : Matrix -> int -> int -> float
    val row : Matrix -> int -> float[]
    val column : Matrix -> int -> float[]
    val toRows : Matrix -> float[][]
    val transpose : Matrix -> Matrix
    val add : Matrix -> Matrix -> Matrix
    val subtract : Matrix -> Matrix -> Matrix
    val multiply : Matrix -> Matrix -> Matrix
    val scale : float -> Matrix -> Matrix
    val multiplyVector : Matrix -> float[] -> float[]
```

**Choice.** File layout: `LinearAlgebra.fs` holds `Vector`, then the type `Matrix`, then (from Tasks 6 and 7) the decomposition types and their modules, and last the one `module Matrix`. An F# module cannot be reopened, so Tasks 6 and 7 insert their types and modules above `module Matrix` and add their functions inside it.

**Choice.** `ofRows [||]` is the 0 × 0 matrix and `[| [||]; [||] |]` is 2 × 0. `init`, `zero` and `identity` raise `ArgumentOutOfRangeException` for a negative size, and `get`, `row` and `column` for an index outside the matrix. `norm` is max |xᵢ| · √Σ(xᵢ / max)², 0 for an empty or all-zero vector, NaN when any component is NaN, +∞ when any other component is infinite. `multiply` sums in index order (i, k, j).

- [ ] **Step 1: Write the failing tests** (`"LinearAlgebra"`):

| Test name | Asserts |
|---|---|
| `vector arithmetic` | `dot [\|1.;2.;3.\|] [\|4.;5.;6.\|] = 32.0`; `add`, `subtract` and `scale 2.0` of the same give `[\|5.;7.;9.\|]`, `[\|-3.;-3.;-3.\|]`, `[\|2.;4.;6.\|]` |
| `norm is scaled against overflow and underflow` | `norm [\|3.;4.\|] = 5.0`; `norm [\|3e200;4e200\|]` and `norm [\|3e-200;4e-200\|]`: `expectClose 1e-15 0.0` against 5e200 and 5e-200; `norm [\|\|] = 0.0`; `norm [\|1.;nan;infinity\|]` is NaN; `norm [\|1.;-infinity\|] = infinity` (Review Focus 1) |
| `construction and reads` | `ofRows [\|[\|1.;2.;3.\|];[\|4.;5.;6.\|]\|]`: `rowCount 2`, `columnCount 3`, `get m 1 2 = 6.0`, `row m 1 = [\|4.;5.;6.\|]`, `column m 2 = [\|3.;6.\|]`, `toRows` gives the rows; `identity 3`, `zero 2 3`, `diagonal [\|1.;2.\|]` and `init 2 2 (fun i j -> float (10 * i + j))` read as their definitions; `ofRows [\|\|]` is 0 × 0 |
| `arithmetic` | `multiply` of `[[1,2],[3,4]]` and `[[5,6],[7,8]]` is `[[19,22],[43,50]]`; a 2 × 3 by 3 × 2 product; `transpose`; `add`, `subtract`, `scale`; `multiplyVector [[1,2],[3,4]] [\|1.;1.\|] = [\|3.;7.\|]` (all `Expect.equal`) |
| `structural equality` | two `ofRows` of equal arrays are `=`; changing one entry makes them `<>` |
| `copies in and out` | mutating a row array after `ofRows`, and mutating the arrays returned by `row`, `column` and `toRows`, leaves `get` unchanged (Review Focus 2) |
| `mismatches raise` | `ArgumentException` for ragged `ofRows`, `Vector.dot` and `Vector.add` of lengths 2 and 3, `Matrix.add` of 2 × 2 and 2 × 3, `multiply` of 2 × 3 by 2 × 3, `multiplyVector` with a vector of the wrong length (Review Focus 5); `ArgumentOutOfRangeException` for `get m 2 0` on a 2 × 3 and `zero -1 2` |

- [ ] **Step 2: Run** the `"LinearAlgebra"` filter. Expected: the build fails, `Vector` is not defined.
- [ ] **Step 3: Implement** `LinearAlgebra.fs` (`Vector`, `Matrix`), with doc comments (raise conditions; cost of `multiply` O(n·m·p)).
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): dense vectors and matrices`.

### Task 6: LU and Cholesky (spec 6.6, second part)

**Files:**
- Modify: `src/Ranvier.Numerics/LinearAlgebra.fs`, `tests/Ranvier.Science.Tests/LinearAlgebraTests.fs`, `tests/Ranvier.Science.Tests/CrossTargetTests.fs`

**Interfaces:**
- Consumes: `Matrix`, `Vector` (Task 5).
- Produces:

```fsharp
type LuDecomposition               // internal representation
module LuDecomposition =
    val solve : LuDecomposition -> float[] -> float[]
    val determinant : LuDecomposition -> float
type CholeskyDecomposition         // internal representation
module CholeskyDecomposition =
    val solve : CholeskyDecomposition -> float[] -> float[]
module Matrix =                     // added inside the existing module
    val lu : Matrix -> Result<LuDecomposition, string>                 // partial pivoting
    val solve : Matrix -> float[] -> Result<float[], string>
    val inverse : Matrix -> Result<Matrix, string>
    val determinant : Matrix -> float
    val cholesky : Matrix -> Result<CholeskyDecomposition, string>
```

**Choice.** Thresholds, relative to the matrix's scale (ε = 2⁻⁵²): `lu` returns `Error "singular …"` when a pivot satisfies |uₖₖ| ≤ n · ε · maxᵢⱼ |aᵢⱼ|. `cholesky` reads the lower triangle and returns `Error "not symmetric …"` unless |aᵢⱼ − aⱼᵢ| ≤ 1e-12 · maxᵢⱼ |aᵢⱼ| for every pair, and `Error "not positive definite …"` when a pivot dₖ ≤ n · ε · maxᵢ |aᵢᵢ|. `determinant` is the LU product with the permutation's sign, and 0.0 when `lu` gives `Error`. A non-square matrix raises `ArgumentException`; a 0 × 0 matrix gives `Ok`, determinant 1, and `solve [||] = [||]`. `lu`, `cholesky` and `qr` (Task 7) scan for a non-finite entry first: a matrix holding a NaN or infinite entry gives `Ok`, and every number read through the result is NaN; no singularity, symmetry or positive-definiteness check runs on it (the symmetry test is false for a NaN, and an infinite entry makes the pivot threshold infinite, so without the scan the result would be an `Error`). Each doc comment states this.

- [ ] **Step 1: Write the failing tests** (`"LinearAlgebra"`), with `A = [[4,-2,1],[-2,4,-2],[1,-2,4]]` and `b = [|11.;-16.;17.|]`, whose solution is `[|1.;-2.;3.|]` (check: A·x = b in integers):

| Test name | Asserts |
|---|---|
| `LU solves an integer system` | `lu A`, `solve` of `b`: each component `expectClose 0.0 1e-14` against `[\|1.;-2.;3.\|]`; `Matrix.solve A b` gives the same |
| `determinant` | `Matrix.determinant [[1,2],[3,4]]`: `expectClose 1e-15 0.0 -2.0`; of `A`: `expectClose 1e-14 0.0 36.0` (cofactor expansion: 4·12 + 2·(−6) + 1·0 = 36) |
| `the inverse of the Hilbert matrix of order 5` | `inverse` of hᵢⱼ = 1/(i + j + 1) against the closed form (−1)^(i+j) (i + j + 1) C(n + i, n − j − 1) C(n + j, n − i − 1) C(i + j, i)², n = 5, 0-based: each entry `expectClose 1e-9 0.0` (κ ≈ 4.8e5, *general knowledge*) |
| `inverse times the matrix is the identity` | `M = init 6 6 (fun i j -> float ((i * 7 + j * 3) % 11) - 5.0 + (if i = j then 20.0 else 0.0))`: `multiply (inverse M) M` within absolute 1e-13 of `identity 6` |
| `a singular matrix gives Error` | `[[1,2],[2,4]]`: `lu`, `solve` and `inverse` give `expectError "singular"`; `determinant = 0.0`; `zero 3 3` likewise |
| `scale does not decide singularity` | `scale 1e-150 A` and `scale 1e150 A`: `lu` is `Ok`, and `solve` of the equally scaled `b` matches the unscaled solution within absolute 1e-14 (Review Focus 1) |
| `a decomposition is reusable` | two `LuDecomposition.solve` calls on one decomposition give equal arrays; mutating the first result leaves the second call's result unchanged |
| `shapes` | `ArgumentException` for `lu` of a 2 × 3 matrix and for `solve` with a vector of length 2 against `A`; `lu (zero 0 0)` is `Ok` with determinant 1.0 and `solve [\|\|] = [\|\|]` |
| `a NaN or infinite entry gives NaN results` | `A` with entry (0, 0) NaN: `lu` is `Ok`, `solve` is all NaN, `determinant` is NaN; `A` with entry (1, 0) NaN (an asymmetric pair): `cholesky` is `Ok` with NaN reads, not `Error "not symmetric"`; `A` with entry (0, 0) infinite: `lu` is `Ok` and `determinant` is NaN, not `Error "singular"` |
| `Cholesky solves a positive definite system` | `cholesky A`, `solve b`: within absolute 1e-14 of `[\|1.;-2.;3.\|]`; `scale 1e-150 A` is `Ok` (Review Focus 1) |
| `Cholesky rejects what it cannot factor` | `[[1,2],[3,4]]`: `expectError "not symmetric"`; `[[1,2],[2,1]]` and `[[1,1],[1,1]]`: `expectError "not positive definite"`; a 2 × 3 matrix raises `ArgumentException` |

- [ ] **Step 2: Write the two exact cross-target cases** by the procedure in Global Constraints: `LU solve gives the same bits on both targets` (`A4 = [[2,1,-1,3],[4,-6,0,1],[-2,7,2,0],[1,0,5,-3]]`, `b4 = [|0.1;-0.7;1.3;2.9|]`, the four components of `Matrix.solve`), and `Cholesky solve gives the same bits on both targets` (`S4 = [[4,1,0.5,0],[1,3,0.25,0.1],[0.5,0.25,2,0.3],[0,0.1,0.3,1.5]]`, `b = [|1.;2.;3.;4.|]`).
- [ ] **Step 3: Run** the `"LinearAlgebra"` filter. Expected: the build fails, `Matrix.lu` is not defined.
- [ ] **Step 4: Implement** the two decompositions with their doc comments (thresholds, costs O(n³) to factor and O(n²) to solve).
- [ ] **Step 5: Run** the `"LinearAlgebra"` and `"CrossTarget"` filters; paste the exact-case literals as the procedure says. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing; the two exact cases pass under mocha.
- [ ] **Step 7: Commit** `feat(numerics): LU and Cholesky decompositions`.

### Task 7: QR least squares and the symmetric eigenproblem (spec 6.6, third part)

**Files:**
- Modify: `src/Ranvier.Numerics/LinearAlgebra.fs`, `tests/Ranvier.Science.Tests/LinearAlgebraTests.fs`, `tests/Ranvier.Science.Tests/CrossTargetTests.fs`

**Interfaces:**
- Consumes: `Matrix`, `Vector` (Task 5).
- Produces:

```fsharp
type QrDecomposition                // internal representation
module QrDecomposition =
    val leastSquares : QrDecomposition -> float[] -> Result<float[], string>   // Error when rank-deficient
    val r : QrDecomposition -> Matrix                                           // upper-triangular factor, columns × columns
type SymmetricEigen = { Values: float[]; Vectors: Matrix }                    // ascending values; vectors as columns
module Matrix =
    val qr : Matrix -> QrDecomposition                                          // Householder; raises unless rows ≥ columns
    val symmetricEigen : Matrix -> Result<SymmetricEigen, string>               // cyclic Jacobi
```

**Choice.** `leastSquares` returns `Error "rank-deficient …"` when some |Rₖₖ| ≤ max(m, n) · ε · (the largest column norm of the matrix). `symmetricEigen` applies Task 6's symmetry rule (`Error "not symmetric …"`), sweeps until the off-diagonal sum of squares is at most (ε · ‖A‖_F)², and returns `Error "did not converge …"` after 100 sweeps. Each eigenvector is normalised, with its first component of largest magnitude positive. `qr` and `symmetricEigen` scan for a non-finite entry first, as Task 6 states: a matrix holding a NaN or infinite entry gives `Ok` with NaN results, and no rank or symmetry check runs on it. Each doc comment states this.

- [ ] **Step 1: Write the failing tests:**

| Test name | Asserts |
|---|---|
| `QR fits a line` | design rows `[1; t]` for t = 0, 1, 2, 3, y = `[\|1.;3.;4.;4.\|]`: `[\|1.5;1.0\|]` within absolute 1e-14 (normal equations: n = 4, Σt = 6, Σy = 12, Σt² = 14, Σty = 23, slope (4·23 − 6·12)/(4·14 − 36) = 1, intercept (12 − 6)/4 = 1.5) |
| `QR recovers a consistent solution` | `M = init 6 3 (fun i j -> float ((i + 1) * (j + 2) % 7) - 3.0)`, b = M·[\|2.;-1.;0.5\|]: within absolute 1e-13 of `[\|2.;-1.;0.5\|]` |
| `a rank-deficient design gives Error` | columns `[1;1;1]` and `[2;2;2]`: `expectError "rank-deficient"` |
| `scale does not decide rank` | the line design and y scaled by 1e-150: `Ok` with the unscaled answer within 1e-14 (Review Focus 1) |
| `shapes` | `qr` of a 2 × 3 matrix raises `ArgumentException`; `leastSquares` with y of the wrong length raises `ArgumentException` |
| `R reproduces the Gram matrix` | for the line design of `QR fits a line`: `r` is 2 × 2 with a zero below the diagonal, and `Matrix.multiply (Matrix.transpose r) r` is within absolute 1e-13 of XᵀX = [[4, 6], [6, 14]] (the coordinator's addition: Plan T3 computes a regression's covariance from R) |
| `eigen of a 2 × 2` | `[[2,1],[1,2]]`: `Values` within 1e-15 of `[\|1.;3.\|]`; the columns are `(1, −1)/√2` and `(1, 1)/√2` up to sign: \|v · e\| within 1e-15 of 1 (the sign rule is pinned by the diagonal case, where it is unambiguous) |
| `eigen of the second-difference matrix` | tridiag(−1, 2, −1) of order 10: `Values[k − 1]` within absolute 1e-14 of `2 − 2 cos(kπ/11)` (closed form); ‖A·v − λv‖∞ ≤ 1e-13 for each pair; VᵀV within 1e-14 of the identity |
| `eigen edge cases` | `diagonal [\|3.;1.;2.\|]`: values `[\|1.;2.;3.\|]`, vectors the matching unit columns; `[[5]]` gives values `[\|5.\|]` and vectors `[[1]]`; `zero 0 0` gives empty values; `[[1,2],[3,4]]`: `expectError "not symmetric"`; a NaN entry and an infinite entry each give `Ok` with NaN values (no symmetry check runs on them) |

- [ ] **Step 2: Write the exact cross-target case** `QR least squares gives the same bits on both targets`: design rows `[1; t; t²]` for t = 0, 0.5, 1.5, 2, 3.25, y = `[|0.3;1.1;0.2;-0.8;2.4|]`, the three coefficients.
- [ ] **Step 3: Run** the `"LinearAlgebra"` filter. Expected: the build fails, `Matrix.qr` is not defined.
- [ ] **Step 4: Implement** QR (Householder, with `Vector.norm`) and cyclic Jacobi; doc comments state the thresholds and costs (O(mn²); O(n³) per sweep).
- [ ] **Step 5: Run** the filters, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): QR least squares and the symmetric eigenproblem`.

### Task 8: Roots and minimisation (spec 6.2, first part)

**Files:**
- Create: `src/Ranvier.Numerics/Roots.fs`, `tests/Ranvier.Science.Tests/RootsTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Roots.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Produces:

```fsharp
module Roots =
    val bisection : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Result<float, string>
    val brent : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Result<float, string>
    val newton : tolerance: float -> f: (float -> float) -> df: (float -> float) -> x0: float -> Result<float, string>
type Minimum = { X: float; Value: float; Iterations: int; Converged: bool }
type MinimumN = { X: float[]; Value: float; Iterations: int; Evaluations: int; Converged: bool }
type NelderMeadOptions = { Tolerance: float; InitialStep: float; MaximumIterations: int }
module NelderMeadOptions =
    val standard : NelderMeadOptions      // { Tolerance = 1e-8; InitialStep = 0.1; MaximumIterations = 20_000 }
module Minimize =
    val brent : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Minimum        // on [a, b]
    val nelderMead : NelderMeadOptions -> f: (float[] -> float) -> x0: float[] -> MinimumN
```

`MinimumN` shares field names with `Minimum`: annotate the record type where one is built.

**Choice.** `tolerance` is absolute in x and must be ≥ 0 (a negative or NaN tolerance raises `ArgumentOutOfRangeException`). The root searches accept a and b in either order, swapping them to a < b first so both orders give the same bits, and return `Ok a` (or `Ok b`) when f is exactly 0 there; `Error "not bracketed …"` when f a and f b share a sign; `Error "f returned NaN at …"` on a NaN value. Iteration caps: bisection 2200 (enough to halve the whole double range), Brent 1000, Newton 100, each an `Error` naming the cap. Brent's root is Brent's `zeroin` (1973, *Algorithms for Minimization without Derivatives*, ch. 4; *from documentation*) with `tolerance` as its t. Newton stops when |step| ≤ tolerance, and returns `Error` on a zero or non-finite derivative or an iterate that leaves the finite numbers.

**Choice.** `Minimize.brent` is Brent's `localmin` (ibid., ch. 5) with `tolerance` as its absolute t; it raises unless a < b; 500 iterations, then `Converged = false`. `nelderMead` uses the coefficients 1, 2, ½, ½ (reflection, expansion, contraction, shrink; Lagarias et al. 1998, *general knowledge*), starts from x0 and x0 + InitialStep · eᵢ, orders a NaN value as +∞, and converges when maxᵢ |fᵢ − f_best| ≤ Tolerance · max(1, |f_best|) and maxᵢ ‖xᵢ − x_best‖∞ ≤ Tolerance · max(1, ‖x_best‖∞). One iteration is one reflection cycle; `Evaluations` counts calls of f. An empty x0 raises `ArgumentException`; options with Tolerance < 0, InitialStep ≤ 0 or MaximumIterations < 1 raise `ArgumentOutOfRangeException`.

- [ ] **Step 1: Write the failing tests** (`"Roots"`):

| Test name | Asserts |
|---|---|
| `bisection and brent find the root of two` | `x² − 2` on [0, 2], tolerance 1e-12: `expectClose 0.0 2e-12 (sqrt 2.0)`, both functions (Brent's bracket ends within t + 4ε\|x\| of the root) |
| `brent finds a root within its tolerance` | `cos x − x` on [0, 1] and `(x − 1)³` on [0, 3], tolerance 1e-10: with r the result, `f (r - 2e-10) * f (r + 2e-10) <= 0.0` (a sign change within twice the tolerance) |
| `brent accepts either order and exact endpoints` | `brent 1e-12 f 2.0 0.0` equals `brent 1e-12 f 0.0 2.0`; `f = fun x -> x - 1.0` on [1, 3] gives `Ok 1.0` |
| `an unbracketed or NaN function gives Error` | `x² + 1` on [−1, 1]: `expectError "not bracketed"`; `fun x -> if x > 0.5 then nan else x - 1.0` on [0, 2]: `expectError "NaN"` |
| `newton` | `x² − 2` from 1 with `2x`: within 1e-12 of √2; from 0 (zero derivative): `Error`; `atan x` from 2 with `1/(1 + x²)` (diverges): `Error` |
| `a raising function propagates` | `brent`, `newton` and `Minimize.brent` with `fun _ -> failwith "boom"`: the exception's message is `"boom"` (Review Focus 3) |
| `Minimize.brent finds interior and endpoint minima` | `(x − 2)² + 1` on [0, 5], tolerance 1e-8: `X` within 1e-6 of 2, `Value` within 1e-12 of 1, `Converged`; `cos` on [0, 2π]: `X` within 1e-6 of π; `x` on [0, 1]: `X` within 1e-6 of 0 |
| `Minimize.brent reports a NaN function` | `fun _ -> nan` on [0, 1]: `Converged = false` and `Value` NaN |
| `nelderMead minimises Rosenbrock` | `100(x₁ − x₀²)² + (1 − x₀)²` from `[\|-1.2;1.0\|]`, standard options: `X` within 1e-5 of `[\|1.;1.\|]`, `Converged`, `Evaluations > Iterations`; x0 unchanged |
| `nelderMead in five dimensions` | `Σ (xᵢ − i)²` from zeros: `X` within 1e-5 of `[\|0.;1.;2.;3.;4.\|]` |
| `nelderMead stops at its iteration limit` | Rosenbrock with `MaximumIterations = 5`: `Converged = false`, `Iterations = 5` |
| `domain` | negative tolerance raises `ArgumentOutOfRangeException` for every function; `Minimize.brent` with a ≥ b raises; `nelderMead` with `[\|\|]` raises `ArgumentException` |

- [ ] **Step 2: Run** the `"Roots"` filter. Expected: the build fails, `Roots` is not defined.
- [ ] **Step 3: Implement** `Roots.fs`; doc comments state the tolerance meaning, the caps and the convergence rules.
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): root finding and minimisation`.

### Task 9: Levenberg–Marquardt least squares (spec 6.2, second part)

**Files:**
- Create: `src/Ranvier.Numerics/LeastSquares.fs`, `tests/Ranvier.Science.Tests/LeastSquaresTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`LeastSquares.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Consumes: `Matrix.init`, `Matrix.get`, `Matrix.rowCount`, `Matrix.columnCount`, `Matrix.qr`, `QrDecomposition.leastSquares`, `Vector.norm` (Tasks 5, 7).
- Produces:

```fsharp
type LeastSquaresOptions = { Tolerance: float; MaximumIterations: int }
module LeastSquaresOptions =
    val standard : LeastSquaresOptions    // { Tolerance = 1e-10; MaximumIterations = 500 }
type LeastSquaresResult =
    { Parameters: float[]; Residuals: float[]; Jacobian: Matrix; Cost: float; Iterations: int; Converged: bool }
module LeastSquares =
    val levenbergMarquardt :
        LeastSquaresOptions -> residuals: (float[] -> float[]) -> initial: float[] -> LeastSquaresResult
    val levenbergMarquardtWith :
        LeastSquaresOptions -> residuals: (float[] -> float[]) -> jacobian: (float[] -> Matrix) -> initial: float[]
        -> LeastSquaresResult
```

**Choice.** One iteration evaluates the Jacobian once, then solves for the step δ minimising ‖[J; √λ D] δ + [r; 0]‖ with `Matrix.qr` and `QrDecomposition.leastSquares` (the augmented system has full rank for λ > 0), D diagonal with Dⱼⱼ the largest norm of column j seen so far, or 1 while that is 0 (MINPACK's scaling; *from documentation*: Moré, "The Levenberg–Marquardt algorithm: implementation and theory", 1978). λ starts at 1e-3, is divided by 10 on an accepted step and multiplied by 10 on a rejected one; a step is accepted when the cost falls. Forward differences use hⱼ = √ε · |pⱼ|, or √ε when pⱼ = 0 (MINPACK's `fdjac2`). `Converged` is true when an accepted step lowers the cost by at most Tolerance · cost, when ‖δ‖ ≤ Tolerance · (‖p‖ + Tolerance), or when the cost is 0; false at `MaximumIterations`, when λ passes 1e16 with no accepted step, or when the initial residuals are not finite (then `Iterations = 0`). `Cost` is ½ Σ rᵢ²; `Jacobian` and `Residuals` are at the returned parameters. A residual vector whose length changes, or a Jacobian of the wrong shape, raises `ArgumentException`.

- [ ] **Step 1: Write the failing tests** (`"LeastSquares"`):

| Test name | Asserts |
|---|---|
| `an exact exponential fit` | residuals `p[0] · exp(−p[1] t) − 3 exp(−0.5 t)` for t = 0..9, from `[\|1.;0.1\|]`, standard options: `Parameters` within relative 1e-8 of `[\|3.;0.5\|]`, `Cost <= 1e-20`, `Converged` (closed form: the data are the model at (3, 0.5)) |
| `Rosenbrock as least squares` | residuals `[\|10(p₁ − p₀²); 1 − p₀\|]` from `[\|-1.2;1.0\|]`: within 1e-8 of `[\|1.;1.\|]`, `Converged` |
| `an analytic Jacobian gives the same answer` | the exponential fit with its analytic Jacobian through `levenbergMarquardtWith`: within relative 1e-10 of `[\|3.;0.5\|]`; the forward-difference result's `Jacobian` within relative 1e-6 of the analytic one at the solution |
| `NIST Misra1a` | the 14 observations, start 1 (500, 1e-4) and certified values of NIST StRD Nonlinear Regression `Misra1a` (https://www.itl.nist.gov/div898/strd/nls/data/misra1a.shtml; *from documentation*, cited beside the literals), model y = b₁(1 − e^(−b₂x)): `Parameters` within relative 1e-6 of the certified b₁ and b₂, and `2 · Cost` within relative 1e-6 of the certified residual sum of squares. If the page cannot be read, report to the controller before writing this case |
| `the iteration limit` | Misra1a with `MaximumIterations = 1`: `Converged = false`, `Iterations = 1` |
| `non-finite initial residuals` | residuals `[\|nan\|]`: `Converged = false`, `Iterations = 0` |
| `a raising residual function propagates` | `failwith "boom"` on the second call: the exception's message is `"boom"` (Review Focus 3) |
| `a residual length that changes raises` | residuals of length 3 at the initial point and 2 elsewhere: `ArgumentException` (Review Focus 5) |
| `inputs are not mutated` | `initial` is unchanged after a fit |

- [ ] **Step 2: Run** the `"LeastSquares"` filter. Expected: the build fails, `LeastSquares` is not defined.
- [ ] **Step 3: Implement** `LeastSquares.fs`; the doc comments state the convergence rules, the difference step and the cost per iteration (one Jacobian: n residual calls with forward differences; one QR of (m + n) × n).
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): Levenberg-Marquardt least squares`.

### Task 10: Quadrature and differentiation (spec 6.3)

**Files:**
- Create: `src/Ranvier.Numerics/Calculus.fs`, `tests/Ranvier.Science.Tests/CalculusTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Calculus.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Produces:

```fsharp
type Integral = { Value: float; ErrorEstimate: float; Evaluations: int; Converged: bool }
module Integrate =
    val adaptive : absolute: float -> relative: float -> f: (float -> float) -> a: float -> b: float -> Integral
    val gaussLegendre : points: int -> f: (float -> float) -> a: float -> b: float -> float
    val trapezoid : xs: float[] -> ys: float[] -> float
    val simpson : xs: float[] -> ys: float[] -> float           // composite, uneven spacing allowed
    val cumulativeTrapezoid : xs: float[] -> ys: float[] -> float[]
type Derivative = { Value: float; ErrorEstimate: float }
module Differentiate =
    val derivative : f: (float -> float) -> x: float -> Derivative
```

**Choice.** `adaptive`: the Gauss–Kronrod (7, 15) rule with QUADPACK's nodes, weights and error estimate (`dqk15`; Piessens et al., *QUADPACK*, 1983; *from documentation*, cited beside the constants), bisecting the panel of largest error estimate, found by a linear scan, up to 200 panels. Infinite bounds map to (0, 1) or (−1, 1) by x = a + t/(1 − t), x = b − (1 − t)/t and x = t/(1 − t²). b < a gives the negated integral over [b, a]; a = b gives 0 with `Converged` and no evaluation; a NaN bound or integrand gives a NaN `Value` and `Converged = false`. Negative or NaN tolerances raise `ArgumentOutOfRangeException`.

**Choice.** `gaussLegendre`: nodes by Newton's method on Pₙ from cos(π(i − ¼)/(n + ½)), computed on each call (O(n²)); `points < 1` raises. `trapezoid` and `simpson` need equal lengths (else `ArgumentException`), give 0 below two points, and leave `xs` unchecked for order (each interval adds its signed width). `simpson` takes pairs of intervals with the uneven-spacing Simpson weights and, for an odd interval count, the last interval from the quadratic through the last three points (Cartwright 2017, as SciPy 1.11 does; *from documentation*); two points give the trapezoid. `cumulativeTrapezoid` returns one value per point, the first 0. `derivative` is Ridders' extrapolation of central differences (Numerical Recipes, 3rd edition, §5.7, `dfridr`: factor 1.4, table of 10, safety 2; *from documentation*) from h = 0.1 · max(1, |x|).

- [ ] **Step 1: Write the failing tests** (`"Calculus"`):

| Test name | Asserts |
|---|---|
| `adaptive meets its tolerance on closed forms` | absolute 1e-12, relative 1e-10, for: x² on [0, 1] = 1/3; sin on [0, π] = 2; exp on [0, 1] = e − 1; e⁻ˣ on [0, ∞) = 1; e^(−x²) on (−∞, ∞) = √π; 1/(1 + x²) on (−∞, ∞) = π; 1/√x on [0, 1] = 2; ln x on [0, 1] = −1; cos(50x) on [0, 1] = sin 50/50. Each: `Converged` and \|Value − exact\| ≤ max(1e-12, 1e-10 · \|exact\|) (spec 5.1) |
| `a polynomial of degree 13 converges on one panel` | x¹³ + x¹² on [0, 1]: `Evaluations = 15`, `Value` within 1e-15 of 1/14 + 1/13 (Gauss 7 is exact to degree 13) |
| `reversed and equal bounds` | `adaptive … f 1.0 0.0` has `Value = -(adaptive … f 0.0 1.0).Value`; `a = b` gives 0, `Converged`, `Evaluations = 0` |
| `a divergent integral reports it` | 1/x on [0, 1]: `Converged = false` and `ErrorEstimate > max(absolute, relative · \|Value\|)` |
| `NaN and a raising integrand` | `fun _ -> nan`: NaN `Value`, `Converged = false`; `failwith "boom"` propagates (Review Focus 3) |
| `Gauss–Legendre is exact to degree 2n − 1` | n in 1, 2, 5, 10, 20, 64: ∫₀¹ x^(2n−1) dx against 1/(2n), `expectClose 1e-14 0.0`; n = 20, eˣ on [−1, 1] against e − 1/e, `expectClose 1e-14 0.0` |
| `trapezoid, simpson and cumulativeTrapezoid` | `trapezoid [\|0.;1.;3.\|] [\|0.;1.;3.\|] = 4.5`; `cumulativeTrapezoid` of the same `= [\|0.;0.5;4.5\|]`; `simpson` of x² on `[\|0.;0.3;1.;1.2;2.\|]` (four intervals) and on `[\|0.;0.5;1.5;2.\|]` (three) against 8/3, `expectClose 1e-14 0.0` (exact for quadratics); x³ on 0, 0.5, …, 2 against 4, `expectClose 1e-14 0.0` |
| `sample rules at the edges` | one point and no points give 0 (`cumulativeTrapezoid` gives `[\|0.\|]` and `[\|\|]`); lengths 3 and 2 raise `ArgumentException` (Review Focus 5) |
| `derivative` | eˣ at 1: `Value` within relative 1e-10 of e and `ErrorEstimate <= 1e-9`; sin at 0 within 1e-12 of 1; x³ at 2 within relative 1e-12 of 12; `fun _ -> nan` gives NaN |

- [ ] **Step 2: Run** the `"Calculus"` filter. Expected: the build fails, `Integrate` is not defined.
- [ ] **Step 3: Implement** `Calculus.fs`; doc comments state the panel limit, the transforms, the cost and the convergence rule.
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): adaptive and fixed quadrature, sampled integrals and Ridders' derivative`.

### Task 11: ODE integration (spec 6.4)

**Files:**
- Create: `src/Ranvier.Numerics/Ode.fs`, `tests/Ranvier.Science.Tests/OdeTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Ode.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`, `tests/Ranvier.Science.Tests/CrossTargetTests.fs`

**Interfaces:**
- Produces:

```fsharp
type OdeOptions =
    { AbsoluteTolerance: float; RelativeTolerance: float; InitialStep: float; MaximumStep: float; MaximumSteps: int }
module OdeOptions =
    val standard : OdeOptions   // { AbsoluteTolerance = 1e-9; RelativeTolerance = 1e-9; InitialStep = 0.0;
                                //   MaximumStep = infinity; MaximumSteps = 100_000 }; InitialStep 0 means chosen by the solver
type OdeSolution = { Times: float[]; States: float[][]; Accepted: int; Rejected: int; Evaluations: int }
module Ode =
    val rungeKutta4Step : f: (float -> float[] -> float[]) -> t: float -> y: float[] -> h: float -> float[]
    val rungeKutta4 : f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> t1: float -> steps: int -> float[]
    val solve : OdeOptions -> f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> t1: float
                -> Result<OdeSolution, string>
    val solveAt : OdeOptions -> f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> times: float[]
                  -> Result<float[][], string>
```

**Choice.** `solve` is Dormand–Prince 5(4) with the coefficients and the dense-output coefficients of Hairer, Nørsett and Wanner, *Solving Ordinary Differential Equations I*, 2nd edition, Table II.5.2 and §II.6 (`dopri5`; *from documentation*, cited beside the constants), first-same-as-last. The error norm is the root mean square of eᵢ/(atol + rtol · max(|yᵢ|, |y_newᵢ|)); a step is accepted when it is at most 1; the next step is h · min(5, max(0.2, 0.9 · err^(−1/5))), with no growth right after a rejection, and a non-finite norm rejects with h/5. `InitialStep = 0` uses Hairer's starting-step algorithm (ibid., §II.4), falling back to 1e-6 · (t1 − t0) when that gives a step that is not finite and positive. `Error "step size underflow at t = …"` when |h| ≤ 4ε|t| or |h| is below 2.2e-308; `Error "MaximumSteps (n) reached at t = …"` when accepted plus rejected steps reach `MaximumSteps`. `Times` holds t0 and every accepted step's end, the last exactly t1; `Evaluations` counts calls of f.

**Choice.** Forward only: `rungeKutta4`, `solve` and `solveAt` raise `ArgumentOutOfRangeException` for t1 < t0, for `times` not ascending or starting below t0, and for options with a negative or NaN tolerance (or both 0), `MaximumStep ≤ 0`, `InitialStep < 0` or `MaximumSteps < 1`; `rungeKutta4` raises for `steps < 1`. `rungeKutta4Step` accepts any h. An f result of the wrong length raises `ArgumentException`. t1 = t0 gives one time and a copy of y0. `solveAt` returns the dense-output states, a copy of y0 at a time equal to t0, and `[||]` for empty `times`; its doc comment states its bound, ten times the tolerance on the tested problems. `rungeKutta4` uses h = (t1 − t0)/steps and tₖ = t0 + k·h.

- [ ] **Step 1: Write the failing tests** (`"Ode"`), with `tight = { OdeOptions.standard with AbsoluteTolerance = 1e-10; RelativeTolerance = 1e-10 }` and `within exact v = |v − exact| ≤ 1e-10 + 1e-10 · |exact|`:

| Test name | Asserts |
|---|---|
| `one RK4 step is the quartic Taylor polynomial` | y' = y from 1 with h = 0.1: `expectClose 1e-15 0.0 (1.0 + h + h*h/2.0 + h*h*h/6.0 + h*h*h*h/24.0)` (closed form of RK4 on a linear equation, *general knowledge*) |
| `RK4 is fourth order` | y' = y on [0, 1]: errors at 10 and 20 steps against e have a ratio in [14, 18] |
| `solve meets the tolerance on closed forms` | `tight`: y' = −y on [0, 5] (e⁻⁵); the oscillator `[\|y₁; −y₀\|]` from `[\|1.;0.\|]` on [0, 2π] (`[\|1.;0.\|]`); the logistic y' = y(1 − y) from 0.1 on [0, 10] (1/(1 + 9e⁻¹⁰)); y' = cos t from 0 on [0, 10] (sin 10): every component `within` (spec 5.1) |
| `the fifth-order rule integrates a quartic exactly` | y' = 5t⁴ from 0 on [0, 1], `MaximumStep = 0.25`: last state within 1e-15 of 1 |
| `dense output reproduces a quartic` | `solveAt` for y' = 4t³ at `[\|0.3;0.77;1.5\|]`: each within relative 1e-14 of t⁴ (a fourth-order continuous extension is exact here) |
| `solveAt on the oscillator` | `tight`, times `[\|0.;0.5;1.;2.;Math.PI;6.\|]`: within 10 · (1e-10 + 1e-10 · \|exact\|) of (cos t, −sin t); the first state equals y0 |
| `the solution's shape` | `Times[0] = t0`, the last `= t1`, ascending; `States.Length = Times.Length`; `States[0]` equals y0 but is another array; y0 unchanged after the call (Review Focus 2); a counting f's count equals `Evaluations`; `Accepted + Rejected ≥ 1` |
| `MaximumStep bounds each step` | `MaximumStep = 0.1` on [0, 1]: every difference of `Times` ≤ 0.1 + 1e-15 |
| `failures give Error` | y' = y² from 1 on [0, 2]: `Error`; the oscillator over [0, 1000] with `MaximumSteps = 10`: `expectError "MaximumSteps"`; `fun _ _ -> [\| nan \|]` with `MaximumSteps = 1000`: `Error` |
| `a raising or misshapen right-hand side` | `failwith "boom"` propagates (Review Focus 3); a result of length 2 for a state of length 1 raises `ArgumentException` (Review Focus 5) |
| `domain` | t1 < t0, unsorted `times`, `times` starting below t0, `steps = 0` and each invalid option raise `ArgumentOutOfRangeException`; t1 = t0 gives `Times = [\|t0\|]` |

- [ ] **Step 2: Write the exact cross-target case** `RK4 gives the same bits on both targets`: `rungeKutta4 (fun _ y -> [| y[1]; -y[0] |]) 0.0 [|1.;0.|] 1.0 100`, both components.
- [ ] **Step 3: Run** the `"Ode"` filter. Expected: the build fails, `Ode` is not defined.
- [ ] **Step 4: Implement** `Ode.fs`; doc comments state the method, the error norm, the `Error` cases, the cost (six evaluations per step after the first) and the bounds with their labels.
- [ ] **Step 5: Run** the `"Ode"` and `"CrossTarget"` filters, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): RK4 and Dormand-Prince ODE integration with dense output`.

### Task 12: `Run.ode` (spec 8.7)

**Files:**
- Modify: `src/Ranvier.Quantities/Run.fs` (the type `OdeState` after `Run<'s>`, ending line 29; `Run.ode` after `Run.create`, lines 93 to 95), `tests/Ranvier.Science.Tests/RunTests.fs` (cases appended to the `"Run"` list)

**Interfaces:**
- Consumes: `Run.create` (`Run.fs:94`), `OdeOptions`, `Ode.solve`, `OdeSolution` (Task 11).
- Produces:

```fsharp
namespace Ranvier.Quantities
type OdeState = { mutable Time: float; mutable State: float[] }
module Run =
    val ode : OdeOptions -> clock: (unit -> float) -> start: (unit -> float[]) -> rhs: (float -> float[] -> float[])
              -> Run<OdeState>
```

**Choice.** `Run.ode` is `Run.create clock start' advance`: `start'` returns `{ Time = 0.0; State = Array.copy (start ()) }`; `advance s gap` calls `Ode.solve options rhs s.Time s.State (s.Time + gap)` and on `Ok` sets `State` to the last state and `Time` to the last time, and on `Error message` raises `InvalidOperationException message`, leaving the state as it was, so the run's failure rules (`Run.fs:21-28`) retry it. Reads inside `rhs` are untracked, as every read inside `advance` is. The doc comment states that `Time` counts from 0 at each start, not from the clock, and that the run is the two memos of `Run.create`.

- [ ] **Step 1: Write the failing tests** (appended to `"Run"`), each in `withGraph`, with `clock = createSignal 0.0`, `k = createSignal 0.5`, `y0 = createSignal 1.0`, `rhs = fun _ (y: float[]) -> [| -k.Value * y[0] |]`, options with both tolerances 1e-10:

| Test name | Asserts |
|---|---|
| `an ode run starts at time 0 with a copy of the start state` | first read: `Time = 0.0`, `State = [\|1.0\|]`; a start returning a held array `a`: mutating `a[0]` after the read leaves `State[0] = 1.0` (Review Focus 2) |
| `a clock gap integrates the state` | clock 0 → 2: `Time = 2.0`, `State[0]` within 1e-9 of `exp -1.0`; then clock → 3: `Time = 3.0`, within 1e-9 of `exp -1.5` (closed form y = e^(−kt)) |
| `a value read by start restarts the run` | `y0` → 4.0: `Time = 0.0`, `State = [\|4.0\|]` |
| `reads inside rhs do not restart the run` | clock → 1, then `k` → 1.0: the next read keeps `Time = 1.0`; clock → 2: `State[0]` within 1e-9 of `exp (-0.5) * exp (-1.0)` |
| `a failed solve fails the read and a later clock retries` | an `rhs` that returns `[\| nan \|]` while a mutable flag is set, options `MaximumSteps = 50`: clock → 1 makes the read raise `InvalidOperationException`; clear the flag, clock → 2: `Time = 2.0`, `State[0]` within 1e-9 of `exp -1.0` (the retry integrates from the last recorded clock, 0, per `Run.fs:25-27`) |

- [ ] **Step 2: Run** the `"Run"` filter. Expected: the build fails, `Run.ode` is not defined.
- [ ] **Step 3: Implement** `OdeState` and `Run.ode` in `Run.fs` (add `open Ranvier.Numerics`).
- [ ] **Step 4: Run** the filter. Expected: all pass, the existing `Run` cases included.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(quantities): a run integrated with Ode.solve`.

### Task 13: Interpolation (spec 6.5)

**Files:**
- Create: `src/Ranvier.Numerics/Interpolate.fs`, `tests/Ranvier.Science.Tests/InterpolateTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Interpolate.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`, `CrossTargetTests.fs`

**Interfaces:**
- Produces:

```fsharp
/// Structural equality; holds copies of the arrays.
type Interpolant      // internal representation: the kind, xs, ys and per-knot slopes (or second derivatives)
module Interpolant =
    val evaluate : Interpolant -> x: float -> float          // NaN outside [xs[0], xs[n − 1]]
    val derivative : Interpolant -> x: float -> float
module Interpolate =
    val linear : xs: float[] -> ys: float[] -> Interpolant
    val naturalCubic : xs: float[] -> ys: float[] -> Interpolant
    val monotoneCubic : xs: float[] -> ys: float[] -> Interpolant     // Fritsch–Carlson
```

**Choice.** `xs` strictly increasing (a NaN breaks it) with at least two points and `ys` of the same length, else `ArgumentException`; NaN in `ys` or a NaN x gives NaN. The interval is found by binary search (O(log n)). `derivative` at an interior knot is the right-hand interval's, at the last knot the left-hand one's, and NaN outside. `naturalCubic` solves its tridiagonal system by the Thomas algorithm. `monotoneCubic` takes the mean of the neighbouring secants as each interior slope (0 where they differ in sign or either is 0), the secant at the ends, and scales a pair by 3/√(α² + β²) when α² + β² > 9 (Fritsch and Carlson, SIAM J. Numer. Anal. 17(2), 1980; *from documentation*). With two points every kind is linear.

- [ ] **Step 1: Write the failing tests** (`"Interpolate"`):

| Test name | Asserts |
|---|---|
| `linear` | xs `[\|0.;1.;3.\|]`, ys `[\|0.;2.;-2.\|]`: `evaluate` 0.5 = 1.0, 2.0 = 0.0, at each knot its y (`Expect.equal`); `derivative` 2.0 = −2.0 |
| `outside the range is NaN` | each kind: `evaluate` at −0.1, 3.1 and `nan` is NaN; at 0 and 3 it is the end y |
| `natural cubic closed form` | xs `[\|0.;1.;2.\|]`, ys `[\|0.;1.;0.\|]`: the spline is −x³/2 + 3x/2 on [0, 1] (M₁ = −3 from M₀ + 4M₁ + M₂ = −12), so `evaluate 0.5 = 0.6875`, `evaluate 1.5 = 0.6875`, `derivative 0.5 = 1.125`, `derivative 1.0 = 0.0`, each `expectClose 1e-15 1e-15` |
| `cubics reproduce lines` | ys = 2x + 1 on xs `[\|0.;0.4;1.7;2.;3.5\|]`: both cubics at 50 points within 1e-14 of 2x + 1 |
| `monotone cubic does not overshoot` | xs 0..5, ys `[\|0.;0.;0.;1.;1.;1.\|]`, 1001 evaluations: each step at least −1e-15, every value in [−1e-15, 1 + 1e-15], within 1e-15 of 0 on [0, 2] and of 1 on [3, 5] |
| `copies and equality` | mutating `xs` and `ys` after construction leaves `evaluate` unchanged (Review Focus 2); two interpolants built from equal arrays are `=` |
| `invalid knots raise` | `ArgumentException` for xs `[\|0.;0.;1.\|]`, `[\|0.;nan;1.\|]`, `[\|1.;0.\|]`, one point, and ys of another length (Review Focus 5) |

- [ ] **Step 2: Write the exact cross-target case** `interpolation gives the same bits on both targets`: xs `[|0.;0.7;1.9;3.2;4.0|]`, ys `[|1.3;-0.4;2.2;0.9;1.7|]`; `naturalCubic` and `monotoneCubic` evaluated at 0.35, 1.1, 2.5, 3.9, and both derivatives at 2.5.
- [ ] **Step 3: Run** the `"Interpolate"` filter. Expected: the build fails, `Interpolate` is not defined.
- [ ] **Step 4: Implement** `Interpolate.fs` with doc comments (domain, NaN rule, costs: O(n) to build, O(log n) to evaluate).
- [ ] **Step 5: Run** the filters, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): linear, natural cubic and monotone cubic interpolation`.

### Task 14: The Fourier transform (spec 6.7)

**Files:**
- Create: `src/Ranvier.Numerics/Fourier.fs`, `tests/Ranvier.Science.Tests/FourierTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Fourier.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`, `CrossTargetTests.fs`

**Interfaces:**
- Consumes: `Generator.create`, `Generator.normals` (0.1.0) in the tests.
- Produces:

```fsharp
module Fourier =
    val forward : re: float[] -> im: float[] -> struct (float[] * float[])     // X_k = Σ_j x_j e^(−2πi jk/n)
    val inverse : re: float[] -> im: float[] -> struct (float[] * float[])     // includes the 1/n
    val realForward : float[] -> struct (float[] * float[])                    // the n/2 + 1 non-negative bins
    val powerSpectrum : float[] -> float[]
    val frequencies : n: int -> spacing: float -> float[]
    val convolve : float[] -> float[] -> float[]                              // linear convolution
```

**Choice.** Twiddles (section 5.3 leaves the option): a table built from (1, 0), (−1, 0) and (0, −1) by half-angle steps, cos(θ/2) = √((1 + cos θ)/2) and sin(θ/2) = sin θ/(2 cos(θ/2)), each stage's twiddles being the previous stage's (even indices) and those times the stage's principal root (odd indices). The table uses only `+ − × ÷ sqrt`, so every power-of-two transform gives the same bits on both targets; its error grows as log₂ n. Other lengths use Bluestein's algorithm with the chirp e^(−iπk²/n) from `Math.Cos` and `Math.Sin` of π · (k² mod 2n)/n, and power-of-two inner transforms. Lengths 0 and 1 return copies.

**Choice.** `realForward x` is the first ⌊n/2⌋ + 1 bins of `forward x (zeros)`. `powerSpectrum x` is re² + im² of those bins, unnormalised. `frequencies n spacing` is the ⌊n/2⌋ + 1 values k/(n · spacing) matching `realForward` (NumPy's `rfftfreq`); n < 1 or spacing ≤ 0 raises `ArgumentOutOfRangeException`. `convolve a b` has length n + m − 1 through a power-of-two transform of at least that length, and is empty when either input is. `re` and `im` of different lengths raise `ArgumentException`.

**Choice.** Bound: each output within 1e-13 · Σⱼ |xⱼ| of the direct sum for n ≤ 4096, pinned below.

- [ ] **Step 1: Write the failing tests** (`"Fourier"`), with the reference `dft` computed in the test as `exactSum` over j of xⱼ times cos and sin of 2π · ((j · k) mod n)/n:

| Test name | Asserts |
|---|---|
| `an impulse transforms to ones` | `[\|1.;0.;0.;0.;0.;0.;0.;0.\|]`: re all 1.0, im all 0.0 (`Expect.equal`) |
| `a constant and a cosine` | 16 ones: re[0] = 16.0; every other bin within absolute 1e-14; xⱼ = cos(2π · 3j/16): bins 3 and 13 within 1e-13 of 8, the rest within 1e-13 of 0 (closed forms) |
| `forward matches the direct sum` | n in 1, 2, 3, 5, 8, 12, 17, 64, 100, 1000, 1024, 4095 (Bluestein) and 4096 (radix-2), the top of the stated domain n ≤ 4096, re and im from `Generator.normals 0.0 1.0 n (Generator.create n)` and seed n + 1: every bin within 1e-13 · Σⱼ (\|reⱼ\| + \|imⱼ\|) of `dft` |
| `inverse undoes forward` | the same inputs: `inverse (forward x)` within 1e-13 · max \|xⱼ\| of x |
| `Parseval` | Σ\|x\|² against (1/n) Σ\|X\|²: `expectClose 1e-13 0.0` for n = 1000 and 1024 |
| `real transforms and frequencies` | for n = 7 and 8: `realForward x` equals the first ⌊n/2⌋ + 1 bins of `forward x zeros`, and `powerSpectrum x` equals re² + im² of them (`Expect.equal`); `frequencies 8 0.5 = [\|0.;0.25;0.5;0.75;1.0\|]`; `frequencies 7 1.0` has 4 values |
| `convolve` | `[\|1.;2.;3.\|]` with `[\|0.;1.;0.5\|]` within 1e-14 of `[\|0.;1.;2.5;4.;1.5\|]`; lengths 300 and 200 against the direct double sum within 1e-12 · Σ\|a\| · max\|b\|; an empty input gives `[\|\|]` |
| `lengths` | `forward [\|1.;2.\|] [\|0.\|]` raises `ArgumentException` (Review Focus 5); `forward [\|\|] [\|\|]` gives two empty arrays; the inputs are unchanged after each call |

- [ ] **Step 2: Write the exact cross-target case** `power-of-two butterflies give the same bits on both targets`: `forward [|0.1;0.7;-1.3;2.9;0.25;-0.6;1.1;3.3|] [|0.;0.5;0.;-0.25;0.;0.;1.;0.|]`, all 16 output values.
- [ ] **Step 3: Run** the `"Fourier"` filter. Expected: the build fails, `Fourier` is not defined.
- [ ] **Step 4: Implement** `Fourier.fs`; doc comments state the sign convention, the bound with its label and the costs (O(n log n), Bluestein about three transforms of length ≥ 2n − 1).
- [ ] **Step 5: Run** the filters, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): Fourier transform of any length, real spectra and convolution`.

### Task 15: `Comoments` (spec 6.8, first part)

**Files:**
- Create: `src/Ranvier.Numerics/Comoments.fs`, `tests/Ranvier.Science.Tests/ComomentsTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Comoments.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`, `CrossTargetTests.fs`

**Interfaces:**
- Consumes: `Sum` (0.1.0).
- Produces:

```fsharp
/// Count, means, spreads and co-moment of a set of (x, y) pairs. Immutable.
[<Struct; NoComparison>]
type Comoments           // private representation: N, the two means and the three second moments, each a Sum
module Comoments =
    val zero : Comoments
    val add : compensated: bool -> Comoments -> x: float -> y: float -> Comoments
    val remove : compensated: bool -> Comoments -> x: float -> y: float -> Comoments
    val merge : compensated: bool -> Comoments -> Comoments -> Comoments
    val unmerge : compensated: bool -> whole: Comoments -> part: Comoments -> Comoments
    val count : Comoments -> float
    val meanX : Comoments -> float
    val meanY : Comoments -> float
    val varianceX : Comoments -> float            // population, divisor n
    val varianceY : Comoments -> float
    val covariance : Comoments -> float           // population, divisor n
    val sampleCovariance : Comoments -> float     // divisor n − 1; NaN below 2
    val correlation : Comoments -> float          // C / √(M2x · M2y); NaN when either spread is 0
    val slope : Comoments -> float                // C / M2x, y on x; NaN when M2x is 0
    val intercept : Comoments -> float            // meanY − slope · meanX
    val internal isFinite : Comoments -> bool     // count 0, or every running total finite; for Task 19
```

**Choice.** Reads are NaN on an empty accumulator; the update, merge and unmerge follow `Moments.fs` (Welford per pair, Chan's merge, and their inverses), with the co-moment term C += δx · (y − meanY_new) and, on merge, C = Cₐ + C_b + δx · δy · nₐ n_b / n (Pébay 2008, SAND2008-6212; *general knowledge*). `merge` returns the other accumulator itself when one is empty, and `unmerge` returns `whole` itself for an empty part, as `Moments` does. Correlation uses `sqrt` only, so the whole type is an exact kernel.

- [ ] **Step 1: Write the failing tests** (`"Comoments"`), with pairs x = `[|1.;2.;3.;4.|]`, y = `[|2.;4.;5.;9.|]` (means 2.5 and 5, Σ(x − x̄)² = 5, Σ(y − ȳ)² = 26, Σ(x − x̄)(y − ȳ) = 11):

| Test name | Asserts |
|---|---|
| `closed-form reads` | `count = 4.0`; `meanX 2.5`, `meanY 5.0`, `varianceX 1.25`, `varianceY 6.5`, `covariance 2.75`, `sampleCovariance` 11/3, `correlation` 11/√130, `slope` 2.2, `intercept` −0.5: each `expectClose 4e-15 4e-15` (closed forms) |
| `merge, unmerge and remove agree with one fold` | 1000 pairs xₖ = √k + 0.1, yₖ = 0.3k − √(k + 2): merge of halves, `unmerge (merge a b) b` against a, and removing the last 500 in reverse against the fold of the first 500; means within relative 1e-12, the second moments within relative 1e-9 |
| `a large offset keeps the covariance` | x = 1e9 + k, y = 2·10⁹ + 3k, k = 0..999: `covariance` within relative 1e-9 of 3 · 83333.25 |
| `empty, single and NaN` | `zero`: every read NaN, `count = 0.0`; one pair: `covariance = 0.0`, `sampleCovariance` NaN, `correlation` NaN; a NaN x gives NaN means and covariance and raises nothing |
| `removal from empty raises` | `Expect.throwsT<InvalidOperationException>` for `remove` on `zero` and for `unmerge true zero (add true zero 1.0 2.0)` |
| `removing everything returns zero` | add then remove three pairs: the result `= Comoments.zero` |

- [ ] **Step 2: Write the exact cross-target case** `comoments give the same bits on both targets`: the pairs (`Recorded.stream[i]`, `Recorded.stream[999 − i]`), folded with `add true`: `count`, `meanX`, `meanY`, `covariance`, `correlation`, `slope`.
- [ ] **Step 3: Run** the `"Comoments"` filter. Expected: the build fails, `Comoments` is not defined.
- [ ] **Step 4: Implement** `Comoments.fs`, with doc comments in the style of `Moments.fs`.
- [ ] **Step 5: Run** the filters, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): bivariate comoments with removal and merge`.

### Task 16: `HigherMoments` and `WeightedMoments` (spec 6.8, second part)

**Files:**
- Create: `src/Ranvier.Numerics/HigherMoments.fs`, `src/Ranvier.Numerics/WeightedMoments.fs`, `tests/Ranvier.Science.Tests/HigherMomentsTests.fs`, `tests/Ranvier.Science.Tests/WeightedMomentsTests.fs`
- Modify: `src/Ranvier.Numerics/Moments.fs` (one internal constructor), `Ranvier.Numerics.fsproj` (both files, in that order, immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`, `CrossTargetTests.fs`

**Interfaces:**
- Consumes: `Sum`, `Moments` with T1's `Moments.sampleVariance`.
- Produces:

```fsharp
module Moments = val internal ofState : count: float -> mean: Sum -> m2: Sum -> Moments

[<Struct; NoComparison>]
type HigherMoments       // private: N, mean, M2, M3, M4, each total a Sum
module HigherMoments =
    val zero : HigherMoments
    val add : compensated: bool -> HigherMoments -> float -> HigherMoments
    val remove : compensated: bool -> HigherMoments -> float -> HigherMoments
    val merge : compensated: bool -> HigherMoments -> HigherMoments -> HigherMoments
    val unmerge : compensated: bool -> whole: HigherMoments -> part: HigherMoments -> HigherMoments
    val count : HigherMoments -> float
    val mean : HigherMoments -> float
    val variance : HigherMoments -> float          // M2/n
    val sampleVariance : HigherMoments -> float    // M2/(n − 1); NaN below 2
    val skewness : HigherMoments -> float          // √n · M3 / (M2 · √M2); NaN when M2 is 0
    val kurtosis : HigherMoments -> float          // excess, n · M4 / M2² − 3; NaN when M2 is 0
    val toMoments : HigherMoments -> Moments
    val internal isFinite : HigherMoments -> bool        // count 0, or every running total finite; for Task 19

[<Struct; NoComparison>]
type WeightedMoments     // private: N, W, W2, mean, S, each total a Sum
module WeightedMoments =
    val zero : WeightedMoments
    val add : compensated: bool -> WeightedMoments -> x: float -> w: float -> WeightedMoments
    val remove : compensated: bool -> WeightedMoments -> x: float -> w: float -> WeightedMoments
    val merge : compensated: bool -> WeightedMoments -> WeightedMoments -> WeightedMoments
    val unmerge : compensated: bool -> whole: WeightedMoments -> part: WeightedMoments -> WeightedMoments
    val count : WeightedMoments -> float                 // observations, zero weights included
    val totalWeight : WeightedMoments -> float           // W = Σw
    val mean : WeightedMoments -> float                  // Σwx / W
    val variance : WeightedMoments -> float              // S / W
    val frequencyVariance : WeightedMoments -> float     // S / (W − 1)
    val reliabilityVariance : WeightedMoments -> float   // S / (W − W2/W), W2 = Σw²
    val effectiveCount : WeightedMoments -> float        // W² / W2 (Kish)
    val internal isFinite : WeightedMoments -> bool      // count 0, or every running total finite; for Task 19
```

**Choice.** `HigherMoments` uses Pébay's update and merge formulas to the fourth moment (SAND2008-6212, equations 2.1 to 2.3; *general knowledge*), `add` as the merge of a singleton, and `unmerge` and `remove` as the merge solved for the remaining part in the order M2, M3, M4. The reads take √ through `sqrt`, never `**`. `WeightedMoments` uses West's update (Comm. ACM 22(9), 1979; *general knowledge*): a weight 0 counts the observation and leaves the other totals; a negative weight raises `ArgumentOutOfRangeException`; a NaN weight gives NaN reads. Reads are NaN on an empty accumulator and where their divisor is 0.

- [ ] **Step 1: Write the failing tests** (`"HigherMoments"` and `"WeightedMoments"`):

| Test name | Asserts |
|---|---|
| `higher moments closed form` | `[\|1.;2.;3.;4.;10.\|]` (mean 4; M2 50, M3 180, M4 1394): `mean 4`, `variance 10`, `sampleVariance 12.5`, `skewness` = `sqrt 5.0 * 180.0 / (50.0 * sqrt 50.0)`, `kurtosis` −0.212: `expectClose 1e-14 1e-14` (closed forms) |
| `a large offset keeps the shape` | the same data plus 1e6: `skewness` and `kurtosis` within relative 1e-8 of the unshifted |
| `higher moments merge, unmerge and remove` | as in `Comoments`: mean within relative 1e-12, variance 1e-9, skewness and kurtosis within absolute 1e-8 |
| `toMoments matches Moments` | 1000 values: `Moments.mean` within 1e-15 relative, `Moments.variance` within 1e-14 relative of a direct `Moments` fold, counts equal |
| `weighted closed form` | x `[\|1.;2.;4.\|]`, w `[\|1.;2.;1.\|]` (W 4, W2 6, mean 2.25, S 4.75): `count 3`, `totalWeight 4`, `mean 2.25`, `variance 1.1875`, `frequencyVariance` 4.75/3, `reliabilityVariance 1.9`, `effectiveCount` 16/6: `expectClose 1e-15 1e-15` |
| `unit weights are plain moments` | 1000 values with w = 1: `variance` and `frequencyVariance` within relative 1e-12 of `Moments.variance` and `Moments.sampleVariance` |
| `zero, negative and NaN weights` | a weight 0 adds 1 to `count` and changes neither `totalWeight` nor `mean`; −1 raises `ArgumentOutOfRangeException`; NaN gives NaN `mean` |
| `weighted merge, unmerge and remove` | as above, tolerances 1e-12 and 1e-9 |
| `empty and removal from empty` | both types: reads NaN on `zero`; `remove` and `unmerge` on empty raise `InvalidOperationException` |

- [ ] **Step 2: Write two exact cross-target cases**: `higher moments give the same bits on both targets` (`Recorded.stream` folded with `add true`: `mean`, `variance`, `skewness`, `kurtosis`), and `weighted moments give the same bits on both targets` (xᵢ = `Recorded.stream[i]`, wᵢ = `float (i % 5)`: `totalWeight`, `mean`, `variance`, `reliabilityVariance`, `effectiveCount`).
- [ ] **Step 3: Run** both filters. Expected: the build fails, `HigherMoments` is not defined.
- [ ] **Step 4: Implement** `Moments.ofState` and the two files, with doc comments in the style of `Moments.fs`.
- [ ] **Step 5: Run** the filters and `"CrossTarget"`, paste the exact-case literals. Expected: all pass on .NET.
- [ ] **Step 6: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 7: Commit** `feat(numerics): higher and weighted moments with removal and merge`.

### Task 17: `Histogram` and `QuantileEstimate` (spec 6.8, third part)

**Files:**
- Create: `src/Ranvier.Numerics/Histogram.fs`, `tests/Ranvier.Science.Tests/HistogramTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Histogram.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Consumes: `OrderStatistic.quantileSorted` (Plan T1, Task 14; spec section 6.13).
- Produces:

```fsharp
/// Counts over fixed edges; structural equality.
type Histogram           // private: Edges (shared, never written), Counts: int[], Underflow, Overflow, Missing
module Histogram =
    val create : edges: float[] -> Histogram
    val uniform : lower: float -> upper: float -> bins: int -> Histogram
    val add : Histogram -> float -> Histogram
    val remove : Histogram -> float -> Histogram
    val merge : Histogram -> Histogram -> Histogram
    val unmerge : whole: Histogram -> part: Histogram -> Histogram
    val edges : Histogram -> float[]
    val counts : Histogram -> int[]
    val underflow : Histogram -> int
    val overflow : Histogram -> int
    val missing : Histogram -> int            // NaN observations
    val total : Histogram -> int              // every observation: in range, under, over and missing
type QuantileEstimate    // the P² algorithm (Jain and Chlamtac, Comm. ACM 28(10), 1985); add only
module QuantileEstimate =
    val create : p: float -> QuantileEstimate
    val add : QuantileEstimate -> float -> QuantileEstimate
    val count : QuantileEstimate -> float
    val value : QuantileEstimate -> float     // exact type-7 below five observations; NaN when empty
```

**Choice.** Bins follow NumPy's rule: [eᵢ, eᵢ₊₁) half-open, the last [eₙ₋₁, eₙ] closed; found by binary search. `add` and `remove` copy the counts, O(bins) each. `create` raises `ArgumentException` unless the edges are finite, strictly increasing and at least two, and copies them; `uniform` raises `ArgumentOutOfRangeException` unless lower < upper, both finite and bins ≥ 1, and its last edge is exactly `upper`. `remove` of a value whose bin (or under, over or missing count) is 0 raises `InvalidOperationException`, as does `unmerge` when a count would go negative; `merge` and `unmerge` raise `ArgumentException` unless the edges are equal. `QuantileEstimate.create` raises unless 0 < p < 1; below five observations `value` is `OrderStatistic.quantileSorted p` of the sorted observations (type 7; no second copy of the interpolation); from the fifth observation `value` is the middle marker's height; a NaN observation makes `value` NaN from then on; its state is copied per `add` (15 numbers).

- [ ] **Step 1: Write the failing tests** (`"Histogram"`):

| Test name | Asserts |
|---|---|
| `NumPy's bin rule` | edges `[\|0.;1.;2.;3.\|]`, add 0, 1, 3, 3.0000001, −0.1, `nan`: `counts = [\|1;1;1\|]`, `overflow 1`, `underflow 1`, `missing 1`, `total 6` |
| `uniform edges` | `uniform 0.0 1.0 4` has edges `[\|0.;0.25;0.5;0.75;1.0\|]`; `uniform 0.0 0.3 3`'s last edge is exactly 0.3 |
| `remove, merge and unmerge` | removing an added value restores the earlier histogram (`=`); `merge` sums every count; `unmerge (merge a b) b = a` |
| `invalid use raises` | `create [\|1.;1.\|]`, `create [\|0.\|]`, `create [\|0.;nan\|]` raise `ArgumentException`; `uniform 1.0 0.0 3` and `uniform 0.0 1.0 0` raise `ArgumentOutOfRangeException`; removing from an empty bin raises `InvalidOperationException`; merging different edges raises `ArgumentException` |
| `copies in and out` | mutating the edges array after `create`, and mutating the arrays returned by `edges` and `counts`, leave the histogram unchanged (Review Focus 2) |
| `the quantile estimate is exact below five` | p = 0.25 over 3, 1, 2: `value` 1.5 (type 7: h = 2 · 0.25 = 0.5 between 1 and 2); `count 3.0`; empty is NaN |
| `the quantile estimate at five` | p = 0.5 over 5, 1, 4, 2, 3: `value = 3.0` (the markers are the sorted observations) |
| `the quantile estimate converges` | 100 000 `Generator.uniform` values (seed 11): p = 0.5 within 0.01 of 0.5 and p = 0.9 within 0.01 of 0.9; 100 000 `Generator.normal` values (seed 12), p = 0.975 within 0.03 of 1.9599639845400538 (the normal's 0.975 quantile; the shorter 1.959963984540054 reads as the double one ulp above, so paste the script's literal as printed: the literal printed by `python -I tests/reference/normal_quantile.py`, Plan T1, Task 14, a 60-digit decimal bisection on the exact double 0.975; the command is written beside the literal) |
| `the quantile estimate's domain` | p = 0, 1 and NaN raise `ArgumentOutOfRangeException`; a NaN observation makes `value` NaN after 10 later finite ones |

- [ ] **Step 2: Run** the `"Histogram"` filter. Expected: the build fails, `Histogram` is not defined.
- [ ] **Step 3: Implement** `Histogram.fs` with doc comments (the bin rule, the costs, the raise conditions).
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): histograms and the P-squared quantile estimate`.

### Task 18: `DecayedMoments`, `Window` and `TimeWindow` (spec 6.8, fourth part)

**Files:**
- Create: `src/Ranvier.Numerics/Windows.fs`, `tests/Ranvier.Science.Tests/WindowTests.fs`
- Modify: `Ranvier.Numerics.fsproj` (`Windows.fs` immediately before `FloatPolicy.fs`), `TestFiles.props`, Fable `Main.fs`

**Interfaces:**
- Consumes: `Moments.add`, `Moments.remove`, `Moments.zero`.
- Produces:

```fsharp
[<Struct; NoComparison>]
type DecayedMoments      // private: HalfLife, Last (NaN before the first item), Weight, Mean, S
module DecayedMoments =
    val create : halfLife: float -> DecayedMoments
    val addAt : DecayedMoments -> t: float -> x: float -> DecayedMoments
    val addItem : DecayedMoments -> x: float -> DecayedMoments
    val weight : DecayedMoments -> float
    val mean : DecayedMoments -> float
    val variance : DecayedMoments -> float
[<NoEquality; NoComparison>]
type Window<'T, 'A>      // private: capacity, add, remove, zero, value, a two-list queue of items, count, removals
module Window =
    val create : capacity: int -> add: ('A -> 'T -> 'A) -> remove: ('A -> 'T -> 'A) -> zero: 'A -> Window<'T, 'A>
    val push : Window<'T, 'A> -> 'T -> Window<'T, 'A>
    val value : Window<'T, 'A> -> 'A
    val count : Window<'T, 'A> -> int
    val items : Window<'T, 'A> -> 'T[]                 // oldest first
    val moments : compensated: bool -> capacity: int -> Window<float, Moments>
[<NoEquality; NoComparison>]
type TimeWindow<'T, 'A>
module TimeWindow =
    val create : span: float -> add: ('A -> 'T -> 'A) -> remove: ('A -> 'T -> 'A) -> zero: 'A -> TimeWindow<'T, 'A>
    val push : TimeWindow<'T, 'A> -> t: float -> 'T -> TimeWindow<'T, 'A>
    val value : TimeWindow<'T, 'A> -> 'A
    val count : TimeWindow<'T, 'A> -> int
    val items : TimeWindow<'T, 'A> -> 'T[]             // oldest first
    val moments : compensated: bool -> span: float -> TimeWindow<float, Moments>
```

**Choice.** `DecayedMoments` takes no `compensated` argument: the spec's table fixes `addAt s t x` and `addItem s x`, and a decayed total has no exact value to compensate towards. `addAt` multiplies the weight and S by 2^(−(t − Last)/halfLife) when t > Last, then adds x with weight 1 by West's update; an earlier t applies no decay and leaves `Last`; a NaN t gives NaN reads. `addItem` is `addAt` at `Last + 1`, the first item at 0. `create` raises `ArgumentOutOfRangeException` unless halfLife > 0 (infinity means no decay). Reads: `weight` 0 and `mean`, `variance` NaN when empty.

**Choice.** `Window` and `TimeWindow` hold functions, so they have no equality (`[<NoEquality; NoComparison>]`); a reader cuts off by returning the previous instance. A push to a full window removes the oldest item, then adds the new one. Each re-folds its value from `zero` over its items once its removals since the last re-fold reach its item count (amortised O(1) per push, spec 6.8); the items live in a front list and a reversed back list. `TimeWindow.push` drops the items older than t − span (it keeps tᵢ ≥ t − span) and raises `ArgumentOutOfRangeException` for a t below the latest or NaN; `create` raises unless span ≥ 0; `Window.create` raises unless capacity ≥ 1.

- [ ] **Step 1: Write the failing tests** (`"Window"`):

| Test name | Asserts |
|---|---|
| `decayed moments closed form` | half-life 1, `addAt 0.0 1.0` then `addAt 1.0 3.0` (weights ½ and 1): `weight 1.5`, `mean` 7/3, `variance` 8/9: `expectClose 1e-15 1e-15` (closed form); `addItem` 1.0 then 3.0 gives the same |
| `an earlier time applies no decay` | after `addAt 5.0 1.0`, `addAt 3.0 3.0`: `weight 2.0`, `mean 2.0` |
| `decayed moments domain` | half-life 0, −1 and NaN raise; empty: `weight 0.0`, `mean` NaN; a NaN t gives NaN `mean` |
| `a window keeps the last capacity items` | `Window.moments true 3`, push 1..5: `items = [\|3.;4.;5.\|]`, `count 3`, `Moments.mean (value w) = 4.0` |
| `re-folds bound drift` | `Window.moments true 50`, 100 000 pushes of 1e9 + (i % 7): `Moments.mean` within relative 1e-12 and `Moments.variance` within relative 1e-9 of a fresh fold of `items`; with `false`, the variance within relative 1e-5 |
| `work per push is amortised constant` | capacity 10 and counting `add` and `remove`, 1000 pushes: adds ≤ 2 · 1000 + 10 and removes ≤ 1000 |
| `a window is persistent` | pushing twice onto one window value gives two windows whose `items` differ, and the first value's `items` is unchanged |
| `copies out` | mutating the array from `items` leaves the next `items` unchanged (Review Focus 2) |
| `a time window keeps items no older than span` | span 10, pushes at t = 0, 5, 10, 15: `items` holds the items of 5, 10 and 15; an equal time is accepted |
| `a time window's moments, value and count` | `TimeWindow.moments true 10.0`, pushes at t = 0, 5, 10, 15 of 1.0, 2.0, 3.0, 4.0 (items kept: tᵢ ≥ t − span = 5): `TimeWindow.count = 3`; `items = [\|2.;3.;4.\|]`; `TimeWindow.value` has `Moments.count 3.0`, `Moments.mean` 3.0 and `Moments.variance` within 1e-15 of 2/3 (closed form of 2, 3, 4); after a further push at t = 40 of 9.0, `count = 1` and `Moments.mean (value w) = 9.0`; an empty window has `count = 0` and `Moments.count (value w) = 0.0` |
| `a time window's domain` | a time below the latest, NaN, span −1, and `Window.create 0` raise `ArgumentOutOfRangeException` |

- [ ] **Step 2: Run** the `"Window"` filter. Expected: the build fails, `DecayedMoments` is not defined.
- [ ] **Step 3: Implement** `Windows.fs` with doc comments (the re-fold rule and its cost, the decay rule and its earlier-time case, the cut-off note for windows).
- [ ] **Step 4: Run** the filter. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): decayed moments and count and time windows`.

### Task 19: Maintained aggregates (spec 6.9)

**Files:**
- Modify: `src/Ranvier.Numerics/Aggregate.fs` (`maintain`, lines 32 to 89, becomes public `maintainedBy`; new functions after `momentsFromScratchBy`), `tests/Ranvier.Science.Tests/AggregateTests.fs` (cases appended to the `"Aggregate"` list, reusing its `rowsOf`, `keyOf` and `valueOf`)

**Interfaces:**
- Consumes: `Comoments` and its internal `isFinite` (Task 15), `HigherMoments`, `WeightedMoments` and their internal `isFinite` (Task 16), `Histogram` (Task 17), `OrderStatistic.quantileSorted` (Plan T1, Task 14); core 0.2.1 `Projection.foldGroupBy`, `Projection.sortBy`, `Projection.Keys`, `Projection.Get`, `createMemo`, `createProjectionOnce`.
- Produces:

```fsharp
module Aggregate =
    val maintainedBy :
        FloatPolicy -> finite: ('A -> bool) -> add: ('A -> 'U -> 'A) -> remove: ('A -> 'U -> 'A) -> zero: 'A
        -> result: ('A -> 'T) -> projection: ('V -> 'U) -> Projection<'K, 'V> -> Maintained<'T>
    val comomentsBy : FloatPolicy -> ('V -> Comoments) -> Projection<'K, 'V> -> Maintained<Comoments>
    val higherMomentsBy : FloatPolicy -> ('V -> HigherMoments) -> Projection<'K, 'V> -> Maintained<HigherMoments>
    val weightedMomentsBy : FloatPolicy -> ('V -> WeightedMoments) -> Projection<'K, 'V> -> Maintained<WeightedMoments>
    val histogramBy : edges: float[] -> ('V -> float) -> Projection<'K, 'V> -> Memo<Histogram>
    val quantileBy : p: float -> ('V -> float) -> Projection<'K, 'V> -> Memo<float>
```

**Choice.** `comomentsBy`, `higherMomentsBy` and `weightedMomentsBy` are `maintainedBy` with the type's `merge` and `unmerge` under `policy.Compensated`, its `zero`, `id` as the result, and the type's internal `isFinite` as `finite` (a read such as `skewness` is NaN for a finite state, so the reads cannot serve). `histogramBy` is `Projection.foldGroupBy projection Histogram.add Histogram.remove (Histogram.create edges) id`, with no re-fold. `quantileBy` raises `ArgumentOutOfRangeException` unless 0 ≤ p ≤ 1, sorts with `Projection.sortBy projection`, and is one memo reading the sorted `Keys` and at most three rows: NaN when empty or when the last value (NaN sorts last) is NaN, else the type-7 value xₗ + (h − l)(xₗ₊₁ − xₗ) with h = (n − 1)p, l = ⌊h⌋, taken from the one implementation of the interpolation: the read gathers xₗ and xₗ₊₁ (only xₗ when l = n − 1) and returns `OrderStatistic.quantileSorted (h − l) [| xₗ; xₗ₊₁ |]`, so the read stays O(1) and no copy of the interpolation remains. Its doc comment states the per-change cost from core 0.2.1's `sortBy` doc comment (`Combinators.fs`, `sortBy`: O(1) neighbour checks and a binary search and shift per touched position, O(N log N) on dense bursts; *from documentation*) plus O(1) for the read.

**Choice.** Core 0.2.1 raises `NotSupportedException` for a `createProjectionOnce` projection in every `Projection` combinator, `foldGroupBy` included (`Combinators.fs`, the `RequireRowMemos` call in `foldGroupBy`; `Api.fs`, the remarks of `createProjectionOnce`), so every aggregate of this module accepts the same projections. Each doc comment says so once; the spec's sentence that `quantileBy` accepts fewer is superseded.

- [ ] **Step 1: Write the failing tests** (appended to `"Aggregate"`), each in `withGraph` over `rowsOf`, with `FloatPolicy.fast` or `FloatPolicy.standard |> FloatPolicy.withRefoldFloor 0` named explicitly (the counts must not depend on T1's floor):

| Test name | Asserts |
|---|---|
| `maintainedBy keeps a custom accumulator` | a (count, sum) pair as `'A`, rows 0..99: after 50 edits and 10 removals equal to the from-scratch pair; `Refolds` after 100 edits under `withRefoldFloor 0` is at least 1 |
| `comomentsBy follows row edits` | rows holding (x, y) projected through `Comoments.add true Comoments.zero x y`: after edits and removals, `covariance` and `slope` within relative 1e-12 of a fold of the current rows |
| `higherMomentsBy and weightedMomentsBy follow row edits` | likewise: `skewness` within absolute 1e-10, weighted `mean` and `variance` within relative 1e-12 |
| `histogramBy counts exactly` | edges `[\|0.;1.;2.;3.\|]` over 20 rows: after edits and removals the memo's value `=` `Histogram` built from the current rows |
| `quantileBy is the type-7 quantile of the current rows` | p = 0.25, 0.5, 0.9 over 101 rows, then after 30 edits, 10 insertions and 10 removals: equal (`Expect.equal`) to the type-7 quantile of the sorted current values, computed in the test by the same formula xₗ + (h − l)(xₗ₊₁ − xₗ) |
| `quantileBy at the edges` | empty rows: NaN; a row holding NaN: NaN; p = −0.1 and 1.1 raise `ArgumentOutOfRangeException` |
| `quantileBy cuts off an unchanged value` | an effect reading the median of 11 rows runs once more after an edit that moves the median, and not after an edit of the largest row to a larger value (a `float` memo cuts off on both targets) |
| `a projection from createProjectionOnce is refused` | `createProjectionOnce ProjectionOptions.None …`: `quantileBy` and `momentsBy` both raise `NotSupportedException` |

- [ ] **Step 2: Run** the `"Aggregate"` filter. Expected: the build fails, `Aggregate.maintainedBy` is not defined.
- [ ] **Step 3: Implement**: rename `maintain` to `maintainedBy`, make it public with a doc comment (its re-fold rule as `sumBy` states it), point `sumBy` and `momentsBy` at it, and add the five functions with doc comments (node count: one fold memo each, `quantileBy` a sort view and one memo; the publish note of Global Constraints for every result but `quantileBy`'s).
- [ ] **Step 4: Run** the filter. Expected: all pass, the existing `Aggregate` cases included.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing.
- [ ] **Step 6: Commit** `feat(numerics): maintained comoments, higher and weighted moments, histograms and quantiles`.

### Task 20: Random variates and the plan's closing measurement (spec 6.10, section 11)

**Files:**
- Modify: `src/Ranvier.Numerics/Generator.fs`, `tests/Ranvier.Science.Tests/GeneratorTests.fs` (cases appended to `"Generator"`), `docs/measurements/gate.md` (a row appended; the file is created by Plan T1, Task 15)

**Interfaces:**
- Consumes: `Generator.uniform`, `Generator.normal`, the private `step` (`Generator.fs:21`); in the tests `Special.gammaUpperRegularized`, `Special.gammaLowerRegularized`, `Special.betaRegularized`, `Special.logFactorial`, `Special.logChoose` (Tasks 1 to 4) and `Comoments` (Task 15).
- Produces:

```fsharp
module Generator =
    val integer : lower: int -> upperExclusive: int -> Generator -> struct (int * Generator)   // unbiased
    val exponential : rate: float -> Generator -> struct (float * Generator)
    val gamma : shape: float -> scale: float -> Generator -> struct (float * Generator)        // Marsaglia–Tsang
    val beta : a: float -> b: float -> Generator -> struct (float * Generator)
    val binomial : trials: int -> p: float -> Generator -> struct (int * Generator)
    val poisson : mean: float -> Generator -> struct (int * Generator)
    val shuffle : 'T[] -> Generator -> struct ('T[] * Generator)      // returns a copy
    val split : Generator -> struct (Generator * Generator)
```

(`binomial` precedes `poisson` in the file: `poisson` calls it.)

**Choice.** Methods, each exact in distribution (spec 6.10):
- `integer`: rejection on 32-bit outputs in `float` arithmetic, r = float upper − float lower, accept a draw b < 2³² − (2³² mod r), return `int (float lower + b % r)`; expected draws below 2; raises unless lower < upperExclusive. It uses only exact operations, so its outputs are the same on both targets.
- `exponential`: −ln(1 − u)/rate. `gamma`: Marsaglia and Tsang, ACM TOMS 26(3), 2000 (*from documentation*), with the boost Γ(shape + 1) · u^(1/shape) below shape 1. `beta`: X/(X + Y) of two gamma deviates, taken in log space (ln X = ln G(a + 1) + ln u / a) when a or b is below 1, so the result lies in [0, 1] and is never NaN.
- `binomial`: Knuth's order-statistic split (TAOCP vol. 2, 3rd edition, §3.4.1; *from documentation*): while trials > 64, draw X ~ Beta(a, trials + 1 − a) with a = 1 + trials/2, continue on (a − 1, p/X) when X ≥ p, else add a and continue on (trials − a, (p − X)/(1 − X)); then count Bernoulli trials. Expected cost O(log trials) beta deviates and at most 64 uniforms.
- `poisson`: the same section's gamma split: while the mean exceeds 16, draw X ~ Gamma(m) with m = ⌊7·mean/8⌋, add m and continue on mean − X when X < mean, else return the count plus `binomial (m − 1) (mean / X)`; below 16 multiply uniforms (1 − u) until the product falls below e^(−mean).
- `shuffle`: Fisher–Yates with `integer`. `split`: eight outputs of the given generator become the four words of each of two generators, each then warmed by 20 steps as `create` does; the doc comment states that no independence proof backs it.
- Domains: rate > 0, shape > 0, scale > 0, a > 0, b > 0 else `ArgumentOutOfRangeException`, and a NaN parameter raises it too (a NaN configuration parameter raises, as an out-of-domain value does: spec section 3.2). `binomial` needs trials ≥ 0 and p in [0, 1], `poisson` 0 ≤ mean ≤ 2³⁰, NaN raising for both.

- [ ] **Step 1: Write the failing tests** (appended to `"Generator"`), each with its own seed, n = 20 000 unless named; `ks cdf xs` is the Kolmogorov–Smirnov statistic computed in the test file:

| Test name | Asserts |
|---|---|
| `integer is uniform on its range` | `integer 0 6`, 60 000 draws: all in [0, 6); chi-squared against 10 000 each, df 5, p-value ≥ 0.001 |
| `integer covers the whole int range` | `integer Int32.MinValue Int32.MaxValue`, 1000 draws: all ≥ `Int32.MinValue` and < `Int32.MaxValue`, some negative and some positive |
| `the first integers of seed 1 are pinned` | the first eight of `integer 0 1000` from `create 1`: `Expect.equal` to eight literals printed by the .NET run and then checked under mocha (exact on both targets) |
| `exponential` | rate 2: ks against 1 − e^(−2x) ≤ 1.95/√n (closed form) |
| `gamma` | (shape, scale) in (0.3, 2), (1, 2), (2.5, 2), (30, 2): ks against `gammaLowerRegularized shape (x / scale)` ≤ 1.95/√n |
| `beta` | (0.5, 0.5) and (2, 5): ks against `betaRegularized a b` ≤ 1.95/√n |
| `poisson` | means 0.5, 5, 15.9, 16, 100: chi-squared against e^(k ln μ − μ − logFactorial k), cells pooled until each expects at least 5, p-value ≥ 0.001; mean 1e4: sample mean within 4√(1e4/n) of 1e4 and sample variance within 5% of 1e4 |
| `binomial` | (20, 0.3) and (100, 0.5), chi-squared against e^(logChoose n k + k ln p + (n − k) ln(1 − p)), pooled as above, p-value ≥ 0.001; (1 000 000, 0.4): mean within 4σ/√n of 400 000 |
| `shuffle is a uniform permutation` | `[\|0;1;2\|]`, 60 000 shuffles: each of the 6 orders' count gives chi-squared df 5 with p-value ≥ 0.001; each result is a permutation; the input is unchanged; the first shuffle of `[\|0..9\|]` from `create 1` is pinned as above |
| `split gives two other streams` | the two generators' first four uniforms differ from each other and from the parent's next four; splitting one state twice gives equal pairs; the correlation of 100 000 paired uniforms through `Comoments` is within 0.01 of 0 |
| `variates at their edges` | `gamma 1e-3 1.0` (10 000 draws) all finite and ≥ 0; `beta 1e-3 1e-3` all in [0, 1], none NaN, mean within 0.02 of 0.5; `binomial 10 0.0 = 0`, `binomial 10 1.0 = 10`, `binomial 0 0.5 = 0`; `poisson 0.0 = 0`; `integer 5 6 = 5`; `shuffle [\|\|]` is empty (Review Focus 4) |
| `domains` | `ArgumentOutOfRangeException` for `integer 3 3`, `exponential 0.0`, `gamma -1.0 1.0`, `beta 1.0 0.0`, `binomial -1 0.5`, `binomial 5 1.5`, `binomial 5 nan`, `poisson -1.0`, `poisson nan`, `poisson 2e9`, and for a NaN parameter: `exponential nan`, `gamma nan 1.0`, `gamma 1.0 nan`, `beta nan 1.0`, `beta 1.0 nan` |

- [ ] **Step 2: Run** the `"Generator"` filter. Expected: the build fails, `Generator.integer` is not defined.
- [ ] **Step 3: Implement** the eight functions in `Generator.fs`, each doc comment naming its method and expected cost (*from documentation* or *general knowledge*).
- [ ] **Step 4: Run** the filter, paste the two pinned blocks from the .NET output. Expected: all pass.
- [ ] **Step 5: Run the gate.** Expected: 0 warnings, 0 failed, 0 failing; the pinned integers and shuffle pass under mocha.
- [ ] **Step 6: Measure the gate** (section 11): time each of the four gate commands once with PowerShell's `Measure-Command`, after one warm-up build, and append to `docs/measurements/gate.md` a dated row "After Plan T2" with each command, its time and the test counts each suite printed, labelled *measured here*, beside the baseline (build 9 s incremental, .NET suite 2 s and 174 tests, Fable compile 4 s warm, mocha 5 s and 171 tests; *measured here* by the coordinator on 2026-11-20, cited from Plan T1, Task 15, `docs/measurements/gate.md`). Compare two figures only when both are this machine's runs of the same command.
- [ ] **Step 7: Commit** `feat(numerics): integer, exponential, gamma, beta, binomial and Poisson variates, shuffle and split`.

---

## What Plan T3 takes from this plan

The Produces blocks of Tasks 1 to 4 (`Special`), 5 to 7 (`Vector`, `Matrix`, `LuDecomposition`, `CholeskyDecomposition`, `QrDecomposition`, `SymmetricEigen`), 8 (`Roots.brent`), 9 (`LeastSquares.levenbergMarquardt`, `LeastSquaresResult.Jacobian`), 10 (`Integrate.adaptive`), 15 (`Comoments`), 19 (`Aggregate.comomentsBy`) and 20 (the `Generator` variates); `Bounds.fs` and `tests/reference/special.py` for its own tests; and the gate times in `docs/measurements/gate.md`.
