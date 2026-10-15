// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Adds, removes, and reorders document pages.</summary>
public static class PdfDocumentPageOperations
{
    /// <summary>Sets a page's rotation, normalised to 0, 90, 180 or 270 degrees clockwise (ISO 32000-2 Table 31 /Rotate).</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="degrees">The rotation in degrees.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public static void SetRotation(PdfDocument document, int pageIndex, int degrees)
    {
        _ = PdfDocumentPages.GetPage(document, pageIndex);
        PdfDocumentEditing.RunEdit(
document,
"Rotate page",
PdfChangeKinds.PageChanges,
new PageEdit(document, [pageIndex], degrees, null),
static (transaction, edit) => PdfDocumentPageOperations.RotateCore(edit.Document, transaction, edit.Pages, edit.Value, false));
    }

    /// <summary>Turns pages by an amount, added to each page's current rotation and normalised.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes.</param>
    /// <param name="degrees">The turn in degrees clockwise, for example 90 or -90.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    public static void RotatePages(PdfDocument document, ReadOnlySpan<int> pages, int degrees)
    {
        PdfDocumentPageOperations.ValidatePages(document, pages);
        PdfDocumentEditing.RunEdit(
document,
"Rotate pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, pages, degrees, null),
static (transaction, edit) => PdfDocumentPageOperations.RotateCore(edit.Document, transaction, edit.Pages, edit.Value, true));
    }

    /// <summary>Deletes pages. At least one page must remain.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes; duplicates are ignored.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    /// <exception cref="ArgumentException">Every page would be deleted.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DeletePages(PdfDocument document, ReadOnlySpan<int> pages) => DeletePages(document, pages, null);

    /// <summary>
    /// Deletes pages. At least one page must remain. The remaining separation pages of a printed page that lost some of
    /// its separations are linked to each other again, and the report says which printed pages these are.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes; duplicates are ignored.</param>
    /// <param name="report">Receives what the deletion changed, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    /// <exception cref="ArgumentException">Every page would be deleted.</exception>
    public static void DeletePages(PdfDocument document, ReadOnlySpan<int> pages, PdfPageEditReport? report)
    {
        PdfDocumentPageOperations.ValidatePages(document, pages);
        PdfDocumentEditing.RunEdit(
document,
"Delete pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, pages, 0, null) { Report = report },
static (transaction, edit) => PdfDocumentPageOperations.DeleteCore(edit.Document, transaction, edit.Pages, edit.Report));
    }

