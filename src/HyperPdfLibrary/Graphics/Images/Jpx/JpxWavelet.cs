// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The inverse discrete wavelet transform (ISO 15444-1 annex F) by lifting: the 5/3 reversible filter on integers and the
/// 9/7 irreversible filter on floats, scaled as PDFium scales it. Each resolution is rebuilt with a horizontal pass over
/// its rows and then a vertical pass over strips of columns. Within a line the low-pass and high-pass samples are kept
/// apart while lifting, so each step is a vector operation over runs of samples, and interleaved at the end.
/// </summary>
internal static class JpxWavelet
{
    /// <summary>The columns processed together by the vertical pass.</summary>
    internal const int StripWidth = 32;

    /// <summary>The samples of each low-pass and high-pass pair; also the halving of a lone high-pass sample.</summary>
    private const int Interleaved = 2;

    /// <summary>The 9/7 low-pass scale K.</summary>
    private const float LowScale = 1.230174105F;

    /// <summary>
    /// The 9/7 high-pass scale: 2/K as PDFium rounds it. The factor of two stands in for the sub-band gain that the
    /// irreversible step sizes leave out.
    /// </summary>
    private const float HighScale = 1.625732422F;

    /// <summary>The first 9/7 lifting step, applied to the low-pass samples.</summary>
    private const float Delta = -0.443506852F;

    /// <summary>The second 9/7 lifting step, applied to the high-pass samples.</summary>
    private const float Gamma = -0.882911075F;

    /// <summary>The third 9/7 lifting step, applied to the low-pass samples.</summary>
    private const float Beta = 0.052980118F;

    /// <summary>The fourth 9/7 lifting step, applied to the high-pass samples.</summary>
    private const float Alpha = 1.586134342F;

    /// <summary>Rebuilds a tile-component from its sub-bands.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The coefficients, sub-bands in place; replaced by the samples.</param>
    /// <param name="scratch">Scratch space of at least <see cref="ScratchLength"/> elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Inverse(JpxTile tile, in JpxTileComponent component, int[] buffer, int[] scratch) =>
        Inverse(tile, component, buffer, scratch, 0);

    /// <summary>Rebuilds only the wavelet levels needed for a reduced-resolution tile.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="component">The component.</param>
    /// <param name="buffer">The coefficients and output samples.</param>
    /// <param name="scratch">The wavelet scratch.</param>
    /// <param name="reductionLevels">The finest levels to leave out.</param>
    internal static void Inverse(JpxTile tile, in JpxTileComponent component, int[] buffer, int[] scratch, int reductionLevels)
    {
        var stride = component.Area.Reduce(reductionLevels).Width;
        for (var r = 1; r <= component.Levels - reductionLevels; r++)
        {
            var area = tile.Resolutions[component.FirstResolution + r].Area;
            var low = tile.Resolutions[component.FirstResolution + r - 1].Area;
            var shape = new LevelShape(area.Width, area.Height, low.Width, low.Height, area.X0 & 1, area.Y0 & 1);
            if (shape.Width == 0 || shape.Height == 0)
            {
                continue;
            }

            if (component.Reversible)
            {
                Horizontal53(buffer, stride, shape, scratch);
                Vertical53(buffer, stride, shape, scratch);
            }
            else
            {
                var samples = MemoryMarshal.Cast<int, float>(buffer.AsSpan());
                var work = MemoryMarshal.Cast<int, float>(scratch.AsSpan());
                Horizontal97(samples, stride, shape, work);
                Vertical97(samples, stride, shape, work);
            }
        }
    }

    /// <summary>Gets the scratch elements a tile-component's transform needs.</summary>
    /// <param name="width">The tile-component width.</param>
    /// <param name="height">The tile-component height.</param>
    /// <returns>The number of elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ScratchLength(int width, int height) => Math.Max(width, height * StripWidth);

    /// <summary>Runs the 5/3 horizontal pass over every row of a resolution.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="shape">The resolution's shape.</param>
    /// <param name="scratch">Scratch space for one row.</param>
    private static void Horizontal53(int[] buffer, int stride, in LevelShape shape, int[] scratch)
    {
        var line = scratch.AsSpan(0, shape.Width);
        for (var y = 0; y < shape.Height; y++)
        {
            var row = buffer.AsSpan(y * stride, shape.Width);
            var lows = row[..shape.LowWidth];
            var highs = row[shape.LowWidth..];
            if (!Lift53(lows, highs, shape.LowWidth, shape.Width - shape.LowWidth, shape.ColumnParity, 1))
            {
                continue;
            }

            Interleave(lows, highs, shape.ColumnParity, line);
            line.CopyTo(row);
        }
    }

    /// <summary>Runs the 5/3 vertical pass over strips of columns of a resolution.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="shape">The resolution's shape.</param>
    /// <param name="scratch">Scratch space for one strip.</param>
    private static void Vertical53(int[] buffer, int stride, in LevelShape shape, int[] scratch)
    {
        var highCount = shape.Height - shape.LowHeight;
        for (var x = 0; x < shape.Width; x += StripWidth)
        {
            var width = Math.Min(StripWidth, shape.Width - x);
            var work = scratch.AsSpan(0, shape.Height * width);
            Gather(buffer, stride, x, width, shape.Height, work);
            var lows = work[..(shape.LowHeight * width)];
            var highs = work[(shape.LowHeight * width)..];
            var changed = Lift53(lows, highs, shape.LowHeight, highCount, shape.RowParity, width);
            Scatter(work, new(shape.LowHeight, highCount, changed ? shape.RowParity : -1, width), buffer.AsSpan(x), stride);
        }
    }

