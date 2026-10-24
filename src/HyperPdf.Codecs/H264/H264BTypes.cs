// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>What the macroblock types and sub-macroblock types of B slices say about prediction (Tables 7-14 and 7-18 of ITU-T H.264).</summary>
internal static class H264BTypes
{
    /// <summary>The mb_type of a macroblock predicted directly.</summary>
    internal const int Direct16x16 = 0;

    /// <summary>The mb_type of a macroblock of four 8x8 partitions.</summary>
    internal const int Partitions8x8 = 22;

    /// <summary>The first mb_type of the intra macroblocks in a B slice.</summary>
    internal const int IntraOffset = 23;

    /// <summary>The prediction mode of a partition that uses list 0 only.</summary>
    internal const int ModeL0 = 1;

    /// <summary>The prediction mode of a partition that uses list 1 only.</summary>
    internal const int ModeL1 = 2;

    /// <summary>The prediction mode of a partition that uses both lists.</summary>
    internal const int ModeBi = 3;

    /// <summary>The shape code of a single 16x16 partition.</summary>
    internal const int Shape16x16 = 0;

    /// <summary>The shape code of two 16x8 partitions.</summary>
    internal const int Shape16x8 = 1;

    /// <summary>The shape code of two 8x16 partitions.</summary>
    internal const int Shape8x16 = 2;

    /// <summary>The sub_mb_type of a direct 8x8 block.</summary>
    internal const int SubDirect = 0;

    /// <summary>The first mb_type of the types that have two partitions.</summary>
    private const int FirstTwoPartition = 4;

    /// <summary>The last mb_type of the types that have two partitions.</summary>
    private const int LastTwoPartition = 21;

    /// <summary>The prediction modes kept for each pair of mb_types.</summary>
    private const int ModesPerPair = 2;

    /// <summary>Gets the prediction modes of the first and second partition for the mb_type pairs 4 and 5, 6 and 7, and so on up to 20 and 21.</summary>
    private static ReadOnlySpan<byte> PairModes => [0x1, 0x1, 0x2, 0x2, 0x1, 0x2, 0x2, 0x1, 0x1, 0x3, 0x2, 0x3, 0x3, 0x1, 0x3, 0x2, 0x3, 0x3];

    /// <summary>Gets the prediction mode of each sub_mb_type from 0 to 12 (0 is direct).</summary>
    private static ReadOnlySpan<byte> SubModes => [0x0, 0x1, 0x2, 0x3, 0x1, 0x1, 0x2, 0x2, 0x3, 0x3, 0x1, 0x2, 0x3];

    /// <summary>Gets the shape of each sub_mb_type: 0 for 8x8, 1 for 8x4, 2 for 4x8, 3 for 4x4.</summary>
    private static ReadOnlySpan<byte> SubShapes => [0x0, 0x0, 0x0, 0x0, 0x1, 0x2, 0x1, 0x2, 0x1, 0x2, 0x3, 0x3, 0x3];

    /// <summary>Gets the shape of a mb_type from 1 to 21.</summary>
    /// <param name="type">The mb_type.</param>
    /// <returns><see cref="Shape16x16"/>, <see cref="Shape16x8"/> or <see cref="Shape8x16"/>.</returns>
    internal static int ShapeOf(int type)
    {
        if (type < FirstTwoPartition)
        {
            return Shape16x16;
        }

        return (type & 1) == 0 ? Shape16x8 : Shape8x16;
    }

    /// <summary>Gets the prediction mode of a partition of a mb_type from 1 to 21.</summary>
    /// <param name="type">The mb_type.</param>
    /// <param name="partition">The partition, 0 or 1.</param>
    /// <returns>The mode: <see cref="ModeL0"/>, <see cref="ModeL1"/> or <see cref="ModeBi"/>.</returns>
    internal static int ModeOf(int type, int partition) =>
        type < FirstTwoPartition ? type : PairModes[(((type - FirstTwoPartition) >> 1) * ModesPerPair) + partition];

    /// <summary>Tells whether a mb_type has two partitions.</summary>
    /// <param name="type">The mb_type.</param>
    /// <returns><see langword="true"/> for 4 to 21.</returns>
    internal static bool HasTwoPartitions(int type) => type is >= FirstTwoPartition and <= LastTwoPartition;

    /// <summary>Gets the prediction mode of a sub_mb_type.</summary>
    /// <param name="subType">The sub_mb_type, 0 to 12.</param>
    /// <returns>0 for direct, otherwise <see cref="ModeL0"/>, <see cref="ModeL1"/> or <see cref="ModeBi"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SubModeOf(int subType) => SubModes[subType];

    /// <summary>Gets the shape of a sub_mb_type.</summary>
    /// <param name="subType">The sub_mb_type, 0 to 12.</param>
    /// <returns>0 for 8x8, 1 for 8x4, 2 for 4x8, 3 for 4x4.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SubShapeOf(int subType) => SubShapes[subType];
}
