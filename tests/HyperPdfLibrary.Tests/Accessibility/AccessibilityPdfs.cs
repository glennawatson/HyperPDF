// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Tagged;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Builds the tagged PDFs for the accessibility tests and reads their reports.</summary>
internal static class AccessibilityPdfs
{
    /// <summary>The number of tagged lines in the default content.</summary>
    internal const int DefaultLines = 2;

    /// <summary>The number of PDF/UA part 2.</summary>
    internal const int PartTwo = 2;

    /// <summary>The XMP packet up to the PDF/UA properties.</summary>
    private const string PacketStart = """
        <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?><x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        """ + """
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/">
        """;

    /// <summary>The XMP packet after the properties.</summary>
    private const string PacketEnd = "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>The left edge of every line.</summary>
    private const int LeftEdge = 72;

    /// <summary>The first line's baseline.</summary>
    private const int Top = 700;

    /// <summary>The distance between lines.</summary>
    private const int Leading = 20;

    /// <summary>The link annotation's rectangle.</summary>
    private const string LinkRect = "/Rect [72 600 140 616]";

    /// <summary>Writes tagged text lines with marked content ids from zero.</summary>
    /// <param name="count">The number of lines.</param>
    /// <returns>The content.</returns>
    internal static string Lines(int count)
    {
        var content = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            _ = content.Append(TaggedPdfBuilder.Text("P", i, LeftEdge, Top - (Leading * i), string.Create(CultureInfo.InvariantCulture, $"Line{i}")));
        }

        return content.ToString();
    }

    /// <summary>Writes a figure's marked content, a filled box.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The content.</returns>
    internal static string FigureContent(int mcid) =>
        string.Create(CultureInfo.InvariantCulture, $"/Figure <</MCID {mcid}>> BDC 0 0 0 rg 72 400 100 50 re f EMC\n");

    /// <summary>Writes an XMP packet with the PDF/UA part and, optionally, a title.</summary>
    /// <param name="part">The <c>pdfuaid:part</c>.</param>
    /// <param name="title">Whether to include a <c>dc:title</c>.</param>
    /// <returns>The packet.</returns>
    internal static string Packet(int part, bool title)
    {
        var titleXml = title ? "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">A title</rdf:li></rdf:Alt></dc:title>" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{PacketStart}<pdfuaid:part>{part}</pdfuaid:part>{titleXml}{PacketEnd}");
    }

    /// <summary>The default layout: a level 1 heading, a paragraph and a described figure.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="document">The document element's number.</param>
    /// <returns>The document element's kids.</returns>
    internal static string DefaultLayout(TaggedPdfBuilder pdf, int document)
    {
        var heading = pdf.Element("H1", document, "/K 0");
        var paragraph = pdf.Element("P", document, "/K 1");
        var figure = pdf.Element("Figure", document, "/K 2 /Alt (A box)");
        return Kids(heading, paragraph, figure);
    }

    /// <summary>Writes element references for a <c>/K</c> array.</summary>
    /// <param name="numbers">The object numbers.</param>
    /// <returns>The references, separated by spaces.</returns>
    internal static string Kids(params int[] numbers)
    {
        var kids = new StringBuilder();
        foreach (var number in numbers)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
        }

        return kids.ToString();
    }

    /// <summary>Adds a link annotation to the page.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="entries">Extra annotation entries, such as <c>/Contents (Go)</c>.</param>
    /// <returns>The annotation's object number; it is also appended to the page's <c>/Annots</c>.</returns>
    internal static int AddLink(TaggedPdfBuilder pdf, string entries) =>
        AddAnnotation(pdf, $"/Subtype /Link {LinkRect} /A << /S /URI /URI (https://example.com) >> {entries}");

    /// <summary>Adds an annotation to the page.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="entries">The annotation entries, including <c>/Subtype</c>.</param>
    /// <returns>The annotation's object number.</returns>
    internal static int AddAnnotation(TaggedPdfBuilder pdf, string entries)
    {
        var number = pdf.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot {entries} /P {TaggedPdfBuilder.PageNumber} 0 R >>"));
        pdf.Page += string.Create(CultureInfo.InvariantCulture, $" /Annots [{number} 0 R]");
        return number;
    }

    /// <summary>Adds a structure element that refers to an object.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="type">The element's type.</param>
    /// <param name="document">The parent's number.</param>
    /// <param name="target">The object's number.</param>
    /// <param name="entries">Extra element entries.</param>
    /// <returns>The element's number.</returns>
    internal static int AddObjectElement(TaggedPdfBuilder pdf, string type, int document, int target, string entries) =>
        pdf.Element(type, document, string.Create(CultureInfo.InvariantCulture, $"/K << /Type /OBJR /Obj {target} 0 R >> {entries}"));

    /// <summary>Builds the PDF a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Build(AccessibilitySpec spec)
    {
        var pdf = new TaggedPdfBuilder { Content = spec.Content, Page = spec.PageExtra };
        var metadata = spec.Xmp is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"/Metadata {pdf.Add(MiniPdf.Stream("/Type /Metadata /Subtype /XML", spec.Xmp))} 0 R");
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var kids = spec.Layout(pdf, document);
        pdf.Set(document, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Document /P {root} 0 R /K [{kids}] >>"));
        pdf.Set(root, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructTreeRoot /K {document} 0 R {spec.RootExtra} >>"));
        var tree = spec.Tree ? string.Create(CultureInfo.InvariantCulture, $"/StructTreeRoot {root} 0 R") : string.Empty;
        pdf.Catalog = $"{spec.MarkInfo} {spec.Lang} {metadata} {spec.ViewerPreferences} {spec.CatalogExtra} {tree}";
        return pdf.Build();
    }

    /// <summary>Builds a PDF and reads its report.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The report.</returns>
    internal static PdfAccessibilityReport Report(AccessibilitySpec spec)
    {
        using var document = TaggedTestDocument.Open(Build(spec));
        return PdfDocumentAccessibility.GetAccessibilityReport(document);
    }
}