    /// <summary>Moves pages, in the order given, so the first lands at <paramref name="destination"/> in the result.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes; each at most once.</param>
    /// <param name="destination">The index of the first moved page afterwards, from 0 to page count minus moved count.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index or the destination is out of range.</exception>
    /// <exception cref="ArgumentException">A page is listed twice.</exception>
    public static void MovePages(PdfDocument document, ReadOnlySpan<int> pages, int destination)
    {
        PdfDocumentPageOperations.ValidatePages(document, pages);
        ArgumentOutOfRangeException.ThrowIfNegative(destination);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination, document.PageCount - pages.Length);
        PdfDocumentEditing.RunEdit(
document,
"Move pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, pages, destination, null),
static (transaction, edit) => PdfDocumentPageOperations.MoveCore(edit.Document, transaction, edit.Pages, edit.Value));
    }

    /// <summary>Puts every page in a new order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="order">For each new position, the page's current index; a permutation of every page.</param>
    /// <exception cref="ArgumentException"><paramref name="order"/> is not a permutation of the pages.</exception>
    public static void ReorderPages(PdfDocument document, ReadOnlySpan<int> order)
    {
        if (order.Length != document.PageCount)
        {
            throw new ArgumentException("The order must list every page once.", nameof(order));
        }

        PdfDocumentPageOperations.ValidatePages(document, order);
        PdfDocumentPageOperations.ValidateOrder(order);
        PdfDocumentEditing.RunEdit(
document,
"Reorder pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, order, 0, null),
static (transaction, edit) => PdfDocumentPageOperations.ReorderCore(edit.Document, transaction, edit.Pages));
    }

    /// <summary>Copies pages of another document (or this one) into this document before a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the first copy takes, from 0 to the page count.</param>
    /// <param name="source">The document copied from; it must stay open until this document is saved.</param>
    /// <param name="sourcePages">The zero based indexes of the pages copied, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InsertPages(PdfDocument document, int index, PdfDocument source, ReadOnlySpan<int> sourcePages) =>
        InsertPages(document, index, source, sourcePages, CancellationToken.None);

    /// <summary>Inserts pages while passing cancellation to the object importer.</summary>
    /// <param name="document">The destination.</param>
    /// <param name="index">The insertion index.</param>
    /// <param name="source">The source document.</param>
    /// <param name="sourcePages">The source page indexes.</param>
    /// <param name="cancellationToken">Cancels copying.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InsertPages(PdfDocument document, int index, PdfDocument source, ReadOnlySpan<int> sourcePages, CancellationToken cancellationToken) =>
        InsertPages(document, index, source, sourcePages, null, cancellationToken);

    /// <summary>
    /// Inserts pages while passing cancellation to the object importer. Copied separation pages stay linked to the other
    /// copied separations of their printed page; the report says which printed pages were not copied whole.
    /// </summary>
    /// <param name="document">The destination.</param>
    /// <param name="index">The insertion index.</param>
    /// <param name="source">The source document.</param>
    /// <param name="sourcePages">The source page indexes.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels copying.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public static void InsertPages(PdfDocument document, int index, PdfDocument source, ReadOnlySpan<int> sourcePages, PdfPageEditReport? report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, document.PageCount);
        PdfDocumentPageOperations.ValidatePages(source, sourcePages);
        PdfDocumentEditing.RunEdit(
document,
"Insert pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, sourcePages, index, source, cancellationToken) { Report = report },
static (transaction, edit) => PdfDocumentPageOperations.InsertCore(edit.Document, transaction, edit.Value, edit.Source!, edit.Pages, edit.Report, edit.CancellationToken));
    }

    /// <summary>Writes some pages as a new document, keeping their content, resources, boxes, rotation and annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes, in order.</param>
    /// <returns>The new document's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExtractPages(PdfDocument document, ReadOnlySpan<int> pages) =>
        ExtractPages(document, pages, CancellationToken.None);

    /// <summary>Writes selected pages while passing cancellation to the page importer.</summary>
    /// <param name="document">The source document.</param>
    /// <param name="pages">The selected page indexes.</param>
    /// <param name="cancellationToken">Cancels copying.</param>
    /// <returns>The new PDF bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExtractPages(PdfDocument document, ReadOnlySpan<int> pages, CancellationToken cancellationToken) =>
        ExtractPages(document, pages, null, cancellationToken);

    /// <summary>
    /// Writes selected pages as a new document. Copied separation pages stay linked to the other copied separations of
    /// their printed page; the report says which printed pages were not copied whole.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="pages">The selected page indexes.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels copying.</param>
    /// <returns>The new PDF bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    public static byte[] ExtractPages(PdfDocument document, ReadOnlySpan<int> pages, PdfPageEditReport? report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfDocumentPageOperations.ValidatePages(document, pages);
        var builder = new PdfDocumentBuilder();
        var importer = new PdfPageImporter(builder, document, true, cancellationToken);
        var selected = new PdfPage[pages.Length];
        for (var i = 0; i < selected.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            selected[i] = PdfDocumentPages.GetPage(document, pages[i]);
        }

        importer.ImportPages(selected, report);
        return builder.ToArray();
    }

    /// <summary>Copies a page's dictionary so it can be changed and stored back with <see cref="StoreEdited"/>.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="page">The page.</param>
    /// <returns>The copy.</returns>
    internal static PdfDictionary CloneForEdit(PdfEditTransaction transaction, PdfPage page) =>
        page.Id.IsValid ? transaction.CloneDictionary(page.Id) ?? page.Dictionary.Clone() : page.Dictionary.Clone();

    /// <summary>Stores an edited copy of a page's dictionary; a direct page is kept for <see cref="FinishEdited"/>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="page">The page that was copied.</param>
    /// <param name="copy">The edited dictionary.</param>
    /// <param name="direct">The page slots of a direct page edit, created on first use.</param>
    internal static void StoreEdited(PdfDocument document, PdfEditTransaction transaction, PdfPage page, PdfDictionary copy, ref List<PdfPageSlot>? direct)
    {
        if (page.Id.IsValid)
        {
            transaction.Replace(page.Id, PdfValue.FromDictionary(copy));
            return;
        }

        // A direct page lives in its parent's /Kids, so the tree is written again around it.
        direct ??= PdfDocumentPageOperations.CurrentSlots(document);
        direct[page.Index] = new(PdfValue.FromDictionary(copy), page.Index);
    }

    /// <summary>Writes the page tree again when direct pages were edited.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="direct">The slots kept by <see cref="StoreEdited"/>, or <see langword="null"/>.</param>
    internal static void FinishEdited(PdfDocument document, PdfEditTransaction transaction, List<PdfPageSlot>? direct)
    {
        if (direct is not null)
        {
            PdfDocumentPageOperations.WritePages(document, transaction, CollectionsMarshal.AsSpan(direct));
        }
    }

    /// <summary>Adds a new page object to the document and puts it in the page tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The index the page takes, from 0 to the page count.</param>
    /// <param name="page">The page dictionary, without a <c>/Parent</c>.</param>
    internal static void InsertNewPage(PdfDocument document, PdfEditTransaction transaction, int index, PdfDictionary page)
    {
        var id = transaction.Add(PdfValue.FromDictionary(page));
        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        slots.Insert(index, new(PdfValue.FromReference(id), -1));
        PdfDocumentPageOperations.WritePages(document, transaction, CollectionsMarshal.AsSpan(slots));
    }

    /// <summary>Collects indexes into a set.</summary>
    /// <param name="pages">The indexes.</param>
    /// <param name="rejectDuplicates">Whether a repeated index is an error.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentException">An index repeats and <paramref name="rejectDuplicates"/> is set.</exception>
    private static HashSet<int> UniquePages(ReadOnlySpan<int> pages, bool rejectDuplicates)
    {
        var set = new HashSet<int>(pages.Length);
        foreach (var index in pages)
        {
            if (!set.Add(index) && rejectDuplicates)
            {
                throw new ArgumentException("A page is listed more than once.", nameof(pages));
            }
        }

        return set;
    }

    /// <summary>Checks a validated page permutation without retaining a set of indexes.</summary>
    /// <param name="pages">The complete page order.</param>
    /// <exception cref="ArgumentException">An index repeats.</exception>
    private static void ValidateOrder(ReadOnlySpan<int> pages)
    {
        var seen = default(PooledBits);
        try
        {
            seen.Rent(pages.Length);
            foreach (var index in pages)
            {
                if (!seen.Add(index))
                {
                    throw new ArgumentException("A page is listed more than once.", nameof(pages));
                }
            }
        }
        finally
        {
            seen.Dispose();
        }
    }

    /// <summary>Determines whether any inserted page brings a label.</summary>
    /// <param name="slots">The pages in their new order.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasInsertedLabels(ReadOnlySpan<PdfPageSlot> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.Label is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gives a page without a label of its own the label after the page before it.</summary>
    /// <param name="labels">The labels so far.</param>
    /// <param name="index">The page's position.</param>
    /// <param name="decimalStyle">The style of a first page with nothing before it.</param>
    /// <returns>The label.</returns>
    private static PdfPageLabel ContinueLabel(PdfPageLabel[] labels, int index, PdfDictionary decimalStyle) => index == 0 ? new(decimalStyle, 1) : labels[index - 1] with
    {
        Number = labels[index - 1].Number + 1
    };

    /// <summary>Checks every index is a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The indexes.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    private static void ValidatePages(PdfDocument document, ReadOnlySpan<int> pages)
    {
        foreach (var index in pages)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)document.PageCount, nameof(pages));
        }
    }

    /// <summary>Gets each page's current /Kids entry and index.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The slots, in page order.</returns>
    private static List<PdfPageSlot> CurrentSlots(PdfDocument document)
    {
        var entries = PdfDocumentPages.GetPageSet(document).Entries;
        var slots = new List<PdfPageSlot>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            slots.Add(new(entries[i].Id.IsValid ? PdfValue.FromReference(entries[i].Id) : PdfValue.FromDictionary(entries[i].Direct!), i));
        }

        return slots;
    }

    /// <summary>Rotates pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes.</param>
    /// <param name="degrees">The rotation or turn.</param>
    /// <param name="relative">Whether <paramref name="degrees"/> is added to the current rotation.</param>
    private static void RotateCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages, int degrees, bool relative)
    {
        List<PdfPageSlot>? direct = null;
        foreach (var index in PdfDocumentPageOperations.UniquePages(pages, false))
        {
            var page = PdfDocumentPages.GetPage(document, index);
            var rotation = PdfPage.NormaliseRotation(relative ? page.Rotation + PdfPage.NormaliseRotation(degrees) : degrees);
            var copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            copy.Set(KnownName.Rotate, PdfValue.FromInteger(rotation));
            PdfDocumentPageOperations.StoreEdited(document, transaction, page, copy, ref direct);
        }

        PdfDocumentPageOperations.FinishEdited(document, transaction, direct);
    }

    /// <summary>Deletes pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes.</param>
    /// <param name="report">Receives what the deletion changed, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Every page would be deleted.</exception>
    private static void DeleteCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages, PdfPageEditReport? report)
    {
        var removed = PdfDocumentPageOperations.UniquePages(pages, false);
        if (removed.Count >= document.PageCount)
        {
            throw new ArgumentException("At least one page must remain.", nameof(pages));
        }

        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        var deleted = new HashSet<int>();
        var kept = new List<PdfPageSlot>(slots.Count - removed.Count);
        foreach (var slot in slots)
        {
            if (!removed.Contains(slot.OldIndex))
            {
                kept.Add(slot);
            }
            else if (slot.Kid.IsReference)
            {
                _ = deleted.Add(slot.Kid.AsReference().Number);
            }
        }

        // A page listed twice survives when one of its listings is kept.
        foreach (var slot in kept)
        {
            _ = deleted.Remove(slot.Kid.AsReference().Number);
        }

        PdfDocumentPruning.PruneDestinations(document, transaction, deleted);
        PdfCarrySeparations.RelinkAfterDelete(document, transaction, removed, report);
        PdfDocumentPageOperations.WritePages(document, transaction, CollectionsMarshal.AsSpan(kept));
    }

    /// <summary>Moves pages so the first lands at a destination.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes, each once.</param>
    /// <param name="destination">The first moved page's index afterwards.</param>
    private static void MoveCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages, int destination)
    {
        var moved = PdfDocumentPageOperations.UniquePages(pages, true);
        if (PdfDocumentPageOperations.TryMoveInPlace(document, transaction, pages, destination))
        {
            return;
        }

        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        var result = new List<PdfPageSlot>(slots.Count);
        foreach (var slot in slots)
        {
            if (!moved.Contains(slot.OldIndex))
            {
                result.Add(slot);
            }
        }

        var insert = new PdfPageSlot[pages.Length];
        for (var i = 0; i < pages.Length; i++)
        {
            insert[i] = slots[pages[i]];
        }

        result.InsertRange(destination, insert);
        PdfDocumentPageOperations.WritePages(document, transaction, CollectionsMarshal.AsSpan(result));
    }

    /// <summary>
    /// Moves pages by taking each out of its node and putting them all in the node at the destination, changing only those
    /// nodes and the page counts above them. It does not apply when the document has page labels, a page is direct, or a
    /// node passes attributes down to its kids; the tree is then written again whole.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes, each once.</param>
    /// <param name="destination">The first moved page's index afterwards.</param>
    /// <returns><see langword="true"/> when the pages were moved.</returns>
    private static bool TryMoveInPlace(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages, int destination)
    {
        var store = document.Objects;
        var set = PdfDocumentPages.GetPageSet(document);
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is not null || !PdfPageTreeEditor.CanMove(store, set))
        {
            return false;
        }

        var kids = new PdfValue[pages.Length];
        for (var i = 0; i < kids.Length; i++)
        {
            var id = set.Entries[pages[i]].Id;
            if (!id.IsValid)
            {
                return false;
            }

            kids[i] = PdfValue.FromReference(id);
        }

        // Taking the highest page first keeps the lower indexes where the tree has them.
        var order = pages.ToArray();
        Array.Sort(order);
        for (var i = order.Length - 1; i >= 0; i--)
        {
            if (!PdfPageTreeEditor.Remove(transaction, store, order[i]))
            {
                return false;
            }
        }

        // The pages are all still in the set, so a tree that cannot take them in place is written again from the set.
        using var located = PdfPageTreeEditor.Locate(store, set, destination, kids.Length);
        if (located is null)
        {
            return false;
        }

        PdfPageTreeEditor.Insert(transaction, store, located, kids);
        Volatile.Write(ref document.State.NextPageSet, set.WithMoved(pages, destination));
        return true;
    }

    /// <summary>Puts every page in a new order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="order">The current index of the page at each new position.</param>
    private static void ReorderCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> order)
    {
        var entries = PdfDocumentPages.GetPageSet(document).Entries;
        var rented = ArrayPool<PdfPageSlot>.Shared.Rent(order.Length);
        try
        {
            var result = rented.AsSpan(0, order.Length);
            for (var position = 0; position < order.Length; position++)
            {
                var index = order[position];
                var entry = entries[index];
                result[position] = new(entry.Id.IsValid ? PdfValue.FromReference(entry.Id) : PdfValue.FromDictionary(entry.Direct!), index);
            }

            PdfDocumentPageOperations.WritePages(document, transaction, result);
        }
        finally
        {
            ArrayPool<PdfPageSlot>.Shared.Return(rented, true);
        }
    }

    /// <summary>Copies pages from a document and places them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The index of the first copy.</param>
    /// <param name="source">The document copied from.</param>
    /// <param name="sourcePages">The source page indexes.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void InsertCore(
        PdfDocument document,
        PdfEditTransaction transaction,
        int index,
        PdfDocument source,
        ReadOnlySpan<int> sourcePages,
        PdfPageEditReport? report,
        CancellationToken cancellationToken)
    {
        var importer = new PdfStoreImporter(transaction, document.Objects, source.Objects, cancellationToken);
        var carrier = new PdfDocumentCarrier(importer);
        var ids = new PdfObjectId[sourcePages.Length];
        var pages = new PdfPage[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            pages[i] = PdfDocumentPages.GetPage(source, sourcePages[i]);
        }

        // Planned before any object is added, while the source's separation groups still describe its pages.
        var separations = PdfCarrySeparations.Plan(source, pages, report);
        var separationKey = SeparationReader.InfoKey(source.Objects.Names);
        for (var i = 0; i < ids.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ids[i] = transaction.Add(default);

            // Links and annotations that name a copied page now name its copy.
            carrier.AddPage(pages[i].Id, ids[i]);
        }

        var annotations = new PdfArray?[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            annotations[i] = carrier.PrepareAnnotations(pages[i], PdfAnnotationFilter.All, report);
        }

        var structParents = PdfDocumentPageOperations.ImportStructure(document, transaction, importer, source, sourcePages, ids);
        var labels = PdfDocumentLabels.CollectSourceLabels(source, pages) is { } sourceLabels ? PdfPageLabelWriter.Retarget(sourceLabels, importer) : null;
        var inserted = new PdfPageSlot[ids.Length];
        var copies = new PdfDictionary[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            copies[i] = PdfDocumentPageOperations.CopyPage(document, importer, pages[i], annotations[i], separationKey, new(separations, i, ids, structParents[i]));
            inserted[i] = new(PdfValue.FromReference(ids[i]), -1) { Label = labels?[i] };
        }

        var set = PdfDocumentPages.GetPageSet(document);
        using var place = PdfDocumentPageOperations.StoreCopies(document, transaction, set, index, ids, copies);
        carrier.Finish(report);
        PdfDocumentPageOperations.PlaceInserted(document, transaction, set, place, index, inserted);
    }

    /// <summary>Copies a page and gives the copy its separation entry and its structure parent key.</summary>
    /// <param name="document">The document.</param>
    /// <param name="importer">The importer.</param>
    /// <param name="page">The source page.</param>
    /// <param name="annotations">The annotations the carrier prepared, or <see langword="null"/>.</param>
    /// <param name="separationKey">The source's <c>/SeparationInfo</c> name.</param>
    /// <param name="plan">What the copy takes from the separation plan and the structure import.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary CopyPage(PdfDocument document, PdfStoreImporter importer, PdfPage page, PdfArray? annotations, PdfName separationKey, in CopyPlan plan)
    {
        var copy = PdfDocumentPageOperations.ImportPage(document, importer, page, annotations, separationKey);
        if (PdfCarrySeparations.Build(plan.Separations, plan.Position, importer, plan.Ids) is { IsNull: false } separation)
        {
            copy.Set(SeparationReader.InfoKey(document.Objects.Names), separation);
        }

        if (plan.StructParent >= 0)
        {
            copy.Set(KnownName.StructParents, PdfValue.FromInteger(plan.StructParent));
        }

        return copy;
    }

    /// <summary>Stores the copied pages and puts them in the page tree: in place when the copies hold what the nodes above them would otherwise pass down.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="set">The document's pages before the insertion.</param>
    /// <param name="index">The index the first copy takes.</param>
    /// <param name="ids">The objects reserved for the copies.</param>
    /// <param name="copies">The copies.</param>
    /// <returns>The path to the node that takes the copies, which the caller disposes, or <see langword="null"/> when the tree has to be written again.</returns>
    private static PageTreePath? StoreCopies(PdfDocument document, PdfEditTransaction transaction, PdfPageSet set, int index, PdfObjectId[] ids, PdfDictionary[] copies)
    {
        var located = PdfPageTreeEditor.Locate(document.Objects, set, index, ids.Length);
        var place = located is not null && PdfPageTreeEditor.KeepsAttributes(located, copies) ? located : null;
        if (place is null)
        {
            located?.Dispose();
        }

        for (var i = 0; i < ids.Length; i++)
        {
            if (place is not null)
            {
                copies[i].Set(KnownName.Parent, PdfValue.FromReference(place.Deepest.Id));
            }

            transaction.Replace(ids[i], PdfValue.FromDictionary(copies[i]));
        }

        return place;
    }

    /// <summary>Puts inserted pages in the page tree: in place when a path to them was found, otherwise by writing the tree again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="set">The document's pages before the insertion.</param>
    /// <param name="place">The path to the node that takes the pages, or <see langword="null"/> to write the tree again.</param>
    /// <param name="index">The index the first inserted page takes.</param>
    /// <param name="inserted">The inserted pages, whose objects already hold their dictionaries.</param>
    private static void PlaceInserted(PdfDocument document, PdfEditTransaction transaction, PdfPageSet set, PageTreePath? place, int index, PdfPageSlot[] inserted)
    {
        if (place is null)
        {
            var slots = PdfDocumentPageOperations.CurrentSlots(document);
            slots.InsertRange(index, inserted);
            PdfDocumentPageOperations.WritePages(document, transaction, CollectionsMarshal.AsSpan(slots));
            return;
        }

        var labels = PdfDocumentPageOperations.LabelsAfterInsert(document, index, inserted);
        var kids = new PdfValue[inserted.Length];
        var entries = new PdfPageEntry[inserted.Length];
        for (var i = 0; i < inserted.Length; i++)
        {
            kids[i] = inserted[i].Kid;
            entries[i] = new(inserted[i].Kid.AsReference(), null, 0);
        }

        PdfPageTreeEditor.Insert(transaction, document.Objects, place, kids);
        if (labels is not null)
        {
            PdfPageLabelWriter.Write(transaction, document.Objects, labels);
        }

        // The edit's invalidation takes these pages instead of reading the tree again.
        Volatile.Write(ref document.State.NextPageSet, set.WithInserted(index, entries));
    }

    /// <summary>Gets the page labels after an insertion, or <see langword="null"/> when the document has none and the pages bring none.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the first inserted page takes.</param>
    /// <param name="inserted">The inserted pages.</param>
    /// <returns>The label of every page afterwards, or <see langword="null"/>.</returns>
    private static PdfPageLabel[]? LabelsAfterInsert(PdfDocument document, int index, PdfPageSlot[] inserted)
    {
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is null && !PdfDocumentPageOperations.HasInsertedLabels(inserted))
        {
            return null;
        }

        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        slots.InsertRange(index, inserted);
        return PdfDocumentPageOperations.CollectLabels(document, CollectionsMarshal.AsSpan(slots));
    }

    /// <summary>Copies the structure elements of the inserted pages when both documents are tagged.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="importer">The importer.</param>
    /// <param name="source">The document copied from.</param>
    /// <param name="sourcePages">The source page indexes.</param>
    /// <param name="ids">The reserved target page objects.</param>
    /// <returns>The new <c>/StructParents</c> key of each page, or -1 for none; all -1 when the target is untagged.</returns>
    private static int[] ImportStructure(PdfDocument document, PdfEditTransaction transaction, PdfStoreImporter importer, PdfDocument source, ReadOnlySpan<int> sourcePages, PdfObjectId[] ids)
    {
        var pages = new PdfPage[ids.Length];
        var targets = new Dictionary<int, PdfObjectId>(ids.Length);
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = PdfDocumentPages.GetPage(source, sourcePages[i]);
            if (pages[i].Id.IsValid)
            {
                targets[pages[i].Id.Number] = ids[i];
            }
        }

        if (PdfStructureImporter.Create(transaction, document.Objects, source.Objects, importer, targets) is { } structure)
        {
            return structure.Import(pages);
        }

        var none = new int[ids.Length];
        Array.Fill(none, -1);
        return none;
    }

    /// <summary>
    /// Copies a page dictionary. Boxes, rotation and resources are written on the page itself, so nothing the target's
    /// page tree carries can be inherited by mistake. The separation dictionary is left out: the caller writes one from
    /// the copy's plan, so it never names a page that was not copied.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="importer">The importer.</param>
    /// <param name="page">The source page.</param>
    /// <param name="annotations">The annotations the carrier prepared, or <see langword="null"/> when the page has none.</param>
    /// <param name="separationKey">The source's <c>/SeparationInfo</c> name.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary ImportPage(PdfDocument document, PdfStoreImporter importer, PdfPage page, PdfArray? annotations, PdfName separationKey)
    {
        var copy = new PdfDictionary(document.Objects, page.Dictionary.Count + 1);
        var source = page.Dictionary;
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (PdfDocumentPageOperations.IsLeftOutOfCopy(key, separationKey))
            {
                continue;
            }

            var value = importer.Import(key.Is(KnownName.Annots) && annotations is not null ? PdfValue.FromArray(annotations) : source.GetValueAt(i));
            if (!value.IsNull)
            {
                copy.Add(importer.ImportName(key), value);
            }
        }

        copy.Set(KnownName.Type, PdfValue.FromName(KnownName.Page));
        copy.Set(KnownName.MediaBox, PdfValue.FromArray(page.MediaBox.ToArray(document.Objects)));
        copy.Set(KnownName.CropBox, PdfValue.FromArray(page.CropBox.ToArray(document.Objects)));
        copy.Set(KnownName.Rotate, PdfValue.FromInteger(page.Rotation));
        if (!copy.ContainsKey(KnownName.Resources))
        {
            copy.Set(KnownName.Resources, page.Resources is { } resources ? importer.Import(PdfValue.FromDictionary(resources)) : PdfValue.FromDictionary(new(document.Objects)));
        }

        return copy;
    }

    /// <summary>Determines whether a page dictionary key is left out of a copied page because the copy writes its own.</summary>
    /// <param name="key">The source key.</param>
    /// <param name="separationKey">The source's <c>/SeparationInfo</c> name.</param>
    /// <returns><see langword="true"/> for /Type, /Parent, /StructParents and /SeparationInfo.</returns>
    private static bool IsLeftOutOfCopy(PdfName key, PdfName separationKey) =>
        key.Is(KnownName.Type) || key.Is(KnownName.Parent) || key.Is(KnownName.StructParents) || key == separationKey;

    /// <summary>Writes the page tree and, when the document has labels, the labels that follow the pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="slots">The pages in their new order.</param>
    private static void WritePages(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<PdfPageSlot> slots)
    {
        var labelled = document.Catalog.GetDictionary(KnownName.PageLabels) is not null || PdfDocumentPageOperations.HasInsertedLabels(slots);
        var labels = labelled ? PdfDocumentPageOperations.CollectLabels(document, slots) : null;
        PdfPageTreeWriter.Write(transaction, document.Objects, slots);
        if (labels is not null)
        {
            PdfPageLabelWriter.Write(transaction, document.Objects, labels);
        }
    }

    /// <summary>Gets the label each page in a new order keeps; an inserted page continues the label before it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="slots">The pages in their new order.</param>
    /// <returns>The labels.</returns>
    private static PdfPageLabel[] CollectLabels(PdfDocument document, ReadOnlySpan<PdfPageSlot> slots)
    {
        var ranges = PdfDocumentLabels.GetLabelRanges(document);

        // Pages before the first range are labelled with their decimal page number; one shared style keeps them in runs.
        var decimalStyle = new PdfDictionary(document.Objects);
        decimalStyle.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        var labels = new PdfPageLabel[slots.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var old = slots[i].OldIndex;
            if (old < 0)
            {
                labels[i] = slots[i].Label ?? PdfDocumentPageOperations.ContinueLabel(labels, i, decimalStyle);
                continue;
            }

            labels[i] = PdfDocumentLabels.FindRange(ranges, old) is { } range ? new(range.Label, old - range.Start + range.Label.GetInt32(KnownName.St, 1)) : new(decimalStyle, old + 1);
        }

        return labels;
    }

    /// <summary>What one copied page takes from the separation plan and the structure import.</summary>
    /// <param name="Separations">The plan, or <see langword="null"/>.</param>
    /// <param name="Position">The page's position among the copied pages.</param>
    /// <param name="Ids">The objects reserved for the copies.</param>
    /// <param name="StructParent">The page's new <c>/StructParents</c> key, or -1 for none.</param>
    private readonly record struct CopyPlan(SeparationCopyPlan? Separations, int Position, PdfObjectId[] Ids, int StructParent);

    /// <summary>The arguments of a page operation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The page indexes.</param>
    /// <param name="value">The operation's number: a rotation, a destination or an insertion index.</param>
    /// <param name="source">The document pages are copied from, for an insertion.</param>
    /// <param name="cancellationToken">Cancels object import.</param>
    private readonly ref struct PageEdit(PdfDocument document, ReadOnlySpan<int> pages, int value, PdfDocument? source, CancellationToken cancellationToken = default)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the page indexes.</summary>
        internal ReadOnlySpan<int> Pages { get; } = pages;

        /// <summary>Gets the operation's number.</summary>
        internal int Value { get; } = value;

        /// <summary>Gets the source document.</summary>
        internal PdfDocument? Source { get; } = source;

        /// <summary>Gets the token for this edit.</summary>
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        /// <summary>Gets the report receiving what the edit changed, or <see langword="null"/>.</summary>
        internal PdfPageEditReport? Report { get; init; }
    }
}
