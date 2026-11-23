// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Adds pages with nothing on them to a document.</summary>
public static class PdfBlankPages
{
    /// <summary>Adds a page with nothing on it before a page. The page has a media box and empty resources, and no content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The index the page takes, from 0 to the page count.</param>
    /// <param name="width">The width of the page in points.</param>
    /// <param name="height">The height of the page in points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the document, or a side is not a finite length above zero.</exception>
    public static void InsertPage(PdfDocument document, int index, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, document.PageCount);
        ThrowIfNotALength(width, nameof(width));
        ThrowIfNotALength(height, nameof(height));
        PdfDocumentEditing.RunEdit(document, "Insert blank page", PdfChangeKinds.PageChanges, new BlankPageEdit(document, index, width, height), Apply);
    }

    /// <summary>Rejects a side that is not a finite length above zero.</summary>
    /// <param name="side">The side in points.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <exception cref="ArgumentOutOfRangeException">The side is not a finite length above zero.</exception>
    private static void ThrowIfNotALength(float side, string name)
    {
        if (!float.IsFinite(side) || side <= 0)
        {
            throw new ArgumentOutOfRangeException(name, side, "A side of a page must be a finite length above zero.");
        }
    }

    /// <summary>Makes the page and puts it in the page tree.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">Where the page goes and how big it is.</param>
    private static void Apply(PdfEditTransaction transaction, BlankPageEdit edit)
    {
        var store = edit.Document.Objects;
        var page = new PdfDictionary(store);
        page.Set(KnownName.Type, PdfValue.FromName(KnownName.Page));
        page.Set(KnownName.MediaBox, PdfValue.FromArray(new PdfRectangle(0, 0, edit.Width, edit.Height).ToArray(store)));

        // A page needs its resources even when it draws nothing (ISO 32000-2 Table 31).
        page.Set(KnownName.Resources, PdfValue.FromDictionary(new(store)));
        PdfDocumentPageOperations.InsertNewPage(edit.Document, transaction, edit.Index, page);
    }
}
