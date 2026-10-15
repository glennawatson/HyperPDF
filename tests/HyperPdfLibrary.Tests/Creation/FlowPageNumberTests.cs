// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Flow page numbering options (<see cref="PdfFlowOptions"/>): horizontal alignment, vertical footer offset, prefix/suffix formatting and portrait/landscape bounds.</summary>
public sealed class FlowPageNumberTests
{
    /// <summary>The width of portrait test pages.</summary>
    private const float PortraitWidth = 300F;

    /// <summary>The height of portrait test pages.</summary>
    private const float PortraitHeight = 400F;

    /// <summary>The width of landscape test pages.</summary>
    private const float LandscapeWidth = 400F;

    /// <summary>The height of landscape test pages.</summary>
    private const float LandscapeHeight = 300F;

    /// <summary>The page margin.</summary>
    private const float Margin = 25F;

    /// <summary>The body size of the test text.</summary>
    private const float BodySize = 11F;

    /// <summary>A low footer offset near the bottom edge of the page.</summary>
    private const float LowFooterOffset = 10F;

    /// <summary>A higher footer offset further from the bottom edge of the page.</summary>
    private const float HighFooterOffset = 32F;

    /// <summary>The top row of the footer scan band on portrait pages.</summary>
    private const int PortraitFooterTop = 340;

    /// <summary>The bottom row of the footer scan band on portrait pages.</summary>
    private const int PortraitFooterBottom = 398;

    /// <summary>The top row of the footer scan band on landscape pages.</summary>
    private const int LandscapeFooterTop = 240;

    /// <summary>The bottom row of the footer scan band on landscape pages.</summary>
    private const int LandscapeFooterBottom = 298;

    /// <summary>The level below which a pixel channel counts as dark ink.</summary>
    private const int Dark = 128;

    /// <summary>The number of repeated words used to force a second page.</summary>
    private const int MultiPageWordCount = 150;

    /// <summary>The expected page count of the multi-page test.</summary>
    private const int ExpectedMultiPages = 2;

    /// <summary>An invalid negative footer offset.</summary>
    private const float NegativeOffset = -4F;

    /// <summary>Left, centre and right <see cref="PdfFlowOptions.PageNumberAlignment"/> position the footer within the content margins on both portrait and landscape pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageNumberAlignmentPositionsFooterInPortraitAndLandscape()
    {
        var portraitLeft = FindFooterInk(PortraitWidth, PortraitHeight, PdfTableAlignment.Left, PortraitFooterTop, PortraitFooterBottom);
        var portraitCenter = FindFooterInk(PortraitWidth, PortraitHeight, PdfTableAlignment.Center, PortraitFooterTop, PortraitFooterBottom);
        var portraitRight = FindFooterInk(PortraitWidth, PortraitHeight, PdfTableAlignment.Right, PortraitFooterTop, PortraitFooterBottom);

        var landscapeLeft = FindFooterInk(LandscapeWidth, LandscapeHeight, PdfTableAlignment.Left, LandscapeFooterTop, LandscapeFooterBottom);
        var landscapeCenter = FindFooterInk(LandscapeWidth, LandscapeHeight, PdfTableAlignment.Center, LandscapeFooterTop, LandscapeFooterBottom);
        var landscapeRight = FindFooterInk(LandscapeWidth, LandscapeHeight, PdfTableAlignment.Right, LandscapeFooterTop, LandscapeFooterBottom);

        await Assert.That(portraitLeft.First).IsGreaterThanOrEqualTo((int)Margin - 1);
        await Assert.That(portraitCenter.First).IsGreaterThan(portraitLeft.First);
        await Assert.That(portraitRight.First).IsGreaterThan(portraitCenter.First);
        await Assert.That(portraitRight.Last).IsLessThanOrEqualTo((int)(PortraitWidth - Margin) + 1);

        await Assert.That(landscapeLeft.First).IsGreaterThanOrEqualTo((int)Margin - 1);
        await Assert.That(landscapeCenter.First).IsGreaterThan(landscapeLeft.First);
        await Assert.That(landscapeRight.First).IsGreaterThan(landscapeCenter.First);
        await Assert.That(landscapeRight.Last).IsLessThanOrEqualTo((int)(LandscapeWidth - Margin) + 1);
    }

    /// <summary><see cref="PdfFlowOptions.PageNumberOffset"/> controls the vertical distance of the page-number baseline from the bottom edge of the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageNumberOffsetShiftsVerticalPosition()
    {
        var lowRow = FindFooterTopRow(LowFooterOffset);
        var highRow = FindFooterTopRow(HighFooterOffset);

        await Assert.That(lowRow).IsGreaterThan(highRow);
    }

