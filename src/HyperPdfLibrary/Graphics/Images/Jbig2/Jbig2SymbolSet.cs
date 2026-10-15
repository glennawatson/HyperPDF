// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The symbols a symbol dictionary or text region can use: the stores of the dictionaries it refers to, in order, and
/// optionally the store of symbols being decoded. It does not own the stores. A set taken from a workspace goes back
/// to it when disposed.
/// </summary>
[DebuggerDisplay("Jbig2SymbolSet: {Count} symbols")]
internal sealed class Jbig2SymbolSet : IDisposable
{
    /// <summary>The stores a new set has room for.</summary>
    private const int InitialStores = 4;

    /// <summary>The factor a full set grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The stores, in symbol order; the first <see cref="_storeCount"/> are in use.</summary>
    private Jbig2SymbolStore[] _stores = new Jbig2SymbolStore[InitialStores];

    /// <summary>The number of stores.</summary>
    private int _storeCount;

    /// <summary>The workspace that takes this set back when it is disposed, or <see langword="null"/>.</summary>
    private Jbig2Workspace? _owner;

    /// <summary>Gets the number of symbols across every store.</summary>
    internal int Count
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _storeCount; i++)
            {
                count += _stores[i].Count;
            }

            return count;
        }
    }

    /// <summary>Forgets the stores and gives the set back to its workspace.</summary>
    public void Dispose()
    {
        Array.Clear(_stores, 0, _storeCount);
        _storeCount = 0;
        var owner = _owner;
        _owner = null;
        owner?.Sets.Give(this);
    }

    /// <summary>Creates an empty set from a workspace's idle objects.</summary>
    /// <param name="workspace">The workspace that keeps the set when it is disposed.</param>
    /// <returns>The set.</returns>
    internal static Jbig2SymbolSet Rent(Jbig2Workspace workspace)
    {
        var set = workspace.Sets.Take() ?? new();
        set._owner = workspace;
        return set;
    }

    /// <summary>Appends a store's symbols.</summary>
    /// <param name="store">The store.</param>
    internal void Add(Jbig2SymbolStore store)
    {
        if (_storeCount == _stores.Length)
        {
            Array.Resize(ref _stores, _stores.Length * GrowthFactor);
        }

        _stores[_storeCount] = store;
        _storeCount++;
    }

    /// <summary>Creates a set holding this set's stores followed by another store.</summary>
    /// <param name="store">The store to append.</param>
    /// <returns>The new set, which the caller disposes.</returns>
    internal Jbig2SymbolSet With(Jbig2SymbolStore store)
    {
        var set = _owner is null ? new() : Rent(_owner);
        for (var i = 0; i < _storeCount; i++)
        {
            set.Add(_stores[i]);
        }

        set.Add(store);
        return set;
    }

    /// <summary>Finds the store and index of a symbol.</summary>
    /// <param name="index">The symbol across every store.</param>
    /// <param name="store">The store holding it.</param>
    /// <param name="local">The symbol's index in that store.</param>
    /// <returns><see langword="false"/> when the index is past the last symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryFind(long index, [NotNullWhen(true)] out Jbig2SymbolStore? store, out int local)
    {
        store = null;
        local = 0;
        if (index < 0)
        {
            return false;
        }

        for (var i = 0; i < _storeCount; i++)
        {
            var candidate = _stores[i];
            if (index < candidate.Count)
            {
                store = candidate;
                local = (int)index;
                return true;
            }

            index -= candidate.Count;
        }

        return false;
    }
}
