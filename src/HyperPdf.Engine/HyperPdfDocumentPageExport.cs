// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Printing;
using HyperPdfLibrary;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentPageExport over the document's owned state.</summary>
internal static class HyperPdfDocumentPageExport
{
    /// <summary>Writes a new PDF holding the given pages, in order, laid onto sheets.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">Zero based page indices.</param>
    /// <param name="layout">Pages per sheet, paper and whether annotations are included.</param>
    /// <param name="destination">The stream receiving the PDF.</param>
    /// <returns><see langword="true"/> when written.</returns>
    internal static bool ExportPages(HyperPdfDocument self, ReadOnlySpan<int> pages, in SheetLayout layout, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (self.IsDisposed || pages.IsEmpty || !HyperPdfPageExport.AllExist(self, pages))
        {
            return false;
        }

        try
        {
            var builder = new PdfDocumentBuilder();
            if (HyperPdfPageExport.Compose(self, builder, new(builder, self.Document), pages, layout) == 0)
            {
                return false;
            }

            builder.Save(destination);
            return true;
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException)
        {
            return false;
        }
    }
}
