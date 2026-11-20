namespace Ranvier.Numerics

open System

/// Vectors as `float[]`. Two vectors of different lengths raise `ArgumentException` naming both lengths. Every
/// function returns a fresh array and leaves its inputs unchanged.
module Vector =
    let private checkSameLength (name: string) (a: float[]) (b: float[]) : unit =
        if a.Length <> b.Length then
            raise (
                ArgumentException(
                    sprintf "%s needs vectors of one length; the lengths are %d and %d." name a.Length b.Length
                )
            )

    /// Σ aᵢ bᵢ, summed in index order without compensation; 0 for empty vectors. Cost O(n).
    let dot (a: float[]) (b: float[]) : float =
        checkSameLength "dot" a b
        let mutable sum = 0.0

        for i in 0 .. a.Length - 1 do
            sum <- sum + a[i] * b[i]

        sum

    /// The componentwise sum. Cost O(n).
    let add (a: float[]) (b: float[]) : float[] =
        checkSameLength "add" a b
        Array.init a.Length (fun i -> a[i] + b[i])

    /// The componentwise difference a − b. Cost O(n).
    let subtract (a: float[]) (b: float[]) : float[] =
        checkSameLength "subtract" a b
        Array.init a.Length (fun i -> a[i] - b[i])

    /// Every component multiplied by the factor. Cost O(n).
    let scale (factor: float) (v: float[]) : float[] = Array.map (fun x -> factor * x) v

    /// The Euclidean norm, max |xᵢ| · √Σ(xᵢ / max)², which does not overflow or underflow where the norm itself is
    /// representable. 0 for an empty or all-zero vector, NaN when any component is NaN, and +∞ when any other
    /// component is infinite. Uses only + − × ÷, `sqrt`, `abs` and comparisons. Cost O(n).
    let norm (v: float[]) : float =
        let mutable largest = 0.0
        let mutable anyNaN = false
        let mutable anyInfinite = false

        for x in v do
            if Double.IsNaN x then anyNaN <- true
            elif Double.IsInfinity x then anyInfinite <- true
            elif abs x > largest then largest <- abs x

        if anyNaN then nan
        elif anyInfinite then infinity
        elif largest = 0.0 then 0.0
        else
            let mutable sum = 0.0

            for x in v do
                let r = x / largest
                sum <- sum + r * r

            largest * sqrt sum

/// A dense matrix, stored by rows. Immutable. `=` compares the shapes and then the entries, so two matrices built from
/// equal rows are equal; it is the record's default equality, with float entry equality (a NaN entry equals nothing).
type Matrix =
    internal
        { RowCount: int
          ColumnCount: int
          Data: float[] }

/// The LU factorisation P·A = L·U of a square matrix by partial pivoting, made by `Matrix.lu`. Immutable. A
/// decomposition of a matrix holding a NaN or infinite entry reads NaN everywhere.
type LuDecomposition =
    internal
        { Order: int
          /// L below the diagonal (unit diagonal implied) and U on and above it, by rows.
          Factors: float[]
          /// Row i of P·A is row Permutation[i] of A.
          Permutation: int[]
          Sign: float
          NonFinite: bool }

