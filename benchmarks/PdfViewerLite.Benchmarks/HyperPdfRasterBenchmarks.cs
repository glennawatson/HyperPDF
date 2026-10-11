// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Raster;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures the raster-only report on a 900-page scanned court report (the Internet Archive's United States Reports
/// volume 341). The operator scan reads each page's content operators and never decodes an image. The interpreter
/// route runs the content interpreter with the text device, which skips images, so it cannot list them; it is timed
/// as the floor for any interpreter-based design. Does nothing when the corpus file is not cached.
/// </summary>
public class HyperPdfRasterBenchmarks
{
    /// <summary>The corpus file id.</summary>
    private const string CorpusId = "ia-us-reports-341";

    /// <summary>The open document, or null when the file is not cached.</summary>
    private PdfDocument? _document;

    /// <summary>Opens the corpus file.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "reference-pdfs", $"{CorpusId}.pdf");
        _document = File.Exists(path) ? PdfDocumentReader.Open(path, null) : null;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Gets the page count, to keep the interpreter route comparable.</summary>
    /// <returns>The pages scanned.</returns>
    [Benchmark(Baseline = true)]
    public int OperatorScan() => _document is null ? 0 : RasterReportBuilder.Build(_document, false, CancellationToken.None).Pages.Count;

    /// <summary>Scans the operators and extracts the text of pages with an invisible text layer.</summary>
    /// <returns>The pages scanned.</returns>
    [Benchmark]
    public int OperatorScanWithText() => _document is null ? 0 : RasterReportBuilder.Build(_document, true, CancellationToken.None).Pages.Count;

    /// <summary>Runs the content interpreter with the text device over every page.</summary>
    /// <returns>The characters found.</returns>
    [Benchmark]
    public int InterpreterTextPass()
    {
        if (_document is null)
        {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < _document.PageCount; i++)
        {
            total += PdfDocumentText.ExtractText(_document, i).CharCount;
        }

        return total;
    }
}
