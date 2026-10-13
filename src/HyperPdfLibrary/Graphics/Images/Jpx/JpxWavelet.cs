// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The inverse discrete wavelet transform (ISO 15444-1 annex F) by lifting: the 5/3 reversible filter on integers and the
/// 9/7 irreversible filter on floats (equations F-8 to F-14, with the sub-band gain folded into the scales). A line is
/// rebuilt from its low-pass and high-pass samples, which are kept apart while lifting, so each step is a vector
/// operation over runs of samples; rows are interleaved at the end, and the vertical pass lifts whole rows at once, so the
/// same code serves a row of samples and a window of rows with every column side by side.
/// </summary>
internal static class JpxWavelet
{
    /// <summary>The samples of each low-pass and high-pass pair; also the halving of a lone high-pass sample.</summary>
    private const int Interleaved = 2;

    /// <summary>The rows the 5/3 vertical filter reads beyond an output row: one lifting step reads the neighbour, the next the neighbour's neighbour.</summary>
    private const int ReversibleMargin = 2;

    /// <summary>The rows the 9/7 vertical filter reads beyond an output row: four lifting steps.</summary>
    private const int IrreversibleMargin = 4;

    /// <summary>The 9/7 low-pass scale K.</summary>
    private const float LowScale = 1.230174105F;

    /// <summary>
    /// The 9/7 high-pass scale: 1/K from the synthesis equations, times the factor of two that the sub-band gain adds
    /// for each high-pass direction (T.800 E-4). The step sizes leave that gain out.
    /// </summary>
    private const float HighScale = 2F / LowScale;

    /// <summary>The first 9/7 lifting step, applied to the low-pass samples.</summary>
    private const float Delta = -0.443506852F;

    /// <summary>The second 9/7 lifting step, applied to the high-pass samples.</summary>
    private const float Gamma = -0.882911075F;

    /// <summary>The third 9/7 lifting step, applied to the low-pass samples.</summary>
    private const float Beta = 0.052980118F;

    /// <summary>The fourth 9/7 lifting step, applied to the high-pass samples.</summary>
    private const float Alpha = 1.586134342F;

    /// <summary>Gets the rows on each side of a block of output rows that the vertical filter reads (the 5/3 filter reads two, the 9/7 filter four).</summary>
    /// <param name="reversible">Whether the 5/3 filter is used.</param>
    /// <returns>The margin in rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RowMargin(bool reversible) => reversible ? ReversibleMargin : IrreversibleMargin;

    /// <summary>Rebuilds one row of a resolution from the horizontal low-pass and high-pass samples of that row (5/3).</summary>
    /// <param name="lows">The low-pass samples, changed in place.</param>
    /// <param name="highs">The high-pass samples, changed in place.</param>
    /// <param name="parity">1 when the row starts on an odd column.</param>
    /// <param name="output">Receives the row, as wide as both inputs together.</param>
    internal static void SynthesizeRow53(Span<int> lows, Span<int> highs, int parity, Span<int> output)
    {
        if (Lift53(lows, highs, lows.Length, highs.Length, parity, 1))
        {
            Interleave(lows, highs, parity, output);
            return;
        }

        output[0] = lows.Length == 1 ? lows[0] : highs[0];
    }

    /// <summary>Rebuilds one row of a resolution from the horizontal low-pass and high-pass samples of that row (9/7).</summary>
    /// <param name="lows">The low-pass samples, changed in place.</param>
    /// <param name="highs">The high-pass samples, changed in place.</param>
    /// <param name="parity">1 when the row starts on an odd column.</param>
    /// <param name="output">Receives the row, as wide as both inputs together.</param>
    internal static void SynthesizeRow97(Span<float> lows, Span<float> highs, int parity, Span<float> output)
    {
        if (Lift97(lows, highs, lows.Length, highs.Length, parity, 1))
        {
            Interleave(lows, highs, parity, output);
            return;
        }

        output[0] = lows.Length == 1 ? lows[0] : highs[0];
    }

