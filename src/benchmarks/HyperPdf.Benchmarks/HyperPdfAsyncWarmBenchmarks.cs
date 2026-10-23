// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Compares the synchronous and async-first APIs on a document that is already open, with its pages recorded and its text
/// extracted, so the cost measured is the call itself. The parallel benchmarks render eight pages at once: the sync form
/// blocks pool threads in <see cref="Parallel"/>, the async form awaits eight tasks.
/// </summary>
public class HyperPdfAsyncWarmBenchmarks
{
    /// <summary>The pages rendered at once.</summary>
    private const int ParallelPages = 8;

    /// <summary>The pixel buffers, one for each parallel page.</summary>
    private readonly byte[][] _pixels = new byte[ParallelPages][];

    /// <summary>The tasks of the async parallel render, reused so the benchmark does not allocate the array.</summary>
    private readonly Task<bool>[] _tasks = new Task<bool>[ParallelPages];

    /// <summary>The search results, reused.</summary>
    private readonly List<PdfTextMatch> _found = [];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The open document.</summary>
    private OpenedDocument _opened = null!;

    /// <summary>The renderer.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Gets or sets the sample: Report or Scan.</summary>
    [Params("Report", "Scan")]
    public string Document { get; set; } = "Report";

    /// <summary>Gets or sets how the file is read: Mapped, FileStream (opened with the sync API) or FileStreamAsync (opened with the async API, so its pages are loaded ahead).</summary>
    [Params("Mapped", "FileStream", "FileStreamAsync")]
    public string Source { get; set; } = "Mapped";

    /// <summary>Opens the document and warms every cache the benchmarks use.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        var path = HyperPdfAsyncSamples.PathFor(Document);
        _opened = Source == "FileStreamAsync"
            ? await HyperPdfAsyncSamples.OpenAsync(path, "FileStream", _drawingServices, CancellationToken.None).ConfigureAwait(false)
            : HyperPdfAsyncSamples.OpenSync(path, Source, _drawingServices);
        _renderer = new(_opened.Document);
        for (var i = 0; i < ParallelPages; i++)
        {
            _pixels[i] = new byte[HyperPdfAsyncSamples.MaxWidth * HyperPdfAsyncSamples.MaxHeight * HyperPdfAsyncSamples.BytesPerPixel];
            _ = HyperPdfAsyncSamples.RenderSync(_renderer, _opened.Document, i, _pixels[i]);
            _ = HyperPdfLibrary.Document.PdfDocumentText.GetTextPage(_opened.Document, i);
        }

