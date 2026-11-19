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
            testList "ToleranceTests.fs" [ Ranvier.Science.Tests.ToleranceTests.tests ]
            testList "UnitsTests.fs" [ Ranvier.Science.Tests.UnitsTests.tests ]
            testList "QuantityTests.fs" [ Ranvier.Science.Tests.QuantityTests.tests ]
            testList "GeneratorTests.fs" [ Ranvier.Science.Tests.GeneratorTests.tests ]
            testList "RunTests.fs" [ Ranvier.Science.Tests.RunTests.tests ]
            testList "ConfidenceSequenceTests.fs" [ Ranvier.Science.Tests.ConfidenceSequenceTests.tests ]
            testList "CrossTargetTests.fs" [ Ranvier.Science.Tests.CrossTargetTests.tests ]
        ]

Mocha.runTests all |> ignore
