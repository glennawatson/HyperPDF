// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks the mapping between unrotated page space and the canvas.</summary>
public sealed class PageTransformTests
{
    /// <summary>The page width in points.</summary>
    private const float Width = 200F;

    /// <summary>The page height in points.</summary>
    private const float Height = 300F;

    /// <summary>Canvas units per point.</summary>
    private const double Scale = 2;

    /// <summary>The page's left on the canvas.</summary>
    private const double Left = 50;

    /// <summary>The page's top on the canvas.</summary>
    private const double Top = 70;

    /// <summary>A point in the page the tests map.</summary>
    private const float PointX = 30F;

    /// <summary>A point in the page the tests map.</summary>
    private const float PointY = 40F;

    /// <summary>The width of the rectangle the rectangle test maps.</summary>
    private const float RectWidth = 10F;

    /// <summary>The height of the rectangle the rectangle test maps.</summary>
    private const float RectHeight = 20F;

    /// <summary>Without rotation a point lands at the page origin plus its scaled offset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnrotatedPointIsScaledFromTheOrigin()
    {
        var transform = Create(PageRotation.None);

        var mapped = transform.MapToCanvas(new PagePoint(PointX, PointY));

        await Assert.That(mapped.X).IsEqualTo(Left + (PointX * Scale));
        await Assert.That(mapped.Y).IsEqualTo(Top + (PointY * Scale));
    }

    /// <summary>A quarter turn swaps the axes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QuarterTurnSwapsTheAxes()
    {
        var transform = Create(PageRotation.Rotate90);

        var mapped = transform.MapToCanvas(new PagePoint(PointX, PointY));

        await Assert.That(mapped.X).IsEqualTo(Left + ((Height - PointY) * Scale));
        await Assert.That(mapped.Y).IsEqualTo(Top + (PointX * Scale));
    }

    /// <summary>Mapping to the canvas and back returns the same page point for every rotation.</summary>
    /// <param name="rotation">The rotation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageRotation.None)]
    [Arguments(PageRotation.Rotate90)]
    [Arguments(PageRotation.Rotate180)]
    [Arguments(PageRotation.Rotate270)]
    public async Task MappingRoundTrips(PageRotation rotation)
    {
        var transform = Create(rotation);

        var canvas = transform.MapToCanvas(new PagePoint(PointX, PointY));
        var back = transform.MapToPage(canvas.X, canvas.Y);

        await Assert.That(back.X).IsEqualTo(PointX);
        await Assert.That(back.Y).IsEqualTo(PointY);
    }

    /// <summary>A rotated rectangle comes out with a positive width and height.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RectangleIsNormalised()
    {
        var transform = Create(PageRotation.Rotate180);

        var mapped = transform.MapToCanvas(new PageRect(PointX, PointY, RectWidth, RectHeight));

        await Assert.That(mapped.Width).IsEqualTo(RectWidth * Scale);
        await Assert.That(mapped.Height).IsEqualTo(RectHeight * Scale);
    }

    /// <summary>Creates a transform for the test page.</summary>
    /// <param name="rotation">The rotation.</param>
    /// <returns>The transform.</returns>
    private static PageTransform Create(PageRotation rotation) =>
        new(new LayoutRect(Left, Top, Width * Scale, Height * Scale), new PageSize(Width, Height), rotation, Scale);
}
