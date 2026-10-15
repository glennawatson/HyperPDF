// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// Rents and returns the array that caches a store's parsed objects, so opening and closing documents reuses the arrays.
/// A rented array can be longer than the objects it caches; the slots past the last object are never used.
/// </summary>
internal static class StoreCache
{
    /// <summary>Rents an array whose first slots are empty.</summary>
    /// <param name="size">The number of slots needed.</param>
    /// <returns>The array, with at least <paramref name="size"/> empty slots.</returns>
    internal static object?[] Rent(int size)
    {
        if (size == 0)
        {
            return [];
        }

        var rented = ArrayPool<object?>.Shared.Rent(size);

        // Other users of the shared pool may have returned the array without clearing it.
        rented.AsSpan().Clear();
        return rented;
    }

    /// <summary>Gives an array back to the pool, clearing the objects it holds.</summary>
    /// <param name="cache">The array from <see cref="Rent"/> or <see cref="Grow"/>.</param>
    internal static void Return(object?[] cache)
    {
        if (cache.Length > 0)
        {
            ArrayPool<object?>.Shared.Return(cache, true);
        }
    }

    /// <summary>Empties the cache and makes sure it has room for a number of objects.</summary>
    /// <param name="cache">The array, replaced when it is too small.</param>
    /// <param name="size">The number of slots needed.</param>
    internal static void Reset(ref object?[] cache, int size)
    {
        if (cache.Length >= size && cache.Length > 0)
        {
            Array.Clear(cache);
            return;
        }

        var replacement = Rent(size);
        var old = cache;
        Volatile.Write(ref cache, replacement);
        Return(old);
    }

    /// <summary>Makes sure the cache has room for a number of objects, keeping the objects it holds.</summary>
    /// <param name="cache">The array, replaced when it is too small.</param>
    /// <param name="size">The number of slots needed.</param>
    internal static void Grow(ref object?[] cache, int size)
    {
        if (cache.Length >= size)
        {
            return;
        }

        var replacement = Rent(size);
        cache.CopyTo(replacement, 0);

        // Readers can still hold the old cache after publication. Let the GC reclaim it.
        Volatile.Write(ref cache, replacement);
    }
}
