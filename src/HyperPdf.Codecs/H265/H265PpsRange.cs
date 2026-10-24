// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The range extension settings of a picture parameter set (7.3.2.3.2).</summary>
/// <param name="Log2MaxTransformSkipSize">The base 2 logarithm of the largest block that may skip the transform.</param>
/// <param name="CrossComponentPrediction">Whether chroma residuals are predicted from luma residuals.</param>
/// <param name="ChromaQpOffsetListEnabled">Whether coding units may pick a chroma quantiser offset from a list.</param>
/// <param name="DiffCuChromaQpOffsetDepth">How many levels below the coding tree block the chroma offset may change.</param>
/// <param name="CbQpOffsetList">The Cb offsets a coding unit may pick.</param>
/// <param name="CrQpOffsetList">The Cr offsets a coding unit may pick.</param>
/// <param name="Log2SaoOffsetScaleLuma">The shift applied to luma sample adaptive offsets.</param>
/// <param name="Log2SaoOffsetScaleChroma">The shift applied to chroma sample adaptive offsets.</param>
internal sealed record H265PpsRange(
    int Log2MaxTransformSkipSize,
    bool CrossComponentPrediction,
    bool ChromaQpOffsetListEnabled,
    int DiffCuChromaQpOffsetDepth,
    int[] CbQpOffsetList,
    int[] CrQpOffsetList,
    int Log2SaoOffsetScaleLuma,
    int Log2SaoOffsetScaleChroma);
