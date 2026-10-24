// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Where the neighbouring samples of an intra block are read from.</summary>
/// <param name="Plane">The colour plane.</param>
/// <param name="Stride">The samples in a row of the plane.</param>
/// <param name="SubW">The luma samples per plane sample across.</param>
/// <param name="SubH">The luma samples per plane sample down.</param>
/// <param name="CurrentX">The column of the block in luma samples.</param>
/// <param name="CurrentY">The row of the block in luma samples.</param>
/// <param name="Constrained">Whether only intra coded neighbours may be used.</param>
internal readonly record struct H265NeighbourSource(ushort[] Plane, int Stride, int SubW, int SubH, int CurrentX, int CurrentY, bool Constrained);
