/// Scripted runs: each drives the mounted page and reports what it sees, one line at a time.
module SequentialMonitor.Script

/// Reports the text of the element with id `probe` and the number of chart marks on the page.
let private hello (props: Props) =
    let document = props.Platform.Document
    let text = document.getElementById("probe").textContent
    let marks = document.querySelectorAll("[data-d5-key]").length
    props.Platform.Report $"hello | {text} | marks {marks}"
    0

/// Runs the named script against the page mounted for `props` and returns its exit code.
/// Its last line is `script done <code>`.
let run (props: Props) (name: string) : int =
    let report = props.Platform.Report

    let code =
        try
            match name with
            | "hello" -> hello props
            | other ->
                report $"unknown script: {other}"
                2
        with failure ->
            report $"script failed: {failure.Message}"
            1

    report $"script done {code}"
    code
