// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Where a four sample segment of an edge is in a plane, and how the samples across and along the edge are spaced.</summary>
/// <param name="Plane">The plane.</param>
/// <param name="Q0">The index of the first sample on the Q side of the segment.</param>
/// <param name="Across">The distance between samples across the edge.</param>
/// <param name="Along">The distance between samples along the edge.</param>
internal readonly record struct H265EdgeSegment(ushort[] Plane, int Q0, int Across, int Along);
