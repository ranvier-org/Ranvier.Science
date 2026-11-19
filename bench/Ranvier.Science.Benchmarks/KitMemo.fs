module Ranvier.Science.Benchmarks.KitMemo

/// Caches the result for the most recent control values.
// Copied unchanged from AstroShowcase/Kit.fs:72-82.
let memo (f: float[] -> 'a) : float[] -> 'a =
    let key: float[] ref = ref null
    let value = ref Unchecked.defaultof<'a>

    fun p ->
        if isNull key.Value || key.Value <> p then
            value.Value <- f p
            key.Value <- Array.copy p

        value.Value
