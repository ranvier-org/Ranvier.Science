# Ranvier.Science toolkit (0.2.0) design

Date: 2026-11-20
Status: written under the owner's standing instruction of 2026-11-20 to finish the implementation, review, docs and
publication without stopping for approvals. Every call below is the author's (Claude's) unless it cites the owner; the
owner may revise any of them.
Baseline: `Ranvier.Science` `main` at `f329053` (packages 0.1.0 on core `Ranvier` 0.2.0); core `Ranvier` 0.2.1 on
nuget.org.
Inputs: the slice's design (`Ranvier/docs/superpowers/specs/2026-11-19-science-vertical-slice-design.md`, §2 non-goals),
its findings (`docs/FINDINGS.md`, §3 and §9), and the research synthesis
(`Ranvier/docs/.ai/RESEARCH-science-synthesis.md`, §3.6 and §4).

Every figure carries a label: *measured here* (with what was run), *from documentation* (with the file or page), or
*general knowledge* (not checked). ⚠️ marks a value the builder confirms against its primary source before using it.

## 1. Intent

The owner asked for Ranvier.Science to be filled out "with the full toolkit that one might expect from such a suite".
Today the three packages hold what the slice needed and little else, about 1,350 lines: compensated sums, moments,
maintained aggregates, tolerances and a generator; units, models, a one-line formula notation and runs; a normal
quantile, a confidence sequence and a fixed-sample interval.

A person who picks up a numerics, quantities and statistics suite expects to find, at least: special functions, root
finding and minimisation, quadrature, ODE integration, interpolation, dense linear algebra, a Fourier transform, a set of
accumulators for streams and windows, random variates; a catalogue of units with prefixes, parsing and conversion,
physical constants with their uncertainty, and propagation of uncertainty; probability distributions, descriptive
statistics, confidence intervals, hypothesis tests, multiple-testing corrections, regression, correlation, curve
fitting, resampling and density estimation.

What makes this suite more than a port of those algorithms is where they live: on the reactive graph, on .NET and in the
browser from one source. So the toolkit has two kinds of part:

