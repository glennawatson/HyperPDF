// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Export;

/// <summary>A growable run of characters held in a pooled array, so writing text out costs no builder or chunk allocations.</summary>
internal ref struct TextExportSink
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 256;

    /// <summary>How many times larger the array becomes when it is full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array, or <see langword="null"/> before the first write.</summary>
    private char[]? _buffer;

    /// <summary>The characters written.</summary>
    private int _length;

    /// <summary>Initializes a new instance of the <see cref="TextExportSink"/> struct.</summary>
    /// <param name="capacity">The characters expected.</param>
    internal TextExportSink(int capacity)
    {
        _buffer = ArrayPool<char>.Shared.Rent(Math.Max(capacity, MinimumCapacity));
        _length = 0;
    }

    /// <summary>Adds one character.</summary>
    /// <param name="c">The character.</param>
    internal void Append(char c)
    {
        Reserve(1);
        _buffer![_length] = c;
        _length++;
    }

    /// <summary>Adds text.</summary>
    /// <param name="text">The text.</param>
    internal void Append(ReadOnlySpan<char> text)
    {
        Reserve(text.Length);
        text.CopyTo(_buffer.AsSpan(_length));
        _length += text.Length;
    }

    /// <summary>Makes the text written so far into a string.</summary>
    /// <returns>The string.</returns>
    internal readonly string Build() => _buffer is null ? string.Empty : new(_buffer, 0, _length);

    /// <summary>Gives the rented array back.</summary>
    internal void Dispose()
    {
        if (_buffer is not { } buffer)
        {
            return;
        }

        ArrayPool<char>.Shared.Return(buffer);
        _buffer = null;
    }

    /// <summary>Makes room for more characters.</summary>
    /// <param name="extra">The characters about to be added.</param>
    private void Reserve(int extra)
    {
        _buffer ??= ArrayPool<char>.Shared.Rent(Math.Max(extra, MinimumCapacity));
        if (_length + extra <= _buffer.Length)
        {
            return;
        }

        var bigger = ArrayPool<char>.Shared.Rent(Math.Max(_buffer.Length * GrowthFactor, _length + extra));
        _buffer.AsSpan(0, _length).CopyTo(bigger);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = bigger;
    }
}
