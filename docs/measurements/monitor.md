# Frame work of the sequential monitor in a hidden window and a headless browser

This file holds one run of the script `frames` on each target, both **measured here** on 2026-11-19 at the commit that
last changes this file. Each target's figures are compared with the budget only; a native figure is never compared with
a browser figure, and no block of frames is compared with another.

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
   `frames | highest rate under the budget | none | 120 frames`.

The 95th percentile is the element at index `ceil (0.95 * count) - 1` of the sorted frame times: the 570th of 600, and the
114th of 120 (*from documentation*: `meanAndP95` in `Script.fs`). The mean is the arithmetic mean of the same times.

After each block of frames, the script checks every channel: the items delivered in the block are within 1 of
rate × frames / 60, and the channel's fold holds exactly the items delivered since the block began (*from
documentation*: `measure` in `Script.fs`). Both runs passed every check, printing no `frames | failed:` line (*measured
here*). The script exits 1 when the default page's mean or 95th percentile is at or over the budget, or when a check
fails.

## Machine and versions

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
  `C:\Program Files\Google\Chrome\Application\chrome.exe`; the two agree). Node.js v26.7.0 runs `run.mjs` and
  `serve.mjs`, outside the timed region (*measured here*: `node --version`).
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

From the repository root. Both heads were built before the runs; nothing else built or ran from this task while each
`frames` run ran.

```
dotnet build -c Release examples/Monitor/native
dotnet fickl examples/Monitor/web -e .fs.js -o examples/Monitor/web/dist -c Release

dotnet run -c Release --project examples/Monitor/native -- --script frames
node examples/Monitor/web/run.mjs frames
```

## Native: a hidden window

Full output, exit code 0 (*measured here*, by the third command; every figure is the time of the frame defined above):

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

Against the budget (each figure from the output above, *measured here*):

- Default page: mean 0.43 ms and 95th percentile 2.05 ms, both under 6.06 ms.
- Sweep: every rate from 2,000 to 512,000 a second per channel has both figures under the budget; at 1,024,000 the
  95th percentile, 7.04 ms, is over it. The highest rate that every lower rate also meets is 512,000 a second per
  channel, with a mean of 2.52 ms and a 95th percentile of 5.61 ms.

The timer's smallest step is 0.0001 ms (*measured here*: the `frames | timer |` line).

## Browser: headless Chrome

Full output, exit code 0 (*measured here*, by the fourth command; every figure is the time of the frame defined above):

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

Against the budget (each figure from the output above, *measured here*):

- Default page: mean 0.59 ms and 95th percentile 4.90 ms, both under 6.06 ms.
- Sweep: the 95th percentile is under the budget at 2,000, 4,000 and 8,000 a second per channel (5.50 ms at 8,000) and
  over it from 16,000 up (6.40 ms at 16,000). The mean is under the budget up to 256,000 (4.61 ms) and over it from
  512,000 (6.30 ms). The highest rate that every lower rate also meets is 8,000 a second per channel, with a mean of
  1.81 ms and a 95th percentile of 5.50 ms.

The timer's smallest step is 0.1 ms (*measured here*: the `frames | timer |` line). The page is served without
cross-origin isolation, and Chrome coarsens `performance.now ()` for such a page (*general knowledge*). Each frame time
is the difference of two such readings, so it is a multiple of about 0.1 ms and can be off by up to 0.1 ms (*general
knowledge*, from the step); every 95th percentile above ends in 0 in its second decimal, as such a multiple does
(*measured here*: the output above).

## Limits of these figures

- The 95th percentile of 120 frames is the 114th of the 120 sorted frame times, so it rests on the slowest 7 frames of
  the block (*from documentation*: `meanAndP95` in `Script.fs`, index `ceil (0.95 * 120) - 1` = 113).
- The record is one run per target. How far any figure moves from one run to the next was not measured.
- Each sweep block starts right after a restart. By reading `examples/Monitor/model/History.fs` (a sample when a
  channel's count reaches the next due count, which grows geometrically), `examples/Monitor/model/Monitor.fs` (`frame`
  delivers rate × 1/60 s of items per channel) and `examples/Monitor/model/Feed.fs` (`EmitAtRate`), and not by
  measurement: how often a frame appends a history sample depends on the frames since the restart and not on the rate
  (*from documentation*: those three files).

## The pinned scripts at the same commit

Run after the two `frames` runs, on the same builds (*measured here*). Each output is the same on both targets and
equal, line for line, to the output of the same script on the same target at commit `4adc36b` (*measured here*: a
case-sensitive comparison, line by line, with the outputs saved by those runs).

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
