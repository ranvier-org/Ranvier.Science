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

## Commands for probes A, C, D and E

The probes are the list `"Probes"` in `tests/Ranvier.Science.Tests/Probes.fs`, one test per probe. Every outcome
below is *measured here* on 2026-11-19, with `Ranvier` 0.2.0 and Fickl 0.10.1 (Fable 5.27.0), from the repository
root:

- .NET: `dotnet run -c Release --project tests/Ranvier.Science.Tests -- --filter-test-list "Probes"`, reporting
  `5 tests run ... 5 passed, 0 ignored, 0 failed, 0 errored`.
- JavaScript: `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then
  `npx mocha dist/tests/Main.fs.js`, reporting `7 passing` (the five probes and the two smoke tests).

Each test asserts the observed behaviour. Where the targets differ, the test asserts each target's behaviour behind
`#if FABLE_COMPILER`.

## A

Question: do F# units of measure compile and erase on both targets?

Test: `units of measure compile and erase`. With `[<Measure>] type m` and `[<Measure>] type s`, it asserts
`(6.0<m> / 2.0<s>) = 3.0<m/s>`, `float 3.0<m/s> = 3.0`, `LanguagePrimitives.FloatWithMeasure<m> 2.0 = 2.0<m>`, and
that `let twice (x: float<'u>) = x + x` gives `4.0<m>` for `2.0<m>`.

- .NET (*measured here*): passes.
- JavaScript (*measured here*): Fickl compiles the file without an error or a warning, and the test passes. The
  emitted `dist/tests/Ranvier.Science.Tests/Probes.fs.js` holds plain numbers: the assertions compile to
  `Expect_equal(6 / 2, 3, ...)`, `Expect_equal(3, 3, ...)`, `Expect_equal(2, 2, ...)` and
  `Expect_equal(twice(2), 4, ...)`, and `twice` compiles to `function twice(x) { return x + x; }`. A measure leaves
  no wrapper, conversion call or type tag in the output.

Consequence: Task 9 can type quantities with F# units of measure on both targets. Its fallback for a compiler that
rejects measures is not needed.

## B

Not yet run. Probe B (an empty View page through Fickl) is the first task of Plan 2.

## C

Question: can a projection row factory create a stream fold that the row owns, so that removing the key stops the
stream?

Test: `a stream fold can be created in a projection row factory`. A keyed collection holds key 1. A
`createProjectionWith` over the collection's keys has a factory that creates a `ManualStream<int>`, calls
`createStreamFold 0 (+) start` (where `start` records its `CancellationToken`) and returns `fun () -> total.Value`.

- .NET (*measured here*): passes. The first `rows.Get 1` raises `NotReadyException` and starts the row's stream
  (one start). After pushing 1, 2 and 3 into the row's stream, `rows.Get 1` is `6`. The token is not cancelled while
  the key is present. After `keys.Remove 1` and a read of `rows.Keys` (empty), both the token passed to `start` and
  the token passed to the source's `Run` report `IsCancellationRequested = true`.
- JavaScript (*measured here*): passes with the same assertions. Every step is synchronous: pushes on the graph
  thread publish at once, and the removal cancels the tokens during the pass, with no microtask wait.

Consequence: Plan 2 can create each channel's stream fold in its projection row factory. The per-channel
`createRoot` fallback is not needed.

## D

Question: does a memo that reads a pending stream report pending without raising, and which member reports a memo's
status?

