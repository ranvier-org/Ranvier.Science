/// The sequential monitor page: the controls, the table of channels, the formula, the chart and the diagnostics.
module SequentialMonitor.App

open Ranvier
open Ranvier.D5.View
open Ranvier.Dom
open Ranvier.Quantities
open Ranvier.View
open Ranvier.View.Dsl

/// The mounted page.
type Page =
    {
        Monitor: Monitor
        /// The chart; `SceneBuilds` counts its computations.
        Scene: Memo<Ranvier.D5.Scene>
        /// Why the last control entry was refused; "" after an accepted one.
        Messages: Signal<string>
        Props: Props
        mutable SceneBuilds: int
    }

/// A table row of one channel: its key and its six cells.
type private RowView = { Key: string; Cells: Memo<string[]> }

let private textOf (x: float) = string x

let private alphaText (m: Monitor) = textOf (float m.Alpha.Value)
let private tightestAtText (m: Monitor) = textOf (float m.TightestAt.Value)
let private targetText (m: Monitor) = textOf (float m.Target.Value)
let private rateText (m: Monitor) = textOf (float m.Rate.Value)

/// Applies a control's field on `change`: a refused entry puts the field back and shows the message.
let private apply (set: Monitor -> string -> Result<unit, string>) (current: Monitor -> string) (page: Page) (event: Dom.Event) =
    let field = unbox<HTMLInputElement> event.currentTarget

    match set page.Monitor field.value with
    | Result.Ok() -> page.Messages.Value <- ""
    | Result.Error message ->
        page.Messages.Value <- message
        field.value <- current page.Monitor

let private cellStyle = "padding:3px 14px 3px 0;text-align:left;white-space:nowrap"

let private rowComponent: Component<unit -> Row> =
    Component.template
        (fun _ (current: unit -> Row) ->
            let row = current ()
            { Key = row.Key; Cells = createMemo (fun _ -> row.Cells()) })
        (tr (tr.data ("row", fun (v: RowView) -> Some v.Key)) {
            for i in 0..5 do
                td (td.style cellStyle) { fun (v: RowView) -> v.Cells.Value[i] }
        })

let private pooledComponent: Component<Monitor> =
    Component.template
        (fun _ (m: Monitor) -> createMemo (fun _ -> Monitor.pooledCells m))
        (tr (tr.data ("row", Some "All"), tr.style "border-top:1px solid #94a3b8;font-weight:600") {
            for i in 0..5 do
                td (td.style cellStyle) { fun (cells: Memo<string[]>) -> cells.Value[i] }
        })

let private control (label: string) (unitText: string) (read: Monitor -> string) (set: Monitor -> string -> Result<unit, string>) =
    span (span.style "margin-right:22px") {
        label + "  "

        input (
            input.style "width:76px;font:inherit",
            input.props.value (fun (page: Page) -> read page.Monitor),
            input.on ("change", apply set read)
        )

        " " + unitText
    }

let private formulaUnit (page: Page) =
    match page.Monitor.Formula.Value.Valid with
    | ValueSome c ->
        let symbol = UnitInfo.symbol c.Unit
        if symbol = "" then "dimensionless" else symbol
    | ValueNone -> ""

let private formulaStatus (page: Page) =
    let f = page.Monitor.Formula.Value

    match f.Error, f.Valid with
    | ValueSome message, ValueSome _ -> "stale: " + message
    | ValueSome message, ValueNone -> message
    | ValueNone, ValueSome _ -> "ok"
    | ValueNone, ValueNone -> ""

let private poolingLine (page: Page) =
    let m = page.Monitor
    let distance = Monitor.pooledDistance m
    $"pooled, maintained vs from scratch: {distance} ulp     re-folds: {m.Pooled.Refolds()}"

let private frameLine (page: Page) =
    let d = page.Monitor.Diagnostics.Value
    $"last frame: {d.Items} items folded, {d.Rows} rows recomputed, {d.Restarts} streams restarted"

let private removeLast (m: Monitor) =
    match Monitor.rows m |> Array.tryLast with
    | Some r -> Monitor.removeChannel m r.Key
    | None -> ()

