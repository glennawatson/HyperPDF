// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Edits the outline (ISO 32000-2 12.3.3). Every entry is an indirect object with <c>/Parent</c>, and entries at one level
/// are a doubly linked list through <c>/Prev</c> and <c>/Next</c> with <c>/First</c> and <c>/Last</c> on the parent. An
/// entry's <c>/Count</c> is the number of entries that show below it: positive when it is open, negative when closed.
/// Each operation touches the entry, its neighbours and its ancestors, so it costs the length of the sibling walk to the
/// position plus the depth of the outline. Adding a list of entries builds each once.
/// </summary>
[DebuggerDisplay("PdfOutlineEditor")]
internal sealed class PdfOutlineEditor
{
    /// <summary>The most entries removed or counted in one operation.</summary>
    private const int MaxEntries = 4_000_000;

    /// <summary>The document being edited.</summary>
    private readonly PdfDocument _document;

    /// <summary>The working set of objects.</summary>
    private readonly PdfObjectEdits _edits;

    /// <summary>The outline root, once known.</summary>
    private PdfObjectId _rootId;

    /// <summary>Initializes a new instance of the <see cref="PdfOutlineEditor"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="edits">The working set.</param>
    internal PdfOutlineEditor(PdfDocument document, PdfObjectEdits edits)
    {
        _document = document;
        _edits = edits;
    }

    /// <summary>Adds bookmarks, with the bookmarks below them, as children of an entry.</summary>
    /// <param name="parent">The parent entry, or an invalid id for the top level.</param>
    /// <param name="index">The position among the parent's children; a negative number or one past the end appends.</param>
    /// <param name="specs">The bookmarks to add.</param>
    /// <param name="ids">Receives the object of each bookmark added at the top of <paramref name="specs"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <exception cref="ArgumentException">The parent is not an outline entry, or a bookmark's target is not valid.</exception>
    internal void Add(PdfObjectId parent, int index, IReadOnlyList<PdfBookmarkSpec> specs, List<PdfObjectId> ids, CancellationToken cancellationToken)
    {
        var parentId = ResolveParent(parent);
        var position = index;
        foreach (var spec in specs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = CreateSubtree(spec, parentId, 0, out var contribution, cancellationToken);
            Insert(parentId, position, id, contribution);
            ids.Add(id);
            position = position < 0 ? position : position + 1;
        }
    }

