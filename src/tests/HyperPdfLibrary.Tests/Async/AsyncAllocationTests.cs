// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>
/// Checks that the async forms allocate nothing once their work is already done: a text page extracted, a page recorded,
/// links and the outline read, and, for a stream source, the page's objects already loaded ahead.
/// </summary>
[NotInParallel]
public sealed class AsyncAllocationTests
{
    /// <summary>The pages of the small sample.</summary>
    private const int Pages = 4;

    /// <summary>The calls made before measuring, so tiering and lazy tables settle.</summary>
    private const int Warmup = 150;

    /// <summary>The calls measured.</summary>
    private const int Calls = 200;

    /// <summary>The wait before each throttled read completes, in milliseconds.</summary>
    private const int LatencyMilliseconds = 1;

    /// <summary>The content bytes of each page of the large sample, which is bigger than the load-ahead budget.</summary>
    private const int LargeContent = 256 * 1024;

    /// <summary>The pages of the large sample.</summary>
    private const int LargePages = 16;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The name of the source that streams the large sample.</summary>
    private const string LargeSource = "stream-large";

    /// <summary>The small sample: text, links and an outline.</summary>
    private static readonly byte[] Small = TestPdf.Create(Pages);

    /// <summary>A sample bigger than the load-ahead budget, so a stream source loads page by page.</summary>
    private static readonly byte[] Large = HeavyPdf.Create(LargePages, LargeContent);

    /// <summary>Gets the sources the warm forms are measured on.</summary>
    public static IEnumerable<string> Sources { get; } = ["mapped", "memory", "stream", LargeSource];

    /// <summary>Every warm async form returns a completed task and allocates nothing.</summary>
    /// <param name="source">How the document is read.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Sources))]
    public async Task WarmFormsAllocateNothing(string source)
    {
        var directory = Directory.CreateTempSubdirectory("hyperpdf-alloc-");
        try
        {
            var path = Path.Combine(directory.FullName, "sample.pdf");
            var bytes = source == LargeSource ? Large : Small;
            await File.WriteAllBytesAsync(path, bytes);
            await using var stream = new ThrottledStream(bytes, TimeSpan.FromMilliseconds(LatencyMilliseconds));
            using var document = await OpenAsync(source, path, stream);
            using var renderer = new PdfPageRenderer(document);
            PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1F, out var width, out var height);
            var pixels = new byte[width * height * BytesPerPixel];
            var request = new PdfTileRequest(0, 1F, 0, 0, 0, PdfRenderFlags.None);
            var token = CancellationToken.None;

            // Warm every cache the forms read: the objects, the text page, the recorded page, the links and the outline.
            _ = await PdfDocumentText.GetTextPageAsync(document, 0, token);
            _ = await PdfDocumentLinks.GetLinksAsync(document, 0, token);
            _ = await PdfDocumentNavigation.GetOutlineAsync(document, token);
            _ = await renderer.RenderAsync(request, pixels, width, height, width * BytesPerPixel, token);

            var prefetch = await Measure(() => PdfDocumentPages.PrefetchPageAsync(document, 0, token));
            var page = await Measure(() => PdfDocumentPages.GetPageAsync(document, 0, token));
            var text = await Measure(() => PdfDocumentText.GetTextPageAsync(document, 0, token));
            var links = await Measure(() => PdfDocumentLinks.GetLinksAsync(document, 0, token));
            var annotations = await Measure(() => PdfDocumentContent.ScanAnnotationsAsync(document, 0, token));
            var outline = await Measure(() => PdfDocumentNavigation.GetOutlineAsync(document, token));

            // A page of the large sample takes long to replay, so its render is measured on the small sample's sources only.
            var render = source == LargeSource ? 0L : await Measure(() => renderer.RenderAsync(request, pixels, width, height, width * BytesPerPixel, token));

            // One summary, so a failure names the form that allocated.
            var summary = $"prefetch={prefetch} page={page} text={text} links={links} annotations={annotations} outline={outline} render={render}";
            await Assert.That(summary).IsEqualTo("prefetch=0 page=0 text=0 links=0 annotations=0 outline=0 render=0");
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>Opens the sample for a kind of source.</summary>
    /// <param name="source">The kind.</param>
    /// <param name="path">The sample file.</param>
    /// <param name="stream">The slow stream over the sample.</param>
    /// <returns>The document.</returns>
    private static async Task<PdfDocument> OpenAsync(string source, string path, Stream stream) => source switch
    {
        "mapped" => await PdfDocumentReader.OpenWithAsync(path, new() { Source = PdfSourceKind.Mapped, DrawingServices = SkiaTestRegistration.CreateServices() }, CancellationToken.None),
        "memory" => await PdfDocumentReader.OpenWithAsync(path, new() { Source = PdfSourceKind.Memory, DrawingServices = SkiaTestRegistration.CreateServices() }, CancellationToken.None),
        _ => await SkiaTestRegistration.OpenAsync(stream, null, CancellationToken.None),
    };

    /// <summary>Measures the bytes allocated by awaiting a task-returning form repeatedly, each call completing at once.</summary>
    /// <param name="form">The form.</param>
    /// <returns>The bytes allocated on this thread over all the calls.</returns>
    private static async Task<long> Measure(Func<ValueTask> form)
    {
        for (var i = 0; i < Warmup; i++)
        {
            await form();
        }

        var attempts = new AllocationAttempts();
        while (attempts.Next())
        {
            for (var i = 0; i < Calls; i++)
            {
                await form();
            }

            attempts.Stop();
        }

        return attempts.Smallest;
    }

    /// <summary>Measures the bytes allocated by awaiting a result-returning form repeatedly, each call completing at once.</summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="form">The form.</param>
    /// <returns>The bytes allocated on this thread over all the calls.</returns>
    private static async Task<long> Measure<T>(Func<ValueTask<T>> form)
    {
        for (var i = 0; i < Warmup; i++)
        {
            _ = await form();
        }

        var attempts = new AllocationAttempts();
        while (attempts.Next())
        {
            for (var i = 0; i < Calls; i++)
            {
                _ = await form();
            }

            attempts.Stop();
        }

        return attempts.Smallest;
    }
}
