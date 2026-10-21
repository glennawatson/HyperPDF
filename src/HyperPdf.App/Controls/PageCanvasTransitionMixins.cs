// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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
            canvas.Transition = TransitionRun.Start(step, Stopwatch.GetTimestamp());

        /// <summary>Moves the playing transition one frame on, and ends it once its time is up.</summary>
        /// <param name="run">The run.</param>
        internal void StepTransition(TransitionRun run)
        {
            if (run.Progress >= 1)
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

            var (x, y) = TransitionTiming.GetSlideOffset(run.Step.Direction, run.Progress, viewport.Width, viewport.Height);
            return new(x, y);
        }

        /// <summary>Draws the curtain that has not yet been drawn back, for an effect that reveals the page.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="viewport">The area the page fills.</param>
        internal void DrawCurtain(DrawingContext context, in Rect viewport)
        {
            if (canvas.CurtainBrush is not { } brush)
            {
                return;
            }

            Span<LayoutRect> rectangles = stackalloc LayoutRect[TransitionCurtain.MaxRectangles];
            var count = TransitionCurtain.GetRectangles(canvas.Transition, new(viewport.X, viewport.Y, viewport.Width, viewport.Height), rectangles, out var opacity);
            if (count == 0)
            {
                return;
            }

            using (context.PushOpacity(opacity))
            {
                foreach (var rectangle in rectangles[..count])
                {
                    context.FillRectangle(brush, new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
                }
            }
        }
    }

    /// <summary>Gets the frames of a run, or none when it is not playing.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="run">The run.</param>
    /// <returns>The frames.</returns>
    private static IObservable<TransitionRun> Frames(PageCanvas canvas, in TransitionRun run) =>
        run.IsRunning && TopLevel.GetTopLevel(canvas) is { } top
            ? top.AnimationFrames.SelectWith(run, static (running, _) => running)
            : Signal.Never<TransitionRun>();
}
