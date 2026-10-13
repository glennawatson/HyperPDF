// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and edits page content.</summary>
public static class PdfDocumentPageContent
{
    /// <summary>Reads a page's content into objects that can be inspected, deleted, moved and recoloured.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPageContent GetPageContent(PdfDocument document, int pageIndex) => PdfPageContentReader.Read(document, pageIndex);

    /// <summary>Drops what the document cached about page content and tells renderers to redraw, after an edit changed a page's content.</summary>
    /// <param name="document">The document.</param>
    internal static void InvalidatePageContent(PdfDocument document)
    {
        Volatile.Write(ref document.State.TextPages, null);

        // A layer set that was dropped is read again at the version floor, which is already past every version handed out,
        // so there is nothing to invalidate and no need to read it just to do so.
        Volatile.Read(ref document.State.OptionalContent)?.InvalidatePages();
    }

    /// <summary>Writes a page's changed content in a transaction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">The page content.</param>
    internal static void ApplyPageContent(PdfDocument document, PdfPageContent content)
    {
        PdfDocumentEditing.RunEdit(document, "Edit page content", PdfChangeKinds.Other, PdfCacheScopes.PageContent, content, static (_, state) => PdfPageContentApplication.CommitToPage(state));
        _ = Interlocked.Increment(ref document.State.PageContentEdits);
        PdfDocumentPageContent.InvalidatePageContent(document);
    }

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RunInTransaction<TState>(PdfDocument document, string label, PdfChangeKinds kinds, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct => PdfDocumentEditing.RunEdit(document, label, kinds, state, edit);

    /// <summary>Redraws pages after an undo or redo when page content was ever edited, since the history can bring back old content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="changed">Whether the undo or redo changed the document.</param>
    /// <returns>The same value.</returns>
    internal static bool AfterHistory(PdfDocument document, bool changed)
    {
        if (changed && Volatile.Read(ref document.State.PageContentEdits) > 0)
        {
            PdfDocumentPageContent.InvalidatePageContent(document);
        }

        return changed;
    }
}
