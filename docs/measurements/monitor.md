# Frame work of the sequential monitor in a hidden window and a headless browser

This file holds four runs of the script `frames` on each target, all **measured here** on 2026-11-19, at three commits
that time the same work. The tasks named are those of the plan that built the page
(`Ranvier/docs/superpowers/plans/2026-11-19-science-slice-2-monitor.md`).

| Run | Commit | Which run |
|---|---|---|
| 1 | `3d0b878` | Task 8's first run |
| 2 | `3d0b878` | the controller's run, after run 1 |
| 3 | `b440fa9` | the record of Task 8's fix round |
| 4 | `8d1cb0e` | the final gate of Task 9 |

From `3d0b878` to `8d1cb0e` the one code file that changes is `examples/Monitor/src/Script.fs`, and in it only the
rule of the script's last line (`b440fa9`, step 4 below) and a doc comment; the timed region, the frame counts, the
checks and the budget are the same (*from documentation*: the diff of the two commits). Each target's figures are
compared with the budget only; a native figure is never compared with a browser figure, and no block of frames is
compared with another.

## The budget

6.06 ms, the period of a 165 Hz display (*from documentation*: `AstroShowcase.Web/measure/README.md`, line 11, names
the 165 Hz display the earlier comparison ran on; 6.06 ms is 1000 / 165 rounded to two decimals). A figure is under the
budget when it is below 6.06 ms.

## What one frame is

The time of one frame is `platform.Now ()` read before and after (*from documentation*: `frameTime` in
`examples/Monitor/src/Script.fs`):

1. `graph.Run (fun () -> Monitor.frame monitor (1.0 / 60.0))`, which folds the items each channel's feed delivers for
   1/60 s at the page's rate, in one batch with the diagnostics; as the batch ends it runs the recomputation of the rows
   and the pooled row, the chart scene and the DOM writes;
2. one `getBoundingClientRect ()` on the page's root element (its outermost element, the parent of the table), which
   makes the engine compute style and layout (*general knowledge* for WebKit's and Blink's
   `Element::getBoundingClientRect`; the native head's call reaches `WebCore::Element::getBoundingClientRect` directly,
   *from documentation*: `ranvier_dom_element_get_bounding_client_rect` in `host/dom/RanvierDomGlue.cpp` of the
   `Ranvier.WebCore` clone).

Not timed, on both targets: paint, compositing and presentation. Each timed frame follows the previous one directly: the
native script steps no window between frames, and the browser script runs inside one animation-frame callback, so no
turn of the window's loop or of the browser's event loop runs between them. The window is hidden and Chrome is
headless. **No frame rate follows from these figures.**

`platform.Now ()` is `Stopwatch.Elapsed.TotalMilliseconds` in the window (`examples/Monitor/native/Program.fs`) and
`performance.now ()` in the browser (`examples/Monitor/web/Browser.fs`), each a monotonic clock in milliseconds.

## The script

`frames` in `examples/Monitor/src/Script.fs` runs on the page as mounted, with no frame run before the script: four
channels A to D at 1,000 observations a second each (*from documentation*: `setup` in `examples/Monitor/src/App.fs` adds
the four channels, and `Monitor.create` in `examples/Monitor/model/Monitor.fs` sets the rate). Every count in the steps
below is *from documentation*: `frames` in `Script.fs`.

1. **Timer.** 100,000 successive readings of `platform.Now ()`; the smallest non-zero difference between two successive
   readings is printed on the `frames | timer |` line.
2. **Default page.** 60 frames unrecorded, then 600 timed.
3. **Sweep.** For each rate of 2,000, 4,000 and so on, doubling, to 1,024,000 observations a second per channel: one
   `graph.Run` that writes the rate through `Rate.Set` and calls `Monitor.restart` (every channel starts again from its
   seed), then 20 frames unrecorded and 120 timed. The rate is written through the input, not through the rate field's
   parser, whose bound of 1,000,000 applies to a typed entry only (*from documentation*: `Monitor.setRate` in
   `examples/Monitor/model/Monitor.fs`).
