// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>A growing buffer of big-endian values, with places that can be filled in later, for writing OpenType tables.</summary>
internal sealed class ByteBuffer
{
    /// <summary>The bytes.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>Gets the number of bytes written.</summary>
    internal int Length => _bytes.Count;

    /// <summary>Writes some unsigned 16-bit values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as bytes.</returns>
    internal static byte[] Words(params int[] values)
    {
        var buffer = new ByteBuffer();
        buffer.Write(values);
        return buffer.ToArray();
    }

    /// <summary>Joins byte arrays.</summary>
    /// <param name="parts">The arrays.</param>
    /// <returns>The joined array.</returns>
    internal static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(static part => part)];

    /// <summary>Writes some unsigned 16-bit values.</summary>
    /// <param name="values">The values.</param>
    internal void Write(params int[] values)
    {
        foreach (var value in values)
        {
            _bytes.Add((byte)(value >> 0x8));
            _bytes.Add((byte)value);
        }
    }

    /// <summary>Writes an unsigned 32-bit value.</summary>
    /// <param name="value">The value.</param>
    internal void Write32(uint value) => Write((int)(value >> 0x10), (int)(value & 0xFFFF));

    /// <summary>Writes some bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    internal void WriteBytes(byte[] bytes) => _bytes.AddRange(bytes);

    /// <summary>Writes a placeholder for a 16-bit value.</summary>
    /// <returns>Where the placeholder is, for <see cref="Patch"/>.</returns>
    internal int Reserve()
    {
        var at = Length;
        Write(0);
        return at;
    }

    /// <summary>Fills in a placeholder.</summary>
    /// <param name="at">Where the placeholder is.</param>
    /// <param name="value">The value.</param>
    internal void Patch(int at, int value)
    {
        _bytes[at] = (byte)(value >> 0x8);
        _bytes[at + 1] = (byte)value;
    }

    /// <summary>Writes a child table after what is written so far and fills in an offset to it.</summary>
    /// <param name="offsetAt">Where the offset placeholder is.</param>
    /// <param name="child">The child table.</param>
    internal void AttachChild(int offsetAt, byte[] child)
    {
        Patch(offsetAt, Length);
        WriteBytes(child);
    }

    /// <summary>Gets the bytes.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToArray() => [.. _bytes];
}
