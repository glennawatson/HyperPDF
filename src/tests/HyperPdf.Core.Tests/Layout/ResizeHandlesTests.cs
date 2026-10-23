// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks finding the corner resize handle under a pointer.</summary>
public sealed class ResizeHandlesTests
{
    /// <summary>The rectangle's left.</summary>
    private const double Left = 100;

    /// <summary>The rectangle's top.</summary>
    private const double Top = 200;

    /// <summary>The rectangle's width.</summary>
    private const double Width = 80;

    /// <summary>The rectangle's height.</summary>
    private const double Height = 60;

    /// <summary>How close to a corner counts as on its handle.</summary>
    private const double Reach = 6;

    /// <summary>How far from the exact corner the pointer is, which is within reach.</summary>
    private const double Near = 2;

    /// <summary>The share of the way to the middle.</summary>
    private const double Half = 0.5;

    /// <summary>A pointer on a corner grabs that handle and anchors the opposite corner.</summary>
    /// <param name="x">The pointer x.</param>
    /// <param name="y">The pointer y.</param>
    /// <param name="oppositeX">The expected anchor x.</param>
    /// <param name="oppositeY">The expected anchor y.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Left, Top, Left + Width, Top + Height)]
    [Arguments(Left + Width, Top, Left, Top + Height)]
    [Arguments(Left + Width, Top + Height, Left, Top)]
    [Arguments(Left, Top + Height, Left + Width, Top)]
    public async Task CornerGrabsItsHandle(double x, double y, double oppositeX, double oppositeY)
    {
        var grabbed = ResizeHandles.TryGrab(Area(), x + Near, y - Near, Reach, out var opposite);

        await Assert.That(grabbed).IsTrue();
        await Assert.That(opposite.X).IsEqualTo(oppositeX);
        await Assert.That(opposite.Y).IsEqualTo(oppositeY);
    }

    /// <summary>A pointer in the middle or just beyond the reach grabs nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MiddleAndFarPointsGrabNothing()
    {
        var middle = ResizeHandles.TryGrab(Area(), Left + (Width * Half), Top + (Height * Half), Reach, out _);
        var far = ResizeHandles.TryGrab(Area(), Left - Reach - 1, Top, Reach, out _);

        await Assert.That(middle).IsFalse();
        await Assert.That(far).IsFalse();
    }

    /// <summary>Makes the rectangle the tests grab handles of.</summary>
    /// <returns>The rectangle.</returns>
    private static LayoutRect Area() => new(Left, Top, Width, Height);
}
