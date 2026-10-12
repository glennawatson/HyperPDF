// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks what a document keeps while it renders, that the page pictures stay within their memory limit, and that disposing the document empties every cache.</summary>
public sealed class RenderLifetimeTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 200;

    /// <summary>The side of the test scan in pixels.</summary>
    private const int ScanSide = 300;

    /// <summary>The size of the tiles rendered, in pixels.</summary>
    private const int TileSize = 64;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pages in the multi-page document.</summary>
    private const int PageCount = 5;

    /// <summary>The documents opened by the race test.</summary>
    private const int Rounds = 20;

    /// <summary>The threads that render while the document is disposed.</summary>
    private const int Renderers = 4;

    /// <summary>The distinct widget tints created while a document stays open.</summary>
    private const int TintPolicies = 8;

    /// <summary>The most milliseconds the race test waits before it disposes the document.</summary>
    private const int MaxDelay = 4;

    /// <summary>The renders each thread tries; far more than fit before the dispose, so each thread is still rendering when it happens.</summary>
    private const int RendersPerThread = 1_000_000;

    /// <summary>The scale of the tiles.</summary>
    private const float Scale = 1;

    /// <summary>The entries of the gray scan image.</summary>
    private const string ScanEntries = "/Type /XObject /Subtype /Image /Width 300 /Height 300 /ColorSpace /DeviceGray /BitsPerComponent 8";

    /// <summary>The catalog's object id in the test files.</summary>
    private static readonly PdfObjectId Catalog = new(1, 0);

    /// <summary>Disposing the document empties the image cache, the page pictures and the text pages, and later work fails cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeEmptiesEveryCache()
    {
        using var page = CreateScanPage();
        _ = page.RenderPage();
        _ = PdfDocumentText.GetTextPage(page.Document, 0);
        var cache = page.Renderer.Cache;
        var imagesBefore = cache.Images.Count;
        var bytesBefore = cache.Images.Bytes;
        var picturesBefore = page.Renderer.PictureBytes;
        page.Document.Dispose();
        await Assert.That(imagesBefore).IsEqualTo(1);
        await Assert.That(bytesBefore).IsEqualTo((long)ScanSide * ScanSide);
        await Assert.That(picturesBefore).IsGreaterThan(bytesBefore);
        await Assert.That(cache.Images.Count).IsEqualTo(0);
        await Assert.That(cache.Images.Bytes).IsEqualTo(0);
        await Assert.That(page.Renderer.PictureCount).IsEqualTo(0);
        await Assert.That(() => PdfDocumentText.GetTextPage(page.Document, 0)).Throws<ObjectDisposedException>();
        await Assert.That(() => StoreReading.GetObject(page.Document.Objects, Catalog)).Throws<ObjectDisposedException>();
        await Assert.That(() => Render(page.Renderer, 0)).Throws<ObjectDisposedException>();
    }

    /// <summary>A gray scan costs one byte per pixel in the image cache, and the page picture counts it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesCountTheirImages()
    {
        using var page = CreateScanPage();
        _ = page.RenderPage();
        await Assert.That(page.Renderer.Cache.Images.Bytes).IsEqualTo((long)ScanSide * ScanSide);
        await Assert.That(page.Renderer.PictureBytes).IsGreaterThanOrEqualTo((long)ScanSide * ScanSide);
    }

    /// <summary>The picture limit stays renderer-owned while decoded image settings select a shared cache.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EqualOptionsShareCacheButConflictingOptionsDoNot()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(1), null);
        var first = new PdfPageRenderer(document);
        using var equivalent = new PdfPageRenderer(document, PdfRenderOptions.Default with { });
        using var fewerPictures = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = 1 });
        using var limited = new PdfPageRenderer(document, PdfRenderOptions.Default with { ImageCacheBytes = 1 });

        await Assert.That(ReferenceEquals(first.Cache, equivalent.Cache)).IsTrue();
        await Assert.That(ReferenceEquals(first.Cache, fewerPictures.Cache)).IsTrue();
        await Assert.That(ReferenceEquals(first.Cache, limited.Cache)).IsFalse();
        await Assert.That(first.Cache.Images.Capacity).IsEqualTo(PdfRenderOptions.DefaultImageCacheBytes);
        await Assert.That(limited.Cache.Images.Capacity).IsEqualTo(1);

        first.Dispose();
        Render(equivalent, 0);
        await Assert.That(equivalent.PictureCount).IsEqualTo(1);
    }

    /// <summary>A cache stays alive for equal live renderers and closes when the last one leaves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposingLastRendererReleasesDistinctPolicyCaches()
    {
        using var page = CreateScanPage();
        for (var i = 0; i < TintPolicies; i++)
        {
            var options = PdfRenderOptions.Default with { FormHighlight = new((uint)i, byte.MaxValue) };
            PdfRenderCache cache;
            using (var second = new PdfPageRenderer(page.Document, options))
            {
                using (var first = new PdfPageRenderer(page.Document, options with { }))
                {
                    cache = first.Cache;
                    await Assert.That(ReferenceEquals(cache, second.Cache)).IsTrue();
                    Render(first, 0);
                    await Assert.That(cache.Images.Count).IsEqualTo(1);
                }

                await Assert.That(cache.Images.Count).IsEqualTo(1);
            }

            await Assert.That(cache.Images.Count).IsEqualTo(0);
            await Assert.That(page.Document.State.RendererCaches?.Count ?? 0).IsEqualTo(1);
        }
    }

    /// <summary>With a small picture limit only the newest page stays; with the default limit every page of a small document stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PictureLimitKeepsOnlyTheNewestPageWhenTiny()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(PageCount), null);
        using var tiny = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = 1 });
        using var roomy = new PdfPageRenderer(document);
        for (var i = 0; i < PageCount; i++)
        {
            Render(tiny, i);
            Render(roomy, i);
        }

        await Assert.That(tiny.PictureCount).IsEqualTo(1);
        await Assert.That(roomy.PictureCount).IsEqualTo(PageCount);
        await Assert.That(roomy.PictureBytes).IsGreaterThan(0);
        await Assert.That(tiny.PictureBytes).IsLessThan(roomy.PictureBytes);
    }

    /// <summary>The picture limit may not be negative.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativePictureLimitIsRejected()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(1), null);
        await Assert.That(() => new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = -1 })).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Renders racing a dispose either finish or fail with <see cref="ObjectDisposedException"/>; nothing else escapes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersRacingDisposeFailCleanly()
    {
        var unexpected = new List<Exception>();
        for (var round = 0; round < Rounds; round++)
        {
            using var page = CreateScanPage();
            var tasks = new Task[Renderers];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => RenderUntilDisposed(page, unexpected));
            }

            await Task.Delay(round % MaxDelay);
            page.Document.Dispose();
            await Task.WhenAll(tasks);
        }

        await Assert.That(unexpected).IsEmpty();
    }

    /// <summary>Renders until the document is disposed under it, recording any exception that is not the expected one.</summary>
    /// <param name="page">The document and renderer.</param>
    /// <param name="unexpected">Receives unexpected exceptions.</param>
    private static void RenderUntilDisposed(RenderTestPage page, List<Exception> unexpected)
    {
        try
        {
            for (var i = 0; i < RendersPerThread; i++)
            {
                Render(page.Renderer, 0);
            }
        }
        catch (ObjectDisposedException)
        {
            // The document was disposed while the render ran, which is the clean failure.
        }
        catch (Exception exception)
        {
            lock (unexpected)
            {
                unexpected.Add(exception);
            }
        }
    }

    /// <summary>Renders one tile of a page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pageIndex">The page index.</param>
    private static void Render(PdfPageRenderer renderer, int pageIndex)
    {
        var pixels = new byte[TileSize * TileSize * BytesPerPixel];
        _ = renderer.Render(new(pageIndex, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }

    /// <summary>Creates a page that draws a gray scan.</summary>
    /// <returns>The page.</returns>
    private static RenderTestPage CreateScanPage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 200 0 0 200 0 0 cm /Im Do Q", };
        var samples = new byte[ScanSide * ScanSide];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(i % byte.MaxValue);
        }

        var image = pdf.AddStream(ScanEntries, samples);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        return new(pdf.ToBytes());
    }
}
