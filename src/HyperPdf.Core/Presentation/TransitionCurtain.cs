// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Presentation;

/// <summary>Works out the part of the page a reveal effect has not yet uncovered, so any toolkit can fill it with the canvas colour.</summary>
public static class TransitionCurtain
{
    /// <summary>The most rectangles a curtain is made of.</summary>
    public static readonly int MaxRectangles = 192;

    /// <summary>The columns of cells a dissolve or glitter is made of.</summary>
    private const int CellColumns = 16;

    /// <summary>The rows of cells a dissolve or glitter is made of.</summary>
    private const int CellRows = 12;

    /// <summary>The multiplier that scrambles a cell's number into its place in the order cells open.</summary>
    private const uint Scramble = 2_654_435_761U;

    /// <summary>The cells the first and last diagonals share a row or column with, taken off the diagonals' count.</summary>
    private const int DiagonalOverlap = 2;

    /// <summary>The bits the scrambled number is shifted down by, leaving its best mixed bits.</summary>
    private const int ScrambleShift = 8;

    /// <summary>The mask of the bits kept after the shift.</summary>
    private const uint ScrambleMask = 0xFFFFFF;

    /// <summary>The share of the time the cells open over; the rest is left so every cell has opened before the effect ends.</summary>
    private const double OpenSpread = 0.9;

    /// <summary>The share of a glitter cell's opening time that comes from where it is rather than from chance.</summary>
    private const double GlitterSweep = 0.5;

    /// <summary>The rectangles of the frame around a growing box.</summary>
    private const int BoxFrameSides = 4;

    /// <summary>The strips blinds are made of.</summary>
    private const int BlindStrips = 8;

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

        // Sliding effects cover nothing: they move the pages themselves, which the viewer does by drawing each page that far from its place.
        if (!run.IsRunning || MovesPages(run.Step.Effect) || rectangles.Length < MaxRectangles)
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

            case TransitionEffect.Blinds:
            {
                return Blinds(area, run.Step.Vertical, progress, rectangles);
            }

            case TransitionEffect.Dissolve:
            {
                return Cells(area, progress, 0, rectangles);
            }

            case TransitionEffect.Glitter:
            {
                return Cells(area, progress, GlitterSweep, rectangles);
            }

            default:
            {
                return Box(area, run.Step.Inward, progress, rectangles);
            }
        }
    }

    /// <summary>Determines whether an effect covers nothing, because it moves pages or draws nothing.</summary>
    /// <param name="effect">The effect.</param>
    /// <returns><see langword="true"/> when there is no curtain.</returns>
    private static bool MovesPages(TransitionEffect effect) =>
        effect is TransitionEffect.None or TransitionEffect.Slide or TransitionEffect.Push or TransitionEffect.Cover or TransitionEffect.Uncover;

    /// <summary>
    /// Gets the cells a dissolve or glitter has not opened. Each cell opens at a time set by its number, scrambled so the cells open in
    /// no pattern; a glitter mixes in how far along the page the cell is, so the opening sweeps across with sparkle ahead of it.
    /// </summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="progress">How far the effect has played.</param>
    /// <param name="sweep">The share of the opening time that comes from the cell's place, from 0 to 1.</param>
    /// <param name="rectangles">Receives the covered cells.</param>
    /// <returns>How many rectangles were written.</returns>
    private static int Cells(in LayoutRect area, double progress, double sweep, Span<LayoutRect> rectangles)
    {
        var width = area.Width / CellColumns;
        var height = area.Height / CellRows;
        var count = 0;
        for (var row = 0; row < CellRows; row++)
        {
            for (var column = 0; column < CellColumns; column++)
            {
                // Spread over the first nine tenths of the time so the last cell has opened before the effect ends.
                if (OpensAt(row, column, sweep) * OpenSpread < progress)
                {
                    continue;
                }

                rectangles[count] = new(area.X + (width * column), area.Y + (height * row), width, height);
                count++;
            }
        }

        return count;
    }

    /// <summary>Gets when a cell opens.</summary>
    /// <param name="row">The cell's row.</param>
    /// <param name="column">The cell's column.</param>
    /// <param name="sweep">The share of the time that comes from the cell's place.</param>
    /// <returns>The time as a share of the effect, from 0 to 1.</returns>
    private static double OpensAt(int row, int column, double sweep)
    {
        var scrambled = ((uint)((row * CellColumns) + column) * Scramble) >> ScrambleShift;
        var chance = (double)(scrambled & ScrambleMask) / (ScrambleMask + 1);
        var place = (column + row) / (double)(CellColumns + CellRows - DiagonalOverlap);
        return (chance * (1 - sweep)) + (place * sweep);
    }

    /// <summary>Gets what blinds have not reached: each strip opens from its start, so the rest of it stays covered.</summary>
    /// <param name="area">The area the page fills.</param>
    /// <param name="vertical">Whether the strips are columns rather than rows.</param>
    /// <param name="progress">How far the blinds have opened.</param>
    /// <param name="rectangles">Receives the covered rectangles.</param>
    /// <returns>How many rectangles were written.</returns>
    private static int Blinds(in LayoutRect area, bool vertical, double progress, Span<LayoutRect> rectangles)
    {
        var rest = 1 - progress;
        for (var i = 0; i < BlindStrips; i++)
        {
            if (vertical)
            {
                var strip = area.Width / BlindStrips;
                rectangles[i] = new(area.X + (strip * (i + progress)), area.Y, strip * rest, area.Height);
            }
            else
            {
                var strip = area.Height / BlindStrips;
                rectangles[i] = new(area.X, area.Y + (strip * (i + progress)), area.Width, strip * rest);
            }
        }

        return BlindStrips;
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
        return BoxFrameSides;
    }
}
