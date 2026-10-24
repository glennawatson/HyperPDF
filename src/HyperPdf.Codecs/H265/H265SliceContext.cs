// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>The state of decoding one slice segment: the parameter sets in use, the CABAC state, the quantiser state and the work areas.</summary>
[DebuggerDisplay("H265SliceContext: segment at {Header.Segment.Address}")]
internal sealed class H265SliceContext
{
    /// <summary>The largest transform block area.</summary>
    private const int MaxBlockArea = 32 * 32;

    /// <summary>The Rice parameter states kept for persistent adaptation.</summary>
    private const int StatCoeffCount = 4;

    /// <summary>Initializes a new instance of the <see cref="H265SliceContext"/> class.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="layout">The picture layout.</param>
    /// <param name="picture">The picture being decoded.</param>
    /// <param name="prediction">The work areas of inter prediction, which the decoder keeps from picture to picture.</param>
    /// <param name="saoSource">A work area at least as long as the luma plane, which the decoder keeps from picture to picture.</param>
    internal H265SliceContext(H265Sps sps, H265Pps pps, H265PictureLayout layout, H265Picture picture, H265PredictionBuffers prediction, ushort[] saoSource)
    {
        SaoSource = saoSource;
        Sps = sps;
        Pps = pps;
        Layout = layout;
        Picture = picture;
        Header = null!;
        Contexts = new byte[H265CabacContexts.Count];
        StatCoeff = new int[StatCoeffCount];
        WppContexts = new byte[H265CabacContexts.Count];
        WppStatCoeff = new int[StatCoeffCount];
        DependentContexts = new byte[H265CabacContexts.Count];
        DependentStatCoeff = new int[StatCoeffCount];
        Coefficients = new int[MaxBlockArea];
        Scratch = new int[MaxBlockArea];
        Prediction = prediction;
        ChromaModes = new int[4];
    }

    /// <summary>Gets the work area that holds a copy of a plane while sample adaptive offset reads it.</summary>
    internal ushort[] SaoSource { get; }

    /// <summary>Gets the sequence parameter set.</summary>
    internal H265Sps Sps { get; }

    /// <summary>Gets the picture parameter set.</summary>
    internal H265Pps Pps { get; }

    /// <summary>Gets the picture layout.</summary>
    internal H265PictureLayout Layout { get; }

    /// <summary>Gets the picture being decoded.</summary>
    internal H265Picture Picture { get; }

    /// <summary>Gets or sets the header of the slice segment being decoded.</summary>
    internal H265SliceHeader Header { get; set; }

    /// <summary>Gets or sets the raster scan address of the first coding tree block of the slice this segment belongs to.</summary>
    internal int SliceAddrRs { get; set; }

    /// <summary>Gets or sets the index of the slice in the picture, which <see cref="H265Picture.SliceRefs"/> is indexed by.</summary>
    internal int SliceIndex { get; set; }

    /// <summary>Gets or sets a value indicating whether no reference picture of the slice follows the current picture in output order.</summary>
    internal bool NoBackwardPred { get; set; }

    /// <summary>Gets the pictures of reference list 0.</summary>
    internal List<H265Picture> RefList0 { get; } = [];

    /// <summary>Gets the pictures of reference list 1.</summary>
    internal List<H265Picture> RefList1 { get; } = [];

    /// <summary>Gets a work area that holds the initial reference list before it is reordered.</summary>
    internal List<H265Picture> RefInitial { get; } = [];

    /// <summary>Gets the context variables.</summary>
    internal byte[] Contexts { get; }

    /// <summary>Gets the Rice parameter states.</summary>
    internal int[] StatCoeff { get; }

    /// <summary>Gets the context variables saved after the second coding tree block of a row, for wavefront synchronisation.</summary>
    internal byte[] WppContexts { get; }

    /// <summary>Gets the Rice parameter states saved with <see cref="WppContexts"/>.</summary>
    internal int[] WppStatCoeff { get; }

    /// <summary>Gets the context variables saved at the end of a slice segment, for a dependent segment to continue from.</summary>
    internal byte[] DependentContexts { get; }

    /// <summary>Gets the Rice parameter states saved with <see cref="DependentContexts"/>.</summary>
    internal int[] DependentStatCoeff { get; }

    /// <summary>Gets the coefficients of the transform block being read, row after row.</summary>
    internal int[] Coefficients { get; }

    /// <summary>Gets a work area the size of the largest transform block.</summary>
    internal int[] Scratch { get; }

    /// <summary>Gets the state of reading a residual block.</summary>
    internal H265ResidualState Residual { get; } = new();

    /// <summary>Gets the work areas of inter prediction.</summary>
    internal H265PredictionBuffers Prediction { get; }

    /// <summary>Gets the intra chroma prediction mode of each prediction block of the current coding unit.</summary>
    internal int[] ChromaModes { get; }

    /// <summary>Gets or sets the quantiser of the current coding unit.</summary>
    internal int QpY { get; set; }

    /// <summary>Gets or sets the predicted quantiser of the current quantisation group.</summary>
    internal int QpYPred { get; set; }

    /// <summary>Gets or sets the quantiser of the last coding unit decoded, which the next quantisation group predicts from.</summary>
    internal int LastQpY { get; set; }

    /// <summary>Gets or sets a value indicating whether the quantiser change of the current quantisation group was read.</summary>
    internal bool IsCuQpDeltaCoded { get; set; }

    /// <summary>Gets or sets the quantiser change of the current quantisation group.</summary>
    internal int CuQpDeltaVal { get; set; }

    /// <summary>Gets or sets a value indicating whether the chroma quantiser offset of the current group was read.</summary>
    internal bool IsCuChromaQpOffsetCoded { get; set; }

    /// <summary>Gets or sets the Cb quantiser offset of the current coding unit.</summary>
    internal int CuQpOffsetCb { get; set; }

    /// <summary>Gets or sets the Cr quantiser offset of the current coding unit.</summary>
    internal int CuQpOffsetCr { get; set; }

    /// <summary>Gets a value indicating whether the picture has any chroma.</summary>
    internal bool HasChroma => Sps.ChromaArrayType != ChromaSampling.Monochrome;
}
