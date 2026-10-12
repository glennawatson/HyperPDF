// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentContentInspection over the document's owned state.</summary>
internal static class HyperPdfDocumentContentInspection
{
    /// <summary>Checks the document as a whole: its form type and document scripts.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>What cannot be shown.</returns>
    internal static UnsupportedContent CheckDocument(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return UnsupportedContent.None;
        }

        var content = PdfDocumentContent.HasXfaForm(self.Document) ? UnsupportedContent.XfaForm : UnsupportedContent.None;
        return PdfDocumentContent.GetJavaScriptActionCount(self.Document) > 0 ? content | UnsupportedContent.JavaScript : content;
    }

    /// <summary>Checks one page's annotations and field scripts.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>What on the page cannot be shown or run.</returns>
    internal static UnsupportedContent CheckPage(HyperPdfDocument self, int pageIndex)
    {
        if (self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return UnsupportedContent.None;
        }

        var found = PdfDocumentContent.ScanAnnotations(self.Document, pageIndex);
        var content = UnsupportedContent.None;
        if ((found & PdfAnnotationContent.Multimedia) != 0)
        {
            content |= UnsupportedContent.Multimedia;
        }

        if ((found & PdfAnnotationContent.ThreeD) != 0)
        {
            content |= UnsupportedContent.ThreeD;
        }

        return PdfDocumentContent.HasAcroForm(self.Document) && HyperPdfContentInspection.HasUnknownScripts(self, pageIndex) ? content | UnsupportedContent.JavaScript : content;
    }
}
