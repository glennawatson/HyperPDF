// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Media.Native.VaApi;

/// <summary>
/// Where each field sits in the H.265 buffers of VA-API (<c>VAPictureParameterBufferHEVC</c> and <c>VASliceParameterBufferHEVC</c> in
/// va_dec_hevc.h). The offsets were checked against a C compiler's <c>offsetof</c> for the structures as declared in that header.
/// </summary>
internal static class VaH265Layout
{
    /// <summary>The size of the picture parameter buffer.</summary>
    internal const int PictureSize = 604;

    /// <summary>The size of a <c>VAPictureHEVC</c>.</summary>
    internal const int EntrySize = 28;

    /// <summary>The entries in the decoded picture buffer field.</summary>
    internal const int ReferenceFrameCount = 15;

    /// <summary>The offset of the current picture.</summary>
    internal const int CurrentPicture = 0;

    /// <summary>The offset of the decoded picture buffer.</summary>
    internal const int ReferenceFrames = EntrySize;

    /// <summary>The offset of the luma width (16 bits).</summary>
    internal const int Width = 448;

    /// <summary>The offset of the luma height (16 bits).</summary>
    internal const int Height = 450;

    /// <summary>The offset of the picture fields word (32 bits).</summary>
    internal const int PictureFields = 452;

    /// <summary>The offset of the largest decoded picture buffer size minus 1.</summary>
    internal const int MaxDecPicBufferingMinus1 = 456;

    /// <summary>The offset of the luma bit depth minus 8.</summary>
    internal const int BitDepthLumaMinus8 = 457;

    /// <summary>The offset of the chroma bit depth minus 8.</summary>
    internal const int BitDepthChromaMinus8 = 458;

    /// <summary>The offset of the pulse code modulation luma bit depth minus 1.</summary>
    internal const int PcmBitDepthLumaMinus1 = 459;

    /// <summary>The offset of the pulse code modulation chroma bit depth minus 1.</summary>
    internal const int PcmBitDepthChromaMinus1 = 460;

    /// <summary>The offset of the smallest coding block size minus 3, as a base 2 logarithm.</summary>
    internal const int Log2MinCbMinus3 = 461;

    /// <summary>The offset of the difference between the largest and smallest coding block sizes.</summary>
    internal const int Log2DiffMaxMinCb = 462;

    /// <summary>The offset of the smallest transform block size minus 2, as a base 2 logarithm.</summary>
    internal const int Log2MinTbMinus2 = 463;

    /// <summary>The offset of the difference between the largest and smallest transform block sizes.</summary>
    internal const int Log2DiffMaxMinTb = 464;

    /// <summary>The offset of the smallest pulse code modulation block size minus 3.</summary>
    internal const int Log2MinPcmCbMinus3 = 465;

    /// <summary>The offset of the difference between the largest and smallest pulse code modulation block sizes.</summary>
    internal const int Log2DiffMaxMinPcmCb = 466;

    /// <summary>The offset of the largest intra transform tree depth.</summary>
    internal const int MaxTransformHierarchyDepthIntra = 467;

    /// <summary>The offset of the largest inter transform tree depth.</summary>
    internal const int MaxTransformHierarchyDepthInter = 468;

    /// <summary>The offset of the initial quantiser minus 26.</summary>
    internal const int InitQpMinus26 = 469;

    /// <summary>The offset of the quantiser delta depth.</summary>
    internal const int DiffCuQpDeltaDepth = 470;

    /// <summary>The offset of the Cb quantiser offset.</summary>
    internal const int CbQpOffset = 471;

    /// <summary>The offset of the Cr quantiser offset.</summary>
    internal const int CrQpOffset = 472;

    /// <summary>The offset of the parallel merge level minus 2.</summary>
    internal const int Log2ParallelMergeLevelMinus2 = 473;

    /// <summary>The offset of the number of tile columns minus 1.</summary>
    internal const int TileColumnsMinus1 = 474;

    /// <summary>The offset of the number of tile rows minus 1.</summary>
    internal const int TileRowsMinus1 = 475;

    /// <summary>The offset of the tile column widths minus 1 (19 entries of 16 bits).</summary>
    internal const int ColumnWidthMinus1 = 476;

    /// <summary>The offset of the tile row heights minus 1 (21 entries of 16 bits).</summary>
    internal const int RowHeightMinus1 = 514;

    /// <summary>The most tile column widths the buffer holds.</summary>
    internal const int ColumnWidthCount = 19;

    /// <summary>The most tile row heights the buffer holds.</summary>
    internal const int RowHeightCount = 21;

    /// <summary>The offset of the slice parsing fields word (32 bits).</summary>
    internal const int SliceParsingFields = 556;

    /// <summary>The offset of the picture order count lsb size minus 4.</summary>
    internal const int Log2MaxPocLsbMinus4 = 560;

    /// <summary>The offset of the number of short-term reference picture sets.</summary>
    internal const int NumShortTermRefPicSets = 561;

