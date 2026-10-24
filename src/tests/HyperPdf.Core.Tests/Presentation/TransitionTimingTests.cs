// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Presentation;

namespace HyperPdf.Core.Tests.Presentation;

/// <summary>Checks the timing and direction of page transitions.</summary>
public sealed class TransitionTimingTests
{
    /// <summary>How long the test transitions take.</summary>
    private const double Seconds = 2;

    /// <summary>A time well after a transition ends.</summary>
    private const double Overrun = 6;

    /// <summary>The width of the area the slide tests fill.</summary>
    private const double Width = 800;

    /// <summary>The height of the area the slide tests fill.</summary>
    private const double Height = 600;

    /// <summary>The progress at the halfway point of the smooth step curve.</summary>
    private const double HalfProgress = 0.5;

    /// <summary>Left to right.</summary>
    private const int East = 0;

    /// <summary>Bottom to top.</summary>
    private const int North = 90;

    /// <summary>Right to left.</summary>
    private const int West = 180;

    /// <summary>Top to bottom.</summary>
    private const int South = 270;

    /// <summary>Progress starts at nothing and ends at done, and stays done.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressRunsFromNothingToDone()
    {
        await Assert.That(TransitionTiming.GetProgress(Seconds, 0.0)).IsEqualTo(0);
        await Assert.That(TransitionTiming.GetProgress(Seconds, Seconds)).IsEqualTo(1);
        await Assert.That(TransitionTiming.GetProgress(Seconds, Overrun)).IsEqualTo(1);
    }

    /// <summary>The smooth step is at the middle halfway through.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressIsHalfAtTheMiddle() =>
        await Assert.That(TransitionTiming.GetProgress(Seconds, Seconds * HalfProgress)).IsEqualTo(HalfProgress);

    /// <summary>A zero duration finishes at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroDurationIsDone() =>
        await Assert.That(TransitionTiming.GetProgress(0, 1.0)).IsEqualTo(1);

    /// <summary>A direction rounds to the nearest straight one, and wraps around.</summary>
    /// <param name="degrees">The direction in degrees.</param>
    /// <param name="expected">The straight direction.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, TransitionDirection.LeftToRight)]
    [Arguments(44, TransitionDirection.LeftToRight)]
    [Arguments(46, TransitionDirection.BottomToTop)]
    [Arguments(North, TransitionDirection.BottomToTop)]
    [Arguments(West, TransitionDirection.RightToLeft)]
    [Arguments(South, TransitionDirection.TopToBottom)]
    [Arguments(315, TransitionDirection.LeftToRight)]
    [Arguments(-90, TransitionDirection.TopToBottom)]
    [Arguments(450, TransitionDirection.BottomToTop)]
    public async Task DirectionRoundsToAStraightOne(int degrees, TransitionDirection expected) =>
        await Assert.That(TransitionTiming.GetDirection(degrees)).IsEqualTo(expected);

    /// <summary>A slide starts a whole area away from its place, in the direction it moves from.</summary>
    /// <param name="degrees">The direction of the slide.</param>
    /// <param name="x">The expected horizontal distance.</param>
    /// <param name="y">The expected vertical distance.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(East, -Width, 0.0)]
    [Arguments(North, 0.0, Height)]
    [Arguments(West, Width, 0.0)]
    [Arguments(South, 0.0, -Height)]
    public async Task SlideStartsOneAreaAway(int degrees, double x, double y)
    {
        var start = TransitionTiming.GetSlideOffset(degrees, 0, Width, Height);

        await Assert.That(start.X).IsEqualTo(x);
        await Assert.That(start.Y).IsEqualTo(y);
    }

    /// <summary>A finished slide has no distance left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishedSlideIsInPlace()
    {
        var arrived = TransitionTiming.GetSlideOffset(East, 1, Width, Height);

        await Assert.That(Math.Abs(arrived.X)).IsEqualTo(0);
        await Assert.That(Math.Abs(arrived.Y)).IsEqualTo(0);
    }
}
