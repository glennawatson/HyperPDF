// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks the scroll offsets that show a place on a page.</summary>
public sealed class PageScrollingTests
{
    /// <summary>The page width in the test.</summary>
    private const float PageWidth = 100F;

    /// <summary>The page height in the test.</summary>
    private const float PageHeight = 200F;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 10;

    /// <summary>The margin around the pages.</summary>
    private const double Margin = 20;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 500;

    /// <summary>A viewport height that shows a page and a half.</summary>
    private const double ViewportHeight = 300;

    /// <summary>The space the view keeps around a place.</summary>
    private const double ContentMargin = 16;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 10;

    /// <summary>The page the tests scroll to.</summary>
    private const int ThirdPage = 2;

    /// <summary>The offset a reader already has.</summary>
    private const double Current = 77;

    /// <summary>An offset far past any slot.</summary>
    private const double FarOffset = 100_000;

    /// <summary>A position a quarter of the way down a page.</summary>
    private const double Quarter = 0.25;

    /// <summary>A continuous layout puts a place at its page position less the margin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContinuousTopIsThePagePositionLessTheMargin()
    {
        var layout = Create(ScrollMode.Vertical, false);
        var bounds = layout.GetPageBounds(ThirdPage);

        var top = PageScrolling.GetTop(layout, ThirdPage, Quarter, ContentMargin, ViewportHeight);

        await Assert.That(top).IsEqualTo(bounds.Y + (bounds.Height * Quarter) - ContentMargin);
    }

    /// <summary>A page by page layout keeps the offset inside the page's slot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageByPageClampsInsideTheSlot()
    {
        var layout = Create(ScrollMode.Vertical, true);
        layout.GetRowExtent(ThirdPage, out var slotTop, out var slotBottom);

        var below = PageScrolling.ClampToSlot(layout, ThirdPage, FarOffset, ViewportHeight);
        var above = PageScrolling.ClampToSlot(layout, ThirdPage, -FarOffset, ViewportHeight);

        await Assert.That(below).IsEqualTo(Math.Max(slotTop, slotBottom - ViewportHeight));
        await Assert.That(above).IsEqualTo(slotTop);
    }

    /// <summary>A continuous layout leaves the offset alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContinuousLayoutDoesNotClamp()
    {
        var layout = Create(ScrollMode.Vertical, false);

        var offset = PageScrolling.ClampToSlot(layout, ThirdPage, FarOffset, ViewportHeight);

        await Assert.That(offset).IsEqualTo(FarOffset);
    }

    /// <summary>Without a viewport height there is nothing to clamp against.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownViewportDoesNotClamp()
    {
        var layout = Create(ScrollMode.Vertical, true);

        var offset = PageScrolling.ClampToSlot(layout, ThirdPage, FarOffset, null);

        await Assert.That(offset).IsEqualTo(FarOffset);
    }

    /// <summary>A horizontal layout scrolls to the page's left side and keeps the reader's vertical offset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HorizontalLayoutMovesSideways()
    {
        var layout = Create(ScrollMode.Horizontal, false);
        var bounds = layout.GetPageBounds(ThirdPage);

        var left = PageScrolling.GetLeft(layout, ThirdPage, Current, ContentMargin);
        var top = PageScrolling.GetScrollTop(layout, ThirdPage, Quarter, Current, ContentMargin, ViewportHeight);

        await Assert.That(left).IsEqualTo(Math.Max(0, bounds.X - ContentMargin));
        await Assert.That(top).IsEqualTo(Current);
    }

    /// <summary>A vertical layout keeps the reader's horizontal offset and scrolls to the place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalLayoutMovesDown()
    {
        var layout = Create(ScrollMode.Vertical, false);

        var left = PageScrolling.GetLeft(layout, ThirdPage, Current, ContentMargin);
        var top = PageScrolling.GetScrollTop(layout, ThirdPage, Quarter, Current, ContentMargin, ViewportHeight);

        await Assert.That(left).IsEqualTo(Current);
        await Assert.That(top).IsEqualTo(PageScrolling.GetTop(layout, ThirdPage, Quarter, ContentMargin, ViewportHeight));
    }

    /// <summary>A span already in view leaves the offset alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpanInViewKeepsTheOffset()
    {
        var offset = PageScrolling.GetOffsetToShow(Current, ViewportHeight, Current + Quarter, Current + ContentMargin, ContentMargin);

        await Assert.That(offset).IsEqualTo(Current);
    }

    /// <summary>A span above the view scrolls up to it, keeping the margin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpanAboveScrollsUp()
    {
        var offset = PageScrolling.GetOffsetToShow(FarOffset, ViewportHeight, Current, Current + ContentMargin, ContentMargin);

        await Assert.That(offset).IsEqualTo(Current - ContentMargin);
    }

    /// <summary>A span below the view scrolls down until its far edge shows, keeping the margin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpanBelowScrollsDown()
    {
        const double far = Current + ViewportHeight + ContentMargin;

        var offset = PageScrolling.GetOffsetToShow(0, ViewportHeight, far - ContentMargin, far, ContentMargin);

        await Assert.That(offset).IsEqualTo(far - ViewportHeight + ContentMargin);
    }

    /// <summary>A missing layout is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingLayoutIsRefused() =>
        await Assert.That(static () => PageScrolling.GetLeft(null!, 0, 0, 0)).Throws<ArgumentNullException>();

    /// <summary>Creates a layout of equal pages.</summary>
    /// <param name="scroll">The scroll mode.</param>
    /// <param name="pageByPage">Whether the layout scrolls page by page.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Create(ScrollMode scroll, bool pageByPage)
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        return DocumentLayout.Create(sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { Scroll = scroll, PageByPage = pageByPage, ViewportHeight = ViewportHeight });
    }
}
