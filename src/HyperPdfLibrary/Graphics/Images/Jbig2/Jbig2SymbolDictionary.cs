// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>A decoded symbol dictionary: its exported symbols and, when asked for, its final coding contexts.</summary>
[DebuggerDisplay("Jbig2SymbolDictionary: {Symbols.Count} symbols")]
internal sealed class Jbig2SymbolDictionary : IDisposable
{
    /// <summary>The cache the retained contexts return to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The retained generic contexts, or <see langword="null"/>.</summary>
    private byte[]? _genericContexts;

    /// <summary>The bytes of <see cref="_genericContexts"/> in use.</summary>
    private int _genericLength;

    /// <summary>The retained refinement contexts, or <see langword="null"/>.</summary>
    private byte[]? _refinementContexts;

    /// <summary>The bytes of <see cref="_refinementContexts"/> in use.</summary>
    private int _refinementLength;

    /// <summary>The exported symbols, or <see langword="null"/> after disposal.</summary>
    private Jbig2SymbolStore? _symbols;

    /// <summary>The workspace that takes this dictionary back when it is disposed, or <see langword="null"/>.</summary>
    private Jbig2Workspace? _owner;

    /// <summary>Initializes a new instance of the <see cref="Jbig2SymbolDictionary"/> class.</summary>
    /// <param name="symbols">The exported symbols, now owned by the dictionary.</param>
    /// <param name="genericContexts">The generic contexts to keep, or empty.</param>
    /// <param name="refinementContexts">The refinement contexts to keep, or empty.</param>
    /// <param name="scratchPools">The cache the kept contexts are rented from.</param>
    internal Jbig2SymbolDictionary(Jbig2SymbolStore symbols, ReadOnlySpan<byte> genericContexts, ReadOnlySpan<byte> refinementContexts, ScratchPools scratchPools)
    {
        _scratchPools = scratchPools;
        Open(symbols, genericContexts, refinementContexts);
    }

    /// <summary>Gets the exported symbols.</summary>
    internal Jbig2SymbolStore Symbols => _symbols ?? throw new ObjectDisposedException(nameof(Jbig2SymbolDictionary));

    /// <summary>Gets the retained generic contexts; empty when none were kept.</summary>
    internal ReadOnlySpan<byte> GenericContexts => _genericContexts is null ? default : _genericContexts.AsSpan(0, _genericLength);

    /// <summary>Gets the retained refinement contexts; empty when none were kept.</summary>
    internal ReadOnlySpan<byte> RefinementContexts => _refinementContexts is null ? default : _refinementContexts.AsSpan(0, _refinementLength);

    /// <summary>Returns the symbols' and contexts' buffers to the pool and the object to its workspace.</summary>
    public void Dispose()
    {
        if (_symbols is null)
        {
            return;
        }

        _symbols.Dispose();
        _symbols = null;
        Return(ref _genericContexts, ref _genericLength);
        Return(ref _refinementContexts, ref _refinementLength);
        var owner = _owner;
        _owner = null;
        owner?.Dictionaries.Give(this);
    }

    /// <summary>Creates a dictionary from a workspace's idle objects.</summary>
    /// <param name="workspace">The workspace that keeps the object when the dictionary is disposed.</param>
    /// <param name="symbols">The exported symbols, now owned by the dictionary.</param>
    /// <param name="genericContexts">The generic contexts to keep, or empty.</param>
    /// <param name="refinementContexts">The refinement contexts to keep, or empty.</param>
    /// <returns>The dictionary.</returns>
    internal static Jbig2SymbolDictionary Rent(Jbig2Workspace workspace, Jbig2SymbolStore symbols, ReadOnlySpan<byte> genericContexts, ReadOnlySpan<byte> refinementContexts)
    {
        var dictionary = workspace.Dictionaries.Take();
        if (dictionary is null)
        {
            dictionary = new(symbols, genericContexts, refinementContexts, workspace.ScratchPools);
        }
        else
        {
            dictionary.Open(symbols, genericContexts, refinementContexts);
        }

        dictionary._owner = workspace;
        return dictionary;
    }

    /// <summary>Copies contexts into a rented array.</summary>
    /// <param name="contexts">The contexts, or empty.</param>
    /// <param name="scratchPools">The cache to rent from.</param>
    /// <param name="length">Receives the bytes kept.</param>
    /// <returns>The array, or <see langword="null"/> when there is nothing to keep.</returns>
    private static byte[]? Keep(ReadOnlySpan<byte> contexts, ScratchPools scratchPools, out int length)
    {
        length = contexts.Length;
        if (contexts.IsEmpty)
        {
            return null;
        }

        var array = scratchPools.Bytes.Rent(contexts.Length);
        contexts.CopyTo(array);
        return array;
    }

    /// <summary>Takes the symbols and copies the contexts to keep.</summary>
    /// <param name="symbols">The exported symbols.</param>
    /// <param name="genericContexts">The generic contexts to keep, or empty.</param>
    /// <param name="refinementContexts">The refinement contexts to keep, or empty.</param>
    private void Open(Jbig2SymbolStore symbols, ReadOnlySpan<byte> genericContexts, ReadOnlySpan<byte> refinementContexts)
    {
        _symbols = symbols;
        _genericContexts = Keep(genericContexts, _scratchPools, out _genericLength);
        _refinementContexts = Keep(refinementContexts, _scratchPools, out _refinementLength);
    }

    /// <summary>Gives a kept array back to the cache.</summary>
    /// <param name="array">The array, cleared afterwards.</param>
    /// <param name="length">The bytes kept, cleared afterwards.</param>
    private void Return(ref byte[]? array, ref int length)
    {
        if (array is not null)
        {
            _scratchPools.Bytes.Return(array);
        }

        array = null;
        length = 0;
    }
}
