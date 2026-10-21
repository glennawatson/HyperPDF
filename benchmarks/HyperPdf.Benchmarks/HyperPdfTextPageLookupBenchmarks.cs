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

/// <summary>
/// Measures page hit testing in a margin, in a gap and on a glyph, the first cluster lookup on long plain and Indic pages,
/// and warm page cache lookups on one thread and on four. It uses only members that exist before and after the change so
/// the baseline ref can run the same definition.
/// </summary>
public class HyperPdfTextPageLookupBenchmarks
{
    /// <summary>The characters on the grid page.</summary>
    private const int CharacterCount = 5000;

    /// <summary>The reach of tolerant queries.</summary>
    private const float Tolerance = 4F;

    /// <summary>A point well outside the page.</summary>
    private const float MarginCoordinate = -500F;

    /// <summary>How many times the sample sentence repeats in a long text.</summary>
    private const int TextRepeats = 120;

    /// <summary>The pages kept by the cache under test.</summary>
    private const int CachePages = 16;

    /// <summary>The lookups each thread makes in one cache benchmark call.</summary>
    private const int LookupsPerThread = 2000;

    /// <summary>The thread count of the parallel cache benchmark.</summary>
    private const int Threads = 4;

    /// <summary>The divisor used to pick the middle of a page.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>The fixture document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The page that owns the synthetic characters.</summary>
    private PdfPage _ownerPage = null!;

    /// <summary>The characters of the grid page.</summary>
    private PdfTextChar[] _gridChars = null!;

    /// <summary>The grid page.</summary>
    private PdfTextPage _gridPage = null!;

    /// <summary>A point in the gap between two glyphs.</summary>
    private Vector2 _gapPoint;

    /// <summary>A point on a glyph.</summary>
    private Vector2 _glyphPoint;

    /// <summary>The characters for the long ASCII page.</summary>
    private PdfTextChar[] _asciiChars = null!;

    /// <summary>The long ASCII text.</summary>
    private string _asciiText = string.Empty;

    /// <summary>The characters for the long Indic page.</summary>
    private PdfTextChar[] _indicChars = null!;

    /// <summary>The long Devanagari text.</summary>
    private string _indicText = string.Empty;

    /// <summary>The index of each character in its text, which is the identity.</summary>
    private int[] _identityAscii = null!;

    /// <summary>The index of each character in its text, which is the identity.</summary>
    private int[] _identityIndic = null!;

    /// <summary>The cache under test, with a warm page.</summary>
    private PdfTextPageCache _cache = null!;

    /// <summary>Builds the pages, texts and cache before timing starts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>"),
            null);
        _ownerPage = PdfDocumentPages.GetPage(_document, 0);
        _gridChars = GridData.CreateGrid(CharacterCount);
        _gridPage = CreatePage(_gridChars, string.Empty, []);
        var middle = _gridChars[CharacterCount / MiddleDivisor].Box;
        _glyphPoint = new(middle.Left + 1, middle.Bottom + 1);
        _gapPoint = new(middle.Right + 1, middle.Bottom + 1);

        _asciiText = string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", TextRepeats));
        _indicText = string.Concat(Enumerable.Repeat("क्ष कि नमस्ते ", TextRepeats));
        _asciiChars = GridData.CreateGrid(_asciiText.Length);
        _indicChars = GridData.CreateGrid(_indicText.Length);
        _identityAscii = GridData.Identity(_asciiText.Length);
        _identityIndic = GridData.Identity(_indicText.Length);

        _cache = new(CachePages);
        _ = _cache.GetOrAdd(0, _gridPage, static (_, page) => page);
    }

    /// <summary>Releases the fixture document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Tests a point far outside the page with a tolerance.</summary>
    /// <returns>The hit index.</returns>
    [Benchmark]
    public int GetIndexAtPositionInMarginWithTolerance() =>
        _gridPage.GetIndexAtPosition(new(MarginCoordinate, MarginCoordinate), Tolerance, Tolerance);

    /// <summary>Tests a point in the gap between two glyphs with a tolerance.</summary>
    /// <returns>The hit index.</returns>
    [Benchmark]
    public int GetIndexAtPositionInGapWithTolerance() => _gridPage.GetIndexAtPosition(_gapPoint, Tolerance, Tolerance);

    /// <summary>Tests a point on a glyph with a tolerance.</summary>
    /// <returns>The hit index.</returns>
    [Benchmark]
    public int GetIndexAtPositionOnGlyphWithTolerance() => _gridPage.GetIndexAtPosition(_glyphPoint, Tolerance, Tolerance);

    /// <summary>Builds a long ASCII page and asks for the first cluster start.</summary>
    /// <returns>The cluster start.</returns>
    [Benchmark]
    public int GetClusterStartFirstCallOnAsciiPage() =>
        CreatePage(_asciiChars, _asciiText, _identityAscii).GetClusterStart(_asciiText.Length / MiddleDivisor);

    /// <summary>Builds a long Devanagari page and asks for the first cluster start.</summary>
    /// <returns>The cluster start.</returns>
    [Benchmark]
    public int GetClusterStartFirstCallOnIndicPage() =>
        CreatePage(_indicChars, _indicText, _identityIndic).GetClusterStart(_indicText.Length / MiddleDivisor);

    /// <summary>Looks up a warm cached page repeatedly on one thread.</summary>
    /// <returns>The characters seen.</returns>
    [Benchmark]
    public int GetOrAddWarmPageOneThread() => LookUp();

    /// <summary>Looks up a warm cached page repeatedly from four threads at once.</summary>
    /// <returns>The characters seen.</returns>
    [Benchmark]
    public int GetOrAddWarmPageFourThreads()
    {
        var total = 0;
        _ = Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, _ => Interlocked.Add(ref total, LookUp()));
        return total;
    }

    /// <summary>Makes the lookups of one thread.</summary>
    /// <returns>The characters seen.</returns>
    private int LookUp()
    {
        var seen = 0;
        for (var i = 0; i < LookupsPerThread; i++)
        {
            seen += _cache.GetOrAdd(0, _gridPage, static (_, page) => page).CharCount;
        }

        return seen;
    }

    /// <summary>Constructs a text page from prebuilt characters.</summary>
    /// <param name="chars">The characters.</param>
    /// <param name="text">The page text, one character per text character, or empty.</param>
    /// <param name="charOfText">The character of each text index.</param>
    /// <returns>The text page.</returns>
    private PdfTextPage CreatePage(PdfTextChar[] chars, string text, int[] charOfText) =>
        new(_ownerPage, chars, new int[chars.Length], null, new([], new int[chars.Length], null), text, charOfText);

    /// <summary>Builds the fixed geometry and index lists.</summary>
    private static class GridData
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

        /// <summary>Lists the indexes 0 to count - 1.</summary>
        /// <param name="count">The count.</param>
        /// <returns>The indexes.</returns>
        public static int[] Identity(int count)
        {
            var values = new int[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = i;
            }

            return values;
        }
    }
}
