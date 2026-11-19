/// The page's chart: every channel's mean with its confidence sequence and fixed-sample interval, against the count.
module SequentialMonitor.Charts

open Ranvier.D5
open Ranvier.Quantities
open SequentialMonitor.Plot

[<Literal>]
let private Width = 960.0

/// The upper end of the horizontal axis, in observations.
[<Literal>]
let private CountLimit = 1e7

[<Literal>]
let private BandOpacity = 0.22

/// The stroke width of a fixed-sample interval's line, in pixels.
[<Literal>]
let private IntervalWidth = 0.7

[<Literal>]
let private IntervalOpacity = 0.85

/// White, the colour of the target line and its legend swatch; distinct from every colour of `palette`.
let private targetColour: Rgb = (255, 255, 255)

/// "<name> [<unit symbol>]", from a quantity's entry; the name alone when the unit has no symbol.
let axisLabel (entry: Entry) : string =
    let symbol = UnitInfo.symbol entry.Unit
    if symbol = "" then entry.Name else entry.Name + " [" + symbol + "]"

let private isFinite (x: float) = abs x < infinity

/// The indices of the samples drawn: a finite count within the horizontal range and finite values in every series.
let private drawn (band: Band) (series: float[] list) : int[] =
    [|
        for i in 0 .. band.Counts.Length - 1 do
            let count = band.Counts[i]

            if isFinite count && count > 0.0 && count <= CountLimit && series |> List.forall (fun s -> isFinite s[i]) then
                yield i
    |]

/// A path of one MoveTo at the plot's lower-left corner: the mark of a series with nothing to draw.
let private placeholder (p: Panel) = Path.ofCommands [ MoveTo(pt p.Left (p.Top + p.Height)) ]

let private linePath (p: Panel) (band: Band) (values: float[]) =
    match drawn band [ values ] with
    | [||] -> placeholder p
    | indices -> Shape.line Curve.Linear [ for i in indices -> Some(pt (p.Px band.Counts[i]) (p.Py values[i])) ]

let private areaPath (p: Panel) (band: Band) =
    match drawn band [ band.Lower; band.Upper ] with
    | [||] -> placeholder p
    | indices ->
        Shape.area Curve.Linear [ for i in indices -> Some(pt (p.Px band.Counts[i]) (p.Py band.Lower[i]), pt (p.Px band.Counts[i]) (p.Py band.Upper[i])) ]

/// The four marks of one channel, keyed `band/<key>`, `mean/<key>`, `fixlo/<key>` and `fixhi/<key>`.
let private channelMarks (p: Panel) (colour: Rgb) (row: Row) : Mark list =
    let band = row.Band.Value

    [
        fill ("band/" + row.Key) colour BandOpacity (areaPath p band)
        stroke ("mean/" + row.Key) colour 1.6 (linePath p band band.Mean)
        stroke ("fixlo/" + row.Key) colour IntervalWidth (linePath p band band.FixedLower) |> Mark.withOpacity IntervalOpacity
        stroke ("fixhi/" + row.Key) colour IntervalWidth (linePath p band band.FixedUpper) |> Mark.withOpacity IntervalOpacity
    ]

/// The legend inside the top right of the plot: a filled block for the band, a thin line for the fixed-sample
/// interval and a line for the target, each drawn as its marks are.
let private legendMarks (p: Panel) : Mark list =
    let x = p.Left + p.Width - 8.0
    let y (i: int) = p.Top + 12.0 + 14.0 * float i
    let label (i: int) (value: string) = text $"{p.Key}/lg{i}" (x - 18.0) (y i + 3.5) 10.0 "end" grey value

    [
        label 0 "confidence sequence"
        fill $"{p.Key}/lgs0" grey BandOpacity (rect (x - 13.0) (y 0 - 4.0) 13.0 8.0)
        label 1 "fixed-sample interval: valid at one planned n only"
        stroke $"{p.Key}/lgs1" grey IntervalWidth (segment (x - 13.0) (y 1) x (y 1)) |> Mark.withOpacity IntervalOpacity
        label 2 "target"
        stroke $"{p.Key}/lgs2" targetColour 1.2 (segment (x - 13.0) (y 2) x (y 2))
    ]

/// The chart of every channel: per channel a band, a mean line and the two lines of the fixed-sample interval,
/// and one line at the target. A tracked read of each row's Band and of the target.
let scene (m: Monitor) : Scene =
    let rows = Monitor.rows m
    let target = float m.Target.Value
    let height = Monitor.ChartPlotHeight + 62.0

    let xLabel, yLabel =
        match Array.tryHead rows with
        | Some r -> axisLabel r.N.Entry, axisLabel r.Mean.Entry
        | None -> "", axisLabel m.Target.Entry

    let p =
        panelWith
            "chart"
            0.0
            0.0
            Width
            height
            "mean with its confidence sequence, per channel"
            Log
            (Monitor.BurnIn, CountLimit)
            xLabel
            Linear
            (target - Monitor.ChartHalfRange, target + Monitor.ChartHalfRange)
            yLabel

    let channels =
        rows |> Array.toList |> List.mapi (fun i r -> channelMarks p palette[i % palette.Length] r) |> List.concat

    Scene.create Width height "Sequential monitor chart" (p.Frame @ channels @ [ hline p "target" targetColour target ] @ legendMarks p)
