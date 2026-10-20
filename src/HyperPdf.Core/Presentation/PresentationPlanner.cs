// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Presentation;

/// <summary>
/// Decides what happens when a page appears in a presentation. A page's transition and advance time are only followed when the
/// reader allows each, and reduced motion stops both, so nothing moves or changes by itself unless the reader asked for it.
/// </summary>
public static class PresentationPlanner
{
    /// <summary>The shortest time before a page advances by itself, so a page cannot flash past.</summary>
    private const double MinAdvanceSeconds = 0.25;

    /// <summary>The longest time before a page advances by itself.</summary>
    private const double MaxAdvanceSeconds = 86_400;

    /// <summary>The longest an effect may last.</summary>
    private const double MaxDurationSeconds = 10;

    /// <summary>Plans how a page appears.</summary>
    /// <param name="sequence">A number that changes with every page shown.</param>
    /// <param name="transition">What the page asks for, or <see langword="null"/> when it asks for nothing.</param>
    /// <param name="options">What the reader allows.</param>
    /// <returns>The step.</returns>
    public static PresentationStep Plan(int sequence, PageTransition? transition, PresentationOptions options)
    {
        if (transition is null)
        {
            return new(sequence, TransitionStyle.Replace, TransitionEffect.None, 0, false, false, 0, false, null);
        }

        var advance = options.FollowTimings && !options.ReduceMotion ? ClampAdvance(transition.AdvanceAfterSeconds) : null;
        var effect = options.FollowTransitions && !options.ReduceMotion && transition.DurationSeconds > 0 ? EffectOf(transition.Style) : TransitionEffect.None;
        return new(
            sequence,
            transition.Style,
            effect,
            Math.Min(transition.DurationSeconds, MaxDurationSeconds),
            transition.Vertical,
            transition.Inward,
            Math.Max(transition.Direction, 0),
            effect != TransitionEffect.None && IsApproximate(transition.Style),
            advance);
    }

    /// <summary>Gets how a style is drawn.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The effect.</returns>
    public static TransitionEffect EffectOf(TransitionStyle style) => style switch
    {
        TransitionStyle.Fade or TransitionStyle.Dissolve or TransitionStyle.Glitter or TransitionStyle.Fly or TransitionStyle.Blinds => TransitionEffect.Fade,
        TransitionStyle.Wipe or TransitionStyle.Uncover => TransitionEffect.Wipe,
        TransitionStyle.Split => TransitionEffect.Split,
        TransitionStyle.Box => TransitionEffect.Box,
        TransitionStyle.Push or TransitionStyle.Cover => TransitionEffect.Slide,
        _ => TransitionEffect.None,
    };

    /// <summary>Determines whether a style is drawn as something close to it rather than as asked.</summary>
    /// <param name="style">The style.</param>
    /// <returns><see langword="true"/> when it is only approximated.</returns>
    public static bool IsApproximate(TransitionStyle style) =>
        style is TransitionStyle.Blinds or TransitionStyle.Dissolve or TransitionStyle.Glitter or TransitionStyle.Fly or TransitionStyle.Uncover;

    /// <summary>Keeps an advance time within what is sensible, and drops one that is not a positive number.</summary>
    /// <param name="seconds">The time the page asks for.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    private static double? ClampAdvance(double? seconds) =>
        seconds is { } value && value > 0 && double.IsFinite(value) ? Math.Clamp(value, MinAdvanceSeconds, MaxAdvanceSeconds) : null;
}
