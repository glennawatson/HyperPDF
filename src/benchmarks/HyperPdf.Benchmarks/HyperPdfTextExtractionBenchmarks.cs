// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Extracts the text of a page of 60 lines the first time, with the fonts and glyphs loaded for the first time too, and again with the page's text dropped but its fonts kept.</summary>
public class HyperPdfTextExtractionBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 60;

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The document the repeat extraction uses.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdf = TestPdf.CreateTextFilled(1, Lines);
        _document = PdfDocumentReader.Open(_pdf, null);
        _ = PdfDocumentText.GetTextPage(_document, 0).CharCount;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Opens the document and extracts the page's text, loading its fonts and glyph outlines.</summary>
    /// <returns>The characters on the page.</returns>
    [Benchmark(Baseline = true)]
    public int OpenAndExtract()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        return PdfDocumentText.GetTextPage(document, 0).CharCount;
    }

    /// <summary>Drops the page's extracted text and extracts it again with the fonts and glyphs already loaded.</summary>
    /// <returns>The characters on the page.</returns>
    [Benchmark]
    public int ExtractAgain()
    {
        PdfDocumentPageContent.InvalidatePageContent(_document);
        return PdfDocumentText.GetTextPage(_document, 0).CharCount;
    }
}