4. **The highest rate.** The last line reports the highest swept rate such that it and every lower swept rate have both
   the mean and the 95th percentile under the budget. When 2,000 does not qualify, the line is
   `frames | highest rate under the budget | none | 120 frames`. At `3d0b878` (runs 1 and 2) the last line took the
   highest rate with both figures under the budget whether or not a lower rate was over it; for those two runs the rate
   below is read from their sweep lines by the rule of this step.

The 95th percentile is the element at index `ceil (0.95 * count) - 1` of the sorted frame times: the 570th of 600, and the
114th of 120 (*from documentation*: `meanAndP95` in `Script.fs`). The mean is the arithmetic mean of the same times.

After each block of frames, the script checks every channel: the items delivered in the block are within 1 of
rate × frames / 60, and the channel's fold holds exactly the items delivered since the block began (*from
documentation*: `measure` in `Script.fs`). The script exits 1 when the default page's mean or 95th percentile is at or
over the budget, or when a check fails. Every run passed every check on both targets (*measured here*): runs 1 to 3
print no `frames | failed:` line, and run 4 ended `script done 0`.

## Machine and versions

All four runs ran on this machine.

- Processor: `AMD Ryzen 9 9900X 12-Core Processor`, 12 cores, 24 logical processors (*measured here*:
  `Get-CimInstance Win32_Processor`, `Name`, `NumberOfCores`, `NumberOfLogicalProcessors`).
- Memory: 66,156,724,224 bytes (*measured here*: `Get-CimInstance Win32_ComputerSystem`, `TotalPhysicalMemory`).
- Operating system: Microsoft Windows 11 Pro 10.0.26200 (*measured here*: `Get-CimInstance Win32_OperatingSystem`).
- .NET: SDK 11.0.100 (*measured here*: `dotnet --version`). The native head targets `net10.0` and asks for
  `Microsoft.NETCore.App` 10.0.0 (*from documentation*:
  `examples/Monitor/native/bin/Release/net10.0/Monitor.Native.runtimeconfig.json`); the installed 10.0 runtimes are
  10.0.8, 10.0.12 and 10.0.15 (*measured here*: `dotnet --list-runtimes`). The default roll-forward runs the latest
  installed patch, 10.0.15 (*general knowledge*; not read from the process).
- Chrome: 157.0.8103.78 (*measured here*: the `Last Version` file Chrome wrote in its profile folder
  `examples/Monitor/web/.chrome-profile`, and the product version of
  `C:\Program Files\Google\Chrome\Application\chrome.exe`; the two agree, read in Task 8 and again after run 4).
  Node.js v26.7.0 runs `run.mjs` and `serve.mjs`, outside the timed region (*measured here*: `node --version`).
