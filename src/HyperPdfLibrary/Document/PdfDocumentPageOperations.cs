// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Adds, removes, and reorders document pages.</summary>
public static class PdfDocumentPageOperations
{
    /// <summary>Sets a page's rotation, normalised to 0, 90, 180 or 270 degrees clockwise as PDFium does.</summary>
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
    public static void DeletePages(PdfDocument document, ReadOnlySpan<int> pages)
    {
        PdfDocumentPageOperations.ValidatePages(document, pages);
        PdfDocumentEditing.RunEdit(
document,
"Delete pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, pages, 0, null),
static (transaction, edit) => PdfDocumentPageOperations.DeleteCore(edit.Document, transaction, edit.Pages));
    }

    /// <summary>Moves pages, in the order given, so the first lands at <paramref name="destination"/> in the result, as <c>FPDF_MovePages</c> does.</summary>
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
        _ = PdfDocumentPageOperations.UniquePages(order, true);
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
    public static void InsertPages(PdfDocument document, int index, PdfDocument source, ReadOnlySpan<int> sourcePages)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, document.PageCount);
        PdfDocumentPageOperations.ValidatePages(source, sourcePages);
        PdfDocumentEditing.RunEdit(
document,
"Insert pages",
PdfChangeKinds.PageChanges,
new PageEdit(document, sourcePages, index, source),
static (transaction, edit) => PdfDocumentPageOperations.InsertCore(edit.Document, transaction, edit.Value, edit.Source!, edit.Pages));
    }

    /// <summary>Writes some pages as a new document, keeping their content, resources, boxes, rotation and annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based page indexes, in order.</param>
    /// <returns>The new document's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is not a page.</exception>
    public static byte[] ExtractPages(PdfDocument document, ReadOnlySpan<int> pages)
    {
        PdfDocumentPageOperations.ValidatePages(document, pages);
        var builder = new PdfDocumentBuilder();
        var importer = new PdfPageImporter(builder, document, true);
        var selected = new PdfPage[pages.Length];
        for (var i = 0; i < selected.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            selected[i] = PdfDocumentPages.GetPage(document, pages[i]);
        }

        importer.ImportPages(selected);
        return builder.ToArray();
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

    /// <summary>Determines whether any inserted page brings a label.</summary>
    /// <param name="slots">The pages in their new order.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasInsertedLabels(List<PdfPageSlot> slots)
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
        var pages = PdfDocumentPages.GetPageSet(document).Pages;
        var slots = new List<PdfPageSlot>(pages.Length);
        foreach (var page in pages)
        {
            slots.Add(new(page.Id.IsValid ? PdfValue.FromReference(page.Id) : PdfValue.FromDictionary(page.Dictionary), page.Index));
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
            var copy = page.Id.IsValid ? transaction.CloneDictionary(page.Id) ?? page.Dictionary.Clone() : page.Dictionary.Clone();
            copy.Set(KnownName.Rotate, PdfValue.FromInteger(rotation));
            if (page.Id.IsValid)
            {
                transaction.Replace(page.Id, PdfValue.FromDictionary(copy));
                continue;
            }

            // A direct page lives in its parent's /Kids, so the tree is written again around it.
            direct ??= PdfDocumentPageOperations.CurrentSlots(document);
            direct[index] = new(PdfValue.FromDictionary(copy), index);
        }

        if (direct is not null)
        {
            PdfDocumentPageOperations.WritePages(document, transaction, direct);
        }
    }

    /// <summary>Deletes pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes.</param>
    /// <exception cref="ArgumentException">Every page would be deleted.</exception>
    private static void DeleteCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages)
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
        PdfDocumentPageOperations.WritePages(document, transaction, kept);
    }

    /// <summary>Moves pages so the first lands at a destination.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="pages">The page indexes, each once.</param>
    /// <param name="destination">The first moved page's index afterwards.</param>
    private static void MoveCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> pages, int destination)
    {
        var moved = PdfDocumentPageOperations.UniquePages(pages, true);
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
        PdfDocumentPageOperations.WritePages(document, transaction, result);
    }

    /// <summary>Puts every page in a new order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="order">The current index of the page at each new position.</param>
    private static void ReorderCore(PdfDocument document, PdfEditTransaction transaction, ReadOnlySpan<int> order)
    {
        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        var result = new List<PdfPageSlot>(order.Length);
        foreach (var index in order)
        {
            result.Add(slots[index]);
        }

        PdfDocumentPageOperations.WritePages(document, transaction, result);
    }

    /// <summary>Copies pages from a document and places them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The index of the first copy.</param>
    /// <param name="source">The document copied from.</param>
    /// <param name="sourcePages">The source page indexes.</param>
    private static void InsertCore(PdfDocument document, PdfEditTransaction transaction, int index, PdfDocument source, ReadOnlySpan<int> sourcePages)
    {
        var importer = new PdfStoreImporter(transaction, document.Objects, source.Objects);
        var carrier = new PdfDocumentCarrier(importer);
        var ids = new PdfObjectId[sourcePages.Length];
        var pages = new PdfPage[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            ids[i] = transaction.Add(default);
            pages[i] = PdfDocumentPages.GetPage(source, sourcePages[i]);

            // Links and annotations that name a copied page now name its copy.
            carrier.AddPage(pages[i].Id, ids[i]);
        }

        var annotations = new PdfArray?[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            annotations[i] = carrier.PrepareAnnotations(pages[i], PdfAnnotationFilter.All);
        }

        var structParents = PdfDocumentPageOperations.ImportStructure(document, transaction, importer, source, sourcePages, ids);
        var labels = PdfDocumentLabels.CollectSourceLabels(source, pages) is { } sourceLabels ? PdfPageLabelWriter.Retarget(sourceLabels, importer) : null;
        var inserted = new PdfPageSlot[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            var copy = PdfDocumentPageOperations.ImportPage(document, importer, pages[i], annotations[i]);
            if (structParents[i] >= 0)
            {
                copy.Set(KnownName.StructParents, PdfValue.FromInteger(structParents[i]));
            }

            transaction.Replace(ids[i], PdfValue.FromDictionary(copy));
            inserted[i] = new(PdfValue.FromReference(ids[i]), -1) { Label = labels?[i] };
        }

        carrier.Finish();
        var slots = PdfDocumentPageOperations.CurrentSlots(document);
        slots.InsertRange(index, inserted);
        PdfDocumentPageOperations.WritePages(document, transaction, slots);
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
    /// page tree carries can be inherited by mistake.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="importer">The importer.</param>
    /// <param name="page">The source page.</param>
    /// <param name="annotations">The annotations the carrier prepared, or <see langword="null"/> when the page has none.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary ImportPage(PdfDocument document, PdfStoreImporter importer, PdfPage page, PdfArray? annotations)
    {
        var copy = new PdfDictionary(document.Objects, page.Dictionary.Count + 1);
        var source = page.Dictionary;
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (key.Is(KnownName.Type) || key.Is(KnownName.Parent) || key.Is(KnownName.StructParents))
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

    /// <summary>Writes the page tree and, when the document has labels, the labels that follow the pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="slots">The pages in their new order.</param>
    private static void WritePages(PdfDocument document, PdfEditTransaction transaction, List<PdfPageSlot> slots)
    {
        var labels = document.Catalog.GetDictionary(KnownName.PageLabels) is null && !PdfDocumentPageOperations.HasInsertedLabels(slots) ? null : PdfDocumentPageOperations.CollectLabels(
document,
slots);
        PdfPageTreeWriter.Write(transaction, document.Objects, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(slots));
        if (labels is not null)
        {
            PdfPageLabelWriter.Write(transaction, document.Objects, labels);
        }
    }

    /// <summary>Gets the label each page in a new order keeps; an inserted page continues the label before it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="slots">The pages in their new order.</param>
    /// <returns>The labels.</returns>
    private static PdfPageLabel[] CollectLabels(PdfDocument document, List<PdfPageSlot> slots)
    {
        var ranges = PdfDocumentLabels.GetLabelRanges(document);

        // Pages before the first range are labelled with their decimal page number; one shared style keeps them in runs.
        var decimalStyle = new PdfDictionary(document.Objects);
        decimalStyle.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        var labels = new PdfPageLabel[slots.Count];
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

    /// <summary>The arguments of a page operation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The page indexes.</param>
    /// <param name="value">The operation's number: a rotation, a destination or an insertion index.</param>
    /// <param name="source">The document pages are copied from, for an insertion.</param>
    private readonly ref struct PageEdit(PdfDocument document, ReadOnlySpan<int> pages, int value, PdfDocument? source)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the page indexes.</summary>
        internal ReadOnlySpan<int> Pages { get; } = pages;

        /// <summary>Gets the operation's number.</summary>
        internal int Value { get; } = value;

        /// <summary>Gets the source document.</summary>
        internal PdfDocument? Source { get; } = source;
    }
}
