# The accuracy record: every stated bound, its test and its reference

This file lists each accuracy bound that a doc comment in `Ranvier.Numerics`, `Ranvier.Quantities` or `Ranvier.Statistics`
states as a contract, with the kernel, the domain it holds over, the test that asserts it and where the test's reference
value comes from. Every bound in the tables below is asserted by a test that runs on .NET and, compiled through Fickl, on
JavaScript, *measured here* on 2026-11-21 at the commit that adds this file (the branch's previous commit plus the one test this task added), by the two suite commands run
from the repository root: `dotnet run -c Release --project tests/Ranvier.Science.Tests` (794 tests, 0 failed) and
`dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` followed by
`npx mocha dist/tests/Main.fs.js` (789 passing). A bound is asserted on each target separately: a kernel that calls `exp`,
`log` or a trigonometric function can differ in the last digits between targets, and only the kernels built from
`+ − × ÷` and `sqrt` are claimed bit-equal across them (section 5.3 of `docs/superpowers/specs/2026-11-20-toolkit-design.md`;
the targets are in its section 5.1 and are not repeated here). Two kinds of row are exceptions to "asserted by a test" and say so in the row: the entries of the "Stated but not pinned" table, which no test asserts or asserts only in part, and the rows whose bound is asserted at one case only (`Aggregate.momentsBy`, the `Window` dominant-item row, `Integrate.gaussLegendre`'s tested n). The tables of measured figures that are not guarantees record what a test pins on a tested set, not a bound.

How to read the tables. **Bound** is the doc comment's figure: "relative 1e-12" is `|actual − expected| ≤ 1e-12 · |expected|`,
"absolute" is without the factor, and "max(a, b)" is the larger of two. **Domain** is where the doc comment states the bound
holds, and, in a row's text or in a "Limits" note under its table, where the branch now falls short of it. **Reference** is
where the test's expected value comes from: a closed form or identity (named), a section of a committed script under
`tests/reference/` (the command that prints it is written out the first time and abbreviated after), or the kernel's own
independently pinned function. Every figure carries a label: *measured here* with its command, *from documentation* with
the file, or *general knowledge*. Test names are those of the test list in the named file under `tests/Ranvier.Science.Tests/`.

A figure written "(scan not kept)" comes from a dense scan against mpmath whose script is not in the repository; the doc
comment that states it says so, and the record cites that doc comment (*from documentation*), not a re-run.

Commands for the reference scripts, all run as `python -I tests/reference/special.py <section>` with Python 3.14's standard
library only: sections `gamma`, `erf`, `incgamma`, `incbeta`, `normal`, `lognormal`, `zeta`, `weibull`, `dist-gamma`,
`dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`, `dist-regimes`, `dist-edges`, `discrete`, `intervals`,
`parametric`, `counts`, `ranks`, `regression`, `correlation`, `resampling` (*from documentation*: the script's header,
`tests/reference/special.py`). Each section prints the F# literals that its test file holds, computed in decimal arithmetic
from the exact value of each double input.

## Search

The bounds were collected from the doc comments (`///` blocks) of every `.fs` file under `src/Ranvier.Numerics`,
`src/Ranvier.Quantities` and `src/Ranvier.Statistics` that match any of these wordings, case-insensitively: "error at most",
"round trip", "relative error", "within", "absolute error", "ulp", "relative 1e-", "at most 1e-", "bound", "accura" (which
takes "accuracy", "accurate" and "accurately"), and, added in the fix round, "agree" and "to 1e-". Those last two matched 4 further Statistics blocks and no Numerics or Quantities block; one of them, the `Density` module remarks, states a bound (its row is below), and the others (`Describe.quantileSorted`, `Describe.quantiles`, `Correlation.kendallTest`) do not. The figure is the number of doc-comment blocks (a run of consecutive `///`
lines) that match at least one wording, *measured here* by a script that scans the files; most blocks match several
wordings:

| Package | Blocks matched |
|---|---|
| `Ranvier.Numerics` | 57 |
| `Ranvier.Quantities` | 3 |
| `Ranvier.Statistics` | 59 |

A block that matches but states no bound is left out: a tolerance argument or stopping rule (`Tolerance`, `Roots`,
`LeastSquares`, `Integrate.adaptive`'s convergence test), a floor, cap or clock tolerance that is a definition, the accuracy
of an internal starting point, "within rounding" with no figure, a cost or amortisation remark, an error estimate that its
comment calls heuristic, a bound of a support or of an interval's range, and `ConfidenceSequence`'s coverage (see the end of
the Statistics section).

## Ranvier.Numerics

### Contract bounds

| Kernel | Bound | Domain | Test | Reference |
|---|---|---|---|---|
| `Special.gammaLn` | max(relative 1e-13, absolute 1e-14): the absolute floor applies where \|ln Γ\| < 0.1 | x > 0 and negative non-integers | `SpecialTests.fs`: `gammaLn matches the reference` | `special.py gamma` |
| `Special.gamma` | relative 1e-13 | \|Γ(x)\| ≥ 1e-300 (+∞ from 171.63) | `SpecialTests.fs`: `gamma matches the reference`, `gamma of n is (n - 1)!`, `gamma of one half is the root of pi`, `gamma overflows from 171.63` | `special.py gamma`; identities Γ(n) = (n − 1)! (exact for n ≤ 23) and Γ(½) = √π |
| `Special.digamma` | max(relative 1e-13, absolute 1e-14): the floor applies where \|ψ\| < 0.1 | x > 0 and negative non-integers | `SpecialTests.fs`: `digamma matches the reference` | `special.py gamma` |
| `Special.log1p` | relative 1e-15 | x ≥ −1 | `SpecialTests.fs`: `log1p and expm1 at small arguments`, `log1p and expm1 match the reference at negative arguments`, `log1p and expm1 inverses and ranges` | Taylor series written in the test (x = ±1e-5, 1e-10); `special.py gamma` at negative x; the round trip through `expm1` |
| `Special.expm1` | relative 1e-15 | every finite x | the same three tests | as `log1p` |
| `Special.betaLn` | max(relative 1e-13, absolute 1e-14): the floor applies where \|ln B\| < 0.1 | a, b > 0 | `SpecialTests.fs`: `beta and betaLn match the reference`, `beta where ln B is at least 256 in magnitude` | `special.py gamma` |
| `Special.beta` | relative 1e-13 where \|ln B\| < 256; relative 5e-13 where \|ln B\| ≥ 256 and B ≥ 1e-300 | a, b > 0 | the same two tests (the second asserts the 5e-13 regime, and that the first holds no case at or above 256) | `special.py gamma`, pairs with \|ln B\| ≥ 256 |
| `Special.logFactorial` | max(relative 1e-13, absolute 1e-14); exactly 0 at 0 and 1 | n ≥ 0 | `SpecialTests.fs`: `logFactorial matches the reference` | `special.py gamma` |
| `Special.logChoose` | relative 1e-13; exactly 0 at k = 0 and k = n | 0 ≤ k ≤ n | `SpecialTests.fs`: `logChoose matches the reference` | `special.py gamma` |
| `Special.erf` | relative 1e-13 | every x with \|erf(x)\| ≥ 1e-300 | `SpecialTests.fs`: `erf matches the reference` | `special.py erf` |
| `Special.erfc` | relative 1e-13 | every x with erfc(x) ≥ 1e-300 (x up to about 26.2) | `SpecialTests.fs`: `erfc matches the reference` (asserts each expected value is at least 1e-300) | `special.py erf` |
| `Special.gammaLowerRegularized` | relative 1e-12 | 0 < a ≤ 1000, P(a, x) ≥ 1e-300 | `SpecialTests.fs`: `incomplete gamma matches the reference` | `special.py incgamma` |
| `Special.gammaUpperRegularized` | relative 1e-12 | 0 < a ≤ 1000, Q(a, x) ≥ 1e-300 | the same test | `special.py incgamma` |
| `Special.gammaLowerRegularizedInverse` | P(a, x) at the result within relative 1e-12 of p; NaN (a stated floor) where p < 1e-300 and both P and xᵃe⁻ˣ/Γ(a) underflow at the starting point | 0 < a ≤ 1000, root a normal double, the doubles beside it resolve p | `SpecialTests.fs`: `the inverse round-trips`, `the inverse below 1e-300 at a large shape` | the round trip through `gammaLowerRegularized`, pinned by the row above; the 1.9e-301 NaN at a = 546.39 is the doc's *measured here* figure for a grid scan (scan not kept) |
| `Special.betaRegularized` | relative 1e-12 | 0 < a, b ≤ 1000, I_x(a, b) ≥ 1e-300 | `SpecialTests.fs`: `incomplete beta matches the reference` (asserts each expected value is at least 1e-300) | `special.py incbeta` |
| `Special.betaRegularizedInverse` | I_x(a, b) at the result within relative 1e-12 of p; near 1 for b < 1, where adjacent doubles step I_x by more than 1e-12 p, the root lies within one double of the result | 0 < a, b ≤ 1000, root a normal double, the doubles beside it resolve p | `SpecialTests.fs`: `the beta inverse round-trips`, `the beta inverse near 1 for b below 1`, `the beta inverse at 0 and 1` | the round trip through `betaRegularized`, pinned by the row above; the neighbouring-doubles check is in the test |
| `Integrate.gaussLegendre` | relative 1e-14 | x^(2n−1) on [0, 1] for n up to 100 (tested at 1, 2, 5, 10, 20, 64, 100), and eˣ on [−1, 1] at n = 20 | `CalculusTests.fs`: `Gauss–Legendre is exact to degree 2n − 1` | closed forms 1/(2n) and e − 1/e |
| `Fourier.forward` | each output within 1e-13 · Σⱼ(\|Re xⱼ\| + \|Im xⱼ\|) of the direct sum | every bin for n ≤ 1024; bins 0, n/2, n − 1 and every 37th at n = 4095 and 4096 | `FourierTests.fs`: `forward matches the direct sum` | the direct DFT sum computed in the test (`dftAt`) |
| `Fourier.inverse` (as `inverse ∘ forward`) | each output within 1e-13 · max over j of max(\|Re xⱼ\|, \|Im xⱼ\|) of the input | n ≤ 4096 | `FourierTests.fs`: `inverse undoes forward` | the identity `inverse ∘ forward = id` |
| `Fourier.convolve` | within 1e-12 · Σ\|a\| · max\|b\| of the direct double sum | lengths 300 and 200 | `FourierTests.fs`: `convolve` | the direct convolution sum computed in the test (`exactSum`) |
| `FloatPolicy.standard` | between re-folds a maintained sum of 8 rows stays within 2 ulp of the from-scratch compensated fold (the doc reports 0 ulp measured; the test asserts at most 2) | 8 rows, 1,000,000 edits on .NET and 100,000 on JavaScript | `AggregateTests.fs`: `between re-folds under the standard floor a small sum stays within 2 ulp of the from-scratch fold` | the from-scratch compensated fold of the current values (`plainFold`) |
| `Aggregate.momentsBy` (`maintainedBy` accuracy) | after removing a row far larger than the rest the mean carries an absolute error of order n·ε·(largest magnitude held since the last re-fold), ε = 2⁻⁵² (*general knowledge*); asserted as 4·ε·1e16 on 4 rows, with the mean exactly 1.75 where the rows' mean is 2 | rows 1e16, 1, 2, 3 under `FloatPolicy.standard`; the bound is the doc's order-of figure, asserted only at this one case | `AggregateTests.fs`: `removing a dominant row leaves an error within the stated bound` | the rows' mean, 2 (closed form); and the from-scratch fold, via `removing a dominant row matches the fold once the floor-0 re-fold has run` |
| `Window.moments` (capacity 50, items 1e9 + (i mod 7)) | mean within relative 1e-12 of a fresh fold of the items; compensated variance within relative 1e-8; uncompensated variance within relative 1e-6 | after 100001, 100010 and 100049 pushes (counts that do not end on a re-fold) | `WindowTests.fs`: `re-folds bound drift` | a fresh fold of `Window.items` |
| `Window.moments` and `TimeWindow.moments` after a dominant item leaves | mean absolute error of order count·ε·(largest \|item\|), variance of order count·ε·(item²), until the next re-fold (*general knowledge*: the order-of form); asserted as 3·ε·1e20 and 3·ε·1e40 at count 3, where the compensated mean reads 1366.33 against 2 | capacity 3, items 1e20, 1, 2, 3 (and 4, 5 until the re-fold at the sixth push) | `WindowTests.fs`: `a dominant item's error lasts until the next re-fold, within the stated bound` | a fresh fold of the window's items, which is exact after the departure |

### Measured figures that the doc comment says are not guarantees

Each doc comment below states a figure *measured here* on a tested set and says in the same place that it is not a bound
that holds in general. The test pins the figure on the tested set only.

| Kernel | Figure | Domain | Test | Reference |
|---|---|---|---|---|
| `Integrate.adaptive` | every result converged and within max(absolute, relative · \|exact\|) of the exact integral at absolute 1e-12, relative 1e-10; `Converged` is a heuristic and the error estimate is not a bound | the closed-form list of the test: finite and infinite ranges, endpoint singularities, cos 50x | `CalculusTests.fs`: `adaptive meets its tolerance on closed forms` | closed forms in the test (x², sin, exp, Gaussian, 1/(1 + x²), 1/√x, ln x, cos 50x and others; the last from a Γ ratio through `Special.gammaLn`) |
| `Ode.solve` | global error at most 2 · (AbsoluteTolerance + RelativeTolerance · \|exact\|) per component; the doc says no tolerance bounds the global error in general | y' = −y, the harmonic oscillator, the logistic equation and y' = cos t, both tolerances 1e-10 | `OdeTests.fs`: `solve meets the tolerance on closed forms` | closed forms in the test |
| `Ode.solveAt` | an interior state within ten times the tolerance; the doc says the continuous extension does not guarantee this in general | the oscillator at six times; a quartic | `OdeTests.fs`: `solveAt on the oscillator`, `dense output reproduces a quartic` (the quartic to relative 1e-14) | cos t, −sin t; t⁴ |

### Stated but not pinned

| Kernel | Bound | File and line | Why it is not pinned |
|---|---|---|---|
| `Uncertain.standardUncertainty` | for n contributions the relative error is at most about (n/2 + 3) · 2⁻⁵³ (*general knowledge*: a first-order rounding bound of the scaled sum) | `src/Ranvier.Numerics/Uncertain.fs`, lines 274 to 275 | the doc comment says "not measured", and no test in `tests/Ranvier.Science.Tests` asserts it (the tests that read `standardUncertainty` check exact values or a physical-constant tolerance, not this bound) |
| `Aggregate.sumBy` and `Aggregate.maintainedBy` | the n·ε·(largest magnitude) absolute error of each running total between re-folds, stated on `maintainedBy` (*general knowledge*) and inherited by `sumBy` | `src/Ranvier.Numerics/Aggregate.fs`, line 42 (`maintainedBy`'s remarks; `sumBy`, line 123, defers to it) | asserted only through the mean of `momentsBy` at one case (row above); no test asserts the figure on a sum itself: `1e20 then 1, then 1e20 removed` pins the values 1.0 and 0.0 under the two policies, not the bound |

### Limits of the contract bounds, as the branch has them now

These domain limits are known and are not fixed in this task; the final review wave fixes them and updates this record. They
are cited to the review ledger of the plan that found them (`.superpowers/sdd/…/progress.md` in the working tree, which is
not committed): the figures below are the ledger's and were not re-measured here, except where a committed doc comment
states them.

- `Special.gammaLowerRegularized` and `Special.gammaUpperRegularized` return NaN for a above about 1e6 near x = a, because
  they have no Temme uniform expansion (the plan T2 final review). The committed doc comment of `Discrete.cdf` states the same
  limit as seen through the Poisson: NaN near the mean from a mean of about 1 674 055, *measured here* by that doc comment's
  bisection (`src/Ranvier.Statistics/Discrete.fs`). The stated domain 0 < a ≤ 1000 is not affected.
- One-pass `Moments` loses about ε·\|mean\|/sd in relative variance accuracy for data with an offset: about 1e-4 at offset 1e9
  with spread 1e-3 (plan T3 Task 12 and the final-wave investigation). Every bound built on `Moments.variance` or
  `sampleStandardDeviation` inherits this; the rows of the Statistics tables that do are the t and F tests, the mean,
  difference and variance intervals, and the confidence sequence, whose tested samples have no large offset.

## Ranvier.Quantities

`Ranvier.Quantities` states no accuracy bound. Its three matching doc-comment blocks are `Quantity.deriveWithin` (a
tolerance argument that the caller supplies), and `Run.createWith` and `Run.ode` (a clock tolerance of 1e-9 that is part of
the definition of when a run advances). Nothing was left unpinned, and no table is given for it.

## Ranvier.Statistics

### Contract bounds

The Statistics bounds hold over the parameter values and samples that the named tests pin; beyond them the doc comments say
the error grows and give the growth. Those growth formulas are not bounds and are not repeated, except where a limit is
listed in the note under the table.

| Kernel | Bound | Domain | Test | Reference |
|---|---|---|---|---|
| `NormalQuantile.ofProbability` | relative 1.2e-9 | 0 < p < 1, p ≠ ½ (the quantile at ½ is 0, which has no relative error) | `ConfidenceSequenceTests.fs`: `normal quantile relative error is within 1.2e-9 against Continuous.quantile` (added by this task: 999 thousandths, 10⁻³ to 10⁻³⁰⁰, 1 − 10⁻³ to 1 − 10⁻¹⁵, and p either side of Acklam's switch at 0.02425; it fails at 1e-9, first at p = 0.004 with relative error 1.07e-9, *measured here* by running it with the bound at 1e-9) | `Continuous.quantile` of the standard normal, itself pinned to 1e-12 by `ContinuousTests.fs`: `normal quantile against the reference` (`special.py normal`). The existing `normal quantile` test in the same file asserts absolute tolerances of 1e-8 to 1e-12 at five points, not this relative bound |
| `Continuous.cdf` | relative 1e-12 where the value is at least 1e-300; exactly 0 and 1 outside the support | normal, uniform, exponential, Cauchy, logistic at the tested parameters; log-normal for \|mu\| up to 50 at sigma 0.1 and above; Weibull for shape up to 20; gamma, chi-squared, beta, Student t and F for shapes and degrees of freedom 0.5 to 100, and at the stated wider ends (gamma 0.01 to 1000, chi-squared 0.05 to 1e5, beta 0.2 to 500, t 0.05 to 1000, F with one of d1, d2 up to 1000 and the other 0.5 to 100) | `ContinuousTests.fs`: `normal cdf and survival against the reference`, `logNormal against the reference`, `weibull survival at a large shape and a scale that is not a power of two`, `gamma against the reference`, `chi-squared against the reference`, `beta against the reference`, `Student t against the reference`, `F against the reference`, `where a plain form would cancel or round 1 - x, against the reference`, `the stated domains at their edges against the reference` | `special.py normal`, `lognormal`, `weibull`, `dist-gamma`, `dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`, `dist-regimes`, `dist-edges`; closed forms for the exponential, uniform, Cauchy and logistic. The doc's dense scan (10 792 points, worst 1.5e-13) is scan not kept |
| `Continuous.survival` | relative 1e-12 where the value is at least 1e-300; 1 at and below the support's lower end, exactly 0 at and above its upper end | as `cdf` | the same tests | as `cdf` |
| `Continuous.quantile` | the round trip: `cdf` at the result within relative 1e-12 of p for p ≤ ½, and `survival` within relative 1e-12 of 1 − p for p > ½, down to p = 1e-300; near 1 for the beta with a small b, where adjacent doubles move the survival by more than 1e-12 of itself, the result is within one double of the root | the domain of `cdf`, where the quantile is a normal double and the doubles beside it resolve p | `ContinuousTests.fs`: `quantile round trips`, `quantile round trips for the incomplete-function families`, `quantile round trips at the edges of the stated domains`, `gamma and chi-squared quantile round trips within 1e-14 of 1`, `log-normal quantile round trip at a large mu` | the round trip through `cdf` and `survival`, pinned by the rows above |
| `Continuous.variance` (Weibull) | relative 1e-13 for shape 10 to 1e8; within 1e-14 at shape 2 | the Weibull | `ContinuousTests.fs`: `weibull variance against the reference at large shape`, `weibull closed forms` | `special.py weibull` (the ζ(n) constants of the series come from `special.py zeta`) |
| `Discrete.mass` | relative 1e-12 where the value is at least 1e-300 (worst 5.9e-13 on the doc's scan) | binomial with trials up to 1000, p 1e-10 to 0.999; Poisson with mean 0.001 to 1000; geometric p 1e-10 to 0.999; negative binomial r 0.5 to 100, p 0.002 to 0.999, mean up to 1000; hypergeometric population up to 1000; Bernoulli and discrete uniform one rounding each | `DiscreteTests.fs`: `binomial against exact fractions`, `binomial tails`, `Poisson against the reference`, `negative binomial against the reference`, `hypergeometric against exact fractions`, `the stated domains at their edges against the reference`, `where the direct mass and the rounded 1 - p lose accuracy, against the reference` | exact fractions (binomial, hypergeometric) and 50-digit decimal sums (Poisson, geometric, negative binomial) from `special.py discrete`; scan of 166 569 points is scan not kept |
| `Discrete.logMass` | absolute 1e-12 where the mass is at least 1e-300 (worst 5.7e-13 on the doc's scan) | as `mass` | `DiscreteTests.fs`: `the stated domains at their edges against the reference`, `logMass is the log of mass` | `special.py discrete` |
| `Discrete.cdf` | relative 1e-12 where the value is at least 1e-300 (worst 3.9e-13, the hypergeometric); the Poisson's is NaN near the mean from about 1 674 055; no accuracy is stated beyond mean 1000 | as `mass` | the tests of `mass`, and `DiscreteTests.fs`: `a Poisson past its mean limit: NaN near the mean, and quantile raises`, `a Poisson at its mean limit: the quantile is the least k` | `special.py discrete` |
| `Discrete.survival` | relative 1e-12 where the value is at least 1e-300 (worst 4.3e-13); below p = 0.002 for the negative binomial the error grows (1.9e-12 at r = 0.5, p = 5e-4) | as `mass`; the negative binomial for p ≥ 0.002 | the tests of `mass`, including `where the direct mass and the rounded 1 - p lose accuracy, against the reference` | `special.py discrete` |
| `HypothesisTest` p-values (module remarks) | the p-value has the relative error of `Continuous.cdf` and `Continuous.survival`, at most 1e-12 where it is at least 1e-300; each function names its pinning test | each function below | each row below | `special.py parametric`, `counts`, `ranks` |
| `HypothesisTest.oneSampleT` | p-value relative 1e-12 | R's `datasets::sleep` group 1 and the paired differences | `HypothesisTests.fs`: `one-sample t against the reference` | `special.py parametric` |
| `HypothesisTest.oneSampleZ` | p-value relative 1e-12 | the test's sample | `HypothesisTests.fs`: `one-sample z against the reference` | `special.py parametric` |
| `HypothesisTest.welchT`, `pooledT` | p-value relative 1e-12 | the test's samples | `HypothesisTests.fs`: `Welch and pooled t against the reference` | `special.py parametric` |
| `HypothesisTest.varianceRatio` | p-value relative 1e-12 | the test's samples | `HypothesisTests.fs`: `variance ratio against the reference` | `special.py parametric` |
| `HypothesisTest.oneWayAnova` | p-value relative 1e-12 | the test's groups | `HypothesisTests.fs`: `one-way ANOVA against the reference` | `special.py parametric` |
| `HypothesisTest.binomial` | p-value relative 1e-12 | the rows of the test | `HypothesisTests.fs`: `exact binomial against exact fractions` | exact fractions, `special.py counts` |
| `HypothesisTest.chiSquaredGoodnessOfFit` | p-value relative 1e-12 (with the statistic) | Mendel's counts | `HypothesisTests.fs`: `goodness of fit against Mendel's counts` | `special.py counts` |
| `HypothesisTest.chiSquaredIndependence` | p-value relative 1e-12 | the tables of the test | `HypothesisTests.fs`: `independence against the reference` | `special.py counts` |
| `HypothesisTest.fisherExact` | p-value relative 1e-12 | the tea-tasting table and the tables of the second test | `HypothesisTests.fs`: `Fisher's tea-tasting table`, `Fisher against exact fractions` | the closed form of the tea table; exact fractions, `special.py counts` |
| `HypothesisTest.mannWhitney` | p-value relative 1e-12 against the approximating formula (R computes it exactly for fewer than 50 observations per sample, so this is not R's value there) | three data sets: ties, U on each side of its mean, U at its mean | `HypothesisTests.fs`: `Mann–Whitney against the reference` | `special.py ranks` |
| `HypothesisTest.wilcoxonSignedRank` | p-value relative 1e-12 against the approximating formula | three data sets (ties and a zero, a zero without ties, V at its mean), and 100000 differences of ±1 | `HypothesisTests.fs`: `Wilcoxon on the sleep differences`, `Wilcoxon against the reference`, `the continuity correction and the tie correction by closed forms` | `special.py ranks`; closed forms for the ±1 case |
| `HypothesisTest.kolmogorovSmirnov` | p-value relative 1e-12 against the Kolmogorov series (at most 3 terms for λ from 0.04 to 6) | the series on each side of the switch at λ = 1.18 (λ of 0.52, 1.17, 1.19), small and large λ | `HypothesisTests.fs`: `one-sample Kolmogorov–Smirnov`, `the Kolmogorov series on each side of the switch at lambda 1.18`, `Kolmogorov–Smirnov at small and large lambda` | `special.py ranks`, which also asserts the term count on a grid of step 0.001 (the check is in the script; *measured here* on 2026-11-21 by `python -I tests/reference/special.py ranks`, which exited 0, the script exiting non-zero if the double series needs more than 3 terms) |
| `HypothesisTest.kolmogorovSmirnovTwoSample` | p-value relative 1e-12 against the series | λ of 0.89 and 1.73, with a tied value | `HypothesisTests.fs`: `two-sample Kolmogorov–Smirnov` | `special.py ranks` |
| `MeanInterval.student` | bound relative 1e-12 | ten observations, α 0.05 and 0.01; the doc reports the largest relative error as 1.5e-16 (*measured here* on .NET by a one-off F# script, not kept) | `IntervalTests.fs`: `the Student interval against the reference`, `small samples give NaN bounds`; the coverage of the 95% interval on 2,000 seeded samples by `the Student interval covers at its level` | `special.py intervals` |
| `MeanInterval.knownSigma` | bound relative 1e-12 | ten observations, σ 3, α 0.05 | `IntervalTests.fs`: `the known-sigma interval against the reference` | `special.py intervals` |
| `ProportionInterval.wilson`, `clopperPearson`, `agrestiCoull`, `jeffreys` | each bound relative 1e-12 (the doc reports the largest relative error as 3.7e-16, one-off script not kept); every bound in [0, 1] | (81, 263) at α 0.05 and 0.01, (0, 20), (20, 20), (1, 29) | `IntervalTests.fs`: `proportion intervals against the reference`, `proportion intervals at 0 and n successes`, `Clopper-Pearson at one trial and at zero successes has a closed form` | `special.py intervals`; the closed form 1 − (α/2)^(1/n) |
| `VarianceInterval.chiSquared` | bound relative 1e-12 (largest 2.2e-16, one-off script not kept) | ten observations, α 0.05 | `IntervalTests.fs`: `the variance interval against the reference` | `special.py intervals` |
| `DifferenceInterval.welch`, `pooled` | bound relative 1e-12 (largest 7.4e-17, one-off script not kept) | R's `datasets::sleep`, α 0.05 | `IntervalTests.fs`: `difference intervals against the reference` | `special.py intervals` |
| `Regression.simple` | relative 1e-12 for every field | the 10 pairs of the test; var_x, var_y and cov finite and, where not 0, normal doubles (spreads about 1e-154 to 1e154); x counts as constant where var_x ≤ (8ε\|x̄\|)² | `RegressionTests.fs`: `simple regression against exact fractions`, `simple regression at the edges of the spreads Comoments can hold`, `NaN comoments give a simple regression of NaN fields`, `x emptied back to a constant by removals gives a NaN slope`, `a residue left by removals from far away is not detected` | exact fractions, `special.py regression` |
| `Regression.linear` | relative 1e-12 for every field; residuals within absolute 1e-12 | well-conditioned designs: the integer designs (1, x1, x2) and (x1, x2) of 12 rows | `RegressionTests.fs`: `linear regression against exact fractions`, `regression without a constant against exact fractions` | exact fractions, `special.py regression` |
| `Regression.weighted` | relative 1e-12 for every field; 1e-9 with a weight of 1e30 on one row | well-conditioned designs: (1, x1), weights 1, 2, 3 with one 0; and the dominant weight | `RegressionTests.fs`: `weighted regression against exact fractions`, `a dominant weight leaves R squared at the exact fraction` | exact fractions, `special.py regression` (its `weighted` and `dominant` cases) |
| `Regression.polynomial` | coefficients within absolute 1e-12 | the degree 2 polynomial through 8 integer points; no bound shown for higher degrees | `RegressionTests.fs`: `polynomial regression recovers exact coefficients` | the exact coefficients (closed form) |
| `Regression.curve` | parameters within relative 1e-8 and the residual standard error below 1e-8 on y = 2.5 e^(−1.3x); on the 10-point line the parameters within 1e-8 and the standard errors and covariance within relative 1e-6 of `Regression.linear`'s (the forward difference limits them to about √ε ≈ 1.5e-8, *general knowledge*) | the 21 points of the first test (start (1, 1)); the 10-point line | `RegressionTests.fs`: `curve fitting recovers exact parameters`, `a model linear in its parameters gives the linear regression` | the exact parameters 2.5 and −1.3 (closed form); `Regression.linear`, pinned by the rows above |
| `Correlation.pearsonTest` | statistic and p-value within 1e-12 relative of exact fractions | the 8 pairs of the test | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions` | exact fractions, `special.py correlation` |
| `Correlation.spearmanTest` | statistic and p-value within 1e-12 relative of exact fractions | the tied 8 pairs of the test | `CorrelationTests.fs`: `Spearman with ties against the reference` | exact average ranks, `special.py correlation` |
| `Density.gaussianKernel` | where x − xᵢ overflows the true value is below 1e-307, so the 0 returned is within the smallest normal double of it | extreme scales | `ResamplingTests.fs`: `the estimate at extreme scales is exact or honestly zero` | the closed form of the normal density |
| `Density.silvermanBandwidth` and `scottBandwidth` (module remarks) | relative 1e-13 against exact rational arithmetic, *measured here* by the test | four rows: offsets 1 to 1e9 with spreads 1e-9 to 1e-3, 50 doubles offset + i·spread | `ResamplingTests.fs`: `bandwidths of offset data match exact rational arithmetic` | `special.py resampling` (rationals and 60-digit decimals, rounded once) |

Rows stating a bound that no doc comment gives, but that a test pins (recorded as "stated in no doc comment"):

| Kernel | Bound | Domain | Test | Reference |
|---|---|---|---|---|
| `Correlation.pearson` | stated in no doc comment (the doc comment gives only that exactly linear data read ±1 or a few ulps inside it, 0.9999999999999998 for the integers 1 to 5 against 2x + 1); the test asserts relative 1e-12 | the 8 permutation pairs of the test; scales 1e200 and 1e-200 | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Pearson at the scales of 1e200 and 1e-200`, `perfect correlation gives an infinite t and a p-value of 0` | exact fractions, `special.py correlation` |
| `Correlation.spearman` | stated in no doc comment; the tests assert relative 1e-12 (and 1e-14 on a monotone cube) | untied permutations; the tied 8 pairs | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Spearman with ties against the reference` | exact average ranks; Pearson on untied data (identity); `special.py correlation` |
| `Correlation.kendall` and `kendallTest` | stated in no doc comment; the tests assert tau-b relative 1e-12 against the reference and 1e-14 against a direct pair count, and z within 1e-12 | untied and tied 8 pairs; 39 generated samples of sizes 2 to 40 with ties | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Kendall tau-b matches a direct count`, `Kendall over generated samples with ties matches the direct count and variance` | the direct pair count written in the test (`countPairs`); `special.py correlation` |

### Measured figures that the doc comment says are not bounds

The `Continuous` module remarks say `density` and `logDensity` carry no bound beyond the accuracy of the functions they call; the figures below are measured on the stated ranges.

| Kernel | Bound | Domain | Test | Reference |
|---|---|---|---|---|
| `Continuous.density` and `logDensity` (module remarks) | measured, not a bound: relative 2e-13 for shapes and degrees of freedom 0.5 to 100; 1.2e-12 for the gamma at shape 1000 and 1.1e-10 for the chi-squared at 1e5 degrees of freedom (scan not kept); the test asserts 1e-12, 2e-12 at gamma 1000 and 2e-10 at chi-squared 1e5 | the ends of each stated range; the gamma, chi-squared, beta, Student t and F | `ContinuousTests.fs`: `the stated domains at their edges against the reference` | `special.py dist-edges` |

### Limits of the contract bounds, as the branch has them now

These are known and are not fixed in this task; the final review wave fixes them and updates this record. The figures are the
review ledger's (not committed) and were not re-measured here.

- The t tests' standard error underflows near 1e-200 data scale (plan T3 Task 6). The one-sample, Welch and pooled t
  p-values and the intervals built on the same quantities hold their bounds for data of ordinary scale only.
- Everything built on `Moments.variance` or `sampleStandardDeviation` inherits the one-pass `Moments` loss for offset data,
  about 1e-4 relative in the variance at offset 1e9 with spread 1e-3 (plan T3 Task 12; see the Numerics limits). The t, F and
  ANOVA p-values, the mean, difference and variance intervals, and `ConfidenceSequence` hold their bounds on the tested
  samples, which carry no large offset.
- Weighted regression with a dominant weight and an offset y inflates the residual standard error (plan T3 Task 10, finding
  O1). The `Regression.weighted` bound is asserted for the 12-row case with a weight of 1e30 on one row at 1e-9, where y has no
  large offset.
- The `Regression.linear` and `weighted` 1e-12 bound does not hold for y with a large offset: 7.5e-9 on the slope at
  y + 1e8 (plan T3 Task 10).
- `Regression.curve`'s forward-difference step for a parameter fitted near 0 understates the standard errors (plan T3
  Task 11, finding X1).
- `Correlation.pearson`, `spearman` and `kendall` state no bound in their doc comments although the tests pin them (plan T3
  Task 11, finding M3); the last table above records them.

### Not accuracy bounds, left out

- `ConfidenceSequence`'s coverage figures: 0.0415 of 2,000 streams excluded the mean at α = 0.05, and 0.009 at α = 0.0125,
  both *measured here* on 2026-11-20 and reproduced in this task's `ConfidenceSequence` test run (printed as "Coverage under
  continuous monitoring" and "Coverage at the page's setting"). The guarantee is asymptotic and states no coverage at a
  finite count, so it is not an accuracy bound. The tests are `ConfidenceSequenceTests.fs`: `the sequence covers under
  continuous monitoring and the fixed-sample interval does not` and `the sequence covers at the page's setting`.
- `Sequential`'s duality with the confidence sequence, which the doc comment says fails within rounding of an endpoint.
