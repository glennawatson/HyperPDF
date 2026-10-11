// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// The tab-swap case: the user opens a heavy document, then switches away 60 ms later. Each pair runs the same work
/// two ways. The synchronous form cannot be stopped, so it runs to the end; the async form is cancelled after 60 ms. The
/// time of each operation is the time until the work has stopped, so the async form's mean minus 60 ms is its cancel
/// latency, and the difference between the pair is the work that would have been wasted. The allocation audit gives the
/// bytes wasted after the cancel.
/// </summary>
public class HyperPdfAsyncCancelBenchmarks
{
    /// <summary>The wait before the cancel, in milliseconds.</summary>
    private const int CancelAfterMilliseconds = 60;

    /// <summary>The pages of the document with a huge page tree.</summary>
    private const int ManyPageCount = 150_000;

    /// <summary>The content bytes of each page of the huge page tree.</summary>
    private const int ManyPageContent = 16;

    /// <summary>The pages of the heavy document.</summary>
    private const int HeavyPages = 2;

    /// <summary>The content bytes of each heavy page.</summary>
    private const int HeavyContent = 8 * 1024 * 1024;

    /// <summary>The wait before each read of the slow stream completes, in milliseconds.</summary>
    private const int ReadLatencyMilliseconds = 2;

    /// <summary>The pages of the small document.</summary>
    private const int SmallPages = 3;

    /// <summary>The result when a render finished without drawing.</summary>
    private const int NotDrawn = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The page width in pixels.</summary>
    private const int PageWidth = 612;

    /// <summary>The page height in pixels.</summary>
    private const int PageHeight = 792;

    /// <summary>The pixels rendered into.</summary>
    private readonly byte[] _pixels = new byte[PageWidth * PageHeight * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The document with a huge page tree.</summary>
    private byte[] _many = [];

    /// <summary>The heavy document.</summary>
    private byte[] _heavy = [];

    /// <summary>The small document the next tab opens.</summary>
    private byte[] _small = [];

    /// <summary>Builds the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _many = HeavyPdf.Create(ManyPageCount, ManyPageContent);
        _heavy = HeavyPdf.Create(HeavyPages, HeavyContent);
        _small = TestPdf.Create(SmallPages);
    }

    /// <summary>Opens the huge page tree to the end; the work cannot be stopped.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int OpenManyPagesUncancellable()
    {
        using var document = BenchmarkDrawing.Open(_many, null, _drawingServices);
        return document.PageCount;
    }

