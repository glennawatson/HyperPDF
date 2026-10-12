// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures saving a document as PDF/A: a one-page document with a JavaScript action and no output intent, the same
/// page with non-embedded Helvetica, which embeds a font subset, and a 100-page document.
/// </summary>
public class HyperPdfPdfAConversionBenchmarks
{
    /// <summary>The pages in the long document.</summary>
    private const int Pages = 100;

    /// <summary>The page size of the one-page documents.</summary>
    private const int PageSize = 200;

    /// <summary>A page with a script and no fonts.</summary>
    private PdfDocument _scripts = null!;

    /// <summary>A page with non-embedded Helvetica.</summary>
    private PdfDocument _text = null!;

    /// <summary>A 100-page document.</summary>
    private PdfDocument _book = null!;

    /// <summary>Opens the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var scripts = new RenderTestPdf(PageSize, PageSize) { Content = "0 0 1 rg 10 10 50 50 re f" };
        var script = scripts.AddObject("<< /S /JavaScript /JS (app.alert\\('hi'\\);) >>");
        scripts.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/OpenAction {script} 0 R");
        _scripts = PdfDocumentReader.Open(scripts.ToBytes(), null);
        var text = new RenderTestPdf(PageSize, PageSize);
        var font = text.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        text.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >>");
        text.Content = "BT /F1 24 Tf 10 100 Td (Hello PDF/A) Tj ET";
        _text = PdfDocumentReader.Open(text.ToBytes(), null);
        _book = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _scripts.Dispose();
        _text.Dispose();
        _book.Dispose();
    }

    /// <summary>Converts the page with a script to PDF/A-2b.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public long ScriptsPdfA2B() => Convert(_scripts, PdfALevel.PdfA2B);

    /// <summary>Converts the page with non-embedded Helvetica to PDF/A-2u, embedding a font subset.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long FontEmbeddingPdfA2U() => Convert(_text, PdfALevel.PdfA2U);

    /// <summary>Converts the 100-page document to PDF/A-4.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long HundredPagesPdfA4() => Convert(_book, PdfALevel.PdfA4);

    /// <summary>Converts a document into memory.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <returns>The bytes written.</returns>
    private static long Convert(PdfDocument document, PdfALevel level)
    {
        using var stream = new MemoryStream();
        _ = PdfAConverter.SaveAsPdfA(document, level, stream);
        return stream.Length;
    }
}
