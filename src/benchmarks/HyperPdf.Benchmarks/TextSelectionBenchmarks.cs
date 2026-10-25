// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Reading;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures one selection drag move through <see cref="TextSelection"/> over about 5,000 characters on three pages, the work
/// <see cref="SelectionRectsBenchmarks"/> measures at the document call. The two together show what the selection's own bookkeeping adds.
/// </summary>
public class TextSelectionBenchmarks
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

    /// <summary>The selection under test.</summary>
    private readonly TextSelection _selection = new();

    /// <summary>The document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>Which of two focus positions the next move goes to.</summary>
    private bool _flip;

    /// <summary>Opens the document and starts a selection across all three pages.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateTextFilled(PageCount, LinesPerPage));
        _selection.Begin(new(0, FirstPageSkip));
        _ = _selection.MoveFocus(new(LastPage, LastPageCharacters));
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Moves the end of the selection within the last page and reads the rectangles of all three pages, as one drag move does.</summary>
    /// <returns>The number of rectangles on the last page.</returns>
    [Benchmark]
    public int DragMoveOnTheLastPage()
    {
        _flip = !_flip;
        _ = _selection.MoveFocus(new(LastPage, _flip ? LastPageCharacters : LastPageCharacters - 1));
        var document = _document.Document;
        _ = _selection.GetRects(document, 0);
        _ = _selection.GetRects(document, 1);
        return _selection.GetRects(document, LastPage).Count;
    }

    /// <summary>Reads the rectangles of the three pages again with nothing changed, as a repaint does.</summary>
    /// <returns>The number of rectangles on the last page.</returns>
    [Benchmark]
    public int RepaintWithNothingChanged()
    {
        var document = _document.Document;
        _ = _selection.GetRects(document, 0);
        _ = _selection.GetRects(document, 1);
        return _selection.GetRects(document, LastPage).Count;
    }

    /// <summary>Reads the selected text.</summary>
    /// <returns>The text length.</returns>
    [Benchmark]
    public int SelectedText() => _selection.GetText(_document.Document).Length;
}
