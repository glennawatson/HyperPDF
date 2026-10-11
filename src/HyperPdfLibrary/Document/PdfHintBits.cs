// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Reads the big-endian packed integer arrays in a PDF hint stream.</summary>
/// <param name="data">The encoded hint bytes.</param>
/// <param name="positionBits">The first bit to read.</param>
[DebuggerDisplay("PdfHintBits: {PositionBits} bits")]
internal ref struct PdfHintBits(ReadOnlySpan<byte> data, long positionBits)
{
    /// <summary>The bits in one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The longest packed unsigned integer.</summary>
    private const int MaxWidth = 32;

    /// <summary>The encoded hint bytes.</summary>
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>Gets the current bit position.</summary>
    internal long PositionBits { get; private set; } = positionBits;

    /// <summary>Gets the unread bit count.</summary>
    internal readonly long RemainingBits => ((long)_data.Length * ByteBits) - PositionBits;

    /// <summary>Reads a packed unsigned integer, including a zero-width value.</summary>
    /// <param name="width">The number of bits, from zero through 32.</param>
    /// <param name="value">The decoded value.</param>
    /// <returns>Whether the bits were present.</returns>
    internal bool TryRead(int width, out uint value)
    {
        value = 0;
        if (width is < 0 or > MaxWidth || RemainingBits < width)
        {
            return false;
        }

        for (var i = 0; i < width; i++)
        {
            value = (value << 1) | (uint)((_data[(int)(PositionBits / ByteBits)] >> (ByteBits - 1 - (int)(PositionBits % ByteBits))) & 1);
            PositionBits++;
        }

        return true;
    }

    /// <summary>Skips a count of packed values without multiplying beyond the available data.</summary>
    /// <param name="count">The number of values.</param>
    /// <param name="width">Each value's bit width.</param>
    /// <returns>Whether all bits were present.</returns>
    internal bool TrySkipValues(long count, int width)
    {
        if (count < 0 || width is < 0 or > MaxWidth)
        {
            return false;
        }

        if (width == 0)
        {
            return true;
        }

        if (count > RemainingBits / width)
        {
            return false;
        }

        PositionBits += count * width;
        return true;
    }

    /// <summary>Moves to the next byte boundary.</summary>
    internal void Align() => PositionBits = (PositionBits + (ByteBits - 1)) & ~(ByteBits - 1L);
}
