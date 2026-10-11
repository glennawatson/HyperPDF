// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>
/// Checks the tab-swap case: a document that is still opening or rendering when its token is cancelled stops its CPU work
/// and its I/O promptly, and the process stays usable for the next document.
/// </summary>
[NotInParallel]
public sealed class AsyncCancellationTests
{
    /// <summary>The pages of the heavy document.</summary>
    private const int HeavyPages = 3;

    /// <summary>The content bytes of each heavy page.</summary>
    private const int HeavyContent = 16 * 1024 * 1024;

    /// <summary>The pages of the document with a huge page tree.</summary>
    private const int ManyPageCount = 150_000;

    /// <summary>The content bytes of each page of the huge page tree.</summary>
    private const int ManyPageContent = 16;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The wait before the token is cancelled, in milliseconds.</summary>
    private const int CancelAfterMilliseconds = 60;

    /// <summary>How long opening the huge page tree must take, in milliseconds, for the test to mean anything.</summary>
    private const int MinimumOpenMilliseconds = 120;

    /// <summary>How long after the cancellation the work may take to stop, in milliseconds.</summary>
    private const int StopLimitMilliseconds = 600;

    /// <summary>The wait before each throttled read completes, in milliseconds.</summary>
    private const int ReadLatencyMilliseconds = 40;

    /// <summary>The pages of the sample document.</summary>
    private const int SamplePages = 3;

    /// <summary>The number of warm async cache hits measured for allocation.</summary>
    private const int WarmQueries = 256;

    /// <summary>The time the tests let reads that were in flight settle, in milliseconds.</summary>
    private const int SettleMilliseconds = 200;

    /// <summary>The heavy document: a few pages of many drawn lines.</summary>
    private static readonly byte[] Heavy = HeavyPdf.Create(HeavyPages, HeavyContent);

    /// <summary>A document with a huge page tree, so opening it takes a while.</summary>
    private static readonly byte[] ManyPages = HeavyPdf.Create(ManyPageCount, ManyPageContent);

    /// <summary>A small document.</summary>
    private static readonly byte[] Sample = TestPdf.Create(SamplePages);

    /// <summary>An open-only token does not cancel reads from the completed document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenOnlyCancellationDoesNotPoisonDocument()
    {
        using var opening = new CancellationTokenSource();
        using var document = await PdfDocumentReader.OpenWithAsync(
            new MemoryPdfByteSource(Sample),
            true,
            PdfOpenOptions.Default,
            opening.Token,
            CancellationToken.None);
        await opening.CancelAsync();

        var page = await PdfDocumentPages.GetPageAsync(document, 0, CancellationToken.None);
        await Assert.That(page.Index).IsEqualTo(0);
        await Assert.That(document.PageCount).IsEqualTo(SamplePages);
    }

    /// <summary>Warm page, text and outline reads complete inline without creating managed objects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmAsyncReadersCompleteWithoutAllocating()
    {
        using var document = PdfDocumentReader.Open(Sample, null);
        _ = await PdfDocumentPages.GetPageAsync(document, 0, CancellationToken.None);
        _ = await PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None);
        _ = await PdfDocumentNavigation.GetOutlineAsync(document, CancellationToken.None);

