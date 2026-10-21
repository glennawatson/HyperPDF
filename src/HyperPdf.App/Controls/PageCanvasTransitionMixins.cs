// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HyperPdf.Core.Presentation;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Plays the effect a page asks for as it appears in a presentation. The new page is drawn as normal and either slides in or is
/// uncovered from behind a curtain in the colour of the canvas, so the effect needs no picture of the old page.
/// </summary>
internal static class PageCanvasTransitionMixins
{
    /// <summary>Half, for the middle of a rectangle.</summary>
    private const double Half = 0.5;

    /// <summary>Adds the page transition frames and drawing to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the frames of the transition playing now, each carrying the run; none once it ends.</summary>
        internal IObservable<TransitionRun> TransitionFrames => canvas.WhenChanged(static c => c.Transition)
            .SelectWith(canvas, static (c, run) => Frames(c, run))
            .SwitchMap(static frames => frames);

        /// <summary>Gets a value indicating whether a transition is playing.</summary>
        internal bool IsTransitioning => canvas.Transition.IsRunning;

        /// <summary>Starts the effect of the page now shown, or ends the one playing when the page asks for none.</summary>
        /// <param name="step">The step for the page, or <see langword="null"/> when presenting ended.</param>
        internal void StartTransition(PresentationStep? step) =>
            canvas.Transition = step is { Effect: not TransitionEffect.None } playing ? new(true, playing, Stopwatch.GetTimestamp()) : default;

        /// <summary>Moves the playing transition one frame on, and ends it once its time is up.</summary>
        /// <param name="run">The run.</param>
        internal void StepTransition(TransitionRun run)
        {
            if (Progress(run) >= 1)
            {
                canvas.Transition = default;
            }

            canvas.InvalidateVisual();
        }

        /// <summary>Gets how far the playing page is from its place, for an effect that slides the page in.</summary>
        /// <param name="viewport">The area the page fills.</param>
        /// <returns>The offset; zero when the page does not slide.</returns>
        internal Vector GetSlideOffset(in Rect viewport)
        {
            var run = canvas.Transition;
            if (!run.IsRunning || run.Step.Effect != TransitionEffect.Slide)
            {
                return default;
            }

            var (x, y) = TransitionTiming.GetSlideOffset(run.Step.Direction, Progress(run), viewport.Width, viewport.Height);
            return new(x, y);
        }

