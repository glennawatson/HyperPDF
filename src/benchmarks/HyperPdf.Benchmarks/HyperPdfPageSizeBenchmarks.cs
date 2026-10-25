// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the engine's page size bookkeeping on a long document: building the engine document, refreshing after an
/// edit that touches no page, and reading one page's size the way the OCR loop does.
/// </summary>
public class HyperPdfPageSizeBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 3000;

    /// <summary>The pages whose size the OCR loop reads.</summary>
    private const int OcrPages = 200;

    /// <summary>The managed document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The engine document over it.</summary>
    private HyperPdfDocument _engine = null!;

    /// <summary>Opens the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _engine = new(_document, "page-size-benchmark.pdf");
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Builds the engine document, which reads every page's size.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int BuildEngineDocument() => new HyperPdfDocument(_document, "page-size-benchmark.pdf").PageCount;

    /// <summary>Refreshes the engine after an edit that changes no page.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int PagesEditedWithoutPageChange()
    {
        HyperPdfNavigation.PagesEdited(_engine, PdfCacheScopes.None);
        return _engine.PageCount;
    }

    /// <summary>Reads one page's size at a time by copying every size, the way callers did before <c>GetPageSize</c>.</summary>
    /// <returns>The sum of the widths.</returns>
    [Benchmark(Baseline = true)]
    public double ReadSizeOfEachPageByCopyingAll()
    {
        var sum = 0.0;
        for (var i = 0; i < OcrPages; i++)
        {
            sum += _engine.GetPageSizes()[i].Width;
        }

        return sum;
    }

    /// <summary>Reads one page's size at a time, as the OCR loop does.</summary>
    /// <returns>The sum of the widths.</returns>
    [Benchmark]
    public double ReadSizeOfEachPage()
    {
        var sum = 0.0;
        for (var i = 0; i < OcrPages; i++)
        {
            sum += _engine.GetPageSize(i).Width;
        }

        return sum;
    }
}
