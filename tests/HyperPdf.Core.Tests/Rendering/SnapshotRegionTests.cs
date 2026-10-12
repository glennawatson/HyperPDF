// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Tests for <see cref="SnapshotRegion"/>.</summary>
public sealed class SnapshotRegionTests
{
    /// <summary>The comparison tolerance for scales.</summary>
    private const double Tolerance = 1e-4;

    /// <summary>The points in an inch.</summary>
    private const double PointsPerInch = 72;

    /// <summary>Verifies a page shown at 100% on a plain screen is rendered at 200 dots per inch.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainScreenRendersAtTwoHundredDpi()
    {
        const double left = 30;
        const double top = 60;
        const double width = 200;
        const double height = 100;
        var layoutScale = ZoomCalculator.PixelsPerPoint;
        var region = SnapshotRegion.Create(left, top, width, height, layoutScale, 1);
        var expected = SnapshotRegion.MinDotsPerInch / PointsPerInch;
        var ratio = expected / layoutScale;

        await Assert.That((double)region.Scale).IsEqualTo(expected).Within(Tolerance);
        await Assert.That(region.OffsetX).IsEqualTo((int)Math.Floor(left * ratio));
        await Assert.That(region.OffsetY).IsEqualTo((int)Math.Floor(top * ratio));
        await Assert.That(region.Width).IsEqualTo((int)Math.Floor(width * ratio));
        await Assert.That(region.Height).IsEqualTo((int)Math.Floor(height * ratio));
        await Assert.That(region.IsEmpty).IsFalse();
    }

    /// <summary>Verifies a sharp screen or a big zoom renders at twice the screen's resolution.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharpScreenRendersAtTwiceItsResolution()
    {
        const double zoom = 2;
        const double scaling = 2;
        const double side = 100;
        var layoutScale = zoom * ZoomCalculator.PixelsPerPoint;
        var region = SnapshotRegion.Create(0, 0, side, side, layoutScale, scaling);

        await Assert.That((double)region.Scale).IsEqualTo(layoutScale * scaling * SnapshotRegion.ScreenMultiple).Within(Tolerance);
        await Assert.That(region.Width).IsEqualTo((int)(side * scaling * SnapshotRegion.ScreenMultiple));
    }

    /// <summary>Verifies a huge area is scaled down to the pixel limit, and a tiny one is empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LimitsSizeAndSkipsTinyAreas()
    {
        const double side = 5000;
        const double tiny = 2;
        var big = SnapshotRegion.Create(0, 0, side, side, ZoomCalculator.PixelsPerPoint, 1);
        var empty = SnapshotRegion.Create(0, 0, tiny, side, ZoomCalculator.PixelsPerPoint, 1);

        await Assert.That((double)big.Width * big.Height).IsLessThanOrEqualTo(SnapshotRegion.MaxPixels);
        await Assert.That(big.Width).IsGreaterThan(0);
        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(default(SnapshotRegion).IsEmpty).IsTrue();
    }
}
