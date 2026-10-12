// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A CIDFont's /W and /DW as veraPDF reads them: single widths win over ranges, the first range holding a CID wins, a
/// malformed entry ends the array, and /DW defaults to 1000.
/// </summary>
[DebuggerDisplay("PdfACidWidths: {_singles.Count} single, {_ranges.Count} ranges")]
internal sealed class PdfACidWidths
{
    /// <summary>The default width without /DW.</summary>
    private const double DefaultWidth = 1000;

    /// <summary>The items of a first-CID, width-array entry.</summary>
    private const int ListEntryLength = 2;

    /// <summary>The items of a first-CID, last-CID, width entry.</summary>
    private const int RangeEntryLength = 3;

    /// <summary>The position of the width in a range entry.</summary>
    private const int RangeWidthOffset = 2;

    /// <summary>The widths of single CIDs.</summary>
    private readonly Dictionary<int, double> _singles = [];

    /// <summary>The CID ranges with one width each.</summary>
    private readonly List<PdfACidWidthRange> _ranges = [];

    /// <summary>Initializes a new instance of the <see cref="PdfACidWidths"/> class.</summary>
    /// <param name="cidFont">The CIDFont dictionary.</param>
    internal PdfACidWidths(PdfDictionary cidFont)
    {
        Default = cidFont.Get(KnownName.DW) is { IsNumber: true } width ? width.AsNumber() : DefaultWidth;
        if (cidFont.GetArray(KnownName.W) is { } entries)
        {
            Read(entries);
        }
    }

    /// <summary>Gets the default width.</summary>
    internal double Default { get; }

    /// <summary>Gets a CID's width.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The width, or the default.</returns>
    internal double Width(int cid)
    {
        if (_singles.TryGetValue(cid, out var width))
        {
            return width;
        }

        foreach (var (first, last, rangeWidth) in _ranges)
        {
            if (cid >= first && cid <= last)
            {
                return rangeWidth;
            }
        }

        return Default;
    }

    /// <summary>Reads the /W entries.</summary>
    /// <param name="entries">The array.</param>
    private void Read(PdfArray entries)
    {
        var i = 0;
        while (i + 1 < entries.Count && entries.Get(i).IsNumber)
        {
            var first = (int)entries.Get(i).AsInteger();
            var next = entries.Get(i + 1);
            if (next.Kind != PdfKind.Integer)
            {
                if (next.AsArray() is { } widths)
                {
                    AddSingles(first, widths);
                }

                i += ListEntryLength;
                continue;
            }

            var width = entries.Get(i + RangeWidthOffset);
            if (!width.IsNumber)
            {
                return;
            }

            _ranges.Add(new(first, (int)next.AsInteger(), width.AsNumber()));
            i += RangeEntryLength;
        }
    }

    /// <summary>Adds the widths of consecutive CIDs.</summary>
    /// <param name="first">The first CID.</param>
    /// <param name="widths">The widths.</param>
    private void AddSingles(int first, PdfArray widths)
    {
        for (var j = 0; j < widths.Count; j++)
        {
            if (widths.Get(j) is { IsNumber: true } width)
            {
                _singles[first + j] = width.AsNumber();
            }
        }
    }
}
