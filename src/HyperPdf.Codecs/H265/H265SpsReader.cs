// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads sequence parameter sets (7.3.2.2).</summary>
internal static class H265SpsReader
{
    /// <summary>The largest sequence parameter set number.</summary>
    private const int MaxId = 15;

    /// <summary>The bits of the video parameter set number.</summary>
    private const int VpsIdBits = 4;

    /// <summary>The bits of the number of sub-layers.</summary>
    private const int SubLayerBits = 3;

    /// <summary>The chroma format whose planes may be coded separately.</summary>
    private const int ChromaFormat444 = 3;

    /// <summary>The offset added to the coded bit depths.</summary>
    private const int BitDepthBase = 8;

    /// <summary>The offset added to the coded size of the picture order count field.</summary>
    private const int PocBitsBase = 4;

    /// <summary>The offset added to the coded smallest coding block size.</summary>
    private const int MinCbBase = 3;

    /// <summary>The offset added to the coded smallest transform block size.</summary>
    private const int MinTbBase = 2;

    /// <summary>The most bits a picture order count field may have.</summary>
    private const int MaxPocBits = 16;

    /// <summary>The most sizes along one side that this reader accepts.</summary>
    private const int MaxPictureSide = 16_384;

    /// <summary>The most short-term reference picture sets a sequence parameter set may list.</summary>
    private const int MaxShortTermSets = 64;

    /// <summary>The most long-term reference pictures a sequence parameter set may list.</summary>
    private const int MaxLongTerm = 32;

    /// <summary>The bits of the sizes in the pulse code modulation settings.</summary>
    private const int PcmDepthBits = 4;

    /// <summary>The bits of the extension flags that follow the first four.</summary>
    private const int ExtensionBits = 4;

    /// <summary>The largest bit depth this decoder reads.</summary>
    private const int MaxBitDepth = 16;

    /// <summary>Reads a sequence parameter set.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal static H265Sps Read(ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        reader.Skip(VpsIdBits);
        var maxSubLayersMinus1 = (int)reader.Read(SubLayerBits);
        reader.Skip(1);
        var profile = H265ProfileTier.Read(ref reader, maxSubLayersMinus1);
        var id = (int)ExpGolomb.ReadUe(ref reader);
        var chromaFormat = (int)ExpGolomb.ReadUe(ref reader);
        var separate = chromaFormat == ChromaFormat444 && reader.ReadBit();
        var width = (int)ExpGolomb.ReadUe(ref reader);
        var height = (int)ExpGolomb.ReadUe(ref reader);
        var crop = ReadCrop(ref reader, chromaFormat);
        var depthLuma = BitDepthBase + (int)ExpGolomb.ReadUe(ref reader);
        var depthChroma = BitDepthBase + (int)ExpGolomb.ReadUe(ref reader);
        var log2MaxPocLsb = PocBitsBase + (int)ExpGolomb.ReadUe(ref reader);
        var ordering = ReadOrdering(ref reader, maxSubLayersMinus1);
        var blocks = ReadBlocks(ref reader);
        var scaling = ReadScaling(ref reader);
        var amp = reader.ReadBit();
        var sao = reader.ReadBit();
        var pcm = ReadPcm(ref reader);
        var sets = ReadShortTermSets(ref reader);
        var longTerm = ReadLongTerm(ref reader, log2MaxPocLsb);
        var temporalMvp = reader.ReadBit();
        var strong = reader.ReadBit();
        if (reader.ReadBit())
        {
            H265VuiReader.Skip(ref reader, maxSubLayersMinus1);
        }