    /// <summary>Runs the 9/7 horizontal pass over every row of a resolution.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="shape">The resolution's shape.</param>
    /// <param name="scratch">Scratch space for one row.</param>
    private static void Horizontal97(Span<float> buffer, int stride, in LevelShape shape, Span<float> scratch)
    {
        var line = scratch[..shape.Width];
        for (var y = 0; y < shape.Height; y++)
        {
            var row = buffer.Slice(y * stride, shape.Width);
            var lows = row[..shape.LowWidth];
            var highs = row[shape.LowWidth..];
            if (!Lift97(lows, highs, shape.LowWidth, shape.Width - shape.LowWidth, shape.ColumnParity, 1))
            {
                continue;
            }

            Interleave(lows, highs, shape.ColumnParity, line);
            line.CopyTo(row);
        }
    }

    /// <summary>Runs the 9/7 vertical pass over strips of columns of a resolution.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="shape">The resolution's shape.</param>
    /// <param name="scratch">Scratch space for one strip.</param>
    private static void Vertical97(Span<float> buffer, int stride, in LevelShape shape, Span<float> scratch)
    {
        var highCount = shape.Height - shape.LowHeight;
        for (var x = 0; x < shape.Width; x += StripWidth)
        {
            var width = Math.Min(StripWidth, shape.Width - x);
            var work = scratch[..(shape.Height * width)];
            Gather(buffer, stride, x, width, shape.Height, work);
            var lows = work[..(shape.LowHeight * width)];
            var highs = work[(shape.LowHeight * width)..];
            var changed = Lift97(lows, highs, shape.LowHeight, highCount, shape.RowParity, width);
            Scatter(work, new(shape.LowHeight, highCount, changed ? shape.RowParity : -1, width), buffer[x..], stride);
        }
    }

    /// <summary>Runs the 5/3 lifting steps on one line of separated low-pass and high-pass samples.</summary>
    /// <param name="lows">The low-pass samples.</param>
    /// <param name="highs">The high-pass samples.</param>
    /// <param name="lowCount">The low-pass sample count.</param>
    /// <param name="highCount">The high-pass sample count.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate, so with a high-pass sample.</param>
    /// <param name="size">The values per sample: one for rows, the strip width for columns.</param>
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
    /// <param name="size">The values per sample: one for rows, the strip width for columns.</param>
    /// <returns><see langword="true"/> when the line needs interleaving; a single sample is left as it is, as in PDFium.</returns>
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

    /// <summary>Copies a strip of columns into scratch, one strip row after another.</summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="x">The first column.</param>
    /// <param name="width">The strip width.</param>
    /// <param name="height">The rows.</param>
    /// <param name="work">Receives the strip.</param>
    private static void Gather<T>(ReadOnlySpan<T> buffer, int stride, int x, int width, int height, Span<T> work)
    {
        for (var y = 0; y < height; y++)
        {
            buffer.Slice((y * stride) + x, width).CopyTo(work.Slice(y * width, width));
        }
    }

    /// <summary>Writes a lifted strip back, interleaving its low-pass and high-pass rows.</summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="work">The strip: its low-pass rows, then its high-pass rows.</param>
    /// <param name="strip">The strip's shape.</param>
    /// <param name="buffer">The tile-component from the strip's first column.</param>
    /// <param name="stride">The tile-component width.</param>
    private static void Scatter<T>(ReadOnlySpan<T> work, in StripShape strip, Span<T> buffer, int stride)
    {
        var width = strip.Width;
        var step = strip.Parity < 0 ? 1 : Interleaved;
        var lowStart = Math.Max(strip.Parity, 0);
        var highStart = strip.Parity < 0 ? strip.LowCount : 1 - strip.Parity;
        var highs = work[(strip.LowCount * width)..];
        for (var i = 0; i < strip.LowCount; i++)
        {
            work.Slice(i * width, width).CopyTo(buffer.Slice(((step * i) + lowStart) * stride, width));
        }

        for (var i = 0; i < strip.HighCount; i++)
        {
            highs.Slice(i * width, width).CopyTo(buffer.Slice(((step * i) + highStart) * stride, width));
        }
    }

    /// <summary>The shape of a strip of columns for the vertical pass.</summary>
    /// <param name="LowCount">The low-pass rows.</param>
    /// <param name="HighCount">The high-pass rows.</param>
    /// <param name="Parity">1 when the first row is high-pass, 0 when low-pass, -1 to write the rows back unmoved.</param>
    /// <param name="Width">The strip width.</param>
    private readonly record struct StripShape(int LowCount, int HighCount, int Parity, int Width);

    /// <summary>The shape of one resolution level's rebuild.</summary>
    /// <param name="Width">The resolution width.</param>
    /// <param name="Height">The resolution height.</param>
    /// <param name="LowWidth">The low-pass columns: the next lower resolution's width.</param>
    /// <param name="LowHeight">The low-pass rows: the next lower resolution's height.</param>
    /// <param name="ColumnParity">1 when the resolution starts on an odd column.</param>
    /// <param name="RowParity">1 when the resolution starts on an odd row.</param>
    private readonly record struct LevelShape(int Width, int Height, int LowWidth, int LowHeight, int ColumnParity, int RowParity);
}
