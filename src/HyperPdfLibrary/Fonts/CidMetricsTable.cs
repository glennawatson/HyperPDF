// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The /W or /W2 metrics of a CID font, flattened into arrays indexed by CID so a lookup is one read. When runs
/// overlap the first one wins. CIDs above 65535 are ignored: the predefined CMaps and Adobe's character collections
/// stay within 16 bits (ISO 32000-2, 9.7.5).
/// </summary>
[DebuggerDisplay("CidMetricsTable: {_advances.Length} CIDs")]
internal sealed class CidMetricsTable
{
    /// <summary>The largest CID.</summary>
    private const int MaxCid = ushort.MaxValue;

    /// <summary>The numbers in one /W2 entry: w1y, vx and vy.</summary>
    private const int VerticalValues = 3;

    /// <summary>The advance of each CID; NaN when no run covers it.</summary>
    private readonly float[] _advances;

    /// <summary>The vertical origin x of each CID, for /W2.</summary>
    private readonly float[] _originsX;

    /// <summary>The vertical origin y of each CID, for /W2.</summary>
    private readonly float[] _originsY;

    /// <summary>Initializes a new instance of the <see cref="CidMetricsTable"/> class.</summary>
    /// <param name="entries">The entries in array order.</param>
    /// <param name="vertical">Whether the entries hold vertical origins.</param>
    private CidMetricsTable(List<CidMetric> entries, bool vertical)
    {
        var last = -1;
        foreach (var entry in entries)
        {
            last = Math.Max(last, Math.Min(entry.Last, MaxCid));
        }

        _advances = new float[last + 1];
        _advances.AsSpan().Fill(float.NaN);
        _originsX = vertical ? new float[last + 1] : [];
        _originsY = vertical ? new float[last + 1] : [];
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            Fill(entries[i], vertical);
        }
    }

    /// <summary>Gets an empty table.</summary>
    internal static CidMetricsTable Empty { get; } = new([], false);

    /// <summary>Reads a /W array: <c>c [w1 w2 ...]</c> or <c>cFirst cLast w</c> entries.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The table.</returns>
    internal static CidMetricsTable ReadWidths(PdfArray? array) => array is null ? Empty : new(Parse(array, 1), false);

    /// <summary>Reads a /W2 array: <c>c [w1y vx vy ...]</c> or <c>cFirst cLast w1y vx vy</c> entries.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The table.</returns>
    internal static CidMetricsTable ReadVertical(PdfArray? array) => array is null ? Empty : new(Parse(array, VerticalValues), true);

    /// <summary>Finds a CID's advance.</summary>
    /// <param name="cid">The CID.</param>
    /// <param name="advance">The advance in glyph units.</param>
    /// <returns><see langword="true"/> when a run covers the CID.</returns>
    internal bool TryGetAdvance(int cid, out float advance)
    {
        advance = (uint)cid < (uint)_advances.Length ? _advances[cid] : float.NaN;
        return !float.IsNaN(advance);
    }

    /// <summary>Finds a CID's vertical metrics.</summary>
    /// <param name="cid">The CID.</param>
    /// <param name="metric">The advance and origin in glyph units.</param>
    /// <returns><see langword="true"/> when a run covers the CID.</returns>
    internal bool TryGetVertical(int cid, out CidMetric metric)
    {
        if (!TryGetAdvance(cid, out var advance) || _originsX.Length == 0)
        {
            metric = default;
            return false;
        }

        metric = new(cid, cid, advance, _originsX[cid], _originsY[cid]);
        return true;
    }

    /// <summary>Parses a /W or /W2 array (ISO 32000-2, 9.7.4.3 and 9.7.4.4).</summary>
    /// <param name="array">The array.</param>
    /// <param name="values">The numbers per CID.</param>
    /// <returns>The entries in order.</returns>
    private static List<CidMetric> Parse(PdfArray array, int values)
    {
        var entries = new List<CidMetric>();
        var reader = new CidMetricReader(entries, values);
        var damaged = false;
        for (var i = 0; i < array.Count; i++)
        {
            // An array where a CID is expected is skipped and reading resumes, so one bad item does not drop the widths after it.
            damaged |= !reader.Add(array.Get(i));
        }

        if (damaged || reader.IsMidEntry)
        {
            PdfOpenContext.Report(array.Owner?.Context, PdfDiagnosticCode.BadFontWidths, "A font's /W or /W2 widths are malformed; the usable part is kept.", 0, -1);
        }

        return entries;
    }

    /// <summary>Writes an entry into the arrays.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="vertical">Whether origins are stored.</param>
    private void Fill(CidMetric entry, bool vertical)
    {
        var first = Math.Max(entry.First, 0);
        var last = Math.Min(entry.Last, _advances.Length - 1);
        for (var cid = first; cid <= last; cid++)
        {
            _advances[cid] = entry.Advance;
            if (!vertical)
            {
                continue;
            }

            _originsX[cid] = entry.OriginX;
            _originsY[cid] = entry.OriginY;
        }
    }
}
