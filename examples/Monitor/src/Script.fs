/// Scripted runs: each drives the mounted page and reports what it sees, one line at a time.
module SequentialMonitor.Script

open Ranvier
open Ranvier.D5
open Ranvier.Dom
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
    [ for i in 0u .. nodes.length - 1u -> nodes.item i ]

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
    let dom = expect.Equal "the table rows' text" (cells |> List.map (String.concat "")) shown
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

/// Runs the named script against the page mounted for `props` and returns its exit code.
/// Its last line is `script done <code>`.
let run (props: Props) (name: string) : int =
    let report = props.Platform.Report

    let code =
        try
            match name with
            | "table" -> table props
            | "counts" -> counts props
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
