// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the text bounds work behind one selection drag move over about 5,000 characters on three pages.
/// The canvas itself is in the app project, so these call the document the way the canvas does.
/// </summary>
public class SelectionRectsBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 3;

    /// <summary>The text lines on each page.</summary>
    private const int LinesPerPage = 60;

    /// <summary>The characters left out of the front of the first page.</summary>
    private const int FirstPageSkip = 500;

    /// <summary>The characters selected on the last page.</summary>
    private const int LastPageCharacters = 340;

    /// <summary>The last page, where the drag ends.</summary>
    private const int LastPage = 2;

    /// <summary>The bounds of one page, reused between calls.</summary>
    private readonly List<PageRect> _rects = [];

    /// <summary>The document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The character count of each page.</summary>
    private int[] _counts = null!;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateTextFilled(PageCount, LinesPerPage));
        _counts = new int[PageCount];
        for (var page = 0; page < PageCount; page++)
        {
            _counts[page] = _document.Document.GetCharacterCount(page);
        }
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Works out the bounds on all three pages, as every drag move did before only the changed pages were rebuilt.</summary>
    /// <returns>The number of rectangles on the last page.</returns>
    [Benchmark(Baseline = true)]
    public int SelectionBoundsOnAllThreePages()
    {
        var document = _document.Document;
        document.GetTextBounds(0, FirstPageSkip, _counts[0] - FirstPageSkip, Cleared());
        document.GetTextBounds(1, 0, _counts[1], Cleared());
        document.GetTextBounds(LastPage, 0, LastPageCharacters, Cleared());
        return _rects.Count;
    }

    /// <summary>Works out the bounds on the last page only, as a drag move that stays on the last page does now.</summary>
    /// <returns>The number of rectangles on the last page.</returns>
    [Benchmark]
    public int SelectionBoundsOnTheFocusPageOnly()
    {
        _document.Document.GetTextBounds(LastPage, 0, LastPageCharacters, Cleared());
        return _rects.Count;
    }

    /// <summary>Empties and returns the reused list.</summary>
    /// <returns>The list.</returns>
    private List<PageRect> Cleared()
    {
        _rects.Clear();
        return _rects;
    }
}
