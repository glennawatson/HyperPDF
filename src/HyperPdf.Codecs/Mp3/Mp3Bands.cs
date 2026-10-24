// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp3;

/// <summary>Finds the scale factor bands of a granule: runs of coefficients that share a scale factor.</summary>
internal static class Mp3Bands
{
    /// <summary>The widths in a row of the long band table, including the zero that ends it.</summary>
    private const int LongRowLength = 23;

    /// <summary>The widths in a row of the short and mixed band tables, including the zero that ends it.</summary>
    private const int ShortRowLength = 40;

    /// <summary>The bands of a long block.</summary>
    private const int LongBandCount = 22;

    /// <summary>The first row of the table that belongs to MPEG 2.</summary>
    private const int Mpeg2FirstRow = 2;

    /// <summary>The first row of the table that belongs to MPEG 1.</summary>
    private const int Mpeg1FirstRow = 5;

    /// <summary>The sample rate code of 8 kHz, which MPEG 2.5 has its own row for.</summary>
    private const int EightKilohertzCode = 2;

    /// <summary>The row of the tables for MPEG 2.5 at 8 kHz.</summary>
    private const int EightKilohertzRow = 1;

    /// <summary>
    /// Gets the widths of the scale factor entries of a granule: the bands for a long block, three entries to a band for short windows,
    /// or the long bands then the short ones for a mixed block.
    /// </summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <returns>The widths.</returns>
    internal static ReadOnlySpan<byte> Widths(in Mp3FrameHeader header, Mp3GranuleChannel info)
    {
        var row = RowOf(header);
        if (!info.IsShort)
        {
            return Mp3TableData.LongBands.Slice(row * LongRowLength, LongBandCount);
        }

        var widths = (info.MixedBlock ? Mp3TableData.MixedBands : Mp3TableData.ShortBands).Slice(row * ShortRowLength, ShortRowLength);
        return widths[..widths.IndexOf((byte)0)];
    }

    /// <summary>Gets the row of the band tables for a sample rate.</summary>
    /// <param name="header">The frame header.</param>
    /// <returns>The row.</returns>
    private static int RowOf(in Mp3FrameHeader header) => header.Version switch
    {
        MpegVersion.Mpeg1 => Mpeg1FirstRow + header.SampleRateIndex,
        MpegVersion.Mpeg2 => Mpeg2FirstRow + header.SampleRateIndex,
        _ => header.SampleRateIndex == EightKilohertzCode ? EightKilohertzRow : 0,
    };
}
