/// Scripted runs: each drives the mounted page and reports what it sees, one line at a time.
module SequentialMonitor.Script

open Ranvier
open Ranvier.D5
open Ranvier.Dom
open Ranvier.Numerics
open SequentialMonitor.App

/// The expectations of one script: each failed one is reported with its expected and observed value.
type private Expectations(report: string -> unit, script: string) =
    let mutable failed = 0

    member _.Equal (what: string) (expected: 'a) (observed: 'a) : bool =
        if expected = observed then
            true
        else
            failed <- failed + 1
            report $"{script} | failed: {what}: expected {expected}, observed {observed}"
            false

    member _.Failed = failed

let private pageOf (props: Props) = props.Session.Page :?> Page

let private inGraph (props: Props) (body: unit -> 'a) : 'a = props.Session.Graph.Run body

let private line (cells: string[]) = String.concat " | " cells

/// The elements of the table that show a channel or the pooled row, in document order.
let private tableRows (document: Ranvier.Dom.Document) : Node list =
    let nodes = document.querySelectorAll "tr[data-row]"
    [ for i in 0 .. int nodes.length - 1 -> nodes.item (uint32 i) ]

/// Rebuilds the page with two channels on the recorded stream and compares the table with the pinned text.
let private table (props: Props) =
    let report = props.Platform.Report
    let expect = Expectations(report, "table")
    let page = pageOf props
    let m = page.Monitor
    props.Session.Auto <- false

    inGraph props (fun () ->
        for r in Monitor.rows m do
            Monitor.removeChannel m r.Key

        Monitor.addChannel m |> ignore
        Monitor.addChannel m |> ignore)

    inGraph props (fun () ->
        batch (fun () ->
            (m.Rows.Get "A").Feed.Push Recorded.stream
            (m.Rows.Get "B").Feed.Push(Recorded.stream |> Array.map (fun x -> 2.0 - x)))

        m.FormulaText.Value <- "(mean - target) / se")

    let cells = inGraph props (fun () -> [ for r in Monitor.rows m -> r.Cells() ] @ [ Monitor.pooledCells m ])

    for row in cells do
        report $"table | {line row}"

    let pinned = expect.Equal "the table text" Recorded.tableLines (cells |> List.map line)
    let shown = tableRows props.Platform.Document |> List.map (fun node -> node.textContent)
    let dom =
        expect.Equal "the number of table rows" cells.Length shown.Length
        && expect.Equal "the table rows' text" (cells |> List.map (String.concat "")) shown
    let verdict ok = if ok then "equal" else "different"
    report $"table | pinned text: {verdict pinned} | DOM text: {verdict dom}"
    if expect.Failed = 0 then 0 else 1

/// The number of history samples of each channel, in channel order.
let private sampleCounts (props: Props) (m: Monitor) =
    inGraph props (fun () -> Monitor.rows m |> Array.map (fun r -> (History.samples r.Observations.Peek.History).Length))

let private markKeys (props: Props) (page: Page) =
    inGraph props (fun () -> page.Scene.Value |> Scene.marks |> List.map Mark.key)

/// The reactivity counts of the page as drawn: tests 3, 4 and 5 of the graph, the scene's builds and its mark keys.
let private counts (props: Props) =
    let report = props.Platform.Report
    let expect = Expectations(report, "counts")
    let page = pageOf props
    let m = page.Monitor
    props.Session.Auto <- false
    let frame () = inGraph props (fun () -> Monitor.frame m (1.0 / 60.0))

    for _ in 1..120 do
        frame ()

    // A frame that leaves every row's sample count unchanged rebuilds no scene.
    let mutable found = false
    let mutable tries = 0

    while not found && tries < 600 do
        let samples, builds = sampleCounts props m, page.SceneBuilds
        frame ()
        tries <- tries + 1

        if sampleCounts props m = samples then
            found <- true
            let delta = page.SceneBuilds - builds
            expect.Equal "scene builds over a frame with no new sample" 0 delta |> ignore
            report $"counts | scene builds over a frame with no new sample: +{delta} (frame {120 + tries})"

    expect.Equal "a frame with no new sample within 600 frames" true found |> ignore

    // Two consecutive scenes: the frame before a rebuild and the rebuilt one.
    let mutable rebuilt = false
    tries <- 0

    while not rebuilt && tries < 600 do
        let keys, builds = markKeys props page, page.SceneBuilds
        frame ()
        tries <- tries + 1

        if page.SceneBuilds > builds then
            rebuilt <- true
            let next = markKeys props page
            expect.Equal "the mark keys of two consecutive scenes" keys next |> ignore
            let verdict = if keys = next then "equal" else "different"
            report $"counts | mark keys of two consecutive scenes: {verdict} ({next.Length} keys)"

    expect.Equal "a frame that rebuilds the scene within 600 frames" true rebuilt |> ignore

    // Test 3: changing α folds nothing and recomputes each channel's bounds and band once.
    let all = inGraph props (fun () -> Monitor.rows m)
    let before = all |> Array.map (fun r -> { r.Counts with Starts = r.Counts.Starts }, r.Feed.Delivered, r.Observations.Peek)
    let builds = page.SceneBuilds
    expect.Equal "α 0.01 accepted" (Result.Ok()) (inGraph props (fun () -> Monitor.setAlpha m "0.01")) |> ignore

    for r, (c, delivered, peek) in Array.zip all before do
        expect.Equal (r.Key + ": bounds") (c.Bounds + 1) r.Counts.Bounds |> ignore
        expect.Equal (r.Key + ": band") (c.Bands + 1) r.Counts.Bands |> ignore
        expect.Equal (r.Key + ": starts") c.Starts r.Counts.Starts |> ignore
        expect.Equal (r.Key + ": means") c.Means r.Counts.Means |> ignore
        expect.Equal (r.Key + ": delivered") delivered r.Feed.Delivered |> ignore
        expect.Equal (r.Key + ": the same folded state") true (obj.ReferenceEquals(peek, r.Observations.Peek)) |> ignore

    report $"counts | α 0.05 to 0.01: bounds +1, band +1, starts +0, means +0, delivered +0, same folded state, on {all.Length} channels"
    let alphaBuilds = page.SceneBuilds - builds
    expect.Equal "scene builds over α to 0.01" 1 alphaBuilds |> ignore
    report $"counts | scene builds over α to 0.01: +{alphaBuilds}"

    expect.Equal "α 0.05 accepted" (Result.Ok()) (inGraph props (fun () -> Monitor.setAlpha m "0.05")) |> ignore
    let builds = page.SceneBuilds
    expect.Equal "α 0.05000001 accepted" (Result.Ok()) (inGraph props (fun () -> Monitor.setAlpha m "0.05000001")) |> ignore
    let smallBuilds = page.SceneBuilds - builds
    expect.Equal "scene builds over α 0.05 to 0.05000001" 0 smallBuilds |> ignore
    report $"counts | scene builds over α 0.05 to 0.05000001: +{smallBuilds}"

    // Test 4: adding a channel recomputes the split and re-runs no fold.
    let existing = inGraph props (fun () -> Monitor.rows m)
    let before = existing |> Array.map (fun r -> { r.Counts with Starts = r.Counts.Starts }, r.Observations.Peek)
    let splits = m.AlphaSplits
    let keys = markKeys props page
    let added = inGraph props (fun () -> Monitor.addChannel m)
    expect.Equal "the new key" "E" added |> ignore
    expect.Equal "splits" (splits + 1) m.AlphaSplits |> ignore

    for r, (c, peek) in Array.zip existing before do
        expect.Equal (r.Key + ": bounds") (c.Bounds + 1) r.Counts.Bounds |> ignore
        expect.Equal (r.Key + ": starts") c.Starts r.Counts.Starts |> ignore
        expect.Equal (r.Key + ": the same folded state") true (obj.ReferenceEquals(peek, r.Observations.Peek)) |> ignore

    report $"counts | add channel {added}: split +{m.AlphaSplits - splits}, bounds +1, starts +0, same folded state, on {existing.Length} channels"
    let next = markKeys props page
    let newKeys = next |> List.filter (fun k -> not (List.contains k keys))
    expect.Equal "the keys added with a channel" [ "band/" + added; "mean/" + added; "fixlo/" + added; "fixhi/" + added ] newKeys |> ignore
    expect.Equal "the keys kept when adding a channel" true (keys |> List.forall (fun k -> List.contains k next)) |> ignore
    report $"""counts | adding a channel adds {next.Length - keys.Length} keys: {String.concat ", " newKeys}"""

    // Test 5: a publication on one channel recomputes that row alone and edits the pooled moments once.
    let rows = inGraph props (fun () -> Monitor.rows m)
    let a = rows[0]
    let others = rows[1..]
    let ca = { a.Counts with Starts = a.Counts.Starts }
    let co = others |> Array.map (fun r -> { r.Counts with Starts = r.Counts.Starts })
    let refolds = m.Pooled.Refolds()
    inGraph props (fun () -> batch (fun () -> a.Feed.Push [| 0.5 |]))
    expect.Equal "A: means" (ca.Means + 1) a.Counts.Means |> ignore
    expect.Equal "A: bounds" (ca.Bounds + 1) a.Counts.Bounds |> ignore
    expect.Equal "A: settled" (ca.Settled + 1) a.Counts.Settled |> ignore

    for r, c in Array.zip others co do
        expect.Equal (r.Key + ": means") c.Means r.Counts.Means |> ignore
        expect.Equal (r.Key + ": bounds") c.Bounds r.Counts.Bounds |> ignore
        expect.Equal (r.Key + ": settled") c.Settled r.Counts.Settled |> ignore

    expect.Equal "re-folds" refolds (m.Pooled.Refolds()) |> ignore
    let otherKeys = others |> Array.map (fun r -> r.Key) |> String.concat " "
    report $"counts | publication on A: A means +1, bounds +1, settled +1; {otherKeys} +0; re-folds +{m.Pooled.Refolds() - refolds}"

    if expect.Failed = 0 then
        report "counts | all as expected"
        0
    else
        1

/// The period of a 165 Hz display, in milliseconds: the budget of one frame's work.
let private budget = 6.06

/// The smallest non-zero difference between successive readings of the platform's clock over `reads` readings;
/// infinity when every difference is 0.
let private timerStep (platform: IPlatform) (reads: int) : float =
    let mutable smallest = infinity
    let mutable previous = platform.Now()

    for _ in 2..reads do
        let now = platform.Now()
        let step = now - previous

        if step > 0.0 && step < smallest then
            smallest <- step

        previous <- now

    smallest

/// The milliseconds of one frame of 1/60 s: the frame's batch, with its recomputation, chart scene and DOM writes,
/// and then the style and layout computed for a read of `root`'s bounding box.
let private frameTime (props: Props) (m: Monitor) (root: Ranvier.Dom.Element) : float =
    let platform = props.Platform
    let started = platform.Now()
    inGraph props (fun () -> Monitor.frame m (1.0 / 60.0))
    root.getBoundingClientRect () |> ignore
    platform.Now() - started

/// The mean of `times` and their 95th percentile, the element at index ceil (0.95 n) - 1 of the n sorted times.
let private meanAndP95 (times: float[]) : struct (float * float) =
    let sorted = Array.sort times
    struct (Array.average times, sorted[int (ceil (0.95 * float sorted.Length)) - 1])

/// <summary>
/// Times the frame work of the default page, then of the page at 2,000 to 1,024,000 observations a second per channel,
/// doubling, each rate from a restart. Returns 0 when the default page's mean and 95th percentile are both under
/// <c>budget</c> and every channel folded the items due at its rate; 1 otherwise.
/// </summary>
/// <remarks>
/// The last line reports the highest swept rate that, together with every lower swept rate, has both figures under
/// <c>budget</c>, or <c>none</c> when 2,000 a second is at or over it.
/// </remarks>
let private frames (props: Props) =
    let report = props.Platform.Report
    let expect = Expectations(report, "frames")
    let m = (pageOf props).Monitor
    props.Session.Auto <- false
    let root = (props.Platform.Document.getElementById "table").parentElement
    let ms (x: float) = Format.fixed' 2 x
    let under (mean: float) (p95: float) = mean < budget && p95 < budget

    let reads = 100000
    let step = timerStep props.Platform reads
    let stepText = if step < infinity then Format.fixed' 6 step + " ms" else "none"
    report $"frames | timer | smallest non-zero step {stepText} over {reads} reads"

    // `unrecorded` frames, then the times of `timed` frames; each channel must fold every item due at the rate.
    let measure (unrecorded: int) (timed: int) =
        let rows, rate = inGraph props (fun () -> Monitor.rows m, float m.Rate.Value)
        let before = rows |> Array.map (fun r -> r.Feed.Delivered)

        for _ in 1..unrecorded do
            frameTime props m root |> ignore

        let times = Array.init timed (fun _ -> frameTime props m root)
        let due = rate * float (unrecorded + timed) / 60.0

        for r, start in Array.zip rows before do
            let delivered = r.Feed.Delivered - start
            let what = $"{r.Key}: items delivered within 1 of {Format.count due}"
            expect.Equal what true (abs (float delivered - due) <= 1.0) |> ignore
            expect.Equal (r.Key + ": items folded") (float delivered) (Moments.count r.Observations.Peek.Moments) |> ignore

        meanAndP95 times

    let channels, defaultRate = inGraph props (fun () -> (Monitor.rows m).Length, float m.Rate.Value)
    let struct (mean, p95) = measure 60 600
    report $"frames | {channels} channels at {int defaultRate} /s | mean {ms mean} ms | p95 {ms p95} ms | 600 frames | budget {ms budget} ms"
    expect.Equal "the default page's mean and 95th percentile under the budget" true (under mean p95) |> ignore

    let sweep =
        [|
            for k in 0..9 do
                let rate = 2000 <<< k

                inGraph props (fun () ->
                    m.Rate.Set(LanguagePrimitives.FloatWithMeasure(float rate))
                    Monitor.restart m)

                let struct (mean, p95) = measure 20 120
                report $"frames | sweep | {rate} /s per channel | mean {ms mean} ms | p95 {ms p95} ms | 120 frames"
                yield (rate, mean, p95)
        |]

    match sweep |> Array.takeWhile (fun (_, mean, p95) -> under mean p95) |> Array.tryLast with
    | Some(rate, mean, p95) ->
        report $"frames | highest rate under the budget | {rate} /s per channel | mean {ms mean} ms | p95 {ms p95} ms | 120 frames"
    | None -> report "frames | highest rate under the budget | none | 120 frames"

    if expect.Failed = 0 then 0 else 1

/// Runs the named script against the page mounted for `props` and returns its exit code.
/// Its last line is `script done <code>`.
let run (props: Props) (name: string) : int =
    let report = props.Platform.Report

    let code =
        try
            match name with
            | "table" -> table props
            | "counts" -> counts props
            | "frames" -> frames props
            | "picture" ->
                let code = table props
                report (props.Platform.SavePicture "monitor")
                code
            | other ->
                report $"unknown script: {other}"
                2
        with failure ->
            report $"script failed: {failure.Message}"
            1

    report $"script done {code}"
    code
