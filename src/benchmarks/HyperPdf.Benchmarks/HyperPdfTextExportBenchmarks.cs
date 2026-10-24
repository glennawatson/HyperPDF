// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Finds the web links in, and exports as Markdown and HTML, a page of 150 lines that hold web and email addresses.</summary>
public class HyperPdfTextExportBenchmarks
{
    /// <summary>The lines on the page.</summary>
    private const int Lines = 150;

    /// <summary>The distance between lines.</summary>
    private const int LineSpacing = 5;

    /// <summary>The top of the first line.</summary>
    private const int Top = 780;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The extracted page text.</summary>
    private PdfTextPage _page = null!;

    /// <summary>Builds the document and extracts its text outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder();
        for (var i = 0; i < Lines; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"BT /F1 4 Tf 10 {Top - (i * LineSpacing)} Td (Line {i} www.example.com/p{i} user{i}@example.org *this*) Tj ET\n");
        }

        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, content.ToString()),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            null);
        _page = PdfDocumentText.GetTextPage(_document, 0);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Finds the web and email addresses on the page, which the page does once and keeps.</summary>
    /// <returns>The links found.</returns>
    [Benchmark]
    public int FindWebLinks()
    {
        PdfDocumentPageContent.InvalidatePageContent(_document);
        return PdfDocumentText.GetTextPage(_document, 0).GetWebLinks().Count;
    }

    /// <summary>Extracts the page again without finding links, which is the cost the link search includes.</summary>
    /// <returns>The characters on the page.</returns>
    [Benchmark(Baseline = true)]
    public int ExtractOnly()
    {
        PdfDocumentPageContent.InvalidatePageContent(_document);
        return PdfDocumentText.GetTextPage(_document, 0).CharCount;
    }

    /// <summary>Reads the page's text as page objects, as the editing tools do.</summary>
    /// <returns>The objects read.</returns>
    [Benchmark]
    public int ReadPageObjects() => PdfPageContentReader.Read(_document, 0).Objects.Count;

    /// <summary>Exports the page as Markdown.</summary>
    /// <returns>The length of the text.</returns>
    [Benchmark]
    public int ExportMarkdown() => PdfTextExport.ToMarkdown(_page).Length;

    /// <summary>Exports the page as HTML.</summary>
    /// <returns>The length of the text.</returns>
    [Benchmark]
    public int ExportHtml() => PdfTextExport.ToHtml(_page).Length;
}
