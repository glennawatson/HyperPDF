// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Carries the outline entries that lead to copied pages. An entry stays when its destination is a copied page or when an
/// entry below it stays; an entry that stays only for its children loses its own destination. The kept entries are
/// retargeted and appended to the end of the target's top-level entries, with the open and closed state of the source.
/// </summary>
[DebuggerDisplay("PdfCarryOutlines")]
internal sealed class PdfCarryOutlines
{
    /// <summary>The most entries read.</summary>
    private const int MaxEntries = 20_000;

    /// <summary>The deepest level read.</summary>
    private const int MaxDepth = 64;

    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>The destination retargeting.</summary>
    private readonly PdfCarryDestinations _destinations;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryOutlines"/> class.</summary>
    /// <param name="context">The context.</param>
    /// <param name="destinations">The destination retargeting.</param>
    internal PdfCarryOutlines(PdfCarryContext context, PdfCarryDestinations destinations)
    {
        _context = context;
        _destinations = destinations;
    }

    /// <summary>Copies the entries that lead to copied pages into the target's outline.</summary>
    internal void Finish()
    {
        if (_context.Source.Catalog.GetDictionary(KnownName.Outlines) is not { } sourceRoot)
        {
            return;
        }

        var budget = MaxEntries;
        var items = ReadLevel(sourceRoot.GetDictionary(KnownName.First), 0, [], ref budget);
        if (items.Count == 0)
        {
            return;
        }

        var catalog = _context.Catalog;
        var existing = catalog.GetDictionary(KnownName.Outlines);
        var rootId = catalog.GetRaw(KnownName.Outlines).AsReference();
        if (!rootId.IsValid)
        {
            rootId = _context.Sink.Reserve();
            catalog.Set(KnownName.Outlines, PdfValue.FromReference(rootId));
            _context.CatalogChanged = true;
        }

        var root = existing?.Clone() ?? NewRoot();
        var last = existing is null ? default : FindLast(existing);
        ReserveLevel(items);
        WriteLevel(items, rootId, last);
        Link(root, rootId, items, last);
    }

    /// <summary>Counts what an entry adds to its parent's count.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The entry itself, and its descendants when it is open.</returns>
    private static int CountVisible(OutlineItem item) => 1 + (item.IsOpen ? item.Visible : 0);

    /// <summary>Counts the entries below an entry that show when it is open.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The count.</returns>
    private static int CountDescendants(OutlineItem item)
    {
        var count = 0;
        foreach (var kid in item.Kids)
        {
            kid.Visible = CountDescendants(kid);
            count += CountVisible(kid);
        }

        return count;
    }

    /// <summary>Sets a sibling link when there is a sibling.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="key">/Prev or /Next.</param>
    /// <param name="sibling">The sibling, or an invalid id.</param>
    private static void SetLink(PdfDictionary entry, KnownName key, PdfObjectId sibling)
    {
        if (sibling.IsValid)
        {
            entry.Set(key, PdfValue.FromReference(sibling));
        }
    }

    /// <summary>Reads one level of the outline and the levels below it, keeping the entries that lead to copied pages.</summary>
    /// <param name="first">The first entry of the level.</param>
    /// <param name="depth">The level's depth.</param>
    /// <param name="visited">The entries already read, to stop at loops.</param>
    /// <param name="budget">The entries still allowed.</param>
    /// <returns>The kept entries.</returns>
    private List<OutlineItem> ReadLevel(PdfDictionary? first, int depth, HashSet<PdfDictionary> visited, ref int budget)
    {
        var items = new List<OutlineItem>();
        for (var entry = first; entry is not null && budget > 0 && depth < MaxDepth && visited.Add(entry); entry = entry.GetDictionary(KnownName.Next))
        {
            budget--;
            var kids = ReadLevel(entry.GetDictionary(KnownName.First), depth + 1, visited, ref budget);
            var destination = _destinations.Rewrite(entry.GetRaw(KnownName.Dest));
            var action = destination.IsNull ? RewriteGoTo(entry.GetRaw(KnownName.A)) : default;
            if (!destination.IsNull || !action.IsNull || kids.Count > 0 || IsElementCopied(entry))
            {
                items.Add(new(entry, destination, action, kids, entry.GetInteger(KnownName.Count) > 0));
            }
        }

        return items;
    }

