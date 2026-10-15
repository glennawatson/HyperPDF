// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A text reader over characters held in memory, so XML can be read without first making a string of them.</summary>
[DebuggerDisplay("MemoryTextReader: {_position} of {_text.Length}")]
internal sealed class MemoryTextReader : TextReader
{
    /// <summary>The characters.</summary>
    private readonly ReadOnlyMemory<char> _text;

    /// <summary>The next character to read.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="MemoryTextReader"/> class.</summary>
    /// <param name="text">The characters.</param>
    internal MemoryTextReader(ReadOnlyMemory<char> text) => _text = text;

    /// <inheritdoc/>
    public override int Peek() => _position < _text.Length ? _text.Span[_position] : -1;

    /// <inheritdoc/>
    public override int Read()
    {
        if (_position >= _text.Length)
        {
            return -1;
        }

        var next = _text.Span[_position];
        _position++;
        return next;
    }

    /// <inheritdoc/>
    public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

    /// <inheritdoc/>
    public override int Read(Span<char> buffer)
    {
        var count = Math.Min(buffer.Length, _text.Length - _position);
        _text.Span.Slice(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }
}