    /// <summary>Runs the 5/3 vertical lifting over a window of rows, all columns at once.</summary>
    /// <param name="lows">The low-pass rows, one after another, changed in place.</param>
    /// <param name="highs">The high-pass rows, one after another, changed in place.</param>
    /// <param name="lowRows">The number of low-pass rows.</param>
    /// <param name="highRows">The number of high-pass rows.</param>
    /// <param name="parity">1 when the window's first row is a high-pass row.</param>
    /// <param name="width">The samples in a row.</param>
    internal static void LiftRows53(Span<int> lows, Span<int> highs, int lowRows, int highRows, int parity, int width) =>
        _ = Lift53(lows, highs, lowRows, highRows, parity, width);

    /// <summary>Runs the 9/7 vertical lifting over a window of rows, all columns at once.</summary>
    /// <param name="lows">The low-pass rows, one after another, changed in place.</param>
    /// <param name="highs">The high-pass rows, one after another, changed in place.</param>
    /// <param name="lowRows">The number of low-pass rows.</param>
    /// <param name="highRows">The number of high-pass rows.</param>
    /// <param name="parity">1 when the window's first row is a high-pass row.</param>
    /// <param name="width">The samples in a row.</param>
    internal static void LiftRows97(Span<float> lows, Span<float> highs, int lowRows, int highRows, int parity, int width) =>
        _ = Lift97(lows, highs, lowRows, highRows, parity, width);

    /// <summary>Runs the 5/3 lifting steps on one line of separated low-pass and high-pass samples.</summary>
    /// <param name="lows">The low-pass samples.</param>
    /// <param name="highs">The high-pass samples.</param>
    /// <param name="lowCount">The low-pass sample count.</param>
    /// <param name="highCount">The high-pass sample count.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate, so with a high-pass sample.</param>
    /// <param name="size">The values per sample: one for rows, the row width for columns.</param>
    /// <returns><see langword="true"/> when the line needs interleaving; a single sample does not.</returns>
    private static bool Lift53(Span<int> lows, Span<int> highs, int lowCount, int highCount, int parity, int size)
    {
        if (lowCount + highCount == 1)
        {
            if (parity == 1)
            {
                // A lone sample on an odd coordinate is a high-pass sample: halve it (equation F-7).
                foreach (ref var value in highs[..size])
                {
                    value /= Interleaved;
                }
            }

            return false;
        }

        var edge = parity == 0;
        Update<JpxReversibleLowStep, int>(lows, lowCount, highs, highCount, edge, size, 0);
        Update<JpxReversibleHighStep, int>(highs, highCount, lows, lowCount, !edge, size, 0);
        return true;
    }

    /// <summary>Runs the 9/7 scaling and lifting steps on one line of separated low-pass and high-pass samples.</summary>
    /// <param name="lows">The low-pass samples.</param>
    /// <param name="highs">The high-pass samples.</param>
    /// <param name="lowCount">The low-pass sample count.</param>
    /// <param name="highCount">The high-pass sample count.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate, so with a high-pass sample.</param>
    /// <param name="size">The values per sample: one for rows, the row width for columns.</param>
    /// <returns><see langword="true"/> when the line needs interleaving; a single sample is left as it is (T.800 F.3.8: a one-sample line is its own reconstruction).</returns>
    private static bool Lift97(Span<float> lows, Span<float> highs, int lowCount, int highCount, int parity, int size)
    {
        if (lowCount + highCount == 1)
        {
            return false;
        }

        Scale(lows[..(lowCount * size)], LowScale);
        Scale(highs[..(highCount * size)], HighScale);
        var edge = parity == 0;
        Update<JpxIrreversibleStep, float>(lows, lowCount, highs, highCount, edge, size, Delta);
        Update<JpxIrreversibleStep, float>(highs, highCount, lows, lowCount, !edge, size, Gamma);
        Update<JpxIrreversibleStep, float>(lows, lowCount, highs, highCount, edge, size, Beta);
        Update<JpxIrreversibleStep, float>(highs, highCount, lows, lowCount, !edge, size, Alpha);
        return true;
    }

