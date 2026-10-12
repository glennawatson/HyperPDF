// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the PDF/A conformance reading report: over a generated 100-page document without a claim, and over a page
/// that claims PDF/A-2b, where the report also checks the claimed level rule by rule.
/// </summary>
public class HyperPdfConformanceBenchmarks
{
    /// <summary>The pages in the generated document.</summary>
    private const int PageCount = 100;

    /// <summary>The size of the claimed page, in points.</summary>
    private const int PageSize = 100;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>A page that claims PDF/A-2b.</summary>
    private PdfDocument _claimed = null!;

    /// <summary>Gets the XMP packet that claims PDF/A-2b.</summary>
    private static ReadOnlySpan<byte> Packet =>
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"u8
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\""u8
        + " xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"><pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance>"u8
        + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>"u8;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(PageCount), null);
        _claimed = PdfDocumentReader.Open(Claimed(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _claimed.Dispose();
    }

    /// <summary>Reads the conformance report: XMP, output intents, every object and the resources every page's content uses.</summary>
    /// <returns>The number of observations.</returns>
    [Benchmark(Baseline = true)]
    public int GetConformance() => PdfDocumentConformance.GetConformance(_document).Observations.Length;

    /// <summary>Reads the conformance report of a claimed page, with the rule-by-rule check of PDF/A-2b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int GetConformanceClaimed() => PdfDocumentConformance.GetConformance(_claimed).PdfAValidation!.Failed.Length;

    /// <summary>Builds a page of text in an unembedded font whose XMP claims PDF/A-2b.</summary>
    /// <returns>The file.</returns>
    private static byte[] Claimed()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize)
        {
            Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>",
            Content = "BT /F1 12 Tf 10 50 Td (Claimed) Tj ET 0 0 1 rg 10 10 80 20 re f",
        };
        var metadata = pdf.AddStream("/Type /Metadata /Subtype /XML", Packet.ToArray());
        pdf.CatalogEntries = $"/Metadata {metadata} 0 R";
        return pdf.ToBytes();
    }
}
