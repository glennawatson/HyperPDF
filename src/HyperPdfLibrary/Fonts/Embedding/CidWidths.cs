// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Writes the <c>/DW</c> and <c>/W</c> entries of a CIDFont (ISO 32000-2, 9.7.4.3).</summary>
internal static class CidWidths
{
    /// <summary>The width a CIDFont has when it has no <c>/DW</c>.</summary>
    internal const float StandardDefault = 1000F;

    /// <summary>The shortest run of equal widths written as a range, which is shorter than listing them.</summary>
    private const int MinimumRange = 4;

    /// <summary>The entries of a <c>/W</c> array before it grows.</summary>
    private const int InitialEntries = 8;

    /// <summary>How far apart two widths may be and still count as equal, in thousandths of an em.</summary>
    private const float Tolerance = 0.0005F;

    /// <summary>Chooses the default width: the most common one, so most CIDs need no entry.</summary>
    /// <param name="widths">The widths of the CIDs shown.</param>
    /// <returns>The most common width; the standard default when there are no widths.</returns>
    internal static float ChooseDefault(ReadOnlySpan<float> widths)
    {
        if (widths.IsEmpty)
        {
            return StandardDefault;
        }

        var counts = new Dictionary<float, int>();
        var best = widths[0];
        var bestCount = 0;
        foreach (var width in widths)
        {
            var count = counts.GetValueOrDefault(width) + 1;
            counts[width] = count;
            if (count < bestCount || (count == bestCount && !Same(width, StandardDefault)))
            {
                continue;
            }

            best = width;
            bestCount = count;
        }

        return best;
    }

    /// <summary>Creates the <c>/W</c> array for the CIDs whose width is not the default.</summary>
    /// <param name="owner">The open document the array belongs to, or <see langword="null"/>.</param>
    /// <param name="cids">The CIDs, ascending.</param>
    /// <param name="widths">The width of each CID.</param>
    /// <param name="defaultWidth">The <c>/DW</c> width.</param>
    /// <returns>The array.</returns>
    internal static PdfArray Create(PdfObjectStore? owner, ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, float defaultWidth)
    {
        var array = new PdfArray(owner, InitialEntries);
        var i = 0;
        while (i < cids.Length)
        {
            if (Same(widths[i], defaultWidth))
            {
                i++;
                continue;
            }

            var repeat = RunOfEqual(cids, widths, i);
            if (repeat < MinimumRange)
            {
                i = AddList(owner, array, cids, widths, i, defaultWidth);
                continue;
            }

            array.Add(PdfValue.FromInteger(cids[i]));
            array.Add(PdfValue.FromInteger(cids[i + repeat - 1]));
            array.Add(PdfNumber.ToValue(widths[i]));
            i += repeat;
        }

        return array;
    }

    /// <summary>Determines whether two widths are the same.</summary>
    /// <param name="a">The first width.</param>
    /// <param name="b">The second width.</param>
    /// <returns><see langword="true"/> when they differ by less than the tolerance.</returns>
    internal static bool Same(float a, float b) => MathF.Abs(a - b) < Tolerance;

    /// <summary>Counts the consecutive CIDs from a start that share one width.</summary>
    /// <param name="cids">The CIDs.</param>
    /// <param name="widths">Their widths.</param>
    /// <param name="start">The first CID to count.</param>
    /// <returns>The number of CIDs in the run.</returns>
    private static int RunOfEqual(ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, int start)
    {
        var end = start + 1;
        while (end < cids.Length && cids[end] == cids[end - 1] + 1 && Same(widths[end], widths[start]))
        {
            end++;
        }

        return end - start;
    }

    /// <summary>Adds a list of widths for consecutive CIDs, stopping before a long run of equal widths or a gap.</summary>
    /// <param name="owner">The open document the array belongs to, or <see langword="null"/>.</param>
    /// <param name="array">The <c>/W</c> array.</param>
    /// <param name="cids">The CIDs.</param>
    /// <param name="widths">Their widths.</param>
    /// <param name="start">The first CID of the list.</param>
    /// <param name="defaultWidth">The <c>/DW</c> width.</param>
    /// <returns>The index after the last CID added.</returns>
    private static int AddList(PdfObjectStore? owner, PdfArray array, ReadOnlySpan<int> cids, ReadOnlySpan<float> widths, int start, float defaultWidth)
    {
        var list = new PdfArray(owner, InitialEntries);
        list.Add(PdfNumber.ToValue(widths[start]));
        var i = start + 1;
        while (i < cids.Length && cids[i] == cids[i - 1] + 1 && !Same(widths[i], defaultWidth) && RunOfEqual(cids, widths, i) < MinimumRange)
        {
            list.Add(PdfNumber.ToValue(widths[i]));
            i++;
        }

        array.Add(PdfValue.FromInteger(cids[start]));
        array.Add(PdfValue.FromArray(list));
        return i;
    }
}
