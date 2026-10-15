// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds the straight horizontal and vertical lines in the paths a page fills and strokes: a stroked segment, or a
/// filled rectangle thin enough to be a border. Layout analysis uses them to find table borders.
/// </summary>
internal static class RulingCollector
{
    /// <summary>The most points in a contour that is read as a filled rectangle.</summary>
    private const int RectanglePoints = 5;

    /// <summary>The most commands in a path that is searched; longer paths are drawings, not borders.</summary>
    private const int MaxPathCommands = 512;

    /// <summary>The most rulings kept for a page.</summary>
    private const int MaxRulings = 4096;

    /// <summary>The thickest a line or filled bar may be, in points, and still be a rule.</summary>
    private const float MaxThickness = 3F;

    /// <summary>The shortest a rule may be, in points.</summary>
    private const float MinLength = 6F;

    /// <summary>The thinnest a rule's box is made, so it has an area.</summary>
    private const float MinThickness = 0.1F;

    /// <summary>How far a point may stray from a line's axis, in points, and still be on it.</summary>
    private const float AxisTolerance = 0.05F;

    /// <summary>Half, for finding a rule's middle.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds the rules a filled path draws.</summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="ctm">The matrix from user space to viewer space.</param>
    /// <param name="rulings">Receives the rules.</param>
    internal static void AddFilled(PdfPath path, Matrix3x2 ctm, List<LayoutRuling> rulings)
    {
        var commands = path.Commands;
        if (commands.Length is 0 or > MaxPathCommands || rulings.Count >= MaxRulings)
        {
            return;
        }

        Span<Vector2> points = stackalloc Vector2[RectanglePoints];
        var count = 0;
        var simple = true;
        for (var i = 0; i <= commands.Length; i++)
        {
            var starts = i == commands.Length || commands[i].Kind == PdfPathCommandKind.MoveTo;
            if (starts)
            {
                if (count > 0)
                {
                    AddRectangle(points[..count], simple, rulings);
                }

                count = 0;
                simple = true;
            }

            if (i == commands.Length)
            {
                break;
            }

            AddPoint(commands[i], ctm, points, ref count, ref simple);
        }
    }

    /// <summary>Adds the rules a stroked path draws: each horizontal or vertical segment, with the line's width as its thickness.</summary>
    /// <param name="path">The path in user space.</param>
    /// <param name="ctm">The matrix from user space to viewer space.</param>
    /// <param name="lineWidth">The line width in user space.</param>
    /// <param name="rulings">Receives the rules.</param>
    internal static void AddStroked(PdfPath path, Matrix3x2 ctm, float lineWidth, List<LayoutRuling> rulings)
    {
        var commands = path.Commands;
        if (commands.Length is 0 or > MaxPathCommands)
        {
            return;
        }

        var thickness = Math.Max(lineWidth * MathF.Sqrt(MathF.Abs(ctm.GetDeterminant())), MinThickness);
        if (thickness > MaxThickness)
        {
            return;
        }

        var start = default(Vector2);
        var current = default(Vector2);
        foreach (var command in commands)
        {
            if (rulings.Count >= MaxRulings)
            {
                return;
            }

            switch (command.Kind)
            {
                case PdfPathCommandKind.MoveTo:
                {
                    current = Vector2.Transform(new(command.Point.X, command.Point.Y), ctm);
                    start = current;
                    break;
                }

                case PdfPathCommandKind.LineTo:
                {
                    var next = Vector2.Transform(new(command.Point.X, command.Point.Y), ctm);
                    AddSegment(current, next, thickness, rulings);
                    current = next;
                    break;
                }

                case PdfPathCommandKind.Close:
                {
                    AddSegment(current, start, thickness, rulings);
                    current = start;
                    break;
                }

                default:
                {
                    // A curve is no border: leave the point where it ends.
                    current = Vector2.Transform(new(command.Point.X, command.Point.Y), ctm);
                    break;
                }
            }
        }
    }

    /// <summary>Adds a command's point to the contour being read, noting when the contour is not made of straight lines.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ctm">The matrix from user space to viewer space.</param>
    /// <param name="points">The contour's points.</param>
    /// <param name="count">The points so far.</param>
    /// <param name="simple">Whether the contour is straight lines so far, and short enough.</param>
    private static void AddPoint(in PdfPathCommand command, Matrix3x2 ctm, Span<Vector2> points, ref int count, ref bool simple)
    {
        if (command.Kind is PdfPathCommandKind.Close)
        {
            return;
        }

        if (command.Kind is PdfPathCommandKind.QuadraticTo or PdfPathCommandKind.CubicTo || count >= points.Length)
        {
            simple = false;
            return;
        }

        points[count] = Vector2.Transform(new(command.Point.X, command.Point.Y), ctm);
        count++;
    }

    /// <summary>Adds a contour as a rule when it is a thin, axis aligned rectangle.</summary>
    /// <param name="points">The contour's points in viewer space.</param>
    /// <param name="simple">Whether the contour is made of straight lines.</param>
    /// <param name="rulings">Receives the rule.</param>
    private static void AddRectangle(ReadOnlySpan<Vector2> points, bool simple, List<LayoutRuling> rulings)
    {
        if (!simple || points.Length < RectanglePoints - 1 || rulings.Count >= MaxRulings)
        {
            return;
        }

        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        if (!AllOnCorners(points, min, max))
        {
            return;
        }

        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var horizontal = width >= height;
        if ((horizontal ? width : height) >= MinLength && (horizontal ? height : width) <= MaxThickness)
        {
            rulings.Add(new(new(min.X, min.Y, max.X, max.Y), horizontal));
        }
    }

    /// <summary>Determines whether every point sits on a corner of the box, so the contour is an axis aligned rectangle.</summary>
    /// <param name="points">The contour's points.</param>
    /// <param name="min">The box's lowest corner.</param>
    /// <param name="max">The box's highest corner.</param>
    /// <returns><see langword="true"/> when all points are corners.</returns>
    private static bool AllOnCorners(ReadOnlySpan<Vector2> points, Vector2 min, Vector2 max)
    {
        foreach (var point in points)
        {
            var onX = MathF.Abs(point.X - min.X) <= AxisTolerance || MathF.Abs(point.X - max.X) <= AxisTolerance;
            var onY = MathF.Abs(point.Y - min.Y) <= AxisTolerance || MathF.Abs(point.Y - max.Y) <= AxisTolerance;
            if (!onX || !onY)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds a segment as a rule when it runs along an axis and is long enough.</summary>
    /// <param name="from">Where the segment starts, in viewer space.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="thickness">The line's thickness in viewer space.</param>
    /// <param name="rulings">Receives the rule.</param>
    private static void AddSegment(Vector2 from, Vector2 to, float thickness, List<LayoutRuling> rulings)
    {
        var dx = MathF.Abs(to.X - from.X);
        var dy = MathF.Abs(to.Y - from.Y);
        if (dy <= AxisTolerance && dx >= MinLength)
        {
            var middle = (from.Y + to.Y) * Half;
            rulings.Add(new(new(Math.Min(from.X, to.X), middle - (thickness * Half), Math.Max(from.X, to.X), middle + (thickness * Half)), true));
        }
        else if (dx <= AxisTolerance && dy >= MinLength)
        {
            var middle = (from.X + to.X) * Half;
            rulings.Add(new(new(middle - (thickness * Half), Math.Min(from.Y, to.Y), middle + (thickness * Half), Math.Max(from.Y, to.Y)), false));
        }
    }
}
