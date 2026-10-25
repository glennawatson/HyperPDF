// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Presentation;

namespace HyperPdf.Core.Tests.Presentation;

/// <summary>Checks that a page's transition and advance time are followed only as far as the reader allows.</summary>
public sealed class PresentationPlannerTests
{
    /// <summary>The seconds an effect lasts in the test.</summary>
    private const double EffectSeconds = 1.5;

    /// <summary>The seconds a page stays in the test.</summary>
    private const double StaySeconds = 4;

    /// <summary>The number given to the step.</summary>
    private const int Sequence = 7;

    /// <summary>Allows both the effect and the timing.</summary>
    private static readonly PresentationOptions Everything = new(true, true, false);

    /// <summary>Allows neither, as the settings are by default.</summary>
    private static readonly PresentationOptions Nothing = new(false, false, false);

    /// <summary>Allows both but reduces motion.</summary>
    private static readonly PresentationOptions Reduced = new(true, true, true);

    /// <summary>With both allowed, a wipe plays as a wipe and the page moves on after its time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsBothWhenAllowed()
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Wipe), Everything);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.Wipe);
        await Assert.That(step.DurationSeconds).IsEqualTo(EffectSeconds);
        await Assert.That(step.AdvanceAfterSeconds).IsEqualTo(StaySeconds);
        await Assert.That(step.Sequence).IsEqualTo(Sequence);
        await Assert.That(step.IsApproximate).IsFalse();
    }

    /// <summary>With the settings off, nothing moves and no page changes by itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsNothingByDefault()
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Wipe), Nothing);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.None);
        await Assert.That(step.AdvanceAfterSeconds).IsNull();
    }

    /// <summary>Reduced motion stops the effect and the timing even when both are ticked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionStopsBoth()
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Fade), Reduced);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.None);
        await Assert.That(step.AdvanceAfterSeconds).IsNull();
    }

    /// <summary>The effect and the timing are allowed separately.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AllowsEachOnItsOwn()
    {
        var onlyEffect = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Box), new(true, false, false));
        var onlyTiming = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Box), new(false, true, false));

        await Assert.That(onlyEffect.Effect).IsEqualTo(TransitionEffect.Box);
        await Assert.That(onlyEffect.AdvanceAfterSeconds).IsNull();
        await Assert.That(onlyTiming.Effect).IsEqualTo(TransitionEffect.None);
        await Assert.That(onlyTiming.AdvanceAfterSeconds).IsEqualTo(StaySeconds);
    }

    /// <summary>A page that asks for nothing plans nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APageThatAsksForNothingPlansNothing()
    {
        var step = PresentationPlanner.Plan(Sequence, null, Everything);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.None);
        await Assert.That(step.AdvanceAfterSeconds).IsNull();
    }

    /// <summary>Styles drawn as something close are marked, so the reader can be told.</summary>
    /// <param name="style">The style.</param>
    /// <param name="approximate">Whether it is only approximated.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(TransitionStyle.Wipe, false)]
    [Arguments(TransitionStyle.Split, false)]
    [Arguments(TransitionStyle.Box, false)]
    [Arguments(TransitionStyle.Fade, false)]
    [Arguments(TransitionStyle.Push, false)]
    [Arguments(TransitionStyle.Cover, false)]
    [Arguments(TransitionStyle.Blinds, true)]
    [Arguments(TransitionStyle.Dissolve, true)]
    [Arguments(TransitionStyle.Glitter, true)]
    [Arguments(TransitionStyle.Fly, true)]
    [Arguments(TransitionStyle.Uncover, true)]
    public async Task MarksApproximatedStyles(TransitionStyle style, bool approximate)
    {
        var step = PresentationPlanner.Plan(Sequence, Page(style), Everything);

        await Assert.That(step.IsApproximate).IsEqualTo(approximate);
        await Assert.That(step.Effect).IsNotEqualTo(TransitionEffect.None);
    }

    /// <summary>A plain replacement draws nothing and is not an approximation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplaceDrawsNothing()
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Replace), Everything);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.None);
        await Assert.That(step.IsApproximate).IsFalse();
    }

    /// <summary>An advance time that is not a positive number is ignored, and a tiny or huge one is kept sensible.</summary>
    /// <param name="requested">The time the page asks for.</param>
    /// <param name="expected">The time used, or <see langword="null"/> for none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0D, null)]
    [Arguments(-3D, null)]
    [Arguments(double.NaN, null)]
    [Arguments(double.PositiveInfinity, null)]
    [Arguments(0.01D, 0.25D)]
    [Arguments(1_000_000D, 86_400D)]
    public async Task KeepsAdvanceTimesSensible(double requested, double? expected)
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Fade) with { AdvanceAfterSeconds = requested }, Everything);

        await Assert.That(step.AdvanceAfterSeconds).IsEqualTo(expected);
    }

    /// <summary>A transition that lasts no time draws nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnEffectOfNoLengthDrawsNothing()
    {
        var step = PresentationPlanner.Plan(Sequence, Page(TransitionStyle.Fade) with { DurationSeconds = 0 }, Everything);

        await Assert.That(step.Effect).IsEqualTo(TransitionEffect.None);
    }

    /// <summary>Makes a page that asks for a style, lasting a while and staying a while.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The transition.</returns>
    private static PageTransition Page(TransitionStyle style) => new(style, EffectSeconds, false, true, 0, StaySeconds);
}
