// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The probability estimation state table of the MQ coder (T.88 table E.1), shared by the decoder and the encoder.</summary>
internal static class Jbig2MqStates
{
    /// <summary>
    /// The Qe probability of each state. A static array rather than a span property, because spans of multi-byte
    /// constants allocate on every access in unoptimised builds.
    /// </summary>
    private static readonly ushort[] QeTable =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    /// <summary>Gets the next state after a more probable symbol.</summary>
    private static ReadOnlySpan<byte> NextMpsTable =>
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x26, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x1D, 0x0F, 0x10, 0x11, 0x12, 0x13,
        0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2D, 0x2E,
    ];

    /// <summary>Gets the next state after a less probable symbol.</summary>
    private static ReadOnlySpan<byte> NextLpsTable =>
    [
        0x01, 0x06, 0x09, 0x0C, 0x1D, 0x21, 0x06, 0x0E, 0x0E, 0x0E, 0x11, 0x12, 0x14, 0x15, 0x0E, 0x0E, 0x0F, 0x10, 0x11,
        0x12, 0x13, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23,
        0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2E,
    ];

    /// <summary>Gets whether a less probable symbol swaps the more probable symbol.</summary>
    private static ReadOnlySpan<byte> SwitchTable =>
    [
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Gets the probability of the less probable symbol in a state.</summary>
    /// <param name="state">The state index.</param>
    /// <returns>The Qe value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Qe(int state) => QeTable[state];

    /// <summary>Gets the state after a more probable symbol.</summary>
    /// <param name="state">The state index.</param>
    /// <returns>The next state index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int NextMps(int state) => NextMpsTable[state];

    /// <summary>Gets the state after a less probable symbol.</summary>
    /// <param name="state">The state index.</param>
    /// <returns>The next state index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int NextLps(int state) => NextLpsTable[state];

    /// <summary>Gets whether a less probable symbol in a state swaps the more probable symbol.</summary>
    /// <param name="state">The state index.</param>
    /// <returns><see langword="true"/> when the symbols swap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Switches(int state) => SwitchTable[state] != 0;
}
