// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Draws pages of one document on pages of another, in front of the content (an overlay) or behind it (an underlay).
/// Each drawn page is copied once as a form XObject (ISO 32000-2 8.10), however many pages it is drawn on, and the target
/// page's own content is kept as it was. A tagged document marks the drawing as an artifact.
/// </summary>
/// <remarks>
/// Content from the other document is copied as it is. If the target claims PDF/A, the pages drawn on it must already
/// meet the same level, for example by embedding their fonts; an optional content group or transparency the claim does
/// not allow is refused.
/// </remarks>
public static class PdfDocumentOverlays
{
    /// <summary>Lists the mappings that draw one page on every page of the target.</summary>
    /// <param name="targetPageCount">The pages in the target.</param>
    /// <param name="sourcePage">The zero based index of the page drawn on all of them.</param>
    /// <returns>The mappings, one per target page.</returns>
    public static PdfOverlayMapping[] MapEveryPage(int targetPageCount, int sourcePage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetPageCount);
        var mappings = new PdfOverlayMapping[targetPageCount];
        for (var page = 0; page < mappings.Length; page++)
        {
            mappings[page] = new(page, sourcePage);
        }

        return mappings;
    }

    /// <summary>Lists the mappings that draw the pages of the other document in turn, starting again after the last.</summary>
    /// <param name="targetPageCount">The pages in the target.</param>
    /// <param name="sourcePageCount">The pages in the document drawn; at least one.</param>
    /// <returns>The mappings, one per target page.</returns>
    public static PdfOverlayMapping[] MapCycling(int targetPageCount, int sourcePageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetPageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourcePageCount);
        var mappings = new PdfOverlayMapping[targetPageCount];
        for (var page = 0; page < mappings.Length; page++)
        {
            mappings[page] = new(page, page % sourcePageCount);
        }

        return mappings;
    }

    /// <summary>Draws pages of another document on pages of a document.</summary>
    /// <param name="target">The document drawn on.</param>
    /// <param name="source">The document whose pages are drawn; it must stay open until the target is saved.</param>
    /// <param name="mappings">Which source page goes on which target page. A target page may appear more than once; its drawings stack in order.</param>
    /// <param name="options">How the pages are drawn.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A mapping names a page that does not exist.</exception>
    /// <exception cref="InvalidOperationException">The target claims PDF/A and the options need optional content it does not allow.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OverlayPages(PdfDocument target, PdfDocument source, ReadOnlySpan<PdfOverlayMapping> mappings, PdfOverlayOptions options) =>
        OverlayPages(target, source, mappings, options, CancellationToken.None);

    /// <summary>Draws pages of another document on pages of a document, passing the token to the copy.</summary>
    /// <param name="target">The document drawn on.</param>
    /// <param name="source">The document whose pages are drawn; it must stay open until the target is saved.</param>
    /// <param name="mappings">Which source page goes on which target page.</param>
    /// <param name="options">How the pages are drawn.</param>
    /// <param name="cancellationToken">Cancels the copy; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A mapping names a page that does not exist.</exception>
    /// <exception cref="InvalidOperationException">The target claims PDF/A and the options need optional content it does not allow.</exception>
    public static void OverlayPages(PdfDocument target, PdfDocument source, ReadOnlySpan<PdfOverlayMapping> mappings, PdfOverlayOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(target, source, mappings);
        if (options.LayerName is not null)
        {
            PdfStampConformance.RequireLayersAllowed(target);
        }

        PdfDocumentEditing.RunEdit(
            target,
            "Overlay pages",
            PdfChangeKinds.PageChanges,
            new OverlayEdit(target, source, mappings.ToArray(), options, cancellationToken),
            Apply);
    }

    /// <summary>Draws pages of another document on pages of a document after loading the pages with real asynchronous I/O.</summary>
    /// <param name="target">The document drawn on.</param>
    /// <param name="source">The document whose pages are drawn; it must stay open until the target is saved.</param>
    /// <param name="mappings">Which source page goes on which target page.</param>
    /// <param name="options">How the pages are drawn.</param>
    /// <param name="cancellationToken">Cancels loading and the copy; the document is left unchanged.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask OverlayPagesAsync(PdfDocument target, PdfDocument source, ReadOnlyMemory<PdfOverlayMapping> mappings, PdfOverlayOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        for (var i = 0; i < mappings.Length; i++)
        {
            var mapping = mappings.Span[i];
            _ = await PdfDocumentPages.GetPageAsync(target, mapping.TargetPage, cancellationToken).ConfigureAwait(false);
            _ = await PdfDocumentPages.GetPageAsync(source, mapping.SourcePage, cancellationToken).ConfigureAwait(false);
        }

        OverlayPages(target, source, mappings.Span, options, cancellationToken);
    }

    /// <summary>Checks that every mapping names pages that exist.</summary>
    /// <param name="target">The target.</param>
    /// <param name="source">The source.</param>
    /// <param name="mappings">The mappings.</param>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist.</exception>
    private static void Validate(PdfDocument target, PdfDocument source, ReadOnlySpan<PdfOverlayMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)mapping.TargetPage, (uint)target.PageCount, nameof(mappings));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)mapping.SourcePage, (uint)source.PageCount, nameof(mappings));
        }
    }

    /// <summary>Draws the mapped pages inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The overlay.</param>
    private static void Apply(PdfEditTransaction transaction, OverlayEdit edit)
    {
        var (target, source, mappings, options, cancellationToken) = edit;
        var forms = new PdfFormImporter(new PdfStoreImporter(transaction, target.Objects, source.Objects, cancellationToken));
        var group = options.LayerName is { } name ? PdfOptionalContentWriter.EnsureGroup(target, transaction, name) : default;
        var marking = PdfStampConformance.GetMarking(target, false);
        var imported = new Dictionary<int, Objects.PdfObjectId>();
        var copies = new Dictionary<int, Objects.PdfDictionary>();
        foreach (var mapping in mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePage = PdfDocumentPages.GetPage(source, mapping.SourcePage);
            var page = PdfDocumentPages.GetPage(target, mapping.TargetPage);
            ref var form = ref CollectionsMarshal.GetValueRefOrAddDefault(imported, mapping.SourcePage, out var formKnown);
            if (!formKnown)
            {
                form = forms.ImportForm(sourcePage, sourcePage.CropBox, options.Annotations, true, group);
            }

            ref var copy = ref CollectionsMarshal.GetValueRefOrAddDefault(copies, mapping.TargetPage, out var copyKnown);
            if (!copyKnown)
            {
                copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            }

            var placement = PdfPageStamping.GetPlacement(sourcePage, page, options.Fit);
            PdfPageStamping.Stamp(target, transaction, page, copy!, new(form, placement, options.Placement == PdfOverlayPlacement.Underlay, default, marking));
        }

        PdfPageStamping.StoreAll(target, transaction, copies);
    }

    /// <summary>An overlay to apply.</summary>
    /// <param name="Target">The document drawn on.</param>
    /// <param name="Source">The document whose pages are drawn.</param>
    /// <param name="Mappings">Which source page goes on which target page.</param>
    /// <param name="Options">How the pages are drawn.</param>
    /// <param name="CancellationToken">Cancels the copy.</param>
    private sealed record OverlayEdit(PdfDocument Target, PdfDocument Source, PdfOverlayMapping[] Mappings, PdfOverlayOptions Options, CancellationToken CancellationToken);
}
