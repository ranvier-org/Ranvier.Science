namespace Ranvier.Statistics

open System
open Ranvier.Numerics

/// <summary>A least-squares line y = Intercept + Slope · x, with its standard errors, from <c>Regression.simple</c>.</summary>
/// <remarks>
/// A field that is undefined for the data is NaN: every field but <c>DegreesOfFreedom</c> and <c>Count</c> where x is
/// constant, and the standard errors and <c>ResidualStandardError</c> where the degrees of freedom are 0.
/// </remarks>
type SimpleRegression =
    {
        /// The least-squares slope, Σ(x − x̄)(y − ȳ) / Σ(x − x̄)².
        Slope: float
        /// ȳ − Slope · x̄.
        Intercept: float
        /// The standard error of the slope.
        SlopeStandardError: float
        /// The standard error of the intercept.
        InterceptStandardError: float
        /// The squared correlation of x and y; NaN where y is constant.
        RSquared: float
        /// The square root of the residual sum of squares over the degrees of freedom.
        ResidualStandardError: float
        /// n − 2, or 0 below three pairs.
        DegreesOfFreedom: float
        /// The number of pairs.
        Count: float
    }

/// <summary>A multiple linear regression from <c>Regression.linear</c>, <c>weighted</c> or <c>polynomial</c>.</summary>
/// <remarks>
/// With as many observations as coefficients the degrees of freedom are 0 and every field that needs the residual
/// variance (the standard errors, t statistics, p-values, <c>AdjustedRSquared</c>, <c>ResidualStandardError</c> and
/// the covariance) is NaN. <c>RSquared</c> and <c>AdjustedRSquared</c> are NaN where the total sum of squares is 0,
/// which is exactly the case of a constant y in a model with a constant column. Where the residual variance is below
/// (mean(f)² + var(f)) · 1e-30, with f the unweighted fitted values of every row (an essentially perfect fit), the t
/// statistics and p-values are NaN and the coefficients and standard errors are kept; that is the test at which R's
/// <c>summary.lm</c> warns "essentially perfect fit: summary may be unreliable" (from documentation: stats/R/lm.R of
/// the R sources at trunk, lines 345 to 348, read there), and this library has no warnings. The residual variance of
/// that test is the unweighted Σ e²/(n − p) over the rows of positive weight, which is R's for weights of 1; R weighs
/// the residuals by the weights as given, so its test moves with the scale of the weights, and this one does not
/// (test "the essentially perfect test reads the unweighted fitted values"). A NaN or infinite entry in the data gives
/// every field NaN, the covariance included, and raises nothing.
/// </remarks>
type LinearRegression =
    {
        /// The least-squares coefficients, one per design column.
        Coefficients: float[]
        /// The standard error of each coefficient, the square root of the diagonal of <c>Covariance</c>.
        StandardErrors: float[]
        /// Each coefficient over its standard error: ±∞ for a non-zero coefficient with a zero standard error, NaN for 0 over 0 and at an essentially perfect fit.
        TStatistics: float[]
        /// The two-sided p-value of each t statistic, from the Student t with <c>DegreesOfFreedom</c> degrees of freedom.
        PValues: float[]
        /// 1 − RSS/TSS, with TSS about the (weighted) mean of y where the design has a constant column and about 0 otherwise.
        RSquared: float
        /// 1 − (1 − R²)(n − c)/(n − p), with c = 1 for a constant column and 0 otherwise.
        AdjustedRSquared: float
        /// The square root of the residual variance, Σ w e² / (n − p).
        ResidualStandardError: float
        /// n − p, with n the number of rows (of positive weight, for <c>weighted</c>) and p the number of columns.
        DegreesOfFreedom: float
        /// The covariance of the coefficients, σ² (XᵀWX)⁻¹.
        Covariance: Matrix
        /// y − X·β for every row, unweighted (a zero-weight row included).
        Residuals: float[]
    }

/// <summary>A nonlinear least-squares fit from <c>Regression.curve</c>.</summary>
/// <remarks>
/// A field that is undefined for the data is NaN: the standard errors, covariance and <c>ResidualStandardError</c>
/// where the degrees of freedom are 0, and the standard errors and covariance where the parameters are not
/// identified (min|Rₖₖ|/max|Rₖₖ| of the column-equilibrated Jacobian at most 1e-7, as R's <c>qr</c> tolerance), and
/// every field but
/// <c>DegreesOfFreedom</c> where the data hold a NaN or infinity. <c>Converged</c> is <c>LeastSquares</c>' own flag
/// and is false where the cost left the range of the doubles.
/// </remarks>
type CurveFit =
    {
        /// The fitted parameters, the best accepted by <c>LeastSquares.levenbergMarquardt</c>.
        Parameters: float[]
        /// The square root of each diagonal entry of <c>Covariance</c>.
        StandardErrors: float[]
        /// σ² (JᵀJ)⁻¹ at the fitted parameters, J the forward-difference Jacobian of the model.
        Covariance: Matrix
        /// The square root of Σ rᵢ² / (n − p) over the residuals at the fit, which is √(2 · cost / (n − p)), with n the
        /// number of points and p the number of parameters.
        ResidualStandardError: float
        /// n − p.
        DegreesOfFreedom: float
        /// The iterations <c>LeastSquares.levenbergMarquardt</c> started.
        Iterations: int
        /// Whether <c>LeastSquares.levenbergMarquardt</c> met a convergence test, and the cost stayed in the doubles.
        Converged: bool
    }

module internal RegressionSupport =
    let epsilon = 2.220446049250313e-16

    /// The ratio of the smallest to the largest |Rₖₖ| of a column-equilibrated Jacobian at or below which parameters
    /// count as not identified: 1e-7, the default <c>tol</c> of R's <c>qr</c> (from documentation: base/R/qr.R at trunk, line 24).
    let identifiability = 1e-7

    // 2^e for 0 <= e <= 1000, by squaring: every intermediate is an exact power of two below 2^1024.
    let private twoPower (e: int) : float =
        let mutable result = 1.0
        let mutable square = 2.0
        let mutable left = e

        while left > 0 do
            if left % 2 = 1 then
                result <- result * square

            left <- left / 2

            if left > 0 then
                square <- square * square

        result

    /// x · 2^e, exact unless the result overflows or is subnormal. The factor is applied in steps of at most 2^1000, so
    /// no intermediate overflows or underflows unless the result does.
    let rec scaleByPowerOfTwo (x: float) (e: int) : float =
        if e > 1000 then scaleByPowerOfTwo (x * twoPower 1000) (e - 1000)
        elif e < -1000 then scaleByPowerOfTwo (x / twoPower 1000) (e + 1000)
        elif e >= 0 then x * twoPower e
        else x / twoPower (-e)

    /// The exponent e, clamped to [-1000, 1000], of the exact power of two 2^e nearest the 2-norm of the values, in
    /// the sense of rounding log2 of the norm; 0 for all-zero values. The norm is formed as the largest magnitude
    /// times the root of the sum of squared ratios, so it does not overflow or underflow where it is itself a double.
    let exponentNear (values: float[]) : int =
        let mutable largest = 0.0

        for v in values do
            if abs v > largest then
                largest <- abs v

        if largest = 0.0 then
            0
        else
            let mutable sum = 0.0

            for v in values do
                let ratio = v / largest
                sum <- sum + ratio * ratio

            let log2 = (Math.Log largest + 0.5 * Math.Log sum) / Math.Log 2.0
            max -1000 (min 1000 (int (Math.Floor(log2 + 0.5))))

    /// <summary>
    /// R⁻¹R⁻ᵀ for an upper-triangular R, by back substitution on each column of the identity then one product, which
    /// is (RᵀR)⁻¹ = (AᵀA)⁻¹ for R from a QR factorisation of A. Returns <c>Error</c> when a diagonal entry of R is 0 or
    /// not finite or an entry of the inverse is not finite. Cost O(n³).
    /// </summary>
    let unscaledCovariance (r: Matrix) : Result<Matrix, string> =
        let n = Matrix.rowCount r
        let usable (d: float) = d <> 0.0 && not (Double.IsNaN d || Double.IsInfinity d)

        if not (Seq.forall (fun i -> usable (Matrix.get r i i)) (seq { 0 .. n - 1 })) then
            Error "the triangular factor is singular or not finite, so the covariance does not exist."
        else
            // s holds R⁻¹ by rows, upper triangular.
            let s: float[] = Array.zeroCreate (n * n)

            for c in 0 .. n - 1 do
                s[c * n + c] <- 1.0 / Matrix.get r c c

                for i in c - 1 .. -1 .. 0 do
                    let mutable sum = 0.0

                    for k in i + 1 .. c do
                        sum <- sum + Matrix.get r i k * s[k * n + c]

                    s[i * n + c] <- -sum / Matrix.get r i i

            let u: float[] = Array.zeroCreate (n * n)

            for j in 0 .. n - 1 do
                for k in 0 .. n - 1 do
                    let mutable sum = 0.0

                    for l in max j k .. n - 1 do
                        sum <- sum + s[j * n + l] * s[k * n + l]

                    u[j * n + k] <- sum

            if Array.exists (fun v -> Double.IsNaN v || Double.IsInfinity v) u then
                Error "the covariance overflows: the design is too close to rank-deficient."
            else
                Ok(Matrix.init n n (fun j k -> u[j * n + k]))