    /// <summary><see cref="PdfFlowOptions.PageNumberPrefix"/> and <see cref="PdfFlowOptions.PageNumberSuffix"/> format the page number across multiple pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageNumberPrefixAndSuffixFormatMultiPageLabels()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var longText = string.Join(' ', Enumerable.Range(0, MultiPageWordCount).Select(static i => $"word{i:D3}"));
        var options = new PdfFlowOptions(font)
        {
            PageWidth = PortraitWidth,
            PageHeight = PortraitHeight,
            Margin = Margin,
            BodySize = BodySize,
            PageNumbers = true,
            PageNumberAlignment = PdfTableAlignment.Right,
            PageNumberPrefix = "Page ",
            PageNumberSuffix = " of 2",
        };

        var pages = PdfTextFlow.Add(creator, [new PdfFlowParagraph(longText)], options, CancellationToken.None);
        var pdf = creator.ToArray();
        var pageOneText = await CreatedFiles.TextOfAsync(pdf, 0);
        var pageTwoText = await CreatedFiles.TextOfAsync(pdf, 1);

        await Assert.That(pages).IsEqualTo(ExpectedMultiPages);
        await Assert.That(pageOneText.Contains("Page 1 of 2", StringComparison.Ordinal)).IsTrue();
        await Assert.That(pageTwoText.Contains("Page 2 of 2", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>An out-of-range <see cref="PdfFlowOptions.PageNumberOffset"/> throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidPageNumberOffsetThrows()
    {
        await Assert.That(static () => RenderWithOffset(NegativeOffset)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(static () => RenderWithOffset(PortraitHeight)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    /// <summary>Renders a single page with the given page-number alignment and returns the horizontal ink span in the footer band.</summary>
    /// <param name="width">The page width.</param>
    /// <param name="height">The page height.</param>
    /// <param name="alignment">The page-number alignment.</param>
    /// <param name="scanTop">The top row of the footer scan band.</param>
    /// <param name="scanBottom">The bottom row of the footer scan band.</param>
    /// <returns>The first and last ink columns.</returns>
    private static InkSpan FindFooterInk(float width, float height, PdfTableAlignment alignment, int scanTop, int scanBottom)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var options = new PdfFlowOptions(font)
        {
            PageWidth = width,
            PageHeight = height,
            Margin = Margin,
            BodySize = BodySize,
            PageNumbers = true,
            PageNumberAlignment = alignment,
            PageNumberPrefix = "P",
        };

        _ = PdfTextFlow.Add(creator, [new PdfFlowParagraph("Body")], options, CancellationToken.None);
        var render = RenderAt(creator.ToArray(), 0);
        var first = -1;
        var last = -1;
        for (var x = 0; x < render.Width; x++)
        {
            for (var y = scanTop; y <= scanBottom; y++)
            {
                if (!IsDark(render, x, y))
                {
                    continue;
                }

                first = first < 0 ? x : first;
                last = x;
            }
        }

        return new(first, last);
    }

    /// <summary>Renders a page with the given <paramref name="offset"/> and returns the first dark pixel row in the footer band.</summary>
    /// <param name="offset">The footer offset from the bottom edge.</param>
    /// <returns>The topmost dark row in the footer band.</returns>
    private static int FindFooterTopRow(float offset)
    {
        var render = RenderWithOffset(offset);
        for (var y = PortraitFooterTop; y <= PortraitFooterBottom; y++)
        {
            for (var x = 0; x < render.Width; x++)
            {
                if (IsDark(render, x, y))
                {
                    return y;
                }
            }
        }

        return -1;
    }

    /// <summary>Renders a single page with the given <paramref name="offset"/>.</summary>
    /// <param name="offset">The footer offset from the bottom edge.</param>
    /// <returns>The rendered page.</returns>
    private static RenderedImage RenderWithOffset(float offset)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var options = new PdfFlowOptions(font) { PageWidth = PortraitWidth, PageHeight = PortraitHeight, Margin = Margin, BodySize = BodySize, PageNumbers = true, PageNumberOffset = offset };

        _ = PdfTextFlow.Add(creator, [new PdfFlowParagraph("Body")], options, CancellationToken.None);
        return RenderAt(creator.ToArray(), 0);
    }

    /// <summary>Renders a page at one pixel per point.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="index">The zero-based page.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderAt(byte[] pdf, int index)
    {
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, index), 0, 1F, out var width, out var height);
        return page.RenderTile(new(index, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Determines whether a pixel is dark ink.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row from the top.</param>
    /// <returns><see langword="true"/> when every channel is below <see cref="Dark"/>.</returns>
    private static bool IsDark(RenderedImage image, int x, int y)
    {
        var pixel = CreationTestKit.PixelAt(image, x, y);
        return pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark;
    }

    /// <summary>The columns of the leftmost and rightmost ink in a region.</summary>
    /// <param name="First">The first column with ink.</param>
    /// <param name="Last">The last column with ink.</param>
    private readonly record struct InkSpan(int First, int Last);
}
