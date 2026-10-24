// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Mp3;

/// <summary>Reads the scale factors of a granule, which raise or lower the gain of each band of coefficients.</summary>
internal static class Mp3Scalefactors
{
    /// <summary>The scale factors in a granule, with room for the zeros past the last band.</summary>
    internal const int Capacity = 40;

    /// <summary>The scale factors of a long block.</summary>
    private const int LongCount = 21;

    /// <summary>The scale factors of a block of short windows.</summary>
    private const int ShortCount = 36;

    /// <summary>The scale factors of a mixed block in MPEG 1: eight long bands and nine short band entries.</summary>
    private const int MixedCount = 35;

    /// <summary>The scale factors that use the first size in a long block.</summary>
    private const int LongFirstSize = 11;

    /// <summary>The scale factors that use the first size in a block of short windows.</summary>
    private const int ShortFirstSize = 18;

    /// <summary>The scale factors that use the first size in a mixed block.</summary>
    private const int MixedFirstSize = 17;

    /// <summary>The number of scale factor groups of a long block that a granule may repeat from the first granule.</summary>
    private const int Groups = 4;

    /// <summary>The number of sizes in an MPEG 2 scale factor partition.</summary>
    private const int Partitions = 4;

    /// <summary>The number of schemes for the left channel and for an intensity coded channel, each.</summary>
    private const int SchemesPerKind = 3;

    /// <summary>The schemes for each kind of block: the plain ones then the intensity ones.</summary>
    private const int SchemesPerBlock = SchemesPerKind * 2;

    /// <summary>The groups of a long block that use the first size; the rest use the second.</summary>
    private const int FirstSizeGroups = 2;

    /// <summary>The sizes stored for each MPEG 1 scale factor code.</summary>
    private const int SizesPerCode = 2;

    /// <summary>The scheme number of the first scheme for an intensity coded channel.</summary>
    private const int IntensitySchemes = 3;

    /// <summary>The mixed block kind in the partition tables.</summary>
    private const int MixedKind = 1;

    /// <summary>The short block kind in the partition tables.</summary>
    private const int ShortKind = 2;

    /// <summary>Gets the sizes in bits of the two groups of an MPEG 1 scale factor code: the first, then the second, for each of 16 codes.</summary>
    private static ReadOnlySpan<byte> Mpeg1Sizes =>
    [
        0x0, 0x0, 0x0, 0x1, 0x0, 0x2, 0x0, 0x3, 0x3, 0x0, 0x1, 0x1, 0x1, 0x2, 0x1, 0x3,
        0x2, 0x1, 0x2, 0x2, 0x2, 0x3, 0x3, 0x1, 0x3, 0x2, 0x3, 0x3, 0x4, 0x2, 0x4, 0x3,
    ];

    /// <summary>Gets the start of each of the four groups of a long block, and the end of the last.</summary>
    private static ReadOnlySpan<byte> GroupStarts => [0x00, 0x06, 0x0B, 0x10, 0x15];

    /// <summary>Gets the moduli that split an MPEG 2 scale factor code into four sizes, four for each of six schemes.</summary>
    private static ReadOnlySpan<byte> Moduli => [0x5, 0x5, 0x4, 0x4, 0x5, 0x5, 0x4, 0x1, 0x4, 0x3, 0x1, 0x1, 0x5, 0x6, 0x6, 0x1, 0x4, 0x4, 0x4, 0x1, 0x4, 0x3, 0x1, 0x1];

    /// <summary>Gets the scale factors in each partition: for long, mixed and short blocks, six schemes, four partitions.</summary>
    private static ReadOnlySpan<byte> PartitionCounts =>
    [
        0x06, 0x05, 0x05, 0x05, 0x06, 0x05, 0x07, 0x03, 0x0B, 0x0A, 0x00, 0x00, 0x07, 0x07, 0x07, 0x00, 0x06, 0x06, 0x06, 0x03, 0x08, 0x08, 0x05, 0x00,
        0x06, 0x09, 0x09, 0x09, 0x06, 0x09, 0x0C, 0x06, 0x0F, 0x12, 0x00, 0x00, 0x06, 0x0F, 0x0C, 0x00, 0x06, 0x0C, 0x09, 0x06, 0x06, 0x12, 0x09, 0x00,
        0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x0C, 0x06, 0x12, 0x12, 0x00, 0x00, 0x0C, 0x0C, 0x0C, 0x00, 0x0C, 0x09, 0x09, 0x06, 0x0F, 0x0C, 0x09, 0x00,
    ];

