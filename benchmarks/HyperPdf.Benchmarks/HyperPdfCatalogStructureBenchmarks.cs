// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading the catalog's less common structures from a warm document: a three-bead article thread, two
/// page-piece producers, a nested document part tree and web capture content sets. Allocations come from the
/// EventPipe trace.
/// </summary>
public class HyperPdfCatalogStructureBenchmarks
{
    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's page.</summary>
    private PdfPage _page = null!;

    /// <summary>Opens the document and reads each structure once so its objects are parsed.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Threads [4 0 R] /DPartRoot 8 0 R /SpiderInfo << /V 1.0 >> "
            + "/Names << /IDS << /Names [(a) [11 0 R 12 0 R]] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /DPart 10 0 R "
            + "/PieceInfo << /AppA << /LastModified (D:20240102030405Z) /Private << /X 1 >> >> /AppB << /LastModified (D:20250630120000+10'00') >> >> >>",
            "<< /Type /Thread /F 5 0 R /I << /Title (Story) >> >>",
            "<< /T 4 0 R /N 6 0 R /V 7 0 R /P 3 0 R /R [158 247 318 905] >>",
            "<< /T 4 0 R /N 7 0 R /V 5 0 R /P 3 0 R /R [322 246 486 904] >>",
            "<< /T 4 0 R /N 5 0 R /V 6 0 R /P 3 0 R /R [157 254 319 903] >>",
            "<< /Type /DPartRoot /DPartRootNode 9 0 R /RecordLevel 1 /NodeNameList [/Job /Recipient] >>",
            "<< /Type /DPart /Parent 8 0 R /DParts [[10 0 R]] /DPM << /Kind /Job >> >>",
            "<< /Type /DPart /Parent 9 0 R /Start 3 0 R /DPM << /Recipient (Ann) >> >>",
            "<< /Type /SpiderContentSet /S /SPS /ID (one) /O [3 0 R] /SI << /AU (http://x/) /TS (D:20230101000000Z) >> >>",
            "<< /Type /SpiderContentSet /S /SIS /ID (two) /O [] /SI [<< /AU (http://x/a.gif) >>] >>"),
            null);
        _page = PdfDocumentPages.GetPage(_document, 0);
        _ = ReadThreads();
        _ = ReadPieceInfo();
        _ = ReadDocumentParts();
        _ = ReadWebCapture();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the article threads and follows each bead ring.</summary>
    /// <returns>The bead count.</returns>
    [Benchmark]
    public int ReadThreads() => PdfDocumentCatalog.GetThreads(_document)[0].Beads.Length;

    /// <summary>Reads the page's two page-piece producers and their dates.</summary>
    /// <returns>The producer count.</returns>
    [Benchmark]
    public int ReadPieceInfo() => PdfDocumentCatalog.GetPieceInfo(_document, _page).Length;

    /// <summary>Reads the document part tree with its metadata and page ranges.</summary>
    /// <returns>The top node's child count.</returns>
    [Benchmark]
    public int ReadDocumentParts() => PdfDocumentWebCapture.GetDocumentParts(_document)!.Root!.Children.Length;

    /// <summary>Reads the web capture content sets named by the IDS tree.</summary>
    /// <returns>The content set count.</returns>
    [Benchmark]
    public int ReadWebCapture() => PdfDocumentWebCapture.GetWebCapture(_document)!.ContentSets.Length;
}