        var allCompleted = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < WarmQueries; i++)
        {
            var page = PdfDocumentPages.GetPageAsync(document, 0, CancellationToken.None);
            var text = PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None);
            var outline = PdfDocumentNavigation.GetOutlineAsync(document, CancellationToken.None);
            allCompleted &= page.IsCompletedSuccessfully && text.IsCompletedSuccessfully && outline.IsCompletedSuccessfully;
            _ = await page;
            _ = await text;
            _ = await outline;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allCompleted).IsTrue();
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>A token that is already cancelled makes every async operation throw OperationCanceledException.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsEveryOperation()
    {
        using var document = PdfDocumentReader.Open(Sample, null);
        using var renderer = new PdfPageRenderer(document);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var token = source.Token;
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1F, out var width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        await using var output = new MemoryStream();

        await Assert.That(await Cancelled(() => PdfDocumentReader.OpenAsync(Sample, null, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentText.GetTextPageAsync(document, 0, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentPages.GetPageAsync(document, 0, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentNavigation.GetOutlineAsync(document, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentLinks.GetLinksAsync(document, 0, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentContent.ScanAnnotationsAsync(document, 0, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentSaving.SaveAsync(document, output, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => renderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), pixels, width, height, width * BytesPerPixel, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(DrainTextAsync(document, token))).IsTrue();
    }

    /// <summary>Cancelling while an open waits on a slow stream stops the reads and throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelWhileOpeningSlowStreamStopsReading()
    {
        await using var stream = new ThrottledStream(Heavy, TimeSpan.FromMilliseconds(ReadLatencyMilliseconds));
        using var source = new CancellationTokenSource();
        var start = Stopwatch.GetTimestamp();
        var opening = PdfDocumentReader.OpenAsync(stream, null, source.Token).AsTask();
        source.CancelAfter(CancelAfterMilliseconds);
        await Assert.That(await Cancelled(() => opening)).IsTrue();
        var stopped = Elapsed(start);
        var readsAtStop = stream.AsyncReads;
        await Task.Delay(SettleMilliseconds);

        await Assert.That(stopped).IsLessThan(CancelAfterMilliseconds + StopLimitMilliseconds);
        await Assert.That(stream.AsyncReads).IsEqualTo(readsAtStop);
    }

    /// <summary>Cancelling an open that is still walking a huge page tree stops the walk.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelWhileWalkingThePageTreeStopsTheWalk()
    {
        var baseline = Stopwatch.GetTimestamp();
        using (var whole = PdfDocumentReader.Open(ManyPages, null))
        {
            await Assert.That(whole.PageCount).IsEqualTo(ManyPageCount);
        }

        var uncancelled = Elapsed(baseline);
        using var source = new CancellationTokenSource();
        source.CancelAfter(CancelAfterMilliseconds);
        var start = Stopwatch.GetTimestamp();
        await Assert.That(await Cancelled(() => PdfDocumentReader.OpenAsync(ManyPages, null, source.Token).AsTask())).IsTrue();
        var elapsed = Elapsed(start);

        await Assert.That(uncancelled).IsGreaterThan(MinimumOpenMilliseconds);
        await Assert.That(elapsed).IsLessThan(CancelAfterMilliseconds + StopLimitMilliseconds);
        await Assert.That(elapsed).IsLessThan(uncancelled);
    }

    /// <summary>Cancelling a render in the middle of a page stops recording it, and the next render still works.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelWhileRenderingStopsRecording()
    {
        using var document = PdfDocumentReader.Open(Heavy, null);
        using var renderer = new PdfPageRenderer(document);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1F, out var width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        using var source = new CancellationTokenSource();
        source.CancelAfter(CancelAfterMilliseconds);
        var start = Stopwatch.GetTimestamp();
        var rendering = renderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), pixels, width, height, width * BytesPerPixel, source.Token).AsTask();
        await Assert.That(await Cancelled(() => rendering)).IsTrue();

        await Assert.That(Elapsed(start)).IsLessThan(CancelAfterMilliseconds + StopLimitMilliseconds);

        // A fresh token on a lighter document of the same renderer family still draws.
        using var light = PdfDocumentReader.Open(Sample, null);
        using var lightRenderer = new PdfPageRenderer(light);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(light, 0), 0, 1F, out var lightWidth, out var lightHeight);
        var lightPixels = new byte[lightWidth * lightHeight * BytesPerPixel];
        await Assert.That(await lightRenderer.RenderAsync(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), lightPixels, lightWidth, lightHeight, lightWidth * BytesPerPixel, CancellationToken.None)).IsTrue();
    }

    /// <summary>Cancelling text extraction in the middle of a page stops it and leaves the cache empty for that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelWhileExtractingTextStopsExtraction()
    {
        using var document = PdfDocumentReader.Open(Heavy, null);
        using var source = new CancellationTokenSource();
        source.CancelAfter(CancelAfterMilliseconds);
        var start = Stopwatch.GetTimestamp();
        await Assert.That(await Cancelled(() => PdfDocumentText.GetTextPageAsync(document, 0, source.Token).AsTask())).IsTrue();

        await Assert.That(Elapsed(start)).IsLessThan(CancelAfterMilliseconds + StopLimitMilliseconds);
    }

    /// <summary>A second document opens at full speed while a first one is cancelled and winding down.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NextDocumentOpensWhileTheFirstStops()
    {
        using var first = new CancellationTokenSource();
        await using var slow = new ThrottledStream(Heavy, TimeSpan.FromMilliseconds(ReadLatencyMilliseconds));
        var opening = PdfDocumentReader.OpenAsync(slow, null, first.Token).AsTask();
        await first.CancelAsync();
        var start = Stopwatch.GetTimestamp();
        using var next = await PdfDocumentReader.OpenAsync(Sample, null, CancellationToken.None);
        var nextText = await PdfDocumentText.GetTextPageAsync(next, 0, CancellationToken.None);
        var openTime = Elapsed(start);

        await Assert.That(await Cancelled(() => opening)).IsTrue();
        await Assert.That(nextText.CharCount).IsGreaterThan(0);
        await Assert.That(openTime).IsLessThan(StopLimitMilliseconds);
    }

    /// <summary>Gets the milliseconds since a timestamp.</summary>
    /// <param name="start">A <see cref="Stopwatch.GetTimestamp"/> value.</param>
    /// <returns>The whole milliseconds.</returns>
    private static long Elapsed(long start) => (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    /// <summary>Runs an operation and reports whether it ended with an OperationCanceledException.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns><see langword="true"/> when it was cancelled.</returns>
    private static async Task<bool> Cancelled(Func<Task> operation)
    {
        try
        {
            await operation();
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Wraps the walk of every text page as an operation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="token">The token.</param>
    /// <returns>The operation.</returns>
    private static Func<Task> DrainTextAsync(PdfDocument document, CancellationToken token) => async () =>
    {
        await foreach (var page in PdfDocumentText.GetTextPagesAsync(document, token))
        {
            _ = page.CharCount;
        }
    };
}
