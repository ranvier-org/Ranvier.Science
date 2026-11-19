namespace SequentialMonitor

open Ranvier.Numerics

/// A fold's count, mean and population standard deviation at one moment.
[<Struct>]
type Sample = { Count: float; Mean: float; Sd: float }

/// Samples of a fold at geometrically spaced counts. Immutable.
type History =
    private
        {
            Samples: Sample[]
            /// The count at which the next sample is taken.
            Due: float
        }

module History =
    [<Literal>]
    let Capacity = 512

    let empty: History = { Samples = [||]; Due = 1.0 }

    /// <summary>
    /// <c>h</c> with a sample of <c>m</c> when its count has reached the next sampling count; <c>h</c> itself otherwise.
    /// </summary>
    /// <remarks>
    /// The first sample is at a count of 1; after a sample at count c the next is due at max (c + 1) (ceil (c · 1.05)).
    /// At <c>Capacity</c> the newest sample replaces the last.
    /// </remarks>
    let add (m: Moments) (h: History) : History =
        let count = Moments.count m

        if count < h.Due then
            h
        else
            let sample =
                {
                    Count = count
                    Mean = Moments.mean m
                    Sd = sqrt (Moments.variance m)
                }

            let samples =
                if h.Samples.Length < Capacity then
                    Array.append h.Samples [| sample |]
                else
                    let copy = Array.copy h.Samples
                    copy[copy.Length - 1] <- sample
                    copy

            {
                Samples = samples
                Due = max (count + 1.0) (ceil (count * 1.05))
            }

    /// Oldest first. The array is the stored instance.
    let samples (h: History) : Sample[] = h.Samples

/// What a channel has folded so far.
type Folded = { Moments: Moments; History: History }

module Folded =
    let zero: Folded =
        {
            Moments = Moments.zero
            History = History.empty
        }

    /// `f` with `x` added to its moments (compensated) and its history sampled at the new count.
    let add (f: Folded) (x: float) : Folded =
        let m = Moments.add true f.Moments x

        {
            Moments = m
            History = History.add m f.History
        }
