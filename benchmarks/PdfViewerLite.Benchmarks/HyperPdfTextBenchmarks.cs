// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Text;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures HyperPDF text pages against PDFium on a page of body text: building the text page, searching it and hit
/// testing. Until the library loads real fonts, Helvetica is played by <see cref="HyperPdfStandInFont"/>, which has
/// Helvetica's widths, so the work matches a real page.
/// </summary>
public class HyperPdfTextBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 48;

    /// <summary>Every how many lines a line is plain; the others are kerned.</summary>
    private const int KernedEvery = 2;

    /// <summary>The font size.</summary>
    private const int FontSize = 10;

    /// <summary>The line spacing.</summary>
    private const int Leading = 14;

    /// <summary>The left margin and the baseline of the first line.</summary>
    private const int Margin = 72;

    /// <summary>The page height.</summary>
    private const int PageHeight = 792;

    /// <summary>The page width.</summary>
    private const int PageWidth = 612;

    /// <summary>The words each line is made from.</summary>
    private const string LineText = "The quick brown fox jumps over the lazy dog while reading a long accessible document";

    /// <summary>The phrase searched for.</summary>
    private const string Query = "lazy dog";

    /// <summary>The x of the hit-test point.</summary>
    private const float HitX = 300;

    /// <summary>The y of the hit-test point.</summary>
    private const float HitY = 400;

    /// <summary>The hit-test tolerance.</summary>
    private const float HitTolerance = 2;

    /// <summary>The reused PDFium match list.</summary>
    private readonly List<TextMatch> _pdfiumMatches = [];

    /// <summary>The reused HyperPDF match list.</summary>
    private readonly List<PdfTextMatch> _matches = [];

    /// <summary>The document on disk, for PDFium.</summary>
    private string _path = string.Empty;

    /// <summary>The document opened with PDFium.</summary>
    private IDocument _pdfium = null!;

    /// <summary>The document opened through the managed viewer adapter.</summary>
    private IDocument _hyperPdf = null!;

    /// <summary>The document opened with HyperPDF.</summary>
    private PdfDocument _document = null!;

    /// <summary>The cached text page.</summary>
    private PdfTextPage _page = null!;

    /// <summary>Writes the page and opens it with both engines, building each text page once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = CreateDocument();
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-text-bench-{Environment.ProcessId}.pdf");
        File.WriteAllBytes(_path, bytes);
        _pdfium = new PdfiumEngine().Open(_path, null);
        _ = _pdfium.GetCharacterCount(0);
        _hyperPdf = new HyperPdfEngine().Open(_path, null);
        _ = _hyperPdf.GetCharacterCount(0);
        _document = PdfDocumentReader.Open(bytes, null);
        PdfFont.Factory ??= HyperPdfStandInFont.Create;
        _page = PdfDocumentText.GetTextPage(_document, 0);
    }

    /// <summary>Closes the documents and deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pdfium.Dispose();
        _hyperPdf.Dispose();
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Builds the page's text page with HyperPDF, fonts already loaded.</summary>
    /// <returns>The character count.</returns>
    [Benchmark]
    public int HyperPdfBuildTextPage() => PdfDocumentText.ExtractText(_document, 0).CharCount;

    /// <summary>Finds a phrase with PDFium, whose text page is already loaded.</summary>
    /// <returns>The match count.</returns>
    [Benchmark(Baseline = true)]
    public int PdfiumFind()
    {
        _pdfiumMatches.Clear();
        _pdfium.Find(0, Query, SearchOptions.None, _pdfiumMatches);
        return _pdfiumMatches.Count;
    }

    /// <summary>Finds a phrase with HyperPDF on the cached text page.</summary>
    /// <returns>The match count.</returns>
    [Benchmark]
    public int HyperPdfFind()
    {
        _matches.Clear();
        _page.Find(Query, PdfTextSearchOptions.None, _matches);
        return _matches.Count;
    }

    /// <summary>Hit tests a point with PDFium.</summary>
    /// <returns>The character index.</returns>
    [Benchmark]
    public int PdfiumHitTest() => _pdfium.GetCharacterIndexAt(0, new(HitX, PageHeight - HitY), HitTolerance);

    /// <summary>Hit tests a point with HyperPDF on the cached text page.</summary>
    /// <returns>The character index.</returns>
    [Benchmark]
    public int HyperPdfHitTest() => _page.GetIndexAtPosition(new(HitX, HitY), HitTolerance, HitTolerance);

    /// <summary>Hit tests through the managed viewer adapter, including its page lookup and coordinate conversion.</summary>
    /// <returns>The character index.</returns>
    [Benchmark]
    public int HyperPdfAdapterHitTest() => _hyperPdf.GetCharacterIndexAt(0, new(HitX, PageHeight - HitY), HitTolerance);

    /// <summary>Makes a page of body text, alternating plain and kerned show operators.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateDocument()
    {
        var split = LineText.IndexOf(' ', StringComparison.Ordinal);
        var kerned = $"[({LineText[..split]}) -250 ({LineText[(split + 1)..]})] TJ T* ";
        const string plain = $"({LineText}) Tj T* ";
        var content = new StringBuilder();
        _ = content.Append(CultureInfo.InvariantCulture, $"BT /F1 {FontSize} Tf {Leading} TL {Margin} {PageHeight - Margin} Td ");
        for (var i = 0; i < Lines; i++)
        {
            _ = content.Append(i % KernedEvery == 0 ? plain : kerned);
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
    }
}
