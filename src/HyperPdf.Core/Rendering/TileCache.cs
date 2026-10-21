// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// A cache of rendered tiles bounded by memory. It evicts the least recently used first, using a second-chance (clock) rule so
/// a lookup is a flag write. The cache owns its surfaces and disposes them on eviction. Whole-page previews are small and
/// stand in for pages while their sharp tiles are made, so they are kept in a list of their own:
/// when the cache is over budget, tiles go first and previews only when no tile is left, and previews never take more
/// than half the budget. It is not thread safe and is used only from the UI thread.
/// </summary>
[DebuggerDisplay("TileCache: {Count} tiles, {CurrentBytes} bytes")]
public sealed class TileCache : IDisposable
{
    /// <summary>The share of the budget previews may take, as a divisor: half.</summary>
    private const int PreviewShare = 2;

    /// <summary>Entries by key.</summary>
    private readonly Dictionary<TileKey, LinkedListNode<CacheEntry>> _entries = [];

    /// <summary>Tiles ordered from most to least recently used.</summary>
    private readonly LinkedList<CacheEntry> _recency = new();

    /// <summary>Previews ordered from most to least recently used.</summary>
    private readonly LinkedList<CacheEntry> _previews = new();

    /// <summary>Initializes a new instance of the <see cref="TileCache"/> class.</summary>
    /// <param name="budgetBytes">The memory budget in bytes.</param>
    public TileCache(long budgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        BudgetBytes = budgetBytes;
    }

