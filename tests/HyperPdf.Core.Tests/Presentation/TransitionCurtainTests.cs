// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Presentation;

namespace HyperPdf.Core.Tests.Presentation;

/// <summary>Checks the part of the page a reveal effect leaves covered.</summary>
public sealed class TransitionCurtainTests
{
    /// <summary>The width of the area the page fills.</summary>
    private const double Width = 400;

    /// <summary>The height of the area the page fills.</summary>
    private const double Height = 200;

    /// <summary>A run that started so long ago that it has finished.</summary>
    private const long LongAgoSeconds = 1000;

    /// <summary>Most of the width, for a wipe that has only just begun.</summary>
    private const double NearlyAll = 0.9;

    /// <summary>Hardly any opacity, for a fade that is over.</summary>
    private const double NearlyNone = 0.1;

    /// <summary>The rectangles a split opening out from the middle leaves.</summary>
    private const int TwoSides = 2;

    /// <summary>A run that has not started playing yet.</summary>
    private const double NotStarted = 0;

    /// <summary>The effect just started shows the whole area covered by a wipe, and one that is over shows none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WipeShrinksFromFullToNothing()
    {
        var started = Get(Run(TransitionEffect.Wipe, 0, false, false, 0));
        var over = Get(Run(TransitionEffect.Wipe, 0, false, false, -LongAgoSeconds));

        await Assert.That(started.Rectangles.Length).IsEqualTo(1);
        await Assert.That(started.Rectangles[0].Width).IsGreaterThan(Width * NearlyAll);
        await Assert.That(over.Rectangles.Length).IsEqualTo(1);
        await Assert.That(over.Rectangles[0].Width).IsLessThan(1);
    }

    /// <summary>A fade covers the whole area with an opacity that falls as it plays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FadeCoversTheAreaWithFallingOpacity()
    {
        var curtain = Get(Run(TransitionEffect.Fade, 0, false, false, -LongAgoSeconds));

        await Assert.That(curtain.Rectangles.Length).IsEqualTo(1);
        await Assert.That(curtain.Rectangles[0]).IsEqualTo(Area());
        await Assert.That(curtain.Opacity).IsLessThan(NearlyNone);
    }

    /// <summary>A split opening out leaves two sides covered, and one closing in leaves the middle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitLeavesTwoSidesOrTheMiddle()
    {
        var outward = Get(Run(TransitionEffect.Split, 0, true, false, 0));
        var inward = Get(Run(TransitionEffect.Split, 0, true, true, 0));

        await Assert.That(outward.Rectangles.Length).IsEqualTo(TwoSides);
        await Assert.That(inward.Rectangles.Length).IsEqualTo(1);
    }

    /// <summary>A box growing out leaves the frame around it, which is four rectangles, and one shrinking in leaves one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoxLeavesAFrameOrABox()
    {
        var outward = Get(Run(TransitionEffect.Box, 0, false, false, 0));
        var inward = Get(Run(TransitionEffect.Box, 0, false, true, 0));

        await Assert.That(outward.Rectangles.Length).IsEqualTo(TransitionCurtain.MaxRectangles);
        await Assert.That(inward.Rectangles.Length).IsEqualTo(1);
    }

    /// <summary>A slide, no effect, or a run that is not playing has no curtain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SomeRunsHaveNoCurtain()
    {
        var stopped = Run(TransitionEffect.Wipe, 0, false, false, 0) with { IsRunning = false };

        await Assert.That(Get(Run(TransitionEffect.Slide, 0, false, false, 0)).Rectangles.Length).IsEqualTo(0);
        await Assert.That(Get(Run(TransitionEffect.None, 0, false, false, 0)).Rectangles.Length).IsEqualTo(0);
        await Assert.That(Get(stopped).Rectangles.Length).IsEqualTo(0);
    }

    /// <summary>Too little room for the rectangles gives no curtain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TooLittleRoomGivesNoCurtain() =>
        await Assert.That(CountInto(new LayoutRect[1])).IsEqualTo(0);

    /// <summary>Starting a run for a page with no effect, or none at all, does not play.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoEffectStartsNoRun()
    {
        var none = TransitionRun.Start(Step(TransitionEffect.None, 0, false, false), Stopwatch.GetTimestamp());
        var absent = TransitionRun.Start(null, Stopwatch.GetTimestamp());
        var wipe = TransitionRun.Start(Step(TransitionEffect.Wipe, 0, false, false), Stopwatch.GetTimestamp());

        await Assert.That(none.IsRunning).IsFalse();
        await Assert.That(absent.IsRunning).IsFalse();
        await Assert.That(wipe.IsRunning).IsTrue();
        await Assert.That(wipe.Progress).IsGreaterThanOrEqualTo(NotStarted);
    }

    /// <summary>Makes the area the page fills.</summary>
    /// <returns>The area.</returns>
    private static LayoutRect Area() => new(0, 0, Width, Height);

    /// <summary>Asks for the curtain of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The covered rectangles and their opacity.</returns>
    private static Curtain Get(in TransitionRun run)
    {
        var buffer = new LayoutRect[TransitionCurtain.MaxRectangles];
        var count = TransitionCurtain.GetRectangles(run, Area(), buffer, out var opacity);
        return new(buffer[..count], opacity);
    }

    /// <summary>Asks for the curtain of a wipe into a buffer of a given size.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>How many rectangles were written.</returns>
    private static int CountInto(LayoutRect[] buffer) =>
        TransitionCurtain.GetRectangles(Run(TransitionEffect.Wipe, 0, false, false, 0), Area(), buffer, out _);

    /// <summary>Makes a run of an effect that started a number of seconds ago.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="direction">The direction in degrees.</param>
    /// <param name="vertical">Whether a split runs vertically.</param>
    /// <param name="inward">Whether the effect moves inwards.</param>
    /// <param name="startedSeconds">The seconds after now it started; negative for the past.</param>
    /// <returns>The run.</returns>
    private static TransitionRun Run(TransitionEffect effect, int direction, bool vertical, bool inward, long startedSeconds) =>
        new(true, Step(effect, direction, vertical, inward), Stopwatch.GetTimestamp() + (Stopwatch.Frequency * startedSeconds));

    /// <summary>Makes a step of an effect.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="direction">The direction in degrees.</param>
    /// <param name="vertical">Whether a split runs vertically.</param>
    /// <param name="inward">Whether the effect moves inwards.</param>
    /// <returns>The step.</returns>
    private static PresentationStep Step(TransitionEffect effect, int direction, bool vertical, bool inward) =>
        new(1, TransitionStyle.Wipe, effect, 1, vertical, inward, direction, false, null);

    /// <summary>The rectangles a curtain covers and how opaque they are.</summary>
    /// <param name="Rectangles">The covered rectangles.</param>
    /// <param name="Opacity">The opacity.</param>
    private readonly record struct Curtain(LayoutRect[] Rectangles, double Opacity);
}
