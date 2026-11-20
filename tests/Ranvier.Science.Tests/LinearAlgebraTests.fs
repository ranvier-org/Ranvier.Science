// Reference values: the closed forms and identities written in each test (hand-computed products, the 3-4-5 triangle, the Hilbert inverse formula, A.x = b in integers for x = [1; -2; 3]).
module Ranvier.Science.Tests.LinearAlgebraTests

open System
open Expecto
open Ranvier.Numerics

let private m23 = Matrix.ofRows [| [| 1.0; 2.0; 3.0 |]; [| 4.0; 5.0; 6.0 |] |]
let private m22a = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |] |]
let private m22b = Matrix.ofRows [| [| 5.0; 6.0 |]; [| 7.0; 8.0 |] |]
let private matA = Matrix.ofRows [| [| 4.0; -2.0; 1.0 |]; [| -2.0; 4.0; -2.0 |]; [| 1.0; -2.0; 4.0 |] |]
let private vecB = [| 11.0; -16.0; 17.0 |]
let private lineDesign = Matrix.init 4 2 (fun i j -> if j = 0 then 1.0 else float i)
let private lineY = [| 1.0; 3.0; 4.0; 4.0 |]

let private secondDifference (n: int) =
    Matrix.init n n (fun i j -> if i = j then 2.0 elif abs (i - j) = 1 then -1.0 else 0.0)

