// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.IO;

/// <summary>
/// The tables of a <see cref="StreamPdfByteSource"/>'s page cache: which page each slot holds, how much of it, when it was
/// last used, and the slot of each cached page. A registered host can keep cleared tables from closed sources and reuse
/// them for sources with the same number of slots.
/// </summary>
internal sealed class StreamPageCache
{
    /// <summary>Initializes a new instance of the <see cref="StreamPageCache"/> class.</summary>
    /// <param name="slots">The number of pages the cache holds.</param>
    private StreamPageCache(int slots)
    {
        Pages = new byte[slots][];
        PageNumbers = new long[slots];
        PageLengths = new int[slots];
        LastUse = new long[slots];
        Slots = [with(slots)];
        PageNumbers.AsSpan().Fill(-1);
    }

    /// <summary>Gets the cached pages by slot; each is rented from the shared pool.</summary>
    internal byte[]?[] Pages { get; }

    /// <summary>Gets the page number in each slot, or -1 when empty.</summary>
    internal long[] PageNumbers { get; }

    /// <summary>Gets the bytes held in each slot; the last page of the file is short.</summary>
    internal int[] PageLengths { get; }

    /// <summary>Gets the clock value of each slot's last use.</summary>
    internal long[] LastUse { get; }

    /// <summary>Gets the slot holding each cached page.</summary>
    internal Dictionary<long, int> Slots { get; }

    /// <summary>Takes the tables of a closed source with the same number of slots, or makes new ones.</summary>
    /// <param name="slots">The number of pages the cache holds.</param>
    /// <param name="pool">The registered host's cache pool, when available.</param>
    /// <returns>Empty tables the caller owns until it gives them to <see cref="Return"/>.</returns>
    internal static StreamPageCache Rent(int slots, ScratchSlots<StreamPageCache>? pool) =>
        pool?.Take(slots, static (cache, count) => cache.Pages.Length == count) ?? new(slots);

    /// <summary>Empties the tables and offers them to the host's pool, when supplied.</summary>
    /// <param name="cache">The tables of a closed source, whose pages have been returned to the pool.</param>
    /// <param name="pool">The registered host's cache pool, when available.</param>
    internal static void Return(StreamPageCache cache, ScratchSlots<StreamPageCache>? pool)
    {
        cache.Slots.Clear();
        cache.PageNumbers.AsSpan().Fill(-1);
        cache.PageLengths.AsSpan().Clear();
        cache.LastUse.AsSpan().Clear();
        _ = pool?.Offer(cache);
    }
}
