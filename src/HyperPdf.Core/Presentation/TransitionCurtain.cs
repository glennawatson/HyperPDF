// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Presentation;

/// <summary>Works out the part of the page a reveal effect has not yet uncovered, so any toolkit can fill it with the canvas colour.</summary>
public static class TransitionCurtain
{
    /// <summary>The most rectangles a curtain is made of.</summary>
    public static readonly int MaxRectangles = 4;

    /// <summary>Half, for the middle of a rectangle.</summary>
    private const double Half = 0.5;

    /// <summary>The rectangles a split leaves covered when it opens out from the middle.</summary>
    private const int SplitSides = 2;

    /// <summary>Gets the rectangles that still cover the page.</summary>
    /// <param name="run">The transition playing.</param>
    /// <param name="area">The area the page fills.</param>
    /// <param name="rectangles">Receives the covered rectangles; at least <see cref="MaxRectangles"/> long.</param>
    /// <param name="opacity">How opaque the covering is: below 1 for a fade, and 1 otherwise.</param>
    /// <returns>How many rectangles were written; none when the effect has no curtain.</returns>
    public static int GetRectangles(in TransitionRun run, in LayoutRect area, Span<LayoutRect> rectangles, out double opacity)
    {
        opacity = 1;
        if (!run.IsRunning || run.Step.Effect is TransitionEffect.None or TransitionEffect.Slide || rectangles.Length < MaxRectangles)
        {
            return 0;
        }

        var progress = run.Progress;
        switch (run.Step.Effect)
        {
            case TransitionEffect.Fade:
            {
                opacity = 1 - progress;
                rectangles[0] = area;
                return 1;
            }

            case TransitionEffect.Wipe:
            {
                rectangles[0] = Wipe(area, TransitionTiming.GetDirection(run.Step.Direction), progress);
                return 1;
            }

            case TransitionEffect.Split:
            {
                return Split(area, run.Step, progress, rectangles);
            }

            default:
            {
                return Box(area, run.Step.Inward, progress, rectangles);
            }
        }
    }

    /// <summary>Gets the part of the page a wipe has not reached.</summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="direction">The direction of the wipe.</param>
    /// <param name="progress">How far the wipe has gone.</param>
    /// <returns>The covered rectangle.</returns>
    private static LayoutRect Wipe(in LayoutRect area, TransitionDirection direction, double progress) => direction switch
    {
        TransitionDirection.LeftToRight => new(area.X + (area.Width * progress), area.Y, area.Width * (1 - progress), area.Height),
        TransitionDirection.BottomToTop => new(area.X, area.Y, area.Width, area.Height * (1 - progress)),
        TransitionDirection.RightToLeft => new(area.X, area.Y, area.Width * (1 - progress), area.Height),
        _ => new(area.X, area.Y + (area.Height * progress), area.Width, area.Height * (1 - progress)),
    };

    /// <summary>
    /// Gets what a split has not reached. Lines moving in from the edges leave the middle covered; lines opening out from the
    /// middle leave the two sides covered.
    /// </summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="step">The step.</param>
    /// <param name="progress">How far the split has gone.</param>
    /// <param name="rectangles">Receives the covered rectangles.</param>
    /// <returns>How many rectangles were written.</returns>
    private static int Split(in LayoutRect area, in PresentationStep step, double progress, Span<LayoutRect> rectangles)
    {
        var rest = 1 - progress;
        if (step.Inward)
        {
            rectangles[0] = step.Vertical
                ? new(area.X + (area.Width * progress * Half), area.Y, area.Width * rest, area.Height)
                : new(area.X, area.Y + (area.Height * progress * Half), area.Width, area.Height * rest);
            return 1;
        }

        var side = rest * Half;
        if (step.Vertical)
        {
            rectangles[0] = new(area.X, area.Y, area.Width * side, area.Height);
            rectangles[1] = new(area.Right - (area.Width * side), area.Y, area.Width * side, area.Height);
            return SplitSides;
        }

        rectangles[0] = new(area.X, area.Y, area.Width, area.Height * side);
        rectangles[1] = new(area.X, area.Bottom - (area.Height * side), area.Width, area.Height * side);
        return SplitSides;
    }

    /// <summary>Gets what a box has not reached: the frame around a growing box, or the box left as the revealed frame shrinks in.</summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="inward">Whether the box sweeps in from the edges.</param>
    /// <param name="progress">How far the box has gone.</param>
    /// <param name="rectangles">Receives the covered rectangles.</param>
    /// <returns>How many rectangles were written.</returns>
    private static int Box(in LayoutRect area, bool inward, double progress, Span<LayoutRect> rectangles)
    {
        var rest = 1 - progress;
        if (inward)
        {
            rectangles[0] = new(area.X + (area.Width * progress * Half), area.Y + (area.Height * progress * Half), area.Width * rest, area.Height * rest);
            return 1;
        }

        var left = area.X + (area.Width * rest * Half);
        var top = area.Y + (area.Height * rest * Half);
        var right = area.Right - (area.Width * rest * Half);
        var bottom = area.Bottom - (area.Height * rest * Half);
        rectangles[0] = new(area.X, area.Y, area.Width, top - area.Y);
        rectangles[1] = new(area.X, bottom, area.Width, area.Bottom - bottom);
        rectangles[2] = new(area.X, top, left - area.X, bottom - top);
        rectangles[3] = new(right, top, area.Right - right, bottom - top);
        return MaxRectangles;
    }
}
