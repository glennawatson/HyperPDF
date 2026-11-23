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
/// Measures shuffling a file's pages in among a document's pages through the engine's page manager. Each iteration opens the
/// document again, because a shuffle keeps the file it took pages from so redo can put them back, and undo does not let it go.
/// </summary>
[InvocationCount(InvocationsPerIteration)]
public class HyperPdfPageInterleaveBenchmarks
{
    /// <summary>The shuffles measured in each iteration, on one opened document.</summary>
    private const int InvocationsPerIteration = 16;

    /// <summary>The pages of the document.</summary>
    private const int Pages = 20;

    /// <summary>The pages of the file shuffled in.</summary>
    private const int ShuffledPages = 10;

    /// <summary>The document's bytes.</summary>
    private readonly byte[] _pdf = TestPdf.Create(Pages);

    /// <summary>The folder for the file shuffled in.</summary>
    private string _folder = string.Empty;

    /// <summary>The file shuffled in.</summary>
    private string _shuffled = string.Empty;

    /// <summary>The document of this iteration.</summary>
    private HyperPdfDocument? _document;

    /// <summary>The document's page manager.</summary>
    private IPageManager _manager = null!;

    /// <summary>Writes the file shuffled in.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-interleave-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _shuffled = Path.Combine(_folder, "backs.pdf");
        File.WriteAllBytes(_shuffled, TestPdf.Create(ShuffledPages));
    }

    /// <summary>Removes the file shuffled in.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document?.Dispose();
        Directory.Delete(_folder, true);
    }

    /// <summary>Opens the document for this iteration.</summary>
    [IterationSetup]
    public void Open()
    {
        _document?.Dispose();
        _document = new(PdfDocumentReader.Open(_pdf, null), "interleave.pdf");
        _manager = HyperPdfDocumentPageManagement.GetPageManager(_document);
    }

    /// <summary>Shuffles the file's pages in, then undoes it.</summary>
    /// <returns>The page count after the undo.</returns>
    /// <exception cref="InvalidOperationException">The shuffle could not be undone.</exception>
    [Benchmark]
    public async ValueTask<int> ShuffleInAndUndo()
    {
        PageEditReport report = new();
        _ = await _manager.Interleave(_shuffled, report).ToTask().ConfigureAwait(false);
        if (!await _manager.Undo().ToTask().ConfigureAwait(false))
        {
            throw new InvalidOperationException("The shuffle could not be undone.");
        }

        return _document!.PageCount;
    }
}
