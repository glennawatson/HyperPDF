// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Builds one page PDFs for the page object tests and runs edits on them.</summary>
internal static class PageObjectSamples
{
    /// <summary>The page width and height in points.</summary>
    internal const int Size = 200;

    /// <summary>The highest channel value.</summary>
    private const byte Full = 255;

    /// <summary>The Helvetica font dictionary every sample page names /F1.</summary>
    private const string Helvetica = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    /// <summary>Builds a page with the given content and Helvetica as /F1.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Page(string content) => Page(content, string.Empty);

    /// <summary>Builds a page with the given content, Helvetica as /F1 and extra resource entries.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="extraResources">Extra entries of the /Resources dictionary, such as an /XObject table.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Page(string content, string extraResources)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var font = pdf.AddObject(Helvetica);
        pdf.Resources = $"/Font << /F1 {font} 0 R >> {extraResources}";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that holds one of every kind of object.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Mixed()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var font = pdf.AddObject(Helvetica);
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8", [Full, 0, 0, 0, Full, 0, 0, 0, Full, Full, Full, 0]);
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 40 40]", "0 0 1 rg 0 0 40 40 re f");
        var shading = pdf.AddObject("<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [100 0 140 0] /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >> /XObject << /Im1 {image} 0 R /Fm1 {form} 0 R >> /Shading << /Sh1 {shading} 0 R >>";
        pdf.Content = """
            q 0.9 0.2 0.1 rg 10 10 60 40 re f Q
            BT /F1 18 Tf 10 150 Td (Hello World) Tj ET
            q 50 0 0 50 120 20 cm /Im1 Do Q
            q 100 60 40 40 re W n /Sh1 sh Q
            q 1 0 0 1 30 100 cm /Fm1 Do Q
            q 10 0 0 10 170 170 cm BI /W 1 /H 1 /CS /G /BPC 8 ID ?
            EI Q
            """;
        return pdf.ToBytes();
    }

    /// <summary>Renders the first page of a PDF at one pixel per point.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The pixels.</returns>
    internal static RenderedImage Render(byte[] pdf)
    {
        using var page = new RenderTestPage(pdf);
        return page.RenderPage();
    }

    /// <summary>Opens a PDF, runs an edit on the first page's content, applies it and saves the document compactly.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>The saved PDF bytes.</returns>
    internal static byte[] Edit(byte[] pdf, Action<PdfPageContent> edit)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);
        edit(content);
        PdfPageContentApplication.Apply(content);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
    }

    /// <summary>Reads the first page's content of a PDF.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The content, which keeps its document open for the life of the test.</returns>
    internal static PdfPageContent Read(byte[] pdf) => PdfDocumentPageContent.GetPageContent(PdfDocumentReader.Open(pdf, null), 0);
}