        _ = HyperPdfLibrary.Document.PdfDocumentNavigation.GetOutline(_opened.Document);
        _ = HyperPdfLibrary.Document.PdfDocumentLinks.GetLinks(_opened.Document, 0);
    }

    /// <summary>Releases the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _opened.Dispose();
    }

    /// <summary>Renders page 1, already recorded.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark(Baseline = true)]
    public bool RenderWarmSync() => HyperPdfAsyncSamples.RenderSync(_renderer, _opened.Document, 0, _pixels[0]);

    /// <summary>Renders page 1, already recorded, with the async API.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark]
    public ValueTask<bool> RenderWarmAsync() => HyperPdfAsyncSamples.RenderAsync(_renderer, _opened.Document, 0, _pixels[0], CancellationToken.None);

    /// <summary>Gets the text of a page already extracted.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public int TextWarmSync() => HyperPdfLibrary.Document.PdfDocumentText.GetTextPage(_opened.Document, 0).CharCount;

    /// <summary>Gets the text of a page already extracted, with the async API.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public async ValueTask<int> TextWarmAsync() => (await HyperPdfLibrary.Document.PdfDocumentText.GetTextPageAsync(_opened.Document, 0, CancellationToken.None).ConfigureAwait(false)).CharCount;

    /// <summary>Gets a page that is already loaded.</summary>
    /// <returns>The page width.</returns>
    [Benchmark]
    public float PageWarmSync() => HyperPdfLibrary.Document.PdfDocumentPages.GetPage(_opened.Document, 0).Width;

    /// <summary>Gets a page that is already loaded, with the async API.</summary>
    /// <returns>The page width.</returns>
    [Benchmark]
    public async ValueTask<float> PageWarmAsync() => (await HyperPdfLibrary.Document.PdfDocumentPages.GetPageAsync(_opened.Document, 0, CancellationToken.None).ConfigureAwait(false)).Width;

    /// <summary>Reads the annotation content of a page already read.</summary>
    /// <returns>The content flags.</returns>
    [Benchmark]
    public int AnnotationsWarmSync() => (int)HyperPdfLibrary.Document.PdfDocumentContent.ScanAnnotations(_opened.Document, 0);

    /// <summary>Reads the annotation content of a page already read, with the async API.</summary>
    /// <returns>The content flags.</returns>
    [Benchmark]
    public async ValueTask<int> AnnotationsWarmAsync() =>
        (int)await HyperPdfLibrary.Document.PdfDocumentContent.ScanAnnotationsAsync(_opened.Document, 0, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Searches every page for a word; the text cache keeps only the most recent pages.</summary>
    /// <returns>The matches.</returns>
    [Benchmark]
    public int SearchWarmSync()
    {
        _found.Clear();
        for (var i = 0; i < _opened.Document.PageCount; i++)
        {
            HyperPdfLibrary.Document.PdfDocumentText.GetTextPage(_opened.Document, i).Find(HyperPdfAsyncSamples.Word, PdfTextSearchOptions.None, _found);
        }

        return _found.Count;
    }

    /// <summary>Searches every page for a word, with the async API.</summary>
    /// <returns>The matches.</returns>
    [Benchmark]
    public async ValueTask<int> SearchWarmAsync()
    {
        var matches = 0;
        var pages = HyperPdfLibrary.Document.PdfDocumentText.FindAsync(_opened.Document, HyperPdfAsyncSamples.Word, PdfTextSearchOptions.None, CancellationToken.None);
        await foreach (var page in pages.ConfigureAwait(false))
        {
            matches += page.Matches.Length;
        }

        return matches;
    }

    /// <summary>Reads the outline and the first page's links, already read.</summary>
    /// <returns>A count.</returns>
    [Benchmark]
    public int NavigationWarmSync() =>
        HyperPdfLibrary.Document.PdfDocumentNavigation.GetOutline(_opened.Document).Count
        + HyperPdfLibrary.Document.PdfDocumentLinks.GetLinks(_opened.Document, 0).Count;

    /// <summary>Reads the outline and the first page's links, already read, with the async API.</summary>
    /// <returns>A count.</returns>
    [Benchmark]
    public async ValueTask<int> NavigationWarmAsync() =>
        (await HyperPdfLibrary.Document.PdfDocumentNavigation.GetOutlineAsync(_opened.Document, CancellationToken.None).ConfigureAwait(false)).Count
        + (await HyperPdfLibrary.Document.PdfDocumentLinks.GetLinksAsync(_opened.Document, 0, CancellationToken.None).ConfigureAwait(false)).Count;

    /// <summary>Renders eight recorded pages at once on pool threads.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark]
    public int RenderParallelSync()
    {
        var drawn = 0;
        _ = Parallel.For(0, ParallelPages, i =>
        {
            if (HyperPdfAsyncSamples.RenderSync(_renderer, _opened.Document, i, _pixels[i]))
            {
                _ = Interlocked.Increment(ref drawn);
            }
        });
        return drawn;
    }

    /// <summary>Renders eight recorded pages at once with the async API, each started on a pool thread by the caller.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark]
    public async Task<int> RenderParallelAsyncPool()
    {
        for (var i = 0; i < ParallelPages; i++)
        {
            var page = i;
            _tasks[i] = Task.Run(() => HyperPdfAsyncSamples.RenderAsync(_renderer, _opened.Document, page, _pixels[page], CancellationToken.None).AsTask());
        }

        var drawn = 0;
        foreach (var done in await Task.WhenAll(_tasks).ConfigureAwait(false))
        {
            drawn += done ? 1 : 0;
        }

        return drawn;
    }

    /// <summary>Renders eight recorded pages at once with the async API, started by the awaiting caller alone.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark]
    public async Task<int> RenderParallelAsync()
    {
        for (var i = 0; i < ParallelPages; i++)
        {
            _tasks[i] = HyperPdfAsyncSamples.RenderAsync(_renderer, _opened.Document, i, _pixels[i], CancellationToken.None).AsTask();
        }

        var drawn = 0;
        foreach (var done in await Task.WhenAll(_tasks).ConfigureAwait(false))
        {
            drawn += done ? 1 : 0;
        }

        return drawn;
    }
}