    /// <summary>Reads the scale factors of one channel of one granule.</summary>
    /// <param name="reader">The bits, positioned at the scale factors.</param>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="granule">The granule, 0 or 1.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="scalefactors">Receives the scale factors; for the second granule of MPEG 1 it holds the first granule's on the way in.</param>
    internal static void Read(ref MsbBitReader reader, in Mp3FrameHeader header, Mp3GranuleChannel info, int granule, int channel, Span<int> scalefactors)
    {
        if (header.IsMpeg1)
        {
            ReadMpeg1(ref reader, info, granule, scalefactors);
        }
        else
        {
            ReadMpeg2(ref reader, header, info, channel, scalefactors);
        }
    }

    /// <summary>Reads the scale factors of an MPEG 1 granule.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="granule">The granule.</param>
    /// <param name="scalefactors">Receives the scale factors.</param>
    private static void ReadMpeg1(ref MsbBitReader reader, Mp3GranuleChannel info, int granule, Span<int> scalefactors)
    {
        var first = Mpeg1Sizes[info.ScalefacCompress * SizesPerCode];
        var second = Mpeg1Sizes[(info.ScalefacCompress * SizesPerCode) + 1];
        if (!info.IsShort)
        {
            for (var group = 0; group < Groups; group++)
            {
                var repeats = granule == 1 && (info.Scfsi & (1 << group)) != 0;
                if (!repeats)
                {
                    ReadRun(ref reader, scalefactors, GroupStarts[group], GroupStarts[group + 1], group < FirstSizeGroups ? first : second);
                }
            }

            scalefactors[LongCount..].Clear();
            return;
        }

        var total = info.MixedBlock ? MixedCount : ShortCount;
        var firstCount = info.MixedBlock ? MixedFirstSize : ShortFirstSize;
        ReadRun(ref reader, scalefactors, 0, firstCount, first);
        ReadRun(ref reader, scalefactors, firstCount, total, second);
        scalefactors[total..].Clear();
    }

    /// <summary>Reads a run of scale factors of the same size.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="scalefactors">Receives the scale factors.</param>
    /// <param name="from">The first index.</param>
    /// <param name="to">The index after the last.</param>
    /// <param name="bits">The bits of each.</param>
    private static void ReadRun(ref MsbBitReader reader, Span<int> scalefactors, int from, int to, int bits)
    {
        for (var index = from; index < to; index++)
        {
            scalefactors[index] = bits == 0 ? 0 : (int)reader.Read(bits);
        }
    }

    /// <summary>Reads the scale factors of an MPEG 2 or 2.5 granule, whose sizes and counts depend on the scale factor code.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="scalefactors">Receives the scale factors.</param>
    private static void ReadMpeg2(ref MsbBitReader reader, in Mp3FrameHeader header, Mp3GranuleChannel info, int channel, Span<int> scalefactors)
    {
        var intensity = channel == 1 && header.ChannelMode == Mp3FrameHeader.JointStereoMode && (header.ModeExtension & 1) != 0;
        Span<int> sizes = stackalloc int[Partitions];
        var scheme = SplitCode(info.ScalefacCompress >> (intensity ? 1 : 0), intensity ? IntensitySchemes : 0, sizes);
        var counts = PartitionCounts.Slice(((KindOf(info) * SchemesPerBlock) + scheme) * Partitions, Partitions);
        var index = 0;
        for (var partition = 0; partition < Partitions; partition++)
        {
            ReadRun(ref reader, scalefactors, index, index + counts[partition], sizes[partition]);
            index += counts[partition];
        }

        scalefactors[index..].Clear();
    }

    /// <summary>Tells which table of partitions a granule uses.</summary>
    /// <param name="info">The coding of the granule.</param>
    /// <returns>0 for a long block, 1 for a mixed block, 2 for short windows.</returns>
    private static int KindOf(Mp3GranuleChannel info)
    {
        if (!info.IsShort)
        {
            return 0;
        }

        return info.MixedBlock ? MixedKind : ShortKind;
    }

    /// <summary>Splits an MPEG 2 scale factor code into the four sizes, and says which scheme it used.</summary>
    /// <param name="code">The code.</param>
    /// <param name="firstScheme">The first scheme to try.</param>
    /// <param name="sizes">Receives the four sizes in bits.</param>
    /// <returns>The scheme, 0 to 5.</returns>
    private static int SplitCode(int code, int firstScheme, Span<int> sizes)
    {
        var scheme = firstScheme;
        var remaining = code;
        while (true)
        {
            var moduli = Moduli.Slice(scheme * Partitions, Partitions);
            var product = moduli[0] * moduli[1] * moduli[2] * moduli[3];
            if (remaining < product || scheme == firstScheme + SchemesPerKind - 1)
            {
                var divisor = 1;
                for (var part = Partitions - 1; part >= 0; part--)
                {
                    sizes[part] = (remaining / divisor) % moduli[part];
                    divisor *= moduli[part];
                }

                return scheme;
            }

            remaining -= product;
            scheme++;
        }
    }
}
