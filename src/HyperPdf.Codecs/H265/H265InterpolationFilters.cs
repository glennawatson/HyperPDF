// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Codecs.H265;

/// <summary>The coefficients of the fractional sample interpolation filters (8.5.3.3.3).</summary>
internal static class H265InterpolationFilters
{
    /// <summary>The taps of the luma filter.</summary>
    internal const int LumaTaps = 8;

    /// <summary>The taps of the chroma filter.</summary>
    internal const int ChromaTaps = 4;

    /// <summary>Gets the luma filters for the quarter positions 1 to 3 as two's complement bytes: eight taps each, the last tap of the first being 0.</summary>
    private static ReadOnlySpan<byte> LumaTable =>
    [
        0xFF, 0x04, 0xF6, 0x3A, 0x11, 0xFB, 0x01, 0x00,
        0xFF, 0x04, 0xF5, 0x28, 0x28, 0xF5, 0x04, 0xFF,
        0x00, 0x01, 0xFB, 0x11, 0x3A, 0xF6, 0x04, 0xFF,
    ];

    /// <summary>Gets the chroma filters for the eighth positions 1 to 7 as two's complement bytes: four taps each.</summary>
    private static ReadOnlySpan<byte> ChromaTable =>
    [
        0xFE, 0x3A, 0x0A, 0xFE,
        0xFC, 0x36, 0x10, 0xFE,
        0xFA, 0x2E, 0x1C, 0xFC,
        0xFC, 0x24, 0x24, 0xFC,
        0xFC, 0x1C, 0x2E, 0xFA,
        0xFE, 0x10, 0x36, 0xFC,
        0xFE, 0x0A, 0x3A, 0xFE,
    ];

    /// <summary>Gets the taps of the luma filter for a quarter sample position.</summary>
    /// <param name="fraction">The position, from 1 to 3.</param>
    /// <returns>The eight taps.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<sbyte> Luma(int fraction) => MemoryMarshal.Cast<byte, sbyte>(LumaTable.Slice((fraction - 1) * LumaTaps, LumaTaps));

    /// <summary>Gets the taps of the chroma filter for an eighth sample position.</summary>
    /// <param name="fraction">The position, from 1 to 7.</param>
    /// <returns>The four taps.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<sbyte> Chroma(int fraction) => MemoryMarshal.Cast<byte, sbyte>(ChromaTable.Slice((fraction - 1) * ChromaTaps, ChromaTaps));
}