    /// <summary>Removes an entry and the entries below it.</summary>
    /// <param name="id">The entry.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    internal void Remove(PdfObjectId id)
    {
        RequireItem(id);
        _ = Unlink(id);
        var pending = new Stack<PdfObjectId>();
        var seen = new HashSet<int>();
        pending.Push(id);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current.Number) || _edits.Peek(current) is not { } item)
            {
                continue;
            }

            for (var kid = item.GetRaw(KnownName.First).AsReference(); kid.IsValid && seen.Count < MaxEntries; kid = _edits.Peek(kid)?.GetRaw(KnownName.Next).AsReference() ?? default)
            {
                pending.Push(kid);
            }

            _edits.Delete(current);
        }
    }

    /// <summary>Moves an entry, with the entries below it, to a position among another entry's children.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="newParent">The new parent, or an invalid id for the top level.</param>
    /// <param name="index">The position among the new parent's children, counted after the entry is taken out; a negative number or one past the end appends.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry, or the new parent is the entry or lies below it.</exception>
    internal void Move(PdfObjectId id, PdfObjectId newParent, int index)
    {
        RequireItem(id);
        var parentId = ResolveParent(newParent);
        if (IsSelfOrDescendant(parentId, id))
        {
            throw new ArgumentException("A bookmark cannot be moved into itself or into a bookmark below it.", nameof(newParent));
        }

        var contribution = Unlink(id);
        Insert(parentId, index, id, contribution);
    }

    /// <summary>Changes an entry's title.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="title">The new title.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    internal void Rename(PdfObjectId id, string title)
    {
        RequireItem(id);
        _edits.Edit(id)!.Set(KnownName.Title, PdfValue.FromString(PdfText.Encode(title, _document.Objects.Version)));
    }

    /// <summary>Changes where an entry leads.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="target">The new target.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry, or the target is not valid.</exception>
    internal void SetTarget(PdfObjectId id, PdfBookmarkTarget target)
    {
        RequireItem(id);
        PdfActionTargetWriter.Apply(_document, _edits.Edit(id)!, target);
    }

    /// <summary>Changes an entry's colour and style flags.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="style">The style, or <see langword="null"/> to remove the colour and flags.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    internal void SetStyle(PdfObjectId id, PdfBookmarkStyle? style)
    {
        RequireItem(id);
        ApplyStyle(_edits.Edit(id)!, style);
    }

    /// <summary>Links an entry to the structure element it stands for.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="element">The structure element, or an invalid id to remove the link.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    internal void SetStructureElement(PdfObjectId id, PdfObjectId element)
    {
        RequireItem(id);
        var item = _edits.Edit(id)!;
        if (element.IsValid)
        {
            item.Set(KnownName.SE, PdfValue.FromReference(element));
        }
        else
        {
            _ = item.Remove(KnownName.SE);
        }
    }

    /// <summary>Opens or closes an entry, so its children show or hide.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="open">Whether the entry is open.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    internal void SetOpen(PdfObjectId id, bool open)
    {
        RequireItem(id);
        var item = _edits.Edit(id)!;
        var count = item.GetInteger(KnownName.Count);
        var total = (int)Math.Abs(count);
        if (total == 0 || (count > 0) == open)
        {
            return;
        }

        item.Set(KnownName.Count, PdfValue.FromInteger(open ? total : -total));
        AdjustCounts(item.GetRaw(KnownName.Parent).AsReference(), open ? total : -total, false);
    }

    /// <summary>Works out every entry's <c>/Count</c> again from the entries below it, keeping each entry's open or closed state.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The number of entries whose count changed.</returns>
    internal int RepairCounts(CancellationToken cancellationToken)
    {
        var rootId = EnsureRoot();
        var changed = 0;
        var total = CountLevel(rootId, 0, ref changed, cancellationToken);
        _ = WriteCount(rootId, total, true, ref changed);
        return changed;
    }

    /// <summary>Writes an entry's colour and style flags.</summary>
    /// <param name="item">The entry's dictionary.</param>
    /// <param name="style">The style, or <see langword="null"/> to remove both.</param>
    private static void ApplyStyle(PdfDictionary item, PdfBookmarkStyle? style)
    {
        _ = item.Remove(KnownName.C);
        _ = item.Remove(KnownName.F);
        if (style is null)
        {
            return;
        }

        if (style.Red != 0 || style.Green != 0 || style.Blue != 0)
        {
            item.Set(KnownName.C, PdfValue.FromArray(new(item.Owner, [Component(style.Red), Component(style.Green), Component(style.Blue)])));
        }

        var flags = (style.Italic ? PdfDocumentNavigation.ItalicFlag : 0) | (style.Bold ? PdfDocumentNavigation.BoldFlag : 0);
        if (flags != 0)
        {
            item.Set(KnownName.F, PdfValue.FromInteger(flags));
        }
    }

    /// <summary>Makes a colour component a PDF number between 0 and 1.</summary>
    /// <param name="value">The component.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue Component(float value) => PdfValue.FromReal(Math.Clamp(value, 0F, 1F));

    /// <summary>Gets what an entry adds to its parent's count: itself, and everything below it when it is open.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The count.</returns>
    private static int Contribution(PdfDictionary item) => 1 + (int)Math.Max(0, item.GetInteger(KnownName.Count));

    /// <summary>Makes an outline entry for a bookmark, and the entries below it.</summary>
    /// <param name="spec">The bookmark.</param>
    /// <param name="parent">The parent entry.</param>
    /// <param name="depth">The depth below the top level.</param>
    /// <param name="contribution">Receives what the entry adds to its parent's count.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The entry's object.</returns>
    /// <exception cref="ArgumentException">The bookmarks nest too deeply, or a target is not valid.</exception>
    private PdfObjectId CreateSubtree(PdfBookmarkSpec spec, PdfObjectId parent, int depth, out int contribution, CancellationToken cancellationToken)
    {
        if (depth >= PdfDocumentNavigation.MaxOutlineDepth)
        {
            throw new ArgumentException("The bookmarks nest too deeply.", nameof(spec));
        }

        var item = new PdfDictionary(_document.Objects);
        item.Set(KnownName.Title, PdfValue.FromString(PdfText.Encode(spec.Title, _document.Objects.Version)));
        item.Set(KnownName.Parent, PdfValue.FromReference(parent));
        PdfActionTargetWriter.Apply(_document, item, spec.Target);
        ApplyStyle(item, spec.Style);
        if (spec.StructureElement.IsValid)
        {
            item.Set(KnownName.SE, PdfValue.FromReference(spec.StructureElement));
        }

        var id = _edits.Add(item);
        var total = CreateChildren(spec, id, item, depth, cancellationToken);
        contribution = 1 + (spec.IsOpen ? total : 0);
        return id;
    }

    /// <summary>Makes the entries below a bookmark and links them as its children.</summary>
    /// <param name="spec">The bookmark.</param>
    /// <param name="id">The bookmark's entry.</param>
    /// <param name="item">The entry's dictionary.</param>
    /// <param name="depth">The entry's depth.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The number of entries that show below the entry when it is open.</returns>
    private int CreateChildren(PdfBookmarkSpec spec, PdfObjectId id, PdfDictionary item, int depth, CancellationToken cancellationToken)
    {
        var total = 0;
        var previous = default(PdfObjectId);
        foreach (var child in spec.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kid = CreateSubtree(child, id, depth + 1, out var contribution, cancellationToken);
            total += contribution;
            if (previous.IsValid)
            {
                _edits.Edit(previous)!.Set(KnownName.Next, PdfValue.FromReference(kid));
                _edits.Edit(kid)!.Set(KnownName.Prev, PdfValue.FromReference(previous));
            }
            else
            {
                item.Set(KnownName.First, PdfValue.FromReference(kid));
            }

            previous = kid;
        }

        if (previous.IsValid)
        {
            item.Set(KnownName.Last, PdfValue.FromReference(previous));
            item.Set(KnownName.Count, PdfValue.FromInteger(spec.IsOpen ? total : -total));
        }

        return total;
    }

    /// <summary>Links an entry into a parent's list.</summary>
    /// <param name="parentId">The parent.</param>
    /// <param name="index">The position; a negative number or one past the end appends.</param>
    /// <param name="id">The entry.</param>
    /// <param name="contribution">What the entry adds to the parent's count.</param>
    private void Insert(PdfObjectId parentId, int index, PdfObjectId id, int contribution)
    {
        var parent = _edits.Edit(parentId)!;
        var item = _edits.Edit(id)!;
        item.Set(KnownName.Parent, PdfValue.FromReference(parentId));
        _ = item.Remove(KnownName.Prev);
        _ = item.Remove(KnownName.Next);
        var hadChildren = parent.GetRaw(KnownName.First).IsReference;
        FindPosition(parent, index, out var previous, out var next);
        if (previous.IsValid)
        {
            _edits.Edit(previous)!.Set(KnownName.Next, PdfValue.FromReference(id));
            item.Set(KnownName.Prev, PdfValue.FromReference(previous));
        }
        else
        {
            parent.Set(KnownName.First, PdfValue.FromReference(id));
        }

        if (next.IsValid)
        {
            _edits.Edit(next)!.Set(KnownName.Prev, PdfValue.FromReference(id));
            item.Set(KnownName.Next, PdfValue.FromReference(next));
        }
        else
        {
            parent.Set(KnownName.Last, PdfValue.FromReference(id));
        }

        AdjustCounts(parentId, contribution, !hadChildren);
    }

    /// <summary>Finds the entries on either side of a position in a parent's list.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="index">The position; a negative number or one past the end appends.</param>
    /// <param name="previous">Receives the entry before the position, or an invalid id.</param>
    /// <param name="next">Receives the entry at the position, or an invalid id.</param>
    private void FindPosition(PdfDictionary parent, int index, out PdfObjectId previous, out PdfObjectId next)
    {
        previous = default;
        next = parent.GetRaw(KnownName.First).AsReference();
        if (index < 0)
        {
            previous = parent.GetRaw(KnownName.Last).AsReference();
            next = default;
            return;
        }

        for (var i = 0; i < index && next.IsValid; i++)
        {
            previous = next;
            next = _edits.Peek(next)?.GetRaw(KnownName.Next).AsReference() ?? default;
        }
    }

    /// <summary>Takes an entry out of its parent's list and takes its count out of the ancestors' counts.</summary>
    /// <param name="id">The entry.</param>
    /// <returns>What the entry added to its parent's count.</returns>
    private int Unlink(PdfObjectId id)
    {
        var item = _edits.Edit(id)!;
        var parentId = item.GetRaw(KnownName.Parent).AsReference();
        var previous = item.GetRaw(KnownName.Prev).AsReference();
        var next = item.GetRaw(KnownName.Next).AsReference();
        Relink(previous, KnownName.Next, next);
        Relink(next, KnownName.Prev, previous);
        if (!previous.IsValid)
        {
            Relink(parentId, KnownName.First, next);
        }

        if (!next.IsValid)
        {
            Relink(parentId, KnownName.Last, previous);
        }

        _ = item.Remove(KnownName.Prev);
        _ = item.Remove(KnownName.Next);
        var contribution = Contribution(item);
        AdjustCounts(parentId, -contribution, false);
        return contribution;
    }

    /// <summary>Sets a link of an entry to another entry, or removes the link when there is none.</summary>
    /// <param name="holder">The entry that holds the link; an invalid id does nothing.</param>
    /// <param name="key">The link key.</param>
    /// <param name="target">The entry it leads to, or an invalid id for none.</param>
    private void Relink(PdfObjectId holder, KnownName key, PdfObjectId target)
    {
        if (!holder.IsValid || _edits.Edit(holder) is not { } dictionary)
        {
            return;
        }

        if (target.IsValid)
        {
            dictionary.Set(key, PdfValue.FromReference(target));
        }
        else
        {
            _ = dictionary.Remove(key);
        }
    }

    /// <summary>
    /// Changes the counts of an entry and its ancestors after the entries that show below it changed by an amount. The
    /// change reaches an ancestor only while the entries between are open, as a closed entry hides it.
    /// </summary>
    /// <param name="start">The entry whose children changed.</param>
    /// <param name="delta">The change in the number of entries that show below it.</param>
    /// <param name="openFirst">Whether the first entry becomes open, as it has just taken its first child.</param>
    private void AdjustCounts(PdfObjectId start, int delta, bool openFirst)
    {
        var rootId = EnsureRoot();
        var id = start;
        var first = true;
        while (id.IsValid)
        {
            var item = _edits.Edit(id)!;
            var isRoot = id.Number == rootId.Number;
            var count = item.GetInteger(KnownName.Count);
            var total = (int)Math.Max(0, Math.Abs(count) + delta);
            var open = isRoot || count > 0 || (first && openFirst);
            if (total == 0)
            {
                _ = item.Remove(KnownName.Count);
            }
            else
            {
                item.Set(KnownName.Count, PdfValue.FromInteger(open ? total : -total));
            }

            if (!open || isRoot)
            {
                return;
            }

            id = item.GetRaw(KnownName.Parent).AsReference();
            first = false;
        }
    }

    /// <summary>Counts a level again, entry by entry, and writes the counts that differ.</summary>
    /// <param name="parentId">The parent whose children are counted.</param>
    /// <param name="depth">The depth of the level.</param>
    /// <param name="changed">Counts the entries whose count was rewritten.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The number of entries that show below the parent when it is open.</returns>
    private int CountLevel(PdfObjectId parentId, int depth, ref int changed, CancellationToken cancellationToken)
    {
        var total = 0;
        var seen = new HashSet<int>();
        var kid = _edits.Peek(parentId)?.GetRaw(KnownName.First).AsReference() ?? default;
        while (kid.IsValid && depth < PdfDocumentNavigation.MaxOutlineDepth && seen.Add(kid.Number))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var below = CountLevel(kid, depth + 1, ref changed, cancellationToken);
            total += 1 + Math.Max(0, WriteCount(kid, below, false, ref changed));
            kid = _edits.Peek(kid)?.GetRaw(KnownName.Next).AsReference() ?? default;
        }

        return total;
    }

    /// <summary>Writes an entry's count when it differs from the number of entries below it.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="below">The number of entries that show below it when it is open.</param>
    /// <param name="alwaysOpen">Whether the entry is always open, as the root is.</param>
    /// <param name="changed">Counts the entries whose count was rewritten.</param>
    /// <returns>The count the entry has afterwards.</returns>
    private int WriteCount(PdfObjectId id, int below, bool alwaysOpen, ref int changed)
    {
        var count = _edits.Peek(id)!.GetInteger(KnownName.Count);
        var wanted = alwaysOpen || count > 0 ? below : -below;
        if (count == wanted)
        {
            return wanted;
        }

        var copy = _edits.Edit(id)!;
        if (wanted == 0)
        {
            _ = copy.Remove(KnownName.Count);
        }
        else
        {
            copy.Set(KnownName.Count, PdfValue.FromInteger(wanted));
        }

        changed++;
        return wanted;
    }

    /// <summary>Gets the outline root, making it when the document has none.</summary>
    /// <returns>The root's object.</returns>
    private PdfObjectId EnsureRoot()
    {
        if (_rootId.IsValid)
        {
            return _rootId;
        }

        var raw = _edits.CatalogForRead.GetRaw(KnownName.Outlines);
        if (raw.IsReference && _edits.Peek(raw.AsReference()) is not null)
        {
            _rootId = raw.AsReference();
            return _rootId;
        }

        var root = raw.AsDictionary()?.Clone() ?? new PdfDictionary(_document.Objects);
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.Outlines));
        _rootId = _edits.Add(root);
        _edits.Catalog().Set(KnownName.Outlines, PdfValue.FromReference(_rootId));
        return _rootId;
    }

    /// <summary>Checks that an object is a parent that entries can be added to: the root, or an outline entry.</summary>
    /// <param name="parent">The object, or an invalid id for the root.</param>
    /// <returns>The object to use as the parent.</returns>
    /// <exception cref="ArgumentException">The object is not in the outline.</exception>
    private PdfObjectId ResolveParent(PdfObjectId parent)
    {
        var rootId = EnsureRoot();
        if (!parent.IsValid || parent.Number == rootId.Number)
        {
            return rootId;
        }

        RequireItem(parent);
        return parent;
    }

    /// <summary>Checks that an object is an outline entry.</summary>
    /// <param name="id">The object.</param>
    /// <exception cref="ArgumentException">The object is not an outline entry.</exception>
    private void RequireItem(PdfObjectId id)
    {
        var rootId = EnsureRoot();
        if (!id.IsValid || id.Number == rootId.Number || !IsSelfOrDescendant(id, rootId))
        {
            throw new ArgumentException("The object is not a bookmark of this document.", nameof(id));
        }
    }

    /// <summary>Determines whether an entry is another entry or lies below it.</summary>
    /// <param name="candidate">The entry to test; the root is never below an entry.</param>
    /// <param name="ancestor">The other entry.</param>
    /// <returns><see langword="true"/> when the candidate is the ancestor or below it.</returns>
    private bool IsSelfOrDescendant(PdfObjectId candidate, PdfObjectId ancestor)
    {
        var current = candidate;
        for (var steps = 0; current.IsValid && steps <= PdfDocumentNavigation.MaxOutlineDepth; steps++)
        {
            if (current.Number == ancestor.Number)
            {
                return true;
            }

            current = _edits.Peek(current)?.GetRaw(KnownName.Parent).AsReference() ?? default;
        }

        return false;
    }
}
