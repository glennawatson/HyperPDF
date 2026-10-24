// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that preparing a neighbouring page records it without pushing out the page on screen.</summary>
public sealed class PrepareAheadTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int PageCount = 4;

    /// <summary>The pictures kept after one page is drawn and one is prepared.</summary>
    private const int DrawnAndPrepared = 2;

    /// <summary>The scale of the requests.</summary>
    private const float Scale = 1;

    /// <summary>The size of the tile, in pixels.</summary>
    private const int TileSize = 64;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>With room for every page, a prepared neighbour is cached and drawing it later records nothing more.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparedNeighbourIsCached()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(PageCount), null);
        using var renderer = new PdfPageRenderer(document);
        Draw(renderer, 0);

        var prepared = renderer.PrepareAhead(Request(1), CancellationToken.None);
        var bytesAfterPrepare = renderer.PictureBytes;
        Draw(renderer, 1);

        await Assert.That(prepared).IsTrue();
        await Assert.That(renderer.PictureCount).IsEqualTo(DrawnAndPrepared);
        await Assert.That(renderer.PictureBytes).IsEqualTo(bytesAfterPrepare);
    }

    /// <summary>When the picture limit is tiny, the neighbour is dropped and the page that was drawn is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeighbourDoesNotDisplaceTheDrawnPage()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(PageCount), null);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = 1 });
        Draw(renderer, 0);
        var drawnBytes = renderer.PictureBytes;

        var prepared = renderer.PrepareAhead(Request(1), CancellationToken.None);

        await Assert.That(prepared).IsTrue();
        await Assert.That(renderer.PictureCount).IsEqualTo(1);
        await Assert.That(renderer.PictureBytes).IsEqualTo(drawnBytes);
    }

    /// <summary>An ordinary prepare of the same page does take the newest place, which is the difference prepare-ahead makes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryPrepareTakesTheNewestPlace()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(PageCount), null);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = 1 });
        Draw(renderer, 0);

        _ = renderer.Prepare(Request(1), CancellationToken.None);
        Draw(renderer, 1);

        // Page 1 was the newest, so it was already kept: drawing it recorded nothing and the single kept page is page 1.
        await Assert.That(renderer.PictureCount).IsEqualTo(1);
    }

    /// <summary>A page outside the document is not prepared.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPageIsNotPrepared()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(1), null);
        using var renderer = new PdfPageRenderer(document);

        await Assert.That(renderer.PrepareAhead(Request(PageCount), CancellationToken.None)).IsFalse();
    }

    /// <summary>A cancelled token stops preparation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsPreparation()
    {
        using var document = SkiaTestRegistration.Open(TestPdf.Create(PageCount), null);
        using var renderer = new PdfPageRenderer(document);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => renderer.PrepareAhead(Request(1), source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Makes a request for a page's pictures.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The request.</returns>
    private static PdfTileRequest Request(int pageIndex) => new(pageIndex, Scale, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>Draws a tile of a page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pageIndex">The page.</param>
    private static void Draw(PdfPageRenderer renderer, int pageIndex)
    {
        var pixels = new byte[TileSize * TileSize * BytesPerPixel];
        _ = renderer.Render(Request(pageIndex), new(pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }
}
