// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The range extension tools of a sequence parameter set (7.3.2.2.2).</summary>
/// <param name="TransformSkipRotation">Whether skipped 4x4 residuals of intra blocks are rotated.</param>
/// <param name="TransformSkipContext">Whether skipped residuals use their own significance context.</param>
/// <param name="ImplicitRdpcm">Whether intra blocks that skip the transform use residual DPCM chosen by the prediction mode.</param>
/// <param name="ExplicitRdpcm">Whether inter blocks that skip the transform may signal residual DPCM.</param>
/// <param name="ExtendedPrecision">Whether coefficient and weight precision grows with the bit depth.</param>
/// <param name="IntraSmoothingDisabled">Whether the intra reference smoothing filter is off.</param>
/// <param name="HighPrecisionOffsets">Whether weighted prediction offsets have the bit depth of the samples.</param>
/// <param name="PersistentRice">Whether the Rice parameter carries over between blocks.</param>
/// <param name="CabacBypassAlignment">Whether bypass bins are aligned before coefficient values.</param>
internal readonly record struct H265RangeTools(
    bool TransformSkipRotation,
    bool TransformSkipContext,
    bool ImplicitRdpcm,
    bool ExplicitRdpcm,
    bool ExtendedPrecision,
    bool IntraSmoothingDisabled,
    bool HighPrecisionOffsets,
    bool PersistentRice,
    bool CabacBypassAlignment);
