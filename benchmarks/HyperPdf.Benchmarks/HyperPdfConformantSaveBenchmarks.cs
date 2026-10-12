// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the save and metadata paths that keep a PDF/A claim valid: compact saves of an unclaimed and a PDF/A-1
/// document, an incremental save without edits, and a document property edit with and without a claim.
/// </summary>
public class HyperPdfConformantSaveBenchmarks
{
    /// <summary>The pages in the unedited document.</summary>
    private const int Pages = 100;

    /// <summary>The page size of the claimed documents.</summary>
    private const int PageSize = 100;

    /// <summary>The PDF/A part based on PDF 1.7.</summary>
    private const int Part2 = 2;

    /// <summary>The unclaimed form and outline document.</summary>
    private PdfDocument _book = null!;

    /// <summary>A document that claims PDF/A-1b.</summary>
    private PdfDocument _pdfA1 = null!;

    /// <summary>A document that claims PDF/A-2b, for the claimed edit.</summary>
    private PdfDocument _pdfA2 = null!;

    /// <summary>A document without a claim, for the plain edit.</summary>
    private PdfDocument _plain = null!;

    /// <summary>A document saved without edits.</summary>
    private PdfDocument _unedited = null!;

    /// <summary>Gets the edit made by the metadata benchmarks.</summary>
    private static PdfMetadataEdit Edit => new() { Title = "Benchmark title", Author = "Benchmark author" };

    /// <summary>Opens the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _book = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        _pdfA1 = PdfDocumentReader.Open(Claimed(1), null);
        _pdfA2 = PdfDocumentReader.Open(Claimed(Part2), null);
        _plain = PdfDocumentReader.Open(TestPdf.Create(1), null);
        _unedited = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _book.Dispose();
        _pdfA1.Dispose();
        _pdfA2.Dispose();
        _plain.Dispose();
        _unedited.Dispose();
    }

    /// <summary>Rewrites an unclaimed document with object streams.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark(Baseline = true)]
    public byte[] CompactUnclaimed() => PdfCompactWriter.Save(_book.Objects, PdfCompactOptions.Default);

    /// <summary>Rewrites a PDF/A-1 document with the default layout.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] CompactPdfA1() => PdfCompactWriter.Save(_pdfA1.Objects, PdfCompactOptions.Default);

    /// <summary>Saves a document without edits incrementally.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int SaveUnedited() => PdfIncrementalWriter.Save(_unedited.Objects).Length;

    /// <summary>Edits the properties of a document without a claim, then undoes the edit.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool EditPlainMetadata()
    {
        PdfDocumentMetadataEditing.SetMetadata(_plain, Edit);
        return PdfDocumentEditing.Undo(_plain);
    }

    /// <summary>Edits the properties of a PDF/A-2b document, then undoes the edit.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool EditPdfAMetadata()
    {
        PdfDocumentMetadataEditing.SetMetadata(_pdfA2, Edit);
        return PdfDocumentEditing.Undo(_pdfA2);
    }

    /// <summary>Builds a one-page document whose XMP claims a PDF/A part at level B.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The file.</returns>
    private static byte[] Claimed(int part)
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = "0 0 1 rg 10 10 80 80 re f" };
        var packet = "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\""
            + " xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
            + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Claimed</rdf:li></rdf:Alt></dc:title>"
            + $"<pdfaid:part>{part}</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description></rdf:RDF></x:xmpmeta>"
            + new string(' ', PageSize) + "<?xpacket end=\"w\"?>";
        var metadata = pdf.AddStream("/Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes(packet));
        pdf.CatalogEntries = $"/Metadata {metadata} 0 R";
        return pdf.ToBytes();
    }
}
