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
        ]
