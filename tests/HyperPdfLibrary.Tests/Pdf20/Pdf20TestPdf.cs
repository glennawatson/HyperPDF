// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Pdf20;

/// <summary>Builds the PDF 1.x sample files the PDF 2.0 copy tests convert.</summary>
internal static class Pdf20TestPdf
{
    /// <summary>The text the text samples draw.</summary>
    internal const string SampleText = "Hello PDF 2.0";

    /// <summary>The page size in points.</summary>
    private const int PageSize = 200;

    /// <summary>The namespaces the metadata samples declare.</summary>
    private const string MetadataNamespaces =
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\" xmlns:pdfx=\"http://ns.adobe.com/pdfx/1.3/\"";

    /// <summary>Builds a PDF 1.4 file with every standard information entry, two custom ones, both dates and no XMP.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] InfoOnly()
    {
        var pdf = TextPage();
        var info = pdf.AddObject(
            "<< /Title (Quarterly report) /Author (Ada Lovelace) /Subject (Figures) /Keywords (alpha, beta) /Creator (Writer 3) /Producer (Engine 9) "
            + "/Trapped /False /Department (Sales) /Project#20Code (X-17) /CreationDate (D:20200102030405Z) /ModDate (D:20210203040506Z) >>");
        pdf.TrailerEntries = string.Create(CultureInfo.InvariantCulture, $"/Info {info} 0 R");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file whose XMP and information disagree on the title, the creation date and a custom entry.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] InfoDisagreesWithXmp()
    {
        var pdf = TextPage();
        var info = pdf.AddObject(
            "<< /Title (Info title) /Author (Ada Lovelace) /Producer (Engine 9) /Department (Sales) /CreationDate (D:20200102030405Z) /ModDate (D:20210203040506Z) >>");
        pdf.TrailerEntries = string.Create(CultureInfo.InvariantCulture, $"/Info {info} 0 R");
        ConformancePdf.AttachXmp(
            pdf,
            MetadataNamespaces,
            "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">XMP title</rdf:li></rdf:Alt></dc:title><pdf:Producer>Engine 9</pdf:Producer>"
            + "<xmp:CreateDate>2019-05-06T07:08:09Z</xmp:CreateDate><xmp:Label>Kept as it is</xmp:Label><pdfx:Department>Marketing</pdfx:Department>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file with a text field that has no saved appearance, an XFA packet and a request to draw appearances.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] HybridForm()
    {
        var pdf = TextPage();
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var xfa = pdf.AddStream(string.Empty, "<xdp:xdp xmlns:xdp=\"http://ns.adobe.com/xdp/\"></xdp:xdp>");
        var field = pdf.AddObject(
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Ada Lovelace) /Rect [10 10 190 30] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) >>");
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{field} 0 R]");
        pdf.CatalogEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/AcroForm << /Fields [{field} 0 R] /XFA {xfa} 0 R /NeedAppearances true /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv {font} 0 R >> >> >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file whose form is XFA only: no AcroForm fields.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] XfaOnly()
    {
        var pdf = TextPage();
        var xfa = pdf.AddStream(string.Empty, "<xdp:xdp xmlns:xdp=\"http://ns.adobe.com/xdp/\"></xdp:xdp>");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/AcroForm << /Fields [] /XFA {xfa} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file whose catalog has /NeedsRendering.</summary>
    /// <param name="value">The value: true or false.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] NeedsRendering(bool value)
    {
        var pdf = TextPage();
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/NeedsRendering {(value ? "true" : "false")}");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file that uses many deprecated entries which carry no content.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] DeprecatedEntries()
    {
        var pdf = TextPage();
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Name /F1 /Encoding /WinAnsiEncoding >>");
        var korean = pdf.AddObject("<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Gulim /CIDSystemInfo << /Registry (Adobe) /Ordering (Korea1) /Supplement 2 >> >>");
        var state = pdf.AddObject("<< /Type /ExtGState /BM [/Foo /Multiply] /TR /Identity /TR2 /Default >>");
        var compatible = pdf.AddObject("<< /Type /ExtGState /BM /Compatible >>");
        var spec = pdf.AddObject("<< /Type /Filespec /DOS (A.TXT) /Mac (a.txt) /Unix (/tmp/a.txt) >>");
        var launchSpec = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /S /Launch /F {spec} 0 R >>"));
        var launchWin = pdf.AddObject("<< /S /Launch /Win << /F (notepad.exe) /P (x.txt) >> /Unix (xdg-open) >>");
        var linkA = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Border [0 0 0] /A {launchSpec} 0 R >>"));
        var linkB = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Link /Rect [20 0 30 10] /Border [0 0 0] /A {launchWin} 0 R >>"));
        pdf.Resources = string.Create(
            CultureInfo.InvariantCulture,
            $"/ProcSet [/PDF /Text] /Font << /F1 {font} 0 R /F2 {korean} 0 R >> /ExtGState << /GS1 {state} 0 R /GS2 {compatible} 0 R >>");
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{linkA} 0 R {linkB} 0 R] /ID (web-capture-id)");
        pdf.CatalogEntries =
            "/Names << /AlternatePresentations << /Names [] >> >> /ViewerPreferences << /ViewArea /CropBox /PrintArea /CropBox /Direction /L2R >> "
            + "/MarkInfo << /Marked true /Suspects false >> /SpiderInfo << /V 1.0 >> /Version /1.7";
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file with a deprecated feature that cannot be kept.</summary>
    /// <param name="kind">The feature: movie, sound, trapnet, opi, separation, transfer, soundaction.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Unkeepable(string kind)
    {
        var pdf = TextPage();
        switch (kind)
        {
            case "movie":
                {
                    Annotate(pdf, "<< /Type /Annot /Subtype /Movie /Rect [0 0 50 50] /Movie << /F (clip.mov) >> >>");
                    break;
                }

            case "sound":
                {
                    Annotate(pdf, "<< /Type /Annot /Subtype /Sound /Rect [0 0 50 50] /Sound << /R 44100 >> >>");
                    break;
                }

            case "trapnet":
                {
                    Annotate(pdf, "<< /Type /Annot /Subtype /TrapNet /Rect [0 0 50 50] /LastModified (D:20200101000000Z) /FontFauxing [] >>");
                    break;
                }

            case "soundaction":
                {
                    Annotate(pdf, "<< /Type /Annot /Subtype /Link /Rect [0 0 50 50] /Border [0 0 0] /A << /S /Sound /Sound << /R 44100 >> >> >>");
                    break;
                }

            case "transfer":
                {
                    var state = pdf.AddObject("<< /Type /ExtGState /TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >>");
                    pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS1 {state} 0 R >>");
                    break;
                }

            case "opi":
                {
                    var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /OPI << /2.0 << /Type /OPI /Version 2.0 >> >>", [0]);
                    pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Im1 {image} 0 R >>");
                    break;
                }

            default:
                {
                    pdf.PageEntries = "/SeparationInfo << /Pages [3 0 R] /DeviceColorant /Cyan >>";
                    break;
                }
        }

        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file with a lowercase print field check state in a structure attribute.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] PrintFieldAttribute()
    {
        var pdf = TextPage();
        var element = pdf.AddObject("<< /Type /StructElem /S /Form /A << /O /PrintField /Role /cb /Checked /on >> >>");
        var tree = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructTreeRoot /K [{element} 0 R] >>"));
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/StructTreeRoot {tree} 0 R /MarkInfo << /Marked true >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file with a usage rights signature (Reader extensions) and no other signature.</summary>
    /// <param name="entry">The /Perms key: UR3 for the usage rights signature, DocMDP for a certification signature.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Permissions(string entry)
    {
        var pdf = TextPage();
        var signature = pdf.AddObject("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/Perms << /{entry} {signature} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file whose page and form XObject fill paths with the deprecated F operator.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] DeprecatedFillOperator()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Version = "1.4", Content = "0 0 1 rg 10 10 50 50 re F /Fx0 Do" };
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 100 100]", "1 0 0 rg 70 70 20 20 re F");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fx0 {form} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file with a short file identifier.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] ShortIdentifier()
    {
        var pdf = TextPage();
        pdf.TrailerEntries = "/ID [<0102> <0304>]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a PDF 1.4 file that draws text and has nothing deprecated.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Plain() => TextPage().ToBytes();

    /// <summary>Starts a PDF 1.4 page that draws <see cref="SampleText"/>.</summary>
    /// <returns>The builder.</returns>
    private static RenderTestPdf TextPage()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Version = "1.4" };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >>");
        pdf.Content = $"BT /F1 24 Tf 10 100 Td ({SampleText}) Tj ET";
        return pdf;
    }

    /// <summary>Adds an annotation to the page.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="annotation">The annotation dictionary.</param>
    private static void Annotate(RenderTestPdf pdf, string annotation)
    {
        var number = pdf.AddObject(annotation);
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{number} 0 R]");
    }
}
