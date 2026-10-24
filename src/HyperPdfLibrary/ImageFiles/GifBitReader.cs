// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads codes of varying width from GIF image data, packed from the low bit of each byte.</summary>
/// <param name="data">The joined data sub-blocks.</param>
[DebuggerDisplay("GifBitReader: byte {_position} of {data.Length}")]
internal sealed class GifBitReader(byte[] data)
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Bits read but not yet used.</summary>
    private int _buffer;

    /// <summary>How many bits <see cref="_buffer"/> holds.</summary>
    private int _count;

    /// <summary>The position of the next byte to read.</summary>
    private int _position;

    /// <summary>Reads one code.</summary>
    /// <param name="size">The width of the code in bits.</param>
    /// <param name="code">Receives the code.</param>
    /// <returns><see langword="false"/> when the data ends before a whole code.</returns>
    internal bool TryRead(int size, out int code)
    {
        while (_count < size && _position < data.Length)
        {
            _buffer |= data[_position] << _count;
            _position++;
            _count += ByteBits;
        }

        code = _buffer & ((1 << size) - 1);
        if (_count < size)
        {
            return false;
        }

        _buffer >>= size;
        _count -= size;
        return true;
    }
}