    /// <summary>The offset of the number of long-term reference pictures the sequence lists.</summary>
    internal const int NumLongTermRefPicSps = 562;

    /// <summary>The offset of the default active entries of list 0 minus 1.</summary>
    internal const int NumRefIdxL0DefaultMinus1 = 563;

    /// <summary>The offset of the default active entries of list 1 minus 1.</summary>
    internal const int NumRefIdxL1DefaultMinus1 = 564;

    /// <summary>The offset of the deblocking beta offset divided by 2.</summary>
    internal const int PpsBetaOffsetDiv2 = 565;

    /// <summary>The offset of the deblocking tc offset divided by 2.</summary>
    internal const int PpsTcOffsetDiv2 = 566;

    /// <summary>The offset of the number of extra slice header bits.</summary>
    internal const int NumExtraSliceHeaderBits = 567;

    /// <summary>The offset of the bits the short-term reference picture set takes in the slice header (32 bits).</summary>
    internal const int StRpsBits = 568;

    /// <summary>The size of the slice parameter buffer.</summary>
    internal const int SliceSize = 264;

    /// <summary>The entries in each reference list field.</summary>
    internal const int ReferenceListCount = 15;

    /// <summary>The offset of the slice data size (32 bits).</summary>
    internal const int SliceDataSize = 0;

    /// <summary>The offset of the slice data offset (32 bits).</summary>
    internal const int SliceDataOffset = 4;

    /// <summary>The offset of the slice data flag (32 bits).</summary>
    internal const int SliceDataFlag = 8;

    /// <summary>The offset of the byte offset of the slice data from the NAL unit header (32 bits).</summary>
    internal const int SliceDataByteOffset = 12;

    /// <summary>The offset of the slice segment address (32 bits).</summary>
    internal const int SliceSegmentAddress = 16;

    /// <summary>The offset of the two reference lists, 15 bytes each.</summary>
    internal const int RefPicList = 20;

    /// <summary>The offset of the long slice flags word (32 bits).</summary>
    internal const int LongSliceFlags = 52;

    /// <summary>The offset of the collocated reference index.</summary>
    internal const int CollocatedRefIdx = 56;

    /// <summary>The offset of the active entries of list 0 minus 1.</summary>
    internal const int NumRefIdxL0ActiveMinus1 = 57;

    /// <summary>The offset of the active entries of list 1 minus 1.</summary>
    internal const int NumRefIdxL1ActiveMinus1 = 58;

    /// <summary>The offset of the slice quantiser delta.</summary>
    internal const int SliceQpDelta = 59;

    /// <summary>The offset of the slice Cb quantiser offset.</summary>
    internal const int SliceCbQpOffset = 60;

    /// <summary>The offset of the slice Cr quantiser offset.</summary>
    internal const int SliceCrQpOffset = 61;

    /// <summary>The offset of the slice deblocking beta offset divided by 2.</summary>
    internal const int SliceBetaOffsetDiv2 = 62;

    /// <summary>The offset of the slice deblocking tc offset divided by 2.</summary>
    internal const int SliceTcOffsetDiv2 = 63;

    /// <summary>The offset of the luma weight denominator.</summary>
    internal const int LumaLog2WeightDenom = 64;

    /// <summary>The offset of the chroma weight denominator minus the luma one.</summary>
    internal const int DeltaChromaLog2WeightDenom = 65;

    /// <summary>The offset of the luma weight deltas of list 0.</summary>
    internal const int DeltaLumaWeightL0 = 66;

    /// <summary>The offset of the luma offsets of list 0.</summary>
    internal const int LumaOffsetL0 = 81;

    /// <summary>The offset of the chroma weight deltas of list 0, two for each entry.</summary>
    internal const int DeltaChromaWeightL0 = 96;

    /// <summary>The offset of the chroma offsets of list 0, two for each entry.</summary>
    internal const int ChromaOffsetL0 = 126;

    /// <summary>The offset of the luma weight deltas of list 1.</summary>
    internal const int DeltaLumaWeightL1 = 156;

    /// <summary>The offset of the luma offsets of list 1.</summary>
    internal const int LumaOffsetL1 = 171;

    /// <summary>The offset of the chroma weight deltas of list 1, two for each entry.</summary>
    internal const int DeltaChromaWeightL1 = 186;

    /// <summary>The offset of the chroma offsets of list 1, two for each entry.</summary>
    internal const int ChromaOffsetL1 = 216;

    /// <summary>The offset of five minus the largest number of merge candidates.</summary>
    internal const int FiveMinusMaxNumMergeCand = 246;

    /// <summary>The offset of the number of entry points (16 bits).</summary>
    internal const int NumEntryPointOffsets = 248;

    /// <summary>The offset of the number of emulation prevention bytes in the slice header (16 bits).</summary>
    internal const int SliceDataNumEmuPrevnBytes = 252;
}
