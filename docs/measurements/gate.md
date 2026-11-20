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

The Plan T1 build time is a no-op build: nothing had changed since the last build, so it is not comparable with the
baseline's incremental build. The Fable and mocha rows are comparable in kind to the baseline's. The times are wall
clock from `time` around each command, which includes the process start.

The baseline row is the coordinator's, *measured here* on 2026-11-20 and recorded in the plan's Global Constraints.

Machine: Windows 11 Pro, `[System.Environment]::OSVersion` 10.0.26300.0, `[System.Environment]::ProcessorCount` 24
(logical processors); AMD Ryzen 9 9900X, 12 physical cores, .NET SDK 10.0.404 (*from documentation*:
`docs/measurements/aggregates.md`, run on the same machine on 2026-11-19; the Plan T1 row did not re-read the CPU).
