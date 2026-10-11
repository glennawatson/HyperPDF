// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures a warm document-pool acquire through the synchronous and asynchronous viewer paths.</summary>
public class AsyncPipelineBenchmarks
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The pool that owns the open document.</summary>
    private DocumentPool _pool = null!;

    /// <summary>The already-open source.</summary>
    private DocumentSource _source = null!;

    /// <summary>The generated file.</summary>
    private string _path = string.Empty;

    /// <summary>Opens a generated document before measurements begin.</summary>
    /// <returns>The setup task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-async-pipeline-{Environment.ProcessId}.pdf");
        await File.WriteAllBytesAsync(_path, TestPdf.Create(Pages));
        _pool = new(new HyperPdfEngine());
        _source = _pool.Create(_path, null);
        _ = await _source.AcquireAsync(CancellationToken.None);
    }

    /// <summary>Closes the document and deletes the generated file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pool.Dispose();
        File.Delete(_path);
    }

    /// <summary>Gets a ready document synchronously.</summary>
    /// <returns>The document.</returns>
    [Benchmark(Baseline = true)]
    public IDocument WarmAcquire() => _source.Acquire();

    /// <summary>Gets the same ready document through a synchronously completed ValueTask.</summary>
    /// <returns>The document.</returns>
    [Benchmark]
    public ValueTask<IDocument> WarmAcquireAsync() => _source.AcquireAsync(CancellationToken.None);
}
