// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Codecs.H265;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Writes the fields of the H.265 buffers of VA-API that come straight from the parameter sets and slice headers.</summary>
internal static class VaH265Writer
{
    /// <summary>The flag of a picture that is a long-term reference.</summary>
    internal const uint LongTermFlag = 0x8;

    /// <summary>The flag of a picture in the reference picture set before the current picture.</summary>
    internal const uint StCurrBeforeFlag = 0x10;

    /// <summary>The flag of a picture in the reference picture set after the current picture.</summary>
    internal const uint StCurrAfterFlag = 0x20;

    /// <summary>The flag of a picture in the long-term reference picture set.</summary>
    internal const uint LtCurrFlag = 0x40;

    /// <summary>The reference list entry that means no picture.</summary>
    internal const byte NoReference = 0xFF;

    /// <summary>The bit depth the hardware path reads.</summary>
    private const int EightBits = 8;

    /// <summary>The mask of a field two bits wide.</summary>
    private const int TwoBitMask = 3;

    /// <summary>The mask of the NAL unit type in the first byte of the NAL unit header, after the shift.</summary>
    private const int NalTypeMask = 0x3F;

    /// <summary>The value the byte after a pair of zero bytes has when it is an emulation prevention byte.</summary>
    private const byte EmulationPrevention = 3;

    /// <summary>The zero bytes that make the next byte an emulation prevention byte.</summary>
    private const int ZerosBeforePrevention = 2;

    /// <summary>The Cb and Cr weights of one reference list entry.</summary>
    private const int ChromaComponents = 2;

    /// <summary>The value five less than which the number of merge candidates is stored.</summary>
    private const int MergeCandidateBase = 5;

    /// <summary>The offset the smallest coding block size is stored relative to, as a base 2 logarithm.</summary>
    private const int MinCbBase = 3;

    /// <summary>The offset the smallest transform block size is stored relative to, as a base 2 logarithm.</summary>
    private const int MinTbBase = 2;

    /// <summary>The offset the size of the picture order count lsb is stored relative to, as a base 2 logarithm.</summary>
    private const int PocBase = 4;

    /// <summary>The tiles at the end of a row or column whose size follows from the others.</summary>
    private const int ImpliedTiles = 2;

    /// <summary>The bit positions of the picture fields word, in the order the structure lists them.</summary>
    private const int PcmBit = 3;

    /// <summary>The bit of the scaling list flag.</summary>
    private const int ScalingBit = 4;

    /// <summary>The bit of the transform skip flag.</summary>
    private const int TransformSkipBit = 5;

    /// <summary>The bit of the asymmetric motion partition flag.</summary>
    private const int AmpBit = 6;

    /// <summary>The bit of the strong intra smoothing flag.</summary>
    private const int StrongSmoothingBit = 7;

    /// <summary>The bit of the sign data hiding flag.</summary>
    private const int SignHidingBit = 8;

    /// <summary>The bit of the constrained intra prediction flag.</summary>
    private const int ConstrainedIntraBit = 9;

    /// <summary>The bit of the coding unit quantiser delta flag.</summary>
    private const int CuQpDeltaBit = 10;

    /// <summary>The bit of the weighted prediction flag.</summary>
    private const int WeightedPredBit = 11;

    /// <summary>The bit of the weighted bi-prediction flag.</summary>
    private const int WeightedBipredBit = 12;

    /// <summary>The bit of the transquant bypass flag.</summary>
    private const int BypassBit = 13;

    /// <summary>The bit of the tiles flag.</summary>
    private const int TilesBit = 14;

    /// <summary>The bit of the entropy coding sync flag.</summary>
    private const int SyncBit = 15;

    /// <summary>The bit of the loop filter across slices flag.</summary>
    private const int AcrossSlicesBit = 16;

    /// <summary>The bit of the loop filter across tiles flag.</summary>
    private const int AcrossTilesBit = 17;

    /// <summary>The bit of the pulse code modulation loop filter flag.</summary>
    private const int PcmLoopFilterBit = 18;

    /// <summary>The bit of the no picture reordering flag.</summary>
    private const int NoReorderingBit = 19;

    /// <summary>Turns a flag into a bit.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>1 or 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Bit(bool flag) => flag ? 1U : 0U;