    /// <summary>
    /// Applies a lifting step to every target sample from its two neighbours in the source, mirroring at the line ends.
    /// With <paramref name="leading"/> the neighbours of target <c>i</c> are source <c>i - 1</c> and <c>i</c>; otherwise
    /// <c>i</c> and <c>i + 1</c>.
    /// </summary>
    /// <typeparam name="TStep">The lifting step.</typeparam>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="target">The samples updated.</param>
    /// <param name="targetCount">The target sample count.</param>
    /// <param name="source">The neighbour samples.</param>
    /// <param name="sourceCount">The neighbour sample count.</param>
    /// <param name="leading">Whether the neighbours lead the target.</param>
    /// <param name="size">The values per sample.</param>
    /// <param name="factor">The step's lifting coefficient.</param>
    private static void Update<TStep, T>(Span<T> target, int targetCount, Span<T> source, int sourceCount, bool leading, int size, float factor)
        where TStep : IJpxLiftStep<T>
        where T : unmanaged
    {
        if (targetCount == 0 || sourceCount == 0)
        {
            return;
        }

        var shift = leading ? 1 : 0;
        var start = leading ? 1 : 0;
        var end = Math.Min(targetCount, sourceCount - 1 + shift);
        if (end > start)
        {
            // Every target in [start, end) has both neighbours inside the source: one vector run.
            var length = (end - start) * size;
            TStep.Apply(target.Slice(start * size, length), source.Slice((start - shift) * size, length), source.Slice((start - shift + 1) * size, length), factor);
        }

        // The targets before and after the run mirror the source at the line ends.
        for (var i = 0; i < Math.Min(start, targetCount); i++)
        {
            UpdateEdge<TStep, T>(target, source, sourceCount, i, shift, size, factor);
        }

        for (var i = Math.Max(end, start); i < targetCount; i++)
        {
            UpdateEdge<TStep, T>(target, source, sourceCount, i, shift, size, factor);
        }
    }

    /// <summary>Applies a lifting step to one target sample whose neighbours mirror at a line end.</summary>
    /// <typeparam name="TStep">The lifting step.</typeparam>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="target">The samples updated.</param>
    /// <param name="source">The neighbour samples.</param>
    /// <param name="sourceCount">The neighbour sample count.</param>
    /// <param name="index">The target sample.</param>
    /// <param name="shift">1 when the neighbours lead the target, otherwise 0.</param>
    /// <param name="size">The values per sample.</param>
    /// <param name="factor">The step's lifting coefficient.</param>
    private static void UpdateEdge<TStep, T>(Span<T> target, Span<T> source, int sourceCount, int index, int shift, int size, float factor)
        where TStep : IJpxLiftStep<T>
        where T : unmanaged
    {
        var first = Math.Clamp(index - shift, 0, sourceCount - 1);
        var second = Math.Clamp(index - shift + 1, 0, sourceCount - 1);
        TStep.Apply(target.Slice(index * size, size), source.Slice(first * size, size), source.Slice(second * size, size), factor);
    }

    /// <summary>Multiplies samples by a scale.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="scale">The scale.</param>
    private static void Scale(Span<float> samples, float scale)
    {
        ref var start = ref MemoryMarshal.GetReference(samples);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var factor = Vector256.Create(scale);
            for (; i <= samples.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                (Vector256.LoadUnsafe(ref start, (nuint)i) * factor).StoreUnsafe(ref start, (nuint)i);
            }
        }

        for (; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    /// <summary>Interleaves a row's low-pass and high-pass samples.</summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="lows">The low-pass samples.</param>
    /// <param name="highs">The high-pass samples.</param>
    /// <param name="parity">1 when the row starts with a high-pass sample.</param>
    /// <param name="output">Receives the interleaved row, as long as both halves together.</param>
    private static void Interleave<T>(ReadOnlySpan<T> lows, ReadOnlySpan<T> highs, int parity, Span<T> output)
    {
        ref var target = ref MemoryMarshal.GetReference(output[..(lows.Length + highs.Length)]);
        ref var low = ref MemoryMarshal.GetReference(lows);
        ref var high = ref MemoryMarshal.GetReference(highs);
        for (var i = 0; i < lows.Length; i++)
        {
            Unsafe.Add(ref target, (Interleaved * i) + parity) = Unsafe.Add(ref low, i);
        }

        for (var i = 0; i < highs.Length; i++)
        {
            Unsafe.Add(ref target, (Interleaved * i) + 1 - parity) = Unsafe.Add(ref high, i);
        }
    }
}
