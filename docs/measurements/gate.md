# The gate's figures at the end of each plan

This is the one gate record: each plan's last task appends a dated row, and no other file holds gate figures. Every
figure is *measured here*, once, from a warm state (an up-to-date tree, the packages restored, the tools run before), by
the commands below, run from the repository root.

The gate:

1. `dotnet build -c Release`
2. `dotnet run -c Release --project tests/Ranvier.Science.Tests`
3. `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`
4. `npx mocha dist/tests/Main.fs.js`

| Date | Plan | Build | .NET suite | Fable compile | Mocha |
|---|---|---|---|---|---|
| 2026-11-20 | baseline, before Plan T1 | 9 s incremental, 0 warnings, 0 errors | 2 s, 174 tests | 4 s warm | 5 s, 171 tests |
| 2026-11-20 | Plan T1 (HEAD b36fa33, docs only on top) | 1.1 s, no-op (up to date), 0 warnings, 0 errors | 3.0 s, 298 tests, 0 failed | 1.9 s with `Main.fs` touched, so Fable recompiled (1149 ms by its own count), 0 warnings; 0.5 s on the first run, where Fable skipped compilation because the generated files were up to date | 5.3 s, 293 tests |
| 2026-11-21 | After Plan T2 (Task 20, commit bf55e1c) | 3.5 s incremental (`Generator.fs` touched), 1.0 s no-op, 0 warnings, 0 errors | 3.0 s, 559 tests, 0 failed | 2.5 s with `Main.fs` touched (1791 ms by Fable's own count), 2.4 s on a second run in which Fable also reported compiling (1696 ms); 0 warnings | 13.4 s, 554 tests, 0 failing |
| 2026-11-21 | After Plan T3 (the release commit, 0.2.0, on 506198b) | 14.7 s incremental (the version change rebuilt every project), 1.1 s no-op, 0 warnings, 0 errors | 3.5 s, 806 tests, 0 failed | 6.4 s with `Main.fs` touched (3394 ms by Fable's own count), 0 warnings | 7.8 s, 801 tests, 0 failing |

The Plan T1 build time is a no-op build: nothing had changed since the last build, so it is not comparable with the
baseline's incremental build. The Fable and mocha rows are comparable in kind to the baseline's. The times are wall
clock around each command, process start included: from `time` for the baseline and Plan T1 rows, and from
PowerShell `Measure-Command` for the After Plan T2 row. The two timers are different tools, so a figure is compared
only with one taken by the same timer.

The After Plan T2 row was *measured here* on 2026-11-21 with PowerShell `Measure-Command` around each command, after one
warm-up build, from the repository root; test counts are those each suite printed. Compare only the same command on this
machine: the build row against the Plan T1 no-op build (1.1 s then, 1.0 s now); the .NET suite (3.0 s, 298 tests then; 3.0 s,
559 now); the Fable compile with `Main.fs` touched (1.9 s then, 2.5 s now); mocha (5.3 s, 293 tests then; 13.4 s, 554 now).
Mocha before this task's change took 13.0 s by `time` (542 tests, *measured here*), so the 8 s rise since Plan T1 is not
this task's. Its cause is Plan T2 Task 14's Fourier test `forward matches the direct sum`, whose O(n²) direct sum over
every bin at n = 4095 and 4096 took 8,065 ms of 12,363 ms of test time in one mocha run with the JSON reporter at
bf55e1c (*measured here*, the Plan T2 final review, 2026-11-21). The final wave trimmed that test to every bin for
n ≤ 1024 and a sample of bins at 4095 and 4096 (`tests/Ranvier.Science.Tests/FourierTests.fs`).

The After Plan T3 row was *measured here* on 2026-11-21 with PowerShell `Measure-Command` around each command, run once, from the repository root, with the commands above; the build was timed twice (the first after the version change, the second on an unchanged tree). Test counts are those each suite printed. The row records the release commit; the final review's fix wave adds tests after it and appends its own row. Compare only with the After Plan T2 row, which used the same timer: the .NET suite 3.0 s, 559 tests then and 3.5 s, 806 now; the Fable compile 2.5 s then and 6.4 s now; mocha 13.4 s, 554 tests then and 7.8 s, 801 now (the Fourier test trim, above, is the likely cause of the fall, not checked).

The baseline row is the coordinator's, *measured here* on 2026-11-20 and recorded in the plan's Global Constraints.

Machine: Windows 11 Pro, `[System.Environment]::OSVersion` 10.0.26300.0, `[System.Environment]::ProcessorCount` 24
(logical processors); AMD Ryzen 9 9900X, 12 physical cores, .NET SDK 10.0.404 (*from documentation*:
`docs/measurements/aggregates.md`, run on the same machine on 2026-11-19; the Plan T1 row did not re-read the CPU).
