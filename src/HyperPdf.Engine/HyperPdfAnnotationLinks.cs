// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Adds link annotations, which send a reader to a web or mail address or to another page of the document when the area they cover is pressed.</summary>
internal static class HyperPdfAnnotationLinks
{
    /// <summary>The start of a target that names a page of the document, followed by the page's number counted from one.</summary>
    internal const string PagePrefix = "page:";

    /// <summary>Adds a link over an area of a page.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="area">The area the link covers, in page space.</param>
    /// <param name="target">An address such as <c>https://example.com</c> or <c>mailto:me@example.com</c>, or <c>page:</c> and a page number.</param>
    /// <returns>The new annotation's index, or -1 when the page or the target is not valid.</returns>
    internal static int AddLink(HyperPdfAnnotations annotationState, int pageIndex, PageRect area, string target)
    {
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page || area.Width <= 0 || area.Height <= 0)
            {
                return -1;
            }

            var store = annotationState.Store;
            var bounds = default(UserBounds);
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, new(area.Left, area.Top)));
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, new(area.Left + area.Width, area.Top + area.Height)));
            var link = PdfAnnotations.Create(store, KnownName.Link, bounds.ToRectangle(0));
            if (!TrySetTarget(annotationState, link, target))
            {
                return -1;
            }

            // A link is not drawn: its border is zero wide, so the page looks as it did.
            link.Set(KnownName.Border, PdfValue.FromArray(PdfArray.FromNumbers(store, [0F, 0F, 0F])));
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, link, null, string.Empty, []);
        }
    }

    /// <summary>Tells whether a target can be written as a link: an absolute address, or a page of the document.</summary>
    /// <param name="target">The target.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    internal static bool IsValid(string target, int pageCount) =>
        TryReadPage(target, out var page) ? page >= 0 && page < pageCount : Uri.TryCreate(target, UriKind.Absolute, out var address) && address.IsAbsoluteUri;

    /// <summary>Reads the page a target names.</summary>
    /// <param name="target">The target.</param>
    /// <param name="page">Receives the zero based page.</param>
    /// <returns><see langword="true"/> when the target names a page.</returns>
    private static bool TryReadPage(string target, out int page)
    {
        page = -1;
        if (!target.StartsWith(PagePrefix, StringComparison.Ordinal) || !int.TryParse(target.AsSpan(PagePrefix.Length), out var number) || number < 1)
        {
            return false;
        }

        page = number - 1;
        return true;
    }

    /// <summary>Writes what the link does into the annotation.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="link">The link annotation.</param>
    /// <param name="target">The target.</param>
    /// <returns><see langword="true"/> when the target could be written.</returns>
    private static bool TrySetTarget(HyperPdfAnnotations annotationState, PdfDictionary link, string target)
    {
        var store = annotationState.Store;
        if (TryReadPage(target, out var index))
        {
            if (index >= annotationState.Document.PageCount)
            {
                return false;
            }

            var destination = new PdfArray(store);
            destination.Add(PdfValue.FromReference(PdfDocumentPages.GetPage(annotationState.Document, index).Id));
            destination.Add(PdfValue.FromName(KnownName.Fit));
            link.Set(KnownName.Dest, PdfValue.FromArray(destination));
            return true;
        }

        if (!Uri.TryCreate(target, UriKind.Absolute, out var address))
        {
            return false;
        }

        var action = new PdfDictionary(store);
        action.Set(KnownName.S, PdfValue.FromName(KnownName.URI));
        PdfAnnotations.SetText(action, KnownName.URI, address.AbsoluteUri);
        link.Set(KnownName.A, PdfValue.FromDictionary(action));
        return true;
    }
}
