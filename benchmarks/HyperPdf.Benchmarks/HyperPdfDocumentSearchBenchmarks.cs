// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures whole-document search, which reads each page's text without making a text page.</summary>
public class HyperPdfDocumentSearchBenchmarks
{
    /// <summary>The number of pages searched.</summary>
    private const int Pages = 40;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens a many-page document.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Searches every page for a word on each of them.</summary>
    /// <returns>The number of pages with a match.</returns>
    [Benchmark]
    public async Task<int> FindEveryPage()
    {
        var pages = 0;
        await foreach (var unused in PdfDocumentText.FindAsync(_document, "quick", PdfTextSearchOptions.None, CancellationToken.None))
        {
            pages += unused.Matches.Length > 0 ? 1 : 0;
        }

        return pages;
    }
}
