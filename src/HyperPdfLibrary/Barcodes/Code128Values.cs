// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace HyperPdfLibrary.Barcodes;

/// <summary>The values of a Code 128 symbol, kept in a pooled buffer that <see cref="Release"/> returns.</summary>
[DebuggerDisplay("Code128Values: {Count} values")]
internal ref struct Code128Values
{
    /// <summary>The most values a character adds: a switch of code set, then its own value.</summary>
    private const int ValuesPerCharacter = 2;

    /// <summary>The start character, the check character and the stop character.</summary>
    private const int Framing = 3;

    /// <summary>The values so far.</summary>
    private int[]? _values;

    /// <summary>Gets the number of values.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the values so far.</summary>
    internal readonly ReadOnlySpan<int> Span => _values.AsSpan(0, Count);

    /// <summary>Rents a buffer big enough for any symbol of a text.</summary>
    /// <param name="textLength">The characters in the text.</param>
    /// <returns>The empty list of values.</returns>
    internal static Code128Values Create(int textLength) => new() { _values = ArrayPool<int>.Shared.Rent((textLength * ValuesPerCharacter) + Framing) };

    /// <summary>Adds a value.</summary>
    /// <param name="value">The value.</param>
    internal void Add(int value)
    {
        _values![Count] = value;
        Count++;
    }

    /// <summary>Returns the buffer to the pool.</summary>
    internal void Release()
    {
        if (_values is null)
        {
            return;
        }

        ArrayPool<int>.Shared.Return(_values);
        _values = null;
    }
}
