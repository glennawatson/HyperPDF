// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures writing a warm text page as HTML and Markdown, and laying out right to left spreads.</summary>
public class PdfTextExportBenchmarks
{
    /// <summary>The pages in the layout.</summary>
    private const int LayoutPages = 200;

    /// <summary>The width of a layout page in points.</summary>
    private const float LayoutPageWidth = 612;

    /// <summary>The height of a layout page in points.</summary>
    private const float LayoutPageHeight = 792;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 10;

    /// <summary>The margin around the pages.</summary>
    private const double Margin = 20;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 1000;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The warmed text page.</summary>
    private PdfTextPage _page = null!;

    /// <summary>The page sizes of the layout.</summary>
    private PageSize[] _sizes = [];

    /// <summary>Opens a tagged page and warms its text page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        _page = PdfDocumentText.GetTextPage(_document, 0);
        _sizes = [.. Enumerable.Repeat(new PageSize(LayoutPageWidth, LayoutPageHeight), LayoutPages)];
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Writes the page as HTML.</summary>
    /// <returns>The length of the HTML.</returns>
    [Benchmark]
    public int ToHtml() => PdfTextExport.ToHtml(_page).Length;

    /// <summary>Writes the page as Markdown.</summary>
    /// <returns>The length of the Markdown.</returns>
    [Benchmark]
    public int ToMarkdown() => PdfTextExport.ToMarkdown(_page).Length;

    /// <summary>Lays out two page spreads that read left to right.</summary>
    /// <returns>The page count of the layout.</returns>
    [Benchmark]
    public int LayoutLeftToRight() => DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Dual, 1, Spacing, Margin, Viewport)).PageCount;

    /// <summary>Lays out every page in one row that scrolls sideways.</summary>
    /// <returns>The page count of the layout.</returns>
    [Benchmark]
    public int LayoutHorizontal() =>
        DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { Scroll = ScrollMode.Horizontal }).PageCount;

    /// <summary>Lays out pages that wrap across the viewport.</summary>
    /// <returns>The page count of the layout.</returns>
    [Benchmark]
    public int LayoutWrapped() =>
        DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { Scroll = ScrollMode.Wrapped }).PageCount;

    /// <summary>Lays out two page spreads that read right to left.</summary>
    /// <returns>The page count of the layout.</returns>
    [Benchmark]
    public int LayoutRightToLeft() =>
        DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Dual, 1, Spacing, Margin, Viewport) { RightToLeft = true }).PageCount;
}
