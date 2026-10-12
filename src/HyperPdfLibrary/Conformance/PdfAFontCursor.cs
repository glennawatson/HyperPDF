// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A big-endian reader over font data with veraPDF's limits: reading past the end or seeking outside the data fails,
/// skipping past the end stops at the end.
/// </summary>
[DebuggerDisplay("PdfAFontCursor: {Position} of {_data.Length}")]
internal sealed class PdfAFontCursor
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in two bytes.</summary>
    private const int ShortBits = 16;

    /// <summary>The data.</summary>
    private readonly byte[] _data;

    /// <summary>Initializes a new instance of the <see cref="PdfAFontCursor"/> class.</summary>
    /// <param name="data">The data.</param>
    internal PdfAFontCursor(byte[] data) => _data = data;

    /// <summary>Gets the current position.</summary>
    internal long Position { get; private set; }

    /// <summary>Gets the data length.</summary>
    internal long Length => _data.Length;

    /// <summary>Moves to an offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <exception cref="InvalidDataException">The offset is outside the data.</exception>
    internal void Seek(long offset)
    {
        if (offset < 0 || offset > _data.Length)
        {
            throw new InvalidDataException("The font data ends before the offset.");
        }

        Position = offset;
    }

    /// <summary>Skips bytes, stopping at the end of the data.</summary>
    /// <param name="count">The number of bytes.</param>
    internal void Skip(int count) => Position = Math.Min(_data.Length, Position + count);

    /// <summary>Reads a byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The data has ended.</exception>
    internal int ReadByte()
    {
        if (Position >= _data.Length)
        {
            throw new InvalidDataException("The font data ended.");
        }

        var value = _data[Position];
        Position++;
        return value;
    }

    /// <summary>Reads bytes without failing at the end: what is missing reads as zero.</summary>
    /// <param name="destination">Receives the bytes.</param>
    internal void ReadAvailable(Span<byte> destination)
    {
        destination.Clear();
        var available = (int)Math.Min(destination.Length, _data.Length - Position);
        _data.AsSpan((int)Position, available).CopyTo(destination);
        Position += available;
    }

    /// <summary>Reads an unsigned 16-bit number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The data has ended.</exception>
    internal int ReadUInt16()
    {
        var high = ReadByte();
        return (high << ByteBits) | ReadByte();
    }

    /// <summary>Reads a signed 16-bit number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The data has ended.</exception>
    internal int ReadInt16() => (short)ReadUInt16();

    /// <summary>Reads an unsigned 32-bit number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The data has ended.</exception>
    internal long ReadUInt32()
    {
        long high = ReadUInt16();
        return (high << ShortBits) | (uint)ReadUInt16();
    }
}
