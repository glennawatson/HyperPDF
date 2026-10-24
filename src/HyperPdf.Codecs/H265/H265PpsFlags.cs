// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The on or off settings of a picture parameter set.</summary>
/// <param name="DependentSlices">Whether a slice may be split into dependent slice segments.</param>
/// <param name="OutputFlagPresent">Whether slice headers say if the picture is output.</param>
/// <param name="SignDataHiding">Whether the sign of one coefficient per group is inferred from the parity of the group.</param>
/// <param name="CabacInitPresent">Whether slice headers may swap the CABAC initialisation tables.</param>
/// <param name="ConstrainedIntraPred">Whether intra prediction may use only intra coded neighbours.</param>
/// <param name="TransformSkip">Whether 4x4 blocks may skip the transform.</param>
/// <param name="SliceChromaQpOffsetsPresent">Whether slice headers carry chroma quantiser offsets.</param>
/// <param name="WeightedPred">Whether P slices use weighted prediction.</param>
/// <param name="WeightedBipred">Whether B slices use weighted prediction.</param>
/// <param name="TransquantBypass">Whether coding units may skip transform and quantisation.</param>
/// <param name="EntropyCodingSync">Whether CABAC state is synchronised at the start of each row of coding tree blocks.</param>
/// <param name="LoopFilterAcrossSlices">Whether the loop filters may cross slice edges.</param>
/// <param name="ListsModificationPresent">Whether slice headers may reorder the reference lists.</param>
/// <param name="SliceHeaderExtensionPresent">Whether slice headers carry extension bytes.</param>
internal readonly record struct H265PpsFlags(
    bool DependentSlices,
    bool OutputFlagPresent,
    bool SignDataHiding,
    bool CabacInitPresent,
    bool ConstrainedIntraPred,
    bool TransformSkip,
    bool SliceChromaQpOffsetsPresent,
    bool WeightedPred,
    bool WeightedBipred,
    bool TransquantBypass,
    bool EntropyCodingSync,
    bool LoopFilterAcrossSlices,
    bool ListsModificationPresent,
    bool SliceHeaderExtensionPresent);
