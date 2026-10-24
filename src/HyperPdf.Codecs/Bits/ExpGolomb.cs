// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
namespace HyperPdf.Codecs.Bits;

/// <summary>Reads the Exp-Golomb numbers and the trailing bits that H.264 syntax is written with.</summary>
internal static class ExpGolomb
{
    /// <summary>The most zero bits an Exp-Golomb number may start with: more would not fit in 32 bits.</summary>
    private const int MaxLeadingZeros = 31;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Reads an unsigned Exp-Golomb number, ue(v).</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The number, or 0 when the bits are damaged, which the reader's overrun flag or the caller's range checks then show.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ReadUe(ref MsbBitReader reader)
    {
        var zeros = reader.ReadUnary();
        return zeros > MaxLeadingZeros ? 0 : (uint)((1UL << zeros) - 1) + reader.Read(zeros);
    }

    /// <summary>Reads a signed Exp-Golomb number, se(v).</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ReadSe(ref MsbBitReader reader)
    {
        var code = ReadUe(ref reader);
        var magnitude = (int)((code + 1) >> 1);
        return (code & 1) != 0 ? magnitude : -magnitude;
    }

    /// <summary>Reads a truncated Exp-Golomb number, te(v).</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="maximum">The largest value, which decides whether one inverted bit or an Exp-Golomb number is read.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ReadTe(ref MsbBitReader reader, int maximum)
    {
        if (maximum > 1)
        {
            return (int)ReadUe(ref reader);
        }

        return reader.ReadBit() ? 0 : 1;
    }

    /// <summary>Finds the position of the stop bit that ends the data of a raw byte sequence payload.</summary>
    /// <param name="rbsp">The payload.</param>
    /// <returns>The bit index of the stop bit, or -1 when the payload has no set bit.</returns>
    internal static long StopBit(ReadOnlySpan<byte> rbsp)
    {
        for (var index = rbsp.Length - 1; index >= 0; index--)
        {
            if (rbsp[index] != 0)
            {
                return ((long)index * ByteBits) + (ByteBits - 1 - BitOperations.TrailingZeroCount((uint)rbsp[index]));
            }
        }

        return -1;
    }

    /// <summary>Tells whether more data follows before the trailing bits of the payload, more_rbsp_data().</summary>
    /// <param name="bitPosition">The reader's bit position.</param>
    /// <param name="stopBit">The bit index of the stop bit, from <see cref="StopBit"/>.</param>
    /// <returns><see langword="true"/> when the reader has not reached the stop bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MoreData(long bitPosition, long stopBit) => bitPosition < stopBit;
}
