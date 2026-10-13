// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Sets a page's transition and automatic advance time (ISO 32000-2 12.4.4), and its measurement viewports (12.9). Each
/// edit joins the open transaction or makes its own, so it can be undone; a document certified with a signature refuses it.
/// </summary>
public static class PdfDocumentTransitionEditing
{
    /// <summary>What an edit of a page's presentation is called when the document's certification refuses it.</summary>
    private const string TransitionDescription = "change the page transitions";

    /// <summary>What an edit of the measurement viewports is called when the document's certification refuses it.</summary>
    private const string MeasureDescription = "change the measurements";

    /// <summary>Sets or removes a page's transition and the time before the page advances by itself.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="transition">The transition, or <see langword="null"/> to remove the transition and the advance time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ArgumentException">A value is not one the standard allows for the style.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetTransition(PdfDocument document, int pageIndex, PdfPageTransition? transition)
    {
        ArgumentNullException.ThrowIfNull(document);
        var page = PdfDocumentPages.GetPage(document, pageIndex).Id;
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Change page transition",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            TransitionDescription,
            new Step<PdfPageTransition?>(page, transition),
            static (edits, run) => PdfTransitionEditor.Set(edits, run.Page, run.Value));
    }

    /// <summary>Sets or removes a page's transition.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="transition">The transition, or <see langword="null"/> to remove it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the transition is set.</returns>
    public static async ValueTask SetTransitionAsync(PdfDocument document, int pageIndex, PdfPageTransition? transition, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.PageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        SetTransition(document, pageIndex, transition);
    }

    /// <summary>Sets a page's measurement viewports; an empty list removes them. Only rectilinear measures can be written.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="viewports">The viewports.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ArgumentException">A viewport has no box, or a measure is not a complete rectilinear measure.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetViewports(PdfDocument document, int pageIndex, IReadOnlyList<PdfViewport> viewports)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(viewports);
        var page = PdfDocumentPages.GetPage(document, pageIndex).Id;
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Change measurements",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            MeasureDescription,
            new Step<IReadOnlyList<PdfViewport>>(page, viewports),
            static (edits, run) => PdfMeasureEditor.SetViewports(edits, run.Page, run.Value));
    }

    /// <summary>Sets a page's measurement viewports.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="viewports">The viewports.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the viewports are set.</returns>
    public static async ValueTask SetViewportsAsync(PdfDocument document, int pageIndex, IReadOnlyList<PdfViewport> viewports, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.PageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        SetViewports(document, pageIndex, viewports);
    }

    /// <summary>A page and one argument of an edit.</summary>
    /// <typeparam name="TValue">The type of the argument.</typeparam>
    /// <param name="Page">The page's object.</param>
    /// <param name="Value">The argument.</param>
    [DebuggerDisplay("Step: {Page}")]
    private readonly record struct Step<TValue>(HyperPdfLibrary.Objects.PdfObjectId Page, TValue Value);
}
