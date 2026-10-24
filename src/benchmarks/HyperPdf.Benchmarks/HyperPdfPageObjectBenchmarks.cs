// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading a page of body text into page objects, writing it again with the original bytes kept, writing every
/// object again from the model, and writing it after one text object lost a glyph and another moved.
/// </summary>
public class HyperPdfPageObjectBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 48;

    /// <summary>The line spacing.</summary>
    private const int Leading = 14;

    /// <summary>The left margin and the baseline of the first line.</summary>
    private const int Margin = 72;

    /// <summary>The page height.</summary>
    private const int PageHeight = 792;

    /// <summary>The words each line is made from.</summary>
    private const string LineText = "The quick brown fox jumps over the lazy dog while reading a long accessible document";

    /// <summary>The document.</summary>
    private PdfDocument? _document;

    /// <summary>The page's content, read once for the write benchmarks.</summary>
    private PdfPageContent? _content;

    /// <summary>Opens the document and reads the page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(CreatePage(), null);
        _content = PdfDocumentPageContent.GetPageContent(_document, 0);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Reads the page into objects.</summary>
    /// <returns>The object count, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int Parse() => PdfDocumentPageContent.GetPageContent(_document!, 0).Objects.Count;

    /// <summary>Writes the content again, copying the bytes of objects nobody changed.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int RegeneratePreserve() => PdfPageContentWriter.Regenerate(_content!, PdfRegenerateMode.Preserve).Length;

    /// <summary>Writes every object again from the model.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int RegenerateRewrite() => PdfPageContentWriter.Regenerate(_content!, PdfRegenerateMode.Rewrite).Length;

    /// <summary>Reads the page, removes a glyph, moves another object and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int EditAndRegenerate()
    {
        var content = PdfDocumentPageContent.GetPageContent(_document!, 0);
        _ = ((PdfTextObject)content.Objects[0]).RemoveGlyph(1);
        content.Objects[1].Translate(0, 1);
        return PdfPageContentWriter.Regenerate(content).Length;
    }

    /// <summary>Builds a page of body text.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePage()
    {
        var content = new StringBuilder("BT /F1 10 Tf\n");
        for (var line = 0; line < Lines; line++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"1 0 0 1 {Margin} {PageHeight - Margin - (line * Leading)} Tm ({LineText}) Tj\n");
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
    }
}
