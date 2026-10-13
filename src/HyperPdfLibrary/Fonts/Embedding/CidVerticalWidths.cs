// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Writes the <c>/DW2</c> and <c>/W2</c> entries of a CIDFont (ISO 32000-2, 9.7.4.3). A glyph's position vector is
/// (<c>w0</c> / 2, <c>vy</c>) unless <c>/W2</c> says otherwise, so only glyphs whose displacement or origin height differ
/// from <c>/DW2</c> get an entry, and each entry states the position vector in full.
/// </summary>
internal static class CidVerticalWidths
{
    /// <summary>The origin height <c>/DW2</c> has when it is absent.</summary>
    internal const float StandardOriginY = 880F;

    /// <summary>The displacement <c>/DW2</c> has when it is absent.</summary>
    internal const float StandardAdvance = -1000F;

    /// <summary>The shortest run of equal entries written as a range, which is shorter than listing them.</summary>
    private const int MinimumRange = 4;

    /// <summary>The entries of <c>/DW2</c>: the origin height and the displacement.</summary>
    private const int DefaultEntries = 2;

    /// <summary>A glyph's position vector is half its horizontal width across.</summary>
    private const float Half = 0.5F;

    /// <summary>The entries of a <c>/W2</c> array before it grows.</summary>
    private const int InitialEntries = 8;

    /// <summary>Chooses <c>/DW2</c>: the most common origin height and displacement, so most CIDs need no entry.</summary>
    /// <param name="metrics">The vertical metrics of the CIDs shown.</param>
    /// <returns>The index of a CID that has the most common pair; zero when there are none.</returns>
    internal static int ChooseDefault(VerticalMetrics metrics)
    {
        var counts = new Dictionary<VerticalPair, int>();
        var best = 0;
        var bestCount = 0;
        for (var i = 0; i < metrics.Advances.Length; i++)
        {
            var pair = new VerticalPair(metrics.OriginsY[i], metrics.Advances[i]);
            var count = counts.GetValueOrDefault(pair) + 1;
            counts[pair] = count;
            if (count <= bestCount)
            {
                continue;
            }

            best = i;
            bestCount = count;
        }

        return best;
    }

    /// <summary>Creates the <c>/W2</c> array for the CIDs that differ from the default.</summary>
    /// <param name="owner">The open document the array belongs to, or <see langword="null"/>.</param>
    /// <param name="cids">The CIDs, ascending.</param>
    /// <param name="widths">The horizontal width <c>w0</c> of each CID.</param>
    /// <param name="metrics">The vertical metrics of each CID.</param>
    /// <param name="defaultAt">The index of a CID with the default pair.</param>
    /// <returns>The array.</returns>
    internal static PdfArray Create(PdfObjectStore? owner, ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, VerticalMetrics metrics, int defaultAt)
    {
        var array = new PdfArray(owner, InitialEntries);
        var standard = new VerticalPair(metrics.OriginsY[defaultAt], metrics.Advances[defaultAt]);
        var i = 0;
        while (i < cids.Length)
        {
            if (Pair(metrics, i) == standard)
            {
                i++;
                continue;
            }

            var repeat = RunOfEqual(cids, widths, metrics, i);
            if (repeat < MinimumRange)
            {
                i = AddList(owner, array, cids, widths, metrics, i, standard);
                continue;
            }

            array.Add(PdfValue.FromInteger(cids[i]));
            array.Add(PdfValue.FromInteger(cids[i + repeat - 1]));
            AddVector(array, widths[i], metrics, i);
            i += repeat;
        }

        return array;
    }

    /// <summary>Gets the <c>/DW2</c> array.</summary>
    /// <param name="owner">The open document the array belongs to, or <see langword="null"/>.</param>
    /// <param name="metrics">The vertical metrics of each CID.</param>
    /// <param name="defaultAt">The index of a CID with the default pair.</param>
    /// <returns>The array <c>[vy w1]</c>, or <see langword="null"/> when it is the standard default.</returns>
    internal static PdfArray? CreateDefault(PdfObjectStore? owner, VerticalMetrics metrics, int defaultAt)
    {
        var originY = metrics.OriginsY[defaultAt];
        var advance = metrics.Advances[defaultAt];
        if (CidWidths.Same(originY, StandardOriginY) && CidWidths.Same(advance, StandardAdvance))
        {
            return null;
        }

        var array = new PdfArray(owner, DefaultEntries);
        array.Add(PdfNumber.ToValue(originY));
        array.Add(PdfNumber.ToValue(advance));
        return array;
    }

    /// <summary>Gets the origin height and displacement of a CID.</summary>
    /// <param name="metrics">The vertical metrics.</param>
    /// <param name="index">The CID's index.</param>
    /// <returns>The pair.</returns>
    private static VerticalPair Pair(VerticalMetrics metrics, int index) => new(metrics.OriginsY[index], metrics.Advances[index]);

    /// <summary>Adds <c>w1 vx vy</c> for a CID.</summary>
    /// <param name="array">The array.</param>
    /// <param name="width">The CID's horizontal width.</param>
    /// <param name="metrics">The vertical metrics.</param>
    /// <param name="index">The CID's index.</param>
    private static void AddVector(PdfArray array, float width, VerticalMetrics metrics, int index)
    {
        array.Add(PdfNumber.ToValue(metrics.Advances[index]));
        array.Add(PdfNumber.ToValue(MathF.Round(width * Half)));
        array.Add(PdfNumber.ToValue(metrics.OriginsY[index]));
    }

    /// <summary>Counts the consecutive CIDs from a start that share one entry.</summary>
    /// <param name="cids">The CIDs.</param>
    /// <param name="widths">Their horizontal widths.</param>
    /// <param name="metrics">Their vertical metrics.</param>
    /// <param name="start">The first CID to count.</param>
    /// <returns>The number of CIDs in the run.</returns>
    private static int RunOfEqual(ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, VerticalMetrics metrics, int start)
    {
        var end = start + 1;
        while (end < cids.Length && cids[end] == cids[end - 1] + 1 && CidWidths.Same(widths[end], widths[start]) && Pair(metrics, end) == Pair(metrics, start))
        {
            end++;
        }

        return end - start;
    }

    /// <summary>Adds a list of entries for consecutive CIDs, stopping before a long run of equal entries, a default CID or a gap.</summary>
    /// <param name="owner">The open document the array belongs to, or <see langword="null"/>.</param>
    /// <param name="array">The <c>/W2</c> array.</param>
    /// <param name="cids">The CIDs.</param>
    /// <param name="widths">Their horizontal widths.</param>
    /// <param name="metrics">Their vertical metrics.</param>
    /// <param name="start">The first CID of the list.</param>
    /// <param name="standard">The default pair.</param>
    /// <returns>The index after the last CID added.</returns>
    private static int AddList(PdfObjectStore? owner, PdfArray array, ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, VerticalMetrics metrics, int start, VerticalPair standard)
    {
        var list = new PdfArray(owner, InitialEntries);
        AddVector(list, widths[start], metrics, start);
        var i = start + 1;
        while (i < cids.Length && cids[i] == cids[i - 1] + 1 && Pair(metrics, i) != standard && RunOfEqual(cids, widths, metrics, i) < MinimumRange)
        {
            AddVector(list, widths[i], metrics, i);
            i++;
        }

        array.Add(PdfValue.FromInteger(cids[start]));
        array.Add(PdfValue.FromArray(list));
        return i;
    }
}
