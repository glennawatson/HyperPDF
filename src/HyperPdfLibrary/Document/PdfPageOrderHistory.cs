// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Document;

/// <summary>Keeps the pages before and after each edit in the history, and hands them back on undo and redo.</summary>
internal static class PdfPageOrderHistory
{
    /// <summary>
    /// Keeps the pages before and after a committed edit, or hands the kept pages to the next invalidation when the edit is
    /// undone or redone. Called before the invalidation, without holding the store's lock.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="record">The history record committed, undone or redone.</param>
    /// <param name="undone">Whether the record was undone.</param>
    /// <param name="start">The pages as the committed transaction started, or <see langword="null"/> when it did not start through the document.</param>
    internal static void KeepOrRestore(PdfDocument document, PdfEditRecord record, bool undone, PdfPagesAtStart? start)
    {
        ref var state = ref document.State;
        if (undone)
        {
            Restore(ref state, record, true);
            return;
        }

        // A commit of an edit that changed the page tree left its pages; a redo leaves none, because the edit did not run again.
        var after = Volatile.Read(ref state.NextPageSet);
        if (start is null && after is null)
        {
            Restore(ref state, record, false);
            return;
        }

        var before = start is null ? Volatile.Read(ref state.PageSet) : start.Pages;
        if (before is not null)
        {
            Orders(ref state).AddOrUpdate(record, new(before.Unmade(), after?.Unmade()));
        }
    }

    /// <summary>Hands the pages kept for an edit to the next invalidation.</summary>
    /// <param name="state">The document's state.</param>
    /// <param name="record">The history record undone or redone.</param>
    /// <param name="undone">Whether it was undone.</param>
    private static void Restore(ref PdfDocumentState state, PdfEditRecord record, bool undone)
    {
        if (Volatile.Read(ref state.PageOrders) is not { } orders || !orders.TryGetValue(record, out var kept))
        {
            return;
        }

        // The history puts back exactly the objects these pages were read from: an undo happens only once every later edit
        // is undone, and a redo only straight after the undo. A fresh copy is handed out, so the kept one never makes pages.
        if ((undone ? kept.Before : kept.After) is { } pages)
        {
            Volatile.Write(ref state.NextPageSet, pages.Unmade());
        }
    }

    /// <summary>Gets the table of kept pages, making it once.</summary>
    /// <param name="state">The document's state.</param>
    /// <returns>The table; an entry goes when the history drops its record.</returns>
    private static ConditionalWeakTable<PdfEditRecord, PdfPageOrders> Orders(ref PdfDocumentState state)
    {
        if (Volatile.Read(ref state.PageOrders) is { } existing)
        {
            return existing;
        }

        ConditionalWeakTable<PdfEditRecord, PdfPageOrders> created = new();
        return Interlocked.CompareExchange(ref state.PageOrders, created, null) ?? created;
    }
}
