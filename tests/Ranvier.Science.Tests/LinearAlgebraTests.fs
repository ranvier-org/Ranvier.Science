// Reference values: the closed forms and identities written in each test (hand-computed products, the 3-4-5 triangle).
module Ranvier.Science.Tests.LinearAlgebraTests

open System
open Expecto
open Ranvier.Numerics

let private m23 = Matrix.ofRows [| [| 1.0; 2.0; 3.0 |]; [| 4.0; 5.0; 6.0 |] |]
let private m22a = Matrix.ofRows [| [| 1.0; 2.0 |]; [| 3.0; 4.0 |] |]
let private m22b = Matrix.ofRows [| [| 5.0; 6.0 |]; [| 7.0; 8.0 |] |]

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
        ]