- **Kernels**: plain functions over numbers and arrays. Each one sits inside a single node when used on the graph (the
  research's grain rule: a node holds a quantity, an array or a block, synthesis §3.2).
- **Graph-aware parts**: maintained and windowed folds, formulas that become model entries, uncertainty as a tracked
  read, a simulation run with an integrator, and the interface fixes the slice's findings asked for before widening.

## 2. Goals, non-goals, anti-goals

**Goals.** The work is done when all of these hold.

1. Every public function named in sections 6 to 8 exists, carries a doc comment that states its contract and its accuracy
   bound where it computes an approximation, and has tests that run on .NET and, through Fickl, on JavaScript.
2. Every accuracy bound a doc comment states is pinned by a test against a reference value whose source is named beside
   it (section 5).
3. Kernels built only from `+ − × ÷` and `sqrt` give identical results on both targets. `CrossTargetTests.fs` pins at
   least one exact case for each of: LU solve, QR least squares, Cholesky solve, the moments family (comoments, higher
   and weighted moments), interpolation, RK4 and the Fourier transform's butterflies on a power-of-two length with
   twiddles taken from a table both targets compute alike (section 5.3).
4. The interface changes of section 4 are made, the Monitor example, the benchmarks and the tests are migrated, and
   `docs/FINDINGS.md` §3 marks each item, every bullet of item 7 included, as resolved (with a pointer), not applicable,
   or deferred (with its reason).
5. `docs/GUIDE.md` walks through at least ten tasks a scientist would bring (section 9); every snippet in it is the text
   of a test that compiles and runs on both targets.
6. The three packages are at 0.2.0 on core `Ranvier` 0.2.1, build, test and pack in CI, and publish from CI. Publication
   waits on the owner re-saving the nuget.org trusted-publishing policy for the recreated repository: the publish run
   44667750478 of 2026-11-20 failed its token exchange with "No match", the policy still naming the repository that
   became the private archive (*measured here*: that run's log).

**Non-goals.** Out of 0.2.0, each a candidate for a later round:

- Delivery components (unit-aware axes, uncertainty bands, a visible pending state), a notebook or cell host, session
  persistence, lineage and a durable cache, a C# surface (synthesis §4, layers 3, 4 and "Across").
- Stiff and implicit ODE methods, DAEs and PDEs; sparse matrices, the SVD and non-symmetric eigenproblems.
- Bessel, elliptic and zeta functions.
- Exact small-sample distributions for Kolmogorov–Smirnov, Mann–Whitney and Wilcoxon; Shapiro–Wilk; multivariate
  distributions; Bayesian inference and MCMC.
- Evaluating an F#-defined graph over many input vectors. It needs the core (a graph per scenario, or a fork), so it is
  a core proposal; 0.2.0 evaluates formulas over many vectors inside one node instead (section 8.6).
- Affine units (degree Celsius, degree Fahrenheit).
- A two-sample sequential test with unequal allocation; a multi-arm sequential test.
- Any change under `Ranvier/src`. Core changes stay proposals (`docs/FINDINGS.md` §2).

**Anti-goals.** The work has failed, even if it runs, when any of these is true.

- A node per arithmetic operation or per array element appears anywhere (carried from the slice).
- The package keeps its own dependency tracker, cache or scheduler beside the core's (carried from the slice).
- A kernel's result differs between the targets by more than the bound its doc comment states.
- A function returns a plausible number where it failed to converge or met a singular input, with nothing that says so.
  Failure is reported through `Result` or a `Converged` field (section 3.2).
- A test pins a reference value with no stated source.
- A reason of the form "another tool already does this" decides any part of the design (the owner's evaluation rule).

## 3. Constraints and conventions

### 3.1 Carried from the slice

- Library code compiles with Fable: no reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`.
- `FSharpCoreImplicitPackageVersion` 8.0.100, `Fable.Package.SDK` 1.4.1 with `FablePackageType=library`, MIT license.
  Core `Ranvier` 0.2.1 also depends on FSharp.Core 8.0.100 (*from documentation*: its nuspec on nuget.org).
- British spelling. Doc comments follow `Ranvier/.claude/rules/comments.md`: state the contract, the raise conditions,
  the cost where it is not O(1), and the accuracy bound; no narration.
- The version is set by hand in `src/Directory.Build.props`. Only the three `src/` libraries are packable.
- Tests live in `tests/Ranvier.Science.Tests` and are listed in `TestFiles.props`, which the Fable suite shares.

### 3.2 Error conventions (the author's call)

| Situation | Convention |
|---|---|
| An argument outside the function's domain (a negative standard deviation, `p` outside [0, 1], mismatched lengths) | Raise `ArgumentOutOfRangeException` or `ArgumentException`, as the 0.1.0 code does |
| A NaN data argument (an observation, an x at which to evaluate) where the domain is otherwise satisfied | Return NaN (or NaN fields), raise nothing |
| A NaN configuration parameter (a distribution's or variate's parameter, a rate, a half-life, a tolerance, settings) | Raise `ArgumentOutOfRangeException`, as an out-of-domain value does |
| A data-dependent failure of a direct method (a singular matrix, a root not bracketed, a non-positive-definite matrix) | `Result<'T, string>`, the message naming the condition |
| An iterative method that may stop before its tolerance (minimisation, least squares, adaptive quadrature, ODE steps) | A record with the best result found and `Converged: bool`, or `Result` when no usable result exists (an ODE step size underflow) |

### 3.3 Graph conventions

- A kernel never reads or creates a node. The graph-aware parts state how many nodes they create.
- An accumulator is an immutable value with `add` (and `remove`, `merge`, `unmerge` where the algebra allows), so it
  folds through `createStreamFold`, `Projection.foldGroup` and `Aggregate.maintainedBy` unchanged. Accumulators that
  keep running sums take the `compensated: bool` argument first, as `Sum` and `Moments` do; `DecayedMoments` (section
  6.8) takes none, since its weights shrink at every item.
- **Cut-off.** The core's default equality, `JsIdentityPolicy`, compares a `float` by value and every reference type by
  reference on .NET, and everything but primitives by reference under Fable (*from documentation*: core v0.2.1
  `src/Ranvier/Types.fs`, `ComparerResolution.resolve` and `GraphOptions.Default`; `docs/PROBES.md`, probe E3). So a
  memo that returns a new record, union, array or struct notifies its readers even when the value is equal. The
  library's types keep F# structural equality, which tests use and which `StructuralPolicy` applies on .NET, but no doc
  comment claims a cut-off from it. Under the default policy a reader cuts off by returning the previous instance, as
  `Tolerance.holdSeries` and `Quantity.blockFrom` already let it. The library's own memos either return `float` (which
  cuts off on both targets) or say in their doc comment that they publish on every recomputation. This is the author's
  answer to the builder check's first question (section 13).

## 4. Interface changes (breaking; 0.2.0)

Each resolves an item of `docs/FINDINGS.md` §3.

| # | Change | Resolves |
|---|---|---|
| I1 | One unit-equality rule: `UnitInfo.sameUnit a b` is true when the dimensions are equal and `|a.Scale − b.Scale| ≤ 1e-12 · max(|a.Scale|, |b.Scale|)`, symmetric in its arguments. `UnitInfo.equivalent` is removed; the formula checker uses `sameUnit`. Tests sit at the boundary in both argument orders | §3.1 |
| I2 | `UnitInfo.multiply`, `divide`, `power` and `sqrt` raise `ArithmeticException` when the resulting scale is not finite or is 0; `Formula.check` turns that into an `Error` naming the unit | §3.7, ruling R-L |
| I3 | `FloatPolicy.standard` carries `RefoldFloor = 64`. A test measures the largest distance in ulps between the maintained value and the from-scratch fold over a stated workload between re-folds, and the doc comment states that bound as *measured here* | §3.3 |
| I4 | The burn-in moves into the sequence (signatures below); `ofMomentsAfter` is removed, and `ofMoments`, `radius` and `rhoTightestAt` stay public as they are. The module's doc comment states the coverage measured at each setting the coverage test runs | §3.2 |
| I5 | `Entry` gains `Uncertainty`, `Measured` and `Formula` (section 8.4). A formula's result can be an entry (`Quantity.formula`), and a typed view of an entry is checked at run time (`Quantity.view`) | §3.4 |
| I6 | An entry leaves its model when the owner that declared it is cleaned up; the identifier may then be declared again (section 8.5) | §3.5 |
| I7 | The packages depend on core `Ranvier` 0.2.1 | — |
| I8 | The measures `m`, `kg`, `s` and `K` become abbreviations of FSharp.Core's `Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols` measures, so a `float<UnitSymbols.m>` and a `float<m>` are one type. Source-compatible | — |
| I9 | `Moments` gains `sampleVariance` (divisor n − 1; NaN below 2), `standardDeviation` and `sampleStandardDeviation`. Additive | — |

I4's signatures:

```fsharp
type SequenceSettings = { Alpha: float; Rho: float; BurnIn: float }   // BurnIn a count, as Moments.count is a float
ConfidenceSequence.settings : alpha: float -> rho: float -> burnIn: float -> SequenceSettings
ConfidenceSequence.tunedFor : alpha: float -> plannedCount: float -> burnIn: float -> SequenceSettings
                              // Rho = rhoTightestAt alpha plannedCount
ConfidenceSequence.standardBurnIn : float                             // 30, the setting the coverage test measures
ConfidenceSequence.interval : SequenceSettings -> Moments -> Interval  // NaN bounds while the count is below BurnIn
```

`settings` and `tunedFor` raise as `radius` and today's `ofMomentsAfter` do; `interval` checks the fields again, so a
record built directly is held to the same rules.

The other §3 items are dispositioned in `docs/FINDINGS.md` §3 itself:

- §3.6 (drawing helpers that take values) and §3.7's tracked Attitude chart frame concern the AstroShowcase pages, which
  left this repository on 2026-11-20: not applicable.
- §3.7's page-record fields (`Internals`, `Schema`, `Session.Graph`), the view's `each` over a projection and
  `Ranvier.D5.View`'s listeners belong to the delivery layer, a non-goal of 0.2.0: deferred.
- §3.7's `Run` against `Flow.Reset ()`: deferred; `Run.ode` (section 8.7) adds no reset.

## 5. Accuracy and references

### 5.1 Bounds

Each approximating kernel states its bound in its doc comment, in the form "relative error at most E over D" (or
absolute, near zero). The bounds the author sets as targets; the builder may tighten them where the tests show more, and
may loosen one only with a ledger ruling and a doc comment that says why:

| Kernel | Target |
|---|---|
| `gammaLn`, `gamma`, `beta`, `erf`, `erfc`, `digamma` | relative 1e-13 on their stated domains, where the value is at least 1e-300 (for `erfc`, x up to about 26.2; past that it is subnormal) |
| Regularised incomplete gamma and beta | relative 1e-12 where the value is at least 1e-300 |
| Their inverses | round trip: the forward function at the result within 1e-12 relative of the target probability |
| Distribution `cdf` and `survival` | relative 1e-12 where the value is at least 1e-300 |
| Distribution `quantile` | round trip as the inverses |
| Adaptive quadrature, ODE solve | the requested tolerance, checked on problems with closed-form answers |

### 5.2 Reference values

A reference value comes from one of:

- **A closed form or identity**, written in the test: Γ(n) = (n − 1)!, the Student t with 1 degree of freedom is the
  Cauchy, the χ² with 2 degrees of freedom is the exponential, I_x(1, 1) = x, and so on.
- **A reference script** under `tests/reference/`, run with Python 3.14's standard library alone (`math`, `decimal`,
  `fractions`, `statistics`). No third-party package is installed for it. The script computes in high precision
  (`decimal` at 50 digits or more, or exact `fractions`), prints F# literals, and is committed with the command that
  produced the literals named in the test file beside them (*measured here*).
- **A published table or standard** (the SI Brochure, CODATA, IAU resolutions), cited by name and edition beside the
  value (*from documentation*).

### 5.3 Cross-target equality

Addition, subtraction, multiplication, division and square root are correctly rounded IEEE 754 binary64 operations on
both targets, and neither the .NET JIT nor a JavaScript engine contracts them into fused multiply-adds (*general
knowledge* ⚠️; the exact cross-target tests of goal 3 are the check). `Math.Exp`, `Math.Log`, `Math.Sin` and their
kin are not correctly rounded and may differ between targets in the last place. So a kernel that uses only the first
group gives the same bits on both targets, and goal 3 pins that. A kernel that uses the second group meets its bound on
each target separately, and its cross-target tests compare within that bound. The Fourier transform's twiddle factors
come from `cos` and `sin`; its exact cross-target case uses a length whose twiddles are exact (n = 4) or a table both
targets build from the same correctly rounded operations, the builder's choice.

## 6. Ranvier.Numerics additions

Signatures are the contract; parameter order follows the 0.1.0 code (configuration first, data last, so partial
application builds folds). Names in this section are fixed; private helpers and file layout are the builder's.

### 6.1 Special functions: `Special`

```fsharp
Special.gammaLn : float -> float                 // ln |Γ(x)|; x > 0 and non-integer negative x
Special.gamma : float -> float                   // Γ(x), with the reflection formula below 0.5; +∞ past 171.62
Special.digamma : float -> float
Special.beta : float -> float -> float
Special.betaLn : float -> float -> float
Special.logFactorial : int -> float              // ln n!, n ≥ 0
Special.logChoose : int -> int -> float          // ln C(n, k), 0 ≤ k ≤ n
Special.erf : float -> float
Special.erfc : float -> float
Special.log1p : float -> float                   // ln(1 + x), relative 1e-15 near 0
Special.expm1 : float -> float                   // eˣ − 1, relative 1e-15 near 0
Special.gammaLowerRegularized : float -> float -> float          // P(a, x)
Special.gammaUpperRegularized : float -> float -> float          // Q(a, x), accurate where P is near 1
Special.gammaLowerRegularizedInverse : float -> float -> float   // x with P(a, x) = p
Special.betaRegularized : float -> float -> float -> float       // I_x(a, b)
Special.betaRegularizedInverse : float -> float -> float -> float
```

The methods are the builder's (Lanczos or a Stirling series with shifts for Γ; Cody's rational approximations or an
equivalent for `erf`; series and Lentz continued fractions for the incomplete functions; safeguarded Newton or Halley
for the inverses).

