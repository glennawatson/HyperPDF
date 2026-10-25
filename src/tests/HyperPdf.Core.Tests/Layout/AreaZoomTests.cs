// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Tests for <see cref="AreaZoom"/>.</summary>
public sealed class AreaZoomTests
{
    /// <summary>The comparison tolerance.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>Half, for the middle of the view.</summary>
    private const double Half = 0.5;

    /// <summary>The view's width.</summary>
    private const double ViewportWidth = 1000;

    /// <summary>The view's height.</summary>
    private const double ViewportHeight = 800;

    /// <summary>Verifies a wide area fills the view's width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideAreaFillsTheWidth()
    {
        const double width = 250;
        const double height = 100;
        var zoom = AreaZoom.GetZoom(1, width, height, ViewportWidth, ViewportHeight);

        await Assert.That(zoom).IsEqualTo(ViewportWidth / width).Within(Tolerance);
    }

    /// <summary>Verifies a tall area fills the view's height, and the zoom builds on the current zoom.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TallAreaFillsTheHeight()
    {
        const double current = 1.5;
        const double width = 100;
        const double height = 400;
        var zoom = AreaZoom.GetZoom(current, width, height, ViewportWidth, ViewportHeight);

        await Assert.That(zoom).IsEqualTo(current * ViewportHeight / height).Within(Tolerance);
    }

    /// <summary>Verifies a press with almost no drag zooms in one step, and a tiny area stays within the zoom limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickZoomsInOneStepAndLimitsHold()
    {
        const double tiny = 1;
        const double sliver = 9;
        var click = AreaZoom.GetZoom(1, tiny, tiny, ViewportWidth, ViewportHeight);
        var huge = AreaZoom.GetZoom(ZoomCalculator.MaxZoom, sliver, sliver, ViewportWidth, ViewportHeight);

        await Assert.That(AreaZoom.IsClick(tiny, tiny)).IsTrue();
        await Assert.That(AreaZoom.IsClick(sliver, tiny)).IsFalse();
        await Assert.That(click).IsEqualTo(ZoomCalculator.ZoomIn(1)).Within(Tolerance);
        await Assert.That(huge).IsEqualTo(ZoomCalculator.MaxZoom).Within(Tolerance);
    }

    /// <summary>Verifies the centred offset puts the point in the middle, kept within the content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentresWithinTheContent()
    {
        const double extent = 5000;
        const double middle = 2000;
        const double nearStart = 100;
        const double nearEnd = 4950;

        await Assert.That(AreaZoom.GetCentredOffset(middle, ViewportHeight, extent)).IsEqualTo(middle - (ViewportHeight * Half)).Within(Tolerance);
        await Assert.That(AreaZoom.GetCentredOffset(nearStart, ViewportHeight, extent)).IsEqualTo(0).Within(Tolerance);
        await Assert.That(AreaZoom.GetCentredOffset(nearEnd, ViewportHeight, extent)).IsEqualTo(extent - ViewportHeight).Within(Tolerance);
        await Assert.That(AreaZoom.GetCentredOffset(middle, ViewportHeight, ViewportHeight * Half)).IsEqualTo(0).Within(Tolerance);
    }
}
