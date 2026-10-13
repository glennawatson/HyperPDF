// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A PDFium page loaded for the length of one locked reference call.</summary>
/// <param name="handle">The <c>FPDF_PAGE</c>.</param>
internal readonly ref struct PdfiumPage(nint handle)
{
    /// <summary>Gets the <c>FPDF_PAGE</c>.</summary>
    internal nint Handle { get; } = handle;

    /// <summary>Loads a page. Call while holding the PDFium lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The page; dispose it before releasing the lock.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page.</exception>
    internal static PdfiumPage Load(PdfiumReferenceDocument document, int pageIndex)
    {
        var page = PdfiumReferenceLibrary.NativeMethods.FPDF_LoadPage(document.Handle, pageIndex);
        return page == 0 ? throw new InvalidDataException($"PDFium could not load page {pageIndex}.") : new(page);
    }

    /// <summary>Closes the page.</summary>
    internal void Dispose() => PdfiumReferenceLibrary.NativeMethods.FPDF_ClosePage(Handle);
}
