// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>How a P or B slice builds and uses its reference lists.</summary>
/// <param name="NumRefIdxL0">The pictures in list 0.</param>
/// <param name="NumRefIdxL1">The pictures in list 1.</param>
/// <param name="ModificationL0">The entries picked for list 0, or <see langword="null"/> to use the default order.</param>
/// <param name="ModificationL1">The entries picked for list 1, or <see langword="null"/> to use the default order.</param>
/// <param name="MvdL1Zero">Whether the motion vector difference of list 1 prediction is zero and not coded.</param>
/// <param name="CabacInit">Whether the CABAC initialisation tables of P and B slices are swapped.</param>
/// <param name="CollocatedFromL0">Whether the co-located picture is picked from list 0.</param>
/// <param name="CollocatedRefIdx">The index of the co-located picture in its list.</param>
/// <param name="MaxMergeCand">The most merge candidates.</param>
internal sealed record H265RefSettings(
    int NumRefIdxL0,
    int NumRefIdxL1,
    int[]? ModificationL0,
    int[]? ModificationL1,
    bool MvdL1Zero,
    bool CabacInit,
    bool CollocatedFromL0,
    int CollocatedRefIdx,
    int MaxMergeCand);