### 6.2 Roots, minimisation and least squares: `Roots`, `Minimize`, `LeastSquares`

```fsharp
Roots.bisection : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Result<float, string>
Roots.brent : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Result<float, string>
Roots.newton : tolerance: float -> f: (float -> float) -> df: (float -> float) -> x0: float -> Result<float, string>

type Minimum = { X: float; Value: float; Iterations: int; Converged: bool }
Minimize.brent : tolerance: float -> f: (float -> float) -> a: float -> b: float -> Minimum      // on [a, b]
type MinimumN = { X: float[]; Value: float; Iterations: int; Evaluations: int; Converged: bool }
type NelderMeadOptions = { Tolerance: float; InitialStep: float; MaximumIterations: int }
NelderMeadOptions.standard : NelderMeadOptions
Minimize.nelderMead : NelderMeadOptions -> f: (float[] -> float) -> x0: float[] -> MinimumN

type LeastSquaresOptions = { Tolerance: float; MaximumIterations: int }
LeastSquaresOptions.standard : LeastSquaresOptions
type LeastSquaresResult =
    { Parameters: float[]; Residuals: float[]; Jacobian: Matrix; Cost: float; Iterations: int; Converged: bool }
LeastSquares.levenbergMarquardt :
    LeastSquaresOptions -> residuals: (float[] -> float[]) -> initial: float[] -> LeastSquaresResult
LeastSquares.levenbergMarquardtWith :
    LeastSquaresOptions -> residuals: (float[] -> float[]) -> jacobian: (float[] -> Matrix) -> initial: float[]
    -> LeastSquaresResult
```

`tolerance` is absolute in x. A root search that is not bracketed (`f a` and `f b` of one sign) returns `Error`; Newton
that leaves the finite numbers or exceeds its iteration limit returns `Error`. The iteration limits are constants the doc
comments state. `levenbergMarquardt` uses forward differences for the Jacobian; `Cost` is half the sum of squared
residuals.

### 6.3 Calculus: `Integrate`, `Differentiate`

```fsharp
type Integral = { Value: float; ErrorEstimate: float; Evaluations: int; Converged: bool }
Integrate.adaptive : absolute: float -> relative: float -> f: (float -> float) -> a: float -> b: float -> Integral
Integrate.gaussLegendre : points: int -> f: (float -> float) -> a: float -> b: float -> float
Integrate.trapezoid : xs: float[] -> ys: float[] -> float
Integrate.simpson : xs: float[] -> ys: float[] -> float          // composite, uneven spacing allowed
Integrate.cumulativeTrapezoid : xs: float[] -> ys: float[] -> float[]

type Derivative = { Value: float; ErrorEstimate: float }
Differentiate.derivative : f: (float -> float) -> x: float -> Derivative
```

`adaptive` is a globally adaptive Gauss–Kronrod (7, 15) rule with a stated subdivision limit; either bound may be
infinite. `Converged` is false when the error estimate exceeds `max(absolute, relative · |Value|)` at the limit.
`derivative` is Ridders' extrapolation of central differences, or an equivalent the doc comment names.

### 6.4 ODE integration: `Ode`

```fsharp
type OdeOptions =
    { AbsoluteTolerance: float; RelativeTolerance: float; InitialStep: float; MaximumStep: float; MaximumSteps: int }
OdeOptions.standard : OdeOptions          // InitialStep 0 means chosen by the solver
type OdeSolution = { Times: float[]; States: float[][]; Accepted: int; Rejected: int; Evaluations: int }

Ode.rungeKutta4Step : f: (float -> float[] -> float[]) -> t: float -> y: float[] -> h: float -> float[]
Ode.rungeKutta4 : f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> t1: float -> steps: int -> float[]
Ode.solve : OdeOptions -> f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> t1: float
            -> Result<OdeSolution, string>
Ode.solveAt : OdeOptions -> f: (float -> float[] -> float[]) -> t0: float -> y0: float[] -> times: float[]
              -> Result<float[][], string>
```

`solve` is the Dormand–Prince 5(4) pair with step-size control; `solveAt` returns the states at `times` (ascending, the
first at least `t0`) from the method's continuous extension. Integration runs forward only (`t1 ≥ t0`, raise
otherwise). `Error` reports a step size below the representable spacing at t, or `MaximumSteps` reached. Inputs are never
mutated.

### 6.5 Interpolation: `Interpolate`, `Interpolant`

```fsharp
type Interpolant            // structural equality; holds copies of the arrays
Interpolate.linear : xs: float[] -> ys: float[] -> Interpolant
Interpolate.naturalCubic : xs: float[] -> ys: float[] -> Interpolant
Interpolate.monotoneCubic : xs: float[] -> ys: float[] -> Interpolant     // Fritsch–Carlson
Interpolant.evaluate : Interpolant -> x: float -> float                    // NaN outside [xs[0], xs[n − 1]]
Interpolant.derivative : Interpolant -> x: float -> float
```

`xs` strictly increasing with at least two points and `ys` of the same length, else raise.

### 6.6 Linear algebra: `Vector`, `Matrix` and decompositions

