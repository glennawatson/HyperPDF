// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>A picture parameter set: what is the same for the slices of a picture.</summary>
/// <param name="Id">The set's number.</param>
/// <param name="SpsId">The number of the sequence parameter set it belongs to.</param>
/// <param name="Cabac">Whether macroblocks are coded with CABAC rather than CAVLC.</param>
/// <param name="BottomFieldPicOrderPresent">Whether slice headers carry the picture order count of the bottom field.</param>
/// <param name="NumRefIdxL0Default">The default number of reference pictures in list 0.</param>
/// <param name="NumRefIdxL1Default">The default number of reference pictures in list 1.</param>
/// <param name="WeightedPred">Whether P slices use explicit weighted prediction.</param>
/// <param name="WeightedBipredIdc">How B slices weight their predictions: 0 not at all, 1 explicitly, 2 by distance.</param>
/// <param name="PicInitQp">The quantiser slices start from.</param>
/// <param name="ChromaQpOffset">The offset of the Cb quantiser from the luma quantiser.</param>
/// <param name="SecondChromaQpOffset">The offset of the Cr quantiser from the luma quantiser.</param>
/// <param name="DeblockingControlPresent">Whether slice headers control the deblocking filter.</param>
/// <param name="ConstrainedIntraPred">Whether intra prediction may use only intra neighbours.</param>
/// <param name="RedundantPicCntPresent">Whether slice headers carry the redundant picture count.</param>
/// <param name="Transform8x8">Whether macroblocks may use the 8x8 transform.</param>
/// <param name="Scaling">The scaling lists the picture uses.</param>
[DebuggerDisplay("H264Pps: id {Id}, sequence {SpsId}, cabac {Cabac}")]
internal sealed record H264Pps(
    int Id,
    int SpsId,
    bool Cabac,
    bool BottomFieldPicOrderPresent,
    int NumRefIdxL0Default,
    int NumRefIdxL1Default,
    bool WeightedPred,
    int WeightedBipredIdc,
    int PicInitQp,
    int ChromaQpOffset,
    int SecondChromaQpOffset,
    bool DeblockingControlPresent,
    bool ConstrainedIntraPred,
    bool RedundantPicCntPresent,
    bool Transform8x8,
    H264ScalingLists Scaling);