        var range = ReadExtensions(ref reader);
        var sps = new H265Sps(
            id,
            profile,
            chromaFormat,
            separate,
            width,
            height,
            crop,
            depthLuma,
            depthChroma,
            log2MaxPocLsb,
            ordering,
            blocks,
            new(amp, sao, strong, temporalMvp),
            pcm,
            scaling,
            sets,
            longTerm,
            range);
        Validate(sps, reader.IsOverrun);
        return sps;
    }

    /// <summary>Reads the scaling list flags and the lists.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The lists, or <see langword="null"/> when scaling lists are off.</returns>
    private static H265ScalingLists? ReadScaling(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return null;
        }

        return reader.ReadBit() ? H265ScalingLists.Read(ref reader) : H265ScalingLists.Default;
    }

    /// <summary>Reads the conformance window and turns its offsets into luma samples.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="chromaFormat">The chroma format.</param>
    /// <returns>The crop.</returns>
    private static VideoCrop ReadCrop(ref MsbBitReader reader, int chromaFormat)
    {
        if (!reader.ReadBit())
        {
            return default;
        }

        var unitX = ChromaSampling.SubWidth(chromaFormat);
        var unitY = ChromaSampling.SubHeight(chromaFormat);
        var left = (int)ExpGolomb.ReadUe(ref reader) * unitX;
        var right = (int)ExpGolomb.ReadUe(ref reader) * unitX;
        var top = (int)ExpGolomb.ReadUe(ref reader) * unitY;
        var bottom = (int)ExpGolomb.ReadUe(ref reader) * unitY;
        return new(left, right, top, bottom);
    }

    /// <summary>Reads the decoded picture buffer limits, keeping those of the highest sub-layer.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maxSubLayersMinus1">The number of temporal sub-layers less one.</param>
    /// <returns>The limits.</returns>
    private static H265SubLayerOrdering ReadOrdering(ref MsbBitReader reader, int maxSubLayersMinus1)
    {
        var allLayers = reader.ReadBit();
        var ordering = default(H265SubLayerOrdering);
        for (var i = allLayers ? 0 : maxSubLayersMinus1; i <= maxSubLayersMinus1; i++)
        {
            var buffering = 1 + (int)ExpGolomb.ReadUe(ref reader);
            var reorder = (int)ExpGolomb.ReadUe(ref reader);
            var latency = (int)ExpGolomb.ReadUe(ref reader);
            ordering = new(buffering, reorder, latency);
        }

        return ordering;
    }

    /// <summary>Reads the block sizes.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The sizes.</returns>
    private static H265BlockSizes ReadBlocks(ref MsbBitReader reader)
    {
        var minCb = MinCbBase + (int)ExpGolomb.ReadUe(ref reader);
        var ctb = minCb + (int)ExpGolomb.ReadUe(ref reader);
        var minTb = MinTbBase + (int)ExpGolomb.ReadUe(ref reader);
        var maxTb = minTb + (int)ExpGolomb.ReadUe(ref reader);
        var depthInter = (int)ExpGolomb.ReadUe(ref reader);
        var depthIntra = (int)ExpGolomb.ReadUe(ref reader);
        return new(minCb, ctb, minTb, maxTb, depthInter, depthIntra);
    }

    /// <summary>Reads the pulse code modulation settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The settings.</returns>
    private static H265Pcm ReadPcm(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return default;
        }

        var depthLuma = 1 + (int)reader.Read(PcmDepthBits);
        var depthChroma = 1 + (int)reader.Read(PcmDepthBits);
        var minCb = MinCbBase + (int)ExpGolomb.ReadUe(ref reader);
        var maxCb = minCb + (int)ExpGolomb.ReadUe(ref reader);
        return new(true, depthLuma, depthChroma, minCb, maxCb, reader.ReadBit());
    }

    /// <summary>Reads the short-term reference picture sets.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The sets.</returns>
    /// <exception cref="InvalidDataException">The set lists too many sets.</exception>
    private static List<H265ShortTermRps> ReadShortTermSets(ref MsbBitReader reader)
    {
        var count = (int)ExpGolomb.ReadUe(ref reader);
        if (count > MaxShortTermSets)
        {
            throw new InvalidDataException("A sequence parameter set lists too many short-term reference picture sets.");
        }

        var sets = new List<H265ShortTermRps>(count);
        for (var i = 0; i < count; i++)
        {
            sets.Add(H265ShortTermRpsReader.Read(ref reader, i, count, sets));
        }

        return sets;
    }

    /// <summary>Reads the long-term reference pictures.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="pocBits">The bits of the low picture order count.</param>
    /// <returns>The candidates.</returns>
    /// <exception cref="InvalidDataException">The set lists too many candidates.</exception>
    private static H265LongTermSps ReadLongTerm(ref MsbBitReader reader, int pocBits)
    {
        if (!reader.ReadBit())
        {
            return new(false, [], []);
        }

        var count = (int)ExpGolomb.ReadUe(ref reader);
        if (count > MaxLongTerm)
        {
            throw new InvalidDataException("A sequence parameter set lists too many long-term reference pictures.");
        }

        var lsb = new int[count];
        var used = new bool[count];
        for (var i = 0; i < count; i++)
        {
            lsb[i] = (int)reader.Read(pocBits);
            used[i] = reader.ReadBit();
        }

        return new(true, lsb, used);
    }

    /// <summary>Reads the extension flags and the range extension.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The range tools.</returns>
    /// <exception cref="NotSupportedException">The set uses an extension this decoder does not read.</exception>
    private static H265RangeTools ReadExtensions(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return default;
        }

        var range = reader.ReadBit();
        var multilayer = reader.ReadBit();
        var extension3d = reader.ReadBit();
        var screenContent = reader.ReadBit();
        reader.Skip(ExtensionBits);
        if (extension3d || screenContent)
        {
            throw new NotSupportedException("3D and screen content extensions of H.265 are not supported.");
        }

        var tools = range ? ReadRangeExtension(ref reader) : default;
        if (multilayer)
        {
            reader.Skip(1);
        }

        return tools;
    }

    /// <summary>Reads the range extension flags.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The flags.</returns>
    private static H265RangeTools ReadRangeExtension(ref MsbBitReader reader) =>
        new(
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit(),
            reader.ReadBit());

    /// <summary>Checks the values of a set.</summary>
    /// <param name="sps">The set.</param>
    /// <param name="overrun">Whether the bits ran out.</param>
    /// <exception cref="InvalidDataException">A value is out of range.</exception>
    private static void Validate(H265Sps sps, bool overrun)
    {
        if (overrun || !SizeInRange(sps) || !BlocksInRange(sps.Blocks))
        {
            throw new InvalidDataException("The H.265 sequence parameter set is damaged or out of range.");
        }
    }

    /// <summary>Checks the ids, picture size and bit depths.</summary>
    /// <param name="sps">The set.</param>
    /// <returns><see langword="true"/> when they are in range.</returns>
    private static bool SizeInRange(H265Sps sps) =>
        sps.Id <= MaxId
        && sps.ChromaFormat <= ChromaFormat444
        && sps.Width is > 0 and <= MaxPictureSide
        && sps.Height is > 0 and <= MaxPictureSide
        && sps.BitDepthLuma <= MaxBitDepth
        && sps.BitDepthChroma <= MaxBitDepth
        && sps.Log2MaxPocLsb <= MaxPocBits;

    /// <summary>Checks the block sizes.</summary>
    /// <param name="blocks">The sizes.</param>
    /// <returns><see langword="true"/> when they are in range.</returns>
    private static bool BlocksInRange(H265BlockSizes blocks) =>
        blocks.CtbLog2 <= H265CodingLimits.MaxCtbLog2
        && blocks.MaxTbLog2 <= Math.Min(blocks.CtbLog2, H265CodingLimits.MaxTbLog2)
        && blocks.MinTbLog2 < blocks.MinCbLog2;
}
