// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Compares opening a document, and opening it then rendering page 1, through the synchronous API and the async-first
/// API, over each source kind. Every operation opens the file afresh, so the document's own caches are cold; the
/// operating system's file cache is warm.
/// </summary>
public class HyperPdfAsyncOpenBenchmarks
{
    /// <summary>The pixels of the shared render buffer.</summary>
    private readonly byte[] _pixels = new byte[HyperPdfAsyncSamples.MaxWidth * HyperPdfAsyncSamples.MaxHeight * HyperPdfAsyncSamples.BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The file.</summary>
    private string _path = string.Empty;

    /// <summary>Gets or sets the sample: Report, Scan, Form or Book.</summary>
    [Params("Report", "Scan", "Form", "Book")]
    public string Document { get; set; } = "Report";

    /// <summary>Gets or sets how the file is read: Mapped, FileStream, Memory or UserStream.</summary>
    [Params("Mapped", "FileStream", "Memory", "UserStream")]
    public string Source { get; set; } = "Mapped";

    /// <summary>Finds the file.</summary>
    [GlobalSetup]
    public void Setup() => _path = HyperPdfAsyncSamples.PathFor(Document);

    /// <summary>Opens the file and reads the page count.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int OpenSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        return opened.Document.PageCount;
    }

    /// <summary>Opens the file with async I/O and reads the page count.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public async ValueTask<int> OpenAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        return opened.Document.PageCount;
    }

    /// <summary>Opens the file and renders page 1.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark]
    public bool OpenRenderFirstSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        using var renderer = new PdfPageRenderer(opened.Document);
        return HyperPdfAsyncSamples.RenderSync(renderer, opened.Document, 0, _pixels);
    }

    /// <summary>Opens the file with async I/O and renders page 1 with the async API.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark]
    public async ValueTask<bool> OpenRenderFirstAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        using var renderer = new PdfPageRenderer(opened.Document);
        return await HyperPdfAsyncSamples.RenderAsync(renderer, opened.Document, 0, _pixels, CancellationToken.None).ConfigureAwait(false);
    }
}