    /// <summary>Retargets a go-to action; any other action is left out of the carried outline.</summary>
    /// <param name="raw">The action as stored in the source.</param>
    /// <returns>The value to copy, or null.</returns>
    private PdfValue RewriteGoTo(PdfValue raw) =>
        StoreReading.Resolve(
        _context.Source,
        raw).AsDictionary() is { } action
        && action.IsName(
        KnownName.S,
        KnownName.GoTo) ? _destinations.RewriteAction(raw) : default;

    /// <summary>Finds the last top-level entry of the target's outline, following the sibling links when the root lacks /Last.</summary>
    /// <param name="root">The target's outline root.</param>
    /// <returns>The entry, or an invalid id when the outline is empty.</returns>
    private PdfObjectId FindLast(PdfDictionary root)
    {
        var last = root.GetRaw(KnownName.Last).AsReference();
        if (last.IsValid || _context.TargetStore is not { } store)
        {
            return last;
        }

        var entry = root.GetRaw(KnownName.First).AsReference();
        for (var i = 0; entry.IsValid && i < MaxEntries; i++)
        {
            last = entry;
            entry = StoreReading.GetDictionary(store, entry)?.GetRaw(KnownName.Next).AsReference() ?? default;
        }

        return last;
    }

    /// <summary>Creates an outline root for a target that has none.</summary>
    /// <returns>The root.</returns>
    private PdfDictionary NewRoot()
    {
        var root = _context.NewDictionary();
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.Outlines));
        return root;
    }

    /// <summary>Reserves objects for a level and the levels below, and counts what each entry shows.</summary>
    /// <param name="items">The entries of the level.</param>
    private void ReserveLevel(List<OutlineItem> items)
    {
        foreach (var item in items)
        {
            item.Id = _context.Sink.Reserve();
            item.Visible = CountDescendants(item);
            ReserveLevel(item.Kids);
        }
    }

    /// <summary>Writes the entries of a level and the levels below.</summary>
    /// <param name="items">The entries of the level.</param>
    /// <param name="parent">The parent entry or the outline root.</param>
    /// <param name="before">The entry before the first, or an invalid id.</param>
    private void WriteLevel(List<OutlineItem> items, PdfObjectId parent, PdfObjectId before)
    {
        var sink = _context.Sink;
        for (var i = 0; i < items.Count; i++)
        {
            var previous = i == 0 ? before : items[i - 1].Id;
            var next = i + 1 < items.Count ? items[i + 1].Id : default;
            sink.Set(items[i].Id, PdfValue.FromDictionary(BuildEntry(items[i], parent, previous, next)));
            WriteLevel(items[i].Kids, items[i].Id, default);
        }
    }

    /// <summary>Builds the copy of an entry.</summary>
    /// <param name="item">The entry.</param>
    /// <param name="parent">The parent's id.</param>
    /// <param name="previous">The previous sibling, or an invalid id.</param>
    /// <param name="next">The next sibling, or an invalid id.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary BuildEntry(OutlineItem item, PdfObjectId parent, PdfObjectId previous, PdfObjectId next)
    {
        var sink = _context.Sink;
        var copy = _context.NewDictionary();
        foreach (var key in (ReadOnlySpan<KnownName>)[KnownName.Title, KnownName.C, KnownName.F])
        {
            copy.Set(key, sink.Import(item.Source.GetRaw(key)));
        }

        copy.Set(KnownName.Parent, PdfValue.FromReference(parent));
        SetLink(copy, KnownName.Prev, previous);
        SetLink(copy, KnownName.Next, next);
        if (item.Kids.Count > 0)
        {
            copy.Set(KnownName.First, PdfValue.FromReference(item.Kids[0].Id));
            copy.Set(KnownName.Last, PdfValue.FromReference(item.Kids[^1].Id));
            copy.Set(KnownName.Count, PdfValue.FromInteger(item.IsOpen ? item.Visible : -item.Visible));
        }

        copy.Set(KnownName.Dest, sink.Import(item.Destination));
        copy.Set(KnownName.A, sink.Import(item.Action));
        SetStructureElement(copy, item.Source);
        return copy;
    }

    /// <summary>Links an entry to the copy of its structure element (ISO 32000-2 12.3.3, Table 151), when that element was copied.</summary>
    /// <param name="copy">The entry's copy, changed in place.</param>
    /// <param name="source">The entry in the source.</param>
    private void SetStructureElement(PdfDictionary copy, PdfDictionary source)
    {
        var element = source.GetRaw(KnownName.SE).AsReference();
        if (element.IsValid && _context.Sink.GetMapped(element.Number) is var mapped and > 0)
        {
            copy.Set(KnownName.SE, PdfValue.FromReference(new(mapped, 0)));
        }
    }

    /// <summary>Determines whether an entry's structure element was copied, which keeps the entry.</summary>
    /// <param name="entry">The entry in the source.</param>
    /// <returns><see langword="true"/> when the element has a copy.</returns>
    private bool IsElementCopied(PdfDictionary entry)
    {
        var element = entry.GetRaw(KnownName.SE).AsReference();
        return element.IsValid && _context.Sink.GetMapped(element.Number) > 0;
    }

    /// <summary>Appends the carried entries to the root's list and writes the root.</summary>
    /// <param name="root">A copy of the target's root, changed in place.</param>
    /// <param name="rootId">The root's id.</param>
    /// <param name="items">The carried top-level entries.</param>
    /// <param name="last">The target's last top-level entry, or an invalid id.</param>
    private void Link(PdfDictionary root, PdfObjectId rootId, List<OutlineItem> items, PdfObjectId last)
    {
        var sink = _context.Sink;
        var total = 0;
        foreach (var item in items)
        {
            total += CountVisible(item);
        }

        if (last.IsValid && (_context.TargetStore is { } conditionalStore9975 ? StoreReading.GetDictionary(conditionalStore9975, last)?.Clone() : default(PdfDictionary)) is { } tail)
        {
            tail.Set(KnownName.Next, PdfValue.FromReference(items[0].Id));
            sink.Set(last, PdfValue.FromDictionary(tail));
        }
        else
        {
            root.Set(KnownName.First, PdfValue.FromReference(items[0].Id));
        }

        root.Set(KnownName.Last, PdfValue.FromReference(items[^1].Id));
        root.Set(KnownName.Count, PdfValue.FromInteger(Math.Max(0, root.GetInt32(KnownName.Count)) + total));
        sink.Set(rootId, PdfValue.FromDictionary(root));
    }

    /// <summary>An outline entry that is carried.</summary>
    [DebuggerDisplay("OutlineItem: {Kids.Count} kids")]
    private sealed class OutlineItem
    {
        /// <summary>Initializes a new instance of the <see cref="OutlineItem"/> class.</summary>
        /// <param name="source">The entry in the source.</param>
        /// <param name="destination">Its retargeted destination, or null.</param>
        /// <param name="action">Its retargeted go-to action, or null.</param>
        /// <param name="kids">The carried entries below it.</param>
        /// <param name="isOpen">Whether it shows its children.</param>
        internal OutlineItem(PdfDictionary source, PdfValue destination, PdfValue action, List<OutlineItem> kids, bool isOpen)
        {
            Source = source;
            Destination = destination;
            Action = action;
            Kids = kids;
            IsOpen = isOpen;
        }

        /// <summary>Gets the entry in the source.</summary>
        internal PdfDictionary Source { get; }

        /// <summary>Gets the retargeted destination, or null.</summary>
        internal PdfValue Destination { get; }

        /// <summary>Gets the retargeted go-to action, or null.</summary>
        internal PdfValue Action { get; }

        /// <summary>Gets the carried entries below it.</summary>
        internal List<OutlineItem> Kids { get; }

        /// <summary>Gets a value indicating whether it shows its children.</summary>
        internal bool IsOpen { get; }

        /// <summary>Gets or sets the entry's object in the target.</summary>
        internal PdfObjectId Id { get; set; }

        /// <summary>Gets or sets the number of entries below it that show when it is open.</summary>
        internal int Visible { get; set; }
    }
}