    /// <summary>Gets or sets the memory budget in bytes.</summary>
    public long BudgetBytes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
            Trim(null);
        }
    }

    /// <summary>Gets the memory currently held, in bytes.</summary>
    public long CurrentBytes { get; private set; }

    /// <summary>Gets the memory previews currently hold, in bytes.</summary>
    public long PreviewBytes { get; private set; }

    /// <summary>Gets the number of cached tiles and previews.</summary>
    public int Count => _entries.Count;

    /// <summary>Looks up a tile and marks it as recently used.</summary>
    /// <param name="key">The key.</param>
    /// <param name="surface">The surface when found.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGet(in TileKey key, out IRenderSurface surface)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            // A hit only sets a bit; eviction gives marked entries a second chance, so no list surgery happens per lookup.
            node.ValueRef.Referenced = true;
            surface = node.Value.Surface;
            return true;
        }

        surface = null!;
        return false;
    }

    /// <summary>Determines whether a tile is cached, without changing its recency.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when cached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in TileKey key) => _entries.ContainsKey(key);

    /// <summary>Determines whether every tile in a window is cached, without changing recency.</summary>
    /// <param name="page">Any tile key of the page and scale; its column and row are ignored.</param>
    /// <param name="window">The tiles to check.</param>
    /// <returns><see langword="true"/> when the cached tiles cover the window.</returns>
    public bool Covers(in TileKey page, in TileWindow window)
    {
        for (var row = window.FirstRow; row <= window.LastRow; row++)
        {
            for (var column = window.FirstColumn; column <= window.LastColumn; column++)
            {
                if (!_entries.ContainsKey(page with { Column = (short)column, Row = (short)row }))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Adds a tile, replacing and disposing any existing tile with the same key, then trims to budget.</summary>
    /// <param name="key">The key.</param>
    /// <param name="surface">The surface; the cache takes ownership.</param>
    public void Add(in TileKey key, IRenderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (_entries.Remove(key, out var existing))
        {
            Release(existing);
        }

        var node = (key.IsPreview ? _previews : _recency).AddFirst(new CacheEntry(key, surface, false));
        _entries.Add(key, node);
        CurrentBytes += surface.ByteSize;
        if (key.IsPreview)
        {
            PreviewBytes += surface.ByteSize;
        }

        Trim(node);
    }

    /// <summary>Removes every tile belonging to a document.</summary>
    /// <param name="documentId">The document identifier.</param>
    public void RemoveDocument(int documentId)
    {
        Sweep(_recency, documentId, documentId, -1);
        Sweep(_previews, documentId, documentId, -1);
    }

    /// <summary>Removes every tile of one page, for example after it was annotated.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="pageIndex">The page index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemovePage(int documentId, int pageIndex) => RemovePage(documentId, documentId, pageIndex);

    /// <summary>Removes every tile of one page under either of two document identifiers, in a single walk of each list.</summary>
    /// <param name="documentId">The first document identifier.</param>
    /// <param name="otherDocumentId">The second document identifier.</param>
    /// <param name="pageIndex">The page index.</param>
    public void RemovePage(int documentId, int otherDocumentId, int pageIndex)
    {
        Sweep(_recency, documentId, otherDocumentId, pageIndex);
        Sweep(_previews, documentId, otherDocumentId, pageIndex);
    }

    /// <summary>Removes every tile.</summary>
    public void Clear()
    {
        while (_recency.Last is { } last)
        {
            _ = _entries.Remove(last.Value.Key);
            Release(last);
        }

        while (_previews.Last is { } preview)
        {
            _ = _entries.Remove(preview.Value.Key);
            Release(preview);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Clear();

    /// <summary>Finds the entry to evict from a list. Entries looked up since they were last passed get a second chance.</summary>
    /// <param name="list">The list.</param>
    /// <param name="newest">The tile being added, which counts as just used so it is passed over once.</param>
    /// <returns>The node to evict, or <see langword="null"/> when the list is empty.</returns>
    private static LinkedListNode<CacheEntry>? Victim(LinkedList<CacheEntry> list, LinkedListNode<CacheEntry>? newest)
    {
        // A marked entry has its mark cleared and moves to the front, so the loop ends within one lap of the list.
        while (list.Last is { } last)
        {
            if (!last.Value.Referenced && last != newest)
            {
                return last;
            }

            // The newest tile is passed over once only, so a tile bigger than the budget still evicts itself.
            newest = last == newest ? null : newest;
            last.ValueRef.Referenced = false;
            list.Remove(last);
            list.AddFirst(last);
        }

        return null;
    }

    /// <summary>Removes the entries of a list that belong to one of two documents, or to one page of them.</summary>
    /// <param name="list">The list.</param>
    /// <param name="documentId">The first document identifier.</param>
    /// <param name="otherDocumentId">The second document identifier.</param>
    /// <param name="pageIndex">The page index, or -1 for every page.</param>
    private void Sweep(LinkedList<CacheEntry> list, int documentId, int otherDocumentId, int pageIndex)
    {
        var node = list.First;
        while (node is not null)
        {
            var next = node.Next;
            var key = node.Value.Key;
            if ((key.DocumentId == documentId || key.DocumentId == otherDocumentId) && (pageIndex < 0 || key.PageIndex == pageIndex))
            {
                _ = _entries.Remove(key);
                Release(node);
            }

            node = next;
        }
    }

    /// <summary>Evicts until within budget: previews beyond their share first, then least recently used tiles, then previews.</summary>
    /// <param name="newest">The node just added, or <see langword="null"/>.</param>
    private void Trim(LinkedListNode<CacheEntry>? newest)
    {
        while (PreviewBytes > BudgetBytes / PreviewShare && Victim(_previews, newest) is { } excess)
        {
            _ = _entries.Remove(excess.Value.Key);
            Release(excess);
        }

        while (CurrentBytes > BudgetBytes)
        {
            var victim = Victim(_recency, newest) ?? Victim(_previews, newest);
            if (victim is null)
            {
                return;
            }

            _ = _entries.Remove(victim.Value.Key);
            Release(victim);
        }
    }

    /// <summary>Unlinks a node and disposes its surface.</summary>
    /// <param name="node">The node.</param>
    private void Release(LinkedListNode<CacheEntry> node)
    {
        node.List!.Remove(node);
        CurrentBytes -= node.Value.Surface.ByteSize;
        if (node.Value.Key.IsPreview)
        {
            PreviewBytes -= node.Value.Surface.ByteSize;
        }

        node.Value.Surface.Dispose();
    }

    /// <summary>A cached tile.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Surface">The surface.</param>
    /// <param name="Referenced">Whether the tile was looked up since eviction last passed it.</param>
    private record struct CacheEntry(TileKey Key, IRenderSurface Surface, bool Referenced);
}
