# Probes

Each section records one probe: what was run and what it showed. Every figure carries a label.

## Core package through Fickl

Question: does Fickl compile a project that reaches `Ranvier` 0.2.0 as a NuGet package?

Result: yes. The Fable test project reaches the package through `Ranvier.Numerics`
(`PackageReference Include="Ranvier" Version="0.2.0"`), so the `ProjectReference` fallback to the core clone was
not needed.

Command (*measured here*), from the repository root:

```
dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release
npx mocha dist/tests/Main.fs.js
```

Relevant output: `Project and references (26 source files) parsed`, `Fable compilation finished`, then
`2 passing` under Mocha, including `a memo follows its signal`.

## A

Not yet run.

## B

Not yet run.

## C

Not yet run.

## D

Not yet run.

## E

Not yet run.
