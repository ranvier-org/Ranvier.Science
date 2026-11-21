# The accuracy record: every stated bound, its test and its reference

This file lists each accuracy bound that a doc comment in `Ranvier.Numerics`, `Ranvier.Quantities` or `Ranvier.Statistics`
states as a contract, with the kernel, the domain it holds over, the test that asserts it and where the test's reference
value comes from. Every bound in the tables below is asserted by a test that runs on .NET and, compiled through Fickl, on
JavaScript, *measured here* on 2026-11-21 after the final review's fix wave, by the two suite commands run from the
repository root: `dotnet run -c Release --project tests/Ranvier.Science.Tests` (827 tests, 0 failed) and
`dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` followed by
`npx mocha dist/tests/Main.fs.js` (822 passing). A bound is asserted on each target separately: a kernel that calls `exp`,
`log` or a trigonometric function can differ in the last digits between targets, and only the kernels built from
`+ − × ÷` and `sqrt` are claimed bit-equal across them (section 5.3 of `docs/superpowers/specs/2026-11-20-toolkit-design.md`;
the targets are in its section 5.1 and are not repeated here). One kind of row is an exception to "asserted by a test" over
its whole domain and says so in the row: a bound asserted at one case only (`Aggregate.momentsBy` and `sumBy`, the `Window`
dominant-item row, `Uncertain.standardUncertainty` and the `Moments` offset row). The tables of measured figures that are
not guarantees record what a test pins on a tested set, not a bound.

How to read the tables. **Bound** is the doc comment's figure: "relative 1e-12" is `|actual − expected| ≤ 1e-12 · |expected|`,
"absolute" is without the factor, and "max(a, b)" is the larger of two. **Domain** is where the doc comment states the bound
holds, and, in a row's text or in a "Limits" note under its table, where it falls short of it. **Reference** is where the
test's expected value comes from: a closed form or identity (named), a section of a committed script under
`tests/reference/` (the command that prints it is written out the first time and abbreviated after), or the kernel's own
independently pinned function. Every figure carries a label: *measured here* with its command, *from documentation* with
the file, or *general knowledge*. Test names are those of the test list in the named file under `tests/Ranvier.Science.Tests/`.

The `Continuous` and `Discrete` doc comments rest the stretches of their domains between the pinned rows on dense scans that
are not committed: against mpmath 1.3.0 for `Continuous`, which spec section 5.2 does not admit under `tests/reference/`,
and against exact fractions and 50-digit decimal sums for `Discrete`. The doc comments say so and give no figure from those
scans as a bound; this record cites them as the doc comments do (*from documentation*), not as a re-run.

Commands for the reference scripts, all run as `python -I tests/reference/special.py <section>` with Python 3.14's standard
library only: sections `gamma`, `erf`, `incgamma`, `incbeta`, `normal`, `lognormal`, `zeta`, `weibull`, `dist-gamma`,
`dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`, `dist-regimes`, `dist-edges`, `discrete`, `intervals`,
`parametric`, `counts`, `ranks`, `regression`, `correlation`, `resampling`, `numerics` (*from documentation*: the script's
`SECTIONS` table, `tests/reference/special.py`). Each section prints the F# literals that its test file holds, computed in
decimal arithmetic or exact fractions from the exact value of each double input.

## Search

