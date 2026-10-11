// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures text-page extraction as the count of adjacent spaces grows.</summary>
public class HyperPdfSpaceBenchmarks
{
    /// <summary>The repeated-space document.</summary>
    private PdfDocument? _repeated;

    /// <summary>The ordinary body-text document.</summary>
    private PdfDocument? _body;

    /// <summary>Gets or sets the number of spaces between two visible glyphs.</summary>
    [Params(0, 32, 256, 1024)]
    public int Spaces { get; set; }

    /// <summary>Opens two documents and warms their fonts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _repeated = PdfDocumentReader.Open(CreateDocument($"A{new string(' ', Spaces)}B"), null);
        _body = PdfDocumentReader.Open(CreateDocument("The quick brown fox jumps over the lazy dog while reading a long accessible document"), null);
        _ = PdfDocumentText.GetTextPage(_repeated, 0);
        _ = PdfDocumentText.GetTextPage(_body, 0);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _repeated?.Dispose();
        _body?.Dispose();
    }

    /// <summary>Extracts a page with the configured run of adjacent spaces.</summary>
    /// <returns>The extracted character count.</returns>
    [Benchmark]
    public int RepeatedSpaces() => PdfDocumentText.ExtractText(_repeated!, 0).CharCount;

    /// <summary>Extracts normal body text through the same path.</summary>
    /// <returns>The extracted character count.</returns>
    [Benchmark]
    public int BodyText() => PdfDocumentText.ExtractText(_body!, 0).CharCount;

    /// <summary>Creates a one-page PDF with the chosen text.</summary>
    /// <param name="value">The printable text.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateDocument(string value) => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, $"BT /F1 10 Tf 72 720 Td ({value}) Tj ET"),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
}