- Fickl 0.10.1 (Fable 5.27.0) built the browser head (*measured here*: the first line of the build's output).

Package versions of each head (*from documentation*: `docs/PROBES.md`, section B, which read them from each head's
`obj/project.assets.json`):

- Native: `Ranvier` 0.2.0, `Ranvier.View` and `Ranvier.View.Dom` 0.2.0, `Ranvier.View.WebCore` 0.2.0,
  `Ranvier.View.WebCore.Sdl` 0.2.1, `Ranvier.D5`, `Ranvier.D5.Charts` and `Ranvier.D5.View` 0.1.1. These runs are of
  the Release build, which has no `Ranvier.View.WebCore.Hmr`. The engine package beneath them is `Ranvier.WebCore`
  0.1.3, with `Ranvier.WebCore.Native.win-x64` 0.1.3 (*measured here*: the `libraries` of
  `examples/Monitor/native/obj/project.assets.json`).
- Browser: `Ranvier` 0.2.0, `Ranvier.Dom` 0.1.0, `Ranvier.View` and `Ranvier.View.Dom` 0.2.0, `Fable.Core` 5.3.0, and
  `Ranvier.Css`, `Ranvier.Css.Core` and `Ranvier.Css.Bindings` 0.1.1 (these three *measured here*: the `libraries` of
  `examples/Monitor/web/obj/project.assets.json`). `Ranvier.D5`, `Ranvier.D5.Charts` and `Ranvier.D5.View` 0.1.1 are
  compiled from the `Ranvier.D5` clone through the project reference, at the clone's commit `08e5642`. The clone's
  working tree has an uncommitted change to `src/Ranvier.D5/Ranvier.D5.fsproj` that adds two packaging items, `None`
  items with `Pack="true"` (*measured here*: `git -C ../Ranvier.D5 diff -- src/Ranvier.D5/Ranvier.D5.fsproj`,
  `1 file changed, 2 insertions(+)`). Section B of `docs/PROBES.md` records the clone's full state.

## Commands

From the repository root. Both heads were built before each run; nothing else built or ran from the session that made
a run while it ran.

```
dotnet build -c Release examples/Monitor/native
dotnet fickl examples/Monitor/web -e .fs.js -o examples/Monitor/web/dist -c Release

dotnet run -c Release --project examples/Monitor/native -- --script frames
node examples/Monitor/web/run.mjs frames
```

Runs 1 and 3 used the last two commands; run 2 used them with `--no-build` added to the native one. The record of run 4
gives its exit codes, its default-page figures and its last lines, not its commands.

## Results

Every figure below is *measured here*, the time of the frame defined above, read from each run's output: the default
page's mean and 95th percentile over 600 frames, and the highest rate under the budget by the rule of step 4. The sweep
lines are in the appendix.

### Native: a hidden window

| Run | Commit | Default page, mean | Default page, 95th percentile | Highest rate under the budget |
|---|---|---|---|---|
| 1 | `3d0b878` | 0.47 ms | 2.08 ms | 512,000 /s per channel |
| 2 | `3d0b878` | 0.47 ms | 1.96 ms | 512,000 /s per channel |
| 3 | `b440fa9` | 0.43 ms | 2.05 ms | 512,000 /s per channel |
| 4 | `8d1cb0e` | 0.44 ms | 2.07 ms | 256,000 /s per channel |

The timer's smallest step is 0.0001 ms in runs 1 to 3 (*measured here*: the `frames | timer |` line); the record of run 4
does not keep that line.

### Browser: headless Chrome

| Run | Commit | Default page, mean | Default page, 95th percentile | Highest rate under the budget |
|---|---|---|---|---|
| 1 | `3d0b878` | 0.72 ms | 5.70 ms | none (at 2,000 /s the 95th percentile is 6.80 ms) |
| 2 | `3d0b878` | 0.62 ms | 4.90 ms | 8,000 /s per channel |
| 3 | `b440fa9` | 0.59 ms | 4.90 ms | 8,000 /s per channel |
| 4 | `8d1cb0e` | 0.62 ms | 5.00 ms | 2,000 /s per channel |

Run 1 printed `4000 /s per channel` on its last line, by the rule of `3d0b878`; under the rule of step 4 it is `none`.

The timer's smallest step is 0.1 ms in runs 1 to 3 (*measured here*: the `frames | timer |` line; run 4's record does not
keep it). The page is served without cross-origin isolation, and Chrome coarsens `performance.now ()` for such a page
(*general knowledge*). Each frame time is the difference of two such readings, so it is a multiple of about 0.1 ms and
can be off by up to 0.1 ms (*general knowledge*, from the step); every 95th percentile the four runs printed ends in 0 in
its second decimal, as such a multiple does (*measured here*: the outputs).

### Against the budget

- **Default page.** Both figures are under 6.06 ms in four of four runs on each target (*measured here*: the tables
  above). The browser's margin under the budget, 6.06 ms less the run's 95th percentile, is 0.36 ms in run 1, 1.16 ms in
  runs 2 and 3, and 1.06 ms in run 4 (*measured here*, from the same figures).
- **The highest rate under the budget**, over the four runs: from 256,000 to 512,000 observations a second per channel
  on the native head, and from none to 8,000 on the browser (*measured here*: the tables above). The value of one run
  is not a capacity figure: it moved between runs at commits that time the same work.

## Limits of these figures

- The 95th percentile of 120 frames is the 114th of the 120 sorted frame times, so it rests on the slowest 7 frames of
  the block (*from documentation*: `meanAndP95` in `Script.fs`, index `ceil (0.95 * 120) - 1` = 113).