    /// <summary>Opens the huge page tree and cancels after 60 ms.</summary>
    /// <returns>1 when the open was cancelled.</returns>
    [Benchmark]
    public async Task<int> OpenManyPagesCancelled()
    {
        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        try
        {
            using var document = await BenchmarkDrawing.OpenAsync(_many, null, _drawingServices, source.Token).ConfigureAwait(false);
            return document.PageCount;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
    }

    /// <summary>Renders a heavy page to the end; the work cannot be stopped.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark]
    public bool RenderHeavyUncancellable()
    {
        using var document = BenchmarkDrawing.Open(_heavy, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return renderer.Render(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), new(_pixels, PageWidth, PageHeight, PageWidth * BytesPerPixel));
    }

    /// <summary>Renders a heavy page and cancels after 60 ms.</summary>
    /// <returns>1 when the render was cancelled.</returns>
    [Benchmark]
    public async Task<int> RenderHeavyCancelled()
    {
        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        using var document = BenchmarkDrawing.Open(_heavy, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        try
        {
            return await renderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), _pixels, PageWidth, PageHeight, PageWidth * BytesPerPixel, source.Token).ConfigureAwait(false) ? 0 : NotDrawn;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
    }

    /// <summary>Extracts the text of a heavy page to the end; the work cannot be stopped.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public int TextHeavyUncancellable()
    {
        using var document = BenchmarkDrawing.Open(_heavy, null, _drawingServices);
        return PdfDocumentText.GetTextPage(document, 0).CharCount;
    }

    /// <summary>Extracts the text of a heavy page and cancels after 60 ms.</summary>
    /// <returns>1 when the extraction was cancelled.</returns>
    [Benchmark]
    public async Task<int> TextHeavyCancelled()
    {
        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        using var document = BenchmarkDrawing.Open(_heavy, null, _drawingServices);
        try
        {
            return (await PdfDocumentText.GetTextPageAsync(document, 0, source.Token).ConfigureAwait(false)).CharCount;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
    }

    /// <summary>
    /// The tab swap with synchronous code: the first tab's open and render run on a pool thread and cannot be stopped; the
    /// second tab opens meanwhile; the time is until both have finished.
    /// </summary>
    /// <returns>The characters of the second tab's first page.</returns>
    [Benchmark]
    public async Task<int> TabSwapSync()
    {
        var first = Task.Run(RenderFirstTabSync, CancellationToken.None);
        await Task.Delay(CancelAfterMilliseconds).ConfigureAwait(false);
        using var second = BenchmarkDrawing.Open(_small, null, _drawingServices);
        var characters = PdfDocumentText.GetTextPage(second, 0).CharCount;
        _ = await first.ConfigureAwait(false);
        return characters;
    }

    /// <summary>
    /// The tab swap with async code: the first tab's open and render are cancelled after 60 ms and the second tab opens at
    /// once; the time is until both have finished.
    /// </summary>
    /// <returns>The characters of the second tab's first page.</returns>
    [Benchmark]
    public async Task<int> TabSwapAsync()
    {
        using var source = new CancellationTokenSource();
        var first = RenderFirstTabAsync(source.Token);
        await Task.Delay(CancelAfterMilliseconds).ConfigureAwait(false);
        await source.CancelAsync().ConfigureAwait(false);
        using var second = await BenchmarkDrawing.OpenAsync(_small, null, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        var characters = (await PdfDocumentText.GetTextPageAsync(second, 0, CancellationToken.None).ConfigureAwait(false)).CharCount;
        _ = await first.ConfigureAwait(false);
        return characters;
    }

    /// <summary>Opens a slow stream with async I/O and cancels after 60 ms.</summary>
    /// <returns>1 when the open was cancelled.</returns>
    [Benchmark]
    public async Task<int> OpenSlowStreamCancelled()
    {
        await using var stream = new ThrottledStream(_heavy, TimeSpan.FromMilliseconds(ReadLatencyMilliseconds));
        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        try
        {
            using var document = await BenchmarkDrawing.OpenAsync(stream, null, _drawingServices, source.Token).ConfigureAwait(false);
            return document.PageCount;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
    }

    /// <summary>Opens a slow stream with blocking reads; the work cannot be stopped.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int OpenSlowStreamUncancellable()
    {
        using var stream = new ThrottledStream(_heavy, TimeSpan.FromMilliseconds(ReadLatencyMilliseconds));
        using var document = BenchmarkDrawing.Open(stream, null, _drawingServices);
        return document.PageCount;
    }

    /// <summary>The first tab done synchronously: open and render a heavy page.</summary>
    /// <returns>Whether the page was drawn.</returns>
    private bool RenderFirstTabSync()
    {
        using var document = BenchmarkDrawing.Open(_heavy, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        var pixels = new byte[PageWidth * PageHeight * BytesPerPixel];
        return renderer.Render(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), new(pixels, PageWidth, PageHeight, PageWidth * BytesPerPixel));
    }

    /// <summary>The first tab done with async calls: open and render a heavy page until cancelled.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>1 when cancelled, else 0.</returns>
    private async Task<int> RenderFirstTabAsync(CancellationToken cancellationToken)
    {
        // The first call runs inline on the caller's thread, so start on a pool thread like the sync tab does.
        await Task.Yield();
        try
        {
            using var document = await BenchmarkDrawing.OpenAsync(_heavy, null, _drawingServices, cancellationToken).ConfigureAwait(false);
            using var renderer = new PdfPageRenderer(document);
            var pixels = new byte[PageWidth * PageHeight * BytesPerPixel];
            _ = await renderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), pixels, PageWidth, PageHeight, PageWidth * BytesPerPixel, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
    }
}
