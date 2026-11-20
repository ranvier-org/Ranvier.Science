namespace Ranvier.Numerics

open System

/// Options of `LeastSquares.levenbergMarquardt`.
type LeastSquaresOptions =
    {
        /// Relative tolerance of the step and cost tests; at least 0 and not NaN.
        Tolerance: float
        /// The cap on iterations (one Jacobian each); at least 1.
        MaximumIterations: int
    }

module LeastSquaresOptions =
    /// Tolerance 1e-10, at most 500 iterations.
    let standard: LeastSquaresOptions =
        { Tolerance = 1e-10
          MaximumIterations = 500 }

/// The result of `LeastSquares.levenbergMarquardt`.
type LeastSquaresResult =
    {
        /// The best accepted parameters: a fresh array.
        Parameters: float[]
        /// The residual vector at `Parameters`: a fresh array.
        Residuals: float[]
        /// The m × n Jacobian at `Parameters`. All NaN when the initial residuals were not finite.
        Jacobian: Matrix
        /// ½ Σ rᵢ² at `Parameters`.
        Cost: float
        /// The number of iterations started, each of which evaluates the Jacobian once; 0 when the fit stopped before
        /// the first.
        Iterations: int
        /// True when a convergence test of `levenbergMarquardt` was met; false when the fit stopped short.
        Converged: bool
    }

