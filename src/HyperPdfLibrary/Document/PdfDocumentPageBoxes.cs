// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Sets and clears the page boundaries of ISO 32000-2 14.11.2 on an open document.</summary>
public static class PdfDocumentPageBoxes
{
    /// <summary>The smallest width or height a box may have when the document claims PDF/A (ISO 19005-1 6.1.13, ISO 19005-2 6.1.13).</summary>
    private const float PdfAMinExtent = 3;

    /// <summary>The largest width or height a box may have when the document claims PDF/A.</summary>
    private const float PdfAMaxExtent = 14_400;

    /// <summary>
    /// Sets one of a page's boundaries. The box is written as four numbers, lower left then upper right, on the page
    /// itself, so it replaces an inherited value. A crop, bleed, trim or art box is clipped to the media box, as readers
    /// treat it (14.11.2.1); the box actually written is returned.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">Which boundary.</param>
    /// <param name="box">The box in default user space units; its corners may be in any order.</param>
    /// <returns>The box written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page, or <paramref name="kind"/> is not defined.</exception>
    /// <exception cref="ArgumentException">The box is empty, not finite, too large, outside the media box, or too small or large for a document that claims PDF/A.</exception>
    public static PdfRectangle SetBox(PdfDocument document, int pageIndex, PdfPageBox kind, in PdfRectangle box)
    {
        ArgumentNullException.ThrowIfNull(document);
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var normal = PdfRectangle.FromCorners(box.Left, box.Bottom, box.Right, box.Top);
        var written = kind == PdfPageBox.Media ? normal : normal.Intersect(page.MediaBox);
        Validate(document, written);
        PdfDocumentEditing.RunEdit(
            document,
            "Set page box",
            PdfChangeKinds.PageChanges,
            new BoxEdit(document, page, kind, written),
            Store);
        return written;
    }

    /// <summary>Removes a crop, bleed, trim or art box from a page, so it takes its default (or, for the crop box, the inherited value).</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">Which boundary.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="ArgumentException">The page's media box cannot be removed.</exception>
    public static void ClearBox(PdfDocument document, int pageIndex, PdfPageBox kind)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (kind == PdfPageBox.Media)
        {
            throw new ArgumentException("The media box is required and cannot be removed.", nameof(kind));
        }

        var page = PdfDocumentPages.GetPage(document, pageIndex);
        PdfDocumentEditing.RunEdit(
            document,
            "Clear page box",
            PdfChangeKinds.PageChanges,
            new BoxEdit(document, page, kind, default),
            Store);
    }

    /// <summary>Sets one of a page's boundaries after loading the page's resources with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">Which boundary.</param>
    /// <param name="box">The box in default user space units.</param>
    /// <param name="cancellationToken">Cancels loading and editing.</param>
    /// <returns>The box written.</returns>
    public static async ValueTask<PdfRectangle> SetBoxAsync(PdfDocument document, int pageIndex, PdfPageBox kind, PdfRectangle box, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = await PdfDocumentPages.GetPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return SetBox(document, pageIndex, kind, box);
    }

    /// <summary>Removes a boundary from a page after loading the page's resources with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">Which boundary.</param>
    /// <param name="cancellationToken">Cancels loading and editing.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask ClearBoxAsync(PdfDocument document, int pageIndex, PdfPageBox kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = await PdfDocumentPages.GetPageAsync(document, pageIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ClearBox(document, pageIndex, kind);
    }

    /// <summary>Gets the key a boundary is stored under.</summary>
    /// <param name="kind">Which boundary.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not defined.</exception>
    internal static KnownName GetKey(PdfPageBox kind) => kind switch
    {
        PdfPageBox.Media => KnownName.MediaBox,
        PdfPageBox.Crop => KnownName.CropBox,
        PdfPageBox.Bleed => KnownName.BleedBox,
        PdfPageBox.Trim => KnownName.TrimBox,
        PdfPageBox.Art => KnownName.ArtBox,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The page box is not defined."),
    };

    /// <summary>Checks that a box can be written.</summary>
    /// <param name="document">The document.</param>
    /// <param name="box">The box to write.</param>
    /// <exception cref="ArgumentException">The box cannot be written.</exception>
    private static void Validate(PdfDocument document, in PdfRectangle box)
    {
        if (!PdfPage.IsUsableBox(box))
        {
            throw new ArgumentException("The box must be finite, have an area, lie inside the media box and not be larger than a million units.", nameof(box));
        }

        var inRange = box.Width is >= PdfAMinExtent and <= PdfAMaxExtent && box.Height is >= PdfAMinExtent and <= PdfAMaxExtent;
        if (!inRange && PdfDocumentOutputIntentRendering.ClaimsPdfA(document))
        {
            throw new ArgumentException("The document claims PDF/A, which needs each page box to be between 3 and 14,400 units wide and high.", nameof(box));
        }
    }

    /// <summary>Writes a box into a copy of the page dictionary.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The change.</param>
    private static void Store(PdfEditTransaction transaction, BoxEdit edit)
    {
        List<PdfPageSlot>? direct = null;
        var copy = PdfDocumentPageOperations.CloneForEdit(transaction, edit.Page);
        var key = GetKey(edit.Kind);
        if (edit.Box.IsEmpty)
        {
            _ = copy.Remove(key);
        }
        else
        {
            copy.Set(key, PdfValue.FromArray(edit.Box.ToArray(null)));
        }

        PdfDocumentPageOperations.StoreEdited(edit.Document, transaction, edit.Page, copy, ref direct);
        PdfDocumentPageOperations.FinishEdited(edit.Document, transaction, direct);
    }

    /// <summary>A change to one page box.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Page">The page.</param>
    /// <param name="Kind">Which boundary.</param>
    /// <param name="Box">The box to write, or empty to remove it.</param>
    private sealed record BoxEdit(PdfDocument Document, PdfPage Page, PdfPageBox Kind, PdfRectangle Box);
}
