// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the accessibility report on a generated 100-page file that claims PDF/UA-2: the document element is in the PDF 2.0
/// namespace, every page shows text in a Type 3 font and draws an inline image, and the headings are role mapped through a namespace.
/// It covers the font, Unicode mapping, namespace role map and untagged image checks.
/// </summary>
public class AccessibilityPart2ReportBenchmarks
{
    /// <summary>The number of pages.</summary>
    private const int PageCount = 100;

    /// <summary>The objects written for each page.</summary>
    private const int ObjectsPerPage = 3;

    /// <summary>The objects written before the first page.</summary>
    private const int FixedObjects = 9;

    /// <summary>The objects written for the font after the fixed ones: the glyph procedure and the ToUnicode map.</summary>
    private const int FontObjects = 2;

    /// <summary>The object number of the glyph procedure.</summary>
    private const int GlyphNumber = FixedObjects + 1;

    /// <summary>The object number of the ToUnicode map.</summary>
    private const int MapNumber = FixedObjects + 2;

    /// <summary>The object number of the page tree.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the font.</summary>
    private const int FontNumber = 3;

    /// <summary>The object number of the structure tree root.</summary>
    private const int RootNumber = 6;

    /// <summary>The object number of the document element.</summary>
    private const int DocumentNumber = 7;

    /// <summary>The object number of the PDF 2.0 namespace.</summary>
    private const int NamespaceNumber = 8;

    /// <summary>The object number of the custom namespace.</summary>
    private const int CustomNamespaceNumber = 9;

    /// <summary>The offset of a page's content stream from its page object.</summary>
    private const int ContentOffset = 1;

    /// <summary>The offset of a page's structure element from its page object.</summary>
    private const int ElementOffset = 2;

    /// <summary>The first character code the font defines.</summary>
    private const int FirstCode = 32;

    /// <summary>The number of character codes the font defines.</summary>
    private const int CodeCount = 91;

    /// <summary>The ToUnicode map: each code from the space to the letter z stands for the same character.</summary>
    private const string ToUnicodeMap = """
        /CIDInit /ProcSet findresource begin 12 dict begin begincmap
        /CMapName /Adobe-Identity-UCS def /CMapType 2 def
        1 begincodespacerange <00> <FF> endcodespacerange
        1 beginbfrange <20> <7A> <0020> endbfrange
        endcmap CMapName currentdict /CMap defineresource pop end end
        """;

    /// <summary>The XMP packet with the PDF/UA-2 claim.</summary>
    private const string Packet = """
        <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?><x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/">
        <pdfuaid:part>2</pdfuaid:part><pdfuaid:rev>2024</pdfuaid:rev><dc:title><rdf:Alt><rdf:li xml:lang="x-default">A title</rdf:li></rdf:Alt></dc:title>
        </rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end="w"?>
        """;

    /// <summary>The page content: a tagged line, an untagged inline image and an artifact path.</summary>
    private const string Content =
        "/X <</MCID 0>> BDC BT /F1 12 Tf 72 700 Td (Some body text) Tj ET EMC\nq 40 0 0 40 300 300 cm BI /W 1 /H 1 /CS /G /BPC 8 ID A EI Q\n/Artifact BMC 0 0 10 10 re f EMC\n";

    /// <summary>The generated file.</summary>
    private byte[] _bytes = [];

    /// <summary>The document opened for the warm measurements.</summary>
    private PdfDocument _document = null!;

    /// <summary>Generates the file and opens it once, so the warm measurements read cached tags and page content.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _bytes = Generate();
        _document = PdfDocumentReader.Open(_bytes, null);
        _ = PdfDocumentAccessibility.GetAccessibilityReport(_document);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the report from a document whose tags and page content are already read.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public PdfAccessibilityReport Warm() => PdfDocumentAccessibility.GetAccessibilityReport(_document);

    /// <summary>Reads the report through the asynchronous form from a document whose tags and page content are already read.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public ValueTask<PdfAccessibilityReport> WarmAsync() => PdfDocumentAccessibility.GetAccessibilityReportAsync(_document, CancellationToken.None);

    /// <summary>Opens the file and reads the report, including the tags, the fonts and the content of every page.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public PdfAccessibilityReport Cold()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfDocumentAccessibility.GetAccessibilityReport(document);
    }

    /// <summary>Builds the 100-page file.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] Generate()
    {
        var objects = new List<string>(FixedObjects + (PageCount * ObjectsPerPage));
        var pages = new StringBuilder();
        var kids = new StringBuilder();
        for (var i = 0; i < PageCount; i++)
        {
            var first = FixedObjects + FontObjects + (i * ObjectsPerPage) + 1;
            _ = pages.Append(CultureInfo.InvariantCulture, $"{first} 0 R ");
            _ = kids.Append(CultureInfo.InvariantCulture, $"{first + ElementOffset} 0 R ");
            AddPage(objects, first);
        }

        string[] fixedObjects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Version /2.0 /MarkInfo << /Marked true >> /Lang (en) /StructTreeRoot 6 0 R /Metadata 5 0 R /ViewerPreferences << /DisplayDocTitle true >> >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{pages}] /Count {PageCount} >>"),
            FontBody(),
            "<< >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", Packet),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructTreeRoot /K {DocumentNumber} 0 R /Namespaces [{NamespaceNumber} 0 R {CustomNamespaceNumber} 0 R] >>"),
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Document /P {RootNumber} 0 R /NS {NamespaceNumber} 0 R /K [{kids}] >>"),
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Namespace /NS (urn:example:custom) /RoleMapNS << /X [/P {NamespaceNumber} 0 R] >> >>"),
        ];
        string[] fontObjects = [MiniPdf.Stream(string.Empty, "1000 0 0 0 750 750 d1 0 0 750 750 re f"), MiniPdf.Stream(string.Empty, ToUnicodeMap)];
        return MiniPdf.Build([.. fixedObjects, .. fontObjects, .. objects]);
    }

    /// <summary>Writes the Type 3 font with a glyph procedure and a ToUnicode map.</summary>
    /// <returns>The font dictionary.</returns>
    private static string FontBody() => string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 750 750] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /g {GlyphNumber} 0 R >> "
        + $"/Encoding << /Type /Encoding /Differences [{FirstCode} {Names()}] >> /FirstChar {FirstCode} /LastChar {FirstCode + CodeCount - 1} /Widths [{Widths()}] /ToUnicode {MapNumber} 0 R >>");

    /// <summary>Writes the glyph name of every code.</summary>
    /// <returns>The names.</returns>
    private static string Names() => string.Concat(Enumerable.Repeat("/g ", CodeCount));

    /// <summary>Writes the width of every code.</summary>
    /// <returns>The widths.</returns>
    private static string Widths() => string.Concat(Enumerable.Repeat("1000 ", CodeCount));

    /// <summary>Adds one page's objects: page, content and a structure element in the custom namespace.</summary>
    /// <param name="objects">Receives the objects.</param>
    /// <param name="first">The page object's number.</param>
    private static void AddPage(List<string> objects, int first)
    {
        objects.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {FontNumber} 0 R >> >> /Contents {first + ContentOffset} 0 R /Tabs /S >>"));
        objects.Add(MiniPdf.Stream(string.Empty, Content));
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /X /P {DocumentNumber} 0 R /NS {CustomNamespaceNumber} 0 R /Pg {first} 0 R /K 0 >>"));
    }
}
