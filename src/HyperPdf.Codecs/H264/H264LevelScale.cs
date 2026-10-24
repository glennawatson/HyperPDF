// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>The level scale factors of the 4x4 transform for each scaling list and each remainder of the quantiser divided by 6.</summary>
internal sealed class H264LevelScale
{
    /// <summary>The values the quantiser remainder can have.</summary>
    private const int Remainders = 6;

    /// <summary>The pixels in a 4x4 block.</summary>
    private const int BlockSize = 16;

    /// <summary>The width of a 4x4 block.</summary>
    private const int BlockWidth = 4;

    /// <summary>The kinds of position in a block that have their own base factor.</summary>
    private const int Kinds = 3;

    /// <summary>The kind of the positions with an even row and an even column.</summary>
    private const int EvenEven = 0;

    /// <summary>The kind of the positions with an odd row and an odd column.</summary>
    private const int OddOdd = 1;

    /// <summary>The kind of the other positions.</summary>
    private const int Mixed = 2;

    /// <summary>The samples in an 8x8 block.</summary>
    private const int LargeBlockSize = 64;

    /// <summary>The width of an 8x8 block.</summary>
    private const int LargeBlockWidth = 8;

    /// <summary>The mask that takes a position modulo 4.</summary>
    private const int QuarterMask = 3;

    /// <summary>The kinds of position in an 8x8 block that have their own base factor.</summary>
    private const int Kinds8x8 = 6;

    /// <summary>The factors, by scaling list, then remainder, then raster position.</summary>
    private readonly int[] _factors = new int[H264ScalingLists.Count4x4 * Remainders * BlockSize];

    /// <summary>The factors of 8x8 blocks, by scaling list, then remainder, then raster position.</summary>
    private readonly int[] _factors8 = new int[H264ScalingLists.Count8x8 * Remainders * LargeBlockSize];

    /// <summary>Initializes a new instance of the <see cref="H264LevelScale"/> class.</summary>
    /// <param name="lists">The scaling lists.</param>
    internal H264LevelScale(H264ScalingLists lists)
    {
        var zigzag = H264Tables.ZigZag4x4;
        for (var list = 0; list < H264ScalingLists.Count4x4; list++)
        {
            for (var remainder = 0; remainder < Remainders; remainder++)
            {
                for (var scan = 0; scan < BlockSize; scan++)
                {
                    var position = zigzag[scan];
                    _factors[(((list * Remainders) + remainder) * BlockSize) + position] = lists.Lists4x4[list][scan] * NormAdjust(remainder, position);
                }
            }
        }

        var zigzag8 = H264Tables.ZigZag8x8;
        for (var list = 0; list < H264ScalingLists.Count8x8; list++)
        {
            for (var remainder = 0; remainder < Remainders; remainder++)
            {
                for (var scan = 0; scan < LargeBlockSize; scan++)
                {
                    var position = zigzag8[scan];
                    _factors8[(((list * Remainders) + remainder) * LargeBlockSize) + position] = lists.Lists8x8[list][scan] * NormAdjust8x8(remainder, position);
                }
            }
        }
    }

    /// <summary>Gets the six base factors of 8x8 blocks for each remainder (the matrix of 8-318, one row per remainder).</summary>
    private static ReadOnlySpan<byte> Bases8x8 =>
    [
        0x14, 0x12, 0x20, 0x13, 0x19, 0x18,
        0x16, 0x13, 0x23, 0x15, 0x1C, 0x1A,
        0x1A, 0x17, 0x2A, 0x18, 0x21, 0x1F,
        0x1C, 0x19, 0x2D, 0x1A, 0x23, 0x21,
        0x20, 0x1C, 0x33, 0x1E, 0x28, 0x26,
        0x24, 0x20, 0x3A, 0x22, 0x2E, 0x2B,
    ];

    /// <summary>Gets which base factor each position of a 4x4 pattern takes in an 8x8 block, by row then column of the position modulo 4.</summary>
    private static ReadOnlySpan<byte> PositionKinds8x8 => [0x0, 0x3, 0x4, 0x3, 0x3, 0x1, 0x5, 0x1, 0x4, 0x5, 0x2, 0x5, 0x3, 0x1, 0x5, 0x1];

    /// <summary>Gets the three base factors for each remainder: for even-even positions, odd-odd positions, and the rest.</summary>
    private static ReadOnlySpan<byte> Bases => [0xA, 0x10, 0xD, 0xB, 0x12, 0xE, 0xD, 0x14, 0x10, 0xE, 0x17, 0x12, 0x10, 0x19, 0x14, 0x12, 0x1D, 0x17];

    /// <summary>Gets the 16 factors of a scaling list and remainder, by raster position.</summary>
    /// <param name="list">The scaling list: 0 to 2 for intra, 3 to 5 for inter.</param>
    /// <param name="remainder">The quantiser modulo 6.</param>
    /// <returns>The factors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<int> Factors(int list, int remainder) => _factors.AsSpan(((list * Remainders) + remainder) * BlockSize, BlockSize);

    /// <summary>Gets the 64 factors of an 8x8 scaling list and remainder, by raster position.</summary>
    /// <param name="list">The 8x8 scaling list: 0 for intra luma, 1 for inter luma.</param>
    /// <param name="remainder">The quantiser modulo 6.</param>
    /// <returns>The factors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<int> Factors8x8(int list, int remainder) => _factors8.AsSpan(((list * Remainders) + remainder) * LargeBlockSize, LargeBlockSize);

    /// <summary>Gets the base factor of an 8x8 block for a position (8-317 and 8-318).</summary>
    /// <param name="remainder">The quantiser modulo 6.</param>
    /// <param name="position">The raster position in the block.</param>
    /// <returns>The factor.</returns>
    private static int NormAdjust8x8(int remainder, int position)
    {
        var row = (position / LargeBlockWidth) & QuarterMask;
        var column = (position % LargeBlockWidth) & QuarterMask;
        return Bases8x8[(remainder * Kinds8x8) + PositionKinds8x8[(row * BlockWidth) + column]];
    }

    /// <summary>Gets the base factor for a position.</summary>
    /// <param name="remainder">The quantiser modulo 6.</param>
    /// <param name="position">The raster position in the block.</param>
    /// <returns>The factor.</returns>
    private static int NormAdjust(int remainder, int position)
    {
        var oddRow = ((position / BlockWidth) & 1) == 1;
        var oddColumn = ((position % BlockWidth) & 1) == 1;
        var kind = Mixed;
        if (oddRow == oddColumn)
        {
            kind = oddRow ? OddOdd : EvenEven;
        }

        return Bases[(remainder * Kinds) + kind];
    }
}
