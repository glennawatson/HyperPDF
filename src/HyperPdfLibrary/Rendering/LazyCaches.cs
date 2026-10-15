// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Rendering;

/// <summary>Makes a cache the first time a document needs it, so documents that never use it do not pay for it.</summary>
internal static class LazyCaches
{
    /// <summary>Gets the cache in a slot, making and publishing it on first use; threads that race share the first one published.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="slot">The field that holds the cache.</param>
    /// <returns>The cache.</returns>
    internal static ObjectCache<TKey, TValue> Get<TKey, TValue>(ref ObjectCache<TKey, TValue>? slot)
        where TKey : notnull
        where TValue : class
    {
        if (Volatile.Read(ref slot) is { } existing)
        {
            return existing;
        }

        var created = new ObjectCache<TKey, TValue>();
        return Interlocked.CompareExchange(ref slot, created, null) ?? created;
    }

    /// <summary>Empties the cache in a slot when it has been made; an unused slot stays empty.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="slot">The field that holds the cache.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear<TKey, TValue>(ref ObjectCache<TKey, TValue>? slot)
        where TKey : notnull
        where TValue : class => Volatile.Read(ref slot)?.Clear();
}
