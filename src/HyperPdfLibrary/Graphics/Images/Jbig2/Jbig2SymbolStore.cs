// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The bitmaps of a symbol or pattern dictionary packed in one pooled buffer, so a dictionary of thousands of symbols
/// needs two arrays instead of one per symbol. A view from <see cref="Get"/> is only valid until the next add.
/// </summary>
[DebuggerDisplay("Jbig2SymbolStore: {Count} symbols, {_used} bytes")]
internal sealed class Jbig2SymbolStore : IDisposable
{
    /// <summary>The bytes rented at first.</summary>
    private const int InitialBytes = 4096;

    /// <summary>The symbols rented at first.</summary>
    private const int InitialSymbols = 64;

    /// <summary>The factor a full buffer grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The cache this store returns its arrays to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The rows of every symbol.</summary>
    private byte[]? _data;

    /// <summary>Where each symbol lives.</summary>
    private Jbig2Symbol[]? _symbols;

    /// <summary>The bytes of <see cref="_data"/> in use.</summary>
    private int _used;

    /// <summary>Initializes a new instance of the <see cref="Jbig2SymbolStore"/> class.</summary>
    internal Jbig2SymbolStore()
        : this(new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2SymbolStore"/> class with an explicit cache.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal Jbig2SymbolStore(ScratchPools scratchPools)
    {
        _scratchPools = scratchPools;
        _data = scratchPools.Bytes.Rent(InitialBytes);
        _symbols = scratchPools.Get<Jbig2Symbol>().Rent(InitialSymbols);
    }

    /// <summary>Gets the number of symbols.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the scratch cache that owns this store's arrays.</summary>
    internal ScratchPools ScratchPools => _scratchPools;

    /// <summary>Gets the rows buffer, which must not be used after disposal.</summary>
    private byte[] Data => _data ?? throw new ObjectDisposedException(nameof(Jbig2SymbolStore));

    /// <summary>Gets the symbol table, which must not be used after disposal.</summary>
    private Jbig2Symbol[] Symbols => _symbols ?? throw new ObjectDisposedException(nameof(Jbig2SymbolStore));

    /// <summary>Returns the buffers to the pool.</summary>
    public void Dispose()
    {
        // A store belongs to one decode on one thread, so plain field access is enough.
        if (_data is not null)
        {
            _scratchPools.Bytes.Return(_data);
            _data = null;
        }

        if (_symbols is null)
        {
            return;
        }

        _scratchPools.Get<Jbig2Symbol>().Return(_symbols);
        _symbols = null;
    }

    /// <summary>Determines whether a symbol has a bitmap.</summary>
    /// <param name="index">The symbol.</param>
    /// <returns><see langword="true"/> when the symbol exists and is present.</returns>
    internal bool IsPresent(int index) => (uint)index < (uint)Count && Symbols[index].IsPresent;

    /// <summary>Gets a symbol's bitmap.</summary>
    /// <param name="index">The symbol, which must exist.</param>
    /// <returns>The bitmap; empty for an absent symbol.</returns>
    internal Jbig2BitmapView Get(int index)
    {
        var symbol = Symbols[index];
        return symbol.IsPresent ? new(Data.AsSpan(symbol.Offset, symbol.Length), symbol.Width, symbol.Height) : default;
    }

    /// <summary>Adds a symbol with no bitmap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddAbsent() => Append(Jbig2Symbol.Absent);

    /// <summary>Adds a copy of a bitmap.</summary>
    /// <param name="bitmap">The bitmap; it may be empty.</param>
    /// <returns><see langword="false"/> when the store would pass <see cref="Jbig2Limits.MaxStoreBytes"/>.</returns>
    internal bool TryAdd(Jbig2BitmapView bitmap)
    {
        var width = Math.Max(bitmap.Width, 0);
        var height = Math.Max(bitmap.Height, 0);
        var length = Jbig2Bits.Stride(width) * height;
        if (!TryReserve(length))
        {
            return false;
        }

        bitmap.Data[..length].CopyTo(Data.AsSpan(_used, length));
        Append(new(_used, width, height));
        _used += length;
        return true;
    }

    /// <summary>Adds a copy of a rectangle of a bitmap; parts outside the bitmap are white.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="x">The rectangle's left column.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <returns><see langword="false"/> when the store would pass <see cref="Jbig2Limits.MaxStoreBytes"/>.</returns>
    internal bool TryAddColumns(Jbig2BitmapView bitmap, int x, int width)
    {
        var height = bitmap.Height;
        var stride = Jbig2Bits.Stride(width);
        var length = stride * height;
        if (!TryReserve(length))
        {
            return false;
        }

        var target = Data.AsSpan(_used, length);
        target.Clear();
        for (var y = 0; y < height && width > 0; y++)
        {
            CopyColumns(bitmap.Row(y), bitmap.Width, x, target.Slice(y * stride, stride), width);
        }

        Append(new(_used, width, height));
        _used += length;
        return true;
    }

    /// <summary>Adds a copy of another store's symbol.</summary>
    /// <param name="source">The store holding the symbol.</param>
    /// <param name="index">The symbol.</param>
    /// <returns><see langword="false"/> when the store would pass <see cref="Jbig2Limits.MaxStoreBytes"/>.</returns>
    internal bool TryAddCopy(Jbig2SymbolStore source, int index)
    {
        if (source.IsPresent(index))
        {
            return TryAdd(source.Get(index));
        }

        AddAbsent();
        return true;
    }

    /// <summary>Copies a run of pixels from a row into the start of another row.</summary>
    /// <param name="source">The source row.</param>
    /// <param name="sourceWidth">The source row's width.</param>
    /// <param name="x">The first source pixel.</param>
    /// <param name="target">The target row, cleared.</param>
    /// <param name="width">The pixels to copy.</param>
    private static void CopyColumns(ReadOnlySpan<byte> source, int sourceWidth, int x, Span<byte> target, int width)
    {
        if ((x & Jbig2Bits.BitMask) == 0 && x + width <= sourceWidth)
        {
            source.Slice(x >> Jbig2Bits.ByteShift, target.Length).CopyTo(target);
            ClearPadding(target, width);
            return;
        }

        for (var i = 0; i < width; i++)
        {
            if (Jbig2Bits.Get(source, x + i, sourceWidth) != 0)
            {
                Jbig2Bits.SetBlack(target, i);
            }
        }
    }

    /// <summary>Clears the bits after the last pixel of a row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="width">The width in pixels.</param>
    private static void ClearPadding(Span<byte> row, int width)
    {
        var padding = (row.Length << Jbig2Bits.ByteShift) - width;
        if (padding > 0)
        {
            row[^1] &= (byte)(byte.MaxValue << padding);
        }
    }

    /// <summary>Makes room for more bytes, growing the pooled buffer.</summary>
    /// <param name="length">The bytes needed.</param>
    /// <returns><see langword="false"/> when the store would pass <see cref="Jbig2Limits.MaxStoreBytes"/>.</returns>
    private bool TryReserve(int length)
    {
        var needed = (long)_used + length;
        if (needed > Jbig2Limits.MaxStoreBytes)
        {
            return false;
        }

        var data = Data;
        if (needed <= data.Length)
        {
            return true;
        }

        var grown = _scratchPools.Bytes.Rent((int)Math.Min(Math.Max(needed, (long)data.Length * GrowthFactor), Jbig2Limits.MaxStoreBytes));
        data.AsSpan(0, _used).CopyTo(grown);
        _scratchPools.Bytes.Return(data);
        _data = grown;
        return true;
    }

    /// <summary>Appends a symbol entry, growing the pooled table.</summary>
    /// <param name="symbol">The entry.</param>
    private void Append(Jbig2Symbol symbol)
    {
        var symbols = Symbols;
        if (Count == symbols.Length)
        {
            var grown = _scratchPools.Get<Jbig2Symbol>().Rent(symbols.Length * GrowthFactor);
            symbols.AsSpan(0, Count).CopyTo(grown);
            _scratchPools.Get<Jbig2Symbol>().Return(symbols);
            _symbols = grown;
            symbols = grown;
        }

        symbols[Count] = symbol;
        Count++;
    }
}