let private setup (ctx: Setup) (props: Props) : Page =
    let monitor = Monitor.create ()

    for _ in 1..4 do
        Monitor.addChannel monitor |> ignore

    let self = ref Unchecked.defaultof<Page>

    let scene =
        createMemo (fun _ ->
            self.Value.SceneBuilds <- self.Value.SceneBuilds + 1
            Charts.scene monitor)

    let page =
        {
            Monitor = monitor
            Scene = scene
            Messages = createSignal ""
            Props = props
            SceneBuilds = 0
        }

    self.Value <- page
    let graph = Graph.Current
    props.Session.Graph <- graph
    props.Session.Page <- box page

    // Each frame delivers the elapsed seconds while the page runs on its own.
    let platform = props.Platform
    let alive = ref true
    let last = ref (platform.Now())

    let rec tick (_: float) =
        if alive.Value then
            let now = platform.Now()
            let dt = (now - last.Value) / 1000.0
            last.Value <- now

            if props.Session.Auto then
                graph.Run(fun () -> Monitor.frame monitor dt)

            platform.RequestFrame tick

    platform.RequestFrame tick
    ctx.OnCleanup(fun () -> alive.Value <- false)
    page

let private buttonStyle = "margin-left:8px;padding:3px 12px;font:inherit"

let app: Component<Props> =
    Component.template
        setup
        (div (div.style "font-family:sans-serif;font-size:14px;color:#0f172a;background:#f8fafc;padding:16px 20px") {
            div (div.style "display:flex;align-items:center;border-bottom:1px solid #cbd5e1;padding-bottom:8px") {
                span (span.style "font-size:20px;font-weight:600;flex:1") { "Sequential monitor" }

                button (
                    button.style buttonStyle,
                    button.onClick (fun (page: Page) _ -> page.Monitor.Running.Value <- not page.Monitor.Running.Value)
                ) {
                    fun (page: Page) -> if page.Monitor.Running.Value then "Pause" else "Resume"
                }

                button (button.style buttonStyle, button.onClick (fun (page: Page) _ -> Monitor.restart page.Monitor)) { "Restart" }
            }

            div (div.style "margin:12px 0 4px 0") {
                control "Level α" "" alphaText Monitor.setAlpha
                control "Tightest near n" "" tightestAtText Monitor.setTightestAt
                control "Target" "mK" targetText Monitor.setTarget
                control "Rate" "/s" rateText Monitor.setRate
            }

            div (div.id "messages", div.style "color:#b91c1c;min-height:18px") { fun (page: Page) -> page.Messages.Value }

            table (table.id "table", table.style "border-collapse:collapse;margin:6px 0") {
                thead () {
                    tr (tr.style "border-bottom:1px solid #94a3b8") {
                        for heading in [ "Channel"; "n"; "mean"; "std error"; "confidence sequence"; "your formula" ] do
                            th (th.style cellStyle) { heading }
                    }
                }

                tbody () {
                    each (fun (row: Row) -> row.Key) (fun (page: Page) -> Monitor.rows page.Monitor) rowComponent
                    render pooledComponent (fun (page: Page) -> page.Monitor)
                }
            }

            div () {
                button (button.style buttonStyle, button.onClick (fun (page: Page) _ -> Monitor.addChannel page.Monitor |> ignore)) {
                    "+ channel"
                }

                button (button.style buttonStyle, button.onClick (fun (page: Page) _ -> removeLast page.Monitor)) { "remove" }
            }

            div (div.style "margin:12px 0") {
                "your formula:  "

                input (
                    input.style "width:280px;font:inherit",
                    input.props.value (fun (page: Page) -> page.Monitor.FormulaText.Value),
                    input.on (
                        "change",
                        fun (page: Page) event -> page.Monitor.FormulaText.Value <- (unbox<HTMLInputElement> event.currentTarget).value
                    )
                )

                span (span.style "margin-left:16px") { fun (page: Page) -> formulaUnit page }
                span (span.style "margin-left:16px") { fun (page: Page) -> formulaStatus page }
            }

            // The chart's axes are drawn in the current colour.
            div (div.style "color:#e2e8f0") { render Renderer.passive (fun (page: Page) -> fun () -> page.Scene.Value) }

            div (div.style "margin-top:10px") {
                div (div.style "font-weight:600") { "Diagnostics" }
                div (div.style "white-space:pre") { fun (page: Page) -> poolingLine page }
                div () { fun (page: Page) -> frameLine page }
            }
        })