```fsharp
Vector.dot, Vector.add, Vector.subtract : float[] -> float[] -> ...
Vector.scale : float -> float[] -> float[]
Vector.norm : float[] -> float                   // Euclidean, scaled against overflow

type Matrix                                      // dense, row-major, immutable; structural equality
Matrix.ofRows : float[][] -> Matrix              // copies; rows of one length
Matrix.init : rows: int -> columns: int -> (int -> int -> float) -> Matrix
Matrix.identity : int -> Matrix
Matrix.zero : rows: int -> columns: int -> Matrix
Matrix.diagonal : float[] -> Matrix
Matrix.rowCount, Matrix.columnCount : Matrix -> int
Matrix.get : Matrix -> int -> int -> float
Matrix.row, Matrix.column : Matrix -> int -> float[]
Matrix.toRows : Matrix -> float[][]
Matrix.transpose : Matrix -> Matrix
Matrix.add, Matrix.subtract, Matrix.multiply : Matrix -> Matrix -> Matrix
Matrix.scale : float -> Matrix -> Matrix
Matrix.multiplyVector : Matrix -> float[] -> float[]

type LuDecomposition
Matrix.lu : Matrix -> Result<LuDecomposition, string>          // partial pivoting; Error when singular
LuDecomposition.solve : LuDecomposition -> float[] -> float[]
LuDecomposition.determinant : LuDecomposition -> float
Matrix.solve : Matrix -> float[] -> Result<float[], string>
Matrix.inverse : Matrix -> Result<Matrix, string>
Matrix.determinant : Matrix -> float

type CholeskyDecomposition
Matrix.cholesky : Matrix -> Result<CholeskyDecomposition, string>  // Error unless symmetric positive definite
CholeskyDecomposition.solve : CholeskyDecomposition -> float[] -> float[]

type QrDecomposition
Matrix.qr : Matrix -> QrDecomposition                            // Householder; rows ≥ columns, else raise
QrDecomposition.leastSquares : QrDecomposition -> float[] -> Result<float[], string>   // Error when rank-deficient
QrDecomposition.r : QrDecomposition -> Matrix          // the upper-triangular factor, columns × columns

type SymmetricEigen = { Values: float[]; Vectors: Matrix }        // ascending values; vectors as columns
Matrix.symmetricEigen : Matrix -> Result<SymmetricEigen, string>  // cyclic Jacobi; Error unless symmetric
```

The rank and singularity thresholds are relative to the matrix's scale and are stated in the doc comments. Dimension
mismatches raise.

### 6.7 Fourier transform: `Fourier`

```fsharp
Fourier.forward : re: float[] -> im: float[] -> struct (float[] * float[])
Fourier.inverse : re: float[] -> im: float[] -> struct (float[] * float[])     // includes the 1/n
Fourier.realForward : float[] -> struct (float[] * float[])                     // the n/2 + 1 non-negative bins
Fourier.powerSpectrum : float[] -> float[]
Fourier.frequencies : n: int -> spacing: float -> float[]
Fourier.convolve : float[] -> float[] -> float[]                               // linear convolution
```

Any length: iterative radix-2 for powers of two, Bluestein's algorithm otherwise. Complex values are two arrays, so
nothing depends on `System.Numerics.Complex` under Fable.

### 6.8 Accumulators

All immutable, folding as section 3.3 describes. Remove and unmerge raise `InvalidOperationException` on an empty
accumulator, as `Moments.remove` does.

