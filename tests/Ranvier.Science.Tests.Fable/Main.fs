module Ranvier.Science.Tests.Fable.Main

open Fable.Mocha

// Every [<Tests>] value of tests/Ranvier.Science.Tests, in compile order, under the name of its file.
let all =
    testList
        "Ranvier.Science"
        [
            testList "Smoke.fs" [ Ranvier.Science.Tests.Smoke.tests ]
        ]

Mocha.runTests all |> ignore
