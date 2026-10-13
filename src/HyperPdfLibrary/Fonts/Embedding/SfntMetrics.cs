// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads per-glyph metrics from an 'hmtx' or 'vmtx' table, which share one layout.</summary>
internal static class SfntMetrics
{
    /// <summary>The bytes of one long metric: an advance and a side bearing.</summary>
    internal const int LongMetricSize = 4;

    /// <summary>The bytes of a short side bearing.</summary>
    internal const int ShortBearingSize = 2;

    /// <summary>Gets a glyph's advance.</summary>
    /// <param name="table">The metrics table.</param>
    /// <param name="metricCount">The number of long metrics.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance in design units; zero when the table has no metrics.</returns>
    internal static int Advance(ReadOnlySpan<byte> table, int metricCount, int glyph) =>
        metricCount <= 0 ? 0 : FontBytes.U16(table, Math.Min(glyph, metricCount - 1) * LongMetricSize);

    /// <summary>Gets a glyph's side bearing.</summary>
    /// <param name="table">The metrics table.</param>
    /// <param name="metricCount">The number of long metrics.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The side bearing in design units.</returns>
    internal static int Bearing(ReadOnlySpan<byte> table, int metricCount, int glyph) =>
        glyph < metricCount
            ? FontBytes.S16(table, (glyph * LongMetricSize) + ShortBearingSize)
            : FontBytes.S16(table, (metricCount * LongMetricSize) + ((glyph - metricCount) * ShortBearingSize));
}
