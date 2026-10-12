// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentTextLayers over the document's owned state.</summary>
internal static class HyperPdfDocumentTextLayers
{
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
            var written = HyperPdfAnnotationTextLayer.AddTextLayer(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, words);
            if (written > 0)
            {
                // The page has new text: its characters, words and web links are read again.
                PdfDocumentText.GetTextPages(self.Document).Clear();
                HyperPdfEditing.Edited(self);
            }

            return written;
        }
    }
}