The bounds were collected from the doc comments (`///` blocks) of every `.fs` file under `src/Ranvier.Numerics`,
`src/Ranvier.Quantities` and `src/Ranvier.Statistics` that match any of these wordings, case-insensitively: "error at most",
"round trip", "relative error", "within", "absolute error", "ulp", "relative 1e-", "at most 1e-", "bound", "accura" (which
takes "accuracy", "accurate" and "accurately"), "agree" and "to 1e-". The figure is the number of doc-comment blocks (a run
of consecutive `///` lines) that match at least one wording, *measured here* on 2026-11-21 by a scratch script that scans
the files (it reproduces 57, 3 and 63 on the record's first version, commit 0cd8883); most blocks match several wordings:

| Package | Blocks matched |
|---|---|
| `Ranvier.Numerics` | 59 |
| `Ranvier.Quantities` | 3 |
| `Ranvier.Statistics` | 67 |

The fix wave added six matching blocks: `Moments.variance` and `sampleVariance` (a row below), the internal
`PValue.continuous` (a row below), `Correlation.spearman` and `kendall` (rows below), and the internal
`Spread.sampleStandardDeviation` of `Resampling.fs`, which states no figure. A block that matches but states no bound is
left out: a tolerance argument or stopping rule (`Tolerance`, `Roots`, `LeastSquares`, `Integrate.adaptive`'s convergence
test), a floor, cap or clock tolerance that is a definition, the accuracy of an internal starting point, "within rounding"
with no figure, a cost or amortisation remark, an error estimate that its comment calls heuristic, a bound of a support or
of an interval's range, and `ConfidenceSequence`'s coverage (see the end of the Statistics section).

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
| `Special.gammaLowerRegularizedInverse` | P(a, x) at the result within relative 1e-12 of p; NaN (a stated floor) where p < 1e-300 and both P and xᵃe⁻ˣ/Γ(a) underflow at the starting point | 0 < a ≤ 1000, root a normal double, the doubles beside it resolve p | `SpecialTests.fs`: `the inverse round-trips`, `the inverse below 1e-300 at a large shape` | the round trip through `gammaLowerRegularized`, pinned by the row above; the 1.9e-301 NaN at a = 546.39 is the doc's *measured here* figure for a grid scan that is not committed |
| `Special.betaRegularized` | relative 1e-12 | 0 < a, b ≤ 1000, I_x(a, b) ≥ 1e-300 | `SpecialTests.fs`: `incomplete beta matches the reference` (asserts each expected value is at least 1e-300) | `special.py incbeta` |
| `Special.betaRegularizedInverse` | I_x(a, b) at the result within relative 1e-12 of p; near 1 for b < 1, where adjacent doubles step I_x by more than 1e-12 p, the root lies within one double of the result | 0 < a, b ≤ 1000, root a normal double, the doubles beside it resolve p | `SpecialTests.fs`: `the beta inverse round-trips`, `the beta inverse near 1 for b below 1`, `the beta inverse at 0 and 1` | the round trip through `betaRegularized`, pinned by the row above; the neighbouring-doubles check is in the test |
| `Integrate.gaussLegendre` | relative 1e-14 | x^(2n−1) on [0, 1] for n up to 100 (tested at 1, 2, 5, 10, 20, 64, 100), and eˣ on [−1, 1] at n = 20 | `CalculusTests.fs`: `Gauss–Legendre is exact to degree 2n − 1` | closed forms 1/(2n) and e − 1/e |
| `Fourier.forward` | each output within 1e-13 · Σⱼ(\|Re xⱼ\| + \|Im xⱼ\|) of the direct sum | every bin for n ≤ 1024; bins 0, n/2, n − 1 and every 37th at n = 4095 and 4096 | `FourierTests.fs`: `forward matches the direct sum` | the direct DFT sum computed in the test (`dftAt`) |
| `Fourier.inverse` (as `inverse ∘ forward`) | each output within 1e-13 · max over j of max(\|Re xⱼ\|, \|Im xⱼ\|) of the input | n ≤ 4096 | `FourierTests.fs`: `inverse undoes forward` | the identity `inverse ∘ forward = id` |
| `Fourier.convolve` | within 1e-12 · Σ\|a\| · max\|b\| of the direct double sum | lengths 300 and 200 | `FourierTests.fs`: `convolve` | the direct convolution sum computed in the test (`exactSum`) |
| `FloatPolicy.standard` | between re-folds a maintained sum of 8 rows stays within 2 ulp of the from-scratch compensated fold, the bound asserted; the doc reports 0 ulp measured | 8 rows, 1,000,000 edits on .NET and 100,000 on JavaScript | `AggregateTests.fs`: `between re-folds under the standard floor a small sum stays within 2 ulp of the from-scratch fold` | the from-scratch compensated fold of the current values (`plainFold`) |
| `Aggregate.maintainedBy` accuracy, through `sumBy` and `momentsBy` | after removing a row far larger than the rest each running total carries an absolute error of order n·ε·(largest magnitude held since the last re-fold), ε = 2⁻⁵², n the rows held (*general knowledge*); asserted with the constant 1 at one case: 4·ε·1e16 on the sum (both policies, no re-fold yet) and on the mean of `momentsBy`, which reads exactly 1.75 where the rows' mean is 2 | rows 1e16, 1, 2, 3, the first removed; no other size is pinned | `AggregateTests.fs`: `removing a dominant row from a sum leaves an error within n · ε times the largest magnitude`, `removing a dominant row leaves an error within the stated bound` | the rows' sum 6 and mean 2 (closed forms); and the from-scratch fold, via `removing a dominant row matches the fold once the floor-0 re-fold has run` |
| `Moments.variance` and `sampleVariance` | for data with an offset the relative error reaches about ε·\|mean\|/sd (*general knowledge*), asserted with the constant 1 at one case, 3.8e-10 there, under both flags | 1000 doubles 1e6 + ((i · 7919) mod 1000)/500 − 1, spread about 1 | `MomentsTests.fs`: `an offset of 1e6 costs at most ε · \|mean\| / sd of the sample variance` | the exact sample variance of the doubles, `special.py numerics` |
| `Uncertain.standardUncertainty` | for n contributions the relative error is at most about (n/2 + 3)·2⁻⁵³ (*general knowledge*: a first-order rounding bound of the scaled sum), asserted with the constant 1 at one case | 1000 contributions in (0, 10) from the minimal-standard LCG, seed 20261121 | `UncertainTests.fs`: `standardUncertainty of 1000 contributions is within its stated bound` | √Σc² of the exact contributions, `special.py numerics` |
| `Window.moments` (capacity 50, items 1e9 + (i mod 7)) | mean within relative 1e-12 of a fresh fold of the items; compensated variance within relative 1e-8; uncompensated variance within relative 1e-6 | after 100001, 100010 and 100049 pushes (counts that do not end on a re-fold) | `WindowTests.fs`: `re-folds bound drift` | a fresh fold of `Window.items` |
| `Window.moments` and `TimeWindow.moments` after a dominant item leaves | mean absolute error of order count·ε·(largest \|item\|), variance of order count·ε·(item²), until the next re-fold (*general knowledge*: the order-of form); asserted as 3·ε·1e20 and 3·ε·1e40 at count 3, where the compensated mean reads 1366.33 against 2 | capacity 3, items 1e20, 1, 2, 3 (and 4, 5 until the re-fold at the sixth push) | `WindowTests.fs`: `a dominant item's error lasts until the next re-fold, within the stated bound` | a fresh fold of the window's items, which is free of that error after the departure |

### Measured figures that the doc comment says are not guarantees

Each doc comment below states a figure *measured here* on a tested set and says in the same place that it is not a bound
that holds in general. The test pins the figure on the tested set only.

| Kernel | Figure | Domain | Test | Reference |
|---|---|---|---|---|
| `Integrate.adaptive` | every result converged and within max(absolute, relative · \|exact\|) of the exact integral at absolute 1e-12, relative 1e-10; `Converged` is a heuristic and the error estimate is not a bound | the closed-form list of the test: finite and infinite ranges, endpoint singularities, cos 50x | `CalculusTests.fs`: `adaptive meets its tolerance on closed forms` | closed forms in the test (x², sin, exp, Gaussian, 1/(1 + x²), 1/√x, ln x, cos 50x and others; the last from a Γ ratio through `Special.gammaLn`) |
| `Ode.solve` | global error at most 2 · (AbsoluteTolerance + RelativeTolerance · \|exact\|) per component; the doc says no tolerance bounds the global error in general | y' = −y, the harmonic oscillator, the logistic equation and y' = cos t, both tolerances 1e-10 | `OdeTests.fs`: `solve meets the tolerance on closed forms` | closed forms in the test |
| `Ode.solveAt` | an interior state within ten times the tolerance; the doc says the continuous extension does not guarantee this in general | the oscillator at six times; a quartic | `OdeTests.fs`: `solveAt on the oscillator`, `dense output reproduces a quartic` (the quartic to relative 1e-14) | cos t, −sin t; t⁴ |

### Limits of the contract bounds

- `Special.gammaLowerRegularized` and `Special.gammaUpperRegularized` return NaN for a above about 1e6 near x = a, because
  they have no Temme uniform expansion. Their doc comments state it, and so does `Discrete.cdf`'s, as seen through the
  Poisson: NaN near the mean from a mean of about 1 674 055 (*from documentation*: `Discrete.cdf` in
  `src/Ranvier.Statistics/Discrete.fs`, pinned by `DiscreteTests.fs`: `a Poisson past its mean limit: NaN near the mean,
  and quantile raises` and `a Poisson at
  its mean limit: the quantile is the least k`). The stated domain 0 < a ≤ 1000 is not affected. `docs/FINDINGS.md`
  section 9 records it as a later enhancement.
- The offset loss of `Moments` (the row above) is inherited by every bound built on `Moments.variance` or
  `sampleStandardDeviation`: the t, F and ANOVA tests, the mean, difference and variance intervals, and the confidence
  sequence say in their remarks that their bounds are relative to the moments' variance and that `Moments` adds this loss.
  Their tested samples carry no large offset.

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
| `NormalQuantile.ofProbability` | relative 1.2e-9 | 0 < p < 1, p ≠ ½ (the quantile at ½ is 0, which has no relative error) | `ConfidenceSequenceTests.fs`: `normal quantile relative error is within 1.2e-9 against Continuous.quantile` (999 thousandths, 10⁻³ to 10⁻³⁰⁰, 1 − 10⁻³ to 1 − 10⁻¹⁵, and p either side of Acklam's switch at 0.02425; it fails at 1e-9, first at p = 0.004 with relative error 1.07e-9, *measured here* when the record was first written, by running it with the bound at 1e-9) | `Continuous.quantile` of the standard normal, itself pinned to 1e-12 by `ContinuousTests.fs`: `normal quantile against the reference` (`special.py normal`). The existing `normal quantile` test in the same file asserts absolute tolerances of 1e-8 to 1e-12 at five points, not this relative bound |
| `Continuous.cdf` | relative 1e-12 where the value is at least 1e-300; exactly 0 and 1 outside the support | normal, uniform, exponential, Cauchy, logistic at the tested parameters; log-normal for \|mu\| up to 50 at sigma 0.1 and above; Weibull for shape up to 20; gamma, chi-squared, beta, Student t and F for shapes and degrees of freedom 0.5 to 100, and at the stated wider ends (gamma 0.01 to 1000, chi-squared 0.05 to 1e5, beta 0.2 to 500, t 0.05 to 1000, F with one of d1, d2 up to 1000 and the other 0.5 to 100) | `ContinuousTests.fs`: `normal cdf and survival against the reference`, `logNormal against the reference`, `weibull survival at a large shape and a scale that is not a power of two`, `gamma against the reference`, `chi-squared against the reference`, `beta against the reference`, `Student t against the reference`, `F against the reference`, `where a plain form would cancel or round 1 - x, against the reference`, `the stated domains at their edges against the reference`; the gamma's x / scale underflow by `the gamma reads its value at 0 where x / scale underflows` | `special.py normal`, `lognormal`, `weibull`, `dist-gamma`, `dist-chi-squared`, `dist-beta`, `dist-student-t`, `dist-fisher-f`, `dist-regimes`, `dist-edges`; closed forms for the exponential, uniform, Cauchy and logistic. Between the pinned rows the five incomplete-function families rest on the uncommitted mpmath scan (see the introduction) |
| `Continuous.survival` | relative 1e-12 where the value is at least 1e-300; 1 at and below the support's lower end, exactly 0 at and above its upper end | as `cdf` | the same tests | as `cdf` |
| `Continuous.quantile` | the round trip: `cdf` at the result within relative 1e-12 of p for p ≤ ½, and `survival` within relative 1e-12 of 1 − p for p > ½, down to p = 1e-300; near 1 for the beta with a small b, where adjacent doubles move the survival by more than 1e-12 of itself, the result is within one double of the root | the domain of `cdf`, where the quantile is a normal double and the doubles beside it resolve p | `ContinuousTests.fs`: `quantile round trips`, `quantile round trips for the incomplete-function families`, `quantile round trips at the edges of the stated domains` (whose comment states the one-third rule for the p it leaves out), `gamma and chi-squared quantile round trips within 1e-14 of 1`, `log-normal quantile round trip at a large mu` | the round trip through `cdf` and `survival`, pinned by the rows above |
| `Continuous.variance` (Weibull) | relative 1e-13 for shape 10 to 1e8; within 1e-14 at shape 2; at most 1e-12 at shapes 5 and 9.9 | the Weibull | `ContinuousTests.fs`: `weibull variance against the reference at large shape`, `weibull variance against the reference below shape 10`, `weibull closed forms` | `special.py weibull` (the ζ(n) constants of the series come from `special.py zeta`) |
| `Discrete.mass` | relative 1e-12 where the value is at least 1e-300 | binomial with trials up to 1000, p 1e-10 to 0.999; Poisson with mean 0.001 to 1000; geometric p 1e-10 to 0.999; negative binomial r 0.5 to 100, p 0.002 to 0.999, mean up to 1000; hypergeometric population up to 1000; Bernoulli and discrete uniform one rounding each | `DiscreteTests.fs`: `binomial against exact fractions`, `binomial tails`, `Poisson against the reference`, `negative binomial against the reference`, `hypergeometric against exact fractions`, `the stated domains at their edges against the reference`, `where the direct mass and the rounded 1 - p lose accuracy, against the reference` | exact fractions (binomial, hypergeometric) and 50-digit decimal sums (Poisson, geometric, negative binomial) from `special.py discrete`; between the pinned rows the uncommitted scan (see the introduction) |
| `Discrete.logMass` | absolute 1e-12 where the mass is at least 1e-300 | as `mass` | `DiscreteTests.fs`: `the stated domains at their edges against the reference`, `logMass is the log of mass` | `special.py discrete` |
| `Discrete.cdf` | relative 1e-12 where the value is at least 1e-300; the Poisson's is NaN near the mean from about 1 674 055; no accuracy is stated beyond mean 1000 | as `mass` | the tests of `mass`, and `DiscreteTests.fs`: `a Poisson past its mean limit: NaN near the mean, and quantile raises`, `a Poisson at its mean limit: the quantile is the least k` | `special.py discrete` |
| `Discrete.survival` | relative 1e-12 where the value is at least 1e-300; below p = 0.002 for the negative binomial the error grows (1.9e-12 at r = 0.5, p = 5e-4) | as `mass`; the negative binomial for p ≥ 0.002 | the tests of `mass`, including `where the direct mass and the rounded 1 - p lose accuracy, against the reference` | `special.py discrete` |
| `HypothesisTest` p-values (module remarks) | the p-value has the relative error of `Continuous.cdf` and `Continuous.survival`, at most 1e-12 where it is at least 1e-300, relative to the moments' mean and variance; each function names its pinning test. A spread below about 1.5e-154 or above about 1.3e154, and a constant sample whose mean is below 2⁻⁴⁸⁰, give a NaN statistic and p-value | each function below | each row below, and `HypothesisTests.fs`: `a spread the moments cannot resolve gives NaN, not an infinite statistic`, `a spread of 1e-150 gives the scale-free statistics` (the sleep data and the ANOVA groups at 1e-150 against the unscaled reference rows) | `special.py parametric`, `counts`, `ranks` |
| `PValue.continuous` (internal, Less + Greater) | Less + Greater within 1e-15 of 1 for the t and normal distributions, within 4e-15 for the F | 5000 seeded pairs of samples | `HypothesisTests.fs`: `Less + Greater is 1 on seeded samples` | the identity cdf + survival = 1 |
| `HypothesisTest.oneSampleT` | p-value relative 1e-12 | R's `datasets::sleep` group 1 and the paired differences; 300 values with a p-value of 5.1e-300 | `HypothesisTests.fs`: `one-sample t against the reference`, `a p-value near 1e-300 and groups of unequal size against the reference` | `special.py parametric` |
| `HypothesisTest.oneSampleZ` | p-value relative 1e-12 | the test's sample | `HypothesisTests.fs`: `one-sample z against the reference` | `special.py parametric` |
| `HypothesisTest.welchT`, `pooledT` | p-value relative 1e-12 | the test's samples | `HypothesisTests.fs`: `Welch and pooled t against the reference` | `special.py parametric` |
| `HypothesisTest.varianceRatio` | p-value relative 1e-12 | the test's samples | `HypothesisTests.fs`: `variance ratio against the reference` | `special.py parametric` |
| `HypothesisTest.oneWayAnova` | p-value relative 1e-12 | the test's groups; three groups of sizes 5, 7 and 3 | `HypothesisTests.fs`: `one-way ANOVA against the reference`, `a p-value near 1e-300 and groups of unequal size against the reference` | `special.py parametric` |
| `HypothesisTest.binomial` | p-value relative 1e-12 | the rows of the test | `HypothesisTests.fs`: `exact binomial against exact fractions` | exact fractions, `special.py counts` |
| `HypothesisTest.chiSquaredGoodnessOfFit` | p-value relative 1e-12 (with the statistic) | Mendel's counts | `HypothesisTests.fs`: `goodness of fit against Mendel's counts` | `special.py counts` |
| `HypothesisTest.chiSquaredIndependence` | p-value relative 1e-12; a statistic that is not finite gives every field NaN | the tables of the test; the first table times 2^990 | `HypothesisTests.fs`: `independence against the reference`, `independence at counts near the double maximum` | `special.py counts`; homogeneity of degree 1 (identity) |
| `HypothesisTest.fisherExact` | p-value relative 1e-12 | the tea-tasting table and the tables of the second test; a zero margin gives p 1 | `HypothesisTests.fs`: `Fisher's tea-tasting table`, `Fisher against exact fractions`, `Fisher's odds ratio at a zero cell` | the closed form of the tea table; exact fractions, `special.py counts` |
| `HypothesisTest.mannWhitney` | p-value relative 1e-12 against the approximating formula (R computes it exactly for fewer than 50 observations per sample, so this is not R's value there) | three data sets: ties, U on each side of its mean, U at its mean | `HypothesisTests.fs`: `Mann–Whitney against the reference` | `special.py ranks` |
| `HypothesisTest.wilcoxonSignedRank` | p-value relative 1e-12 against the approximating formula | three data sets (ties and a zero, a zero without ties, V at its mean), and 100000 differences of ±1 | `HypothesisTests.fs`: `Wilcoxon on the sleep differences`, `Wilcoxon against the reference`, `the continuity correction and the tie correction by closed forms` | `special.py ranks`; closed forms for the ±1 case |
| `HypothesisTest.kolmogorovSmirnov` | p-value relative 1e-12 against the Kolmogorov series (at most 3 terms for λ from 0.04 to 6); a p-value below 1e-300 (λ from about 18.6) carries no relative bound | the series on each side of the switch at λ = 1.18 (λ of 0.52, 1.17, 1.19), small and large λ | `HypothesisTests.fs`: `one-sample Kolmogorov–Smirnov`, `the Kolmogorov series on each side of the switch at lambda 1.18`, `Kolmogorov–Smirnov at small and large lambda` | `special.py ranks`, which also asserts the term count on a grid of step 0.001 (the check is in the script; *measured here* on 2026-11-21 by `python -I tests/reference/special.py ranks`, which exited 0, the script exiting non-zero if the double series needs more than 3 terms) |
| `HypothesisTest.kolmogorovSmirnovTwoSample` | p-value relative 1e-12 against the series | λ of 0.89 and 1.73, with a tied value | `HypothesisTests.fs`: `two-sample Kolmogorov–Smirnov` | `special.py ranks` |
| `MeanInterval.student` | bound relative 1e-12; NaN bounds for a spread the moments cannot resolve | ten observations, α 0.05 and 0.01; the doc reports the largest relative error as 1.5e-16 (*measured here* on .NET by a one-off F# script, not kept) | `IntervalTests.fs`: `the Student interval against the reference`, `small samples give NaN bounds`, `a spread the moments cannot resolve gives NaN bounds, not a zero-width interval`; the coverage of the 95% interval on 2,000 seeded samples by `the Student interval covers at its level` | `special.py intervals` |
| `MeanInterval.knownSigma` | bound relative 1e-12 | ten observations, σ 3, α 0.05 | `IntervalTests.fs`: `the known-sigma interval against the reference` | `special.py intervals` |
| `ProportionInterval.wilson`, `clopperPearson`, `agrestiCoull`, `jeffreys` | each bound relative 1e-12 (the doc reports the largest relative error as 3.7e-16, one-off script not kept); every bound in [0, 1] | (81, 263) at α 0.05 and 0.01, (0, 20), (20, 20), (1, 29) | `IntervalTests.fs`: `proportion intervals against the reference`, `proportion intervals at 0 and n successes`, `Clopper-Pearson at one trial and at zero successes has a closed form` | `special.py intervals`; the closed form 1 − (α/2)^(1/n) |
| `VarianceInterval.chiSquared` | bound relative 1e-12 (largest 2.2e-16, one-off script not kept); NaN bounds for a spread the moments cannot resolve | ten observations, α 0.05 | `IntervalTests.fs`: `the variance interval against the reference`, `a spread the moments cannot resolve gives NaN bounds, not a zero-width interval` | `special.py intervals` |
| `DifferenceInterval.welch`, `pooled` | bound relative 1e-12 (largest 7.4e-17, one-off script not kept); NaN bounds for a spread the moments cannot resolve | R's `datasets::sleep`, α 0.05 | `IntervalTests.fs`: `difference intervals against the reference`, `a spread the moments cannot resolve gives NaN bounds, not a zero-width interval` | `special.py intervals` |
| `ConfidenceSequence.ofMoments`, `FixedSample.ofMoments`, `SequentialTest.add` | not an accuracy bound: NaN bounds, or a NaN p-value for good, where the spread is below about 1.5e-154 or above about 1.3e154 | 200 values centred on 0 at the scales 1e-200 and 1e-162 | `ConfidenceSequenceTests.fs`: `a spread the moments cannot resolve gives NaN bounds`; `SequentialTestTests.fs`: `a spread the moments cannot resolve gives a NaN p-value, not a rejection` | the scale of the data (a true null) |
| `Regression.simple` | relative 1e-12 for every field where 1 − R² is at least about 2e-4; the standard errors lose about ε/(1 − R²) relative, and are NaN where 1 − R² ≤ 64ε or var_y = 0 beside a non-zero covariance | the 10 pairs of the test; var_x, var_y and cov finite and, where not 0, normal doubles (spreads about 1e-154 to 1e154); x counts as constant where var_x ≤ (8ε\|x̄\|)² | `RegressionTests.fs`: `simple regression against exact fractions`, `simple regression gives NaN standard errors where 1 - R squared is rounding` (amp 1e-5 within 1e-4 of `linear`'s, amp 1e-8 and y · 1e-165 NaN), `simple regression at the edges of the spreads Comoments can hold`, `NaN comoments give a simple regression of NaN fields`, `x emptied back to a constant by removals gives a NaN slope`, `a residue left by removals from far away is not detected` | exact fractions, `special.py regression`; `Regression.linear` |
| `Regression.linear` | relative 1e-12 for every field; residuals within absolute 1e-12 | well-conditioned designs: the integer designs (1, x1, x2) and (x1, x2) of 12 rows, and (1, x) of 12 rows with y offset by 1e8; without a constant column an offset in y costs about ε·\|offset\|/spread (*general knowledge*) | `RegressionTests.fs`: `linear regression against exact fractions`, `regression without a constant against exact fractions`, `a dominant weight and an offset in y give the exact fractions` | exact fractions, `special.py regression` |
| `Regression.weighted` | relative 1e-12 for every field | (1, x1), weights 1, 2, 3 with one 0; (1, x) with one weight from 1e4 to 1e15 beside weights of 1 and y offset by up to 1e10 (seven rows); a weight of 1e30 on the first row of (1, x1) | `RegressionTests.fs`: `weighted regression against exact fractions`, `a dominant weight and an offset in y give the exact fractions`, `a dominant weight gives every field at the exact fraction`, `the essentially perfect test reads the unweighted fitted values`; the limit at a weight ratio of 1e30 by `a weight ratio of 1e30 can read as rank-deficient` | exact fractions, `special.py regression` (its `weighted`, `dominant` and `stiffCases` tables) |
| `Regression.polynomial` | coefficients within absolute 1e-12 | the degree 2 polynomial through 8 integer points; no bound shown for higher degrees; powers that overflow give NaN fields and powers that underflow read as rank-deficient | `RegressionTests.fs`: `polynomial regression recovers exact coefficients`, `polynomial names itself and reads every x` | the exact coefficients (closed form) |
| `Regression.curve` | parameters within relative 1e-8 and the residual standard error below 1e-8 on y = 2.5 e^(−1.3x); on the 10-point line the parameters within 1e-8 and the standard errors and covariance within relative 1e-6 of `Regression.linear`'s (the forward difference limits them to about √ε ≈ 1.5e-8, *general knowledge*); for an intercept fitted near 0 the parameters and standard errors within 1e-6 of `linear`'s | the 21 points of the first test (start (1, 1)); the 10-point line; y = 2x plus noise of 1e-6 and 1e-3 from (1, 1) | `RegressionTests.fs`: `curve fitting recovers exact parameters`, `a model linear in its parameters gives the linear regression`, `a parameter fitted near 0 keeps the standard errors of linear`, `a parameter driven out of the doubles' range does not converge` | the exact parameters 2.5 and −1.3 (closed form); `Regression.linear`, pinned by the rows above |
| `Correlation.pearson` | relative 1e-12 | the 8 permutation pairs of the test; scales 1e200 and 1e-200 | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Pearson at the scales of 1e200 and 1e-200`, `perfect correlation gives an infinite t and a p-value of 0` | exact fractions, `special.py correlation` |
| `Correlation.spearman` | relative 1e-12 | untied permutations; the tied 8 pairs | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Spearman with ties against the reference` | exact average ranks; Pearson on untied data (identity); `special.py correlation` |
| `Correlation.kendall` | relative 1e-12 against the reference; within 1e-14 of a direct count of every pair | untied and tied 8 pairs; 39 generated samples of sizes 2 to 40 with ties | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Kendall tau-b matches a direct count`, `Kendall over generated samples with ties matches the direct count and variance` | the direct pair count written in the test (`countPairs`); `special.py correlation` |
| `Correlation.pearsonTest` | statistic and p-value within 1e-12 relative of exact fractions; near \|r\| = 1 the statistic's relative error grows as about ε/(1 − \|r\|) (*general knowledge*) | the 8 pairs of the test | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions` | exact fractions, `special.py correlation` |
| `Correlation.spearmanTest` | statistic and p-value within 1e-12 relative of exact fractions | the tied 8 pairs of the test | `CorrelationTests.fs`: `Spearman with ties against the reference` | exact average ranks, `special.py correlation` |
| `Correlation.kendallTest` | statistic and p-value within 1e-12 relative of the reference; the statistic within 1e-12 of S over R's variance | untied and tied 8 pairs; sizes 3 to 40 with ties | `CorrelationTests.fs`: `Pearson, Spearman and Kendall against exact fractions`, `Kendall tau-b matches a direct count`, `Kendall over generated samples with ties matches the direct count and variance` | `special.py correlation`; R's variance of S computed in the test (`varianceOfS`) |
| `Bootstrap.standardError` | relative 1e-12 of the spread of the statistics taken about their offset | 400 resample means of 50 values at an offset of 1e9 | `ResamplingTests.fs`: `the bootstrap standard error of statistics with a large offset` | the recorded statistics minus 1e9, exact by Sterbenz, folded by `Moments` |
| `Density.gaussianKernel` | where x − xᵢ overflows the true value is below 1e-307, so the 0 returned is within the smallest normal double of it (*general knowledge*: u·φ(u) ≤ φ(1)) | extreme scales | `ResamplingTests.fs`: `the estimate at extreme scales is exact or honestly zero` | the closed form of the normal density |
| `Density.silvermanBandwidth` and `scottBandwidth` (module remarks) | relative 1e-13 against exact rational arithmetic, *measured here* by the test | four rows: offsets 1 to 1e9 with spreads 1e-9 to 1e-3, 50 doubles offset + i·spread | `ResamplingTests.fs`: `bandwidths of offset data match exact rational arithmetic` | `special.py resampling` (rationals and 60-digit decimals, rounded once) |

### Measured figures that the doc comment says are not bounds

The `Continuous` module remarks say `density` and `logDensity` carry no bound beyond the accuracy of the functions they call;
the test asserts the figures below at the ends of each range.

| Kernel | Figure | Domain | Test | Reference |
|---|---|---|---|---|
| `Continuous.density` and `logDensity` (module remarks) | measured, not a bound: the test asserts relative 1e-12, 2e-12 at gamma shape 1000 and 2e-10 at chi-squared 1e5; between the ends the uncommitted mpmath scan (see the introduction) | the ends of each stated range of the gamma, chi-squared, beta, Student t and F | `ContinuousTests.fs`: `the stated domains at their edges against the reference` | `special.py dist-edges` |

### Limits of the contract bounds

Each limit below is stated in the doc comment named.

- The proportion intervals (`ProportionInterval` remarks) and the variance interval (`VarianceInterval.chiSquared`) read a
  bound at 1 − α/2, whose rounding costs accuracy at small α (2.9e-10 to 3.7e-9 relative at α = 1e-10), and a beta
  quantile carries its round trip, not a relative error (up to 5.7e-11 for n of 1e5 or more and at most 3 successes). Both
  figures come from an mpmath scan in the review of Plan T3 Task 5, not committed (spec §5.2), as the doc comments say.
- `HypothesisTest.fisherExact`: the bound is pinned on tables of totals up to 30; a review measured it against exact
  rational arithmetic up to a total of 3400, with the error growing with the total (not committed).
- `Regression.weighted`: a weight ratio of about 1e30 or more can make a full-rank design read as rank-deficient, as
  `Matrix.qr`'s rank test is relative to the largest column norm (pinned, row above); a positive weight below about 1e-323
  times the largest counts as 0 (*general knowledge*).
- `Regression.curve`: a parameter fitted within its rounding of 0, far below its start, can end the fit with `Converged`
  false although its parameters and standard errors match `linear`'s (the noise of 1e-6 in the curve row's third test; the
  test does not assert `Converged` there).
- The t tests, intervals and sequential test of a constant sample whose mean is below 2⁻⁴⁸⁰ in magnitude, zeros included,
  give NaN where an exactly zero spread elsewhere gives ±∞ or a zero-width interval: below that mean an underflowed spread
  cannot be told from none (the `HypothesisTest` remarks).

### Not accuracy bounds, left out

- `ConfidenceSequence`'s coverage figures: 0.0415 of 2,000 streams excluded the mean at α = 0.05, and 0.009 at α = 0.0125,
  both *measured here* on 2026-11-20 and reproduced by every run of the `ConfidenceSequence` test list (printed as
  "Coverage under continuous monitoring" and "Coverage at the page's setting"). The guarantee is asymptotic and states no
  coverage at a finite count, so it is not an accuracy bound. The tests are `ConfidenceSequenceTests.fs`: `the sequence
  covers under continuous monitoring and the fixed-sample interval does not` and `the sequence covers at the page's
  setting`.
- `Sequential`'s duality with the confidence sequence, which the doc comment says can fail within rounding of an endpoint.
