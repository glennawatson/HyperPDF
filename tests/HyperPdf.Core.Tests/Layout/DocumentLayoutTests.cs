// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Tests for <see cref="DocumentLayout"/>.</summary>
public sealed class DocumentLayoutTests
{
    /// <summary>The page width used in tests.</summary>
    private const float PageWidth = 100F;

    /// <summary>The page height used in tests.</summary>
    private const float PageHeight = 200F;

    /// <summary>The spacing.</summary>
    private const double Spacing = 10;

    /// <summary>The margin.</summary>
    private const double Margin = 20;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 500;

    /// <summary>The number of pages in the test document.</summary>
    private const int Pages = 5;

    /// <summary>The number of rows five pages occupy in either dual mode.</summary>
    private const int DualRows = 3;

    /// <summary>The viewport height for page by page tests.</summary>
    private const double SlotHeight = 300;

    /// <summary>The left page of the second dual spread.</summary>
    private const int SpreadFirstPage = 2;

    /// <summary>The right page of the second dual spread.</summary>
    private const int SpreadSecondPage = 3;

    /// <summary>A zoom making pages taller than the viewport.</summary>
    private const double ZoomedScale = 2;

    /// <summary>Divisor for centring and halving.</summary>
    private const double Half = 2;

    /// <summary>Verifies single column positions, centring and extent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleColumnStacksPages()
    {
        var layout = Create(PageLayoutMode.Single, 1);

        var first = layout.GetPageBounds(0);
        var second = layout.GetPageBounds(1);
        await Assert.That(first).IsEqualTo(new((Viewport - PageWidth) / Half, Margin, PageWidth, PageHeight));
        await Assert.That(second.Y).IsEqualTo(Margin + PageHeight + Spacing);
        await Assert.That(layout.ExtentHeight).IsEqualTo((Half * Margin) + (Pages * PageHeight) + ((Pages - 1) * Spacing));
        await Assert.That(layout.ExtentWidth).IsEqualTo(Viewport);
    }

    /// <summary>Verifies the visible page range lookup.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsVisiblePages()
    {
        var layout = Create(PageLayoutMode.Single, 1);
        const double top = 250;
        const double bottom = 480;
        const int expectedFirst = 1;
        const int expectedLast = 2;

        layout.GetVisiblePages(top, bottom, out var first, out var last);

        await Assert.That(first).IsEqualTo(expectedFirst);
        await Assert.That(last).IsEqualTo(expectedLast);
    }

    /// <summary>Verifies nothing is visible past the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingVisibleBeyondExtent()
    {
        var layout = Create(PageLayoutMode.Single, 1);

        layout.GetVisiblePages(layout.ExtentHeight + 1, layout.ExtentHeight + Viewport, out var first, out var last);

        await Assert.That(first).IsEqualTo(-1);
        await Assert.That(last).IsLessThan(first);
    }

    /// <summary>Verifies the dual cover arrangement places page 1 alone on the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualCoverPutsFirstPageOnTheRight()
    {
        var layout = Create(PageLayoutMode.DualCover, 1);
        const int secondPage = 1;
        const int thirdPage = 2;

        var cover = layout.GetPageBounds(0);
        var left = layout.GetPageBounds(secondPage);
        var right = layout.GetPageBounds(thirdPage);
        await Assert.That(cover.X).IsGreaterThanOrEqualTo(Viewport / Half);
        await Assert.That(left.Y).IsEqualTo(right.Y);
        await Assert.That(right.X).IsEqualTo(left.Right + Spacing);
        await Assert.That(cover.X).IsEqualTo(right.X);
        await Assert.That(layout.RowCount).IsEqualTo(DualRows);
    }

    /// <summary>Verifies dual mode pairs pages starting from the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualPairsFromFirstPage()
    {
        var layout = Create(PageLayoutMode.Dual, 1);

        await Assert.That(layout.GetPageBounds(0).Y).IsEqualTo(layout.GetPageBounds(1).Y);
        await Assert.That(layout.RowCount).IsEqualTo(DualRows);
    }

