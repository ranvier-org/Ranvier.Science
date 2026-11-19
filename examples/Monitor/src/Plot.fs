// A copy of AstroShowcase/Plot.fs, under the namespace of this page; it needs Ranvier.D5 alone.
/// Chart building blocks on Ranvier.D5 marks: a framed plot area with D5 scales and axes, and line, band, bar and label marks.
module SequentialMonitor.Plot

open System
open Ranvier.D5

type Rgb = int * int * int

let paint ((r, g, b): Rgb) = Paint.rgb r g b
let pt x y = Point.create x y

let mint: Rgb = (94, 234, 212)
let blue: Rgb = (96, 165, 250)
let coral: Rgb = (251, 113, 133)
let amber: Rgb = (251, 191, 36)
let violet: Rgb = (167, 139, 250)
let green: Rgb = (74, 222, 128)
let grey: Rgb = (148, 163, 184)
let dim: Rgb = (51, 65, 85)
let ink: Rgb = (226, 232, 240)
let panelFill: Rgb = (17, 24, 39)
let plotFill: Rgb = (11, 17, 30)
let plotEdge: Rgb = (36, 48, 68)
let palette = [| mint; blue; coral; amber; violet; green |]

/// Text in the default family of a Ranvier.D5 text mark, the generic `sans-serif`.
let text key x y size anchor (colour: Rgb) (value: string) =
    let m = Mark.text key (pt x y) value |> Mark.withFill (paint colour) |> Mark.withFontSize size
    Mark.withTextStyle { Mark.textStyle m with Anchor = anchor } m

/// A heading: the same text at a heavier weight.
let heading key x y size (colour: Rgb) (value: string) =
    let m = text key x y size "start" colour value
    Mark.withTextStyle { Mark.textStyle m with Weight = 600 } m

let stroke key (colour: Rgb) width path =
    Mark.path key path |> Mark.withFill Paint.none |> Mark.withStroke (paint colour) width

let fill key (colour: Rgb) opacity path =
    Mark.path key path |> Mark.withFill (paint colour) |> Mark.withOpacity opacity

let rect x y w h = Shape.rectangle (Bounds.create x y (max 0.0 w) (max 0.0 h))

/// A rectangle with quarter-circle corners.
let roundRect x y w h r =
    let arc px py = ArcTo(r, r, 0.0, false, true, pt px py)

    Path.ofCommands
        [ MoveTo(pt (x + r) y)
          LineTo(pt (x + w - r) y)
          arc (x + w) (y + r)
          LineTo(pt (x + w) (y + h - r))
          arc (x + w - r) (y + h)
          LineTo(pt (x + r) (y + h))
          arc x (y + h - r)
          LineTo(pt x (y + r))
          arc (x + r) y
          Close ]
let segment x0 y0 x1 y1 = Path.ofCommands [ MoveTo(pt x0 y0); LineTo(pt x1 y1) ]

/// A plot area with its scales. Px and Py map data to scene coordinates and clamp to the plot area.
type Panel =
    { Key: string
      Left: float
      Top: float
      Width: float
      Height: float
      Px: float -> float
      Py: float -> float
      Frame: Mark list }

type AxisKind =
    | Linear
    | Log

let private fmt (v: float) =
    let a = abs v

    if v = 0.0 then "0"
    elif a >= 10000.0 || a < 0.01 then v.ToString("0.#e0")
    elif a >= 100.0 then v.ToString("0")
    elif a >= 10.0 then v.ToString("0.#")
    else v.ToString("0.##")

/// Builds a panel inside the box (x, y, w, h): background, title, grid and D5 axes over the given domains.
let panelWith key x y w h title xKind (x0, x1) xLabel yKind (y0, y1) yLabel =
    let left, top = x + 54.0, y + 26.0
    let pw, ph = w - 66.0, h - 62.0
    let scale kind domain range =
        match kind with
        | Linear -> Scale.linear domain range
        | Log -> Scale.log 10.0 domain range

    let sx = scale xKind (x0, x1) (left, left + pw) |> Scale.clamp true
    let sy = scale yKind (y0, y1) (top + ph, top) |> Scale.clamp true

    let ticks kind (a: float, b: float) sc =
        match kind with
        | Linear -> Scale.ticks 5 sc
        | Log ->
            // An axis of more than twenty decades labels about ten of them.
            let first, last = int (ceil (log10 a)), int (floor (log10 b))
            let stride = if last - first > 20 then max 1 ((last - first + 9) / 10) else 1
            [ for e in first .. stride .. last -> Math.Pow(10.0, float e) ]

    let logLabel (v: float) = "1e" + string (int (Math.Round(log10 v)))
    let label kind v = if kind = Log then logLabel v else fmt v
    let xt, yt = ticks xKind (x0, x1) sx, ticks yKind (y0, y1) sy

    let grid =
        [ for i, v in List.indexed xt ->
              stroke $"{key}/gx{i}" dim 1.0 (segment (Scale.map sx v) top (Scale.map sx v) (top + ph)) |> Mark.withOpacity 0.38
          for i, v in List.indexed yt ->
              stroke $"{key}/gy{i}" dim 1.0 (segment left (Scale.map sy v) (left + pw) (Scale.map sy v)) |> Mark.withOpacity 0.38 ]

    let axisX =
        Axis.values $"{key}/ax" AxisOrientation.Bottom (top + ph) (left, left + pw) [ for i, v in List.indexed xt -> string i, Scale.map sx v, label xKind v ]

    let axisY =
        Axis.values $"{key}/ay" AxisOrientation.Left left (top + ph, top) [ for i, v in List.indexed yt -> string i, Scale.map sy v, label yKind v ]

    let frame =
        [ fill $"{key}/bg" panelFill 1.0 (roundRect x y w h 8.0)
          // The plot area sits a shade darker than the card around it, inside a hairline edge.
          fill $"{key}/plot" plotFill 1.0 (rect left top pw ph)
          stroke $"{key}/edge" plotEdge 1.0 (rect left top pw ph)
          heading $"{key}/title" (x + 10.0) (y + 17.0) 12.5 ink title
          text $"{key}/xl" (left + pw) (top + ph + 33.0) 10.5 "end" grey xLabel
          text $"{key}/yl" (x + w - 10.0) (y + 17.0) 10.5 "end" grey yLabel ]
        @ grid
        @ (axisX @ axisY |> List.map (Mark.withFontSize 10.0))

    { Key = key
      Left = left
      Top = top
      Width = pw
      Height = ph
      Px = Scale.map sx
      Py = Scale.map sy
      Frame = frame }

