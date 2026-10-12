// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Compares scalar and block hit testing on small and dense pages with different hit positions.</summary>
public class HyperPdfHitTestingBlockBenchmarks
{
    /// <summary>The reach of tolerant queries.</summary>
    private const float Tolerance = 4F;

    /// <summary>The divisor used to select the middle glyph.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>A point well outside the document.</summary>
    private const float FarCoordinate = 10_000F;

    /// <summary>The fixture document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The page that owns the synthetic characters.</summary>
    private PdfPage _ownerPage = null!;

    /// <summary>The synthetic characters.</summary>
    private PdfTextChar[] _chars = null!;

    /// <summary>The fixed run indexes.</summary>
    private int[] _runs = null!;

    /// <summary>The constructed text page.</summary>
    private PdfTextPage _page = null!;

    /// <summary>The selected query point.</summary>
    private Vector2 _point;

    /// <summary>Gets or sets the character count, including a page below the index threshold.</summary>
    [Params(32, 128, 1024, 4096)]
    public int Count { get; set; }

    /// <summary>Gets or sets the hit position or miss being measured.</summary>
    [Params("Early", "Middle", "Late", "Miss", "Tolerance", "Overlap")]
    public string Query { get; set; } = string.Empty;

    /// <summary>Builds a fixed character grid and selects the query before timing starts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = HitTestingBenchmarkData.CreateDocument();
        _ownerPage = PdfDocumentPages.GetPage(_document, 0);
        _chars = HitTestingBenchmarkData.CreateGrid(Count);
        _runs = new int[Count];
        var last = Count - 1;
        var selected = Query switch
        {
            "Early" or "Overlap" => 0,
            "Middle" => Count / MiddleDivisor,
            "Late" => last,
            _ => Count / MiddleDivisor,
        };
        if (Query == "Overlap")
        {
            _chars[last] = _chars[0];
        }

        var box = _chars[selected].Box;
        _point = Query switch
        {
            "Miss" => new(FarCoordinate, FarCoordinate),
            "Tolerance" => new(box.Right + 1, box.Bottom + 1),
            _ => new(box.Left + 1, box.Bottom + 1),
        };
        _page = HitTestingBenchmarkData.CreatePage(_ownerPage, _chars, _runs);
    }

    /// <summary>Releases the small fixture document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Scans each character, using the same scalar code as the indexed search.</summary>
    /// <returns>The hit index.</returns>
    [Benchmark(Baseline = true)]
    public int ScalarQuery() => TextHitTesting.Find(_chars, [], _point, Tolerance, Tolerance);

    /// <summary>Uses contiguous block summaries to skip unrelated character ranges.</summary>
    /// <returns>The hit index.</returns>
    [Benchmark]
    public int BlockQuery() => _page.GetIndexAtPosition(_point, Tolerance, Tolerance);

    /// <summary>Measures page construction and retained index allocation on small and dense pages.</summary>
    public class Construction
    {
        /// <summary>The fixture document.</summary>
        private PdfDocument _document = null!;

        /// <summary>The page that owns the synthetic characters.</summary>
        private PdfPage _ownerPage = null!;

        /// <summary>The synthetic characters.</summary>
        private PdfTextChar[] _chars = null!;

        /// <summary>The fixed run indexes.</summary>
        private int[] _runs = null!;

        /// <summary>Gets or sets the character count, including a page below the index threshold.</summary>
        [Params(32, 128, 1024, 4096)]
        public int Count { get; set; }

        /// <summary>Creates a fixed page before measurement.</summary>
        [GlobalSetup]
        public void Setup()
        {
            _document = HitTestingBenchmarkData.CreateDocument();
            _ownerPage = PdfDocumentPages.GetPage(_document, 0);
            _chars = HitTestingBenchmarkData.CreateGrid(Count);
            _runs = new int[Count];
        }

        /// <summary>Releases the fixture document.</summary>
        [GlobalCleanup]
        public void Cleanup() => _document.Dispose();

        /// <summary>Constructs a page, including its block index when the page is large enough.</summary>
        /// <returns>The page.</returns>
        [Benchmark]
        public PdfTextPage ConstructPage() => HitTestingBenchmarkData.CreatePage(_ownerPage, _chars, _runs);

        /// <summary>Builds the one array retained by an indexed page.</summary>
        /// <returns>The block array.</returns>
        [Benchmark]
        public object BuildIndex() => TextHitTesting.BuildBlocks(_chars);
    }

    /// <summary>Builds fixed geometry for the hit-test benchmarks.</summary>
    private static class HitTestingBenchmarkData
    {
        /// <summary>The number of glyphs per row.</summary>
        private const int CellsPerRow = 64;

        /// <summary>The horizontal pitch of grid boxes.</summary>
        private const float CellWidth = 10F;

        /// <summary>The vertical pitch of grid boxes.</summary>
        private const float CellHeight = 14F;

        /// <summary>The visible width of a glyph box.</summary>
        private const float GlyphWidth = 8F;

        /// <summary>The visible height of a glyph box.</summary>
        private const float GlyphHeight = 10F;

        /// <summary>The nominal font size of a character.</summary>
        private const float FontSize = 10F;

        /// <summary>The character code of the test glyph.</summary>
        private const int CharacterCode = (int)'A';

        /// <summary>Creates a minimal page used only to own a text page.</summary>
        /// <returns>The document.</returns>
        public static PdfDocument CreateDocument() => PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>"),
            null);

        /// <summary>Creates unique character boxes in reading order.</summary>
        /// <param name="count">The character count.</param>
        /// <returns>The characters.</returns>
        public static PdfTextChar[] CreateGrid(int count)
        {
            var chars = new PdfTextChar[count];
            for (var i = 0; i < count; i++)
            {
                var row = Math.DivRem(i, CellsPerRow, out var column);
                var x = column * CellWidth;
                var y = row * CellHeight;
                var box = new PdfRectangle(x, y, x + GlyphWidth, y + GlyphHeight);
                chars[i] = new('A', PdfTextCharKind.Normal, CharacterCode, new(x, y), box, box, FontSize, 0, false);
            }

            return chars;
        }

        /// <summary>Constructs a text page from the prebuilt characters.</summary>
        /// <param name="page">The owning page.</param>
        /// <param name="chars">The characters.</param>
        /// <param name="runs">The existing run indexes.</param>
        /// <returns>The text page.</returns>
        public static PdfTextPage CreatePage(PdfPage page, PdfTextChar[] chars, int[] runs) =>
            new(page, chars, runs, string.Empty, []);
    }
}