Test: `a memo reading a pending stream is pending` (a `testCaseAsync`, because the failure step waits for the
stream's task to settle). `memo = createMemo (fun _ -> stream.Value + 1)` over `createStream` of a `ManualStream<int>`
with no item.

- .NET (*measured here*): passes. Observed values:
  - while pending: `memo.TryValue = Pending`, without raising; then `memo.Status = Status.Pending`;
    `memo.Peek = 0`;
  - after one item 4: `memo.TryValue = Ready 5`; `memo.Status = Status.None`;
  - after the stream fails with `exn "boom"` (and `settled` on its start): `memo.TryValue = Failed error` with
    `error.Message = "boom"`; `memo.Status = Status.Error`; `memo.Peek = 5`.
- JavaScript (*measured here*): the same, except `memo.Peek` before the first value. The hoped-for assertion
  `memo.Peek = 0` failed under Mocha with `Expected: 0`, `Actual: <null>`. The test now asserts
  `isNull (box memo.Peek)` under `FABLE_COMPILER` and `memo.Peek = 0` on .NET.

The member that reports a memo's status without raising is `Memo<'T>.TryValue : Reading<'T>`, a tracked read that
brings the memo current first. `Reading<'T>` (`Ranvier/src/Ranvier/Types.fs`, a `[<Struct>]` union) has the cases
`Ready of value: 'T`, `Pending` and `Failed of error: exn`; its doc comment states that `error` is non-null on both
targets. `Memo<'T>.Status : Status` returns the stored `[<Flags>]` enum of the last computation and runs nothing
(`Ranvier/src/Ranvier/Core.fs`, `member _.Status = status`), so it can lag a source that changed since; the test
reads it only after `TryValue`. `Memo<'T>.Peek` is
the cached value without recomputing; before the first value it is `0` for `int` on .NET and `null` under Fable.

Consequence: Task 10 builds `Status: unit -> QuantityStatus` on `TryValue`, mapping `Ready _` to `Ready`, `Pending`
to `Pending` and `Failed e` to `Failed e`. It cannot use `Peek` as a value before the first `Ready`: the result
differs by target.

## E

Question: does an effect that reads a stream fold re-run when the fold returns state equal to the state it was given?
The fold publishes under the graph's equality policy (`Ranvier/src/Ranvier/Streams.fs`, `Publish`: a publication
notifies readers when the status changes or `equal.Equals (value, state)` is false; `equal` is
`graph.Options.Equality.Comparer<'S>()`). The default policy is in `Ranvier/src/Ranvier/Types.fs:279-307`.

Both tests count effect runs with a mutable counter in a `createEffect` that reads `tally.TryValue`. Pushes on the
graph thread outside a batch flush the effect before `Push` returns, on both targets, as the core's test
`an effect sees one run per item on the graph thread, and one per batch` relies on.

### E1

Test: `a fold that returns its previous state notifies no reader`. State is a record `{ Total: int }`; the fold is
`fun s x -> if x = 0 then s else { s with Total = s.Total + x }`. Items 1, 0, 2.

- .NET (*measured here*): passes. Runs: 1 at creation, 2 after item 1, still 2 after item 0, 3 after item 2;
  final `Total = 3`.
- JavaScript (*measured here*): passes with the same counts.

Returning the very state the fold received notifies no reader on either target.

### E2

Test: `a fold that returns a new equal struct`. State is a `[<Struct>]` record `{ Total: int }`, rebuilt on every
item by `fun s x -> { Total = s.Total + x }`. Items 1, 0.

- .NET (*measured here*): runs 1 at creation, 2 after item 1, still 2 after item 0. A struct compares by value
  (`EqualityComparer<'T>.Default`), so the equal struct notifies no reader.
- JavaScript (*measured here*): runs 1 at creation, 2 after item 1, 3 after item 0. Fickl compiles the struct record
  to a class (`class StructTally extends Record`), and the policy compares it by reference (`===`), so each rebuilt
  struct is a change.
- Final `Total = 1` on both targets.

The test asserts 3 under `FABLE_COMPILER` and 2 otherwise.

Consequence: a stream fold that should skip an unchanged item returns the state it received, not an equal copy, in
every state type that compiles to an object under Fable (records and struct records here; the doc comment in
`Types.fs:279-307` adds struct tuples, `DateTime`, `decimal` and `KeyValuePair`). The core 0.2.0 has no
comparer overload of `createStreamFold`; the comparer comes from the graph's `GraphOptions.Equality`. This bounds the
fold functions of Tasks 9 and 10 and of the later plans' stream-fed quantities.

## Workarounds
