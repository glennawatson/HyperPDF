// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentTextLayers over the document's owned state.</summary>
internal static class HyperPdfDocumentTextLayers
{
    /// <summary>The bits the generation takes in a page token, below the object number.</summary>
    private const int TokenNumberShift = 32;

    /// <summary>Writes recognised words onto a page as invisible text placed over where they appear.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written.</returns>
    internal static int AddTextLayer(HyperPdfDocument self, int pageIndex, ReadOnlySpan<OcrWord> words)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return AddLocked(self, pageIndex, words);
        }
    }

    /// <summary>Writes recognised words onto the page a token names, wherever the page is now.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageToken">The token from <see cref="GetPageToken"/>.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written; 0 when the page is no longer in the document.</returns>
    internal static int AddTextLayerToPage(HyperPdfDocument self, long pageToken, ReadOnlySpan<OcrWord> words)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            var pageIndex = FindPage(self, pageToken);
            return pageIndex < 0 ? 0 : AddLocked(self, pageIndex, words);
        }
    }

    /// <summary>Gets a token naming the page at an index that stays with the page when others are inserted, deleted or moved.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The token, or 0 when there is no such page.</returns>
    internal static long GetPageToken(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount ? 0 : ToToken(PdfDocumentPages.GetPage(self.Document, pageIndex).Id);
    }

    /// <summary>Writes words onto a page while the edit gate and the page write lock are held.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written.</returns>
    private static int AddLocked(HyperPdfDocument self, int pageIndex, ReadOnlySpan<OcrWord> words)
    {
        var written = HyperPdfAnnotationTextLayer.AddTextLayer(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, words);
        if (written > 0)
        {
            // The page has new text: its characters, words and web links are read again.
            PdfDocumentText.GetTextPages(self.Document).Clear();
            HyperPdfEditing.Edited(self, PdfCacheScopes.PageContent);
        }

        return written;
    }

    /// <summary>Finds where the page a token names is now.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageToken">The token.</param>
    /// <returns>The zero based page index, or -1 when the page is gone.</returns>
    private static int FindPage(HyperPdfDocument self, long pageToken)
    {
        if (self.IsDisposed || pageToken == 0)
        {
            return -1;
        }

        for (var index = 0; index < self.PageCount; index++)
        {
            if (ToToken(PdfDocumentPages.GetPage(self.Document, index).Id) == pageToken)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Makes a token from a page's object, so it follows the page and not its place.</summary>
    /// <param name="id">The page's object.</param>
    /// <returns>The token; 0 for a page with no object.</returns>
    private static long ToToken(PdfObjectId id) => id.IsValid ? ((long)id.Number << TokenNumberShift) | (uint)id.Generation : 0;
}