        /// <summary>Draws the curtain that has not yet been drawn back, for an effect that reveals the page.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="viewport">The area the page fills.</param>
        internal void DrawCurtain(DrawingContext context, in Rect viewport)
        {
            var run = canvas.Transition;
            if (!run.IsRunning || run.Step.Effect is TransitionEffect.None or TransitionEffect.Slide || canvas.CurtainBrush is not { } brush)
            {
                return;
            }

            var progress = Progress(run);
            if (run.Step.Effect == TransitionEffect.Fade)
            {
                using (context.PushOpacity(1 - progress))
                {
                    context.FillRectangle(brush, viewport);
                }

                return;
            }

            DrawRevealCurtain(context, brush, viewport, run.Step, progress);
        }
    }

    /// <summary>Gets how far a transition has played, from 0 to 1, eased so it starts and ends gently.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The progress.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Progress(in TransitionRun run) => TransitionTiming.GetProgress(run.Step.DurationSeconds, run.Started);

    /// <summary>Reduces a direction in degrees to the nearest of the four straight ones: 0 is left to right, then anti-clockwise.</summary>
    /// <param name="degrees">The direction in degrees.</param>
    /// <returns>0 for left to right, 1 for bottom to top, 2 for right to left, 3 for top to bottom.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Quadrant(int degrees) => (int)TransitionTiming.GetDirection(degrees);

    /// <summary>Gets the frames of a run, or none when it is not playing.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="run">The run.</param>
    /// <returns>The frames.</returns>
    private static IObservable<TransitionRun> Frames(PageCanvas canvas, in TransitionRun run) =>
        run.IsRunning && TopLevel.GetTopLevel(canvas) is { } top
            ? top.AnimationFrames.SelectWith(run, static (running, _) => running)
            : Signal.Never<TransitionRun>();

    /// <summary>Draws the part of the page not yet revealed by a wipe, split or box.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="brush">The canvas colour.</param>
    /// <param name="area">The area the page fills.</param>
    /// <param name="step">The step.</param>
    /// <param name="progress">How far the effect has played.</param>
    private static void DrawRevealCurtain(DrawingContext context, IBrush brush, in Rect area, in PresentationStep step, double progress)
    {
        switch (step.Effect)
        {
            case TransitionEffect.Wipe:
            {
                context.FillRectangle(brush, WipeCurtain(area, TransitionTiming.GetDirection(step.Direction), progress));
                break;
            }

            case TransitionEffect.Split:
            {
                FillSplit(context, brush, area, step, progress);
                break;
            }

            default:
            {
                FillBox(context, brush, area, step.Inward, progress);
                break;
            }
        }
    }

    /// <summary>Gets the part of the page a wipe has not reached.</summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="direction">The direction of the wipe.</param>
    /// <param name="progress">How far the wipe has gone.</param>
    /// <returns>The covered rectangle.</returns>
    private static Rect WipeCurtain(in Rect area, TransitionDirection direction, double progress) => direction switch
    {
        TransitionDirection.LeftToRight => new(area.Left + (area.Width * progress), area.Top, area.Width * (1 - progress), area.Height),
        TransitionDirection.BottomToTop => new(area.Left, area.Top, area.Width, area.Height * (1 - progress)),
        TransitionDirection.RightToLeft => new(area.Left, area.Top, area.Width * (1 - progress), area.Height),
        _ => new(area.Left, area.Top + (area.Height * progress), area.Width, area.Height * (1 - progress)),
    };

    /// <summary>
    /// Draws what a split has not reached. Lines moving in from the edges leave the middle covered; lines opening out from the
    /// middle leave the two sides covered.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="brush">The canvas colour.</param>
    /// <param name="area">The area the page fills.</param>
    /// <param name="step">The step.</param>
    /// <param name="progress">How far the split has gone.</param>
    private static void FillSplit(DrawingContext context, IBrush brush, in Rect area, in PresentationStep step, double progress)
    {
        var rest = 1 - progress;
        if (step.Inward)
        {
            context.FillRectangle(
                brush,
                step.Vertical
                    ? new(area.Left + (area.Width * progress * Half), area.Top, area.Width * rest, area.Height)
                    : new Rect(area.Left, area.Top + (area.Height * progress * Half), area.Width, area.Height * rest));
            return;
        }

        var side = rest * Half;
        if (step.Vertical)
        {
            context.FillRectangle(brush, new(area.Left, area.Top, area.Width * side, area.Height));
            context.FillRectangle(brush, new(area.Right - (area.Width * side), area.Top, area.Width * side, area.Height));
            return;
        }

        context.FillRectangle(brush, new(area.Left, area.Top, area.Width, area.Height * side));
        context.FillRectangle(brush, new(area.Left, area.Bottom - (area.Height * side), area.Width, area.Height * side));
    }

    /// <summary>Draws what a box has not reached: the frame around a growing box, or the box left as the revealed frame shrinks in.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="brush">The canvas colour.</param>
    /// <param name="area">The area the page fills.</param>
    /// <param name="inward">Whether the box sweeps in from the edges.</param>
    /// <param name="progress">How far the box has gone.</param>
    private static void FillBox(DrawingContext context, IBrush brush, in Rect area, bool inward, double progress)
    {
        var rest = 1 - progress;
        if (inward)
        {
            context.FillRectangle(brush, new(area.Left + (area.Width * progress * Half), area.Top + (area.Height * progress * Half), area.Width * rest, area.Height * rest));
            return;
        }

        var left = area.Left + (area.Width * rest * Half);
        var top = area.Top + (area.Height * rest * Half);
        var right = area.Right - (area.Width * rest * Half);
        var bottom = area.Bottom - (area.Height * rest * Half);
        context.FillRectangle(brush, new(area.Left, area.Top, area.Width, top - area.Top));
        context.FillRectangle(brush, new(area.Left, bottom, area.Width, area.Bottom - bottom));
        context.FillRectangle(brush, new(area.Left, top, left - area.Left, bottom - top));
        context.FillRectangle(brush, new(right, top, area.Right - right, bottom - top));
    }

    /// <summary>A transition playing on the canvas.</summary>
    /// <param name="IsRunning">Whether it is still playing.</param>
    /// <param name="Step">What is played.</param>
    /// <param name="Started">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    internal readonly record struct TransitionRun(bool IsRunning, PresentationStep Step, long Started);
}