- The record is four runs per target. They give the ranges above; four runs are too few for a distribution, and what
  moves a figure from one run to the next was not measured.
- Each sweep block starts right after a restart. By reading `examples/Monitor/model/History.fs` (a sample when a
  channel's count reaches the next due count, which grows geometrically), `examples/Monitor/model/Monitor.fs` (`frame`
  delivers rate × 1/60 s of items per channel) and `examples/Monitor/model/Feed.fs` (`EmitAtRate`), and not by
  measurement: how often a frame appends a history sample depends on the frames since the restart and not on the rate
  (*from documentation*: those three files).

## The pinned scripts at `b440fa9`

Run after run 3, on the same builds (*measured here*). Each output is the same on both targets and equal, line for line,
to the output of the same script on the same target at commit `4adc36b` (*measured here*: a case-sensitive comparison,
line by line, with the outputs saved by those runs).

`dotnet run -c Release --project examples/Monitor/native -- --script table` and
`node examples/Monitor/web/run.mjs table`, exit code 0 on both:

```
table | A | 1,000 | 0.92 mK | 0.09 mK | [0.62, 1.22] mK | 10.1
table | B | 1,000 | 1.08 mK | 0.09 mK | [0.78, 1.38] mK | 11.8
table | All | 2,000 | 1.00 mK | 0.06 mK | (pooled, descriptive) | 
table | pinned text: equal | DOM text: equal
script done 0
```

`dotnet run -c Release --project examples/Monitor/native -- --script counts` and
`node examples/Monitor/web/run.mjs counts`, exit code 0 on both:

```
counts | scene builds over a frame with no new sample: +0 (frame 121)
counts | mark keys of two consecutive scenes: equal (70 keys)
counts | α 0.05 to 0.01: bounds +1, band +1, starts +0, means +0, delivered +0, same folded state, on 4 channels
counts | scene builds over α to 0.01: +1
counts | scene builds over α 0.05 to 0.05000001: +0
counts | add channel E: split +1, bounds +1, starts +0, same folded state, on 4 channels
counts | adding a channel adds 4 keys: band/E, mean/E, fixlo/E, fixhi/E
counts | publication on A: A means +1, bounds +1, settled +1; B C D E +0; re-folds +0
counts | all as expected
script done 0
```

## Appendix: the sweep lines

Each block is *measured here*, as the run printed it. Runs 1 to 3 are given in full; the record of run 4 keeps its
default-page figures and its last three lines only.

### Native

Run 1, `3d0b878`:

```
frames | timer | smallest non-zero step 0.000100 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.47 ms | p95 2.08 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 1.32 ms | p95 3.46 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.26 ms | p95 3.41 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 1.56 ms | p95 4.61 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 1.69 ms | p95 4.75 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 1.62 ms | p95 4.02 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 1.44 ms | p95 3.86 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 1.22 ms | p95 3.75 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 1.49 ms | p95 3.53 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 2.73 ms | p95 5.61 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 3.94 ms | p95 6.80 ms | 120 frames
frames | highest rate under the budget | 512000 /s per channel | mean 2.73 ms | p95 5.61 ms | 120 frames
script done 0
```

Run 2, `3d0b878`:

```
frames | timer | smallest non-zero step 0.000100 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.47 ms | p95 1.96 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 1.08 ms | p95 2.83 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.17 ms | p95 3.14 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 1.22 ms | p95 3.24 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 1.41 ms | p95 3.55 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 1.35 ms | p95 3.63 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 1.38 ms | p95 3.72 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 1.59 ms | p95 3.94 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 1.58 ms | p95 3.73 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 2.39 ms | p95 5.14 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 3.60 ms | p95 6.15 ms | 120 frames
frames | highest rate under the budget | 512000 /s per channel | mean 2.39 ms | p95 5.14 ms | 120 frames
script done 0
```

Run 3, `b440fa9`:

```
frames | timer | smallest non-zero step 0.000100 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.43 ms | p95 2.05 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 0.96 ms | p95 2.53 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.24 ms | p95 3.71 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 1.48 ms | p95 4.28 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 1.51 ms | p95 3.74 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 1.96 ms | p95 5.15 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 1.69 ms | p95 4.19 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 1.13 ms | p95 2.98 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 1.38 ms | p95 3.14 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 2.52 ms | p95 5.61 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 3.77 ms | p95 7.04 ms | 120 frames
frames | highest rate under the budget | 512000 /s per channel | mean 2.52 ms | p95 5.61 ms | 120 frames
script done 0
```

Run 4, `8d1cb0e` (default page: mean 0.44 ms, 95th percentile 2.07 ms):

```
frames | sweep | 1024000 /s per channel | mean 4.73 ms | p95 8.43 ms | 120 frames
frames | highest rate under the budget | 256000 /s per channel | mean 1.65 ms | p95 3.83 ms | 120 frames
script done 0
```

### Browser

Run 1, `3d0b878`:

```
frames | timer | smallest non-zero step 0.100000 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.72 ms | p95 5.70 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 1.85 ms | p95 6.80 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.80 ms | p95 5.50 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 2.02 ms | p95 6.20 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 2.42 ms | p95 7.40 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 2.68 ms | p95 8.70 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 3.40 ms | p95 10.90 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 5.00 ms | p95 13.20 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 5.83 ms | p95 15.60 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 7.29 ms | p95 16.10 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 10.72 ms | p95 20.70 ms | 120 frames
frames | highest rate under the budget | 4000 /s per channel | mean 1.80 ms | p95 5.50 ms | 120 frames
script done 0
```

Run 2, `3d0b878`:

```
frames | timer | smallest non-zero step 0.100000 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.62 ms | p95 4.90 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 1.56 ms | p95 4.90 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.71 ms | p95 5.40 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 1.88 ms | p95 6.00 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 2.13 ms | p95 7.00 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 2.38 ms | p95 6.80 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 3.18 ms | p95 9.20 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 3.61 ms | p95 9.80 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 4.45 ms | p95 12.00 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 5.89 ms | p95 13.80 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 7.74 ms | p95 15.30 ms | 120 frames
frames | highest rate under the budget | 8000 /s per channel | mean 1.88 ms | p95 6.00 ms | 120 frames
script done 0
```

Run 3, `b440fa9`:

```
frames | timer | smallest non-zero step 0.100000 ms over 100000 reads
frames | 4 channels at 1000 /s | mean 0.59 ms | p95 4.90 ms | 600 frames | budget 6.06 ms
frames | sweep | 2000 /s per channel | mean 1.45 ms | p95 4.30 ms | 120 frames
frames | sweep | 4000 /s per channel | mean 1.60 ms | p95 5.00 ms | 120 frames
frames | sweep | 8000 /s per channel | mean 1.81 ms | p95 5.50 ms | 120 frames
frames | sweep | 16000 /s per channel | mean 2.08 ms | p95 6.40 ms | 120 frames
frames | sweep | 32000 /s per channel | mean 2.40 ms | p95 6.90 ms | 120 frames
frames | sweep | 64000 /s per channel | mean 2.64 ms | p95 7.40 ms | 120 frames
frames | sweep | 128000 /s per channel | mean 3.58 ms | p95 10.50 ms | 120 frames
frames | sweep | 256000 /s per channel | mean 4.61 ms | p95 11.80 ms | 120 frames
frames | sweep | 512000 /s per channel | mean 6.30 ms | p95 14.10 ms | 120 frames
frames | sweep | 1024000 /s per channel | mean 9.18 ms | p95 18.20 ms | 120 frames
frames | highest rate under the budget | 8000 /s per channel | mean 1.81 ms | p95 5.50 ms | 120 frames
script done 0
```

Run 4, `8d1cb0e` (default page: mean 0.62 ms, 95th percentile 5.00 ms):

```
frames | sweep | 1024000 /s per channel | mean 9.91 ms | p95 19.10 ms | 120 frames
frames | highest rate under the budget | 2000 /s per channel | mean 1.72 ms | p95 5.50 ms | 120 frames
script done 0
```
