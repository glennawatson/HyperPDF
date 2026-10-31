// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures Edit mode on a page: listing its objects, and each change together with its undo.</summary>
public class PageContentEditBenchmarks
{
    /// <summary>The points an object is moved in the move benchmark.</summary>
    private const float Shift = 12;

    /// <summary>The words written in the retype benchmark.</summary>
    private const string Replacement = "Changed words";

    /// <summary>The adapter owning the document and its edit history.</summary>
    private HyperPdfDocument? _document;

    /// <summary>The page content editor.</summary>
    private HyperPdfPageContentEditorService? _editor;

    /// <summary>The page manager whose undo steps the history.</summary>
    private IPageManager? _manager;

    /// <summary>The first text object of page one.</summary>
    private PageObjectInfo _text;

    /// <summary>Opens a tagged page with text and warms each action once.</summary>
    /// <returns>A task completing after the warm-up.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _document = new(PdfDocumentReader.Open(TestPdf.CreateTagged(), null), "page-content-benchmark.pdf");
        _editor = new(_document);
        _manager = HyperPdfDocumentPageManagement.GetPageManager(_document);
        _text = Array.Find(await _editor.ReadObjectsAsync(0, CancellationToken.None).ConfigureAwait(false), static o => o.Kind == PageObjectKind.Text);
        _ = await MoveAndUndo().ConfigureAwait(false);
        _ = await ReplaceAndUndo().ConfigureAwait(false);
        _ = await DeleteAndUndo().ConfigureAwait(false);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Lists the objects of the page, as hovering the page does the first time.</summary>
    /// <returns>The number of objects.</returns>
    [Benchmark]
    public async ValueTask<int> ReadObjects() => (await _editor!.ReadObjectsAsync(0, CancellationToken.None).ConfigureAwait(false)).Length;

    /// <summary>Moves the first text object and undoes the move.</summary>
    /// <returns>Whether the move was undone.</returns>
    [Benchmark]
    public async ValueTask<bool> MoveAndUndo()
    {
        await _editor!.MoveAsync(0, _text.Index, Shift, Shift, CancellationToken.None).ConfigureAwait(false);
        return await _manager!.UndoAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Retypes the first text object and undoes the change.</summary>
    /// <returns>Whether the change was undone.</returns>
    [Benchmark]
    public async ValueTask<bool> ReplaceAndUndo()
    {
        await _editor!.ReplaceTextAsync(0, _text.Index, Replacement, CancellationToken.None).ConfigureAwait(false);
        return await _manager!.UndoAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Removes the first text object and undoes the removal.</summary>
    /// <returns>Whether the removal was undone.</returns>
    [Benchmark]
    public async ValueTask<bool> DeleteAndUndo()
    {
        await _editor!.DeleteAsync(0, _text.Index, CancellationToken.None).ConfigureAwait(false);
        return await _manager!.UndoAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
