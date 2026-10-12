// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Navigation;

namespace HyperPdf.Core.Tests.Navigation;

/// <summary>Tests for <see cref="AutoScroller"/>.</summary>
public sealed class AutoScrollerTests
{
    /// <summary>The comparison tolerance.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>One frame at 60 frames per second.</summary>
    private const double Frame = 1D / 60D;

    /// <summary>Verifies smooth scrolling moves a little each frame at the speed's rate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmoothScrollingMovesEachFrame()
    {
        var scroller = new AutoScroller();
        var speed = AutoScroller.DefaultSpeed;
        var first = scroller.Advance(Frame, speed, stepByLine: false);
        var second = scroller.Advance(Frame, speed, stepByLine: false);

        await Assert.That(first).IsEqualTo(AutoScroller.GetPixelsPerSecond(speed) * Frame).Within(Tolerance);
        await Assert.That(second).IsEqualTo(first).Within(Tolerance);
        await Assert.That(first).IsLessThan(AutoScroller.LineDistance);
    }

    /// <summary>Verifies reduced motion steps whole lines at a steady interval, keeping the same average speed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionStepsWholeLines()
    {
        const int frames = 600;
        var scroller = new AutoScroller();
        var speed = AutoScroller.DefaultSpeed;
        var total = 0D;
        var steps = 0;
        var partial = false;
        for (var i = 0; i < frames; i++)
        {
            var distance = scroller.Advance(Frame, speed, stepByLine: true);
            total += distance;
            steps += distance > 0 ? 1 : 0;
            partial |= distance % AutoScroller.LineDistance > Tolerance;
        }

        var expected = AutoScroller.GetPixelsPerSecond(speed) * Frame * frames;
        await Assert.That(partial).IsFalse();
        await Assert.That(total).IsLessThanOrEqualTo(expected + Tolerance);
        await Assert.That(total).IsGreaterThan(expected - AutoScroller.LineDistance);
        await Assert.That(steps).IsEqualTo((int)(total / AutoScroller.LineDistance));
    }

    /// <summary>Verifies speeds stay in range, a long pause never turns into a jump, and no time moves nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LimitsSpeedAndPauses()
    {
        const double pause = 5;
        const double longestFrame = 0.25;
        var scroller = new AutoScroller();

        await Assert.That(AutoScroller.ClampSpeed(0)).IsEqualTo(AutoScroller.MinSpeed);
        await Assert.That(AutoScroller.ClampSpeed(int.MaxValue)).IsEqualTo(AutoScroller.MaxSpeed);
        await Assert.That(AutoScroller.GetPixelsPerSecond(AutoScroller.MaxSpeed)).IsGreaterThan(AutoScroller.GetPixelsPerSecond(AutoScroller.MinSpeed));
        await Assert.That(scroller.Advance(0, AutoScroller.DefaultSpeed, stepByLine: false)).IsEqualTo(0);
        await Assert.That(scroller.Advance(double.NaN, AutoScroller.DefaultSpeed, stepByLine: false)).IsEqualTo(0);
        await Assert.That(scroller.Advance(pause, AutoScroller.DefaultSpeed, stepByLine: false))
            .IsEqualTo(AutoScroller.GetPixelsPerSecond(AutoScroller.DefaultSpeed) * longestFrame).Within(Tolerance);
    }

    /// <summary>Verifies resetting forgets the part of a line owed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetForgetsOwedDistance()
    {
        const double almostALine = 0.2;
        var scroller = new AutoScroller();
        var speed = AutoScroller.MaxSpeed;
        _ = scroller.Advance(almostALine, speed, stepByLine: true);
        scroller.Reset();
        var after = scroller.Advance(almostALine, speed, stepByLine: true);

        await Assert.That(AutoScroller.GetPixelsPerSecond(speed) * almostALine).IsLessThan(AutoScroller.LineDistance);
        await Assert.That(after).IsEqualTo(0);
    }
}