/// Solving and the determinant from an `LuDecomposition`. Raises `ArgumentException` naming both lengths for a
/// right-hand side of the wrong length.
module LuDecomposition =
    /// The x with A·x = b, by forward and back substitution with every sum in increasing index order. Returns a fresh
    /// array on every call, so one decomposition serves many right-hand sides; an order-0 decomposition gives an empty
    /// array. A decomposition of a matrix with a non-finite entry gives a fresh array of NaN. Raises
    /// `ArgumentException` unless the right-hand side's length equals the matrix order. Cost O(n²). Uses only + − × ÷.
    let solve (decomposition: LuDecomposition) (b: float[]) : float[] =
        let n = decomposition.Order

        if b.Length <> n then
            raise (
                ArgumentException(
                    sprintf
                        "solve needs a right-hand side as long as the matrix order; the order is %d and the vector has length %d."
                        n
                        b.Length
                )
            )

        if decomposition.NonFinite then
            Array.create n nan
        else
            let f = decomposition.Factors
            let x: float[] = Array.zeroCreate n

            for i in 0 .. n - 1 do
                let mutable sum = b[decomposition.Permutation[i]]

                for j in 0 .. i - 1 do
                    sum <- sum - f[i * n + j] * x[j]

                x[i] <- sum

            for i in n - 1 .. -1 .. 0 do
                let mutable sum = x[i]

                for j in i + 1 .. n - 1 do
                    sum <- sum - f[i * n + j] * x[j]

                x[i] <- sum / f[i * n + i]

            x

    /// The determinant: the product of U's diagonal in index order, times the permutation's sign; 1 for order 0. NaN
    /// for a decomposition of a matrix with a non-finite entry. The product can overflow or underflow. Cost O(n).
    let determinant (decomposition: LuDecomposition) : float =
        if decomposition.NonFinite then
            nan
        else
            let n = decomposition.Order
            let mutable product = decomposition.Sign

            for i in 0 .. n - 1 do
                product <- product * decomposition.Factors[i * n + i]

            product

/// The Cholesky factorisation A = L·Lᵀ of a symmetric positive definite matrix, made by `Matrix.cholesky`. Immutable.
/// A decomposition of a matrix holding a NaN or infinite entry reads NaN everywhere.
type CholeskyDecomposition =
    internal
        { Order: int
          /// L by rows; entries above the diagonal are 0.
          Lower: float[]
          NonFinite: bool }

/// Solving from a `CholeskyDecomposition`.
module CholeskyDecomposition =
    /// The x with A·x = b, by two triangular substitutions with every sum in increasing index order. Returns a fresh
    /// array on every call; an order-0 decomposition gives an empty array. A decomposition of a matrix with a
    /// non-finite entry gives a fresh array of NaN. Raises `ArgumentException` naming both lengths unless the
    /// right-hand side's length equals the matrix order. Cost O(n²). Uses only + − × ÷.
    let solve (decomposition: CholeskyDecomposition) (b: float[]) : float[] =
        let n = decomposition.Order

        if b.Length <> n then
            raise (
                ArgumentException(
                    sprintf
                        "solve needs a right-hand side as long as the matrix order; the order is %d and the vector has length %d."
                        n
                        b.Length
                )
            )

        if decomposition.NonFinite then
            Array.create n nan
        else
            let l = decomposition.Lower
            let x: float[] = Array.zeroCreate n

            for i in 0 .. n - 1 do
                let mutable sum = b[i]

                for j in 0 .. i - 1 do
                    sum <- sum - l[i * n + j] * x[j]

                x[i] <- sum / l[i * n + i]

            for i in n - 1 .. -1 .. 0 do
                let mutable sum = x[i]

                for j in i + 1 .. n - 1 do
                    sum <- sum - l[j * n + i] * x[j]

                x[i] <- sum / l[i * n + i]

            x

