// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Barcodes;

/// <summary>The sizes that ISO/IEC 18004 fixes for each QR Code version and error correction level.</summary>
internal static class QrTables
{
    /// <summary>The smallest version.</summary>
    internal const int MinVersion = 1;

    /// <summary>The largest version.</summary>
    internal const int MaxVersion = 40;

    /// <summary>The modules across a version 1 symbol.</summary>
    private const int BaseSize = 17;

    /// <summary>How many modules a version adds to each side.</summary>
    private const int SizeStep = 4;

    /// <summary>The first version that carries version information and spaces its alignment patterns by rule.</summary>
    private const int FirstVersionWithInfo = 7;

    /// <summary>The version whose alignment patterns are spaced by a fixed step.</summary>
    private const int FixedStepVersion = 32;

    /// <summary>The step between alignment patterns in <see cref="FixedStepVersion"/>.</summary>
    private const int FixedStep = 26;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The error correction bytes in each block, by level then version.</summary>
    private static readonly byte[][] EccPerBlock =
    [
        [7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    /// <summary>How many blocks the data is split into, by level then version.</summary>
    private static readonly byte[][] BlockCounts =
    [
        [1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    /// <summary>Gets the modules across a symbol.</summary>
    /// <param name="version">The version, 1 to 40.</param>
    /// <returns>The size.</returns>
    internal static int GetSize(int version) => (version * SizeStep) + BaseSize;

    /// <summary>Gets how many error correction bytes each block has.</summary>
    /// <param name="version">The version.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The byte count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetEccPerBlock(int version, PdfQrErrorCorrection level) => EccPerBlock[(int)level][version - 1];

    /// <summary>Gets how many blocks the data is split into.</summary>
    /// <param name="version">The version.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The block count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBlockCount(int version, PdfQrErrorCorrection level) => BlockCounts[(int)level][version - 1];

    /// <summary>Gets how many modules can hold data or error correction, after the fixed patterns.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The module count.</returns>
    internal static int GetRawModules(int version)
    {
        const int Sixteen = 16;
        const int OneTwentyEight = 128;
        const int SixtyFour = 64;
        const int TwentyFive = 25;
        const int Ten = 10;
        const int FiftyFive = 55;
        const int VersionInfoModules = 36;
        const int AlignmentBase = 2;
        const int AlignmentPerSeven = 7;

        var result = (((Sixteen * version) + OneTwentyEight) * version) + SixtyFour;
        if (version >= AlignmentBase)
        {
            var count = (version / AlignmentPerSeven) + AlignmentBase;
            result -= (((TwentyFive * count) - Ten) * count) - FiftyFive;
            if (version >= FirstVersionWithInfo)
            {
                result -= VersionInfoModules;
            }
        }

        return result;
    }

    /// <summary>Gets how many data bytes a symbol holds.</summary>
    /// <param name="version">The version.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The byte count.</returns>
    internal static int GetDataBytes(int version, PdfQrErrorCorrection level) =>
        (GetRawModules(version) / ByteBits) - (GetEccPerBlock(version, level) * GetBlockCount(version, level));

    /// <summary>Gets the centres of a version's alignment patterns along one axis.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The positions, lowest first; empty for version 1.</returns>
    internal static int[] GetAlignmentPositions(int version)
    {
        if (version == MinVersion)
        {
            return [];
        }

        const int First = 6;
        const int FromEdge = 7;
        const int Pair = 2;
        const int PerSeven = 7;

        var count = (version / PerSeven) + Pair;
        var step = version == FixedStepVersion
            ? FixedStep
            : (((version * SizeStep) + (count * Pair) + 1) / ((count * Pair) - Pair)) * Pair;
        var result = new int[count];
        result[0] = First;
        for (int i = count - 1, position = GetSize(version) - FromEdge; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }
}
