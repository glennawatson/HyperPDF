// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The part of a slice header that says which pictures the slice refers to and how it weights them.</summary>
/// <param name="DirectSpatialMvPred">Whether direct prediction in a B slice works from neighbours in space rather than in time.</param>
/// <param name="NumRefIdxL0">The number of pictures in reference list 0.</param>
/// <param name="NumRefIdxL1">The number of pictures in reference list 1.</param>
/// <param name="ModificationsL0">The operations that reorder reference list 0.</param>
/// <param name="ModificationsL1">The operations that reorder reference list 1.</param>
/// <param name="Weights">The explicit weights, or <see langword="null"/> when the slice sends none.</param>
internal sealed record H264RefSetup(
    bool DirectSpatialMvPred,
    int NumRefIdxL0,
    int NumRefIdxL1,
    H264RefModification[] ModificationsL0,
    H264RefModification[] ModificationsL1,
    H264WeightTable? Weights);
