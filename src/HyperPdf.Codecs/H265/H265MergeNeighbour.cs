// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A spatial neighbour that may supply a merge candidate.</summary>
/// <param name="Available">Whether the neighbour can supply a candidate.</param>
/// <param name="Motion">The neighbour's motion, or <see cref="H265PuMotion.None"/> when it is not available.</param>
internal readonly record struct H265MergeNeighbour(bool Available, H265PuMotion Motion)
{
    /// <summary>Tells whether this neighbour is available and differs from two earlier neighbours.</summary>
    /// <param name="first">The first earlier neighbour to compare with.</param>
    /// <param name="second">The second earlier neighbour to compare with.</param>
    /// <returns><see langword="true"/> when the neighbour adds a candidate.</returns>
    internal bool IsNew(in H265MergeNeighbour first, in H265MergeNeighbour second) =>
        Available && !(first.Available && first.Motion == Motion) && !(second.Available && second.Motion == Motion);
}