    /// <summary>Finds whether the hardware path reads a stream: 8 bit 4:2:0 without scaling lists or range extensions, which the C# decoder also reads.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns><see langword="true"/> when the stream is supported.</returns>
    internal static bool IsSupported(H265Sps sps)
    {
        ArgumentNullException.ThrowIfNull(sps);
        var range = sps.Range;
        return sps.ChromaFormat == 1 && sps.BitDepthLuma == EightBits && sps.BitDepthChroma == EightBits && sps.Scaling is null
            && !(range.TransformSkipRotation || range.TransformSkipContext || range.ImplicitRdpcm || range.ExplicitRdpcm || range.PersistentRice);
    }

    /// <summary>Packs the picture fields of the picture parameters.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The word.</returns>
    internal static uint PictureFields(H265Sps sps, H265Pps pps) =>
        (uint)(sps.ChromaFormat & TwoBitMask)
        | (Bit(sps.Pcm.Enabled) << PcmBit)
        | (Bit(sps.Scaling is not null) << ScalingBit)
        | (Bit(pps.Flags.TransformSkip) << TransformSkipBit)
        | (Bit(sps.Tools.AmpEnabled) << AmpBit)
        | (Bit(sps.Tools.StrongIntraSmoothing) << StrongSmoothingBit)
        | (Bit(pps.Flags.SignDataHiding) << SignHidingBit)
        | (Bit(pps.Flags.ConstrainedIntraPred) << ConstrainedIntraBit)
        | (Bit(pps.Quant.CuQpDeltaEnabled) << CuQpDeltaBit)
        | (Bit(pps.Flags.WeightedPred) << WeightedPredBit)
        | (Bit(pps.Flags.WeightedBipred) << WeightedBipredBit)
        | (Bit(pps.Flags.TransquantBypass) << BypassBit)
        | (Bit(pps.Tiles.Enabled) << TilesBit)
        | (Bit(pps.Flags.EntropyCodingSync) << SyncBit)
        | (Bit(pps.Flags.LoopFilterAcrossSlices) << AcrossSlicesBit)
        | (Bit(pps.Tiles.LoopFilterAcrossTiles) << AcrossTilesBit)
        | (Bit(sps.Pcm.LoopFilterDisabled) << PcmLoopFilterBit)
        | (Bit(sps.Ordering.MaxNumReorder == 0) << NoReorderingBit);

    /// <summary>Packs the slice parsing fields of the picture parameters.</summary>
    /// <param name="picture">The picture.</param>
    /// <returns>The word.</returns>
    internal static uint ParsingFields(H265HardwarePicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var sps = picture.Sps;
        var pps = picture.Pps;
        var type = (picture.Bytes.Span[picture.Slices[0].Offset] >> 1) & NalTypeMask;
        var allIntra = true;
        foreach (var slice in picture.Slices)
        {
            allIntra &= slice.Header.SliceType == H265SliceType.I;
        }

        return Bit(pps.Flags.ListsModificationPresent)
            | (Bit(sps.LongTerm.Present) << 1)
            | (Bit(sps.Tools.TemporalMvpEnabled) << 2)
            | (Bit(pps.Flags.CabacInitPresent) << 3)
            | (Bit(pps.Flags.OutputFlagPresent) << 4)
            | (Bit(pps.Flags.DependentSlices) << 5)
            | (Bit(pps.Flags.SliceChromaQpOffsetsPresent) << 6)
            | (Bit(sps.Tools.SaoEnabled) << 7)
            | (Bit(pps.Deblocking.OverrideEnabled) << 8)
            | (Bit(pps.Deblocking.Disabled) << 9)
            | (Bit(pps.Flags.SliceHeaderExtensionPresent) << 10)
            | (Bit(H265Nal.IsIrap(type)) << 11)
            | (Bit(H265Nal.IsIdr(type)) << 12)
            | (Bit(allIntra) << 13);
    }

    /// <summary>Counts the emulation prevention bytes in the start of a NAL unit, up to the byte where the slice data begins once those bytes are removed.</summary>
    /// <param name="nal">The NAL unit as it was in the stream.</param>
    /// <param name="dataStart">The offset of the slice data counted without emulation prevention bytes, from the start of the NAL unit.</param>
    /// <returns>The number of emulation prevention bytes before the slice data.</returns>
    internal static int CountPreventionBytes(ReadOnlySpan<byte> nal, int dataStart)
    {
        var count = 0;
        var kept = 0;
        var zeros = 0;
        foreach (var value in nal)
        {
            if (kept >= dataStart)
            {
                break;
            }

            if (zeros >= ZerosBeforePrevention && value == EmulationPrevention)
            {
                count++;
                zeros = 0;
                continue;
            }

            kept++;
            zeros = value == 0 ? zeros + 1 : 0;
        }

        return count;
    }

