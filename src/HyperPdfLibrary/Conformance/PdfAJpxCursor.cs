// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads JP2 boxes forward through image data, stopping at the end as a stream read would.</summary>
internal ref struct PdfAJpxCursor
{
    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>Initializes a new instance of the <see cref="PdfAJpxCursor"/> struct.</summary>
    /// <param name="data">The data.</param>
    /// <param name="position">The offset to start at.</param>
    internal PdfAJpxCursor(ReadOnlySpan<byte> data, int position)
    {
        _data = data;
        Position = position;
    }

    /// <summary>Gets the offset of the next byte.</summary>
    internal int Position { get; private set; }

    /// <summary>Reads an unsigned big-endian number of up to eight bytes.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <param name="value">The number; it wraps for eight bytes with the top bit set, as veraPDF's does.</param>
    /// <returns><see langword="false"/> when fewer bytes remain.</returns>
    internal bool TryReadNumber(int length, out long value)
    {
        value = 0;
        if (_data.Length - Position < length)
        {
            Position = _data.Length;
            return false;
        }

        foreach (var b in _data.Slice(Position, length))
        {
            value = (value << BitsPerByte) + b;
        }

        Position += length;
        return true;
    }

    /// <summary>Reads bytes.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns><see langword="false"/> when fewer bytes remain.</returns>
    internal bool TryRead(int length, out ReadOnlySpan<byte> bytes)
    {
        if (length < 0 || _data.Length - Position < length)
        {
            bytes = default;
            Position = _data.Length;
            return false;
        }

        bytes = _data.Slice(Position, length);
        Position += length;
        return true;
    }

    /// <summary>Skips bytes, stopping at the end.</summary>
    /// <param name="length">The number of bytes; nothing happens when it is not positive.</param>
    internal void Skip(long length)
    {
        if (length > 0)
        {
            Position = (int)Math.Min(_data.Length, Position + length);
        }
    }
}
