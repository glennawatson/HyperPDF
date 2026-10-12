// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Tests for <see cref="ZoomCalculator"/>.</summary>
public sealed class ZoomCalculatorTests
{
    /// <summary>The comparison tolerance.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The number of sides a margin applies to.</summary>
    private const double Sides = 2;

    /// <summary>A US letter page.</summary>
    private static readonly PageSize Letter = new(612, 792);

    /// <summary>A page narrower than <see cref="Letter"/>.</summary>
    private static readonly PageSize Narrow = new(400, 792);

    /// <summary>Verifies fit width fills the available width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthFillsViewport()
    {
        const double viewportWidth = 1036;
        const double margin = 12;
        var zoom = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitWidth, viewportWidth, viewportWidth, 0, margin));

        var expected = (viewportWidth - (Sides * margin)) / Letter.Width / ZoomCalculator.PixelsPerPoint;
        await Assert.That(zoom).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>Verifies fit page is limited by height on a wide viewport.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitPageIsLimitedByHeight()
    {
        const double viewportWidth = 2000;
        const double viewportHeight = 600;
        var zoom = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitPage, viewportWidth, viewportHeight, 0, 0));

        await Assert.That(zoom).IsEqualTo(viewportHeight / Letter.Height / ZoomCalculator.PixelsPerPoint).Within(Tolerance);
    }

    /// <summary>Verifies fit width in two page mode fills the viewport with the widest spread, not twice the widest page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthFitsTheWidestSpread()
    {
        const double viewportWidth = 1036;
        const double margin = 12;
        const double spacing = 10;
        var zoom = ZoomCalculator.GetFitZoom([Letter, Narrow, Narrow, Narrow], new(PageRotation.None, PageLayoutMode.Dual, ZoomMode.FitWidth, viewportWidth, viewportWidth, spacing, margin));

        var expected = (viewportWidth - (Sides * margin) - spacing) / (Letter.Width + Narrow.Width) / ZoomCalculator.PixelsPerPoint;
        await Assert.That(zoom).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>Verifies the cover fills half the spread width, leaving room for the empty facing page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthLeavesRoomBesideTheCover()
    {
        const double viewportWidth = 1036;
        const double margin = 12;
        const double spacing = 10;
        var zoom = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.DualCover, ZoomMode.FitWidth, viewportWidth, viewportWidth, spacing, margin));

        var expected = (viewportWidth - (Sides * margin) - spacing) / (Sides * Letter.Width) / ZoomCalculator.PixelsPerPoint;
        await Assert.That(zoom).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>Verifies a one page document in two page mode fits like a single page, as its only row holds one page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthOfOnePageInTwoPageMode()
    {
        const double viewportWidth = 1036;
        const double margin = 12;
        const double spacing = 10;
        var dual = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Dual, ZoomMode.FitWidth, viewportWidth, viewportWidth, spacing, margin));
        var single = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitWidth, viewportWidth, viewportWidth, spacing, margin));

        await Assert.That(dual).IsEqualTo(single).Within(Tolerance);
    }

    /// <summary>Verifies zoom steps move monotonically and clamp.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomStepsAreMonotonic()
    {
        const double start = 1.0;

        await Assert.That(ZoomCalculator.ZoomIn(start)).IsGreaterThan(start);
        await Assert.That(ZoomCalculator.ZoomOut(start)).IsLessThan(start);
        await Assert.That(ZoomCalculator.ZoomIn(ZoomCalculator.MaxZoom)).IsEqualTo(ZoomCalculator.MaxZoom);
        await Assert.That(ZoomCalculator.ZoomOut(ZoomCalculator.MinZoom)).IsEqualTo(ZoomCalculator.MinZoom);
    }
}
