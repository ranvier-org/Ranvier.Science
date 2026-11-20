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