/// <summary>Linear regression: a line from <c>Comoments</c>, and multiple, weighted and polynomial least squares.</summary>
/// <remarks>
/// The multiple fits solve by Householder QR (<c>Matrix.qr</c>), never by the normal equations, so the condition
/// number of the problem enters once, not squared.
/// </remarks>
module Regression =
    let private isFinite (x: float) : bool = not (Double.IsNaN x || Double.IsInfinity x)

    let private hypot (a: float) (b: float) : float =
        let high = max (abs a) (abs b)
        let low = min (abs a) (abs b)
        if high = 0.0 then 0.0 else high * sqrt (1.0 + (low / high) * (low / high))

    /// <summary>
    /// The line through the pairs of the comoments: slope C / M2x, the intercept ȳ − slope · x̄, the residual variance
    /// n(var_y − cov²/var_x)/(n − 2) evaluated as n · var_y · (1 − R²)/(n − 2) with R² the squared correlation, the
    /// slope standard error s/√(n·var_x), the intercept standard error s·√(1/n + x̄²/(n·var_x)) and R² =
    /// cov²/(var_x·var_y), with the roots and ratios formed apart. Domain: the comoments store the squares of the
    /// spreads, so var_x, var_y and cov must be finite and, where not 0, normal doubles (spreads from about 1e-154 to
    /// 1e154); every field but the degrees of freedom and the count is NaN outside it (tests "simple regression at the
    /// edges of the spreads Comoments can hold" and "NaN comoments give a simple regression of NaN fields"). An x
    /// spread whose square underflows to exactly 0 reads as a constant and is not detected; a var_y of exactly 0
    /// beside a non-zero covariance, which |cov|² ≤ var_x·var_y forbids, is a y spread that underflowed, and gives NaN
    /// standard errors and residual standard error. The residual variance keeps only about ε·var_y in absolute terms,
    /// so the standard errors carry a relative error of about ε/(1 − R²) (ε = 2⁻⁵²): where 1 − R² is at most 64ε they
    /// are rounding, and the standard errors and the residual standard error are NaN, the line and R² kept (test
    /// "simple regression gives NaN standard errors where 1 - R squared is rounding", which also pins amp 1e-5, where
    /// 1 − R² is about 6e-12, within 1e-4 of <c>linear</c>'s). Relative error of every field at most 1e-12 where
    /// 1 − R² is at least about 2e-4, over the 10 pairs of the test "simple regression against exact fractions";
    /// <c>linear</c> on a constant and x has no such loss. Where var_x is at most (8ε|x̄|)², x counts as constant: every
    /// field but the degrees of freedom and the count is NaN. A <c>Comoments</c> emptied back to a constant x by
    /// <c>remove</c> or <c>unmerge</c> keeps a rounding-level M2x whose slope would be a large finite number. The
    /// threshold reads that residue as zero where it is of the order of the rounding of the mean: it does for twelve
    /// pairs removed within 1.2e-13 of the remaining x = 3 (the test "x emptied back to a constant by removals gives a
    /// NaN slope"). Pairs removed from farther away can leave a larger residue, which is not detected: twelve pairs
    /// within 1.2 of x = 3 leave a finite slope of ordinary size, whose standard error is far larger (the test "a
    /// residue left by removals from far away is not detected"). A y emptied back to a constant the same way can keep
    /// a residue too, and R² then reads a small number where a fresh fold gives NaN. Fold the remaining pairs afresh
    /// after such removals. Fewer than two pairs gives NaN fields; two pairs give the line through them with NaN
    /// standard errors and residual standard error. A constant y (exactly zero covariance) gives slope 0, standard
    /// errors 0 and a NaN R². Cost O(1).
    /// </summary>
    let simple (m: Comoments) : SimpleRegression =
        let n = Comoments.count m
        let dof = if n > 2.0 then n - 2.0 else 0.0

        let undefined: SimpleRegression =
            {
                Slope = nan
                Intercept = nan
                SlopeStandardError = nan
                InterceptStandardError = nan
                RSquared = nan
                ResidualStandardError = nan
                DegreesOfFreedom = dof
                Count = n
            }

        if not (n >= 2.0) then
            undefined
        else
            let varianceX = Comoments.varianceX m
            let meanX = Comoments.meanX m
            let limit = 8.0 * RegressionSupport.epsilon * abs meanX

            // A spread that overflowed, or whose square is subnormal and so has lost bits, is no spread.
            let unusable (v: float) = not (isFinite v) || (v <> 0.0 && abs v < 2.2250738585072014e-308)

            if not (varianceX > limit * limit) || unusable varianceX || unusable (Comoments.varianceY m) || unusable (Comoments.covariance m) then
                undefined
            else
                let varianceY = Comoments.varianceY m
                let r = Comoments.correlation m
                let rSquared = if varianceY > 0.0 then r * r else nan

                // n · var_y · (1 − R²) keeps only ε · var_y absolute, so where 1 − R² is at most 64ε the residual
                // variance is rounding; a zero var_y beside a non-zero covariance is a spread whose square underflowed.
                let s =
                    if n <= 2.0 then nan
                    elif varianceY = 0.0 then (if Comoments.covariance m = 0.0 then 0.0 else nan)
                    elif 1.0 - rSquared <= 64.0 * RegressionSupport.epsilon then nan
                    else sqrt n * sqrt varianceY * sqrt ((1.0 - rSquared) / dof)

                let spread = sqrt n * sqrt varianceX

                {
                    Slope = Comoments.slope m
                    Intercept = Comoments.intercept m
                    SlopeStandardError = s / spread
                    InterceptStandardError = s * hypot (1.0 / sqrt n) (meanX / spread)
                    RSquared = rSquared
                    ResidualStandardError = s
                    DegreesOfFreedom = dof
                    Count = n
                }

    let private checkShape (name: string) (design: Matrix) (y: float[]) : unit =
        let rows = Matrix.rowCount design
        let columns = Matrix.columnCount design

        if columns = 0 then
            raise (ArgumentException(sprintf "%s needs at least one design column." name))

        if y.Length <> rows then
            raise (
                ArgumentException(
                    sprintf "%s needs one observation per design row; the design has %d rows and y has length %d." name rows y.Length
                )
            )

        if rows < columns then
            raise (
                ArgumentException(
                    sprintf "%s needs at least as many rows as columns; the design is %d x %d." name rows columns
                )
            )

    // Whether any entry of the design, y or the weights is NaN or infinite.
    let private anyNonFinite (design: Matrix) (y: float[]) (weights: float[]) : bool =
        let mutable found = Array.exists (fun v -> not (isFinite v)) y || Array.exists (fun v -> not (isFinite v)) weights

        for i in 0 .. Matrix.rowCount design - 1 do
            for j in 0 .. Matrix.columnCount design - 1 do
                if not (isFinite (Matrix.get design i j)) then
                    found <- true

        found

    let private fit (name: string) (design: Matrix) (y: float[]) (weights: float[]) : Result<LinearRegression, string> =
        let rows = Matrix.rowCount design
        let columns = Matrix.columnCount design

        if anyNonFinite design y weights then
            Ok
                {
                    Coefficients = Array.create columns nan
                    StandardErrors = Array.create columns nan
                    TStatistics = Array.create columns nan
                    PValues = Array.create columns nan
                    RSquared = nan
                    AdjustedRSquared = nan
                    ResidualStandardError = nan
                    DegreesOfFreedom = nan
                    Covariance = Matrix.init columns columns (fun _ _ -> nan)
                    Residuals = Array.create rows nan
                }
        else
            // Row factors r = √(w / 4^k), with 4^k above the largest weight, so every r lies in [0, 1]. A row of factor 0
            // takes no part in the fit.
            let largestWeight = Array.max weights
            let above = RegressionSupport.exponentNear [| largestWeight |] + 1
            let k = if largestWeight = 0.0 then 0 elif above >= 0 then (above + 1) / 2 else -((-above) / 2)
            let factor = Array.map (fun w -> sqrt (RegressionSupport.scaleByPowerOfTwo w (-2 * k))) weights

            // The rows of positive weight, heaviest first, ties in row order: Householder QR mixes each row into those
            // after it, so a light row ahead of a heavy one would be rounded at the heavy row's ε.
            let kept =
                [| for i in 0 .. rows - 1 do if factor[i] > 0.0 then i |]
                |> Array.sortWith (fun i l ->
                    let c = compare factor[l] factor[i]
                    if c <> 0 then c else compare i l)

            let count = kept.Length

            if count < columns then
                Error(sprintf "%s: only %d rows have positive weights, fewer than the %d design columns." name count columns)
            else
                let isConstant (j: int) =
                    let first = Matrix.get design kept[0] j
                    first <> 0.0 && Array.forall (fun i -> Matrix.get design i j = first) kept

                let constantColumn = Array.tryFind isConstant [| 0 .. columns - 1 |]
                let constant = constantColumn.IsSome
                let weightedColumn (j: int) = Array.map (fun i -> factor[i] * Matrix.get design i j) kept
                // y is divided by the power of two near the norm of the weighted y, then, where the design has a
                // constant column, the heaviest row's value is subtracted from every row, exactly for close values
                // (Sterbenz), and the centred values are divided by the power of two near their own weighted norm.
                // The fit runs on those quotients, so an offset in y does not round away the spread about it.
                let firstExponent = RegressionSupport.exponentNear (Array.map (fun i -> factor[i] * y[i]) kept)
                let firstScaled (v: float) = RegressionSupport.scaleByPowerOfTwo v (-firstExponent)
                let reference = if constant then firstScaled y[kept[0]] else 0.0
                let centred = Array.map (fun v -> firstScaled v - reference) y
                let centredExponent = RegressionSupport.exponentNear (Array.map (fun i -> factor[i] * centred[i]) kept)
                let yExponent = firstExponent + centredExponent
                // Every row's centred y in units of 2^yExponent.
                let scaledY = Array.map (fun v -> RegressionSupport.scaleByPowerOfTwo v (-centredExponent)) centred
                // The reference in the same units, and as the amount the constant column's coefficient carries back.
                let scaledReference = RegressionSupport.scaleByPowerOfTwo reference (-centredExponent)
                let referenceY = if constant then y[kept[0]] else 0.0
                // The power of two near each column's norm: the fit runs on the quotients.
                let columnExponents = Array.init columns (fun j -> RegressionSupport.exponentNear (weightedColumn j))

                let a =
                    Matrix.init count columns (fun r j ->
                        RegressionSupport.scaleByPowerOfTwo
                            (factor[kept[r]] * Matrix.get design kept[r] j)
                            (-columnExponents[j]))

                let b = Array.map (fun i -> factor[i] * scaledY[i]) kept
                let decomposition = Matrix.qr a

                match QrDecomposition.leastSquares decomposition b with
                | Error message -> Error(name + ": " + message)
                | Ok beta ->
                    match RegressionSupport.unscaledCovariance (QrDecomposition.r decomposition) with
                    | Error message -> Error(name + ": " + message)
                    | Ok unscaled ->
                        let dof = count - columns
                        let fitted = Matrix.multiplyVector a beta
                        let mutable rss = 0.0

                        for r in 0 .. count - 1 do
                            let e = b[r] - fitted[r]
                            rss <- rss + e * e

                        let squares = Array.sumBy (fun i -> factor[i] * factor[i]) kept

                        // The total sum of squares about the weighted mean of the centred y, which is exactly 0 for a
                        // constant y, not a rounding residue of a computed mean; about 0 without a constant column.
                        let centre =
                            if constant then
                                Array.sumBy (fun i -> factor[i] * factor[i] * scaledY[i]) kept / squares
                            else
                                0.0

                        let mutable tss = 0.0

                        for i in kept do
                            let d = factor[i] * (scaledY[i] - centre)
                            tss <- tss + d * d

                        let variance = if dof > 0 then rss / float dof else nan

                        // The unweighted model of each row, in units of 2^yExponent, about the reference.
                        let modelled =
                            Array.init rows (fun i ->
                                let mutable sum = 0.0

                                for j in 0 .. columns - 1 do
                                    sum <-
                                        sum
                                        + RegressionSupport.scaleByPowerOfTwo (Matrix.get design i j) (-columnExponents[j])
                                          * beta[j]

                                sum)

                        // R's test for an essentially perfect fit (lm.R, lines 345 to 348): the residual variance
                        // against (mean(f)² + var(f))·1e-30, f the unweighted fitted values of every row. The residual
                        // variance is that of the unweighted residuals of the rows of positive weight, whose rounding
                        // the weights do not change.
                        let essentiallyPerfect =
                            dof > 0
                            && (let f = Array.map (fun v -> v + scaledReference) modelled
                                let mean = Array.sum f / float rows

                                let spread =
                                    if rows > 1 then
                                        Array.sumBy (fun v -> (v - mean) * (v - mean)) f / float (rows - 1)
                                    else
                                        0.0

                                let residualVariance =
                                    Array.sumBy (fun i -> (scaledY[i] - modelled[i]) * (scaledY[i] - modelled[i])) kept
                                    / float dof

                                residualVariance < (mean * mean + spread) * 1e-30)

                        let rSquared =
                            if tss > 0.0 then
                                let r2 = 1.0 - rss / tss
                                if r2 < 0.0 then 0.0 else r2
                            else
                                nan

                        let adjusted =
                            if dof > 0 then
                                1.0 - (1.0 - rSquared) * float (count - (if constant then 1 else 0)) / float dof
                            else
                                nan

                        let scaledError j = sqrt (variance * Matrix.get unscaled j j)
                        let unit j = yExponent - columnExponents[j]

                        // The constant column's coefficient carries the reference back: y_ref / c, c its value.
                        let carried (j: int) =
                            if constantColumn = Some j then referenceY / Matrix.get design kept[0] j else 0.0

                        let coefficients =
                            Array.init columns (fun j ->
                                RegressionSupport.scaleByPowerOfTwo beta[j] (unit j) + carried j)

                        // Each t statistic in the scaled units, the carried reference included.
                        let t j =
                            (beta[j] + RegressionSupport.scaleByPowerOfTwo (carried j) (-(unit j))) / scaledError j

                        let residuals =
                            Array.init rows (fun i ->
                                RegressionSupport.scaleByPowerOfTwo (scaledY[i] - modelled[i]) yExponent)

                        Ok
                            {
                                Coefficients = coefficients
                                StandardErrors =
                                    Array.init columns (fun j -> RegressionSupport.scaleByPowerOfTwo (scaledError j) (unit j))
                                TStatistics = Array.init columns (fun j -> if essentiallyPerfect then nan else t j)
                                PValues =
                                    Array.init columns (fun j ->
                                        if essentiallyPerfect then nan else PValue.student TwoSided (float dof) (t j))
                                RSquared = rSquared
                                AdjustedRSquared = adjusted
                                ResidualStandardError =
                                    RegressionSupport.scaleByPowerOfTwo (sqrt variance) (yExponent + k)
                                DegreesOfFreedom = float dof
                                Covariance =
                                    Matrix.init columns columns (fun j l ->
                                        RegressionSupport.scaleByPowerOfTwo
                                            (variance * Matrix.get unscaled j l)
                                            (2 * yExponent - columnExponents[j] - columnExponents[l]))
                                Residuals = residuals
                            }

    /// <summary>
    /// Ordinary least squares of y on the design columns, by <c>Matrix.qr</c> and <c>QrDecomposition.leastSquares</c>,
    /// whose <c>Error</c> for a rank-deficient design is passed on. σ² = eᵀe/(n − p), NaN where n = p; the covariance
    /// is σ²(RᵀR)⁻¹ with R from the same decomposition, and each p-value is two-sided from the Student t with n − p
    /// degrees of freedom. A column holding one non-zero value in every row is a constant: R² is then about the mean
    /// of y and the adjusted R² uses (n − 1)/(n − p); otherwise R² is about zero and it uses n/(n − p). That is the
    /// adjusted R² of R's <c>summary.lm</c>, 1 − (1 − R²)(n − df.int)/(n − p), with df.int = 1 for a model with an
    /// intercept term and 0 without (from documentation: stats/R/lm.R of the R sources at trunk, read there); R
    /// decides by the model's intercept term, this function by a constant column, so columns that span a constant
    /// without holding one (x and 1 − x) are read as having none. R² is clamped below at 0.
    /// Each design column and y are first divided by an exact power of two near their 2-norms, and the coefficients,
    /// covariance and standard errors multiplied back. <c>Matrix.qr</c> calls a design rank-deficient when some
    /// |Rₖₖ| ≤ max(m, n)·ε·(the largest column norm), so a full-rank design whose columns differ in scale by about 1/ε
    /// would read as rank-deficient without it; a power of two is exact, so a design that is already well scaled gives
    /// the same digits. The same scaling of y keeps a sum of squares from overflowing at data near 1e200 or
    /// underflowing near 1e-200: t, p and R² are formed from the scaled quantities, and a standard error or covariance
    /// beyond the doubles reads ±∞ or 0, not a finite wrong number. Where the design has a constant column, y is
    /// first centred on the first row's value, exactly for values within a factor of two of it, and that column's
    /// coefficient carries it back, so an offset in y costs no digits of the spread about it; without a constant
    /// column an offset in y costs about ε·|offset|/spread relative (ε = 2⁻⁵², general knowledge). A NaN or infinite
    /// entry of the design or y gives <c>Ok</c> with every field NaN, before any factorisation. Raises
    /// <c>ArgumentException</c> unless y has one entry per row, the design has at least one column and at least as
    /// many rows as columns. Relative error of every field at most 1e-12 over well-conditioned designs: the integer
    /// designs (1, x1, x2) and (x1, x2) of 12 rows in the tests "linear regression against exact fractions" and
    /// "regression without a constant against exact fractions", and (1, x) of 12 rows with y offset by 1e8 in "a
    /// dominant weight and an offset in y give the exact fractions", against exact fractions; the residuals within
    /// 1e-12 absolute there. Cost O(m·n²).
    /// </summary>
    let linear (design: Matrix) (y: float[]) : Result<LinearRegression, string> =
        checkShape "linear" design y
        fit "linear" design y (Array.create (Matrix.rowCount design) 1.0)

    /// <summary>
    /// Weighted least squares, minimising Σ wᵢ(yᵢ − xᵢ·β)²: each row and observation is multiplied by √wᵢ, as for
    /// <c>linear</c>. Only rows of positive weight count: n is their number, the degrees of freedom are n − p, the
    /// constant column and the mean of y for R² are taken over them, and the mean is the weighted mean. Weights are
    /// first divided by an exact power of four above the largest, so a weight scale near 1e300 or 1e-300 neither
    /// overflows nor underflows a sum; the coefficients, covariance, t, p and R² do not depend on the scale of the
    /// weights, and σ² scales with it. A positive weight below about 1e-323 times the largest underflows to 0 there
    /// and its row counts as one of weight 0 (general knowledge: the least subnormal double is 4.9e-324). The rows
    /// are taken heaviest first, ties in row order, because Householder QR rounds a light row placed ahead of a heavy
    /// one at the heavy row's ε, which with a dominant weight costs the coefficients their digits; y is then centred
    /// on the heaviest row's value as <c>linear</c> centres it. <c>Residuals</c> are y − Xβ unweighted, for every row.
    /// Returns <c>Error</c> when fewer rows have a positive weight than the design has columns, and as <c>linear</c>
    /// does: a weight ratio of about 1e30 or more can put a column of a full-rank design below the rank test of
    /// <c>Matrix.qr</c>, which is relative to the largest column norm (1e30 on the sixth row of (1, x1), x1 = 1..12,
    /// is an Error, on its first row it is not: test "a weight ratio of 1e30 can read as rank-deficient"; R's
    /// <c>qr</c> tolerance would drop the column too, general knowledge). A NaN or +∞ weight, or a NaN or infinite
    /// entry of the design or y, gives <c>Ok</c> with every field NaN. Raises <c>ArgumentException</c> as
    /// <c>linear</c> does, and when the weights are not as long as y, and <c>ArgumentOutOfRangeException</c> for a
    /// negative weight (−∞ included). Relative error of every field at most 1e-12 over well-conditioned designs: the
    /// 12 rows, (1, x1) and weights 1, 2, 3 with one 0 of the test "weighted regression against exact fractions";
    /// (1, x) of 12 rows with one weight from 1e4 to 1e15 beside weights of 1 and y offset by up to 1e10, the seven
    /// rows of "a dominant weight and an offset in y give the exact fractions"; and a weight of 1e30 on the first row
    /// of (1, x1) in "a dominant weight gives every field at the exact fraction". Cost O(m·n²).
    /// </summary>
    let weighted (design: Matrix) (y: float[]) (weights: float[]) : Result<LinearRegression, string> =
        checkShape "weighted" design y

        if weights.Length <> y.Length then
            raise (
                ArgumentException(
                    sprintf "weighted needs one weight per observation; y has length %d and the weights %d." y.Length weights.Length
                )
            )

        for w in weights do
            if w < 0.0 then
                raise (ArgumentOutOfRangeException(nameof weights, "weights must not be negative."))

        fit "weighted" design y weights

    /// <summary>
    /// The least-squares polynomial of the given degree, y = c₀ + c₁x + … + c_d xᵈ, coefficients in ascending powers,
    /// fitted as <c>linear</c> fits on the raw Vandermonde design (columns 1, x, …, xᵈ), its errors named
    /// "polynomial". The conditioning of that design grows quickly with the degree and with the distance of x from 0;
    /// centre and scale x first for a high degree. The powers are not checked against the range of the doubles:
    /// powers that overflow give <c>Ok</c> with every field NaN, as a non-finite design does, and powers that
    /// underflow leave a column that reads as rank-deficient, an <c>Error</c> (degree 4 on x = 1..12 times 1e80 and
    /// times 1e-159, in the test named below). Returns <c>Error</c> as <c>linear</c> does,
    /// including for fewer than degree + 1 distinct x values, and a NaN or infinite x or y gives <c>Ok</c> with every
    /// field NaN, at degree 0 too (test "polynomial names itself and reads every x"). Raises
    /// <c>ArgumentOutOfRangeException</c> for a negative degree, and <c>ArgumentException</c> unless xs and ys have one
    /// length and at least degree + 1 entries.
    /// Coefficients within 1e-12 absolute of the exact ones for the degree 2 polynomial through 8 integer points of the
    /// test "polynomial regression recovers exact coefficients"; no bound is shown for higher degrees. Cost O(n·d²).
    /// </summary>
    let polynomial (degree: int) (xs: float[]) (ys: float[]) : Result<LinearRegression, string> =
        if degree < 0 then
            raise (ArgumentOutOfRangeException(nameof degree, "degree must be zero or more."))

        if xs.Length <> ys.Length then
            raise (
                ArgumentException(
                    sprintf "polynomial needs xs and ys of one length; the lengths are %d and %d." xs.Length ys.Length
                )
            )

        let design =
            Matrix.init xs.Length (degree + 1) (fun i d ->
                let mutable power = 1.0

                for _ in 1..d do
                    power <- power * xs[i]

                power)

        checkShape "polynomial" design ys
        // A NaN or infinite x gives every field NaN, at degree 0 too, where the design never reads x.
        let observations = if Array.exists (fun v -> not (isFinite v)) xs then Array.create ys.Length nan else ys
        fit "polynomial" design observations (Array.create xs.Length 1.0)

    /// <summary>
    /// Nonlinear least squares of ys on a model of xs: <c>LeastSquares.levenbergMarquardt</c> with
    /// <c>LeastSquaresOptions.standard</c> on the residuals yᵢ − model(p, xᵢ), from the initial parameters. Its
    /// forward-difference Jacobian J (n points by p parameters) at the returned parameters gives the covariance
    /// σ²(JᵀJ)⁻¹ = σ² R⁻¹R⁻ᵀ from <c>Matrix.qr</c> of J, never a Cholesky factor of JᵀJ, which would square the
    /// condition number of J; σ² = Σ rᵢ²/(n − p) = 2 · Cost/(n − p), formed from the per-row residuals scaled by an
    /// exact power of two before squaring, so a cost in the subnormal range costs no digits (test "the residual
    /// standard error is exact where the sum of squares is subnormal"). Each Jacobian column is first divided by an exact power of two
    /// near its 2-norm and the covariance and standard errors multiplied back, as <c>linear</c> does for its design,
    /// so the covariance does not depend on the units of the parameters (test "columns scaled by 2^15 and 2^-15 give
    /// the unscaled fit with the errors rescaled"). The scaling does not widen what <c>LeastSquares</c> can fit: its
    /// own rank test runs on the unscaled Jacobian (from documentation: <c>QrDecomposition.leastSquares</c>, whose
    /// threshold is max(m, n)·ε·(the largest column norm)), so parameters whose columns differ in scale by about
    /// 1/ε or more stop the fit with <c>Converged</c> false; rescale such a model. The model is called as
    /// <c>model parameters x</c> with a fresh parameter array and must return a finite number. The fit is local: a
    /// model with several minima gives the one the initial parameters lead to, and <c>Converged</c> is false where
    /// <c>LeastSquares</c> stopped short of its tolerance, which bounds the change of the cost and of the parameters,
    /// not their error. That tolerance has an absolute floor, ‖δ‖ ≤ 1e-10·(‖p‖ + 1e-10) on the step in p, so
    /// each parameter is scaled by the exact power of two nearest its initial magnitude (1 for a start of 0) and
    /// <c>LeastSquares</c> runs on the scaled values q, which are about 1 at the start; the floor is then in units of
    /// the start, not absolute (tests "parameters of 1e-23 are fitted, not left at the start" and "an exponential of
    /// amplitude 1.38e-23 is fitted"). Where a fitted parameter's power of two differs from its start's by more than
    /// 2^20 the fit runs again from the fitted values scaled afresh, repeating while such a parameter moves, up to 32
    /// runs in all, and <c>Iterations</c> counts every run; <c>Converged</c> is false where the 32nd run still moves
    /// one by more than 2^20 (a parameter heading for 0 or infinity). One run closes a gap of about 1e20 between the
    /// start and the fit and a few runs close the gap of the test "a start 1e45 to 1e77 times the answer is rescaled
    /// and restarted until it fits"; an ordinary start is one run (test "an ordinary start is one run"). A
    /// start far below the fitted magnitude can still read as flat to the forward difference, which steps by a
    /// relative 1.5e-8 of the parameter (general knowledge): then a Jacobian column is exactly 0 and the standard
    /// errors are NaN, which is how such a start shows (test "a start too far from the fitted magnitude shows as NaN
    /// standard errors": a slope of 2e-23 from a start of 1e-34 stays at the start, with <c>Converged</c> true).
    /// Start within a few orders of magnitude of the fitted value.
    /// <c>Parameters</c> and <c>Converged</c> are kept where the parameters are not identified, but are meaningful
    /// only as the combination the data fix.
    /// NaN fields: the standard errors, covariance and residual standard error where n = p (no degrees of
    /// freedom), and the standard errors and covariance where R has a zero or non-finite diagonal entry (a parameter
    /// the model does not use: the test "a parameter the model ignores gives NaN errors and keeps the others"), where
    /// the smallest |Rₖₖ| of the column-equilibrated Jacobian is at most 1e-7 times the largest (parameters that only
    /// enter as a combination, p0·p1 or p0·e^p1; 1e-7 is the default <c>tol</c> of R's <c>qr</c>, from documentation:
    /// base/R/qr.R at trunk, line 24; the test "redundant parameters give NaN standard errors and keep the
    /// parameters"; a forward difference lifts exact dependence only to about √ε, general knowledge) or the Jacobian is not finite (test "a Jacobian that is not finite gives NaN errors and keeps the residual
    /// standard error"). A NaN or infinite xs or ys entry gives every field NaN but <c>DegreesOfFreedom</c>,
    /// <c>Converged</c> false and <c>Iterations</c> 0, before the model is called. <c>LeastSquares</c> minimises a
    /// sum of squares: where its cost is not finite (squared residuals of about 1e308 in total, or a model that is
    /// NaN at the start) or reads 0 over residuals that are not 0 (squares below about 1e-308, which underflow), the
    /// fit is outside the doubles' range: the standard errors, covariance and residual standard error are NaN and
    /// <c>Converged</c> is false, with the parameters as <c>LeastSquares</c> left them (test "residuals whose squares
    /// leave the double range give NaN errors and no convergence"). Raises <c>ArgumentException</c> unless xs and ys
    /// have one length, the initial parameters are not empty and there are at least as many points as parameters.
    /// On the 21 points of the test "curve fitting recovers exact parameters" (y = 2.5 e^(−1.3x), from the start
    /// (1, 1)) the parameters are within 1e-8 relative of the exact ones and the residual standard error is below
    /// 1e-8; on the 10-point line of "a model linear in its parameters gives the linear regression" the parameters
    /// are within 1e-8 and the standard errors and covariance within 1e-6 relative of <c>linear</c>'s, the
    /// forward-difference Jacobian limiting them to about √ε ≈ 1.5e-8 (general knowledge). Cost: that of
    /// <c>LeastSquares.levenbergMarquardt</c> plus O(n·p²).
    /// </summary>
    let curve (model: float[] -> float -> float) (initial: float[]) (xs: float[]) (ys: float[]) : CurveFit =
        if xs.Length <> ys.Length then
            raise (
                ArgumentException(
                    sprintf "curve needs xs and ys of one length; the lengths are %d and %d." xs.Length ys.Length
                )
            )

        if initial.Length = 0 then
            raise (ArgumentException "curve needs at least one parameter; the initial vector is empty.")

        if xs.Length < initial.Length then
            raise (
                ArgumentException(
                    sprintf "curve needs at least as many points as parameters; there are %d points and %d parameters." xs.Length initial.Length
                )
            )

        let n = xs.Length
        let p = initial.Length
        let dof = float (n - p)

        let undefined (parameters: float[]) (iterations: int) : CurveFit =
            {
                Parameters = parameters
                StandardErrors = Array.create p nan
                Covariance = Matrix.init p p (fun _ _ -> nan)
                ResidualStandardError = nan
                DegreesOfFreedom = dof
                Iterations = iterations
                Converged = false
            }

        if Array.exists (fun v -> not (isFinite v)) xs || Array.exists (fun v -> not (isFinite v)) ys then
            undefined (Array.create p nan) 0
        else
            // LeastSquares runs on q with p = 2^e · q, e the power of two nearest each parameter's magnitude (0 for 0),
            // so that its step test, which has an absolute floor, sees parameters of about 1.
            let exponentsOf (values: float[]) = Array.map (fun v -> RegressionSupport.exponentNear [| v |]) values

            let toParameters (exponents: int[]) (q: float[]) =
                Array.mapi (fun j v -> RegressionSupport.scaleByPowerOfTwo v exponents[j]) q

            let run (exponents: int[]) (start: float[]) =
                LeastSquares.levenbergMarquardt
                    LeastSquaresOptions.standard
                    (fun q ->
                        let parameters = toParameters exponents q
                        Array.map2 (fun x y -> y - model parameters x) xs ys)
                    (Array.mapi (fun j v -> RegressionSupport.scaleByPowerOfTwo v (-exponents[j])) start)

            let startExponents = exponentsOf initial
            let mutable runExponents = startExponents
            let mutable runStart = initial
            let mutable latest = run runExponents runStart
            let mutable runs = 1
            let mutable totalIterations = latest.Iterations
            let mutable foundExponents = exponentsOf (toParameters runExponents latest.Parameters)
            // A parameter that moved by more than 2^20 from the magnitude it was scaled by is scaled afresh and the fit
            // run again from the fitted values, up to 32 runs in all.
            let mutable moving = Array.exists2 (fun a b -> abs (a - b) > 20) runExponents foundExponents

            while moving && runs < 32 do
                runStart <- toParameters runExponents latest.Parameters
                runExponents <- foundExponents
                latest <- run runExponents runStart
                runs <- runs + 1
                totalIterations <- totalIterations + latest.Iterations
                foundExponents <- exponentsOf (toParameters runExponents latest.Parameters)
                moving <- Array.exists2 (fun a b -> abs (a - b) > 20) runExponents foundExponents

            let result = latest
            let exponents = runExponents
            let stillMoving = moving
            let parameters = toParameters exponents result.Parameters
            let iterations = totalIterations

            // A cost that overflowed, is NaN, or underflowed to 0 over residuals that are not 0, says nothing about the fit.
            let outside =
                not (isFinite result.Cost)
                || (result.Cost = 0.0 && Array.exists (fun r -> r <> 0.0) result.Residuals)

            if outside then
                undefined parameters iterations
            elif dof = 0.0 then
                { undefined parameters iterations with Converged = result.Converged && not stillMoving }
            else
                // σ from the per-row residuals scaled by a power of two before squaring, so that a sum of squares in
                // the subnormal range does not cost digits.
                let sigmaExponent = RegressionSupport.exponentNear result.Residuals

                let scaledSigma =
                    sqrt (
                        Array.sumBy
                            (fun r ->
                                let s = RegressionSupport.scaleByPowerOfTwo r (-sigmaExponent)
                                s * s)
                            result.Residuals
                        / dof
                    )

                let sigma = RegressionSupport.scaleByPowerOfTwo scaledSigma sigmaExponent
                let jacobian = result.Jacobian
                let column (j: int) = Array.init n (fun i -> Matrix.get jacobian i j)
                let finite = Seq.forall (fun j -> Array.forall isFinite (column j)) (seq { 0 .. p - 1 })

                let covariance =
                    if not finite then
                        Error "the Jacobian is not finite."
                    else
                        let columnExponents = Array.init p (fun j -> RegressionSupport.exponentNear (column j))

                        let scaled =
                            Matrix.init n p (fun i j ->
                                RegressionSupport.scaleByPowerOfTwo (Matrix.get jacobian i j) (-columnExponents[j]))

                        let r = QrDecomposition.r (Matrix.qr scaled)

                        match RegressionSupport.unscaledCovariance r with
                        | Error message -> Error message
                        | Ok unscaled ->
                            let diagonal = Array.init p (fun j -> abs (Matrix.get r j j))

                            if not (Array.min diagonal > RegressionSupport.identifiability * Array.max diagonal) then
                                Error "the parameters are not identified."
                            else
                                Ok(columnExponents, unscaled)

                // The Jacobian is with respect to q, so a covariance entry of p carries 2^(e_j + e_l) and a standard error 2^e_j.
                let covarianceAt (j: int) (l: int) =
                    match covariance with
                    | Error _ -> nan
                    | Ok(columnExponents, unscaled) ->
                        RegressionSupport.scaleByPowerOfTwo
                            (scaledSigma * scaledSigma * Matrix.get unscaled j l)
                            (2 * sigmaExponent - columnExponents[j] - columnExponents[l] + exponents[j] + exponents[l])

                {
                    Parameters = parameters
                    StandardErrors =
                        Array.init p (fun j ->
                            match covariance with
                            | Error _ -> nan
                            | Ok(columnExponents, unscaled) ->
                                RegressionSupport.scaleByPowerOfTwo
                                    (scaledSigma * sqrt (Matrix.get unscaled j j))
                                    (sigmaExponent - columnExponents[j] + exponents[j]))
                    Covariance = Matrix.init p p covarianceAt
                    ResidualStandardError = sigma
                    DegreesOfFreedom = dof
                    Iterations = iterations
                    Converged = result.Converged && not stillMoving
                }

