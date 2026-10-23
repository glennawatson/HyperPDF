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

/// <summary>Measures the accessibility report on a generated 100-page tagged file with a heading, a paragraph, a figure and a link on every page.</summary>
public class AccessibilityReportBenchmarks
{
    /// <summary>The number of pages.</summary>
    private const int PageCount = 100;

    /// <summary>The objects written for each page.</summary>
    private const int ObjectsPerPage = 6;

    /// <summary>The objects written before the first page.</summary>
    private const int FixedObjects = 6;

    /// <summary>The offset of a page's heading element from its page object.</summary>
    private const int HeadingOffset = 2;

    /// <summary>The offset of a page's paragraph element.</summary>
    private const int ParagraphOffset = 3;

    /// <summary>The offset of a page's link annotation.</summary>
    private const int AnnotationOffset = 4;

    /// <summary>The offset of a page's Link element.</summary>
    private const int LinkOffset = 5;

    /// <summary>The generated file.</summary>
    private byte[] _bytes = [];

    /// <summary>The document opened for the warm measurement.</summary>
    private PdfDocument _document = null!;

    /// <summary>Generates the file and opens it once, so the warm measurement reads cached tags and page content.</summary>
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

    /// <summary>Opens the file and reads the report, including the tags and the content of every page.</summary>
    /// <returns>The report.</returns>
    [Benchmark]
    public PdfAccessibilityReport Cold()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfDocumentAccessibility.GetAccessibilityReport(document);
    }

    /// <summary>Builds the 100-page tagged file.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] Generate()
    {
        var objects = new List<string>(FixedObjects + (PageCount * ObjectsPerPage));
        var pages = new StringBuilder();
        var kids = new StringBuilder();
        for (var i = 0; i < PageCount; i++)
        {
            var first = FixedObjects + (i * ObjectsPerPage) + 1;
            _ = pages.Append(CultureInfo.InvariantCulture, $"{first} 0 R ");
            _ = kids.Append(CultureInfo.InvariantCulture, $"{first + HeadingOffset} 0 R {first + ParagraphOffset} 0 R {first + LinkOffset} 0 R ");
            AddPage(objects, first);
        }

        string[] fixedObjects =
        [
            "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /Lang (en) /StructTreeRoot 5 0 R /ViewerPreferences << /DisplayDocTitle true >> >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{pages}] /Count {PageCount} >>"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", string.Empty),
            "<< /Type /StructTreeRoot /K 6 0 R >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Document /P 5 0 R /K [{kids}] >>"),
        ];
        return MiniPdf.Build([.. fixedObjects, .. objects]);
    }

    /// <summary>Adds one page's objects: page, content, heading, paragraph, link annotation and Link element.</summary>
    /// <param name="objects">Receives the objects.</param>
    /// <param name="first">The page object's number.</param>
    private static void AddPage(List<string> objects, int first)
    {
        const string content = "/H1 <</MCID 0>> BDC BT /F1 18 Tf 72 700 Td (Heading) Tj ET EMC\n/P <</MCID 1>> BDC BT /F1 12 Tf 72 660 Td (Some body text) Tj ET EMC\n";
        const string pageStart = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >>";
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"{pageStart} /Contents {first + 1} 0 R /Tabs /S /Annots [{first + AnnotationOffset} 0 R] >>"));
        objects.Add(MiniPdf.Stream(string.Empty, content));
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /H1 /P 6 0 R /Pg {first} 0 R /K 0 >>"));
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /P /P 6 0 R /Pg {first} 0 R /K 1 >>"));
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Link /Rect [72 600 140 616] /Contents (Go) /A << /S /URI /URI (https://example.com) >> /P {first} 0 R >>"));
        objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Link /P 6 0 R /Pg {first} 0 R /K << /Type /OBJR /Obj {first + AnnotationOffset} 0 R >> >>"));
    }
}
