// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>The scan orders of 6.5.3 to 6.5.5: the order in which the positions of a square block are visited.</summary>
internal static class H265ScanOrder
{
    /// <summary>The largest block side the tables cover, as a power of two: blocks of 8.</summary>
    private const int MaxLog2 = 3;

    /// <summary>The number of block sides the tables cover: 1, 2, 4 and 8.</summary>
    private const int Sides = MaxLog2 + 1;

    /// <summary>The up-right diagonal scan of each block side, indexed by the base 2 logarithm of the side.</summary>
    private static readonly H265ScanPosition[][] DiagonalScans = BuildAll(BuildDiagonal);

    /// <summary>The horizontal scan of each block side.</summary>
    private static readonly H265ScanPosition[][] HorizontalScans = BuildAll(BuildHorizontal);

    /// <summary>The vertical scan of each block side.</summary>
    private static readonly H265ScanPosition[][] VerticalScans = BuildAll(BuildVertical);

    /// <summary>Gets the up-right diagonal scan of a block.</summary>
    /// <param name="log2Side">The base 2 logarithm of the block side, from 0 to 3.</param>
    /// <returns>The positions in scan order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<H265ScanPosition> Diagonal(int log2Side) => DiagonalScans[log2Side];

    /// <summary>Gets the horizontal scan of a block.</summary>
    /// <param name="log2Side">The base 2 logarithm of the block side, from 0 to 3.</param>
    /// <returns>The positions in scan order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<H265ScanPosition> Horizontal(int log2Side) => HorizontalScans[log2Side];

    /// <summary>Gets the vertical scan of a block.</summary>
    /// <param name="log2Side">The base 2 logarithm of the block side, from 0 to 3.</param>
    /// <returns>The positions in scan order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<H265ScanPosition> Vertical(int log2Side) => VerticalScans[log2Side];

    /// <summary>Builds a scan for every block side.</summary>
    /// <param name="build">Builds the scan of one side.</param>
    /// <returns>The scans.</returns>
    private static H265ScanPosition[][] BuildAll(Func<int, H265ScanPosition[]> build)
    {
        var scans = new H265ScanPosition[Sides][];
        for (var log2 = 0; log2 < Sides; log2++)
        {
            scans[log2] = build(1 << log2);
        }

        return scans;
    }

    /// <summary>Builds the up-right diagonal scan (6.5.3): each diagonal runs from its bottom left to its top right.</summary>
    /// <param name="side">The block side.</param>
    /// <returns>The positions.</returns>
    private static H265ScanPosition[] BuildDiagonal(int side)
    {
        var scan = new H265ScanPosition[side * side];
        var index = 0;
        var x = 0;
        var y = 0;
        while (index < scan.Length)
        {
            while (y >= 0)
            {
                if (x < side && y < side)
                {
                    scan[index] = new((byte)x, (byte)y);
                    index++;
                }

                y--;
                x++;
            }

            y = x;
            x = 0;
        }

        return scan;
    }

    /// <summary>Builds the horizontal scan (6.5.4): row by row.</summary>
    /// <param name="side">The block side.</param>
    /// <returns>The positions.</returns>
    private static H265ScanPosition[] BuildHorizontal(int side)
    {
        var scan = new H265ScanPosition[side * side];
        for (var i = 0; i < scan.Length; i++)
        {
            scan[i] = new((byte)(i % side), (byte)(i / side));
        }

        return scan;
    }

    /// <summary>Builds the vertical scan (6.5.5): column by column.</summary>
    /// <param name="side">The block side.</param>
    /// <returns>The positions.</returns>
    private static H265ScanPosition[] BuildVertical(int side)
    {
        var scan = new H265ScanPosition[side * side];
        for (var i = 0; i < scan.Length; i++)
        {
            scan[i] = new((byte)(i / side), (byte)(i % side));
        }

        return scan;
    }
}
