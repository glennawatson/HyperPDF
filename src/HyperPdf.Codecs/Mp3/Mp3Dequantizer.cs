// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Mp3;

/// <summary>Turns the whole numbers a granule is coded as into coefficients: each is its value to the power of four thirds, scaled by its band's gain.</summary>
internal static class Mp3Dequantizer
{
    /// <summary>The offset the encoder adds to the global gain.</summary>
    private const int GainBias = 210;

    /// <summary>The quarter steps the gain of a mid and side stereo frame drops by, which is the factor of one over the square root of two.</summary>
    private const int MidSideDrop = 2;

    /// <summary>The shift that turns a short window's extra gain into quarter steps.</summary>
    private const int SubblockShift = 3;

    /// <summary>The windows in a block of short windows.</summary>
    private const int ShortWindows = 3;

    /// <summary>The scale factor entries that are long bands in a mixed block of MPEG 1.</summary>
    private const int Mpeg1MixedLongBands = 8;

    /// <summary>The scale factor entries that are long bands in a mixed block of MPEG 2.</summary>
    private const int Mpeg2MixedLongBands = 6;

    /// <summary>The mask of the quarter step of an exponent.</summary>
    private const int QuarterMask = 3;

    /// <summary>The shift that turns quarter steps into whole powers of two.</summary>
    private const int QuarterShift = 2;

    /// <summary>Gets the extra scale factor of each long band when the preflag is set.</summary>
    private static ReadOnlySpan<byte> Pretab => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x01, 0x02, 0x02, 0x03, 0x03, 0x03, 0x02, 0x00];

    /// <summary>Gets the number of entries at the start of a mixed block that are long bands.</summary>
    /// <param name="header">The frame header.</param>
    /// <returns>The number of long entries.</returns>
    internal static int MixedLongEntries(in Mp3FrameHeader header) => header.IsMpeg1 ? Mpeg1MixedLongBands : Mpeg2MixedLongBands;

    /// <summary>Scales the coefficients of a granule.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="widths">The widths of the scale factor entries.</param>
    /// <param name="scalefactors">The scale factors.</param>
    /// <param name="values">The whole numbers.</param>
    /// <param name="used">The number of values up to the last one coded.</param>
    /// <param name="coefficients">Receives the 576 coefficients.</param>
    internal static void Apply(
        in Mp3FrameHeader header,
        Mp3GranuleChannel info,
        ReadOnlySpan<byte> widths,
        ReadOnlySpan<int> scalefactors,
        ReadOnlySpan<int> values,
        int used,
        Span<float> coefficients)
    {
        coefficients.Clear();
        var midSide = header.ChannelMode == Mp3FrameHeader.JointStereoMode && (header.ModeExtension & Mp3Stereo.MidSideBit) != 0;
        var baseExponent = info.GlobalGain - GainBias - (midSide ? MidSideDrop : 0);
        var longEntries = LongEntries(header, info, widths.Length);
        var line = 0;
        for (var entry = 0; entry < widths.Length && line < used; entry++)
        {
            var exponent = entry < longEntries
                ? LongExponent(info, baseExponent, entry, scalefactors[entry])
                : ShortExponent(info, baseExponent, (entry - longEntries) % ShortWindows, scalefactors[entry]);
            Scale(values.Slice(line, widths[entry]), coefficients.Slice(line, widths[entry]), Gain(exponent));
            line += widths[entry];
        }
    }

    /// <summary>Works out two to the power of a number of quarters.</summary>
    /// <param name="quarters">The exponent, in quarter steps.</param>
    /// <returns>The gain.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gain(int quarters) => MathF.ScaleB(Mp3Tables.QuarterRoots.Span[quarters & QuarterMask], quarters >> QuarterShift);

    /// <summary>Gets the number of entries that are long bands.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="entries">All the entries.</param>
    /// <returns>The entries that use the long band formula.</returns>
    private static int LongEntries(in Mp3FrameHeader header, Mp3GranuleChannel info, int entries)
    {
        if (!info.IsShort)
        {
            return entries;
        }

        return info.MixedBlock ? MixedLongEntries(header) : 0;
    }

    /// <summary>Works out the exponent of a long band.</summary>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="baseExponent">The exponent from the global gain.</param>
    /// <param name="entry">The band.</param>
    /// <param name="scalefactor">The band's scale factor.</param>
    /// <returns>The exponent, in quarter steps.</returns>
    private static int LongExponent(Mp3GranuleChannel info, int baseExponent, int entry, int scalefactor)
    {
        var raise = info.Preflag && !info.IsShort ? Pretab[entry] : 0;
        return baseExponent - ((scalefactor + raise) << (info.ScalefacScale + 1));
    }

    /// <summary>Works out the exponent of a short window's band.</summary>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="baseExponent">The exponent from the global gain.</param>
    /// <param name="window">The window, 0 to 2.</param>
    /// <param name="scalefactor">The band's scale factor.</param>
    /// <returns>The exponent, in quarter steps.</returns>
    private static int ShortExponent(Mp3GranuleChannel info, int baseExponent, int window, int scalefactor) =>
        baseExponent - (info.SubblockGain[window] << SubblockShift) - (scalefactor << (info.ScalefacScale + 1));

    /// <summary>Scales one band.</summary>
    /// <param name="values">The whole numbers.</param>
    /// <param name="coefficients">Receives the coefficients.</param>
    /// <param name="gain">The gain of the band.</param>
    private static void Scale(ReadOnlySpan<int> values, Span<float> coefficients, float gain)
    {
        var powers = Mp3Tables.Power43.Span;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (value != 0)
            {
                coefficients[index] = value > 0 ? powers[value] * gain : -powers[-value] * gain;
            }
        }
    }
}