    /// <summary>Gets the flags that tell how the current picture uses a reference picture.</summary>
    /// <param name="reference">The reference picture.</param>
    /// <param name="set">The reference picture set of the current picture.</param>
    /// <returns>The flags.</returns>
    internal static uint ReferenceFlags(H265Picture reference, H265RefPicSet set)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(set);
        return (reference.IsLongTerm ? LongTermFlag : 0)
            | (Array.IndexOf(set.StCurrBefore, reference) >= 0 ? StCurrBeforeFlag : 0)
            | (Array.IndexOf(set.StCurrAfter, reference) >= 0 ? StCurrAfterFlag : 0)
            | (Array.IndexOf(set.LtCurr, reference) >= 0 ? LtCurrFlag : 0);
    }

    /// <summary>Writes the parameters that come from the sequence parameter set.</summary>
    /// <param name="block">The picture parameter buffer.</param>
    /// <param name="sps">The sequence parameter set.</param>
    internal static void WriteSequenceParameters(VaBlock block, H265Sps sps)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(sps);
        var sizes = sps.Blocks;
        block.Set(VaH265Layout.Width, (ushort)sps.Width);
        block.Set(VaH265Layout.Height, (ushort)sps.Height);
        block.Set(VaH265Layout.MaxDecPicBufferingMinus1, (byte)(sps.Ordering.MaxDecPicBuffering - 1));
        block.Set(VaH265Layout.PcmBitDepthLumaMinus1, (byte)Math.Max(sps.Pcm.BitDepthLuma - 1, 0));
        block.Set(VaH265Layout.PcmBitDepthChromaMinus1, (byte)Math.Max(sps.Pcm.BitDepthChroma - 1, 0));
        block.Set(VaH265Layout.Log2MinCbMinus3, (byte)(sizes.MinCbLog2 - MinCbBase));
        block.Set(VaH265Layout.Log2DiffMaxMinCb, (byte)(sizes.CtbLog2 - sizes.MinCbLog2));
        block.Set(VaH265Layout.Log2MinTbMinus2, (byte)(sizes.MinTbLog2 - MinTbBase));
        block.Set(VaH265Layout.Log2DiffMaxMinTb, (byte)(sizes.MaxTbLog2 - sizes.MinTbLog2));
        block.Set(VaH265Layout.Log2MinPcmCbMinus3, (byte)(sps.Pcm.Enabled ? sps.Pcm.MinCbLog2 - MinCbBase : 0));
        block.Set(VaH265Layout.Log2DiffMaxMinPcmCb, (byte)(sps.Pcm.Enabled ? sps.Pcm.MaxCbLog2 - sps.Pcm.MinCbLog2 : 0));
        block.Set(VaH265Layout.MaxTransformHierarchyDepthIntra, (byte)sizes.MaxDepthIntra);
        block.Set(VaH265Layout.MaxTransformHierarchyDepthInter, (byte)sizes.MaxDepthInter);
        block.Set(VaH265Layout.Log2MaxPocLsbMinus4, (byte)(sps.Log2MaxPocLsb - PocBase));
        block.Set(VaH265Layout.NumShortTermRefPicSets, (byte)sps.ShortTermSets.Count);
        block.Set(VaH265Layout.NumLongTermRefPicSps, (byte)(sps.LongTerm.Present ? sps.LongTerm.PocLsb.Length : 0));
    }

    /// <summary>Writes the sizes of the tile columns or rows from their boundaries, each minus 1; the last one is left out because it follows from the others.</summary>
    /// <param name="block">The picture parameter buffer.</param>
    /// <param name="offset">Where the array is.</param>
    /// <param name="capacity">How many entries the array holds.</param>
    /// <param name="boundaries">The boundaries, one more than there are tiles.</param>
    internal static void WriteSizes(VaBlock block, int offset, int capacity, int[] boundaries)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(boundaries);
        for (var i = 0; i < boundaries.Length - ImpliedTiles && i < capacity; i++)
        {
            block.Set(offset + (i * sizeof(ushort)), (ushort)(boundaries[i + 1] - boundaries[i] - 1));
        }
    }

    /// <summary>Writes the settings of a slice that are plain numbers.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="header">The slice segment header.</param>
    /// <param name="pps">The picture parameter set.</param>
    internal static void WriteSliceSettings(VaBlock block, H265SliceHeader header, H265Pps pps)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(pps);
        var refs = header.Refs;
        var temporal = header.TemporalMvp && header.SliceType != H265SliceType.I;
        block.Set(VaH265Layout.CollocatedRefIdx, temporal ? (byte)refs.CollocatedRefIdx : NoReference);
        block.Set(VaH265Layout.NumRefIdxL0ActiveMinus1, (byte)Math.Max(refs.NumRefIdxL0 - 1, 0));
        block.Set(VaH265Layout.NumRefIdxL1ActiveMinus1, (byte)Math.Max(refs.NumRefIdxL1 - 1, 0));
        block.Set(VaH265Layout.SliceQpDelta, (sbyte)(header.Quant.SliceQp - pps.Quant.InitQp));
        block.Set(VaH265Layout.SliceCbQpOffset, (sbyte)header.Quant.CbQpOffset);
        block.Set(VaH265Layout.SliceCrQpOffset, (sbyte)header.Quant.CrQpOffset);
        block.Set(VaH265Layout.SliceBetaOffsetDiv2, (sbyte)header.Deblocking.BetaOffsetDiv2);
        block.Set(VaH265Layout.SliceTcOffsetDiv2, (sbyte)header.Deblocking.TcOffsetDiv2);
        block.Set(VaH265Layout.FiveMinusMaxNumMergeCand, (byte)(MergeCandidateBase - refs.MaxMergeCand));
    }

    /// <summary>Packs the long slice flags.</summary>
    /// <param name="header">The slice segment header.</param>
    /// <param name="last">Whether this is the last slice segment of the picture.</param>
    /// <returns>The word.</returns>
    internal static uint SliceFlags(H265SliceHeader header, bool last)
    {
        ArgumentNullException.ThrowIfNull(header);
        return Bit(last)
            | (Bit(header.Segment.Dependent) << 1)
            | ((uint)header.SliceType << 2)
            | (Bit(header.SaoLuma) << 6)
            | (Bit(header.SaoChroma) << 7)
            | (Bit(header.Refs.MvdL1Zero) << 8)
            | (Bit(header.Refs.CabacInit) << 9)
            | (Bit(header.TemporalMvp) << 10)
            | (Bit(header.Deblocking.Disabled) << 11)
            | (Bit(header.Refs.CollocatedFromL0) << 12)
            | (Bit(header.Deblocking.LoopFilterAcrossSlices) << 13);
    }

    /// <summary>Writes the explicit prediction weights of a slice as the differences from the default weight.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="weights">The weights the slice sent.</param>
    internal static void WriteWeights(VaBlock block, H265PredWeights weights)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(weights);
        block.Set(VaH265Layout.LumaLog2WeightDenom, (byte)weights.LumaLog2Denom);
        block.Set(VaH265Layout.DeltaChromaLog2WeightDenom, (sbyte)(weights.ChromaLog2Denom - weights.LumaLog2Denom));
        WriteWeightList(block, weights, weights.List0, VaH265Layout.DeltaLumaWeightL0, VaH265Layout.DeltaChromaWeightL0);
        if (weights.List1 is { } list1)
        {
            WriteWeightList(block, weights, list1, VaH265Layout.DeltaLumaWeightL1, VaH265Layout.DeltaChromaWeightL1);
        }
    }

    /// <summary>Writes the weights of one reference list.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="weights">The slice's weights, which hold the denominators.</param>
    /// <param name="list">The weights of the list.</param>
    /// <param name="lumaWeight">Where the luma weight differences go; the luma offsets follow them.</param>
    /// <param name="chromaWeight">Where the chroma weight differences go; the chroma offsets follow them.</param>
    private static void WriteWeightList(VaBlock block, H265PredWeights weights, H265WeightList list, int lumaWeight, int chromaWeight)
    {
        var lumaOffset = lumaWeight + VaH265Layout.ReferenceListCount;
        var chromaOffset = chromaWeight + (VaH265Layout.ReferenceListCount * ChromaComponents);
        var count = Math.Min(list.LumaWeight.Length, VaH265Layout.ReferenceListCount);
        for (var i = 0; i < count; i++)
        {
            block.Set(lumaWeight + i, (sbyte)(list.LumaWeight[i] - (1 << weights.LumaLog2Denom)));
            block.Set(lumaOffset + i, (sbyte)list.LumaOffset[i]);
            for (var j = 0; j < ChromaComponents; j++)
            {
                var entry = (i * ChromaComponents) + j;
                block.Set(chromaWeight + entry, (sbyte)(list.ChromaWeight[entry] - (1 << weights.ChromaLog2Denom)));
                block.Set(chromaOffset + entry, (sbyte)list.ChromaOffset[entry]);
            }
        }
    }
}
