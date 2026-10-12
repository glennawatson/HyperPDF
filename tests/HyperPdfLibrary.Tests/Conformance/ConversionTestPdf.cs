// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Builds one-page PDFs that are not PDF/A yet, for the PDF/A conversion tests.</summary>
internal static class ConversionTestPdf
{
    /// <summary>The page size in points.</summary>
    private const int PageSize = 200;

    /// <summary>The PDF/A part the signed page claims.</summary>
    private const int SignedPart = 2;

    /// <summary>Builds a page that shows text in Helvetica, which the file does not embed.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] HelveticaText() => Text("0 Tr");

    /// <summary>Builds a page whose only text is invisible, as an OCR layer draws it.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] InvisibleText() => Text("3 Tr");

    /// <summary>Builds a page with a JavaScript open action and a link that launches a program.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Scripts()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = "0 0 1 rg 10 10 50 50 re f" };
        var script = pdf.AddObject("<< /S /JavaScript /JS (app.alert\\('hi'\\);) >>");
        var link = pdf.AddObject("<< /Type /Annot /Subtype /Link /Rect [0 0 50 50] /Border [0 0 0] /A << /S /Launch /F (calc.exe) >> >>");
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{link} 0 R]");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/OpenAction {script} 0 R");
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that paints with device CMYK and carries no CMYK profile.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Cmyk() => new RenderTestPdf(PageSize, PageSize) { Content = "0 1 1 0 k 10 10 50 50 re f" }.ToBytes();

    /// <summary>Builds a page with a signed signature field.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Signed() => SignedPage().ToBytes();

    /// <summary>Builds a signed page that claims PDF/A-2b but paints in DeviceRGB with no output intent.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] ClaimedSigned()
    {
        var pdf = SignedPage();
        ConformancePdf.Claim(pdf, SignedPart, "B", null);
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that claims a PDF/A level but shows text in non-embedded Helvetica, which every level forbids.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conformance">The claimed conformance letter, or <see langword="null"/> for part 4.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] ClaimedHelveticaText(int part, string? conformance)
    {
        var pdf = TextPage("0 Tr");
        ConformancePdf.Claim(pdf, part, conformance, null);
        return pdf.ToBytes();
    }

    /// <summary>Starts a page with a signed signature field.</summary>
    /// <returns>The builder.</returns>
    private static RenderTestPdf SignedPage()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = "0 0 1 rg 10 10 50 50 re f" };
        var signature = pdf.AddObject("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>");
        var field = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /FT /Sig /T (Signature) /V {signature} 0 R /Subtype /Widget /Rect [0 0 0 0] /F 4 /P 3 0 R >>"));
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{field} 0 R]");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/AcroForm << /Fields [{field} 0 R] /SigFlags 3 >>");
        return pdf;
    }

    /// <summary>Builds a page that shows text in non-embedded Helvetica in a render mode.</summary>
    /// <param name="mode">The text render mode operator, such as "3 Tr".</param>
    /// <returns>The file's bytes.</returns>
    private static byte[] Text(string mode) => TextPage(mode).ToBytes();

    /// <summary>Starts a page that shows text in non-embedded Helvetica in a render mode.</summary>
    /// <param name="mode">The text render mode operator, such as "3 Tr".</param>
    /// <returns>The builder.</returns>
    private static RenderTestPdf TextPage(string mode)
    {
        var pdf = new RenderTestPdf(PageSize, PageSize);
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >>");
        pdf.Content = $"BT {mode} /F1 24 Tf 10 100 Td (Hello PDF/A) Tj ET";
        return pdf;
    }
}