/// <summary>Pearson, Spearman and Kendall correlation of two samples, and the tests that it is 0.</summary>
/// <remarks>
/// Every function raises <c>ArgumentException</c> unless the two arrays have one length. A NaN or infinite entry, a
/// constant sample (the correlation is 0/0) and fewer than 2 pairs give NaN, or a <c>TestResult</c> of NaN fields, and
/// raise nothing. <c>pearsonTest</c> and <c>spearmanTest</c>, which have n − 2 degrees of freedom, also give NaN
/// fields below 3 pairs, where R's <c>cor.test</c> raises. Where
/// |r| = 1 the t statistic is ±∞, a one-sided p-value is 0 or 1 and the two-sided one is 0.
/// </remarks>
module Correlation =
    let private isFinite (x: float) : bool = not (Double.IsNaN x || Double.IsInfinity x)

    let private checkLengths (name: string) (xs: float[]) (ys: float[]) : unit =
        if xs.Length <> ys.Length then
            raise (
                ArgumentException(
                    sprintf "%s needs two samples of one length; the lengths are %d and %d." name xs.Length ys.Length
                )
            )

    // Whether there are too few pairs or a value is not finite, so that the answer is NaN.
    let private unusable (xs: float[]) (ys: float[]) : bool =
        xs.Length < 2 || Array.exists (fun v -> not (isFinite v)) xs || Array.exists (fun v -> not (isFinite v)) ys

    // Pearson r of the pairs from their comoments, each sample first divided by an exact power of two near its 2-norm
    // (r does not change), so that the squares in the comoments neither overflow nor underflow.
    let private pearsonOf (xs: float[]) (ys: float[]) : float =
        let ex = RegressionSupport.exponentNear xs
        let ey = RegressionSupport.exponentNear ys

        Array.fold2
            (fun m x y ->
                Comoments.add
                    true
                    m
                    (RegressionSupport.scaleByPowerOfTwo x (-ex))
                    (RegressionSupport.scaleByPowerOfTwo y (-ey)))
            Comoments.zero
            xs
            ys
        |> Comoments.correlation

    // The sizes of the runs of equal values in a sorted array.
    let private runLengths (sorted: float[]) : float[] =
        let runs = ResizeArray<float>()
        let mutable start = 0

        for i in 1 .. sorted.Length do
            if i = sorted.Length || sorted[i] <> sorted[start] then
                runs.Add(float (i - start))
                start <- i

        runs.ToArray()

    // The number of pairs i < j with values[i] > values[j], by a bottom-up merge sort that takes the left element on a tie.
    let private countInversions (values: float[]) : float =
        let n = values.Length
        let mutable source = Array.copy values
        let mutable target: float[] = Array.zeroCreate n
        let mutable inversions = 0.0
        let mutable width = 1

        while width < n do
            let mutable low = 0

            while low < n do
                let middle = min (low + width) n
                let high = min (low + 2 * width) n
                let mutable i = low
                let mutable j = middle
                let mutable k = low

                while i < middle || j < high do
                    if j >= high || (i < middle && source[i] <= source[j]) then
                        target[k] <- source[i]
                        i <- i + 1
                    else
                        target[k] <- source[j]
                        inversions <- inversions + float (middle - i)
                        j <- j + 1

                    k <- k + 1

                low <- low + 2 * width

            let swap = source
            source <- target
            target <- swap
            width <- width * 2

        inversions

    // Knight's counts for finite samples of at least 2 pairs: S = concordant − discordant, n0 − n1 and n0 − n2 (n0 all
    // pairs, n1 the pairs tied in x, n2 those tied in y), and the tie-group sizes of x and of y.
    let private kendallCounts (xs: float[]) (ys: float[]) : struct (float * float * float * float[] * float[]) =
        let n = xs.Length

        let order =
            Array.init n id
            |> Array.sortWith (fun a b ->
                let c = compare xs[a] xs[b]
                if c <> 0 then c else compare ys[a] ys[b])

        let sortedX = Array.map (fun i -> xs[i]) order
        let sortedY = Array.map (fun i -> ys[i]) order
        let tx = runLengths sortedX
        let ty = runLengths (Array.sort ys)

        // Runs of pairs equal in both coordinates, within the order by x then y.
        let joint = ResizeArray<float>()
        let mutable start = 0

        for i in 1..n do
            if i = n || sortedX[i] <> sortedX[start] || sortedY[i] <> sortedY[start] then
                joint.Add(float (i - start))
                start <- i

        let pairs (runs: seq<float>) = Seq.sumBy (fun t -> t * (t - 1.0) / 2.0) runs
        let n0 = float n * (float n - 1.0) / 2.0
        let discordant = countInversions sortedY
        let s = n0 - pairs tx - pairs ty + pairs joint - 2.0 * discordant
        struct (s, n0 - pairs tx, n0 - pairs ty, tx, ty)

    // R's variance of S under independence, with the tie corrections (stats/R/cor.test.R, lines 113 to 128).
    let private varianceOfS (n: float) (tx: float[]) (ty: float[]) : float =
        let total (f: float -> float) (runs: float[]) = Array.sumBy f runs
        let pair t = t * (t - 1.0)
        let triple t = t * (t - 1.0) * (t - 2.0)
        let weighted t = t * (t - 1.0) * (2.0 * t + 5.0)
        let v0 = n * (n - 1.0) * (2.0 * n + 5.0)
        let v1 = total pair tx * total pair ty
        let v2 = total triple tx * total triple ty
        (v0 - total weighted tx - total weighted ty) / 18.0
        + v1 / (2.0 * n * (n - 1.0))
        + v2 / (9.0 * n * (n - 1.0) * (n - 2.0))

    // The t statistic of a correlation on dof degrees of freedom, with (1 − r)(1 + r) for 1 − r² so that it does not
    // cancel near |r| = 1.
    let private tStatistic (r: float) (dof: float) : float =
        r * sqrt (dof / ((1.0 - r) * (1.0 + r)))

    let private tTest (alternative: Alternative) (r: float) (n: int) : TestResult =
        if Double.IsNaN r || n < 3 then
            TestSupport.nanResult
        else
            let dof = float (n - 2)
            TestSupport.tResult alternative (tStatistic r dof) dof

    /// <summary>
    /// Pearson's product-moment correlation, <c>Comoments.correlation</c> of the pairs folded with compensation: C /
    /// (√M2x · √M2y), clamped to [−1, 1]: exactly linear data give ±1 or a value a few ulps inside it (0.9999999999999998
    /// for the integers 1 to 5 against 2x + 1; measured here, the test "perfect correlation gives an infinite t and a
    /// p-value of 0"). Each sample is
    /// first divided by an exact power of two near its 2-norm, which leaves r unchanged and keeps the squares of the
    /// comoments from overflowing or underflowing (test "Pearson at the scales of 1e200 and 1e-200"). NaN for a
    /// constant sample, fewer than 2 pairs or a non-finite entry. Relative error at most 1e-12 against exact
    /// fractions for the 8 pairs of the test "Pearson, Spearman and Kendall against exact fractions" (two
    /// permutations of 1..8); no wider domain is pinned. Cost O(n).
    /// </summary>
    let pearson (xs: float[]) (ys: float[]) : float =
        checkLengths "pearson" xs ys
        if unusable xs ys then nan else pearsonOf xs ys

    /// <summary>
    /// Spearman's rank correlation: Pearson's r of the average ranks of each sample (<c>Describe.ranks</c>, tied values
    /// sharing the mean of their positions), which is the coefficient with the tie correction built in. A NaN is
    /// checked before ranking, which raises on NaN, so a NaN entry gives NaN. NaN for a constant sample (all ranks
    /// equal), fewer than 2 pairs or a non-finite entry. Relative error at most 1e-12 against exact fractions for the
    /// untied 8 pairs of the test "Pearson, Spearman and Kendall against exact fractions" and the tied 8 pairs of
    /// "Spearman with ties against the reference"; no wider domain is pinned. Cost O(n log n).
    /// </summary>
    let spearman (xs: float[]) (ys: float[]) : float =
        checkLengths "spearman" xs ys
        if unusable xs ys then nan else pearsonOf (Describe.ranks xs) (Describe.ranks ys)

    /// <summary>
    /// Kendall's tau-b, (C − D)/√((n₀ − n₁)(n₀ − n₂)), with C and D the concordant and discordant pairs, n₀ = n(n − 1)/2
    /// and n₁, n₂ the pairs tied in x and in y, by Knight's algorithm (Knight, "A computer method for calculating
    /// Kendall's tau with ungrouped data", JASA 61, 1966; general knowledge): sort by x then y and count the
    /// inversions of y in a merge sort. The counts are exact while n(n − 1)/2 is below 2⁵³, so for n up to about
    /// 1.3e8. NaN where every x or every y is tied (n₀ − n₁ or n₀ − n₂ is 0), for fewer than 2 pairs or a non-finite
    /// entry. Relative error at most 1e-12 against exact fractions for the untied and tied 8 pairs of the tests
    /// "Pearson, Spearman and Kendall against exact fractions" and "Kendall tau-b matches a direct count", and within
    /// 1e-14 of a direct count of every pair for sizes 2 to 40 with ties ("Kendall over generated samples with ties
    /// matches the direct count and variance"). Cost O(n log n).
    /// </summary>
    let kendall (xs: float[]) (ys: float[]) : float =
        checkLengths "kendall" xs ys

        if unusable xs ys then
            nan
        else
            let struct (s, d1, d2, _, _) = kendallCounts xs ys
            if d1 = 0.0 || d2 = 0.0 then nan else s / sqrt (d1 * d2)

    /// <summary>
    /// The test that Pearson's correlation is 0, for normally distributed pairs: t = r√((n − 2)/(1 − r²)) on n − 2
    /// degrees of freedom, the p-value from the Student t distribution on the alternative's side (two-sided is twice
    /// the smaller tail, at most 1), as R's <c>cor.test</c> does. At |r| = 1 the statistic is ±∞ and the p-values are
    /// those of an infinite statistic. NaN fields below 3 pairs, for a constant sample and for a non-finite entry.
    /// Statistic and p-value within 1e-12 relative of exact fractions for the 8 pairs of the test "Pearson,
    /// Spearman and Kendall against exact fractions" (measured here: that test). Near |r| = 1 the statistic's relative
    /// error grows as about ε/(1 − |r|), from the rounding of r in (1 − r)(1 + r) (general knowledge): exactly linear
    /// data can give an r a few ulps inside ±1 and a large finite t where the exact t is infinite, which is why the
    /// test "perfect correlation gives an infinite t and a p-value of 0" uses 8 points. Cost O(n).
    /// </summary>
    let pearsonTest (alternative: Alternative) (xs: float[]) (ys: float[]) : TestResult =
        checkLengths "pearsonTest" xs ys
        if unusable xs ys then TestSupport.nanResult else tTest alternative (pearsonOf xs ys) xs.Length

    /// <summary>
    /// The test that Spearman's correlation is 0 by the t approximation: t = r√((n − 2)/(1 − r²)) on n − 2 degrees of
    /// freedom, with r the Spearman correlation: R's <c>cor.test</c> for <c>method = "spearman"</c> uses that
    /// approximation for n above 1290, with ties, or with <c>exact = FALSE</c>, and otherwise uses the AS 89 algorithm
    /// for its p-value (from documentation: stats/R/cor.test.R at trunk, lines 143 to 175, read there); this never
    /// uses AS 89, and with ties it is R's own answer.
    /// NaN fields below 3 pairs, for a constant sample and for a non-finite entry. Statistic and p-value within 1e-12
    /// relative of exact fractions for the tied 8 pairs of the test "Spearman with ties against the reference". Cost
    /// O(n log n).
    /// </summary>
    let spearmanTest (alternative: Alternative) (xs: float[]) (ys: float[]) : TestResult =
        checkLengths "spearmanTest" xs ys

        if unusable xs ys then
            TestSupport.nanResult
        else
            tTest alternative (pearsonOf (Describe.ranks xs) (Describe.ranks ys)) xs.Length

    /// <summary>
    /// The test that Kendall's tau is 0 by the normal approximation: z = S/√var(S), S = C − D, with R's variance of S
    /// under independence corrected for ties in x and in y (stats/R/cor.test.R at trunk, lines 113 to 133, read there),
    /// no continuity correction, and the p-value from the standard normal on the alternative's side. This is R's
    /// <c>cor.test(method = "kendall", exact = FALSE)</c>; R's default for n below 50 without ties is the exact
    /// distribution of S (from documentation: stats/R/cor.test.R, line 93), which this does not use. The normal
    /// approximation is poor at small n: for three pairs in perfect agreement it gives a two-sided p of 0.117 where
    /// the exact value is 1/3 (general knowledge, by enumerating the 6 orderings; the approximation's value is pinned
    /// by the test "tests on two and three points"). The statistic is undefined at 2 pairs, where R's variance of S is
    /// 0/0, so the result has NaN fields below 3 pairs. The degrees of freedom are NaN. NaN fields also for a constant
    /// sample and for a non-finite entry. Statistic and p-value within 1e-12 relative of the reference for the untied
    /// and tied 8 pairs of the tests "Pearson, Spearman and Kendall against exact fractions" and "Kendall tau-b
    /// matches a direct count", and the statistic within 1e-12 of S over R's variance, computed directly, for sizes 3
    /// to 40 with ties ("Kendall over generated samples with ties matches the direct count and variance"). Cost
    /// O(n log n).
    /// </summary>
    let kendallTest (alternative: Alternative) (xs: float[]) (ys: float[]) : TestResult =
        checkLengths "kendallTest" xs ys

        // At 2 pairs R's variance of S is 0/0 and the statistic is undefined.
        if unusable xs ys || xs.Length < 3 then
            TestSupport.nanResult
        else
            let struct (s, d1, d2, tx, ty) = kendallCounts xs ys

            if d1 = 0.0 || d2 = 0.0 then
                TestSupport.nanResult
            else
                let z = s / sqrt (varianceOfS (float xs.Length) tx ty)

                { TestSupport.nanResult with
                    Statistic = z
                    PValue = PValue.normal alternative z }
