module Ranvier.Science.Tests.Fable.Main

open Fable.Mocha

// Every [<Tests>] value of tests/Ranvier.Science.Tests, in compile order, under the name of its file.
let all =
    testList
        "Ranvier.Science"
        [
            testList "Smoke.fs" [ Ranvier.Science.Tests.Smoke.tests ]
            testList "Probes.fs" [ Ranvier.Science.Tests.Probes.tests ]
            testList "SumTests.fs" [ Ranvier.Science.Tests.SumTests.tests ]
            testList "MomentsTests.fs" [ Ranvier.Science.Tests.MomentsTests.tests ]
            testList "AggregateTests.fs" [ Ranvier.Science.Tests.AggregateTests.tests ]
        ]

Mocha.runTests all |> ignore