let panel key x y w h title xDomain xLabel yDomain yLabel =
    panelWith key x y w h title Linear xDomain xLabel Linear yDomain yLabel

let private finite (x: float, y: float) = Double.IsFinite x && Double.IsFinite y

/// A polyline through the data points; non-finite points break the line.
let line (p: Panel) name colour width (points: seq<float * float>) =
    let pts = points |> Seq.map (fun d -> if finite d then Some(pt (p.Px(fst d)) (p.Py(snd d))) else None) |> List.ofSeq
    stroke $"{p.Key}/{name}" colour width (Shape.line Curve.Linear pts)

let steps (p: Panel) name colour width (points: seq<float * float>) =
    let pts = points |> Seq.map (fun (x, y) -> Some(pt (p.Px x) (p.Py y))) |> List.ofSeq
    stroke $"{p.Key}/{name}" colour width (Shape.line Curve.StepAfter pts)

/// A filled band between lower and upper curves.
let band (p: Panel) name colour opacity (points: seq<float * float * float>) =
    let pts = points |> Seq.map (fun (x, lo, hi) -> Some(pt (p.Px x) (p.Py lo), pt (p.Px x) (p.Py hi))) |> List.ofSeq
    fill $"{p.Key}/{name}" colour opacity (Shape.area Curve.Linear pts)

/// A data-space rectangle.
let bar (p: Panel) name colour opacity x0 x1 y0 y1 =
    let ax, bx = p.Px x0, p.Px x1
    let ay, by = p.Py y1, p.Py y0
    fill $"{p.Key}/{name}" colour opacity (rect (min ax bx) (min ay by) (abs (bx - ax)) (abs (by - ay)))

let vline (p: Panel) name colour x =
    stroke $"{p.Key}/{name}" colour 1.2 (Path.ofCommands [ MoveTo(pt (p.Px x) p.Top); LineTo(pt (p.Px x) (p.Top + p.Height)) ])

let hline (p: Panel) name colour y =
    stroke $"{p.Key}/{name}" colour 1.2 (Path.ofCommands [ MoveTo(pt p.Left (p.Py y)); LineTo(pt (p.Left + p.Width) (p.Py y)) ])

/// A filled circle at a data point.
let dot (p: Panel) name colour radius x y =
    fill $"{p.Key}/{name}" colour 1.0 (Shape.arc 0.0 radius 0.0 (2.0 * Math.PI))
    |> Mark.withTransform (Transform.translate (p.Px x) (p.Py y))

let note (p: Panel) name colour anchor x y (value: string) = text $"{p.Key}/{name}" (p.Px x) (p.Py y) 10.5 anchor colour value

/// A row of colour swatches with labels inside the top of the plot area.
let legend (p: Panel) (items: (Rgb * string) list) =
    items
    |> List.mapi (fun i (c, label) ->
        let y = p.Top + 12.0 + 14.0 * float i
        let x = p.Left + p.Width - 8.0

        [ text $"{p.Key}/lg{i}" (x - 18.0) (y + 3.5) 10.0 "end" grey label
          stroke $"{p.Key}/lgs{i}" c 2.0 (segment (x - 13.0) y x y) ])
    |> List.concat

/// A piecewise colour ramp from deep blue through teal and amber to coral, for t in [0, 1].
let ramp (t: float) : Rgb =
    let stops = [| (30, 41, 110); (37, 99, 235); (45, 212, 191); (250, 204, 21); (251, 113, 133) |]
    let u = Math.Clamp(t, 0.0, 1.0) * float (stops.Length - 1)
    let i = min (stops.Length - 2) (int u)
    let f = u - float i
    let (r0, g0, b0), (r1, g1, b1) = stops[i], stops[i + 1]
    let mix a b = int (Math.Round(float a + (float b - float a) * f))
    mix r0 r1, mix g0 g1, mix b0 b1

/// Marching-squares outlines from the D5 Contour module over a row-major grid laid on the scene rectangle; row 0 is the top row.
let contours key x y w h columns rows (values: float option list) thresholds colour width =
    let ring (points: Point list) =
        match points with
        | first :: rest when rest.Length >= 2 -> MoveTo first :: (rest |> List.map LineTo) @ [ Close ]
        | _ -> []

    match DensityGrid.create (Bounds.create x y w h) columns rows values with
    | Error _ -> []
    | Ok grid ->
        match Contour.compute (ContourConfig.create thresholds) grid with
        | Error _ -> []
        | Ok polygons ->
            polygons
            |> List.mapi (fun i p ->
                let commands = ring p.Outer @ (p.Holes |> List.collect ring)
                if commands.IsEmpty then None else Some(stroke $"{key}/c{i}" colour width (Path.ofCommands commands)))
            |> List.choose id
