// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks how layout rectangles overlap, share an area and grow.</summary>
public sealed class LayoutRectTests
{
    /// <summary>The side of the test rectangles.</summary>
    private const double Side = 100;

    /// <summary>How far the second rectangle overlaps the first.</summary>
    private const double Overlap = 40;

    /// <summary>How far the grown rectangle moves each edge.</summary>
    private const double Grow = 10;

    /// <summary>A distance that leaves a rectangle clear of the first.</summary>
    private const double Apart = 200;

    /// <summary>The ends of an edge that grows, one at each side.</summary>
    private const double Ends = 2;

    /// <summary>Rectangles that overlap intersect, and rectangles that only touch do not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlapIntersectsButTouchingDoesNot()
    {
        var rect = Square(0, 0);

        await Assert.That(rect.Intersects(Square(Side - Overlap, Side - Overlap))).IsTrue();
        await Assert.That(rect.Intersects(Square(Side, 0))).IsFalse();
        await Assert.That(rect.Intersects(Square(Apart, Apart))).IsFalse();
    }

    /// <summary>The shared area of two overlapping rectangles is the part both cover.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IntersectGivesTheSharedArea()
    {
        var shared = Square(0, 0).Intersect(Square(Side - Overlap, Side - Overlap));

        await Assert.That(shared).IsEqualTo(Box(Side - Overlap, Side - Overlap, Overlap, Overlap));
    }

    /// <summary>Rectangles that do not meet share an empty area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IntersectOfSeparateRectanglesIsEmpty() =>
        await Assert.That(Square(0, 0).Intersect(Square(Apart, 0))).IsEqualTo(default(LayoutRect));

    /// <summary>Growing moves each edge out by its own distance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InflateMovesEveryEdgeOut()
    {
        var grown = Square(Side, Side).Inflate(0, Grow);

        await Assert.That(grown).IsEqualTo(Box(Side, Side - Grow, Side, Side + (Grow * Ends)));
    }

    /// <summary>Makes a rectangle.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The rectangle.</returns>
    private static LayoutRect Box(double x, double y, double width, double height) => new(x, y, width, height);

    /// <summary>Makes a square of the test side.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <returns>The square.</returns>
    private static LayoutRect Square(double x, double y) => new(x, y, Side, Side);
}
