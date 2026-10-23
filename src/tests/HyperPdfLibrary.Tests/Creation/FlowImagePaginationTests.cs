// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests for <see cref="PdfFlowImage"/> sizing, <see cref="PdfFlowImageFit"/> pagination modes, and alignment.</summary>
public sealed class FlowImagePaginationTests
{
    /// <summary>The test page width in points (content width = 200 pt).</summary>
    private const float PageWidth = 240F;

    /// <summary>The test page height in points (content height = 200 pt).</summary>
    private const float PageHeight = 240F;

    /// <summary>The page margin in points.</summary>
    private const float Margin = 20F;

    /// <summary>The body font size in points.</summary>
    private const float BodySize = 11F;

    /// <summary>A tall requested image width that produces a 180 pt tall square image.</summary>
    private const float TallSquarePoints = 180F;

    /// <summary>An oversized requested image width (300 pt) wider than the 200 pt content area.</summary>
    private const float OversizedPoints = 300F;

    /// <summary>A small requested image width (48 pt) for testing alignment.</summary>
    private const float SmallImagePoints = 48F;

    /// <summary>The pixel width of the square test raster image.</summary>
    private const int SquarePixels = 40;

    /// <summary>The pixel height of a tall 1:2 aspect ratio test raster image.</summary>
    private const int TallRasterHeight = 80;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int BgraRedOffset = 2;

    /// <summary>The offset of the alpha channel in a BGRA pixel.</summary>
    private const int BgraAlphaOffset = 3;

    /// <summary>The threshold above which the red channel counts as bright.</summary>
    private const int BrightRed = 180;

    /// <summary>The threshold below which green and blue channels count as dark.</summary>
    private const int DarkOther = 80;

    /// <summary>The expected default 96 DPI width in points of a 40 px image (40 * 0.75 = 30).</summary>
    private const int DefaultDpiWidthPixels = 30;

    /// <summary>The expected content width in pixels (240 - 40 = 200).</summary>
    private const int ContentWidthPixels = 200;

    /// <summary>The pixel tolerance for rendered image bounds.</summary>
    private const int BoundsTolerance = 3;

    /// <summary>The expected number of pages when an image breaks to page 2.</summary>
    private const int TwoPages = 2;

