// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Text;
using HyperPdfLibrary.Writing;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Compares whole workloads on a freshly opened document through the synchronous API and the async-first API: render the
/// first ten pages, extract all the text, search for a word, read the outline, links and annotations, and save after one
/// edit. The document's caches start empty; the operating system's file cache is warm.
/// </summary>
public class HyperPdfAsyncWorkBenchmarks
{
    /// <summary>The pixels of the shared render buffer.</summary>
    private readonly byte[] _pixels = new byte[HyperPdfAsyncSamples.MaxWidth * HyperPdfAsyncSamples.MaxHeight * HyperPdfAsyncSamples.BytesPerPixel];

    /// <summary>The stream saves are written to.</summary>
    private readonly MemoryStream _saved = new();

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The file.</summary>
    private string _path = string.Empty;

    /// <summary>Gets or sets the sample: Report or Scan.</summary>
    [Params("Report", "Scan")]
    public string Document { get; set; } = "Report";

    /// <summary>Gets or sets how the file is read: Mapped or FileStream.</summary>
    [Params("Mapped", "FileStream")]
    public string Source { get; set; } = "Mapped";

    /// <summary>Finds the file.</summary>
    [GlobalSetup]
    public void Setup() => _path = HyperPdfAsyncSamples.PathFor(Document);

    /// <summary>Releases the save stream.</summary>
    [GlobalCleanup]
    public void Cleanup() => _saved.Dispose();

    /// <summary>Opens the file and renders its first ten pages in turn.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark(Baseline = true)]
    public int RenderPagesSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        using var renderer = new PdfPageRenderer(opened.Document);
        var drawn = 0;
        for (var i = 0; i < Math.Min(HyperPdfAsyncSamples.RenderedPages, opened.Document.PageCount); i++)
        {
            drawn += HyperPdfAsyncSamples.RenderSync(renderer, opened.Document, i, _pixels) ? 1 : 0;
        }

        return drawn;
    }

    /// <summary>Opens the file with async I/O and renders its first ten pages in turn.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark]
    public async ValueTask<int> RenderPagesAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        using var renderer = new PdfPageRenderer(opened.Document);
        var drawn = 0;
        for (var i = 0; i < Math.Min(HyperPdfAsyncSamples.RenderedPages, opened.Document.PageCount); i++)
        {
            drawn += await HyperPdfAsyncSamples.RenderAsync(renderer, opened.Document, i, _pixels, CancellationToken.None).ConfigureAwait(false) ? 1 : 0;
        }

        return drawn;
    }

    /// <summary>Opens the file and extracts the text of every page.</summary>
    /// <returns>The characters read.</returns>
    [Benchmark]
    public int TextAllSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        var characters = 0;
        for (var i = 0; i < opened.Document.PageCount; i++)
        {
            characters += PdfDocumentText.GetTextPage(opened.Document, i).CharCount;
        }

        return characters;
    }

    /// <summary>Opens the file with async I/O and walks the text of every page.</summary>
    /// <returns>The characters read.</returns>
    [Benchmark]
    public async ValueTask<int> TextAllAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        var characters = 0;
        await foreach (var page in PdfDocumentText.GetTextPagesAsync(opened.Document, CancellationToken.None).ConfigureAwait(false))
        {
            characters += page.CharCount;
        }

        return characters;
    }

    /// <summary>Opens the file and searches every page for a word.</summary>
    /// <returns>The matches.</returns>
    [Benchmark]
    public int SearchSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        var found = new List<PdfTextMatch>();
        for (var i = 0; i < opened.Document.PageCount; i++)
        {
            PdfDocumentText.GetTextPage(opened.Document, i).Find(HyperPdfAsyncSamples.Word, PdfTextSearchOptions.None, found);
        }

        return found.Count;
    }

    /// <summary>Opens the file with async I/O and searches every page for a word.</summary>
    /// <returns>The matches.</returns>
    [Benchmark]
    public async ValueTask<int> SearchAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        var matches = 0;
        await foreach (var page in PdfDocumentText.FindAsync(opened.Document, HyperPdfAsyncSamples.Word, PdfTextSearchOptions.None, CancellationToken.None).ConfigureAwait(false))
        {
            matches += page.Matches.Length;
        }

        return matches;
    }

    /// <summary>Opens the file and reads the outline, every page's links and every page's annotation content.</summary>
    /// <returns>A count.</returns>
    [Benchmark]
    public int NavigationSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        var document = opened.Document;
        var count = PdfDocumentNavigation.GetOutline(document).Count;
        for (var i = 0; i < document.PageCount; i++)
        {
            count += PdfDocumentLinks.GetLinks(document, i).Count + (int)PdfDocumentContent.ScanAnnotations(document, i);
        }

        return count;
    }

    /// <summary>Opens the file with async I/O and reads the outline, every page's links and every page's annotation content.</summary>
    /// <returns>A count.</returns>
    [Benchmark]
    public async ValueTask<int> NavigationAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        var document = opened.Document;
        var count = (await PdfDocumentNavigation.GetOutlineAsync(document, CancellationToken.None).ConfigureAwait(false)).Count;
        for (var i = 0; i < document.PageCount; i++)
        {
            count += (await PdfDocumentLinks.GetLinksAsync(document, i, CancellationToken.None).ConfigureAwait(false)).Count;
            count += (int)await PdfDocumentContent.ScanAnnotationsAsync(document, i, CancellationToken.None).ConfigureAwait(false);
        }

        return count;
    }

    /// <summary>Opens the file, changes the title and saves the update into memory.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public long SaveAfterEditSync()
    {
        using var opened = HyperPdfAsyncSamples.OpenSync(_path, Source, _drawingServices);
        PdfDocumentMetadataEditing.SetMetadata(opened.Document, new PdfMetadataEdit { Title = "Edited" });
        _saved.SetLength(0);
        PdfIncrementalWriter.Save(opened.Document.Objects, _saved);
        return _saved.Length;
    }

    /// <summary>Opens the file with async I/O, changes the title and saves the update into memory with async writes.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public async ValueTask<long> SaveAfterEditAsync()
    {
        using var opened = await HyperPdfAsyncSamples.OpenAsync(_path, Source, _drawingServices, CancellationToken.None).ConfigureAwait(false);
        PdfDocumentMetadataEditing.SetMetadata(opened.Document, new PdfMetadataEdit { Title = "Edited" });
        _saved.SetLength(0);
        await PdfDocumentSaving.SaveAsync(opened.Document, _saved, CancellationToken.None).ConfigureAwait(false);
        return _saved.Length;
    }
}
