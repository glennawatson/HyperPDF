// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Reads one Wayland event from a span of bytes.</summary>
internal ref struct WaylandMessageReader
{
    /// <summary>The Wayland word size in bytes.</summary>
    private const int Word = 4;

    /// <summary>The mask of the opcode in the second head word.</summary>
    private const uint OpcodeMask = 0xFFFF;

    /// <summary>The shift of the size in the second head word.</summary>
    private const int SizeShift = 16;

    /// <summary>The bytes of the message after its head.</summary>
    private readonly ReadOnlySpan<byte> _body;

    /// <summary>How far the arguments have been read.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="WaylandMessageReader"/> struct.</summary>
    /// <param name="header">The message head.</param>
    /// <param name="body">The bytes after the head.</param>
    internal WaylandMessageReader(in WaylandHeader header, ReadOnlySpan<byte> body)
    {
        Header = header;
        _body = body;
        _position = 0;
    }

    /// <summary>Gets the message head.</summary>
    internal readonly WaylandHeader Header { get; }

    /// <summary>Tries to read a message head from the front of the stream.</summary>
    /// <param name="stream">The received bytes.</param>
    /// <param name="header">Receives the head.</param>
    /// <returns><see langword="true"/> when the whole message is present.</returns>
    internal static bool TryReadHeader(ReadOnlySpan<byte> stream, out WaylandHeader header)
    {
        header = default;
        if (stream.Length < WaylandHeader.Length)
        {
            return false;
        }

        var second = BinaryPrimitives.ReadUInt32LittleEndian(stream[Word..]);
        var size = (int)(second >> SizeShift);
        if (size < WaylandHeader.Length || stream.Length < size)
        {
            return false;
        }

        header = new(BinaryPrimitives.ReadUInt32LittleEndian(stream), (int)(second & OpcodeMask), size);
        return true;
    }

    /// <summary>Reads an unsigned argument.</summary>
    /// <returns>The value.</returns>
    internal uint ReadUInt()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_body[_position..]);
        _position += Word;
        return value;
    }

    /// <summary>Reads a signed argument.</summary>
    /// <returns>The value.</returns>
    internal int ReadInt() => (int)ReadUInt();

    /// <summary>Reads a string argument.</summary>
    /// <returns>The string without its terminator, or <see langword="null"/> for a null string.</returns>
    internal string? ReadString()
    {
        var length = (int)ReadUInt();
        if (length == 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(_body.Slice(_position, length - 1));
        _position += (length + (Word - 1)) & ~(Word - 1);
        return text;
    }
}
