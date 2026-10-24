// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>The neighbouring samples of an intra block in one line, from the left column's bottom through the corner to the top row's end.</summary>
internal readonly ref struct H265IntraNeighbours
{
    /// <summary>The line of samples.</summary>
    private readonly ReadOnlySpan<int> _line;

    /// <summary>The index of the corner sample, which is where the left column ends and the top row begins.</summary>
    private readonly int _corner;

    /// <summary>Initializes a new instance of the <see cref="H265IntraNeighbours"/> struct.</summary>
    /// <param name="line">The line of 4n+1 samples.</param>
    /// <param name="size">The block side.</param>
    internal H265IntraNeighbours(ReadOnlySpan<int> line, int size)
    {
        _line = line;
        _corner = size << 1;
    }

    /// <summary>Gets the sample above and to the left of the block.</summary>
    internal int Corner => _line[_corner];

    /// <summary>Gets a sample of the left column.</summary>
    /// <param name="y">The row, from 0 to 2n-1, or -1 for the corner.</param>
    /// <returns>The sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Left(int y) => _line[_corner - 1 - y];

    /// <summary>Gets a sample of the top row.</summary>
    /// <param name="x">The column, from 0 to 2n-1, or -1 for the corner.</param>
    /// <returns>The sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Top(int x) => _line[_corner + 1 + x];
}