/// Dense matrices. A negative size raises `ArgumentOutOfRangeException`, an index outside the matrix raises
/// `ArgumentOutOfRangeException`, and a shape or length mismatch raises `ArgumentException` naming both shapes or
/// lengths. Every function returns a fresh matrix or array and leaves its inputs unchanged.
module Matrix =
    let private checkSize (name: string) (rows: int) (columns: int) : unit =
        if rows < 0 then
            raise (ArgumentOutOfRangeException(nameof rows, sprintf "%s needs rows >= 0; rows = %d." name rows))

        if columns < 0 then
            raise (ArgumentOutOfRangeException(nameof columns, sprintf "%s needs columns >= 0; columns = %d." name columns))

    let private checkSameShape (name: string) (a: Matrix) (b: Matrix) : unit =
        if a.RowCount <> b.RowCount || a.ColumnCount <> b.ColumnCount then
            raise (
                ArgumentException(
                    sprintf
                        "%s needs matrices of one shape; the shapes are %d x %d and %d x %d."
                        name
                        a.RowCount
                        a.ColumnCount
                        b.RowCount
                        b.ColumnCount
                )
            )

    let private checkRow (m: Matrix) (i: int) : unit =
        if i < 0 || i >= m.RowCount then
            raise (
                ArgumentOutOfRangeException(
                    nameof i,
                    sprintf "Row %d is outside a matrix of %d rows." i m.RowCount
                )
            )

    let private checkColumn (m: Matrix) (j: int) : unit =
        if j < 0 || j >= m.ColumnCount then
            raise (
                ArgumentOutOfRangeException(
                    nameof j,
                    sprintf "Column %d is outside a matrix of %d columns." j m.ColumnCount
                )
            )

    /// The matrix whose row i is a copy of rows[i]. An empty array gives the 0 × 0 matrix, and an array of empty rows
    /// gives n × 0. Raises `ArgumentException` unless every row has the length of the first. Cost O(n·m).
    let ofRows (rows: float[][]) : Matrix =
        let columns = if rows.Length = 0 then 0 else rows[0].Length

        for i in 0 .. rows.Length - 1 do
            if rows[i].Length <> columns then
                raise (
                    ArgumentException(
                        sprintf
                            "ofRows needs rows of one length; row 0 has length %d and row %d has length %d."
                            columns
                            i
                            rows[i].Length
                    )
                )

        let data = Array.zeroCreate (rows.Length * columns)

        for i in 0 .. rows.Length - 1 do
            for j in 0 .. columns - 1 do
                data[i * columns + j] <- rows[i][j]

        { RowCount = rows.Length
          ColumnCount = columns
          Data = data }

    /// The matrix whose entry (i, j) is entry i j. Sizes of 0 are accepted. Raises `ArgumentOutOfRangeException` for a
    /// negative size. Cost O(rows·columns) calls of the function, in row-major order.
    let init (rows: int) (columns: int) (entry: int -> int -> float) : Matrix =
        checkSize "init" rows columns
        let data = Array.zeroCreate (rows * columns)

        for i in 0 .. rows - 1 do
            for j in 0 .. columns - 1 do
                data[i * columns + j] <- entry i j

        { RowCount = rows
          ColumnCount = columns
          Data = data }

    /// The n × n identity matrix; n = 0 is accepted. Raises `ArgumentOutOfRangeException` for a negative n.
    let identity (n: int) : Matrix =
        checkSize "identity" n n
        init n n (fun i j -> if i = j then 1.0 else 0.0)

    /// The matrix of zeros; sizes of 0 are accepted. Raises `ArgumentOutOfRangeException` for a negative size.
    let zero (rows: int) (columns: int) : Matrix =
        checkSize "zero" rows columns

        { RowCount = rows
          ColumnCount = columns
          Data = Array.zeroCreate (rows * columns) }

    /// The square matrix with the given entries on its diagonal and zeros elsewhere.
    let diagonal (entries: float[]) : Matrix =
        init entries.Length entries.Length (fun i j -> if i = j then entries[i] else 0.0)

    /// The number of rows.
    let rowCount (m: Matrix) : int = m.RowCount

    /// The number of columns.
    let columnCount (m: Matrix) : int = m.ColumnCount

    /// The entry at a row and a column, both counted from 0. Raises `ArgumentOutOfRangeException` outside the matrix.
    let get (m: Matrix) (i: int) (j: int) : float =
        checkRow m i
        checkColumn m j
        m.Data[i * m.ColumnCount + j]

    /// A copy of row i. Raises `ArgumentOutOfRangeException` for a row outside the matrix. Cost O(columns).
    let row (m: Matrix) (i: int) : float[] =
        checkRow m i
        Array.sub m.Data (i * m.ColumnCount) m.ColumnCount

    /// A copy of column j. Raises `ArgumentOutOfRangeException` for a column outside the matrix. Cost O(rows).
    let column (m: Matrix) (j: int) : float[] =
        checkColumn m j
        Array.init m.RowCount (fun i -> m.Data[i * m.ColumnCount + j])

    /// The rows as fresh arrays. Cost O(rows·columns).
    let toRows (m: Matrix) : float[][] = Array.init m.RowCount (fun i -> row m i)

    /// The transpose. Cost O(rows·columns).
    let transpose (m: Matrix) : Matrix =
        init m.ColumnCount m.RowCount (fun i j -> m.Data[j * m.ColumnCount + i])

    /// The entrywise sum. Raises `ArgumentException` unless the shapes are equal. Cost O(rows·columns).
    let add (a: Matrix) (b: Matrix) : Matrix =
        checkSameShape "add" a b

        { a with
            Data = Array.init a.Data.Length (fun k -> a.Data[k] + b.Data[k]) }

    /// The entrywise difference a − b. Raises `ArgumentException` unless the shapes are equal. Cost O(rows·columns).
    let subtract (a: Matrix) (b: Matrix) : Matrix =
        checkSameShape "subtract" a b

        { a with
            Data = Array.init a.Data.Length (fun k -> a.Data[k] - b.Data[k]) }

    /// The product of an n × m matrix and an m × p matrix, each entry summed over the inner index in increasing order
    /// without compensation. An n × 0 by 0 × p product is the n × p zero matrix. Raises `ArgumentException` unless the
    /// column count of the first equals the row count of the second. Cost O(n·m·p).
    let multiply (a: Matrix) (b: Matrix) : Matrix =
        if a.ColumnCount <> b.RowCount then
            raise (
                ArgumentException(
                    sprintf
                        "multiply needs the column count of the first matrix to equal the row count of the second; the shapes are %d x %d and %d x %d."
                        a.RowCount
                        a.ColumnCount
                        b.RowCount
                        b.ColumnCount
                )
            )

        let n = a.RowCount
        let m = a.ColumnCount
        let p = b.ColumnCount
        let data: float[] = Array.zeroCreate (n * p)

        for i in 0 .. n - 1 do
            for k in 0 .. m - 1 do
                let aik = a.Data[i * m + k]

                for j in 0 .. p - 1 do
                    data[i * p + j] <- data[i * p + j] + aik * b.Data[k * p + j]

        { RowCount = n
          ColumnCount = p
          Data = data }

    /// Every entry multiplied by the factor. Cost O(rows·columns).
    let scale (factor: float) (m: Matrix) : Matrix =
        { m with
            Data = Array.map (fun x -> factor * x) m.Data }

    /// The product of the matrix and a vector. Raises `ArgumentException` unless the vector's length equals the column
    /// count. Cost O(rows·columns).
    let multiplyVector (m: Matrix) (v: float[]) : float[] =
        if v.Length <> m.ColumnCount then
            raise (
                ArgumentException(
                    sprintf
                        "multiplyVector needs a vector as long as the column count; the matrix has %d columns and the vector has length %d."
                        m.ColumnCount
                        v.Length
                )
            )

        Array.init m.RowCount (fun i ->
            let mutable sum = 0.0

            for j in 0 .. m.ColumnCount - 1 do
                sum <- sum + m.Data[i * m.ColumnCount + j] * v[j]

            sum)

    let private checkSquare (name: string) (m: Matrix) : unit =
        if m.RowCount <> m.ColumnCount then
            raise (
                ArgumentException(
                    sprintf "%s needs a square matrix; the shape is %d x %d." name m.RowCount m.ColumnCount
                )
            )

    /// ε = 2⁻⁵², the spacing of doubles at 1.
    let private epsilon = 2.220446049250313e-16

    /// The largest |entry|, and whether any entry is NaN or infinite.
    let private scan (m: Matrix) : struct (float * bool) =
        let mutable largest = 0.0
        let mutable nonFinite = false

        for x in m.Data do
            if Double.IsNaN x || Double.IsInfinity x then nonFinite <- true
            elif abs x > largest then largest <- abs x

        struct (largest, nonFinite)

    /// The LU factorisation P·A = L·U by Gaussian elimination with partial pivoting (the pivot is the first entry of
    /// largest magnitude in the column), every update in a fixed order using only + − × ÷ and comparisons. Returns
    /// `Error "singular …"` when a pivot satisfies |uₖₖ| ≤ n·ε·max|aᵢⱼ|, with ε = 2⁻⁵², a threshold relative to the
    /// scale of the matrix, so multiplying the matrix by a constant does not change the verdict. A 0 × 0 matrix gives
    /// `Ok`. A matrix holding a NaN or infinite entry gives `Ok` with no singularity check, and every number read
    /// through the result is NaN. Raises `ArgumentException` for a non-square matrix. Cost O(n³).
    let lu (a: Matrix) : Result<LuDecomposition, string> =
        checkSquare "lu" a
        let n = a.RowCount
        let struct (largest, nonFinite) = scan a

        if nonFinite then
            Ok(
                { Order = n
                  Factors = Array.zeroCreate (n * n)
                  Permutation = Array.init n id
                  Sign = 1.0
                  NonFinite = true }
                : LuDecomposition
            )
        else
            let f = Array.copy a.Data
            let permutation = Array.init n id
            let threshold = float n * epsilon * largest
            let mutable sign = 1.0
            let mutable singularAt = -1
            let mutable k = 0

            while singularAt < 0 && k < n do
                let mutable p = k
                let mutable best = abs f[k * n + k]

                for i in k + 1 .. n - 1 do
                    let v = abs f[i * n + k]

                    if v > best then
                        best <- v
                        p <- i

                if best <= threshold then
                    singularAt <- k
                else
                    if p <> k then
                        for j in 0 .. n - 1 do
                            let t = f[k * n + j]
                            f[k * n + j] <- f[p * n + j]
                            f[p * n + j] <- t

                        let t = permutation[k]
                        permutation[k] <- permutation[p]
                        permutation[p] <- t
                        sign <- -sign

                    for i in k + 1 .. n - 1 do
                        let factor = f[i * n + k] / f[k * n + k]
                        f[i * n + k] <- factor

                        for j in k + 1 .. n - 1 do
                            f[i * n + j] <- f[i * n + j] - factor * f[k * n + j]

                    k <- k + 1

            if singularAt >= 0 then
                Error(
                    sprintf
                        "lu: the matrix is singular to working precision; the pivot in column %d is at most %g, the threshold n·ε·max|aᵢⱼ|."
                        singularAt
                        threshold
                )
            else
                Ok(
                    { Order = n
                      Factors = f
                      Permutation = permutation
                      Sign = sign
                      NonFinite = false }
                    : LuDecomposition
                )

    /// The x with A·x = b through `lu`; `Error "singular …"` as `lu` gives it, and NaN throughout for a non-finite
    /// entry. Raises `ArgumentException` for a non-square matrix, or for a vector whose length differs from the
    /// matrix order (checked before the factorisation). Cost O(n³).
    let solve (a: Matrix) (b: float[]) : Result<float[], string> =
        checkSquare "solve" a

        if b.Length <> a.RowCount then
            raise (
                ArgumentException(
                    sprintf
                        "solve needs a right-hand side as long as the matrix order; the order is %d and the vector has length %d."
                        a.RowCount
                        b.Length
                )
            )

        lu a |> Result.map (fun d -> LuDecomposition.solve d b)

    /// The inverse through `lu`, one solve per column of the identity: `Error "singular …"` as `lu` gives it, and a
    /// matrix of NaN for a non-finite entry. Raises `ArgumentException` for a non-square matrix. Cost O(n³).
    let inverse (a: Matrix) : Result<Matrix, string> =
        checkSquare "inverse" a
        let n = a.RowCount

        lu a
        |> Result.map (fun d ->
            let data: float[] = Array.zeroCreate (n * n)

            for j in 0 .. n - 1 do
                let e = Array.init n (fun i -> if i = j then 1.0 else 0.0)
                let x = LuDecomposition.solve d e

                for i in 0 .. n - 1 do
                    data[i * n + j] <- x[i]

            { RowCount = n
              ColumnCount = n
              Data = data })

    /// The determinant through `lu`: 0 when `lu` gives `Error` (a pivot within n·ε·max|aᵢⱼ| of 0), NaN for a
    /// non-finite entry, and 1 for a 0 × 0 matrix. Raises `ArgumentException` for a non-square matrix. Cost O(n³).
    let determinant (a: Matrix) : float =
        match lu a with
        | Ok d -> LuDecomposition.determinant d
        | Error _ -> 0.0

    /// The Cholesky factorisation A = L·Lᵀ, reading the lower triangle, every sum in increasing index order using only
    /// + − × ÷ and `sqrt`. Returns `Error "not symmetric …"` unless |aᵢⱼ − aⱼᵢ| ≤ 1e-12·max|aᵢⱼ| for every pair, and
    /// `Error "not positive definite …"` when a pivot dₖ ≤ n·ε·max|aᵢᵢ|, with ε = 2⁻⁵². Both thresholds are relative
    /// to the scale of the matrix. A 0 × 0 matrix gives `Ok`. A matrix holding a NaN or infinite entry gives `Ok` with
    /// no symmetry or definiteness check, and every number read through the result is NaN. Raises `ArgumentException`
    /// for a non-square matrix. Cost O(n³).
    let cholesky (a: Matrix) : Result<CholeskyDecomposition, string> =
        checkSquare "cholesky" a
        let n = a.RowCount
        let struct (largest, nonFinite) = scan a

        if nonFinite then
            Ok(
                { Order = n
                  Lower = Array.zeroCreate (n * n)
                  NonFinite = true }
                : CholeskyDecomposition
            )
        else
            let mutable asymmetric = false

            for i in 0 .. n - 1 do
                for j in 0 .. i - 1 do
                    if abs (a.Data[i * n + j] - a.Data[j * n + i]) > 1e-12 * largest then
                        asymmetric <- true

            if asymmetric then
                Error "cholesky: the matrix is not symmetric; some |aᵢⱼ − aⱼᵢ| exceeds 1e-12·max|aᵢⱼ|."
            else
                let mutable largestDiagonal = 0.0

                for i in 0 .. n - 1 do
                    if abs a.Data[i * n + i] > largestDiagonal then
                        largestDiagonal <- abs a.Data[i * n + i]

                let threshold = float n * epsilon * largestDiagonal
                let l: float[] = Array.zeroCreate (n * n)
                let mutable failedAt = -1
                let mutable j = 0

                while failedAt < 0 && j < n do
                    let mutable d = a.Data[j * n + j]

                    for k in 0 .. j - 1 do
                        d <- d - l[j * n + k] * l[j * n + k]

                    if d <= threshold then
                        failedAt <- j
                    else
                        let diagonal = sqrt d
                        l[j * n + j] <- diagonal

                        for i in j + 1 .. n - 1 do
                            let mutable sum = a.Data[i * n + j]

                            for k in 0 .. j - 1 do
                                sum <- sum - l[i * n + k] * l[j * n + k]

                            l[i * n + j] <- sum / diagonal

                        j <- j + 1

                if failedAt >= 0 then
                    Error(
                        sprintf
                            "cholesky: the matrix is not positive definite; the pivot in column %d is at most %g, the threshold n·ε·max|aᵢᵢ|."
                            failedAt
                            threshold
                    )
                else
                    Ok(
                        { Order = n
                          Lower = l
                          NonFinite = false }
                        : CholeskyDecomposition
                    )
