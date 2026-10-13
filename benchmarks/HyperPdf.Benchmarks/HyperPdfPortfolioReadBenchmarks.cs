// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading a portfolio's schema, folders and items, and the files associated with a document.</summary>
public class HyperPdfPortfolioReadBenchmarks
{
    /// <summary>The embedded file stream entries.</summary>
    private const string EmbeddedFile = "/Type /EmbeddedFile";

    /// <summary>The number of files in the fixture.</summary>
    private const int FileCount = 3;

    /// <summary>The portfolio fixture.</summary>
    private PdfDocument? _document;

    /// <summary>Opens a portfolio with a schema, sort, folders and three files, one of them also associated with the page.</summary>
    /// <exception cref="InvalidOperationException">The fixture does not read as a portfolio.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(<1>folder.txt) 5 0 R (a.txt) 6 0 R (b.bin) 7 0 R] >> >> /Collection 4 0 R /AF [6 0 R] >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /AF [7 0 R] >>",
            "<< /Type /Collection /D (a.txt) /View /D /Schema << /Name << /Subtype /S /N (Name) /O 0 >> /Size << /Subtype /N /N (Size) /O 1 >> >> /Sort << /S /Name >> /Folders 11 0 R >>",
            "<< /Type /Filespec /F (folder.txt) /EF << /F 8 0 R >> /CI 10 0 R >>",
            "<< /Type /Filespec /F (a.txt) /AFRelationship /Source /EF << /F 9 0 R >> >>",
            "<< /Type /Filespec /F (b.bin) /AFRelationship /Data /EF << /F 12 0 R >> >>",
            MiniPdf.Stream(EmbeddedFile, "folder file"),
            MiniPdf.Stream(EmbeddedFile, "a"),
            "<< /Type /CollectionItem /Name (Folder file) /Size 11 >>",
            "<< /Type /Folder /ID 0 /Name (Root) /Child 13 0 R >>",
            MiniPdf.Stream(EmbeddedFile, "bytes of the third file"),
            "<< /Type /Folder /ID 1 /Name (Docs) /Parent 11 0 R >>");
        _document = PdfDocumentReader.Open(bytes, null);
        if (PdfDocumentPortfolio.GetPortfolio(_document)?.Items.Count != FileCount || PdfDocumentAssociatedFiles.GetAllAssociatedFiles(_document).Count != 2)
        {
            throw new InvalidOperationException("The portfolio benchmark fixture did not read as expected.");
        }
    }

    /// <summary>Closes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Lists the embedded files, which the document caches after the first read.</summary>
    /// <returns>The number of files.</returns>
    [Benchmark(Baseline = true)]
    public int ListAttachments() => PdfDocumentAttachments.GetAttachments(_document!).Count;

    /// <summary>Reads the portfolio: schema, sort, folder tree and the collection values of each file.</summary>
    /// <returns>The number of items.</returns>
    [Benchmark]
    public int ReadPortfolio() => PdfDocumentPortfolio.GetPortfolio(_document!)!.Items.Count;

    /// <summary>Reads every associated file: the catalog's, then each page's, annotations', XObjects' and structure elements'.</summary>
    /// <returns>The number of files.</returns>
    [Benchmark]
    public int ReadAssociatedFiles() => PdfDocumentAssociatedFiles.GetAllAssociatedFiles(_document!).Count;
}
