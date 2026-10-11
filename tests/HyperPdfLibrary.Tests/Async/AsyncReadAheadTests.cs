// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>Checks that the async API waits for a slow stream with async I/O and then reads from the cache, never blocking on the stream.</summary>
[NotInParallel]
public sealed class AsyncReadAheadTests
{
    /// <summary>The pages of the sample document.</summary>
    private const int Pages = 4;

    /// <summary>The content bytes of each page of the large sample.</summary>
    private const int LargeContent = 800 * 1024;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The wait before each read of the slow stream completes, in milliseconds.</summary>
    private const int SlowMilliseconds = 40;

    /// <summary>How long the reads wait before the source is disposed, in milliseconds.</summary>
    private const int DisposeDelayMilliseconds = 60;

    /// <summary>How long disposal may take to wake the waiting reads, in milliseconds.</summary>
    private const int HangLimitMilliseconds = 10_000;

    /// <summary>The pages a small cache holds.</summary>
    private const int TwoPages = 2;

    /// <summary>The wait before each read completes, in milliseconds.</summary>
    private const int LatencyMilliseconds = 1;

    /// <summary>A small document, which fits the cache whole.</summary>
    private static readonly byte[] Small = TestPdf.Create(Pages);

    /// <summary>A document larger than the load-ahead budget, so only the parts a page needs are loaded.</summary>
    private static readonly byte[] Large = HeavyPdf.Create(Pages, LargeContent);

    /// <summary>A small document opened and read through the async API makes no blocking read of the stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallDocumentNeverReadsTheStreamSynchronously()
    {
        await using var stream = new ThrottledStream(Small, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        using var document = await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None);
        for (var i = 0; i < document.PageCount; i++)
        {
            _ = await PdfDocumentText.GetTextPageAsync(document, i, CancellationToken.None);
            _ = await PdfDocumentLinks.GetLinksAsync(document, i, CancellationToken.None);
        }

        _ = await PdfDocumentNavigation.GetOutlineAsync(document, CancellationToken.None);
        await Assert.That(stream.SyncReads).IsEqualTo(0);
        await Assert.That(stream.AsyncReads).IsGreaterThan(0);
    }

    /// <summary>A document bigger than the budget loads the objects of a page ahead of rendering it, so the render reads nothing from the stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeDocumentLoadsAPageAheadOfRenderingIt()
    {
        await using var stream = new ThrottledStream(Large, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        using var document = await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None);
        using var renderer = new PdfPageRenderer(document);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1F, out var width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        var syncBefore = stream.SyncReads;

        await Assert.That(await renderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), pixels, width, height, width * BytesPerPixel, CancellationToken.None)).IsTrue();
        await Assert.That(stream.SyncReads).IsEqualTo(syncBefore);
    }

    /// <summary>A second prefetch of a page completes at once while nothing has left the cache, and walks again after something did.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedPrefetchSkipsTheWalkUntilSomethingIsEvicted()
    {
        await using var stream = new ThrottledStream(Large, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        using var document = await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None);
        await PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);
        var reads = stream.AsyncReads;

        var repeat = PdfDocumentPages.PrefetchPageAsync(document, 0, CancellationToken.None);

        await Assert.That(repeat.IsCompletedSuccessfully).IsTrue();
        await Assert.That(stream.AsyncReads).IsEqualTo(reads);
    }

    /// <summary>The cache generation moves only when a cached page is dropped to make room.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheGenerationMovesOnlyOnEviction()
    {
        var buffer = new byte[1];
        await using var stream = new MemoryStream(Large, false);
        using var source = new HyperPdfLibrary.IO.StreamPdfByteSource(stream, TwoPages * HyperPdfLibrary.IO.StreamPdfByteSource.PageSize, false);
        _ = source.Read(0, buffer);
        _ = source.Read(HyperPdfLibrary.IO.StreamPdfByteSource.PageSize, buffer);
        var filling = source.CacheGeneration;
        _ = source.Read((long)TwoPages * HyperPdfLibrary.IO.StreamPdfByteSource.PageSize, buffer);

        await Assert.That(filling).IsEqualTo(0);
        await Assert.That(source.CacheGeneration).IsEqualTo(1);
    }

    /// <summary>Disposing a stream source while an async load and a blocking read wait on the stream wakes both; neither hangs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeWhileReadsWaitOnTheStreamDoesNotHang()
    {
        await using var stream = new ThrottledStream(Large, TimeSpan.FromMilliseconds(SlowMilliseconds));
        var source = new HyperPdfLibrary.IO.StreamPdfByteSource(stream, HyperPdfLibrary.IO.StreamPdfByteSource.DefaultCacheBytes, false);
        var loading = source.PrefetchAsync(0, Large.Length, CancellationToken.None).AsTask();
        var blocking = Task.Run(() => ReadAfterDispose(source), CancellationToken.None);
        await Task.Delay(DisposeDelayMilliseconds);

        source.Dispose();
        var finished = Task.WhenAll(loading, blocking).ContinueWith(static _ => true, TaskScheduler.Default);
        var winner = await Task.WhenAny(finished, Task.Delay(HangLimitMilliseconds));

        await Assert.That(winner).IsSameReferenceAs(finished);
    }

    /// <summary>Disposing a document that was opened from a stream leaves the caller's stream open and stops further reads.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposingTheDocumentLeavesTheStreamOpen()
    {
        await using var stream = new ThrottledStream(Small, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        var document = await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None);
        document.Dispose();
        var reads = stream.AsyncReads;

        await Assert.That(stream.IsDisposed).IsFalse();
        await Assert.That(document.IsDisposed).IsTrue();
        await Assert.That(stream.AsyncReads).IsEqualTo(reads);
    }

    /// <summary>Reads a byte from a source that is being disposed, tolerating the disposal.</summary>
    /// <param name="source">The source.</param>
    private static void ReadAfterDispose(HyperPdfLibrary.IO.StreamPdfByteSource source)
    {
        var buffer = new byte[1];
        try
        {
            _ = source.Read(HyperPdfLibrary.IO.StreamPdfByteSource.PageSize * TwoPages, buffer);
        }
        catch (ObjectDisposedException)
        {
            // Expected: the source was disposed while this read waited for the stream.
        }
    }
}
