namespace SequentialMonitor

open System
open System.Collections.Generic
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities
open Ranvier.Statistics

[<RequireQualifiedAccess>]
type RowState =
    | Waiting
    | NotFinite
    | Failed of message: string
    | Showing

/// Recomputations of one row's nodes since it was created.
type RowCounts =
    {
        mutable Starts: int
        mutable Means: int
        mutable Bounds: int
        mutable Bands: int
        mutable Settled: int
    }

/// A channel's chart series, one element per history sample at or past the burn-in.
type Band =
    {
        Counts: float[]
        Mean: float[]
        Lower: float[]
        Upper: float[]
        FixedLower: float[]
        FixedUpper: float[]
    }

[<NoEquality; NoComparison>]
type Row =
    {
        Key: string
        /// A child of the page's model.
        Model: Model
        TrueMean: Input<mK>
        TrueSd: Input<mK>
        Seed: Input<1>
        Feed: Feed
        Observations: StreamMemo<Folded>
        /// A tracked read.
        State: unit -> RowState
        N: Quantity<1>
        Mean: Quantity<mK>
        Variance: Quantity<mK^2>
        Se: Quantity<mK>
        Lower: Quantity<mK>
        Upper: Quantity<mK>
        Band: Block<Band>
        /// The formula's value on this row; NaN with no valid formula.
        User: Memo<float>
        /// The row's six table cells: channel, n, mean, standard error, sequence, formula. A tracked read.
        Cells: unit -> string[]
        Counts: RowCounts
    }

type Checked = { Expr: Expr; Unit: UnitInfo }

/// The formula as typed, the last one that parsed and checked, and why the typed one did not.
type FormulaState =
    {
        Text: string
        Valid: Checked voption
        Error: string voption
    }

/// What the frame before the last `frame` call delivered and caused. `Restarts` counts the restarts of the rows present
/// at both calls.
type Diagnostics = { Items: int; Rows: int; Restarts: int }

/// The page's channel keys, its restart signal, and the figures `frame` keeps between calls.
[<Sealed>]
type MonitorInternals internal (channels: KeyedCollection<string, string>, restarts: Signal<int>) =
    member internal _.Channels = channels
    member internal _.Restarts = restarts
    member val internal Items = 0 with get, set
    member val internal Means = Dictionary<string, int>()
    member val internal Starts = Dictionary<string, RowCounts * int>()

[<NoEquality; NoComparison>]
type Monitor =
    {
        Model: Model
        /// The row identifiers available to a formula, as constants in the rows' units; a child of `Model`.
        Schema: Model
        Alpha: Input<1>
        TightestAt: Input<1>
        Target: Input<mK>
        Rate: Input<1 / s>
        AlphaPerChannel: Quantity<1>
        Running: Signal<bool>
        Rows: Projection<string, Row>
        Pooled: Maintained<Moments>
        PooledFromScratch: Memo<Moments>
        FormulaText: Signal<string>
        Formula: Memo<FormulaState>
        Diagnostics: Signal<Diagnostics>
        mutable AlphaSplits: int
        Internals: MonitorInternals
    }

/// A formula's evaluator on one row, with the checked formula it was built from; NaN with no formula.
[<NoEquality; NoComparison>]
type private Bound =
    {
        Source: Checked voption
        Evaluate: unit -> float
    }

