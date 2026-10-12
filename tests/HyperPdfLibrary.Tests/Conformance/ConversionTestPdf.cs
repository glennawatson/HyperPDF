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

    /// <summary>The content of a page that fills a blue box in device RGB.</summary>
    private const string BlueBox = "0 0 1 rg 10 10 50 50 re f";

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
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = BlueBox };
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

    /// <summary>Builds a page whose form names ZapfDingbats in its default resources, which no content draws with.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UnusedDingbatsInFormResources()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = BlueBox };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/AcroForm << /Fields [] /DR << /Font << /ZaDb {font} 0 R >> >> /DA (/ZaDb 0 Tf 0 g) >>");
        return pdf.ToBytes();
    }

    /// <summary>
    /// Builds a page with a text field that draws with ZapfDingbats and asks readers to draw appearances again, while its
    /// saved appearance draws only a box.
    /// </summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] DingbatsFieldWithSavedAppearance()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = "0 0 1 rg 10 60 50 50 re f" };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>");
        var normal = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 50 20]", "0 0 1 rg 0 0 50 20 re f");
        var field = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (mark) /V (4) /Rect [10 10 60 30] /F 4 /P 3 0 R /DA (/ZaDb 12 Tf 0 g) /AP << /N {normal} 0 R >> >>"));
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{field} 0 R]");
        pdf.CatalogEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/AcroForm << /Fields [{field} 0 R] /NeedAppearances true /DR << /Font << /ZaDb {font} 0 R >> >> /DA (/ZaDb 0 Tf 0 g) >>");
        return pdf.ToBytes();
    }

    /// <summary>
    /// Builds a page whose content uses an operator the PDF standard does not define, inside a BX/EX section; veraPDF still
    /// fails it.
    /// </summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UndefinedOperatorInCompatibilitySection() =>
        new RenderTestPdf(PageSize, PageSize) { Content = $"BX 1 2 madeup EX {BlueBox}" }.ToBytes();

    /// <summary>Builds a page whose resources hold a form that uses an undefined operator, which the page never draws.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UnusedUndefinedOperatorForm() => UnusedForm("1 2 madeup 0 0 10 10 re f");

    /// <summary>Builds a page whose content uses an operator the PDF standard does not define, outside any BX/EX section.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UndefinedOperator() =>
        new RenderTestPdf(PageSize, PageSize) { Content = $"1 2 madeup {BlueBox}" }.ToBytes();

    /// <summary>Builds an RGB page whose resources hold a form that paints in device CMYK, which the page never draws.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UnusedCmykForm() => UnusedForm("0 1 1 0 k 0 0 10 10 re f");

    /// <summary>Builds a page whose resources hold a form that nests graphics states 30 deep, which the page never draws.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UnusedDeepNestingForm()
    {
        const int Depth = 30;
        return UnusedForm(string.Concat(Enumerable.Repeat("q ", Depth)) + string.Concat(Enumerable.Repeat("Q ", Depth)));
    }

    /// <summary>Builds a page whose resources hold a see-through graphics state, which the content never sets.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] UnusedTransparentGraphicsState()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = BlueBox };
        var state = pdf.AddObject("<< /Type /ExtGState /CA 0.5 /ca 0.5 >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS0 {state} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds an RGB page whose resources hold a form the page never draws.</summary>
    /// <param name="content">The form's content.</param>
    /// <returns>The file's bytes.</returns>
    private static byte[] UnusedForm(string content)
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = BlueBox };
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", content);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fx0 {form} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Starts a page with a signed signature field.</summary>
    /// <returns>The builder.</returns>
    private static RenderTestPdf SignedPage()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = BlueBox };
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
