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
/// the covariance) is NaN. <c>RSquared</c> and <c>AdjustedRSquared</c> are NaN where the total sum of squares is 0.
/// </remarks>
type LinearRegression =
    {
        /// The least-squares coefficients, one per design column.
        Coefficients: float[]
        /// The standard error of each coefficient, the square root of the diagonal of <c>Covariance</c>.
        StandardErrors: float[]
        /// Each coefficient over its standard error: ±∞ for a non-zero coefficient with a zero standard error, NaN for 0 over 0.
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

module internal RegressionSupport =
    let epsilon = 2.220446049250313e-16

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

    let private describe (x: float) : string = if Double.IsNaN x then "a NaN" else "an infinite value"

    let private hypot (a: float) (b: float) : float =
        let high = max (abs a) (abs b)
        let low = min (abs a) (abs b)
        if high = 0.0 then 0.0 else high * sqrt (1.0 + (low / high) * (low / high))

    /// <summary>
    /// The line through the pairs of the comoments: slope C / M2x, the intercept ȳ − slope · x̄, the residual variance
    /// n(var_y − cov²/var_x)/(n − 2) evaluated as n · var_y · (1 − R²)/(n − 2) with R² the squared correlation, the
    /// slope standard error s/√(n·var_x), the intercept standard error s·√(1/n + x̄²/(n·var_x)) and R² =
    /// cov²/(var_x·var_y). Every root and ratio is formed apart, so no product of spreads overflows or underflows
    /// where the answer is a double. Where var_x is at most (8ε|x̄|)², ε = 2⁻⁵², x counts as constant: every field but the
    /// degrees of freedom and the count is NaN. A <c>Comoments</c> emptied back to a constant x by <c>remove</c> or
    /// <c>unmerge</c> keeps a rounding-level M2x whose slope would be a large finite number. The threshold reads that
    /// residue as zero where it is of the order of the rounding of the mean: it does for twelve pairs removed within
    /// 1.2e-13 of the remaining x = 3 (the test "x emptied back to a constant by removals gives a NaN slope"). Pairs
    /// removed from farther away leave a larger residue, not detected: twelve pairs within 1.2 of x = 3 leave a
    /// finite slope (the test "a residue left by removals from far away is not detected"). Fold the remaining pairs
    /// afresh after such removals. Fewer than two pairs gives NaN fields; two pairs give the line through them with
    /// NaN standard errors and residual standard error. A constant y gives slope 0, standard errors 0 and a NaN R².
    /// Cost O(1).
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

            if not (varianceX > limit * limit) then
                undefined
            else
                let varianceY = Comoments.varianceY m
                let r = Comoments.correlation m
                let rSquared = if varianceY > 0.0 then r * r else nan

                let s =
                    if n <= 2.0 then nan
                    elif varianceY = 0.0 then 0.0
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

    // The first non-finite input as an Error, before any factorisation (Matrix.qr reads NaN data as Ok with NaN).
    let private findNonFinite (name: string) (design: Matrix) (y: float[]) (weights: float[]) : string option =
        let mutable found: string option = None

        for i in 0 .. Matrix.rowCount design - 1 do
            for j in 0 .. Matrix.columnCount design - 1 do
                let v = Matrix.get design i j

                if found.IsNone && not (isFinite v) then
                    found <- Some(sprintf "%s: the design holds %s at row %d, column %d." name (describe v) i j)

        for i in 0 .. y.Length - 1 do
            if found.IsNone && not (isFinite y[i]) then
                found <- Some(sprintf "%s: y holds %s at index %d." name (describe y[i]) i)

        for i in 0 .. weights.Length - 1 do
            if found.IsNone && not (isFinite weights[i]) then
                found <- Some(sprintf "%s: the weights hold %s at index %d." name (describe weights[i]) i)

        found

    let private fit (name: string) (design: Matrix) (y: float[]) (weights: float[]) : Result<LinearRegression, string> =
        let rows = Matrix.rowCount design
        let columns = Matrix.columnCount design

        match findNonFinite name design y weights with
        | Some message -> Error message
        | None ->
            // Row factors r = √(w / 4^k), with 4^k above the largest weight, so every r lies in [0, 1]. A row of factor 0
            // takes no part in the fit.
            let largestWeight = Array.max weights
            let above = RegressionSupport.exponentNear [| largestWeight |] + 1
            let k = if largestWeight = 0.0 then 0 elif above >= 0 then (above + 1) / 2 else -((-above) / 2)
            let factor = Array.map (fun w -> sqrt (RegressionSupport.scaleByPowerOfTwo w (-2 * k))) weights
            let kept = [| for i in 0 .. rows - 1 do if factor[i] > 0.0 then i |]
            let count = kept.Length

            if count < columns then
                Error(sprintf "%s: only %d rows have positive weights, fewer than the %d design columns." name count columns)
            else
                let weightedColumn (j: int) = Array.map (fun i -> factor[i] * Matrix.get design i j) kept
                let weightedY = Array.map (fun i -> factor[i] * y[i]) kept
                // The power of two near each column's norm, and near y's: the fit runs on the quotients.
                let columnExponents = Array.init columns (fun j -> RegressionSupport.exponentNear (weightedColumn j))
                let yExponent = RegressionSupport.exponentNear weightedY

                let a =
                    Matrix.init count columns (fun r j ->
                        RegressionSupport.scaleByPowerOfTwo
                            (factor[kept[r]] * Matrix.get design kept[r] j)
                            (-columnExponents[j]))

                let b = Array.map (fun v -> RegressionSupport.scaleByPowerOfTwo v (-yExponent)) weightedY
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

                        let isConstant (j: int) =
                            let first = Matrix.get design kept[0] j
                            first <> 0.0 && Array.forall (fun i -> Matrix.get design i j = first) kept

                        let constant = Seq.exists isConstant (seq { 0 .. columns - 1 })
                        let squares = Array.sumBy (fun i -> factor[i] * factor[i]) kept

                        let centre =
                            if constant then
                                Array.fold2 (fun acc i v -> acc + factor[i] * v) 0.0 kept b / squares
                            else
                                0.0

                        let mutable tss = 0.0

                        for r in 0 .. count - 1 do
                            let d = b[r] - factor[kept[r]] * centre
                            tss <- tss + d * d

                        // The variance of the scaled problem; a value that overflowed is no variance.
                        let variance =
                            if dof > 0 && isFinite (rss / float dof) then rss / float dof else nan

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

                        let residuals =
                            Array.init rows (fun i ->
                                let mutable modelled = 0.0

                                for j in 0 .. columns - 1 do
                                    modelled <-
                                        modelled
                                        + RegressionSupport.scaleByPowerOfTwo (Matrix.get design i j) (-columnExponents[j])
                                          * beta[j]

                                RegressionSupport.scaleByPowerOfTwo
                                    (RegressionSupport.scaleByPowerOfTwo y[i] (-yExponent) - modelled)
                                    yExponent)

                        Ok
                            {
                                Coefficients = Array.init columns (fun j -> RegressionSupport.scaleByPowerOfTwo beta[j] (unit j))
                                StandardErrors =
                                    Array.init columns (fun j -> RegressionSupport.scaleByPowerOfTwo (scaledError j) (unit j))
                                TStatistics = Array.init columns (fun j -> beta[j] / scaledError j)
                                PValues =
                                    Array.init columns (fun j ->
                                        PValue.student TwoSided (float dof) (beta[j] / scaledError j))
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
    /// beyond the doubles reads ±∞ or 0, not a finite wrong number. An <c>Error</c> names the first NaN or infinite
    /// entry of the design or y, before any factorisation. Raises <c>ArgumentException</c> unless y has one entry per
    /// row, the design has at least one column and at least as many rows as columns. Cost O(m·n²).
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
    /// weights, and σ² scales with it. <c>Residuals</c> are y − Xβ unweighted, for every row. Returns <c>Error</c> when
    /// fewer rows have a positive weight than the design has columns, when a weight is NaN or infinite, and as
    /// <c>linear</c> does. Raises <c>ArgumentException</c> as <c>linear</c> does, and when the weights are not as long
    /// as y, and <c>ArgumentOutOfRangeException</c> for a negative weight. Cost O(m·n²).
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
    /// by <c>linear</c> on the raw Vandermonde design (columns 1, x, …, xᵈ). The conditioning of that design grows
    /// quickly with the degree and with the distance of x from 0; centre and scale x first for a high degree. Returns
    /// <c>Error</c> as <c>linear</c> does, including for fewer than degree + 1 distinct x values. Raises
    /// <c>ArgumentOutOfRangeException</c> for a negative degree, and <c>ArgumentException</c> unless xs and ys have one
    /// length and at least degree + 1 entries. Cost O(n·d²).
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

        linear design ys
