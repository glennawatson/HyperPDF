// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// The wrapped lines of a block, held in an array rented from the shared pool so laying out a document does not allocate a
/// list. Keep it in a field and pass it by reference; dispose it to return the array.
/// </summary>
[DebuggerDisplay("PooledLines: {Count} lines")]
internal ref struct PooledLines : ILineSink
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 16;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array.</summary>
    private PdfTextLine[]? _items;

    /// <summary>Gets the number of lines.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a line.</summary>
    /// <param name="index">The index of the line.</param>
    /// <returns>The line.</returns>
    internal readonly PdfTextLine this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);
            return _items![index];
        }
    }

    /// <inheritdoc/>
    public void Add(PdfTextLine line)
    {
        if (_items is null || Count == _items.Length)
        {
            Grow();
        }

        _items![Count] = line;
        Count++;
    }

    /// <summary>Returns the array to the pool.</summary>
    internal void Dispose()
    {
        if (_items is { } items)
        {
            _items = null;
            ArrayPool<PdfTextLine>.Shared.Return(items);
        }

        Count = 0;
    }

    /// <summary>Removes every line, keeping the array.</summary>
    internal void Clear() => Count = 0;

    /// <summary>Moves the lines into a larger array.</summary>
    private void Grow()
    {
        var old = _items;
        var larger = ArrayPool<PdfTextLine>.Shared.Rent(old is null ? MinimumCapacity : old.Length * GrowthFactor);
        old?.AsSpan(0, Count).CopyTo(larger);
        _items = larger;
        if (old is not null)
        {
            ArrayPool<PdfTextLine>.Shared.Return(old);
        }
    }
}
