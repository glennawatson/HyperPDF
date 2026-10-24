// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The mapping of the coded number of the coded block pattern to the pattern, as Table 9-4(a) of ITU-T H.264 gives it for 4:2:0 and 4:2:2 video.</summary>
internal static class H264CodedBlockPattern
{
    /// <summary>The largest code number.</summary>
    internal const int MaxCodeNumber = 47;

    /// <summary>The mask of the luma part of a pattern.</summary>
    internal const int LumaMask = 0xF;

    /// <summary>The shift of the chroma part of a pattern.</summary>
    internal const int ChromaShift = 4;

    /// <summary>Gets the patterns of intra macroblocks, by code number.</summary>
    private static ReadOnlySpan<byte> Intra =>
    [
        0x2F, 0x1F, 0xF, 0x0, 0x17, 0x1B, 0x1D, 0x1E, 0x7, 0xB, 0xD, 0xE, 0x27, 0x2B, 0x2D, 0x2E,
        0x10, 0x3, 0x5, 0xA, 0xC, 0x13, 0x15, 0x1A, 0x1C, 0x23, 0x25, 0x2A, 0x2C, 0x1, 0x2, 0x4,
        0x8, 0x11, 0x12, 0x14, 0x18, 0x6, 0x9, 0x16, 0x19, 0x20, 0x21, 0x22, 0x24, 0x28, 0x26, 0x29,
    ];

    /// <summary>Gets the patterns of inter macroblocks, by code number.</summary>
    private static ReadOnlySpan<byte> Inter =>
    [
        0x0, 0x10, 0x1, 0x2, 0x4, 0x8, 0x20, 0x3, 0x5, 0xA, 0xC, 0xF, 0x2F, 0x7, 0xB, 0xD,
        0xE, 0x6, 0x9, 0x1F, 0x23, 0x25, 0x2A, 0x2C, 0x21, 0x22, 0x24, 0x28, 0x27, 0x2B, 0x2D, 0x2E,
        0x11, 0x12, 0x14, 0x18, 0x13, 0x15, 0x1A, 0x1C, 0x17, 0x1B, 0x1D, 0x1E, 0x16, 0x19, 0x26, 0x29,
    ];

    /// <summary>Looks up a pattern.</summary>
    /// <param name="codeNumber">The coded number, from 0 to 47.</param>
    /// <param name="isIntra">Whether the macroblock is intra predicted with 4x4 or 8x8 blocks.</param>
    /// <returns>The pattern: the luma bits in the low four bits and the chroma part, 0 to 2, above them.</returns>
    internal static int Lookup(int codeNumber, bool isIntra) => isIntra ? Intra[codeNumber] : Inter[codeNumber];
}