/// Nonlinear least squares.
module LeastSquares =
    // sqrt of 2^-52, the relative forward-difference step of MINPACK's fdjac2.
    let private stepScale = 1.4901161193847656e-08

    // Past this the damping has failed to produce a decrease.
    let private lambdaLimit = 1e16

    // A floor that keeps the damping able to grow after repeated division.
    let private lambdaFloor = 1e-300

    let private checkLength (name: string) (expected: int) (actual: int) : unit =
        if expected <> actual then
            raise (
                ArgumentException(
                    sprintf
                        "%s must keep its length; the first residual vector had length %d and this one has length %d."
                        name
                        expected
                        actual
                )
            )

    let private halfSumOfSquares (r: float[]) : float =
        let mutable s = 0.0

        for x in r do
            s <- s + x * x

        0.5 * s

    let private allFinite (r: float[]) : bool =
        r |> Array.forall (fun x -> not (Double.IsNaN x || Double.IsInfinity x))

    // The forward-difference Jacobian at p, given the residuals r there.
    let private differenceJacobian (residuals: float[] -> float[]) (p: float[]) (r: float[]) : Matrix =
        let m = r.Length

        let columns =
            Array.init p.Length (fun j ->
                let h = if p[j] = 0.0 then stepScale else stepScale * abs p[j]
                let shifted = Array.copy p
                shifted[j] <- p[j] + h
                let rj = residuals shifted
                checkLength "the residual function" m rj.Length
                Array.init m (fun i -> (rj[i] - r[i]) / h))

        Matrix.init m p.Length (fun i j -> columns[j][i])

    let private solve
        (options: LeastSquaresOptions)
        (residuals: float[] -> float[])
        (jacobian: float[] -> float[] -> Matrix)
        (initial: float[])
        : LeastSquaresResult =
        if Double.IsNaN options.Tolerance || options.Tolerance < 0.0 then
            raise (
                ArgumentOutOfRangeException(
                    "options",
                    "levenbergMarquardt needs a tolerance of at least 0 that is not NaN."
                )
            )

        if options.MaximumIterations < 1 then
            raise (ArgumentOutOfRangeException("options", "levenbergMarquardt needs MaximumIterations of at least 1."))

        let n = initial.Length

        if n = 0 then
            raise (ArgumentException("levenbergMarquardt needs at least one parameter; the initial vector is empty."))

        let tolerance = options.Tolerance
        let mutable p = Array.copy initial
        let mutable r = Array.copy (residuals p)
        let m = r.Length

        let evaluateJacobian (at: float[]) (rAt: float[]) : Matrix =
            let j = jacobian at rAt

            if Matrix.rowCount j <> m || Matrix.columnCount j <> n then
                raise (
                    ArgumentException(
                        sprintf
                            "the Jacobian must be %d x %d (residuals by parameters); the function returned %d x %d."
                            m
                            n
                            (Matrix.rowCount j)
                            (Matrix.columnCount j)
                    )
                )

            j

        if not (allFinite r) then
            { Parameters = p
              Residuals = r
              Jacobian = Matrix.init m n (fun _ _ -> nan)
              Cost = halfSumOfSquares r
              Iterations = 0
              Converged = false }
        else
            let mutable cost = halfSumOfSquares r
            let mutable j = evaluateJacobian p r
            let mutable lambda = 1e-3
            let scale: float[] = Array.zeroCreate n
            let mutable iterations = 0
            let mutable converged = cost = 0.0
            // A non-finite Jacobian gives no step to try: the fit stops there, short of convergence.
            let mutable finished = converged || not (allFinite j.Data)

            while not finished && iterations < options.MaximumIterations do
                iterations <- iterations + 1

                for c in 0 .. n - 1 do
                    let norm = Vector.norm (Array.init m (fun i -> Matrix.get j i c))

                    if norm > scale[c] then
                        scale[c] <- norm

                let mutable accepted = false
                let mutable firstTrial = true

                while not finished && not accepted do
                    let root = sqrt lambda
                    let isFirst = firstTrial
                    firstTrial <- false

                    let augmented =
                        Matrix.init (m + n) n (fun row c ->
                            if row < m then Matrix.get j row c
                            elif row - m = c then root * (if scale[c] = 0.0 then 1.0 else scale[c])
                            else 0.0)

                    let rhs = Array.init (m + n) (fun i -> if i < m then -r[i] else 0.0)

                    match QrDecomposition.leastSquares (Matrix.qr augmented) rhs with
                    | Error _ ->
                        lambda <- lambda * 10.0

                        if lambda > lambdaLimit then
                            finished <- true
                    | Ok delta ->
                        let stepSmall = Vector.norm delta <= tolerance * (Vector.norm p + tolerance)

                        if isFirst && stepSmall then
                            converged <- true
                            finished <- true
                        else
                            let trial = Array.init n (fun c -> p[c] + delta[c])
                            let rTrial = Array.copy (residuals trial)
                            checkLength "the residual function" m rTrial.Length
                            let costTrial = halfSumOfSquares rTrial

                            if costTrial < cost then
                                let decrease = cost - costTrial
                                let previous = cost
                                p <- trial
                                r <- rTrial
                                cost <- costTrial
                                accepted <- true
                                lambda <- max (lambda / 10.0) lambdaFloor
                                j <- evaluateJacobian p r

                                if cost = 0.0 || stepSmall || decrease <= tolerance * previous then
                                    converged <- true
                                    finished <- true
                                elif not (allFinite j.Data) then
                                    finished <- true
                            else
                                lambda <- lambda * 10.0

                                if lambda > lambdaLimit then
                                    finished <- true

            { Parameters = p
              Residuals = r
              Jacobian = j
              Cost = cost
              Iterations = iterations
              Converged = converged }

    /// Minimises ½ Σ rᵢ² over the parameters by Levenberg–Marquardt, with a forward-difference Jacobian: column j
    /// steps parameter j by √ε·|pⱼ|, or √ε when pⱼ is 0, with ε = 2⁻⁵² (MINPACK's `fdjac2`). See
    /// `levenbergMarquardtWith` for the algorithm, the convergence rules and the raise conditions. Each iteration
    /// makes n residual calls for the Jacobian and one QR factorisation of an (m + n) × n matrix, O(mn²); every
    /// rejected trial adds one more factorisation and one residual call.
    let levenbergMarquardt
        (options: LeastSquaresOptions)
        (residuals: float[] -> float[])
        (initial: float[])
        : LeastSquaresResult =
        solve options residuals (differenceJacobian residuals) initial

    /// Minimises ½ Σ rᵢ² over the parameters by Levenberg–Marquardt with the caller's Jacobian (m residuals by n
    /// parameters). Each trial solves min ‖[J; √λ·D]·δ + [r; 0]‖ by QR, with D diagonal and Dⱼⱼ the largest norm of
    /// column j of J seen so far, or 1 while that is 0 (MINPACK's scaling, from documentation: Moré, "The
    /// Levenberg–Marquardt algorithm: implementation and theory", 1978). λ starts at 1e-3, is divided by 10 on an
    /// accepted step (never below 1e-300) and multiplied by 10 on a rejected one. A trial is accepted when its cost is
    /// lower, so a NaN cost rejects it; a rank-deficient solve counts as a rejected trial. The Jacobian is evaluated
    /// once per iteration, at the accepted parameters.
    ///
    /// `Converged` is true when the step has ‖δ‖ ≤ Tolerance·(‖p‖ + Tolerance) and is either accepted or is the first
    /// trial of an iteration (the step at the λ the iteration starts with, near the Gauss–Newton step); a trial after
    /// a rejection in the same iteration never ends the fit by this test, because rejection raises λ and shrinks the
    /// step wherever no descent exists. It is also true when an accepted step lowers the cost by at most Tolerance
    /// times the old cost, or when the cost is 0. The result is the best accepted parameters. It is false at
    /// `MaximumIterations`, when λ passes 1e16 without an accepted step, when the initial residuals are not all finite
    /// (then `Iterations` is 0 and the Jacobian is NaN), and when the Jacobian at the accepted parameters holds a NaN
    /// or infinite entry, the caller's or the forward difference alike: the fit then stops there without a trial, and
    /// `Iterations` is 0 when that is the start. Tolerance bounds the relative change of the cost and of the
    /// parameters between iterations, not the error of the parameters: a flat valley can stop the fit well short of
    /// the minimum.
    ///
    /// Raises `ArgumentOutOfRangeException` for a negative or NaN tolerance or a cap below 1, and `ArgumentException`
    /// for an empty initial vector, for a residual vector whose length differs from the first, or for a Jacobian that
    /// is not residuals × parameters. An exception raised by the caller's functions propagates unchanged. Inputs are
    /// not mutated and every returned array is fresh.
    let levenbergMarquardtWith
        (options: LeastSquaresOptions)
        (residuals: float[] -> float[])
        (jacobian: float[] -> Matrix)
        (initial: float[])
        : LeastSquaresResult =
        solve options residuals (fun p _ -> jacobian p) initial