[<Tests>]
let tests =
    testList
        "LinearAlgebra"
        [
            test "vector arithmetic" {
                let a = [| 1.0; 2.0; 3.0 |]
                let b = [| 4.0; 5.0; 6.0 |]
                Expect.equal (Vector.dot a b) 32.0 "dot"
                Expect.equal (Vector.add a b) [| 5.0; 7.0; 9.0 |] "add"
                Expect.equal (Vector.subtract a b) [| -3.0; -3.0; -3.0 |] "subtract"
                Expect.equal (Vector.scale 2.0 a) [| 2.0; 4.0; 6.0 |] "scale"
            }

            test "norm is scaled against overflow and underflow" {
                Expect.equal (Vector.norm [| 3.0; 4.0 |]) 5.0 "3-4-5"
                expectClose 1e-15 0.0 5e200 (Vector.norm [| 3e200; 4e200 |]) "overflow range"
                expectClose 1e-15 0.0 5e-200 (Vector.norm [| 3e-200; 4e-200 |]) "underflow range"
                Expect.equal (Vector.norm [||]) 0.0 "empty"
                Expect.equal (Vector.norm [| 0.0; 0.0 |]) 0.0 "all zero"
                Expect.isTrue (Double.IsNaN(Vector.norm [| 1.0; nan; infinity |])) "NaN wins over infinity"
                Expect.equal (Vector.norm [| 1.0; -infinity |]) infinity "infinite component"
            }

            test "construction and reads" {
                let m = m23
                Expect.equal (Matrix.rowCount m) 2 "rowCount"
                Expect.equal (Matrix.columnCount m) 3 "columnCount"
                Expect.equal (Matrix.get m 1 2) 6.0 "get"
                Expect.equal (Matrix.row m 1) [| 4.0; 5.0; 6.0 |] "row"
                Expect.equal (Matrix.column m 2) [| 3.0; 6.0 |] "column"
                Expect.equal (Matrix.toRows m) [| [| 1.0; 2.0; 3.0 |]; [| 4.0; 5.0; 6.0 |] |] "toRows"

                Expect.equal
                    (Matrix.toRows (Matrix.identity 3))
                    [| [| 1.0; 0.0; 0.0 |]; [| 0.0; 1.0; 0.0 |]; [| 0.0; 0.0; 1.0 |] |]
                    "identity"

                Expect.equal (Matrix.toRows (Matrix.zero 2 3)) [| [| 0.0; 0.0; 0.0 |]; [| 0.0; 0.0; 0.0 |] |] "zero"

                Expect.equal
                    (Matrix.toRows (Matrix.diagonal [| 1.0; 2.0 |]))
                    [| [| 1.0; 0.0 |]; [| 0.0; 2.0 |] |]
                    "diagonal"

                Expect.equal
                    (Matrix.toRows (Matrix.init 2 2 (fun i j -> float (10 * i + j))))
                    [| [| 0.0; 1.0 |]; [| 10.0; 11.0 |] |]
                    "init"

                let empty = Matrix.ofRows [||]
                Expect.equal (Matrix.rowCount empty, Matrix.columnCount empty) (0, 0) "ofRows [||] is 0 x 0"
                let wide = Matrix.ofRows [| [||]; [||] |]
                Expect.equal (Matrix.rowCount wide, Matrix.columnCount wide) (2, 0) "two empty rows are 2 x 0"

                Expect.equal
                    (Matrix.rowCount (Matrix.zero 0 0), Matrix.columnCount (Matrix.identity 0))
                    (0, 0)
                    "size 0 is accepted"
            }

            test "arithmetic" {
                Expect.equal
                    (Matrix.toRows (Matrix.multiply m22a m22b))
                    [| [| 19.0; 22.0 |]; [| 43.0; 50.0 |] |]
                    "2 x 2 product"

                // [[1,2,3],[4,5,6]] times [[1,2],[3,4],[5,6]] = [[22,28],[49,64]]
                let m32 = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |]; [| 5.0; 6.0 |] |]

                Expect.equal
                    (Matrix.toRows (Matrix.multiply m23 m32))
                    [| [| 22.0; 28.0 |]; [| 49.0; 64.0 |] |]
                    "2 x 3 by 3 x 2"

                Expect.equal
                    (Matrix.toRows (Matrix.transpose m23))
                    [| [| 1.0; 4.0 |]; [| 2.0; 5.0 |]; [| 3.0; 6.0 |] |]
                    "transpose"

                Expect.equal (Matrix.toRows (Matrix.add m22a m22b)) [| [| 6.0; 8.0 |]; [| 10.0; 12.0 |] |] "add"

                Expect.equal
                    (Matrix.toRows (Matrix.subtract m22b m22a))
                    [| [| 4.0; 4.0 |]; [| 4.0; 4.0 |] |]
                    "subtract"

                Expect.equal (Matrix.toRows (Matrix.scale 2.0 m22a)) [| [| 2.0; 4.0 |]; [| 6.0; 8.0 |] |] "scale"
                Expect.equal (Matrix.multiplyVector m22a [| 1.0; 1.0 |]) [| 3.0; 7.0 |] "multiplyVector"

                let product = Matrix.multiply (Matrix.zero 3 0) (Matrix.zero 0 2)
                Expect.equal product (Matrix.zero 3 2) "n x 0 by 0 x p is the n x p zero matrix"
            }

            test "structural equality" {
                let a = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |] |]
                let b = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |] |]
                let c = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 5.0 |] |]
                Expect.isTrue (a = b) "equal arrays give equal matrices"
                Expect.isTrue (a <> c) "one changed entry gives unequal matrices"
                Expect.isTrue (Matrix.zero 2 3 <> Matrix.zero 3 2) "shape differs"
            }

            test "copies in and out" {
                let source = [| [| 1.0; 2.0; 3.0 |]; [| 4.0; 5.0; 6.0 |] |]
                let m = Matrix.ofRows source
                source[0][0] <- 99.0
                Expect.equal (Matrix.get m 0 0) 1.0 "ofRows copies its input"
                (Matrix.row m 1)[0] <- 99.0
                Expect.equal (Matrix.get m 1 0) 4.0 "row returns a copy"
                (Matrix.column m 2)[0] <- 99.0
                Expect.equal (Matrix.get m 0 2) 3.0 "column returns a copy"
                let rows = Matrix.toRows m
                rows[0][1] <- 99.0
                rows[1][1] <- 99.0
                Expect.equal (Matrix.get m 0 1) 2.0 "toRows copies, first row"
                Expect.equal (Matrix.get m 1 1) 5.0 "toRows copies, second row"
            }

            test "mismatches raise" {
                Expect.throwsT<ArgumentException>
                    (fun () -> Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0 |] |] |> ignore)
                    "ragged ofRows"

                Expect.throwsT<ArgumentException>
                    (fun () -> Vector.dot [| 1.0; 2.0 |] [| 1.0; 2.0; 3.0 |] |> ignore)
                    "dot lengths"

                Expect.throwsT<ArgumentException>
                    (fun () -> Vector.add [| 1.0; 2.0 |] [| 1.0; 2.0; 3.0 |] |> ignore)
                    "add lengths"

                Expect.throwsT<ArgumentException>
                    (fun () -> Vector.subtract [| 1.0; 2.0 |] [| 1.0; 2.0; 3.0 |] |> ignore)
                    "subtract lengths"

                Expect.throwsT<ArgumentException> (fun () -> Matrix.add m22a m23 |> ignore) "add shapes"
                Expect.throwsT<ArgumentException> (fun () -> Matrix.subtract m22a m23 |> ignore) "subtract shapes"
                Expect.throwsT<ArgumentException> (fun () -> Matrix.multiply m23 m23 |> ignore) "multiply shapes"

                Expect.throwsT<ArgumentException>
                    (fun () -> Matrix.multiplyVector m22a [| 1.0; 2.0; 3.0 |] |> ignore)
                    "multiplyVector length"

                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.get m23 2 0 |> ignore) "row out of range"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.get m23 0 3 |> ignore) "column out of range"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.row m23 -1 |> ignore) "row index"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.column m23 3 |> ignore) "column index"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.zero -1 2 |> ignore) "negative rows"
                Expect.throwsT<ArgumentOutOfRangeException> (fun () -> Matrix.identity -1 |> ignore) "negative identity"

                Expect.throwsT<ArgumentOutOfRangeException>
                    (fun () -> Matrix.init 2 -1 (fun _ _ -> 0.0) |> ignore)
                    "negative columns"
            }

            test "LU solves an integer system" {
                let x = [| 1.0; -2.0; 3.0 |]
                let d = Matrix.lu matA |> expectOk "lu"
                let viaLu = LuDecomposition.solve d vecB
                let direct = Matrix.solve matA vecB |> expectOk "solve"

                for i in 0 .. 2 do
                    expectClose 0.0 1e-14 x[i] viaLu[i] (sprintf "lu component %d" i)
                    expectClose 0.0 1e-14 x[i] direct[i] (sprintf "solve component %d" i)
            }

            test "determinant" {
                expectClose 1e-15 0.0 -2.0 (Matrix.determinant m22a) "2 x 2"
                expectClose 1e-14 0.0 36.0 (Matrix.determinant matA) "3 x 3, cofactor expansion 4*12 + 2*(-6) + 1*0"
            }

            test "the inverse of the Hilbert matrix of order 5" {
                let n = 5

                // C(a, b) in exact integers (a <= 10, so every intermediate fits in an int64).
                let binomial (a: int) (b: int) : int64 =
                    if b < 0 || b > a then
                        0L
                    else
                        let mutable r = 1L

                        for t in 1 .. b do
                            r <- r * int64 (a - b + t) / int64 t

                        r

                // Closed form (-1)^(i+j) (i+j+1) C(n+i, n-j-1) C(n+j, n-i-1) C(i+j, i)^2, 0-based.
                let closed i j =
                    let c = binomial (i + j) i
                    let magnitude = int64 (i + j + 1) * binomial (n + i) (n - j - 1) * binomial (n + j) (n - i - 1) * c * c
                    float (if (i + j) % 2 = 0 then magnitude else -magnitude)

                let h = Matrix.init n n (fun i j -> 1.0 / float (i + j + 1))
                let inv = Matrix.inverse h |> expectOk "inverse"

                for i in 0 .. n - 1 do
                    for j in 0 .. n - 1 do
                        expectClose 1e-9 0.0 (closed i j) (Matrix.get inv i j) (sprintf "entry %d,%d" i j)
            }

            test "inverse times the matrix is the identity" {
                let m =
                    Matrix.init 6 6 (fun i j -> float ((i * 7 + j * 3) % 11) - 5.0 + (if i = j then 20.0 else 0.0))

                let product = Matrix.multiply (Matrix.inverse m |> expectOk "inverse") m
                let id6 = Matrix.identity 6

                for i in 0 .. 5 do
                    for j in 0 .. 5 do
                        expectClose 0.0 1e-13 (Matrix.get id6 i j) (Matrix.get product i j) (sprintf "entry %d,%d" i j)
            }

            test "a singular matrix gives Error" {
                let s = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 2.0; 4.0 |] |]
                Matrix.lu s |> expectError "singular" "lu"
                Matrix.solve s [| 1.0; 2.0 |] |> expectError "singular" "solve"
                Matrix.inverse s |> expectError "singular" "inverse"
                Expect.equal (Matrix.determinant s) 0.0 "determinant"
                let z = Matrix.zero 3 3
                Matrix.lu z |> expectError "singular" "lu of zero"
                Matrix.solve z [| 1.0; 2.0; 3.0 |] |> expectError "singular" "solve of zero"
                Matrix.inverse z |> expectError "singular" "inverse of zero"
                Expect.equal (Matrix.determinant z) 0.0 "determinant of zero"
            }

            test "scale does not decide singularity" {
                let expected = [| 1.0; -2.0; 3.0 |]

                for factor in [ 1e-150; 1e150 ] do
                    let scaled = Matrix.scale factor matA
                    Matrix.lu scaled |> expectOk "lu" |> ignore
                    let x = Matrix.solve scaled (Vector.scale factor vecB) |> expectOk "solve"

                    for i in 0 .. 2 do
                        expectClose 0.0 1e-14 expected[i] x[i] (sprintf "factor %g component %d" factor i)
            }

            test "entries near the top of the doubles solve and invert" {
                // Closed form: [[s, 1.5s], [s, -1.5s]]·[1/2; 1/3] = [s; 0], the inverse is [[1/(2s), 1/(2s)],
                // [1/(3s), -1/(3s)]] and the determinant -3s², beyond the doubles.
                for s in [ 8e307; 9e307; 1e308 ] do
                    let a = Matrix.ofRows [| [| s; 1.5 * s |]; [| s; -1.5 * s |] |]
                    let x = Matrix.solve a [| s; 0.0 |] |> expectOk "solve"
                    expectClose 1e-15 0.0 0.5 x[0] (sprintf "scale %g, x0" s)
                    expectClose 1e-15 0.0 (1.0 / 3.0) x[1] (sprintf "scale %g, x1" s)
                    Expect.equal (Matrix.determinant a) -infinity (sprintf "scale %g, determinant" s)

                let s = 1e308
                let inv = Matrix.inverse (Matrix.ofRows [| [| s; 1.5 * s |]; [| s; -1.5 * s |] |]) |> expectOk "inverse"
                // The entries are subnormal, so the bound allows two spacings of the subnormal grid (2 · 2^-1074).
                let spacing = 2.0 * 4.9406564584124654e-324
                expectClose 1e-15 spacing (0.5 / s) (Matrix.get inv 0 0) "inverse 0 0"
                expectClose 1e-15 spacing (0.5 / s) (Matrix.get inv 0 1) "inverse 0 1"
                expectClose 1e-15 spacing (1.0 / 3.0 / s) (Matrix.get inv 1 0) "inverse 1 0"
                expectClose 1e-15 spacing (-1.0 / 3.0 / s) (Matrix.get inv 1 1) "inverse 1 1"
            }

            test "a decomposition is reusable" {
                let d = Matrix.lu matA |> expectOk "lu"
                let first = LuDecomposition.solve d vecB
                let snapshot = Array.copy first
                first[0] <- 99.0
                let second = LuDecomposition.solve d vecB
                Expect.equal second snapshot "the second solve is unaffected by mutating the first result"
                Expect.equal (LuDecomposition.solve d vecB) second "equal arrays"
            }

            test "shapes" {
                Expect.throwsT<ArgumentException> (fun () -> Matrix.lu (Matrix.zero 2 3) |> ignore) "lu non-square"
                Expect.throwsT<ArgumentException> (fun () -> Matrix.solve matA [| 1.0; 2.0 |] |> ignore) "solve length"

                Expect.throwsT<ArgumentException>
                    (fun () -> LuDecomposition.solve (Matrix.lu matA |> expectOk "lu") [| 1.0; 2.0 |] |> ignore)
                    "decomposition solve length"

                let empty = Matrix.lu (Matrix.zero 0 0) |> expectOk "0 x 0"
                Expect.equal (LuDecomposition.determinant empty) 1.0 "0 x 0 determinant"
                Expect.equal (LuDecomposition.solve empty [||]) [||] "0 x 0 solve"
            }

            test "a NaN or infinite entry gives NaN results" {
                let withEntry i j v =
                    Matrix.init 3 3 (fun r c -> if r = i && c = j then v else Matrix.get matA r c)

                let nanLu = Matrix.lu (withEntry 0 0 nan) |> expectOk "lu with NaN"
                let x = LuDecomposition.solve nanLu vecB
                Expect.equal x.Length 3 "length"
                Expect.isTrue (Array.forall Double.IsNaN x) "solve is all NaN"
                Expect.isTrue (Double.IsNaN(LuDecomposition.determinant nanLu)) "determinant is NaN"

                let nanCholesky = Matrix.cholesky (withEntry 1 0 nan) |> expectOk "cholesky with NaN"
                Expect.isTrue (Array.forall Double.IsNaN (CholeskyDecomposition.solve nanCholesky vecB)) "cholesky NaN"

                let infLu = Matrix.lu (withEntry 0 0 infinity) |> expectOk "lu with infinity"
                Expect.isTrue (Double.IsNaN(LuDecomposition.determinant infLu)) "infinite entry, determinant"
                Expect.isTrue (Double.IsNaN(Matrix.determinant (withEntry 0 0 infinity))) "Matrix.determinant"

                let inv = Matrix.inverse (withEntry 0 0 nan) |> expectOk "inverse with NaN"
                Expect.isTrue (Array.forall Double.IsNaN (Matrix.toRows inv |> Array.concat)) "inverse is all NaN"
            }

            test "Cholesky solves a positive definite system" {
                let d = Matrix.cholesky matA |> expectOk "cholesky"
                let x = CholeskyDecomposition.solve d vecB
                let expected = [| 1.0; -2.0; 3.0 |]

                for i in 0 .. 2 do
                    expectClose 0.0 1e-14 expected[i] x[i] (sprintf "component %d" i)

                Matrix.cholesky (Matrix.scale 1e-150 matA) |> expectOk "scaled" |> ignore
            }

            test "Cholesky rejects what it cannot factor" {
                Matrix.cholesky m22a |> expectError "not symmetric" "asymmetric"
                Matrix.cholesky (Matrix.ofRows [| [| 1.0; 2.0 |]; [| 2.0; 1.0 |] |]) |> expectError "not positive definite" "indefinite"
                Matrix.cholesky (Matrix.ofRows [| [| 1.0; 1.0 |]; [| 1.0; 1.0 |] |]) |> expectError "not positive definite" "semidefinite"
                Expect.throwsT<ArgumentException> (fun () -> Matrix.cholesky (Matrix.zero 2 3) |> ignore) "non-square"
            }

            test "QR fits a line" {
                // Normal equations: n = 4, Σt = 6, Σy = 12, Σt² = 14, Σty = 23, slope 1, intercept 1.5.
                let x = QrDecomposition.leastSquares (Matrix.qr lineDesign) lineY |> expectOk "fit"
                expectClose 0.0 1e-14 1.5 x[0] "intercept"
                expectClose 0.0 1e-14 1.0 x[1] "slope"
            }

            test "QR recovers a consistent solution" {
                let m = Matrix.init 6 3 (fun i j -> float ((i + 1) * (j + 2) % 7) - 3.0)
                let truth = [| 2.0; -1.0; 0.5 |]
                let b = Matrix.multiplyVector m truth
                let x = QrDecomposition.leastSquares (Matrix.qr m) b |> expectOk "fit"

                for i in 0 .. 2 do
                    expectClose 0.0 1e-13 truth[i] x[i] (sprintf "component %d" i)
            }

            test "a rank-deficient design gives Error" {
                let m = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 1.0; 2.0 |]; [| 1.0; 2.0 |] |]

                QrDecomposition.leastSquares (Matrix.qr m) [| 1.0; 2.0; 3.0 |]
                |> expectError "rank-deficient" "dependent columns"
            }

            test "scale does not decide rank" {
                for factor in [ 1e-150; 1e150 ] do
                    let d = Matrix.qr (Matrix.scale factor lineDesign)
                    let x = QrDecomposition.leastSquares d (Vector.scale factor lineY) |> expectOk "scaled"
                    expectClose 0.0 1e-14 1.5 x[0] (sprintf "factor %g intercept" factor)
                    expectClose 0.0 1e-14 1.0 x[1] (sprintf "factor %g slope" factor)
            }

            test "QR of designs with entries near the top of the doubles" {
                // Closed form: [[s, s], [s, -s]]·[1/2; 1/2] = [s; 0], with |R₀₀| = |R₁₁| = √2·s. The 4 x 2 design has
                // orthogonal columns and y = s·(1, 1, 1, 1) is its first column, so x = [1; 0]. The 3 x 2 design of
                // equal columns is rank-deficient at any scale.
                let s = 1e308
                let square = Matrix.qr (Matrix.ofRows [| [| s; s |]; [| s; -s |] |])
                let x = QrDecomposition.leastSquares square [| s; 0.0 |] |> expectOk "2 x 2"
                expectClose 1e-15 0.0 0.5 x[0] "2 x 2, x0"
                expectClose 1e-15 0.0 0.5 x[1] "2 x 2, x1"
                let r = QrDecomposition.r square
                expectClose 1e-15 0.0 (sqrt 2.0 * s) (abs (Matrix.get r 0 0)) "2 x 2, R 0 0"
                expectClose 1e-15 0.0 (sqrt 2.0 * s) (abs (Matrix.get r 1 1)) "2 x 2, R 1 1"

                let tall = Matrix.ofRows [| [| s; 1e307 |]; [| s; -1e307 |]; [| s; 1e307 |]; [| s; -1e307 |] |]
                let x4 = QrDecomposition.leastSquares (Matrix.qr tall) [| s; s; s; s |] |> expectOk "4 x 2"
                expectClose 1e-15 0.0 1.0 x4[0] "4 x 2, x0"
                expectClose 0.0 1e-15 0.0 x4[1] "4 x 2, x1"

                let equal = Matrix.ofRows [| [| s; s |]; [| s; s |]; [| s; s |] |]
                QrDecomposition.leastSquares (Matrix.qr equal) [| s; s; s |] |> expectError "rank-deficient" "equal columns"
            }

            test "QR shapes" {
                Expect.throwsT<ArgumentException>
                    (fun () -> Matrix.qr (Matrix.zero 2 3) |> ignore)
                    "qr with fewer rows than columns"

                Expect.throwsT<ArgumentException>
                    (fun () -> QrDecomposition.leastSquares (Matrix.qr lineDesign) [| 1.0; 2.0 |] |> ignore)
                    "leastSquares length"
            }

            test "R reproduces the Gram matrix" {
                let r = QrDecomposition.r (Matrix.qr lineDesign)
                Expect.equal (Matrix.rowCount r) 2 "rows"
                Expect.equal (Matrix.columnCount r) 2 "columns"
                Expect.equal (Matrix.get r 1 0) 0.0 "zero below the diagonal"
                let gram = Matrix.multiply (Matrix.transpose r) r
                let expected = [| [| 4.0; 6.0 |]; [| 6.0; 14.0 |] |]

                for i in 0 .. 1 do
                    for j in 0 .. 1 do
                        expectClose 0.0 1e-13 (expected[i][j]) (Matrix.get gram i j) (sprintf "gram %d %d" i j)
            }

            test "QR of a non-finite design gives NaN" {
                let withEntry v =
                    Matrix.init 4 2 (fun i j -> if i = 1 && j = 1 then v else Matrix.get lineDesign i j)

                for v in [ infinity; nan ] do
                    let d = Matrix.qr (withEntry v)
                    let x = QrDecomposition.leastSquares d lineY |> expectOk "leastSquares"
                    Expect.equal x.Length 2 "length"
                    Expect.isTrue (Array.forall Double.IsNaN x) (sprintf "coefficients with %g" v)
                    let r = QrDecomposition.r d
                    Expect.equal (Matrix.rowCount r, Matrix.columnCount r) (2, 2) "R shape"
                    Expect.isTrue (Array.forall Double.IsNaN (Matrix.toRows r |> Array.concat)) (sprintf "R with %g" v)

                Expect.throwsT<ArgumentException>
                    (fun () -> QrDecomposition.leastSquares (Matrix.qr (withEntry infinity)) [| 1.0 |] |> ignore)
                    "the length is checked first"
            }

            test "eigen of a 2 x 2" {
                let e = Matrix.symmetricEigen (Matrix.ofRows [| [| 2.0; 1.0 |]; [| 1.0; 2.0 |] |]) |> expectOk "eigen"
                expectClose 0.0 1e-15 1.0 e.Values[0] "value 0"
                expectClose 0.0 1e-15 3.0 e.Values[1] "value 1"
                let s = 1.0 / sqrt 2.0
                let v0 = Matrix.column e.Vectors 0
                let v1 = Matrix.column e.Vectors 1
                expectClose 0.0 1e-15 1.0 (abs (Vector.dot v0 [| s; -s |])) "column 0"
                expectClose 0.0 1e-15 1.0 (abs (Vector.dot v1 [| s; s |])) "column 1"
                Expect.isTrue (v0[0] > 0.0 && v1[0] > 0.0) "the first component is the largest and positive"
            }

            test "eigen of the second-difference matrix" {
                let n = 10
                let a = secondDifference n
                let e = Matrix.symmetricEigen a |> expectOk "eigen"

                for k in 1 .. n do
                    let lambda = 2.0 - 2.0 * cos (float k * Math.PI / 11.0)
                    expectClose 0.0 1e-14 lambda e.Values[k - 1] (sprintf "value %d" k)
                    let v = Matrix.column e.Vectors (k - 1)
                    let residual = Vector.subtract (Matrix.multiplyVector a v) (Vector.scale e.Values[k - 1] v)
                    Expect.isTrue (Array.max (Array.map abs residual) <= 1e-13) (sprintf "residual %d" k)

                let gram = Matrix.multiply (Matrix.transpose e.Vectors) e.Vectors

                for i in 0 .. n - 1 do
                    for j in 0 .. n - 1 do
                        expectClose 0.0 1e-14 (if i = j then 1.0 else 0.0) (Matrix.get gram i j) (sprintf "gram %d %d" i j)
            }

            test "eigen of the second-difference matrix at extreme scales" {
                let n = 10

                for factor in [ 1e-160; 1e160 ] do
                    let e = Matrix.symmetricEigen (Matrix.scale factor (secondDifference n)) |> expectOk "eigen"

                    for k in 1 .. n do
                        let lambda = factor * (2.0 - 2.0 * cos (float k * Math.PI / 11.0))
                        expectClose 1e-13 0.0 lambda e.Values[k - 1] (sprintf "factor %g value %d" factor k)
            }

            test "eigen near the top of the doubles" {
                // Closed form: [[s, s], [s, -s]] has the eigenvalues ±√2·s.
                let s = 1e308
                let e = Matrix.symmetricEigen (Matrix.ofRows [| [| s; s |]; [| s; -s |] |]) |> expectOk "eigen"
                expectClose 1e-15 0.0 (-(sqrt 2.0 * s)) e.Values[0] "value 0"
                expectClose 1e-15 0.0 (sqrt 2.0 * s) e.Values[1] "value 1"
            }

            test "eigen edge cases" {
                let d = Matrix.symmetricEigen (Matrix.diagonal [| 3.0; 1.0; 2.0 |]) |> expectOk "diagonal"
                Expect.equal d.Values [| 1.0; 2.0; 3.0 |] "diagonal values"

                Expect.equal
                    d.Vectors
                    (Matrix.ofRows [| [| 0.0; 0.0; 1.0 |]; [| 1.0; 0.0; 0.0 |]; [| 0.0; 1.0; 0.0 |] |])
                    "diagonal vectors"

                let one = Matrix.symmetricEigen (Matrix.ofRows [| [| 5.0 |] |]) |> expectOk "1 x 1"
                Expect.equal one.Values [| 5.0 |] "1 x 1 value"
                Expect.equal one.Vectors (Matrix.ofRows [| [| 1.0 |] |]) "1 x 1 vector"

                let empty = Matrix.symmetricEigen (Matrix.zero 0 0) |> expectOk "0 x 0"
                Expect.equal empty.Values [||] "empty values"
                Matrix.symmetricEigen m22a |> expectError "not symmetric" "asymmetric"

                Expect.throwsT<ArgumentException>
                    (fun () -> Matrix.symmetricEigen (Matrix.zero 2 3) |> ignore)
                    "non-square"

                for v in [ nan; infinity ] do
                    let bad = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; v |] |]
                    let e = Matrix.symmetricEigen bad |> expectOk "non-finite"
                    Expect.isTrue (Array.forall Double.IsNaN e.Values) (sprintf "values with %g" v)

                    Expect.isTrue
                        (Array.forall Double.IsNaN (Matrix.toRows e.Vectors |> Array.concat))
                        (sprintf "vectors with %g" v)
            }
        ]