    /// <summary>When <see cref="PdfFlowImage.WidthPoints"/> is omitted or non-positive, the image defaults to 96 DPI (0.75 pt/px) and respects <see cref="PdfFlowImage.Alignment"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Default96DpiSizingAndHorizontalAlignmentPositionImageWithinContentArea()
    {
        var leftBounds = FindRedBounds(RenderSingleImage(static image => new(image, "Left icon") { Alignment = PdfTableAlignment.Left }));
        var centerBounds = FindRedBounds(RenderSingleImage(static image => new(image, "Center icon", SmallImagePoints) { Alignment = PdfTableAlignment.Center }));
        var rightBounds = FindRedBounds(RenderSingleImage(static image => new(image, "Right icon", SmallImagePoints) { Alignment = PdfTableAlignment.Right }));

        await Assert.That(Math.Abs((leftBounds.Right - leftBounds.Left + 1) - DefaultDpiWidthPixels)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(Math.Abs(leftBounds.Left - (int)Margin)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(centerBounds.Left).IsGreaterThan(leftBounds.Left);
        await Assert.That(rightBounds.Left).IsGreaterThan(centerBounds.Left);
        await Assert.That(Math.Abs(rightBounds.Right - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(BoundsTolerance);
    }

    /// <summary><see cref="PdfFlowImageFit.ScaleToPage"/> breaks to a new page, whereas <see cref="PdfFlowImageFit.ScaleToRemaining"/> shrinks to fit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScaleToPageBreaksToNextPageWhileScaleToRemainingFitsOnCurrentPage()
    {
        var scaleToPagePdf = CreateFlowWithParagraphAndImage(PdfFlowImageFit.ScaleToPage, TallSquarePoints, SquarePixels, SquarePixels, out var scaleToPageCount);
        var scaleToRemainingPdf = CreateFlowWithParagraphAndImage(PdfFlowImageFit.ScaleToRemaining, TallSquarePoints, SquarePixels, SquarePixels, out var scaleToRemainingCount);

        await Assert.That(scaleToPageCount).IsEqualTo(TwoPages);
        await Assert.That(scaleToRemainingCount).IsEqualTo(1);

        var pageTwoBounds = FindRedBounds(RenderPage(scaleToPagePdf, 1));
        var remainingBounds = FindRedBounds(RenderPage(scaleToRemainingPdf, 0));

        var pageTwoWidth = pageTwoBounds.Right - pageTwoBounds.Left + 1;
        var remainingWidth = remainingBounds.Right - remainingBounds.Left + 1;

        await Assert.That(Math.Abs(pageTwoWidth - (int)TallSquarePoints)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(remainingWidth).IsLessThan(pageTwoWidth);
        await Assert.That(remainingBounds.Bottom).IsLessThanOrEqualTo((int)(PageHeight - Margin) + 1);
    }

    /// <summary><see cref="PdfFlowImageFit.ScaleToWidth"/> preserves content width and <see cref="PdfFlowImageFit.ClipOverflow"/> clips overflow to the content box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScaleToWidthAndClipOverflowClampRenderedPixelsToContentBox()
    {
        var scaleToPagePdf = CreateFlowWithParagraphAndImage(PdfFlowImageFit.ScaleToPage, OversizedPoints, SquarePixels, TallRasterHeight, out _);
        var scaleToWidthPdf = CreateFlowWithParagraphAndImage(PdfFlowImageFit.ScaleToWidth, OversizedPoints, SquarePixels, TallRasterHeight, out _);
        var clipOverflowPdf = CreateFlowWithParagraphAndImage(PdfFlowImageFit.ClipOverflow, OversizedPoints, SquarePixels, TallRasterHeight, out _);

        var scaleToPageBounds = FindRedBounds(RenderPage(scaleToPagePdf, 1));
        var scaleToWidthBounds = FindRedBounds(RenderPage(scaleToWidthPdf, 1));
        var clipOverflowBounds = FindRedBounds(RenderPage(clipOverflowPdf, 1));

        var pageFitWidth = scaleToPageBounds.Right - scaleToPageBounds.Left + 1;
        var widthFitWidth = scaleToWidthBounds.Right - scaleToWidthBounds.Left + 1;
        var clippedWidth = clipOverflowBounds.Right - clipOverflowBounds.Left + 1;

        await Assert.That(pageFitWidth).IsLessThan(ContentWidthPixels);
        await Assert.That(Math.Abs(widthFitWidth - ContentWidthPixels)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(Math.Abs(clippedWidth - ContentWidthPixels)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(scaleToWidthBounds.Bottom).IsLessThanOrEqualTo((int)(PageHeight - Margin) + 1);
        await Assert.That(clipOverflowBounds.Right).IsLessThanOrEqualTo((int)(PageWidth - Margin) + 1);
        await Assert.That(clipOverflowBounds.Bottom).IsLessThanOrEqualTo((int)(PageHeight - Margin) + 1);
    }

    /// <summary>Renders a single <see cref="PdfFlowImage"/> on a page.</summary>
    /// <param name="factory">Builds the <see cref="PdfFlowImage"/> from the embedded image.</param>
    /// <returns>The rendered first page.</returns>
    private static RenderedImage RenderSingleImage(Func<PdfCreatedImage, PdfFlowImage> factory)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateRedRaster(SquarePixels, SquarePixels), PdfImageOptions.Default, CancellationToken.None);
        _ = PdfTextFlow.Add(creator, [factory(image)], new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize }, CancellationToken.None);
        return RenderPage(creator.ToArray(), 0);
    }

    /// <summary>Creates a flow with an introductory paragraph followed by an image with the specified fit policy.</summary>
    /// <param name="fit">The image fit policy.</param>
    /// <param name="widthPoints">The requested image width in points.</param>
    /// <param name="pixelWidth">The raster pixel width.</param>
    /// <param name="pixelHeight">The raster pixel height.</param>
    /// <param name="pages">Receives the total page count.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateFlowWithParagraphAndImage(PdfFlowImageFit fit, float widthPoints, int pixelWidth, int pixelHeight, out int pages)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateRedRaster(pixelWidth, pixelHeight), PdfImageOptions.Default, CancellationToken.None);
        PdfFlowBlock[] blocks =
        [
            new PdfFlowParagraph("Introductory paragraph occupying the top of the page before the image."),
            new PdfFlowImage(image, "Test image", widthPoints) { Fit = fit },
        ];

        pages = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize }, CancellationToken.None);
        return creator.ToArray();
    }

    /// <summary>Creates a solid red BGRA32 raster image.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The raster image.</returns>
    private static PdfRasterImage CreateRedRaster(int width, int height)
    {
        var pixels = new byte[width * height * BgraBytes];
        for (var i = 0; i < pixels.Length; i += BgraBytes)
        {
            pixels[i + BgraRedOffset] = byte.MaxValue;
            pixels[i + BgraAlphaOffset] = byte.MaxValue;
        }

        return new(PdfRasterFormat.Bgra32, width, height, pixels);
    }

    /// <summary>Renders a page at 1 pixel per point.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The rendered image.</returns>
    private static RenderedImage RenderPage(byte[] pdf, int pageIndex)
    {
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, pageIndex), 0, 1F, out var width, out var height);
        return page.RenderTile(new(pageIndex, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Finds the bounding box of bright red pixels in the rendered image.</summary>
    /// <param name="image">The rendered image.</param>
    /// <returns>The bounding box of red pixels.</returns>
    private static PixelBox FindRedBounds(RenderedImage image)
    {
        var minX = int.MaxValue;
        var maxX = -1;
        var minY = int.MaxValue;
        var maxY = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red <= BrightRed || pixel.Green >= DarkOther || pixel.Blue >= DarkOther)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        return new(minX, maxX, minY, maxY);
    }

    /// <summary>The pixel bounding box of a colour region.</summary>
    /// <param name="Left">The leftmost column.</param>
    /// <param name="Right">The rightmost column.</param>
    /// <param name="Top">The topmost row.</param>
    /// <param name="Bottom">The bottommost row.</param>
    private readonly record struct PixelBox(int Left, int Right, int Top, int Bottom);
}
