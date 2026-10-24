// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The filtered samples above and to the left of an 8x8 block, and the sample above and to the left of both.</summary>
internal readonly ref struct H264Edges
{
    /// <summary>Initializes a new instance of the <see cref="H264Edges"/> struct.</summary>
    /// <param name="top">The 16 filtered samples above and above and to the right.</param>
    /// <param name="left">The 8 filtered samples on the left.</param>
    /// <param name="corner">The filtered sample above and to the left.</param>
    internal H264Edges(ReadOnlySpan<int> top, ReadOnlySpan<int> left, int corner)
    {
        Top = top;
        Left = left;
        Corner = corner;
    }

    /// <summary>Gets the filtered samples above.</summary>
    internal ReadOnlySpan<int> Top { get; }

    /// <summary>Gets the filtered samples on the left.</summary>
    internal ReadOnlySpan<int> Left { get; }

    /// <summary>Gets the filtered sample above and to the left.</summary>
    internal int Corner { get; }

    /// <summary>Gets a sample of the top row, where index -1 is the corner.</summary>
    /// <param name="index">The column, from -1.</param>
    /// <returns>The sample.</returns>
    internal int TopAt(int index) => index < 0 ? Corner : Top[index];

    /// <summary>Gets a sample of the left column, where index -1 is the corner.</summary>
    /// <param name="index">The row, from -1.</param>
    /// <returns>The sample.</returns>
    internal int LeftAt(int index) => index < 0 ? Corner : Left[index];
}
