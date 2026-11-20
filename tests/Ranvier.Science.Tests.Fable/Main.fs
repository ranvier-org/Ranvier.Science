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
            testList "UncertainTests.fs" [ Ranvier.Science.Tests.UncertainTests.tests ]
            testList "FormulaEntryTests.fs" [ Ranvier.Science.Tests.FormulaEntryTests.tests ]
            testList "LifetimeTests.fs" [ Ranvier.Science.Tests.LifetimeTests.tests ]
            testList "CatalogueTests.fs" [ Ranvier.Science.Tests.CatalogueTests.tests ]
            testList "UnitTextTests.fs" [ Ranvier.Science.Tests.UnitTextTests.tests ]
            testList "NotationTests.fs" [ Ranvier.Science.Tests.NotationTests.tests ]
            testList "ConstantsTests.fs" [ Ranvier.Science.Tests.ConstantsTests.tests ]
            testList "OrderStatisticTests.fs" [ Ranvier.Science.Tests.OrderStatisticTests.tests ]
            testList "MonteCarloTests.fs" [ Ranvier.Science.Tests.MonteCarloTests.tests ]
            testList "AggregateTests.fs" [ Ranvier.Science.Tests.AggregateTests.tests ]
            testList "ToleranceTests.fs" [ Ranvier.Science.Tests.ToleranceTests.tests ]
            testList "UnitsTests.fs" [ Ranvier.Science.Tests.UnitsTests.tests ]
            testList "QuantityTests.fs" [ Ranvier.Science.Tests.QuantityTests.tests ]
            testList "FormulaTests.fs" [ Ranvier.Science.Tests.FormulaTests.tests ]
            testList "OpenOrderTests.fs" [ Ranvier.Science.Tests.OpenOrderTests.tests ]
            testList "GeneratorTests.fs" [ Ranvier.Science.Tests.GeneratorTests.tests ]
            testList "RunTests.fs" [ Ranvier.Science.Tests.RunTests.tests ]
            testList "ConfidenceSequenceTests.fs" [ Ranvier.Science.Tests.ConfidenceSequenceTests.tests ]
            testList "SpecialTests.fs" [ Ranvier.Science.Tests.SpecialTests.tests ]
            testList "LinearAlgebraTests.fs" [ Ranvier.Science.Tests.LinearAlgebraTests.tests ]
            testList "RootsTests.fs" [ Ranvier.Science.Tests.RootsTests.tests ]
            testList "LeastSquaresTests.fs" [ Ranvier.Science.Tests.LeastSquaresTests.tests ]
            testList "CalculusTests.fs" [ Ranvier.Science.Tests.CalculusTests.tests ]
            testList "OdeTests.fs" [ Ranvier.Science.Tests.OdeTests.tests ]
            testList "InterpolateTests.fs" [ Ranvier.Science.Tests.InterpolateTests.tests ]
            testList "FourierTests.fs" [ Ranvier.Science.Tests.FourierTests.tests ]
            testList "ComomentsTests.fs" [ Ranvier.Science.Tests.ComomentsTests.tests ]
            testList "HigherMomentsTests.fs" [ Ranvier.Science.Tests.HigherMomentsTests.tests ]
            testList "WeightedMomentsTests.fs" [ Ranvier.Science.Tests.WeightedMomentsTests.tests ]
            testList "HistogramTests.fs" [ Ranvier.Science.Tests.HistogramTests.tests ]
            testList "CrossTargetTests.fs" [ Ranvier.Science.Tests.CrossTargetTests.tests ]
            testList "MonitorPartsTests.fs" [ Ranvier.Science.Tests.MonitorPartsTests.tests ]
            testList "MonitorTests.fs" [ Ranvier.Science.Tests.MonitorTests.tests ]
        ]

Mocha.runTests all |> ignore
