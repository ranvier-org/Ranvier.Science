# Frame work of the sequential monitor in a hidden window and a headless browser

This file holds one run of the script `frames` on each target, both **measured here** on 2026-11-19 at the commit that
adds this file. Each target's figures are compared with the budget only; a native figure is never compared with a
browser figure.

## The budget

6.06 ms, the period of a 165 Hz display (*from documentation*: `AstroShowcase.Web/measure/README.md`, line 11, names
the 165 Hz display the earlier comparison ran on; 1000 / 165 = 6.0606 ms). A frame is under the budget when its figure
is below 6.06 ms.

## What one frame is

The time of one frame is `platform.Now ()` read before and after:

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

`examples/Monitor/src/Script.fs`, `frames`, on the page as mounted (four channels A to D at 1,000 observations a second
each, no frame run before the script):

1. **Timer.** 100,000 successive readings of `platform.Now ()`; the smallest non-zero difference between two successive
   readings is printed on the `frames | timer |` line.
2. **Default page.** 60 frames unrecorded, then 600 timed.
3. **Sweep.** For each rate of 2,000, 4,000 and so on, doubling, to 1,024,000 observations a second per channel: one
   `graph.Run` that writes the rate through `Rate.Set` and calls `Monitor.restart` (every channel starts again from its
   seed), then 20 frames unrecorded and 120 timed. The rate is written through the input, not through the rate field's
   parser, whose bound of 1,000,000 applies to a typed entry only.
4. The last line reports the highest swept rate at which both the mean and the 95th percentile are under the budget.

The 95th percentile is the element at index `ceil (0.95 * count) - 1` of the sorted frame times: the 570th of 600, and the
114th of 120. The mean is the arithmetic mean of the same times.

After each block of frames, the script checks every channel: the items delivered in the block are within 1 of
rate × frames / 60, and the channel's fold holds exactly the items delivered since the block began. Both runs passed
every check (no `frames | failed:` line). The script exits 1 when the default page's mean or 95th percentile is at or
over the budget, or when a check fails.

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
  compiled from the `Ranvier.D5` clone through the project reference.

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

Against the budget:

- Default page: mean 0.47 ms and 95th percentile 2.08 ms, both under 6.06 ms.
- Sweep: every rate from 2,000 to 512,000 a second per channel is under the budget on both figures; at 1,024,000 the
  95th percentile, 6.80 ms, is over it. The highest rate under the budget is 512,000 a second per channel, with a mean of
  2.73 ms and a 95th percentile of 5.61 ms.

The timer's smallest step is 0.0001 ms, so its resolution is finer than every figure here by a factor of over 4,000.

## Browser: headless Chrome

Full output, exit code 0 (*measured here*, by the fourth command; every figure is the time of the frame defined above):

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

Against the budget:

- Default page: mean 0.72 ms and 95th percentile 5.70 ms, both under 6.06 ms. The 95th percentile is 0.36 ms under it.
- Sweep: the mean is under the budget up to 256,000 a second per channel (5.83 ms) and over it from 512,000 (7.29 ms).
  The 95th percentile is under it only at 4,000 (5.50 ms); it is over it at 2,000 (6.80 ms) and from 8,000 up
  (6.20 ms and above). The highest rate under the budget on both figures is therefore 4,000 a second per channel, while
  the rates either side of it, 2,000 and 8,000, are over the budget on the 95th percentile. This rests on one run; how
  much the 95th percentile of 120 frames moves from run to run was not measured.

The timer's smallest step is 0.1 ms: the page is served without cross-origin isolation, and Chrome coarsens
`performance.now ()` for such a page (*general knowledge*; the step is *measured here*). Each frame time is the
difference of two such readings, so it is a multiple of about 0.1 ms and can be off by up to 0.1 ms; every 95th
percentile above ends in 0 in its second decimal, as such a multiple does. A mean over 600 or 120 frames is finer than
the step.

## The same script, the two blocks of frames

Within each run, the sweep's figures at 2,000 a second are above the default page's at 1,000 a second (native mean 1.32
against 0.47 ms; browser 1.85 against 0.72 ms). The two blocks differ in more than the rate: the default page's timed
frames are frames 61 to 660 after the mount, and the sweep's are frames 21 to 140 after a restart. A candidate, not
tested here: the history takes a sample each time a channel's count grows by 5% (`examples/Monitor/model/History.fs`),
so the scene is rebuilt more often while the counts are small, early after a restart.

## The pinned scripts at the same commit

Run after the two `frames` runs, on the same builds (*measured here*). Each output is the same on both targets and
equal, line for line, to the output of the same script on the same target at commit `4adc36b`, the commit before this
file's (*measured here*: a case-sensitive comparison, line by line, with the outputs saved by those runs).

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
