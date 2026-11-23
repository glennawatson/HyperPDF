// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures deleting a page and undoing it in a document whose page tree already has intermediate nodes, as it does after its
/// first page edit. Each iteration opens the document again and makes that first edit before the measured ones.
/// </summary>
[InvocationCount(InvocationsPerIteration)]
public class PageTreeRewriteBenchmarks
{
    /// <summary>The edits measured in each iteration, on one opened document.</summary>
    private const int InvocationsPerIteration = 16;

    /// <summary>The pages of the document, more than one node holds.</summary>
    private const int Pages = 100;

    /// <summary>The page deleted by each measured edit.</summary>
    private const int Deleted = 10;

    /// <summary>The document's bytes.</summary>
    private readonly byte[] _pdf = TestPdf.Create(Pages);

    /// <summary>The selection of the page deleted.</summary>
    private readonly int[] _deleted = [Deleted];

    /// <summary>The selection of the last page, deleted before the measured edits.</summary>
    private readonly int[] _last = [Pages - 1];

    /// <summary>The document of this iteration.</summary>
    private HyperPdfDocument? _document;

    /// <summary>The document's page manager.</summary>
    private IPageManager _manager = null!;

    /// <summary>Opens the document and makes its first page edit, which gives its page tree intermediate nodes.</summary>
    [IterationSetup]
    public void Open()
    {
        _document?.Dispose();
        var opened = PdfDocumentReader.Open(_pdf, null);
        PdfDocumentPageOperations.DeletePages(opened, _last);
        _document = new(opened, "page-tree-rewrite.pdf");
        _manager = HyperPdfDocumentPageManagement.GetPageManager(_document);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Deletes a page and undoes it.</summary>
    /// <returns>The page count after the undo.</returns>
    /// <exception cref="InvalidOperationException">The deletion could not be undone.</exception>
    [Benchmark]
    public async ValueTask<int> DeleteAndUndo()
    {
        await _manager.Apply(new(PageEditKind.Delete, _deleted, 0)).ToTask().ConfigureAwait(false);
        if (!await _manager.Undo().ToTask().ConfigureAwait(false))
        {
            throw new InvalidOperationException("The deletion could not be undone.");
        }

        return _document!.PageCount;
    }
}
