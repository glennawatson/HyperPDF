// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and edits page links.</summary>
public static class PdfDocumentLinks
{
    /// <summary>Gets a page's link annotations, loading the page first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The links that lead somewhere.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<IReadOnlyList<PdfLink>> GetLinksAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var load = PdfPrefetcher.PrefetchAsync(document.Objects, PdfDocumentPages.GetPage(document, pageIndex).Dictionary, PdfPrefetchKind.Page, cancellationToken);
        return load.IsCompletedSuccessfully ? PdfDocumentLinks.LinksReady(document, pageIndex, cancellationToken) : PdfDocumentLinks.LinksAfterAsync(document, load, pageIndex, cancellationToken);
    }

    /// <summary>Gets a page's link annotations. A link's action wins over its /Dest, because the standard does not permit both (ISO 32000-2 12.5.6.5).</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The links that lead somewhere.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfLink> GetLinks(PdfDocument document, int pageIndex) =>
        ReadLinksPrepared(document, pageIndex, CancellationToken.None);

    /// <summary>Gets a page's links while checking cancellation during first extraction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="cancellationToken">Cancels extraction.</param>
    /// <returns>The links that lead somewhere.</returns>
    private static PdfLink[] ReadLinksPrepared(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read the page set once: the pages and the link cache come from the same snapshot, so a concurrent edit cannot mix them.
        var set = PdfDocumentPages.GetPageSet(document);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)pageIndex, (uint)set.Count, nameof(pageIndex));
        var links = Volatile.Read(ref set.LinkSlot(pageIndex));
        if (links is not null)
        {
            return links;
        }

        links = PdfDocumentLinks.ReadLinks(document, set.GetPage(pageIndex), cancellationToken);
        Volatile.Write(ref set.LinkSlot(pageIndex), links);
        return links;
    }

    /// <summary>Reads a page's links once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed links, or a faulted task.</returns>
    private static ValueTask<IReadOnlyList<PdfLink>> LinksReady(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            return new(PdfDocumentLinks.ReadLinksPrepared(document, pageIndex, cancellationToken));
        }
        catch (Exception ex) when (PdfDocumentAsyncTasks.IsTaskFault(ex))
        {
            return ValueTask.FromException<IReadOnlyList<PdfLink>>(ex);
        }
    }

    /// <summary>Waits for a load, then reads a page's links.</summary>
    /// <param name="document">The document.</param>
    /// <param name="load">The load.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The links.</returns>
    private static async ValueTask<IReadOnlyList<PdfLink>> LinksAfterAsync(PdfDocument document, ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return PdfDocumentLinks.ReadLinksPrepared(document, pageIndex, cancellationToken);
    }

    /// <summary>Reads the link annotations of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The links.</returns>
    private static PdfLink[] ReadLinks(PdfDocument document, PdfPage page, CancellationToken cancellationToken)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        if (annotations is null)
        {
            return [];
        }

        var links = new List<PdfLink>();
        for (var i = 0; i < annotations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var annotation = annotations.GetDictionary(i);
            if (annotation is null || !annotation.IsName(KnownName.Subtype, KnownName.Link) || !annotation.TryGetRectangle(KnownName.Rect, out var bounds))
            {
                continue;
            }

            var action = PdfDocumentLinks.ReadLinkAction(document, annotation);
            if (action.Value is not null)
            {
                links.Add(new(bounds, action));
            }
        }

        return [.. links];
    }

    /// <summary>Reads where a link leads: its action, or else its /Dest.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annotation">The link annotation.</param>
    /// <returns>The action.</returns>
    private static PdfAction ReadLinkAction(PdfDocument document, PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.A) is { } dictionary)
        {
            var action = PdfDocumentNavigation.ReadAction(document, dictionary);
            if (action.IsNavigation)
            {
                return action;
            }

            // A non-navigation action never hides the link's /Dest; an unsupported one is not a link at all.
            if (PdfDocumentNavigation.ResolveDestination(document, annotation.Get(KnownName.Dest)) is { } fallback)
            {
                return new GoToAction(fallback);
            }

            return action.Value is UnsupportedAction ? default : action;
        }

        return PdfDocumentNavigation.ResolveDestination(document, annotation.Get(KnownName.Dest)) is { } destination ? new GoToAction(destination) : default(PdfAction);
    }
}
