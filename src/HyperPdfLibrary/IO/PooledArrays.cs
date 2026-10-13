// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.IO;

/// <summary>Grows and returns the arrays an operation rents from the shared pool to collect entries until it ends.</summary>
internal static class PooledArrays
{
    /// <summary>The number of entries the first pooled array holds at least.</summary>
    private const int InitialCapacity = 8;

    /// <summary>The factor by which a full array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Makes sure a pooled array has room for one more entry.</summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <param name="array">The array; rented on first use and replaced by a larger one when full.</param>
    /// <param name="count">The entries in use.</param>
    /// <param name="clear">True when the entry type holds references that must be cleared when an array is returned.</param>
    /// <returns>The array, with a free slot at index <paramref name="count"/>.</returns>
    internal static T[] EnsureRoom<T>(ref T[]? array, int count, bool clear)
    {
        if (array is null)
        {
            array = ArrayPool<T>.Shared.Rent(InitialCapacity);
            return array;
        }

        if (count < array.Length)
        {
            return array;
        }

        var larger = ArrayPool<T>.Shared.Rent(array.Length * GrowthFactor);
        array.AsSpan(0, count).CopyTo(larger);
        ArrayPool<T>.Shared.Return(array, clear);
        array = larger;
        return larger;
    }

    /// <summary>Gives a pooled array back and forgets it.</summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <param name="array">The array; set to null.</param>
    /// <param name="count">The entries in use.</param>
    /// <param name="clear">True when the entries hold references that must be cleared.</param>
    internal static void Return<T>(ref T[]? array, int count, bool clear)
    {
        if (array is null)
        {
            return;
        }

        if (clear)
        {
            array.AsSpan(0, count).Clear();
        }

        ArrayPool<T>.Shared.Return(array);
        array = null;
    }
}
