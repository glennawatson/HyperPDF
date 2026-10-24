// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Vorbis;

/// <summary>Working room for decoding one floor, kept so a packet allocates nothing.</summary>
internal sealed class VorbisFloorScratch
{
    /// <summary>The most points a floor can have: 31 partitions of 8, plus the two end points.</summary>
    private const int MostPoints = 258;

    /// <summary>Gets the coded heights.</summary>
    internal int[] Heights { get; } = new int[MostPoints];

    /// <summary>Gets the final heights.</summary>
    internal int[] Final { get; } = new int[MostPoints];

    /// <summary>Gets which points the curve passes through.</summary>
    internal bool[] Used { get; } = new bool[MostPoints];
}
