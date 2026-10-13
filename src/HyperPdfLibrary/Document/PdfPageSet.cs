// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// The pages of a document as read at one moment, with their indexes by object number. The set keeps one small entry per
/// page and makes each <see cref="PdfPage"/> when it is first asked for. Never changed once built, except for the
/// caches it fills as pages, links and indexes are first used.
/// </summary>
[DebuggerDisplay("PdfPageSet: {Count} pages")]
internal sealed class PdfPageSet
{
    /// <summary>Set once the page tree has been checked to be well formed, so edits can change it in place.</summary>
    internal const int TreeVerified = 1;

    /// <summary>Set when the page tree cannot be changed in place and must be written again.</summary>
    internal const int TreeUnusable = 2;

    /// <summary>Set when some page tree node carries attributes its kids inherit.</summary>
    internal const int NodesCarryAttributes = 4;

    /// <summary>The store the pages are read from.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The pages made so far, by index; made with the first page.</summary>
    private PdfPage?[]? _made;

    /// <summary>Every page, made with the first request for all of them.</summary>
    private PdfPage[]? _all;

    /// <summary>The link annotations read so far, by page index; made with the first link read.</summary>
    private PdfLink[]?[]? _links;

    /// <summary>The page indexes by page object number; built with the first lookup.</summary>
    private Dictionary<int, int>? _indexes;

    /// <summary>Initializes a new instance of the <see cref="PdfPageSet"/> class.</summary>
    /// <param name="store">The store the pages are read from.</param>
    /// <param name="entries">The pages, in order.</param>
    /// <param name="inherits">The attributes pages inherit; the first is the empty set.</param>
    internal PdfPageSet(PdfObjectStore store, PdfPageEntry[] entries, InheritedAttributes[] inherits)
    {
        _store = store;
        Entries = entries;
        Inherits = inherits;
    }

    /// <summary>Gets the number of pages.</summary>
    internal int Count => Entries.Length;

    /// <summary>Gets the entries of the pages, in order.</summary>
    internal PdfPageEntry[] Entries { get; }

    /// <summary>Gets the attributes pages inherit, by <see cref="PdfPageEntry.InheritIndex"/>.</summary>
    internal InheritedAttributes[] Inherits { get; }

    /// <summary>Gets every page, in order, making those not made yet.</summary>
    internal PdfPage[] Pages => Volatile.Read(ref _all) ?? CreateAll();

    /// <summary>Gets the page indexes by page object number; a page listed twice keeps its first index. Built on first use.</summary>
    internal Dictionary<int, int> Indexes => Volatile.Read(ref _indexes) ?? CreateIndexes();

    /// <summary>Gets or sets what is known about the page tree: a combination of <see cref="TreeVerified"/>, <see cref="TreeUnusable"/> and <see cref="NodesCarryAttributes"/>.</summary>
    internal int TreeState
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    /// <summary>Reads the page tree.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfPageSet Read(PdfObjectStore objects) => PageTreeReader.Read(objects);

    /// <summary>Gets a page, making it on first use.</summary>
    /// <param name="index">The zero-based page; it must be a page of the set.</param>
    /// <returns>The page.</returns>
    internal PdfPage GetPage(int index)
    {
        ref var slot = ref (Volatile.Read(ref _made) ?? CreateMade())[index];
        if (Volatile.Read(ref slot) is { } page)
        {
            return page;
        }

        var created = CreatePage(index);
        return Interlocked.CompareExchange(ref slot, created, null) ?? created;
    }

    /// <summary>Gets the slot for the link annotations read so far from a page. The slots live and die with this set, so they always match its pages; they are made on first use.</summary>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The slot, null until the page's links were read.</returns>
    internal ref PdfLink[]? LinkSlot(int pageIndex) => ref (Volatile.Read(ref _links) ?? CreateLinks())[pageIndex];

    /// <summary>Makes the set that results from putting pages before a page.</summary>
    /// <param name="index">The index the first inserted page takes.</param>
    /// <param name="inserted">The entries of the inserted pages.</param>
    /// <returns>The new set; its pages are made again when asked for, and it keeps what is known of the page tree.</returns>
    internal PdfPageSet WithInserted(int index, ReadOnlySpan<PdfPageEntry> inserted)
    {
        var entries = new PdfPageEntry[Entries.Length + inserted.Length];
        Entries.AsSpan(0, index).CopyTo(entries);
        inserted.CopyTo(entries.AsSpan(index));
        Entries.AsSpan(index).CopyTo(entries.AsSpan(index + inserted.Length));
        return new(_store, entries, Inherits) { TreeState = TreeState };
    }

    /// <summary>Makes the set that results from moving pages, so the first lands at a destination.</summary>
    /// <param name="pages">The indexes of the moved pages, each once, in the order they are placed.</param>
    /// <param name="destination">The index of the first moved page afterwards.</param>
    /// <returns>The new set; its pages are made again when asked for, and it keeps what is known of the page tree.</returns>
    internal PdfPageSet WithMoved(ReadOnlySpan<int> pages, int destination)
    {
        var sorted = ArrayPool<int>.Shared.Rent(pages.Length);
        try
        {
            pages.CopyTo(sorted);
            Array.Sort(sorted, 0, pages.Length);
            var entries = new PdfPageEntry[Entries.Length];
            var kept = 0;
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Array.BinarySearch(sorted, 0, pages.Length, i) >= 0)
                {
                    continue;
                }

                // Kept pages leave a gap where the moved ones go.
                entries[kept < destination ? kept : kept + pages.Length] = Entries[i];
                kept++;
            }

            for (var i = 0; i < pages.Length; i++)
            {
                entries[destination + i] = Entries[pages[i]];
            }

            return new(_store, entries, Inherits) { TreeState = TreeState };
        }
        finally
        {
            ArrayPool<int>.Shared.Return(sorted);
        }
    }

    /// <summary>Makes a page from its entry.</summary>
    /// <param name="index">The page's index.</param>
    /// <returns>The page.</returns>
    private PdfPage CreatePage(int index)
    {
        var entry = Entries[index];
        var dictionary = entry.Id.IsValid ? StoreReading.Resolve(_store, PdfValue.FromReference(entry.Id)).AsDictionary() : entry.Direct;
        return new(index, entry.Id, dictionary!, Inherits[entry.InheritIndex]);
    }

    /// <summary>Makes the table of made pages once.</summary>
    /// <returns>The table.</returns>
    private PdfPage?[] CreateMade()
    {
        var created = new PdfPage?[Entries.Length];
        return Interlocked.CompareExchange(ref _made, created, null) ?? created;
    }

    /// <summary>Makes every page once.</summary>
    /// <returns>The pages.</returns>
    private PdfPage[] CreateAll()
    {
        var created = new PdfPage[Entries.Length];
        for (var i = 0; i < created.Length; i++)
        {
            created[i] = GetPage(i);
        }

        return Interlocked.CompareExchange(ref _all, created, null) ?? created;
    }

    /// <summary>Makes the link slots once.</summary>
    /// <returns>The slots.</returns>
    private PdfLink[]?[] CreateLinks()
    {
        var created = new PdfLink[]?[Entries.Length];
        return Interlocked.CompareExchange(ref _links, created, null) ?? created;
    }

    /// <summary>Builds the index lookup once.</summary>
    /// <returns>The lookup.</returns>
    private Dictionary<int, int> CreateIndexes()
    {
        Dictionary<int, int> created = [with(Entries.Length)];
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Id.IsValid)
            {
                _ = created.TryAdd(Entries[i].Id.Number, i);
            }
        }

        return Interlocked.CompareExchange(ref _indexes, created, null) ?? created;
    }
}
