// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Containers;

/// <summary>
/// A sliding window over a file for container readers: it hands out spans of the bytes at any offset, reading the file in large
/// chunks so a reader that takes a few hundred bytes at a time makes few reads.
/// </summary>
[DebuggerDisplay("FileWindow: {_start} + {_length}")]
internal sealed class FileWindow
{
    /// <summary>The bytes read from the file at a time.</summary>
    private const int ReadSize = 65_536;

    /// <summary>The file.</summary>
    private readonly Stream _stream;

    /// <summary>The bytes held.</summary>
    private byte[] _buffer = new byte[ReadSize << 1];

    /// <summary>The file offset of the first byte held.</summary>
    private long _start;

    /// <summary>The number of bytes held.</summary>
    private int _length;

    /// <summary>Initializes a new instance of the <see cref="FileWindow"/> class.</summary>
    /// <param name="stream">The file; it must be able to seek.</param>
    internal FileWindow(Stream stream) => _stream = stream;

    /// <summary>Gets the length of the file.</summary>
    internal long Length => _stream.Length;

    /// <summary>Gets the bytes from an offset.</summary>
    /// <param name="offset">The file offset.</param>
    /// <param name="minimum">The bytes wanted; fewer come back only at the end of the file.</param>
    /// <returns>The bytes from the offset, at least <paramref name="minimum"/> of them unless the file ends first.</returns>
    internal ReadOnlySpan<byte> Get(long offset, int minimum)
    {
        if (offset < 0 || offset >= _stream.Length)
        {
            return default;
        }

        var relative = offset - _start;
        if (relative >= 0 && relative + minimum <= _length)
        {
            return _buffer.AsSpan((int)relative, _length - (int)relative);
        }

        Move(offset);
        if (_buffer.Length < minimum + ReadSize)
        {
            Array.Resize(ref _buffer, minimum + ReadSize);
        }

        while (_length < minimum)
        {
            _stream.Position = _start + _length;
            var read = _stream.Read(_buffer, _length, _buffer.Length - _length);
            if (read == 0)
            {
                break;
            }

            _length += read;
        }

        return _buffer.AsSpan(0, _length);
    }

    /// <summary>Makes the window start at an offset, keeping the bytes it already holds from there on.</summary>
    /// <param name="offset">The file offset.</param>
    private void Move(long offset)
    {
        var relative = offset - _start;
        if (relative >= 0 && relative <= _length)
        {
            Buffer.BlockCopy(_buffer, (int)relative, _buffer, 0, _length - (int)relative);
            _length -= (int)relative;
        }
        else
        {
            _length = 0;
        }

        _start = offset;
    }
}
