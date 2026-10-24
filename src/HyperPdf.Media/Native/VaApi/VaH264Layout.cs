// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Media.Native.VaApi;

/// <summary>
/// Where each field sits in the H.264 buffers of VA-API (<c>VAPictureParameterBufferH264</c>, <c>VAIQMatrixBufferH264</c> and
/// <c>VASliceParameterBufferH264</c> in va.h), worked out with the C alignment rules: each field starts at a multiple of its own size.
/// </summary>
internal static class VaH264Layout
{
    /// <summary>The size of the picture parameter buffer.</summary>
    internal const int PictureSize = 672;

    /// <summary>The entries in the decoded picture buffer field.</summary>
    internal const int ReferenceFrameCount = 16;

    /// <summary>The offset of the current picture.</summary>
    internal const int CurrentPicture = 0;

    /// <summary>The offset of the decoded picture buffer.</summary>
    internal const int ReferenceFrames = PictureEntry.Size;

    /// <summary>The offset of the width in macroblocks minus 1 (16 bits).</summary>
    internal const int WidthInMbsMinus1 = 612;

    /// <summary>The offset of the height in macroblocks minus 1 (16 bits).</summary>
    internal const int HeightInMbsMinus1 = 614;

    /// <summary>The offset of the luma bit depth minus 8.</summary>
    internal const int BitDepthLumaMinus8 = 616;

    /// <summary>The offset of the chroma bit depth minus 8.</summary>
    internal const int BitDepthChromaMinus8 = 617;

    /// <summary>The offset of the number of reference frames.</summary>
    internal const int NumRefFrames = 618;

    /// <summary>The offset of the sequence fields word (32 bits).</summary>
    internal const int SequenceFields = 620;

    /// <summary>The offset of the initial luma quantiser minus 26.</summary>
    internal const int PicInitQpMinus26 = 628;

    /// <summary>The offset of the Cb quantiser offset.</summary>
    internal const int ChromaQpIndexOffset = 630;

    /// <summary>The offset of the Cr quantiser offset.</summary>
    internal const int SecondChromaQpIndexOffset = 631;

    /// <summary>The offset of the picture fields word (32 bits).</summary>
    internal const int PictureFields = 632;

    /// <summary>The offset of the frame number (16 bits).</summary>
    internal const int FrameNum = 636;

    /// <summary>The size of the scaling list buffer.</summary>
    internal const int MatrixSize = 240;

    /// <summary>The offset of the six 4x4 lists in the scaling list buffer.</summary>
    internal const int Lists4x4 = 0;

    /// <summary>The offset of the two 8x8 lists in the scaling list buffer.</summary>
    internal const int Lists8x8 = 96;

    /// <summary>The size of the slice parameter buffer.</summary>
    internal const int SliceSize = 3128;

    /// <summary>The entries in a reference list field.</summary>
    internal const int ReferenceListCount = 32;

    /// <summary>The offset of the slice data size (32 bits).</summary>
    internal const int SliceDataSize = 0;

    /// <summary>The offset of the slice data offset (32 bits).</summary>
    internal const int SliceDataOffset = 4;

    /// <summary>The offset of the slice data flag (32 bits).</summary>
    internal const int SliceDataFlag = 8;

    /// <summary>The offset of the slice data bit offset (16 bits).</summary>
    internal const int SliceDataBitOffset = 12;

    /// <summary>The offset of the address of the first macroblock (16 bits).</summary>
    internal const int FirstMbInSlice = 14;

    /// <summary>The offset of the slice type.</summary>
    internal const int SliceType = 16;

    /// <summary>The offset of the direct spatial prediction flag.</summary>
    internal const int DirectSpatialMvPred = 17;

    /// <summary>The offset of the active entries of list 0 minus 1.</summary>
    internal const int NumRefIdxL0ActiveMinus1 = 18;

    /// <summary>The offset of the active entries of list 1 minus 1.</summary>
    internal const int NumRefIdxL1ActiveMinus1 = 19;

    /// <summary>The offset of the CABAC initialisation table.</summary>
    internal const int CabacInitIdc = 20;

    /// <summary>The offset of the slice quantiser delta.</summary>
    internal const int SliceQpDelta = 21;

    /// <summary>The offset of the deblocking filter control.</summary>
    internal const int DisableDeblockingFilterIdc = 22;

    /// <summary>The offset of the deblocking alpha offset divided by 2.</summary>
    internal const int SliceAlphaC0OffsetDiv2 = 23;

    /// <summary>The offset of the deblocking beta offset divided by 2.</summary>
    internal const int SliceBetaOffsetDiv2 = 24;

    /// <summary>The offset of reference list 0.</summary>
    internal const int RefPicList0 = 28;

    /// <summary>The offset of reference list 1.</summary>
    internal const int RefPicList1 = RefPicList0 + (ReferenceListCount * PictureEntry.Size);

    /// <summary>The offset of the luma weight denominator.</summary>
    internal const int LumaLog2WeightDenom = 2332;

    /// <summary>The offset of the chroma weight denominator.</summary>
    internal const int ChromaLog2WeightDenom = 2333;

    /// <summary>The offset of the group of list 0 weights: flag, luma weights and offsets, chroma flag, chroma weights and offsets.</summary>
    internal const int WeightsL0 = 2334;

    /// <summary>The offset of the group of list 1 weights.</summary>
    internal const int WeightsL1 = 2722;

    /// <summary>The offset of the luma flag inside a group of weights, relative to the group.</summary>
    internal const int LumaFlag = 0;

    /// <summary>The offset of the luma weights inside a group of weights, relative to the group.</summary>
    internal const int LumaWeights = 2;

    /// <summary>The offset of the luma offsets inside a group of weights, relative to the group.</summary>
    internal const int LumaOffsets = 66;

    /// <summary>The offset of the chroma flag inside a group of weights, relative to the group.</summary>
    internal const int ChromaFlag = 130;

    /// <summary>The offset of the chroma weights inside a group of weights, relative to the group.</summary>
    internal const int ChromaWeights = 132;

    /// <summary>The offset of the chroma offsets inside a group of weights, relative to the group.</summary>
    internal const int ChromaOffsets = 260;
}
