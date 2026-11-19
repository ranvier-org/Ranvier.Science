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
  `6 tests run ... 6 passed, 0 ignored, 0 failed, 0 errored` (E3 added in a later commit; the first run had 5).
- JavaScript: `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release`, then
  `npx mocha dist/tests/Main.fs.js`, reporting `8 passing` (the six probes and the two smoke tests).

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

Each test is a `createStreamFold` over a `ManualStream<int>` and a `createEffect` that reads `tally.TryValue` and
counts its runs in a mutable counter. A push on the graph thread outside a batch flushes the effect before `Push`
returns, on both targets (the core's test `an effect sees one run per item on the graph thread, and one per batch`
relies on the same). Every zero item is followed by a non-zero item, and the count is read after each push.

### E1: the same instance

Test: `a fold that returns its previous state notifies no reader`. State is a record `{ Total: int }`; the fold is
`fun s x -> if x = 0 then s else { s with Total = s.Total + x }`. Items 1, 0, 2.

- .NET (*measured here*): runs 1 at creation, 2 after item 1, 2 after item 0, 3 after item 2; final `Total = 3`.
- JavaScript (*measured here*): the same counts.

### E2: an equal rebuilt struct

Test: `a fold that returns a new equal struct`. State is a `[<Struct>]` record `{ Total: int }`, rebuilt on every
item by `fun s x -> { Total = s.Total + x }`. Items 1, 0.

- .NET (*measured here*): runs 1, 2, then 2 after item 0. The zero item does not re-run the effect.
- JavaScript (*measured here*): runs 1, 2, then 3 after item 0. The zero item re-runs the effect. Fickl compiles the
  struct record to a class (`class StructTally extends Record` in the emitted `Probes.fs.js`).
- Final `Total = 1` on both targets. The test asserts 3 under `FABLE_COMPILER` and 2 otherwise.

### E3: an equal rebuilt record

Test: `a fold that returns a new equal record notifies its reader`. State is the record `{ Total: int }` of E1,
rebuilt on every item by `fun s x -> { Total = s.Total + x }`. Items 1, 0, 2.

- .NET (*measured here*): runs 1, 2, 3 after item 0, 4 after item 2; final `Total = 3`. The zero item re-runs the
  effect, although the new record is equal to the old one under F# structural equality.
- JavaScript (*measured here*): the same counts.

### The rule

A fold publication notifies readers when the status changes or `equal.Equals (value, state)` is false, where
`equal` is `graph.Options.Equality.Comparer<'S>()` (*from documentation*: `Ranvier/src/Ranvier/Streams.fs`,
`Publish`). The default `Equality` is `JsIdentityPolicy` (`Types.fs:539`), whose comparer comes from
`ComparerResolution.resolve` (*from documentation*: `Ranvier/src/Ranvier/Types.fs`):

- .NET (`Types.fs:290-307`): `float` and `float32` compare by IEEE value; other value types (structs) and `string`
  compare with `EqualityComparer<'T>.Default`, by value; every other type, records and classes included, compares by
  reference (`ReferenceComparer`).
- Fable (`Types.fs:279-289`): `===` at every type. Primitives compare by value; everything else compares by
  reference, including structs, struct tuples, `DateTime`, `decimal` and `KeyValuePair`.

| State returned by the fold | .NET | JavaScript | Known by |
|---|---|---|---|
| the same instance it received (record) | no notification | no notification | E1, *measured here* |
| an equal rebuilt struct | no notification | notification | E2, *measured here* |
| an equal rebuilt record | notification | notification | E3, *measured here* |

Consequence: on both targets, a stream fold skips an unchanged item only when it returns the state it received, for
a record state and for a struct state alike. A rebuilt record notifies readers on both targets; a rebuilt equal
struct is skipped on .NET only. `createStreamFold` in 0.2.0 has no comparer overload; the only other lever is the
graph's `GraphOptions.Equality`. This bounds the fold functions of Tasks 9 and 10 and of the later plans'
stream-fed quantities.

## Workarounds

### Raising from `subtract` to request a re-fold

What the slice does: `Aggregate.sumBy` and `Aggregate.momentsBy` (`src/Ranvier.Numerics/Aggregate.fs`) keep their
state, an accumulator with the row count and the removals since the last re-fold, in a `Projection.foldGroupBy`.
Their `subtract` raises `ArithmeticException` when the state it is given is not finite, or, under
`RefoldAfterRemovals`, when the removals including this one reach the row count before it. `Refolds` counts these
raises in a counter the closure owns.

Core lines it relies on (*from documentation*: `Ranvier/src/Ranvier/Combinators.fs`, package 0.2.0 source):

- `ProjectionFold.Step` (1058-1064) runs a step in `try ... with _ -> full <- true`; `Sync` then calls `Recompute`
  (1294-1295), so one raise gives one re-fold in the same read.
- A row edit is one step, `add (subtract s old) v` (1121), with `old` the row's cached value; `foldGroupBy` (1856)
  folds over `map projection upstream`, so `old` is the cached projected value and `projection` runs once per edit
  (*measured here*: test `a row edit makes one subtract and one add`, 1 call on .NET and JavaScript).
- A removed row is `Step (fun s -> subtract s old)` (1146), except the last valued row: `valued = 0` sets the state
  to `zero` without calling `subtract` (1142). `removing the last row returns zero` therefore needs no re-fold.
- `Recompute` (1242-1254) folds `add` from `zero` over the cached row values in `Keys` order (`lastKeys`). A keyed
  collection's `Keys` are in insertion order, and `AddOrUpdate` of an existing key keeps its position
  (*from documentation*: `Ranvier/src/Ranvier/Collections.fs`, `KeyedCollection.AddOrUpdate`). With keys inserted
  `0 .. N-1`, the re-fold order is key order: the two "from-scratch bits" tests compare against a fold in key order
  and pass on both targets (*measured here*).
- The core's own `sumBy` uses the same device: `subtractFinite` (1860-1865) raises `ArithmeticException` on an
  infinite or NaN difference.

What a direct request would replace: a step result or a callback in `foldGroupBy` that asks for a re-fold (for
example a `subtract` returning `ValueNone`) would replace the raise, the exception cost per re-fold on .NET, and
the reliance on `Step` treating every exception alike. A re-fold the core starts for its own reasons (a moved read
of the fold functions, 1267-1268) is not counted in `Refolds`.
