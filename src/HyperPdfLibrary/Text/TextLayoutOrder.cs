// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Puts the runs of a page in the order they sit on it: bands of runs that share a baseline from the top of the page down,
/// and the runs of a band from left to right. A band starts at the highest baseline left and takes every run whose baseline
/// is within half an em of it, the same share that makes runs one line. Only pages of plain horizontal left-to-right text
/// are sorted; a page with rotated or vertical text keeps the order it is drawn in.
/// </summary>
internal static class TextLayoutOrder
{
    /// <summary>The fewest runs that can be out of order.</summary>
    private const int MinimumRuns = 2;

    /// <summary>The share of the first run's em that a baseline may be below the band's and still be in the band.</summary>
    private const float BandShare = 0.5F;

    /// <summary>The largest sine of the angle between a run's direction and the page's horizontal, about three degrees.</summary>
    private const float MaxDirectionSine = 0.05F;

    /// <summary>Sorts runs into layout order.</summary>
    /// <param name="runs">The runs, in drawing order; they are reordered in place.</param>
    internal static void Sort(List<TextRun> runs)
    {
        var count = runs.Count;
        if (count < MinimumRuns)
        {
            return;
        }

        var frames = ArrayPool<TextRunFrame>.Shared.Rent(count);
        var order = ArrayPool<int>.Shared.Rent(count);
        var sorted = ArrayPool<TextRun>.Shared.Rent(count);
        try
        {
            if (!TryFrames(runs, frames))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                order[i] = i;
            }

            order.AsSpan(0, count).Sort(new ByBaseline(frames));
            Bands(CollectionsMarshal.AsSpan(runs), frames, order.AsSpan(0, count), sorted);
            sorted.AsSpan(0, count).CopyTo(CollectionsMarshal.AsSpan(runs));
        }
        finally
        {
            ArrayPool<TextRunFrame>.Shared.Return(frames);
            ArrayPool<int>.Shared.Return(order);
            ArrayPool<TextRun>.Shared.Return(sorted, true);
        }
    }

    /// <summary>Finds the frame of each run, and whether every run is plain horizontal left-to-right text.</summary>
    /// <param name="runs">The runs.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <returns><see langword="true"/> when every run is horizontal and written left to right.</returns>
    private static bool TryFrames(List<TextRun> runs, TextRunFrame[] frames)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            frames[i] = TextRunFrame.Of(runs[i]);
            if (runs[i].Font.IsVertical || frames[i].Axis.X <= 0 || MathF.Abs(frames[i].Axis.Y) > MaxDirectionSine)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Walks the runs from the top of the page down, writing each band's runs from left to right.</summary>
    /// <param name="runs">The runs in drawing order.</param>
    /// <param name="frames">The frame of each run.</param>
    /// <param name="order">The run indexes sorted by baseline, highest first.</param>
    /// <param name="sorted">Receives the runs in layout order.</param>
    private static void Bands(ReadOnlySpan<TextRun> runs, TextRunFrame[] frames, Span<int> order, TextRun[] sorted)
    {
        var next = 0;
        var start = 0;
        while (start < order.Length)
        {
            var top = frames[order[start]];
            var end = start + 1;
            while (end < order.Length && top.Start.Y - frames[order[end]].Start.Y <= BandShare * top.SafeEm)
            {
                end++;
            }

            order[start..end].Sort(new ByStart(frames));
            for (var i = start; i < end; i++)
            {
                sorted[next] = runs[order[i]];
                next++;
            }

            start = end;
        }
    }

    /// <summary>Orders run indexes by baseline, highest first, and by drawing order for equal baselines.</summary>
    /// <param name="Frames">The frame of each run.</param>
    private readonly record struct ByBaseline(TextRunFrame[] Frames) : IComparer<int>
    {
        /// <inheritdoc/>
        public int Compare(int x, int y)
        {
            var byBaseline = Frames[y].Start.Y.CompareTo(Frames[x].Start.Y);
            return byBaseline != 0 ? byBaseline : x.CompareTo(y);
        }
    }

    /// <summary>Orders run indexes from left to right, and by drawing order for equal starts.</summary>
    /// <param name="Frames">The frame of each run.</param>
    private readonly record struct ByStart(TextRunFrame[] Frames) : IComparer<int>
    {
        /// <inheritdoc/>
        public int Compare(int x, int y)
        {
            var byStart = Frames[x].Start.X.CompareTo(Frames[y].Start.X);
            return byStart != 0 ? byStart : x.CompareTo(y);
        }
    }
}
