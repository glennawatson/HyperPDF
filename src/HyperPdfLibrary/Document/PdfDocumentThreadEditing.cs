// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Adds and removes article threads (ISO 32000-2 12.4.3). A thread is named by its position in
/// <see cref="PdfDocumentCatalog.GetThreads"/>. Each edit joins the open transaction or makes its own, so it can be undone;
/// a document certified with a signature refuses the edit.
/// </summary>
public static class PdfDocumentThreadEditing
{
    /// <summary>What an edit of the threads is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the article threads";

    /// <summary>Adds a thread, with its beads in reading order, after the existing threads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="title">The title, or <see langword="null"/>.</param>
    /// <param name="beads">The beads: a page and a rectangle on it for each.</param>
    /// <returns>The thread's object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no beads, or a bead has no rectangle with an area.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bead's page does not exist.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static PdfObjectId AddThread(PdfDocument document, string? title, IReadOnlyList<PdfBead> beads)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(beads);
        var state = new AddState(document, title, beads);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Add article thread",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            static (edits, run) => run.Id = PdfThreadEditor.Add(edits, run.Document, run.Title, run.Beads));
        return state.Id;
    }

    /// <summary>Adds a thread.</summary>
    /// <param name="document">The document.</param>
    /// <param name="title">The title, or <see langword="null"/>.</param>
    /// <param name="beads">The beads.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The thread's object.</returns>
    public static async ValueTask<PdfObjectId> AddThreadAsync(PdfDocument document, string? title, IReadOnlyList<PdfBead> beads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beads);
        foreach (var bead in beads)
        {
            await PdfEditPrefetch.PageAsync(document, bead.PageIndex, cancellationToken).ConfigureAwait(false);
        }

        return AddThread(document, title, beads);
    }

    /// <summary>Removes a thread and its beads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The thread's position in <see cref="PdfDocumentCatalog.GetThreads"/>.</param>
    /// <returns><see langword="true"/> when the document had the thread.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static bool RemoveThread(PdfDocument document, int index)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new RemoveState(index);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Remove article thread",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            static (edits, run) => run.Removed = PdfThreadEditor.Remove(edits, run.Index));
        return state.Removed;
    }

    /// <summary>Removes a thread and its beads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The thread's position.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the document had the thread.</returns>
    public static async ValueTask<bool> RemoveThreadAsync(PdfDocument document, int index, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.Pages, cancellationToken).ConfigureAwait(false);
        return RemoveThread(document, index);
    }

    /// <summary>The arguments of an add, and the thread it made.</summary>
    /// <param name="document">The document.</param>
    /// <param name="title">The title.</param>
    /// <param name="beads">The beads.</param>
    [DebuggerDisplay("AddState: {Title}")]
    private sealed class AddState(PdfDocument document, string? title, IReadOnlyList<PdfBead> beads)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets the title.</summary>
        internal string? Title { get; } = title;

        /// <summary>Gets the beads.</summary>
        internal IReadOnlyList<PdfBead> Beads { get; } = beads;

        /// <summary>Gets or sets the thread's object.</summary>
        internal PdfObjectId Id { get; set; }
    }

    /// <summary>The arguments of a removal, and its result.</summary>
    /// <param name="index">The thread's position.</param>
    [DebuggerDisplay("RemoveState: {Index}")]
    private sealed class RemoveState(int index)
    {
        /// <summary>Gets the thread's position.</summary>
        internal int Index { get; } = index;

        /// <summary>Gets or sets a value indicating whether the thread was removed.</summary>
        internal bool Removed { get; set; }
    }
}
