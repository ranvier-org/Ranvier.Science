# Counts, state comparison and frame times of the tracked pages

The two pages of `examples/AstroTracked` that run on tracked quantities, Atmospheric Drag Decay and Attitude Dynamics,
against the original pages they were ported from, in one copy of the app. Every figure below is **measured here** on
2026-11-20, from the commands in the next section, unless it carries another label. The build is the commit before this
one (`74d7eb2`) plus the one added line of `tracked` in `examples/AstroTracked/Harness.fs`.

## Machine and runtime

- CPU: AMD Ryzen 9 9900X 12-Core Processor (read with `(Get-CimInstance Win32_Processor).Name`).
- GPU adapter, as the runs print it: NVIDIA GeForce RTX 5080 on D3D12.
- Runtime and build, as the runs print them: Release build, .NET 10.0.15, win-x64.
- Window: 1540 x 900 CSS px, hidden; the 3D view is 745 x 578 CSS px (the runs' header lines).
- Operating system: Windows 11 Pro 10.0.26200 (*from documentation*: the session's environment record).

## Commands

Both run from the repository root after `dotnet build -c Release examples/AstroTracked/AstroShowcase.fsproj`, and both
exited 0:

```
dotnet run -c Release --no-build --project examples/AstroTracked/AstroShowcase.fsproj -- --tracked
dotnet run -c Release --no-build --project examples/AstroTracked/AstroShowcase.fsproj -- --bench
```

`--bench` ran with its defaults, 120 timed frames after 20 warm-up frames. The two runs are separate runs: the counts
and the control-change medians below come from the `--tracked` run, the frame means from the `--bench` run, and a figure
of one run is never compared with a figure of the other.

## The `--tracked` run

Every line the run printed after its header lines:

```
decay counts | original | Initial altitude 112 | Ballistic coeff. 112 | Solar activity 112 | reset after two changes 112
decay counts | tracked | Initial altitude 112 | Ballistic coeff. 1 | Solar activity 1 | reset after two changes 1
decay typed | a control typed to its current value | original 0 | tracked 0
decay values | 7 settings at 2 times | status text equal | lifetime bits equal
attitude | 1911 frames | restarts: original 7, tracked 7 | all states bit-identical
attitude | steps 5 and 6 | restarts: original 0, tracked 0
attitude | away for 30 frames | advances while away: original 0, tracked 0
decay change | median ms over 21 changes of the ballistic coefficient | original 13.90 | tracked 12.43
tracked | second creation: no error
PASS  tracked pages
window: still hidden at the end of the run (SDL_GetWindowFlags). The run has no call that shows it.
```

What the lines say, each comparison within this run:

- `decay counts`: the original page calls `Decay.decay` 112 times for a change of any of the three controls and for the
  reset. The tracked page calls it 112 times for a change of the initial altitude and once for a change of the
  ballistic coefficient, of the solar activity and for the reset. (`Probe.Decays` counts the calls across one change and
  the frame after it.)
- `decay typed`: typing the ballistic coefficient as the text its field shows calls `Decay.decay` 0 times on both pages.
  This shows the guard in `App.typed` and not the tracked inputs' own handling of an equal write: `App.typed` writes
  nothing when the typed value equals the control's current value (*from documentation*: `App.fs`, lines 222 to 224,
  read by the controller). The attitude script's step 6, which also writes an equal value, shows the same guard.
- `decay values`: the status text of the two pages is equal, and so are the bits of the lifetime, at 7 settings and 2
  clock times each.
- `attitude`: over a script of 1911 frames the two pages' states are bit-identical after every frame, with 7 restarts of
  the run on each page; in steps 5 and 6 neither page restarts; with the page away for 30 frames neither advances.
- `decay change`: the median over 21 changes of the ballistic coefficient, alternately up and down between 80 and 90,
  each timed with `Stopwatch.GetTimestamp` from before the change to the end of the frame after it, from `goTo` and
  `resetModel` on each page. It is a measurement and asserts nothing. The 21 spans include the window's graph work
  and the draw of the 3D view for that frame. The pair is from one run and was not repeated, so it is not known how much
  it varies between runs.

## The `--bench` run

What `--bench` times (*from documentation*: `examples/AstroTracked/Harness.fs`, the `bench` function and the lines it
prints first): the app's work for one frame. The clock is advanced by 1/60 s, which draws the 3D view once, and the
stopwatch runs from that write until the reactive graph has settled. The turn of the window's loop between two frames
(events, layout, paint, presenting the hidden window) is outside the stopwatch, and no frame rate is reported. A span that
did not draw exactly once fails the run.

The four lines of the two decay pages and the two attitude pages, as printed:

```
page 15 | frame: mean 6.89 ms, median 2.95, p95 23.61 | parts, mean ms: build 0.05 + render 6.26 [pass 5.33] + overlay 0.10 + flush 0.48 | 120 frames, one draw each | Atmospheric Drag Decay
page 18 | frame: mean 7.29 ms, median 3.08, p95 24.20 | parts, mean ms: build 0.02 + render 6.85 [pass 5.99] + overlay 0.11 + flush 0.31 | 120 frames, one draw each | Attitude Dynamics
page 30 | frame: mean 6.44 ms, median 2.96, p95 19.22 | parts, mean ms: build 0.05 + render 5.84 [pass 4.98] + overlay 0.10 + flush 0.44 | 120 frames, one draw each | Atmospheric Drag Decay, tracked
page 31 | frame: mean 6.21 ms, median 2.69, p95 19.51 | parts, mean ms: build 0.02 + render 5.74 [pass 4.92] + overlay 0.09 + flush 0.36 | 120 frames, one draw each | Attitude Dynamics, tracked
```

Within this run, the frame means in ms are 6.89 (Decay, original), 6.44 (Decay, tracked), 7.29 (Attitude, original) and
6.21 (Attitude, tracked); the medians are 2.95, 2.96, 3.08 and 2.69, and the 95th percentiles 23.61, 19.22, 24.20 and
19.51. Each figure is the time of one thing, the frame described above, for that page with its default inputs and the
clock advancing; no control changes during the timed frames. The render part (5.74 to 6.85 ms of the means) is most of
each frame; the decay pages' build, overlay and flush parts together are under 0.7 ms. The run is one run of 120 frames
a page, the pages ran one after another, and it was not repeated, so the differences of 0.45 ms (Decay) and 1.08 ms
(Attitude) between an original page and its tracked page in the means are reported and not interpreted.

## Pictures

The 3D view of the three pages, compared in the region x 220-958, y 106-678 of the whole-window captures that `--shots`
writes (built at commit `71b2c15`; the captures are not committed). The whole-window pictures are not bit-reproducible
in the 3D view between captures. The script `pixel-diff.mjs` (kept in the scratch folder, not in the repository) counts
the pixels of a region that differ between two PNG files and the largest per-channel difference. It printed:

```
4374 of 423447 px differ, max 1/255 | shots-task3/page-15.png vs shots-ctl-full/page-15.png
5578 of 423447 px differ, max 2/255 | shots-ctl-full/page-30.png vs shots-ctl-from30/page-30.png
815 of 423447 px differ, max 1/255 | shots-task3/page-15.png vs shots-task3/page-30.png
```

- the original decay page against itself, in two runs of the same build: 4374 of 423447 pixels differ, at most 1/255;
- the tracked decay page, in a full run against a run that starts at it: 5578 of 423447 pixels differ, at most 2/255;
- the original against the tracked page within one run: 815 of 423447 pixels differ, at most 1/255.

The first count (815) is a within-run figure: two pages of one run. The other two (4374 and 5578) are between-run
figures: one page in two runs. They are not directly comparable, and no conclusion is drawn by setting one against the
other. All three differ by at most 2/255 in any channel, which says only that every difference is tiny.
