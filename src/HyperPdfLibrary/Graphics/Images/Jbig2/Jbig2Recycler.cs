// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The idle working objects of one kind that a decode workspace keeps, so a later decode with the same workspace takes
/// them instead of building them. A workspace belongs to one decode on one thread, so there is no locking.
/// </summary>
/// <typeparam name="T">The kind of object kept.</typeparam>
[DebuggerDisplay("Jbig2Recycler: {_count} idle {typeof(T).Name}")]
internal sealed class Jbig2Recycler<T>
    where T : class
{
    /// <summary>The idle objects kept at first.</summary>
    private const int InitialSlots = 8;

    /// <summary>The factor a full list grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The idle objects; the first <see cref="_count"/> are in use.</summary>
    private T?[] _items = new T?[InitialSlots];

    /// <summary>The number of idle objects.</summary>
    private int _count;

    /// <summary>Takes an idle object.</summary>
    /// <returns>The object, or <see langword="null"/> when none is idle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? Take()
    {
        if (_count == 0)
        {
            return null;
        }

        _count--;
        var item = _items[_count];
        _items[_count] = null;
        return item;
    }

    /// <summary>Keeps an object nobody uses any more.</summary>
    /// <param name="item">The object.</param>
    internal void Give(T item)
    {
        if (_count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * GrowthFactor);
        }

        _items[_count] = item;
        _count++;
    }
}
