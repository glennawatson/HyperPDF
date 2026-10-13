// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The glyphs of a run while it is shaped, kept in arrays rented from the shared pool.</summary>
[DebuggerDisplay("GlyphBuffer: {Count} glyphs")]
internal sealed class GlyphBuffer : IDisposable
{
    /// <summary>How many times larger the arrays get when they are full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The glyphs, in logical order.</summary>
    private GlyphRecord[] _items;

    /// <summary>The positions, parallel to the glyphs; empty until positioning starts.</summary>
    private GlyphPosition[] _positions = [];

    /// <summary>Initializes a new instance of the <see cref="GlyphBuffer"/> class.</summary>
    /// <param name="capacity">The number of glyphs to make room for.</param>
    internal GlyphBuffer(int capacity) => _items = ArrayPool<GlyphRecord>.Shared.Rent(Math.Max(capacity, 1));

    /// <summary>Gets the number of glyphs.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the glyphs.</summary>
    internal Span<GlyphRecord> Items => _items.AsSpan(0, Count);

    /// <summary>Gets the positions of the glyphs.</summary>
    internal Span<GlyphPosition> Positions => _positions.AsSpan(0, Count);

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<GlyphRecord>.Shared.Return(_items);
        _items = [];
        Return(ref _positions);
        Count = 0;
    }

    /// <summary>Adds a glyph at the end.</summary>
    /// <param name="glyph">The glyph.</param>
    internal void Add(GlyphRecord glyph)
    {
        Reserve(Count + 1);
        _items[Count] = glyph;
        Count++;
    }

    /// <summary>Replaces some glyphs with others, shifting the glyphs after them.</summary>
    /// <param name="index">The index of the first glyph to remove.</param>
    /// <param name="removed">The number of glyphs to remove.</param>
    /// <param name="added">The glyphs that take their place.</param>
    internal void Replace(int index, int removed, ReadOnlySpan<GlyphRecord> added)
    {
        var tail = Count - index - removed;
        Reserve(Count - removed + added.Length);
        _items.AsSpan(index + removed, tail).CopyTo(_items.AsSpan(index + added.Length));
        added.CopyTo(_items.AsSpan(index));
        Count += added.Length - removed;
    }

    /// <summary>Removes some glyphs, shifting the glyphs after them.</summary>
    /// <param name="index">The index of the first glyph to remove.</param>
    /// <param name="removed">The number of glyphs to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Remove(int index, int removed) => Replace(index, removed, []);

    /// <summary>Starts positioning: gives each glyph a position with no adjustment.</summary>
    internal void BeginPositions()
    {
        if (_positions.Length < _items.Length)
        {
            Return(ref _positions);
            _positions = ArrayPool<GlyphPosition>.Shared.Rent(_items.Length);
        }

        for (var i = 0; i < Count; i++)
        {
            _positions[i] = new(0, 0, 0);
        }
    }

    /// <summary>Moves a glyph to another index, shifting the glyphs between.</summary>
    /// <param name="from">The glyph's index.</param>
    /// <param name="to">The index it moves to.</param>
    internal void Move(int from, int to)
    {
        if (from == to)
        {
            return;
        }

        var glyph = _items[from];
        if (from < to)
        {
            _items.AsSpan(from + 1, to - from).CopyTo(_items.AsSpan(from));
        }
        else
        {
            _items.AsSpan(to, from - to).CopyTo(_items.AsSpan(to + 1));
        }

        _items[to] = glyph;
    }

    /// <summary>Returns the position array to the pool.</summary>
    /// <param name="positions">The array, which is emptied.</param>
    private static void Return(ref GlyphPosition[] positions)
    {
        if (positions.Length > 0)
        {
            ArrayPool<GlyphPosition>.Shared.Return(positions);
        }

        positions = [];
    }

    /// <summary>Makes room for some glyphs.</summary>
    /// <param name="capacity">The number of glyphs.</param>
    private void Reserve(int capacity)
    {
        if (capacity <= _items.Length)
        {
            return;
        }

        var larger = ArrayPool<GlyphRecord>.Shared.Rent(Math.Max(capacity, _items.Length * GrowthFactor));
        _items.AsSpan(0, Count).CopyTo(larger);
        ArrayPool<GlyphRecord>.Shared.Return(_items);
        _items = larger;
    }
}
