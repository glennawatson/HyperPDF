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

/// <summary>Measures copying one object of a page: the form that paints that object alone, on a page of 60 lines of text.</summary>
public class PageObjectCopyBenchmarks
{
    /// <summary>The lines on the page.</summary>
    private const int Lines = 60;

    /// <summary>The distance between lines.</summary>
    private const int LineSpacing = 10;

    /// <summary>The top of the first line.</summary>
    private const int Top = 780;

    /// <summary>The number of the object that is copied, near the middle of the page.</summary>
    private const int Copied = 30;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder();
        for (var i = 0; i < Lines; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"BT /F1 8 Tf 10 {Top - (i * LineSpacing)} Td (Line {i} of the page) Tj ET\n");
        }

        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, content.ToString()),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the page's objects, which every copy starts with.</summary>
    /// <returns>The number of objects.</returns>
    [Benchmark(Baseline = true)]
    public int ReadObjects() => PdfPageContentReader.Read(_document, 0).Objects.Count;

    /// <summary>Reads the page and builds the form that paints one object alone.</summary>
    /// <returns>The size of the form's data.</returns>
    [Benchmark]
    public int CreateCopyForm()
    {
        var content = PdfPageContentReader.Read(_document, 0);
        return PdfPageContentObjectCopy.CreateForm(content, content.Objects[Copied]).RawLength;
    }
}
