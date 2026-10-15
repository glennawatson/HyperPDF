// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>The lines of a layout, kept in a pooled array. Dispose gives the array back.</summary>
[DebuggerDisplay("TextLines: {Count} lines")]
internal ref struct TextLines
{
    /// <summary>The lines; a pooled array, null until the first line is added.</summary>
    private TextLine[]? _items;

    /// <summary>Gets the number of lines.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a line.</summary>
    /// <param name="index">The line's position.</param>
    internal readonly TextLine this[int index] => _items![index];

    /// <summary>Adds a line.</summary>
    /// <param name="line">The line.</param>
    internal void Add(TextLine line)
    {
        PooledArrays.EnsureRoom(ref _items, Count, clear: false)[Count] = line;
        Count++;
    }

    /// <summary>Removes every line, keeping the array.</summary>
    internal void Clear() => Count = 0;

    /// <summary>Gives the array back.</summary>
    internal void Dispose()
    {
        PooledArrays.Return(ref _items, Count, clear: false);
        Count = 0;
    }
}
