// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks the horizontal and wrapped scroll modes of the page layout.</summary>
public sealed class DocumentLayoutScrollModeTests
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

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 10;

    /// <summary>The pages that fit across the viewport when wrapped: four pages and three gaps are 430 wide, five are 540.</summary>
    private const int FitAcross = 4;

    /// <summary>The sides of the content that have a margin, and the pages in a spread.</summary>
    private const int Sides = 2;

    /// <summary>The rows the ten pages fill when wrapped.</summary>
    private const int WrappedRows = 3;

    /// <summary>A width that reaches a little way into a page.</summary>
    private const int Sliver = 2;

    /// <summary>A page wider than the viewport.</summary>
    private const float WidePage = 1000F;

    /// <summary>The distance from one page's left edge to the next.</summary>
    private const double Step = PageWidth + Spacing;

    /// <summary>The page after the first.</summary>
    private const int SecondPage = 1;

    /// <summary>The page after the second.</summary>
    private const int ThirdPage = 2;

    /// <summary>The last page.</summary>
    private const int LastPage = Pages - 1;

    /// <summary>Verifies that a horizontal layout puts every page in one row, side by side.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_PutsAllPagesInOneRow()
    {
        var layout = Create(ScrollMode.Horizontal, false);

        await Assert.That(layout.RowCount).IsEqualTo(1);
        await Assert.That(layout.GetPageBounds(SecondPage).X - layout.GetPageBounds(0).X).IsEqualTo(Step);
        await Assert.That(layout.GetPageBounds(LastPage).Y).IsEqualTo(layout.GetPageBounds(0).Y);
        await Assert.That(layout.ExtentWidth).IsEqualTo((Pages * PageWidth) + ((Pages - 1) * Spacing) + (Sides * Margin));
        await Assert.That(layout.ExtentHeight).IsEqualTo(PageHeight + (Sides * Margin));
    }

    /// <summary>Verifies that the pages in view are found by their horizontal position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_FindsVisiblePagesBySide()
    {
        var layout = Create(ScrollMode.Horizontal, false);
        var third = layout.GetPageBounds(ThirdPage);

        layout.GetVisiblePages(third.X + 1, third.X + Step + 1, 0, PageHeight, out var first, out var last);

        await Assert.That(first).IsEqualTo(ThirdPage);
        await Assert.That(last).IsEqualTo(ThirdPage + 1);
        await Assert.That(layout.GetPageNearest(third.X + 1, 0)).IsEqualTo(ThirdPage);
        await Assert.That(layout.HitTest(third.X + 1, third.Y + 1)).IsEqualTo(ThirdPage);
    }

    /// <summary>Verifies that a viewport beyond the last page shows nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_ShowsNothingBeyondTheLastPage()
    {
        var layout = Create(ScrollMode.Horizontal, false);

        layout.GetVisiblePages(layout.ExtentWidth + 1, layout.ExtentWidth + Viewport, 0, PageHeight, out var first, out var last);

        await Assert.That(first).IsEqualTo(-1);
        await Assert.That(last).IsLessThan(first);
    }

    /// <summary>Verifies that a right to left horizontal layout runs the pages from the right.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_RightToLeft_StartsAtTheRight()
    {
        var layout = Create(ScrollMode.Horizontal, true);
        var second = layout.GetPageBounds(SecondPage);

        layout.GetVisiblePages(second.X + 1, second.X + Sliver, 0, PageHeight, out var first, out var last);

        await Assert.That(layout.GetPageBounds(0).X).IsGreaterThan(layout.GetPageBounds(SecondPage).X);
        await Assert.That(first).IsEqualTo(SecondPage);
        await Assert.That(last).IsEqualTo(SecondPage);
        await Assert.That(layout.GetPageNearest(second.X + 1, 0)).IsEqualTo(SecondPage);
    }

    /// <summary>Verifies that a wrapped layout fills each row across the viewport.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Wrapped_FillsEachRowAcrossTheViewport()
    {
        var layout = Create(ScrollMode.Wrapped, false);

        await Assert.That(layout.RowCount).IsEqualTo(WrappedRows);
        await Assert.That(layout.GetPageBounds(FitAcross - 1).Y).IsEqualTo(layout.GetPageBounds(0).Y);
        await Assert.That(layout.GetPageBounds(FitAcross).Y).IsGreaterThan(layout.GetPageBounds(0).Y);
        await Assert.That(layout.GetPageBounds(FitAcross).X).IsEqualTo(layout.GetPageBounds(0).X);
        await Assert.That(layout.ExtentWidth).IsEqualTo(Viewport);
    }

    /// <summary>Verifies that a wrapped layout finds the pages in view by their rows.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Wrapped_FindsVisiblePagesByRow()
    {
        var layout = Create(ScrollMode.Wrapped, false);
        var secondRow = layout.GetPageBounds(FitAcross);

        layout.GetVisiblePages(0, Viewport, secondRow.Y + 1, secondRow.Y + Sliver, out var first, out var last);

        await Assert.That(first).IsEqualTo(FitAcross);
        await Assert.That(last).IsEqualTo((Sides * FitAcross) - 1);
    }

    /// <summary>Verifies that a page wider than the viewport has a row to itself.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Wrapped_GivesAWidePageItsOwnRow()
    {
        PageSize[] sizes = [new(PageWidth, PageHeight), new(WidePage, PageHeight), new(PageWidth, PageHeight)];

        var layout = DocumentLayout.Create(sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { Scroll = ScrollMode.Wrapped });

        await Assert.That(layout.RowCount).IsEqualTo(WrappedRows);
    }

    /// <summary>Creates a layout of equal pages.</summary>
    /// <param name="scroll">The scroll mode.</param>
    /// <param name="rightToLeft">Whether pages run right to left.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Create(ScrollMode scroll, bool rightToLeft)
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        return DocumentLayout.Create(sizes, new(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, Viewport) { Scroll = scroll, RightToLeft = rightToLeft });
    }
}
