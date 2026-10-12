// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Implements Repairs over the document's owned state.</summary>
internal static class HyperPdfRepairs
{
    /// <summary>Says in plain words what a repair was.</summary>
    /// <param name="code">The repair.</param>
    /// <returns>The description.</returns>
    internal static string Describe(PdfDiagnosticCode code) => code switch
    {
        PdfDiagnosticCode.XrefRebuilt => "The file's index of its parts was missing or wrong, so it was rebuilt.",
        PdfDiagnosticCode.HeaderOffset => "There was extra data before the start of the file, which was skipped.",
        PdfDiagnosticCode.BrokenObject => "A part of the file was not where the index said, so it was found by searching.",
        PdfDiagnosticCode.BadStreamLength => "A block of data had the wrong length, so its end was found by searching.",
        PdfDiagnosticCode.MissingEndStream => "A block of data was never closed, so it was read to the end of its part.",
        PdfDiagnosticCode.TruncatedStream => "A block of data ended early, so the part that could be read is used.",
        PdfDiagnosticCode.CatalogRebuilt => "The file's main entry was missing, so it was found by searching.",
        PdfDiagnosticCode.TrailerRebuilt => "The file's closing section was missing, so it was made from what was found.",
        PdfDiagnosticCode.PageTreeRebuilt => "The list of pages was broken, so the pages were found by searching.",
        PdfDiagnosticCode.BadPageBox => "A page had a size that was reversed or empty, so it was corrected.",
        PdfDiagnosticCode.BadFontWidths => "A font had bad letter widths, so the usable ones are used.",
        PdfDiagnosticCode.BadFontEncoding => "A font had a bad letter table, so the usable part is used.",
        PdfDiagnosticCode.BadFontDescriptor => "A font had bad details, which were ignored.",
        _ => "A part of the file was damaged and was repaired.",
    };
}
