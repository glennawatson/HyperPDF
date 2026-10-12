// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the PDF/A conformance reading report over a generated 100-page document.</summary>
public class HyperPdfConformanceBenchmarks
{
    /// <summary>The pages in the generated document.</summary>
    private const int PageCount = 100;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the generated document.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(TestPdf.Create(PageCount), null);

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the conformance report: XMP, output intents, every object and every page's resources and colour operators.</summary>
    /// <returns>The number of observations.</returns>
    [Benchmark]
    public int GetConformance() => PdfDocumentConformance.GetConformance(_document).Observations.Length;
}