    /// <summary>Verifies hit testing and nearest page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestsPages()
    {
        var layout = Create(PageLayoutMode.Single, 1);
        var bounds = layout.GetPageBounds(1);
        const int secondPage = 1;

        await Assert.That(layout.HitTest(bounds.X + 1, bounds.Y + 1)).IsEqualTo(secondPage);
        await Assert.That(layout.HitTest(1, bounds.Y + 1)).IsEqualTo(-1);
        await Assert.That(layout.GetPageNearest(bounds.Bottom + (Spacing / Half))).IsEqualTo(secondPage + 1);
    }

    /// <summary>Verifies the current page stays on the page asked for while its spread is shown, and follows the row otherwise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CurrentPageKeepsThePageInTheSpread()
    {
        var layout = Create(PageLayoutMode.Dual, 1);
        layout.GetRowExtent(SpreadSecondPage, out var top, out var bottom);
        var middle = (top + bottom) / Half;

        await Assert.That(layout.GetCurrentPage(middle, SpreadSecondPage)).IsEqualTo(SpreadSecondPage);
        await Assert.That(layout.GetCurrentPage(middle, SpreadFirstPage)).IsEqualTo(SpreadFirstPage);
        await Assert.That(layout.GetCurrentPage(middle, 0)).IsEqualTo(SpreadFirstPage);
        await Assert.That(layout.GetCurrentPage(layout.ExtentHeight, 0)).IsEqualTo(Pages - 1);
        await Assert.That(DocumentLayout.Empty.GetCurrentPage(0, 0)).IsEqualTo(-1);
    }

    /// <summary>Verifies rotation swaps page dimensions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationSwapsDimensions()
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        var layout = DocumentLayout.Create(sizes, new(PageRotation.Rotate90, PageLayoutMode.Single, 1, Spacing, Margin, Viewport));

        await Assert.That(layout.GetPageBounds(0).Width).IsEqualTo(PageHeight);
        await Assert.That(layout.GetPageBounds(0).Height).IsEqualTo(PageWidth);
    }

    /// <summary>Verifies page by page slots fill the viewport, centre their page and show one row at a time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageByPageGivesEachRowAViewport()
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        var layout = DocumentLayout.Create(sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { PageByPage = true, ViewportHeight = SlotHeight });

        layout.GetRowExtent(1, out var top, out var bottom);
        var page = layout.GetPageBounds(1);
        layout.GetVisiblePages(top, top + SlotHeight - 1, out var first, out var last);

        await Assert.That(top).IsEqualTo(SlotHeight);
        await Assert.That(bottom).IsEqualTo(SlotHeight * Half);
        await Assert.That(page.Y).IsEqualTo(top + ((SlotHeight - PageHeight) / Half));
        await Assert.That(first).IsEqualTo(1);
        await Assert.That(last).IsEqualTo(1);
        await Assert.That(layout.ExtentHeight).IsEqualTo(SlotHeight * Pages);
    }

    /// <summary>Verifies a zoomed page taller than the viewport gets a slot of its own height plus margins.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageByPageGrowsForTallPages()
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        var layout = DocumentLayout.Create(sizes, new(PageRotation.None, PageLayoutMode.Dual, ZoomedScale, Spacing, Margin, Viewport) { PageByPage = true, ViewportHeight = SlotHeight });

        layout.GetRowExtent(SpreadSecondPage, out var top, out var bottom);

        await Assert.That(bottom - top).IsEqualTo((PageHeight * ZoomedScale) + (Half * Margin));
        await Assert.That(layout.GetPageBounds(SpreadFirstPage).Y).IsEqualTo(top + Margin);
    }

    /// <summary>Creates a layout of identical pages.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Create(PageLayoutMode mode, double scale)
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        return DocumentLayout.Create(sizes, new(PageRotation.None, mode, scale, Spacing, Margin, Viewport));
    }
}
