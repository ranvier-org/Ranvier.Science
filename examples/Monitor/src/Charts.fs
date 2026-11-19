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

/// "<name> [<unit symbol>]", from a quantity's entry; a dimensionless unit is written "1".
let axisLabel (entry: Entry) : string =
    let symbol = UnitInfo.symbol entry.Unit
    entry.Name + " [" + (if symbol = "" then "1" else symbol) + "]"

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
        fill ("band/" + row.Key) colour 0.22 (areaPath p band)
        stroke ("mean/" + row.Key) colour 1.6 (linePath p band band.Mean)
        stroke ("fixlo/" + row.Key) colour 0.7 (linePath p band band.FixedLower) |> Mark.withOpacity 0.85
        stroke ("fixhi/" + row.Key) colour 0.7 (linePath p band band.FixedUpper) |> Mark.withOpacity 0.85
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

    let legendMarks =
        legend p [ grey, "confidence sequence"; grey, "fixed-sample interval: valid at one planned n only"; amber, "target" ]

    Scene.create Width height "Sequential monitor chart" (p.Frame @ channels @ [ hline p "target" amber target ] @ legendMarks)
