// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Writes article threads (ISO 32000-2 12.4.3): a thread dictionary in the catalog's <c>/Threads</c>, beads chained in a
/// ring through <c>/N</c> and <c>/V</c>, and each page's <c>/B</c> list of the beads on it.
/// </summary>
internal static class PdfThreadEditor
{
    /// <summary>The most beads followed when a thread is removed, which stops a loop that never returns to the first bead.</summary>
    private const int MaxBeads = 100_000;

    /// <summary>Gets the key of the catalog's thread list.</summary>
    private static ReadOnlySpan<byte> ThreadsKey => "Threads"u8;

    /// <summary>Adds a thread at the end of the catalog's thread list.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="title">The thread's title, or <see langword="null"/>.</param>
    /// <param name="beads">The beads, in reading order.</param>
    /// <returns>The thread's object.</returns>
    /// <exception cref="ArgumentException">There are no beads, a bead has no usable rectangle, or a page does not exist.</exception>
    internal static PdfObjectId Add(PdfObjectEdits edits, PdfDocument document, string? title, IReadOnlyList<PdfBead> beads)
    {
        ArgumentOutOfRangeException.ThrowIfZero(beads.Count);
        var pages = Validate(document, beads);
        var store = edits.Store;
        var thread = new PdfDictionary(store);
        thread.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("Thread"u8)));
        if (!string.IsNullOrEmpty(title))
        {
            var info = new PdfDictionary(store);
            info.Set(KnownName.Title, PdfValue.FromString(PdfText.Encode(title, store.Version)));
            thread.Set(KnownName.I, PdfValue.FromDictionary(info));
        }

        var threadId = edits.Add(thread);
        var ids = new PdfObjectId[beads.Count];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = edits.Add(new(store));
        }

        thread.Set(KnownName.F, PdfValue.FromReference(ids[0]));
        for (var i = 0; i < ids.Length; i++)
        {
            WriteBead(edits, edits.Peek(ids[i])!, threadId, ids, i, pages[i], beads[i]);
            AddToPage(edits, pages[i], ids[i]);
        }

        var catalog = edits.Catalog();
        var key = store.Names.Intern(ThreadsKey);
        PdfEditDictionaries.ChildArray(edits, catalog, key).Add(PdfValue.FromReference(threadId));
        return threadId;
    }

    /// <summary>Removes the thread at a position in the catalog's thread list, with its beads.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="index">The thread's position among the threads <see cref="PdfDocumentCatalog.GetThreads"/> reports.</param>
    /// <returns><see langword="true"/> when the document had the thread.</returns>
    internal static bool Remove(PdfObjectEdits edits, int index)
    {
        var key = edits.Store.Names.Intern(ThreadsKey);
        var list = edits.CatalogForRead.GetArray(key);
        var at = FindEntry(list, index);
        if (at < 0)
        {
            return false;
        }

        var reference = list!.GetRaw(at);
        DeleteBeads(edits, edits.Peek(reference.AsReference()));
        edits.Delete(reference.AsReference());
        var catalog = edits.Catalog();
        var copy = PdfEditDictionaries.ChildArray(edits, catalog, key);
        copy.RemoveAt(at);
        if (copy.Count == 0)
        {
            _ = catalog.Remove(key);
        }

        return true;
    }

    /// <summary>Checks the beads and finds the pages they sit on.</summary>
    /// <param name="document">The document.</param>
    /// <param name="beads">The beads.</param>
    /// <returns>The page of each bead.</returns>
    /// <exception cref="ArgumentException">A bead has no rectangle, or an empty one.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    private static PdfPage[] Validate(PdfDocument document, IReadOnlyList<PdfBead> beads)
    {
        var pages = new PdfPage[beads.Count];
        for (var i = 0; i < pages.Length; i++)
        {
            if (beads[i].Bounds is not { IsEmpty: false })
            {
                throw new ArgumentException("Every bead needs a rectangle on its page.", nameof(beads));
            }

            pages[i] = PdfDocumentPages.GetPage(document, beads[i].PageIndex);
        }

        return pages;
    }

    /// <summary>Fills in a bead dictionary (Table 163).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="bead">The dictionary.</param>
    /// <param name="threadId">The thread.</param>
    /// <param name="ids">The objects of all the beads.</param>
    /// <param name="position">The bead's position.</param>
    /// <param name="page">The page the bead sits on.</param>
    /// <param name="spec">The bead.</param>
    private static void WriteBead(PdfObjectEdits edits, PdfDictionary bead, PdfObjectId threadId, PdfObjectId[] ids, int position, PdfPage page, PdfBead spec)
    {
        bead.Set(KnownName.Type, PdfValue.FromName(edits.Store.Names.Intern("Bead"u8)));
        if (position == 0)
        {
            bead.Set(KnownName.T, PdfValue.FromReference(threadId));
        }

        bead.Set(KnownName.N, PdfValue.FromReference(ids[(position + 1) % ids.Length]));
        bead.Set(KnownName.V, PdfValue.FromReference(ids[(position + ids.Length - 1) % ids.Length]));
        bead.Set(KnownName.P, PdfValue.FromReference(page.Id));
        bead.Set(KnownName.R, PdfValue.FromArray(spec.Bounds!.Value.ToArray(edits.Store)));
    }

    /// <summary>Lists a bead in its page's <c>/B</c> array.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="page">The page.</param>
    /// <param name="bead">The bead.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddToPage(PdfObjectEdits edits, PdfPage page, PdfObjectId bead) =>
        PdfEditDictionaries.ChildArray(edits, edits.Edit(page.Id)!, KnownName.B).Add(PdfValue.FromReference(bead));

    /// <summary>Deletes every bead of a thread and takes it off the pages' lists.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="thread">The thread dictionary, or <see langword="null"/>.</param>
    private static void DeleteBeads(PdfObjectEdits edits, PdfDictionary? thread)
    {
        var first = thread?.GetRaw(KnownName.F).AsReference() ?? default;
        var current = first;
        for (var count = 0; current.IsValid && count < MaxBeads; count++)
        {
            var next = DeleteBead(edits, current);
            current = next.IsValid && next.Number != first.Number ? next : default;
        }
    }

    /// <summary>Deletes one bead and takes it off its page's list.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="id">The bead.</param>
    /// <returns>The next bead, or an invalid id.</returns>
    private static PdfObjectId DeleteBead(PdfObjectEdits edits, PdfObjectId id)
    {
        var bead = edits.Peek(id);
        var next = bead?.GetRaw(KnownName.N).AsReference() ?? default;
        if (bead?.GetRaw(KnownName.P).AsReference() is { IsValid: true } pageId && edits.Edit(pageId) is { } page)
        {
            RemoveFromPage(edits, page, id.Number);
        }

        edits.Delete(id);
        return next;
    }

    /// <summary>Takes a bead off a page's list, removing the list when it empties.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="page">The editable page.</param>
    /// <param name="number">The bead's object number.</param>
    private static void RemoveFromPage(PdfObjectEdits edits, PdfDictionary page, int number)
    {
        if (!PdfEditDictionaries.Contains(page.GetArray(KnownName.B), number))
        {
            return;
        }

        var list = PdfEditDictionaries.ChildArray(edits, page, KnownName.B);
        _ = PdfEditDictionaries.RemoveAll(list, number);
        if (list.Count == 0)
        {
            _ = page.Remove(KnownName.B);
        }
    }

    /// <summary>Finds the position of the nth entry that is a dictionary.</summary>
    /// <param name="list">The thread list, or <see langword="null"/>.</param>
    /// <param name="index">The count of dictionary entries to pass.</param>
    /// <returns>The array index, or -1.</returns>
    private static int FindEntry(PdfArray? list, int index)
    {
        var seen = 0;
        for (var i = 0; list is not null && index >= 0 && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is null)
            {
                continue;
            }

            if (seen == index)
            {
                return i;
            }

            seen++;
        }

        return -1;
    }
}