| Type | Operations | Reads |
|---|---|---|
| `Comoments` (bivariate) | `zero`, `add c s x y`, `remove`, `merge`, `unmerge` | `count`, `meanX`, `meanY`, `varianceX`, `varianceY`, `covariance`, `sampleCovariance`, `correlation`, `slope`, `intercept` |
| `HigherMoments` (to the fourth, Pébay's update formulas) | `zero`, `add`, `remove`, `merge`, `unmerge` | `count`, `mean`, `variance`, `sampleVariance`, `skewness`, `kurtosis` (excess), `toMoments` |
| `WeightedMoments` (weights ≥ 0) | `zero`, `add c s x w`, `remove`, `merge`, `unmerge` | `count`, `totalWeight`, `mean`, `variance`, `frequencyVariance`, `reliabilityVariance`, `effectiveCount` |
| `Histogram` (fixed edges; NumPy's bin rule: half-open bins, the last closed) | `create edges`, `uniform lower upper bins`, `add`, `remove`, `merge`, `unmerge` | `edges`, `counts`, `underflow`, `overflow`, `missing` (NaN), `total` |
| `QuantileEstimate` (the P² algorithm, Jain and Chlamtac 1985; add only) | `create p`, `add` | `count`, `value` (exact type-7 below five observations) |
| `DecayedMoments` (exponential decay by half-life) | `create halfLife`, `addAt s t x`, `addItem s x` | `weight`, `mean`, `variance` |
| `Window<'T, 'A>` (the last `capacity` items) | `create capacity add remove zero`, `push` | `value`, `count`, `items` (oldest first) |
| `TimeWindow<'T, 'A>` (items no older than `span`) | `create span add remove zero`, `push w t item` | `value`, `count`, `items` |

`Window.moments compensated capacity` and `TimeWindow.moments compensated span` give the moments case. A window
re-folds its accumulator from its items once its removals since the last re-fold reach its item count (amortised O(1)
per push), which bounds drift as `FloatPolicy.standard` does. `DecayedMoments.addAt` with a time earlier than the last
applies no decay; the doc comment says so.

### 6.9 Maintained aggregates: `Aggregate` additions

```fsharp
Aggregate.maintainedBy :
    FloatPolicy -> finite: ('A -> bool) -> add: ('A -> 'U -> 'A) -> remove: ('A -> 'U -> 'A) -> zero: 'A
    -> result: ('A -> 'T) -> projection: ('V -> 'U) -> Projection<'K, 'V> -> Maintained<'T>
Aggregate.comomentsBy : FloatPolicy -> ('V -> Comoments) -> Projection<'K, 'V> -> Maintained<Comoments>
Aggregate.higherMomentsBy : FloatPolicy -> ('V -> HigherMoments) -> Projection<'K, 'V> -> Maintained<HigherMoments>
Aggregate.weightedMomentsBy : FloatPolicy -> ('V -> WeightedMoments) -> Projection<'K, 'V> -> Maintained<WeightedMoments>
Aggregate.histogramBy : edges: float[] -> ('V -> float) -> Projection<'K, 'V> -> Memo<Histogram>
Aggregate.quantileBy : p: float -> ('V -> float) -> Projection<'K, 'V> -> Memo<float>
```

`maintainedBy` is today's private `maintain` made public. `histogramBy` counts exactly, so it needs no re-fold.
`quantileBy` is the exact type-7 quantile over the core's sorted view; its doc comment states the per-change cost the
builder measures or reads from core 0.2.1's `sortBy` (the incremental sort view, core PR #361). Core 0.2.1 rejects a
projection made by `createProjectionOnce` in every `Projection` combinator, `foldGroupBy` included, so every aggregate
here accepts the same projections (*from documentation*: core v0.2.1 `Api.fs`, `createProjectionOnce`, as the T2
plan read it; this corrects an earlier reading that singled out `sortBy`).

### 6.10 Random variates: `Generator` additions

```fsharp
Generator.integer : lower: int -> upperExclusive: int -> Generator -> struct (int * Generator)   // unbiased
Generator.exponential : rate: float -> Generator -> struct (float * Generator)
Generator.gamma : shape: float -> scale: float -> Generator -> struct (float * Generator)   // Marsaglia–Tsang
Generator.beta : a: float -> b: float -> Generator -> struct (float * Generator)
Generator.poisson : mean: float -> Generator -> struct (int * Generator)
Generator.binomial : trials: int -> p: float -> Generator -> struct (int * Generator)
Generator.shuffle : 'T[] -> Generator -> struct ('T[] * Generator)                         // returns a copy
Generator.split : Generator -> struct (Generator * Generator)
```

Each variate is exact in distribution (no normal approximation); the methods and their expected costs are in the doc
comments. `split` seeds two generators from outputs of the given one; the doc comment says that no independence proof
backs it.

### 6.11 First-order uncertainty: `Uncertain`

```fsharp
type Uncertain                              // a value with its contributions by source; structural equality
Uncertain.exact : float -> Uncertain
Uncertain.source : standardUncertainty: float -> value: float -> Uncertain    // a new, independent source
Uncertain.value, Uncertain.standardUncertainty : Uncertain -> float
Uncertain.covariance, Uncertain.correlation : Uncertain -> Uncertain -> float
Uncertain.contributions : Uncertain -> struct (int * float)[]                 // source id, signed contribution
Uncertain.map : f: (float -> float) -> df: (float -> float) -> Uncertain -> Uncertain
Uncertain.map2 : f: (float -> float -> float) -> dfx: (float -> float -> float) -> dfy: (float -> float -> float)
                 -> Uncertain -> Uncertain -> Uncertain
```

with operators `+ − * /` (between two `Uncertain`s and with a `float` on either side), unary minus, and the static
members that make F#'s `sqrt`, `exp`, `log`, `log10`, `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `sinh`, `cosh`,
`tanh`, `abs` and `**` apply. Under Fable, F#'s `abs` on an `Uncertain` compiles to `Math.abs` and returns NaN (Fable's
`Replacements.fs`, case "Abs", found in T1 Task 5), so the module adds `Uncertain.abs : Uncertain -> Uncertain`, which
code meant for both targets calls. An `Uncertain` holds its value and, per independent source, the contribution
∂f/∂xᵢ · uᵢ; the combined standard uncertainty is the root of the sum of their squares, and two values that share a
source are correlated (the GUM's law of propagation for uncorrelated inputs, extended through shared sources; *general
knowledge*). Source identifiers come from a counter, incremented with `Interlocked` on .NET. A NaN standard uncertainty
propagates as NaN.

### 6.12 Moments additions

I9 (section 4).

### 6.13 Order statistics: `OrderStatistic`

```fsharp
OrderStatistic.quantileSorted : p: float -> sorted: float[] -> float   // type 7: h = (n − 1)p, linear between order statistics
```

NaN for an empty array; raises `ArgumentOutOfRangeException` unless 0 ≤ p ≤ 1; the array is assumed sorted ascending and
is not checked. It is the one type-7 implementation: Monte Carlo propagation (§8.6), `QuantileEstimate`, `Aggregate`'s
`quantileBy` and `Describe.quantileSorted` use it.

## 7. Ranvier.Statistics additions

### 7.1 Distributions: `Continuous`, `Discrete`

Each is a union with private cases and validating constructors, so a distribution has structural equality (section
3.3 says what that does and does not give a memo).

```fsharp
Continuous.normal mean sd · logNormal mu sigma · uniform lower upper · exponential rate · gamma shape scale
· chiSquared dof · beta a b · studentT dof · fisherF d1 d2 · cauchy location scale · weibull shape scale
· logistic location scale

Continuous.density, Continuous.logDensity, Continuous.cdf, Continuous.survival : Continuous -> float -> float
Continuous.quantile : Continuous -> p: float -> float      // p in [0, 1]; 0 and 1 give the support's bounds
Continuous.mean, Continuous.variance, Continuous.standardDeviation : Continuous -> float   // NaN where undefined
Continuous.support : Continuous -> struct (float * float)
Continuous.sample : Continuous -> Generator -> struct (float * Generator)
Continuous.samples : Continuous -> count: int -> Generator -> struct (float[] * Generator)
Continuous.describe : Continuous -> string                  // "Normal(mean = 0, sd = 1)"

Discrete.binomial trials p · poisson mean · geometric p · negativeBinomial r p · hypergeometric population
successes draws · bernoulli p · uniform lower upper

Discrete.mass, Discrete.logMass : Discrete -> int -> float
Discrete.cdf, Discrete.survival : Discrete -> int -> float  // P(X ≤ k), P(X > k)
Discrete.quantile : Discrete -> p: float -> int             // the least k with cdf k ≥ p
Discrete.mean, Discrete.variance, Discrete.support, Discrete.sample, Discrete.samples, Discrete.describe
```

`geometric` counts failures before the first success. The normal quantile is full precision (Acklam's approximation
refined with one Halley step on `erfc`, or an equivalent); `NormalQuantile.ofProbability` stays as the faster
approximation, and its doc comment points to `Continuous.quantile`.

### 7.2 Descriptive statistics: `Describe`

```fsharp
Describe.quantile : p: float -> float[] -> float         // type 7 (R's default, NumPy's "linear"); sorts a copy
Describe.quantileSorted : p: float -> float[] -> float
Describe.quantiles : ps: float[] -> float[] -> float[]
Describe.median, Describe.interquartileRange, Describe.medianAbsoluteDeviation : float[] -> float
Describe.trimmedMean : proportion: float -> float[] -> float
Describe.ranks : float[] -> float[]                      // average ranks for ties; raise on NaN
Describe.autocorrelation : maxLag: int -> float[] -> float[]
type Summary = { Count: int; Mean: float; StandardDeviation: float; Minimum: float; LowerQuartile: float;
                 Median: float; UpperQuartile: float; Maximum: float }
Describe.summary : float[] -> Summary                    // StandardDeviation with divisor n − 1
type BinRule = Sturges | Scott | FreedmanDiaconis | Count of int
Describe.binEdges : BinRule -> float[] -> float[]
```

An empty array gives NaN; an array holding NaN gives NaN from the order statistics (`ranks` raises).
`medianAbsoluteDeviation` is unscaled; its doc comment gives 1.4826 for consistency with the normal.

### 7.3 Intervals

```fsharp
MeanInterval.student : alpha: float -> Moments -> Interval          // t with n − 1, sample standard deviation
MeanInterval.knownSigma : alpha: float -> sigma: float -> Moments -> Interval
ProportionInterval.wilson, ProportionInterval.clopperPearson, ProportionInterval.agrestiCoull,
ProportionInterval.jeffreys : alpha: float -> successes: int -> trials: int -> Interval
VarianceInterval.chiSquared : alpha: float -> Moments -> Interval
DifferenceInterval.welch, DifferenceInterval.pooled : alpha: float -> Moments -> Moments -> Interval   // mean a − mean b
```

`FixedSample.ofMoments` stays as it is.

### 7.4 Hypothesis tests: `HypothesisTest`

The module is `HypothesisTest`, not `Test`, which would collide with Expecto's `Test` type in any test file that opens
Expecto (the T3 plan's finding; the author's ruling).

```fsharp
type Alternative = TwoSided | Less | Greater      // "Less": the parameter is below the null value
type TestResult = { Statistic: float; DegreesOfFreedom: float; DenominatorDegreesOfFreedom: float; PValue: float }
                                                  // NaN where a field does not apply
HypothesisTest.oneSampleT : Alternative -> mu0: float -> Moments -> TestResult
HypothesisTest.oneSampleZ : Alternative -> mu0: float -> sigma: float -> Moments -> TestResult
HypothesisTest.welchT, HypothesisTest.pooledT : Alternative -> Moments -> Moments -> TestResult
HypothesisTest.varianceRatio : Alternative -> Moments -> Moments -> TestResult           // F
HypothesisTest.oneWayAnova : Moments[] -> TestResult
HypothesisTest.binomial : Alternative -> successes: int -> trials: int -> p0: float -> TestResult   // exact
HypothesisTest.chiSquaredGoodnessOfFit : observed: float[] -> expected: float[] -> TestResult
HypothesisTest.chiSquaredIndependence : table: float[][] -> TestResult                   // no continuity correction
HypothesisTest.fisherExact : Alternative -> a: int -> b: int -> c: int -> d: int -> TestResult   // 2 × 2
HypothesisTest.mannWhitney : Alternative -> float[] -> float[] -> TestResult              // normal approximation
HypothesisTest.wilcoxonSignedRank : Alternative -> differences: float[] -> TestResult     // normal approximation
HypothesisTest.kolmogorovSmirnov : cdf: (float -> float) -> float[] -> TestResult         // asymptotic
HypothesisTest.kolmogorovSmirnovTwoSample : float[] -> float[] -> TestResult              // asymptotic
```

The two-sided exact tests (binomial, Fisher) sum the probabilities of outcomes no more likely than the observed one, with
a relative slack of 1e-7, as R's `binom.test` and `fisher.test` do (*general knowledge* ⚠️: the builder confirms against
R's documentation and states the rule in the doc comment). The normal approximations state their tie and continuity
corrections; the asymptotic Kolmogorov–Smirnov p-values state their formula and that they are approximate below the
sample sizes the doc comment names.

### 7.5 Sequential inference and multiple testing

I4 (section 4) changes the confidence sequence. Beside it:

```fsharp
type SequentialTest                         // structural equality
SequentialTest.create : SequenceSettings -> theta0: float -> SequentialTest
SequentialTest.add : compensated: bool -> SequentialTest -> float -> SequentialTest
SequentialTest.pValue : SequentialTest -> float      // 1 before the burn-in; non-increasing after
SequentialTest.rejects : SequentialTest -> bool      // pValue ≤ Alpha
SequentialTest.moments : SequentialTest -> Moments

Adjust.bonferroni, Adjust.holm, Adjust.hochberg, Adjust.benjaminiHochberg, Adjust.benjaminiYekutieli :
    float[] -> float[]                      // adjusted p-values in input order, capped at 1
```

The sequential test's p-value is the running minimum, from the burn-in on, of 1/Λₜ, with Λₜ the normal-mixture
likelihood ratio at the plug-in standard deviation. Its rejection boundary is the confidence sequence's radius (the two
are dual; *general knowledge*: Johari, Pekelis and Walsh, "Always valid inference", and the sequence's own source). A test
pins the duality: `rejects` is true exactly when some interval from the burn-in on excluded `theta0`. Paired two-sample
use folds the differences; the doc comment says so.

### 7.6 Regression, correlation and curve fitting

```fsharp
type SimpleRegression = { Slope: float; Intercept: float; SlopeStandardError: float; InterceptStandardError: float;
                          RSquared: float; ResidualStandardError: float; DegreesOfFreedom: float; Count: float }
Regression.simple : Comoments -> SimpleRegression
type LinearRegression = { Coefficients: float[]; StandardErrors: float[]; TStatistics: float[]; PValues: float[];
                          RSquared: float; AdjustedRSquared: float; ResidualStandardError: float;
                          DegreesOfFreedom: float; Covariance: Matrix; Residuals: float[] }
Regression.linear : design: Matrix -> y: float[] -> Result<LinearRegression, string>
Regression.weighted : design: Matrix -> y: float[] -> weights: float[] -> Result<LinearRegression, string>
Regression.polynomial : degree: int -> xs: float[] -> ys: float[] -> Result<LinearRegression, string>  // ascending powers
type CurveFit = { Parameters: float[]; StandardErrors: float[]; Covariance: Matrix; ResidualStandardError: float;
                  DegreesOfFreedom: float; Iterations: int; Converged: bool }
Regression.curve : model: (float[] -> float -> float) -> initial: float[] -> xs: float[] -> ys: float[] -> CurveFit

Correlation.pearson, Correlation.spearman, Correlation.kendall : float[] -> float[] -> float   // kendall: tau-b
Correlation.pearsonTest, Correlation.spearmanTest, Correlation.kendallTest :
    Alternative -> float[] -> float[] -> TestResult
```

`Regression.simple` over `Aggregate.comomentsBy` is a regression maintained per row change; the guide shows it.
`Regression.linear` uses QR (section 6.6); `RSquared` is computed about the mean when the design has a constant column
and about zero otherwise, and the doc comment says how a constant column is detected.

### 7.7 Resampling and density

```fsharp
Bootstrap.percentile : alpha: float -> resamples: int -> statistic: (float[] -> float) -> float[] -> Generator
                       -> struct (Interval * Generator)
Bootstrap.standardError : resamples: int -> statistic: (float[] -> float) -> float[] -> Generator
                          -> struct (float * Generator)
Density.silvermanBandwidth, Density.scottBandwidth : float[] -> float
Density.gaussianKernel : bandwidth: float -> float[] -> (float -> float)
```

## 8. Ranvier.Quantities additions

### 8.1 Unit catalogue: `Units`, `Prefix`, `UnitCatalogue`

`Units` gains a `UnitOf` value, with a measure type, for each of these (symbol, scale in SI base units). Measure type
names follow FSharp.Core's `UnitSymbols` where it has one (I8); otherwise the symbol when it is an F# identifier, else an
ASCII spelling (`ohm`, `um`, `deg`), the builder's choice within that rule. A symbol whose measure type would hide an
FSharp.Core function is spelled out: `[<Measure>] type min` hides `min` (FS1133, *measured here* by the T1 planner
with `dotnet fsi`), so the minute's measure is `minute`; the unit's symbol stays "min".

| Group | Units |
|---|---|
| SI base | m, kg, s, A, K, mol, cd |
| SI derived with special names | rad, sr (both dimensionless), Hz, N, Pa, J, W, C, V, F, Ω, S, Wb, T, H, lm, lx, Bq, Gy, Sv, kat |
| Prefixed, in common use | km, cm, mm, µm, nm, g, mg, ms, µs, ns, mK (existing), kPa, MPa, kJ, kW, MW, mA, mV, kV |
| Outside the SI, accepted for use with it | min (60 s), h (3600 s), d (86400 s), au (149 597 870 700 m), L (10⁻³ m³), t (1000 kg), eV, Da, ° (π/180) (also `deg`) |
| Other, common in science | a (the Julian year, 31 557 600 s), ly, pc, bar (10⁵ Pa), atm (101 325 Pa) |

The values for au, eV, Da, ly and pc are *general knowledge* ⚠️ until the builder confirms them against the SI Brochure
(9th edition), CODATA 2022 and the IAU resolutions (2012 B2 for au, 2015 B2 for pc) and cites each in its doc comment.

```fsharp
type Prefix = { Symbol: string; Factor: float }
Prefix.quecto … Prefix.quetta                                   // the 24 SI prefixes, "da" included
UnitInfo.prefixed : Prefix -> UnitInfo -> UnitInfo              // factor names gain the prefix: "k" + "m" = "km"
UnitOf.prefixed : Prefix -> UnitOf<'u> -> UnitOf<'v>            // the caller's annotation fixes 'v, as in define

type UnitCatalogue                                              // immutable
UnitCatalogue.si : UnitCatalogue                                // every unit of the table
UnitCatalogue.add : symbol: string -> UnitInfo -> prefixable: bool -> UnitCatalogue -> UnitCatalogue
UnitCatalogue.tryFind : symbol: string -> UnitCatalogue -> UnitInfo voption
```

`tryFind` takes an exact symbol first, then a prefix followed by a prefixable symbol ("da" tried before "d"). Prefixes
attach to "g", not to "kg". `UnitCatalogue.si` marks the SI base and derived units, L, t, eV and Da prefixable, and the
rest not.

### 8.2 Parsing and conversion

```fsharp
UnitInfo.parse : UnitCatalogue -> text: string -> Result<UnitInfo, string>
UnitInfo.conversionFactor : from: UnitInfo -> target: UnitInfo -> Result<float, string>   // Error across dimensions
UnitInfo.convert : from: UnitInfo -> target: UnitInfo -> float -> Result<float, string>
UnitOf.convert : from: UnitOf<'u> -> target: UnitOf<'v> -> float<'u> -> float<'v>        // raise across dimensions
```

The grammar: factors separated by `·`, `*`, `.` or a space, `/` for division (left-associative, so `J/kg/K` is
J·kg⁻¹·K⁻¹), parentheses, `1` as a numerator, and an integer exponent written `^n`, `^-n` or in Unicode superscripts
(`m²`, `s⁻¹`). The parsed unit's factors carry the symbols as written, so `UnitInfo.symbol` of the parse of "km/h" is
"km/h". `UnitCatalogue.add` stores a unit as one factor, its symbol, whatever factors the `UnitInfo` given has, as the
built-in named units are stored: the parse of "kWh" added as `(kilowatt * hour).Info` has the symbol "kWh", and a
custom unit is atomic, so its square root needs an even exponent of the symbol itself (changed in the T1 final review).
An error message names the character position, as `Formula.parse` does.

### 8.3 The model's catalogue and the formula notation

`Model.create name` uses `UnitCatalogue.si`; `Model.createWith catalogue name` takes another; `model.Catalogue` reads
it, and `Model.child` inherits its parent's. The notation (`Formula.parse`, `check`, `evaluator`) gains:

- **Unit literals.** `[unit text]` is one of that unit, parsed against the model's catalogue when checked; a number
  followed by a literal (`9.81 [m/s^2]`) is their product.
- **Conversion.** `convert(expr, [unit])` gives the value of `expr` in that unit; an `Error` across dimensions. `+` and
  `−` still need one unit and do not convert (the slice's D3).
- **Functions.** `sin cos tan asin acos atan sinh cosh tanh` (dimensionless argument and result), `atan2(y, x)` (one
  unit for both; dimensionless result) and `hypot(x, y)` (one unit; that unit).
- **π.** The name `pi` reads π when the model declares no entry `pi`.
- **Dimensionless arguments are taken at scale 1.** A function that needs a dimensionless argument (`exp`, `ln`,
  `log10`, the trigonometric and hyperbolic functions) multiplies its argument's value by its unit's scale first, so
  `sin(30 [deg])` is sin(π/6) and `exp(x)` with x in km/m reads x as a pure number. 0.1.0 rejected a dimensionless unit
  of scale other than 1 (`Formula.fs`, `pureNumber`); this replaces that rule. The inverse functions return radians
  (scale 1). The evaluator takes the scales from the same unit computation `check` does.

`Expr` gains the cases `UnitLiteral of string` and `Convert of Expr * string`.

### 8.4 Entries with uncertainty, and formula entries

`Entry` gains three fields:

```fsharp
Uncertainty: unit -> float      // tracked; the standard uncertainty in Unit; 0 for an exact value, NaN when unknown
Measured: unit -> Uncertain     // tracked; the value with its contributions by source
Formula: string                 // the formula text of an entry declared by Quantity.formula; "" otherwise
```

Each input and constant entry owns one `Uncertain` source, allocated at declaration, so every formula that reads it
shares the source and correlations follow. While its standard uncertainty is 0, its `Measured` is `Uncertain.exact` of
the value and holds no contribution, so a function with an infinite derivative there (`sqrt` at 0, `asin` at 1) gives
uncertainty 0, not 0 · ∞ = NaN; the source's id stays allocated, and a later `SetUncertainty` brings the contribution
back under it (changed in the T1 final review).

```fsharp
Quantity.input model id name unit initial                           // as today; uncertainty 0
Quantity.inputWithUncertainty model id name unit initial (uncertainty: float<'u>) : Input<'u>
Input<'u>.SetUncertainty : float<'u> -> unit
Quantity.measuredConstant model id name unit value (uncertainty: float<'u>) source : Quantity<'u>
Quantity.deriveMeasured model id name unit (compute: unit -> Uncertain) : Quantity<'u>
Quantity.formula : Model -> id: string -> name: string -> text: string -> Result<Entry, string>
Quantity.view : UnitOf<'u> -> Entry -> Result<Quantity<'u>, string>      // Error unless sameUnit (I1)
Formula.measuredEvaluator : Model -> Expr -> (unit -> Uncertain)
```

`Quantity.derive` and `deriveWithin` entries have NaN uncertainty: their compute function is opaque, and the doc comment
points to `deriveMeasured`. A `formula` entry creates two memos, one for the value and one for the measured value; the
second runs only when read. Its unit is the unit `Formula.check` gives.

### 8.5 Entry lifetime

Each declaration (input, constant, derived, formula, block) registers a cleanup with the current owner that removes the
entry, or the block's reserved identifier, from its model. After the owner is cleaned up the identifier may be declared
again, and `TryFind` no longer finds the entry. A read through a removed entry's functions behaves as the core's
disposed node does: it answers with the last outcome its memo computed. A memo that never ran has none (the core answers
with the type's default, a plausible wrong number), so a read through it after the removal raises
`InvalidOperationException` ("the entry … was removed before it was computed"), and `Status` reports `Failed` with that
exception; a block's `Value` follows the same rule (changed in the T1 final review). What "the current owner" is, from core v0.2.1 (`Api.fs` `onCleanup`; `Core.fs` `currentOwner`,
`AddCleanup`, `Graph.Current`, as the T1 plan read them; the author's ruling on the plan's finding):

- Under a graph and outside any root, effect or owning scope, the owner is the graph's root scope, so the entry leaves
  its model when the graph is disposed.
- With no active graph a declaration registers nothing and is permanent, as in 0.1.0 (only `constant` and
  `measuredConstant` can be declared then, since the others create nodes).
- Inside a pure body (a `createMemo` computation, a projection row's reader) `onCleanup` raises, and so does every
  declaration there, leaving the model unchanged. In 0.1.0 a constant declared there was permanent, and the memo's next
  run raised "already declares"; failing at once is the honest form of that.

Names resolve once, when an entry or evaluator is created (as in 0.1.0), and do not follow a later declaration of the
same identifier. So a formula entry, a `measuredEvaluator` or a `monteCarlo` reader that outlives the owner of an entry
it names keeps reading that entry's last value, or raises as above when the entry was removed before it was computed; a
`monteCarlo` reader evaluates a removed formula entry it inlines from its leaves (changed in the T1 final review). The
doc comments of `Model`, `Quantity.formula` and the evaluators say
so, and a test pins it. Declaring a page's entries under the page's owner, as the Monitor does, re-creates them all
together on a re-mount; following re-declarations would make `TryFind` a tracked read and is a candidate for a later
round. Declarations inside an effect are removed before each re-run of the effect, because the core runs an effect's
cleanups then (core v0.2.1 `Api.fs`, `onCleanup`).

### 8.6 Physical constants and Monte Carlo propagation

```fsharp
type PhysicalConstants = { SpeedOfLight: Quantity<m/s>; Planck: Quantity<J s>; ReducedPlanck: …; ElementaryCharge;
                           Boltzmann; Avogadro; Gravitation; VacuumPermittivity; VacuumPermeability; ElectronMass;
                           ProtonMass; NeutronMass; AtomicMassConstant; FineStructure; MolarGas; StefanBoltzmann;
                           BohrRadius; StandardGravity; AstronomicalUnit }
Constants.declare : Model -> PhysicalConstants
```

Identifiers in the model: `c h hbar e k_B N_A G epsilon_0 mu_0 m_e m_p m_n m_u alpha R sigma a_0 g_n au`. Values and
standard uncertainties are CODATA 2022, `g_n` the CGPM's standard gravity, `au` the IAU 2012 value; each entry's
`Source` names its origin. The builder takes every value from NIST's CODATA 2022 table
(`https://physics.nist.gov/cuu/Constants/`) and records the page in the test file; this document gives no values.

Correlations (the author's answer to the builder check's second question):

- The defining constants (c, h, e, k_B, N_A) are exact, and so are the constants computed from them alone (ħ, R, σ),
  declared as exact constants with the computed value.
- α is a measured constant with its own source. μ0 and ε0 are `deriveMeasured` entries computed from α and the exact
  constants (μ0 = 2αh/(e²c), ε0 = 1/(μ0c²)), so they share α's source, and ε0·μ0 = 1/c² has zero propagated
  uncertainty. A test checks both computed values against the NIST table within the table's printed digits.
- G, m_e, m_p, m_n, m_u and a_0 are measured constants with independent sources. CODATA publishes correlations among
  several of them (*general knowledge* ⚠️); 0.2.0 does not keep those, and `Constants.declare`'s doc comment says so.

```fsharp
type MonteCarloResult = { Mean: float; StandardDeviation: float; Lower: float; Upper: float; Draws: int }
Formula.monteCarlo : draws: int -> seed: int -> coverage: float -> Model -> Expr -> (unit -> MonteCarloResult)
```

`monteCarlo` is propagation of distributions (the GUM's Supplement 1, *general knowledge*). It inlines the formula
entries the expression names, down to leaves that are not formula entries. Each draw takes one standard normal deviate
zᵢ per `Uncertain` source the leaves' measured values reach, and sets each leaf to its value plus Σᵢ cᵢzᵢ over its
contributions cᵢ. So an input or constant leaf is normal at its value and standard uncertainty, and leaves that share a
source (μ0 and ε0 through α, or two `deriveMeasured` leaves of one input) move together, linearly in each source. The
expression is evaluated `draws` times inside the reading node. `Lower` and `Upper` bound the probabilistically symmetric
coverage interval. A leaf with a NaN contribution gives NaN fields. This is how 0.2.0 evaluates one expression over
many input vectors: one node, no graph per vector.

### 8.7 A run with an integrator

```fsharp
type OdeState = { mutable Time: float; mutable State: float[] }      // or a class with the same members
Run.ode : OdeOptions -> clock: (unit -> float) -> start: (unit -> float[]) -> rhs: (float -> float[] -> float[])
          -> Run<OdeState>
```

A start sets `Time` to 0 and `State` to a copy of `start ()`; an advance over a clock gap integrates from `Time` to
`Time + gap` with `Ode.solve` and raises its `Error` as a failed advance (the `Run` failure semantics of 0.1.0).

## 9. Docs

- `docs/GUIDE.md`: at least ten tasks, each a short section with one snippet: fit a curve to data; integrate an ODE in a
  `Run`; propagate uncertainty through a formula, linearly and by Monte Carlo; parse and convert units; declare the
  physical constants and use them in a formula; test two samples; adjust a family of p-values; monitor a stream with a
  sequential test; keep a regression current over a projection; take the spectrum of a signal; solve a linear system and
  a least-squares problem; window a stream. Each snippet is the body of a test in `GuideTests.fs`; the guide names the
  test.
- The README describes the 0.2.0 packages by area and links the guide. The README packed into 0.1.0 (from a commit
  before the AstroTracked removal) links into `examples/AstroTracked`, which no longer exists; the README on `main`
  has no such link since `8472a08`, so the 0.2.0 package README, packed from it, fixes those links for 0.2.0 users. The
  0.1.0 package cannot be changed.
- `docs/measurements/accuracy.md` lists each approximating kernel with the bound its tests pin and the reference's
  source.
- Package descriptions, tags and `PackageReleaseNotes` describe 0.2.0.

## 10. Order of work

Three plans, each through subagent-driven development, on the branch `toolkit` of `Ranvier.Science`:

1. **Plan T1, interfaces and quantities** (sections 4, 6.11, 6.12, 8 except 8.7). The interface changes come first, as
   `docs/FINDINGS.md` §9 recommends.
2. **Plan T2, numerics kernels** (sections 6.1 to 6.10, and 8.7, which needs `Ode`).
3. **Plan T3, statistics, docs and release** (sections 7 and 9; the version change; the final review; the pull request,
   CI, merge and publication).

T3 depends on T2 (special functions, linear algebra, comoments, least squares). T1 and T2 are independent except that
T2's `Run.ode` edits `Run.fs`, which T1 does not touch.

## 11. Risks

| Risk | Answer |
|---|---|
| Reference values without a high-precision library | Python's `decimal` and `fractions` give 50-digit references for series and continued fractions; identities cover much of the rest (section 5.2) |
| Transcendental functions differ between targets | Bounds hold per target; exact cross-target equality is claimed only for `+ − × ÷ sqrt` kernels (section 5.3) |
| The Fable suite's compile time grows with the suite | Measured at each plan's end and recorded; a task does not split the suite to save time |
| The breadth invites shallow implementations | Each kernel's contract, bound and failure report are review items; a task is not complete with a bound untested |
| Publication is blocked on the owner | The packages are complete, tested and packed in CI; the publish run is re-run once the policy is re-saved |

## 12. Left to the builder

Algorithms where section 6 or 7 does not name one; private representations; file layout within each project (one file
per section-level area is the default); test organisation; the reference scripts; exact iteration limits and thresholds
(stated in doc comments); the measure type names within the rule of 8.1; the guide's prose.

**Reading the core.** The core checkout `C:\wk\ranvier-release\Ranvier` is at `25a4452` (core 0.2.0), behind
`origin/master`. Read core 0.2.1, which the packages will depend on, at its tag (`git -C ../Ranvier show
v0.2.1:src/Ranvier/<file>`) or in the restored package's sources (`~/.nuget/packages/ranvier/0.2.1/fable/`), not in that
working tree.

## 13. Review record

A reviewer read this document on 2026-11-20 as the person who would plan the build, with read access to both
repositories. It raised six questions, each answered above as the author's call:

1. What "cuts off" means under the default equality: section 3.3 (structural equality kept, no cut-off claimed from it).
2. Whether the constants keep CODATA's correlations: section 8.6 (μ0 and ε0 through α; the others independent, said
   so).
3. Whether readers follow a re-declared identifier: section 8.5 (no; resolved once, pinned by a test).
4. The burn-in signatures: section 4, after the table.
5. Angles in the notation: section 8.3 (dimensionless arguments taken at scale 1).
6. Where the `erfc` bound stops: section 5.1 (values at least 1e-300).

It found five statements that did not match the source, each checked by the author and corrected: the cut-off claim
(core v0.2.1 `Types.fs`, confirmed by reading it), the home of §3.7's remaining items (FINDINGS §2 has none; section 4
now dispositions each), the citation for the publishing block (goal 6), the README links (section 9), and which core
version the checkout holds (section 12).
