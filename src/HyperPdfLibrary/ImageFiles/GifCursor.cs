// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads a GIF file from front to back, and fails with a clear error when the file ends too soon.</summary>
/// <param name="data">The file's bytes.</param>
[DebuggerDisplay("GifCursor: {Position} of {data.Length}")]
internal sealed class GifCursor(byte[] data)
{
    /// <summary>Gets the position of the next byte.</summary>
    internal int Position { get; private set; }

    /// <summary>Gets a value indicating whether every byte has been read.</summary>
    internal bool AtEnd => Position >= data.Length;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The file ends first.</exception>
    internal byte ReadByte()
    {
        Require(1);
        var value = data[Position];
        Position++;
        return value;
    }

    /// <summary>Reads a 16-bit number, least significant byte first.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The file ends first.</exception>
    internal int ReadUInt16()
    {
        var low = ReadByte();
        var high = ReadByte();
        return low | (high << 8);
    }

    /// <summary>Looks at a byte after the next without moving.</summary>
    /// <param name="ahead">How many bytes after the next one.</param>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The file ends first.</exception>
    internal byte Peek(int ahead)
    {
        Require(ahead + 1);
        return data[Position + ahead];
    }

    /// <summary>Moves past some bytes.</summary>
    /// <param name="count">How many.</param>
    /// <exception cref="InvalidDataException">The file ends first.</exception>
    internal void Skip(int count)
    {
        Require(count);
        Position += count;
    }

    /// <summary>Reads some bytes.</summary>
    /// <param name="count">How many.</param>
    /// <returns>A copy of the bytes.</returns>
    /// <exception cref="InvalidDataException">The file ends first.</exception>
    internal byte[] ReadBytes(int count)
    {
        Require(count);
        var bytes = data.AsSpan(Position, count).ToArray();
        Position += count;
        return bytes;
    }

    /// <summary>Reads data sub-blocks up to the empty one that ends them, and joins them.</summary>
    /// <returns>The joined data.</returns>
    /// <exception cref="InvalidDataException">The file ends before the end of the blocks.</exception>
    internal byte[] ReadSubBlocks()
    {
        using var joined = new MemoryStream();
        var length = ReadByte();
        while (length != 0)
        {
            Require(length);
            joined.Write(data, Position, length);
            Position += length;
            length = ReadByte();
        }

        return joined.ToArray();
    }

    /// <summary>Checks that some bytes remain.</summary>
    /// <param name="count">The bytes needed.</param>
    /// <exception cref="InvalidDataException">Fewer bytes remain.</exception>
    private void Require(int count)
    {
        if (count < 0 || Position + count > data.Length)
        {
            throw new InvalidDataException("The GIF is cut short.");
        }
    }
}
