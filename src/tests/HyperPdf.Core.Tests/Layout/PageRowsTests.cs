// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Tests for <see cref="PageRows"/>.</summary>
public sealed class PageRowsTests
{
    /// <summary>The number of pages in the test document.</summary>
    private const int Pages = 5;

    /// <summary>The last page index.</summary>
    private const int LastPage = Pages - 1;

    /// <summary>The second page.</summary>
    private const int Second = 1;

    /// <summary>The third page.</summary>
    private const int Third = 2;

    /// <summary>The fourth page.</summary>
    private const int Fourth = 3;

    /// <summary>The pages in a two page spread.</summary>
    private const int SpreadPages = 2;

    /// <summary>The rows five pages fill in either two page arrangement.</summary>
    private const int DualRows = 3;

    /// <summary>Verifies one page per row steps one page at a time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleStepsOnePage()
    {
        await Assert.That(PageRows.GetRowCount(Pages, PageLayoutMode.Single)).IsEqualTo(Pages);
        await Assert.That(PageRows.GetNextRowPage(Second, Pages, PageLayoutMode.Single)).IsEqualTo(Third);
        await Assert.That(PageRows.GetPreviousRowPage(Third, PageLayoutMode.Single)).IsEqualTo(Second);
        await Assert.That(PageRows.GetNextRowPage(LastPage, Pages, PageLayoutMode.Single)).IsEqualTo(LastPage);
        await Assert.That(PageRows.GetPreviousRowPage(0, PageLayoutMode.Single)).IsEqualTo(0);
    }

    /// <summary>Verifies two pages per row step a whole spread from either page of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualStepsBySpread()
    {
        await Assert.That(PageRows.GetRowCount(Pages, PageLayoutMode.Dual)).IsEqualTo(DualRows);
        await Assert.That(PageRows.GetNextRowPage(0, Pages, PageLayoutMode.Dual)).IsEqualTo(Third);
        await Assert.That(PageRows.GetNextRowPage(Second, Pages, PageLayoutMode.Dual)).IsEqualTo(Third);
        await Assert.That(PageRows.GetPreviousRowPage(Fourth, PageLayoutMode.Dual)).IsEqualTo(0);
        await Assert.That(PageRows.GetNextRowPage(LastPage, Pages, PageLayoutMode.Dual)).IsEqualTo(LastPage);
        await Assert.That(PageRows.GetRow(Fourth, PageLayoutMode.Dual)).IsEqualTo(Second);
    }

    /// <summary>Verifies the cover sits alone and the spreads after it pair the second and third pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoverStandsAlone()
    {
        PageRows.GetRowPages(0, Pages, PageLayoutMode.DualCover, out var coverFirst, out var coverCount);
        PageRows.GetRowPages(Second, Pages, PageLayoutMode.DualCover, out var spreadFirst, out var spreadCount);

        await Assert.That(PageRows.GetRowCount(Pages, PageLayoutMode.DualCover)).IsEqualTo(DualRows);
        await Assert.That(coverFirst).IsEqualTo(0);
        await Assert.That(coverCount).IsEqualTo(1);
        await Assert.That(spreadFirst).IsEqualTo(Second);
        await Assert.That(spreadCount).IsEqualTo(SpreadPages);
        await Assert.That(PageRows.GetNextRowPage(0, Pages, PageLayoutMode.DualCover)).IsEqualTo(Second);
        await Assert.That(PageRows.GetNextRowPage(Third, Pages, PageLayoutMode.DualCover)).IsEqualTo(Fourth);
        await Assert.That(PageRows.GetPreviousRowPage(Third, PageLayoutMode.DualCover)).IsEqualTo(0);
    }

    /// <summary>Verifies an empty document has no rows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyHasNoRows()
    {
        await Assert.That(PageRows.GetRowCount(0, PageLayoutMode.DualCover)).IsEqualTo(0);
        await Assert.That(PageRows.GetRowCount(0, PageLayoutMode.Dual)).IsEqualTo(0);
    }
}
