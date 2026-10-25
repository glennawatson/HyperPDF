// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures multi-page edits and undo through the adapter's asynchronous page manager.</summary>
public class HyperPdfPageManagementBenchmarks
{
    /// <summary>The pages in the existing editing benchmark fixture.</summary>
    private const int Pages = 100;

    /// <summary>The pages selected in each action.</summary>
    private const int SelectedPages = 10;

    /// <summary>The gap between selected pages.</summary>
    private const int SelectionStep = 2;

    /// <summary>The clockwise turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>The selected pages, preserved in ascending order.</summary>
    private readonly int[] _selection = CreateSelection();

    /// <summary>The adapter owning the edit history.</summary>
    private HyperPdfDocument? _document;

    /// <summary>The public page-management interface.</summary>
    private IPageManager? _manager;

    /// <summary>Gets or sets the action measured together with its undo.</summary>
    [Params(PageEditKind.Rotate, PageEditKind.Delete, PageEditKind.Move, PageEditKind.Duplicate)]
    public PageEditKind Kind { get; set; }

    /// <summary>Opens the same generated fixture as the core editing benchmarks and warms the selected action.</summary>
    /// <returns>A task completing after the warm edit and undo.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _document = new(PdfDocumentReader.Open(TestPdf.Create(Pages), null), "page-management-benchmark.pdf");
        _manager = HyperPdfDocumentPageManagement.GetPageManager(_document);
        _ = await ApplyAndUndo().ConfigureAwait(false);
    }

    /// <summary>Closes the document and any retained sources.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Applies the selected multi-page action and undoes it, leaving the original page order.</summary>
    /// <returns>The restored page count.</returns>
    /// <exception cref="InvalidOperationException">The edit cannot be undone.</exception>
    [Benchmark]
    public async ValueTask<int> ApplyAndUndo()
    {
        var manager = _manager!;
        var value = Kind == PageEditKind.Rotate ? QuarterTurn : Pages - SelectedPages;
        await manager.ApplyAsync(new(Kind, _selection, value), CancellationToken.None).ConfigureAwait(false);
        if (!await manager.UndoAsync(CancellationToken.None).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The page edit could not be undone.");
        }

        return _document!.PageCount;
    }

    /// <summary>Creates a spaced multi-page selection.</summary>
    /// <returns>The selected page indexes.</returns>
    private static int[] CreateSelection()
    {
        var selected = new int[SelectedPages];
        for (var index = 0; index < selected.Length; index++)
        {
            selected[index] = (index * SelectionStep) + 1;
        }

        return selected;
    }
}