module Monitor =
    let private unbound: Bound = { Source = ValueNone; Evaluate = fun () -> nan }

    let BurnIn = 30.0

    /// In mK about the target.
    let ChartHalfRange = 3.0

    /// In pixels.
    let ChartPlotHeight = 320.0

    let private trueMeans = [| 0.84; -0.02; 0.0; 0.3 |]

    let private isFinite (x: float) = abs x < infinity

    /// The key of the channel at `position` counted from 1: A to Z, then AA, AB and so on.
    let private keyAt (position: int) : string =
        let mutable rest = position
        let mutable key = ""

        while rest > 0 do
            let digit = (rest - 1) % 26
            key <- string (char (int 'A' + digit)) + key
            rest <- (rest - 1) / 26

        key

    let private positionOf (key: string) : int =
        let mutable position = 0

        for c in key do
            position <- position * 26 + (int c - int 'A' + 1)

        position

    let private stateOf (reading: Reading<Folded>) : RowState =
        match reading with
        | Pending -> RowState.Waiting
        | Failed error -> RowState.Failed error.Message
        | Ready f ->
            let m = f.Moments

            if Moments.count m = 0.0 then
                RowState.Waiting
            elif isFinite (Moments.mean m) && isFinite (Moments.variance m) then
                RowState.Showing
            else
                RowState.NotFinite

    let private emptyBand: Band =
        {
            Counts = [||]
            Mean = [||]
            Lower = [||]
            Upper = [||]
            FixedLower = [||]
            FixedUpper = [||]
        }

    let private bandOf (alpha: float) (rho: float) (history: History) : Band =
        let samples = History.samples history |> Array.filter (fun s -> s.Count >= BurnIn)
        let z = -(NormalQuantile.ofProbability (alpha / 2.0))
        let radius (s: Sample) = ConfidenceSequence.radius alpha rho s.Count s.Sd
        let fixedRadius (s: Sample) = z * s.Sd / sqrt s.Count

        {
            Counts = samples |> Array.map (fun s -> s.Count)
            Mean = samples |> Array.map (fun s -> s.Mean)
            Lower = samples |> Array.map (fun s -> s.Mean - radius s)
            Upper = samples |> Array.map (fun s -> s.Mean + radius s)
            FixedLower = samples |> Array.map (fun s -> s.Mean - fixedRadius s)
            FixedUpper = samples |> Array.map (fun s -> s.Mean + fixedRadius s)
        }

    /// `previous` when every array of `next` is held to it; otherwise `next` with each held array replaced by the
    /// previous instance.
    let private holdBand (previous: Band voption) (next: Band) : Band =
        match previous with
        | ValueNone -> next
        | ValueSome p ->
            let hold = Tolerance.holdSeries (Tolerance.absolute (0.1 * 2.0 * ChartHalfRange / ChartPlotHeight))
            let counts = hold (ValueSome p.Counts) next.Counts
            let mean = hold (ValueSome p.Mean) next.Mean
            let lower = hold (ValueSome p.Lower) next.Lower
            let upper = hold (ValueSome p.Upper) next.Upper
            let fixedLower = hold (ValueSome p.FixedLower) next.FixedLower
            let fixedUpper = hold (ValueSome p.FixedUpper) next.FixedUpper
            let same (a: float[]) (b: float[]) = obj.ReferenceEquals(a, b)

            if
                same counts p.Counts
                && same mean p.Mean
                && same lower p.Lower
                && same upper p.Upper
                && same fixedLower p.FixedLower
                && same fixedUpper p.FixedUpper
            then
                p
            else
                {
                    Counts = counts
                    Mean = mean
                    Lower = lower
                    Upper = upper
                    FixedLower = fixedLower
                    FixedUpper = fixedUpper
                }

    let private waitingCells (key: string) = [| key; "waiting for data"; ""; ""; ""; "" |]

    let private symbolOf (q: Quantity<'u>) = UnitInfo.symbol q.Entry.Unit

    let private withUnit (q: Quantity<'u>) (x: float) = Format.fixed' 2 x + " " + symbolOf q

    /// The distance between `a` and `b` in units in the last place of the larger magnitude; 0 when both are NaN.
    let private ulps (a: float) (b: float) : float =
        let largest = max (abs a) (abs b)

        if Double.IsNaN a && Double.IsNaN b then 0.0
        elif Double.IsNaN a || Double.IsNaN b then nan
        elif a = b then 0.0
        elif Double.IsInfinity largest then infinity
        else
            let mutable e = Math.Floor(Math.Log largest / Math.Log 2.0)

            while Math.Pow(2.0, e) > largest do
                e <- e - 1.0

            while Math.Pow(2.0, e + 1.0) <= largest do
                e <- e + 1.0

            abs (a - b) / Math.Pow(2.0, max (e - 52.0) -1074.0)

    /// The row identifiers, declared with the rows' units, as constants of a child of `page`.
    let private schemaOf (page: Model) : Model =
        let schema = Model.child page "row schema"
        Quantity.constant schema "true_mean" "true mean" Units.millikelvin 0.0<mK> "row schema" |> ignore
        Quantity.constant schema "true_sd" "true standard deviation" Units.millikelvin 0.0<mK> "row schema" |> ignore
        Quantity.constant schema "seed" "seed" Units.one 0.0 "row schema" |> ignore
        Quantity.constant schema "n" "count" Units.one 0.0 "row schema" |> ignore
        Quantity.constant schema "mean" "mean" Units.millikelvin 0.0<mK> "row schema" |> ignore
        Quantity.constant schema "variance" "variance" (UnitOf.squared Units.millikelvin) 0.0<mK^2> "row schema" |> ignore
        Quantity.constant schema "se" "standard error" Units.millikelvin 0.0<mK> "row schema" |> ignore
        Quantity.constant schema "lower" "sequence lower bound" Units.millikelvin 0.0<mK> "row schema" |> ignore
        Quantity.constant schema "upper" "sequence upper bound" Units.millikelvin 0.0<mK> "row schema" |> ignore
        schema

    /// The page's graph with no channel, under the current owner.
    let create () : Monitor =
        let self = ref Unchecked.defaultof<Monitor>
        let model = Model.create "monitor"
        let alpha = Quantity.input model "alpha" "α" Units.one 0.05
        let tightestAt = Quantity.input model "tightest_at" "tightest at" Units.one 1000.0
        let target = Quantity.input model "target" "target" Units.millikelvin 0.0<mK>
        let rate = Quantity.input model "rate" "rate" (Units.one / Units.second) 1000.0</s>
        let channels = createKeyedCollection id
        let restarts = createSignal 0

        let alphaPerChannel =
            Quantity.derive model "alpha_per_channel" "α per channel" Units.one (fun () ->
                self.Value.AlphaSplits <- self.Value.AlphaSplits + 1
                alpha.Value / float (max 1 channels.Rows.Count))

        let formulaText = createSignal ""
        let schema = schemaOf model

        let formula =
            createMemo (fun (previous: FormulaState voption) ->
                let text = formulaText.Value
                let lastValid = previous |> ValueOption.bind (fun p -> p.Valid)

                if text.Trim() = "" then
                    { Text = text; Valid = ValueNone; Error = ValueNone }
                else
                    match Formula.parse text |> Result.bind (fun e -> Formula.check schema e |> Result.map (fun u -> e, u)) with
                    | Result.Ok(expr, unit) ->
                        {
                            Text = text
                            Valid = ValueSome { Expr = expr; Unit = unit }
                            Error = ValueNone
                        }
                    | Result.Error message ->
                        {
                            Text = text
                            Valid = lastValid
                            Error = ValueSome message
                        })

        let factory (item: unit -> string) : unit -> Row =
            let key = item ()
            let position = positionOf key
            let counts = { Starts = 0; Means = 0; Bounds = 0; Bands = 0; Settled = 0 }
            let rowModel = Model.child model key

            let initialMean = if position <= trueMeans.Length then trueMeans[position - 1] else 0.0
            let trueMean = Quantity.input rowModel "true_mean" "true mean" Units.millikelvin (initialMean * 1.0<mK>)
            let trueSd = Quantity.input rowModel "true_sd" "true standard deviation" Units.millikelvin 10.0<mK>
            let seed = Quantity.input rowModel "seed" "seed" Units.one (float position)
            let feed = Feed()

            let observations =
                createStreamFold Folded.zero Folded.add (fun _ ->
                    counts.Starts <- counts.Starts + 1
                    restarts.Value |> ignore
                    feed.Source(float trueMean.Value, float trueSd.Value, int (float seed.Value)))

            let moments () = observations.Value.Moments

            let n = Quantity.derive rowModel "n" "count" Units.one (fun () -> Moments.count (moments ()))

            let mean =
                Quantity.derive rowModel "mean" "mean" Units.millikelvin (fun () ->
                    counts.Means <- counts.Means + 1
                    Moments.mean (moments ()) * 1.0<mK>)

            let variance =
                Quantity.derive rowModel "variance" "variance" (UnitOf.squared Units.millikelvin) (fun () ->
                    let m = moments ()
                    if isFinite (Moments.mean m) then Moments.variance m * 1.0<mK^2> else nan * 1.0<mK^2>)

            let se =
                Quantity.derive rowModel "se" "standard error" Units.millikelvin (fun () ->
                    let m = moments ()
                    if isFinite (Moments.mean m) then Moments.standardError m * 1.0<mK> else nan * 1.0<mK>)

            let sequence =
                Quantity.block rowModel "sequence" "confidence sequence" (fun () ->
                    counts.Bounds <- counts.Bounds + 1
                    let m = moments ()
                    let a = alphaPerChannel.Value
                    ConfidenceSequence.ofMomentsAfter BurnIn a (ConfidenceSequence.rhoTightestAt a tightestAt.Value) m)

            let lower = Quantity.derive rowModel "lower" "sequence lower bound" Units.millikelvin (fun () -> sequence.Value.Lower * 1.0<mK>)
            let upper = Quantity.derive rowModel "upper" "sequence upper bound" Units.millikelvin (fun () -> sequence.Value.Upper * 1.0<mK>)

            let band =
                Quantity.blockFrom rowModel "band" "chart series" (fun previous ->
                    counts.Bands <- counts.Bands + 1

                    let next =
                        match observations.TryValue with
                        | Ready f ->
                            let a = alphaPerChannel.Value
                            bandOf a (ConfidenceSequence.rhoTightestAt a tightestAt.Value) f.History
                        | _ -> emptyBand

                    holdBand previous next)

            let bound =
                createMemo (fun (previous: Bound voption) ->
                    match formula.Value.Valid, previous with
                    | ValueSome c, ValueSome p when p.Source.IsSome && obj.ReferenceEquals(c, p.Source.Value) -> p
                    | ValueSome c, _ ->
                        {
                            Source = ValueSome c
                            Evaluate = Formula.evaluator rowModel c.Expr
                        }
                    | ValueNone, ValueSome p when p.Source.IsNone -> p
                    | ValueNone, _ -> unbound)

            let user = createMemo (fun _ -> bound.Value.Evaluate())

            let state () = stateOf observations.TryValue

            let cells () =
                match state () with
                | RowState.Waiting -> waitingCells key
                | RowState.Failed message -> [| key; "failed: " + message; ""; ""; ""; "" |]
                | RowState.NotFinite -> [| key; Format.count (float n.Value); "not finite"; ""; ""; "" |]
                | RowState.Showing ->
                    let count = float n.Value

                    let sequenceText =
                        if count < BurnIn then
                            "after " + Format.fixed' 0 BurnIn + " observations"
                        else
                            "["
                            + Format.fixed' 2 (float lower.Value)
                            + ", "
                            + Format.fixed' 2 (float upper.Value)
                            + "] "
                            + symbolOf lower

                    let formulaText =
                        match formula.Value.Valid with
                        | ValueSome _ -> Format.fixed' 1 user.Value
                        | ValueNone -> ""

                    [|
                        key
                        Format.count count
                        withUnit mean (float mean.Value)
                        withUnit se (float se.Value)
                        sequenceText
                        formulaText
                    |]

            let row =
                {
                    Key = key
                    Model = rowModel
                    TrueMean = trueMean
                    TrueSd = trueSd
                    Seed = seed
                    Feed = feed
                    Observations = observations
                    State = state
                    N = n
                    Mean = mean
                    Variance = variance
                    Se = se
                    Lower = lower
                    Upper = upper
                    Band = band
                    User = user
                    Cells = cells
                    Counts = counts
                }

            fun () -> row

        let rows = createProjectionWith id factory (fun () -> channels.Rows.Keys :> string seq)

        let settled =
            createProjectionWith
                (fun (r: Row) -> r.Key)
                (fun (item: unit -> Row) ->
                    fun () ->
                        let r = item ()
                        r.Counts.Settled <- r.Counts.Settled + 1

                        match r.Observations.TryValue with
                        | Ready f when stateOf (Ready f) = RowState.Showing -> f.Moments
                        | _ -> Moments.zero)
                (fun () -> rows.Keys |> Array.map rows.Get :> Row seq)

        let policy = FloatPolicy.standard

        let monitor =
            {
                Model = model
                Schema = schema
                Alpha = alpha
                TightestAt = tightestAt
                Target = target
                Rate = rate
                AlphaPerChannel = alphaPerChannel
                Running = createSignal true
                Rows = rows
                Pooled = Aggregate.momentsBy policy id settled
                PooledFromScratch = Aggregate.momentsFromScratchBy policy id settled
                FormulaText = formulaText
                Formula = formula
                Diagnostics = createSignal { Items = 0; Rows = 0; Restarts = 0 }
                AlphaSplits = 0
                Internals = MonitorInternals(channels, restarts)
            }

        self.Value <- monitor
        monitor

    /// Adds the next channel (A, B, C ...) and returns its key: the first key from A that no present channel uses.
    let addChannel (m: Monitor) : string =
        let channels = m.Internals.Channels
        let present = HashSet<string>(untrack (fun () -> channels.Rows.Keys))
        let mutable position = 1

        while present.Contains(keyAt position) do
            position <- position + 1

        let key = keyAt position
        channels.AddOrUpdate key
        key

    /// Removes the channel with `key`; a key that is not present is ignored.
    let removeChannel (m: Monitor) (key: string) : unit = m.Internals.Channels.Remove key |> ignore

    /// Starts every channel again from its seed.
    let restart (m: Monitor) : unit =
        let restarts = m.Internals.Restarts
        restarts.Value <- untrack (fun () -> restarts.Value) + 1

    /// The rows in channel order, A before B before C and AA after Z. A tracked read of the keys.
    let rows (m: Monitor) : Row[] =
        m.Rows.Keys |> Array.sortBy positionOf |> Array.map m.Rows.Get

    /// <summary>
    /// One frame of <c>dt</c> seconds: every feed delivers at the page's rate, in one batch, while <c>Running</c> is
    /// true. A <c>dt</c> that is negative or not finite counts as 0, and one above 0.1 as 0.1. Writes
    /// <c>Diagnostics</c> for the previous frame in the same batch, when its values differ from the ones it holds.
    /// </summary>
    let frame (m: Monitor) (dt: float) : unit =
        let step = if dt >= 0.0 && dt < infinity then min dt 0.1 else 0.0
        let internals = m.Internals

        let current, running, rate =
            untrack (fun () -> rows m, m.Running.Value, float m.Rate.Value)

        let mutable recomputed = 0
        let mutable restarted = 0

        for r in current do
            match internals.Means.TryGetValue r.Key with
            | true, means when means = r.Counts.Means -> ()
            | _ -> recomputed <- recomputed + 1

            match internals.Starts.TryGetValue r.Key with
            | true, (counts, starts) when obj.ReferenceEquals(counts, r.Counts) ->
                restarted <- restarted + (r.Counts.Starts - starts)
            | _ -> ()

        internals.Means.Clear()
        internals.Starts.Clear()

        for r in current do
            internals.Means[r.Key] <- r.Counts.Means
            internals.Starts[r.Key] <- (r.Counts, r.Counts.Starts)

        let tally =
            {
                Items = internals.Items
                Rows = recomputed
                Restarts = restarted
            }

        internals.Items <-
            batch (fun () ->
                if untrack (fun () -> m.Diagnostics.Value) <> tally then
                    m.Diagnostics.Value <- tally

                if running then
                    current |> Array.sumBy (fun r -> r.Feed.EmitAtRate(rate, step))
                else
                    0)

    let private setWith (accept: float -> bool) (message: string) (input: Input<'u>) (text: string) : Result<unit, string> =
        match Format.tryNumber text with
        | ValueSome x when accept x ->
            input.Set(LanguagePrimitives.FloatWithMeasure<'u> x)
            Result.Ok()
        | _ -> Result.Error message

    /// Sets α from the text of its field, or says why not; α keeps its value on Error.
    let setAlpha (m: Monitor) (text: string) : Result<unit, string> =
        setWith (fun x -> x > 0.0 && x < 1.0) "α must be a number between 0 and 1." m.Alpha text

    /// Sets the count the sequence is tightest at, or says why not; it keeps its value on Error.
    let setTightestAt (m: Monitor) (text: string) : Result<unit, string> =
        setWith (fun x -> x >= 1.0 && x < infinity) "The count must be a finite number of at least 1." m.TightestAt text

    /// Sets the target, or says why not; it keeps its value on Error.
    let setTarget (m: Monitor) (text: string) : Result<unit, string> =
        setWith isFinite "The target must be a finite number of mK." m.Target text

    /// Sets the rate, or says why not; it keeps its value on Error.
    let setRate (m: Monitor) (text: string) : Result<unit, string> =
        setWith (fun x -> x >= 0.0 && x <= 1_000_000.0) "The rate must be a number from 0 to 1,000,000 per second." m.Rate text

    /// The pooled row's cells; with channels and none pooled, the waiting cells with the pooled note. A tracked read.
    let pooledCells (m: Monitor) : string[] =
        let all = rows m
        let pooled = m.Pooled.Value.Value

        if all.Length = 0 then
            waitingCells "All"
        else
            let showing = all |> Array.filter (fun r -> r.State() = RowState.Showing) |> Array.length

            let note =
                if showing = all.Length then
                    "(pooled, descriptive)"
                else
                    "(pooled, " + string showing + " of " + string all.Length + " channels)"

            if Moments.count pooled = 0.0 then
                [| "All"; "waiting for data"; ""; ""; note; "" |]
            else
                let symbol = UnitInfo.symbol Units.millikelvin.Info

                [|
                    "All"
                    Format.count (Moments.count pooled)
                    Format.fixed' 2 (Moments.mean pooled) + " " + symbol
                    Format.fixed' 2 (Moments.standardError pooled) + " " + symbol
                    note
                    ""
                |]

    /// Units in the last place between the maintained and the from-scratch pooled mean. A tracked read.
    let pooledDistance (m: Monitor) : float =
        ulps (Moments.mean m.Pooled.Value.Value) (Moments.mean m.PooledFromScratch.Value)
