// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Icons;

/// <summary>
/// Strokes the lines of an icon into a coverage buffer. Each line becomes a box as wide as the stroke and each point a disc, so ends
/// and corners are round. Every box and disc winds the same way, so where they overlap the coverage adds up and is capped at full.
/// </summary>
/// <param name="buffer">The coverage buffer.</param>
/// <param name="scale">The pixels of one grid unit.</param>
/// <param name="origin">The pixel the grid's corner sits at, across and down.</param>
/// <param name="halfWidth">Half the stroke weight in pixels.</param>
internal ref struct IconStroker(CoverageBuffer buffer, float scale, float origin, float halfWidth)
{
    /// <summary>The sides of the shape that stands for a disc. A stroke is a pixel or two wide, so this is closer than a pixel to round.</summary>
    private const int DiscSides = 12;

    /// <summary>The lines a curve is cut into.</summary>
    private const int CurveSteps = 12;

    /// <summary>The fewest lines an arc is cut into.</summary>
    private const int ArcStepsLeast = 4;

    /// <summary>The lines an arc gains for each radian it turns.</summary>
    private const float ArcStepsPerRadian = 4F;

    /// <summary>The weight of a control point in the middle terms of a cubic curve.</summary>
    private const float CubicMiddle = 3F;

    /// <summary>Half, for the middle of two points.</summary>
    private const float Half = 0.5F;

    /// <summary>The length below which a line is only a point.</summary>
    private const float Tiny = 1e-4F;

    /// <summary>The x where the open line started, in pixels.</summary>
    private float _startX;

    /// <summary>The y where the open line started.</summary>
    private float _startY;

    /// <summary>The x of the pen in grid units.</summary>
    private float _gridX;

    /// <summary>The y of the pen in grid units.</summary>
    private float _gridY;

    /// <summary>The x of the pen in pixels.</summary>
    private float _x;

    /// <summary>The y of the pen in pixels.</summary>
    private float _y;

    /// <summary>Starts a new line at a point.</summary>
    /// <param name="x">The x in grid units.</param>
    /// <param name="y">The y in grid units.</param>
    internal void MoveTo(float x, float y)
    {
        _gridX = x;
        _gridY = y;
        _x = origin + (x * scale);
        _y = origin + (y * scale);
        _startX = _x;
        _startY = _y;
    }

    /// <summary>Draws a line from the pen to a point.</summary>
    /// <param name="x">The x in grid units.</param>
    /// <param name="y">The y in grid units.</param>
    internal void LineTo(float x, float y)
    {
        _gridX = x;
        _gridY = y;
        Segment(origin + (x * scale), origin + (y * scale));
    }

    /// <summary>Draws a cubic curve from the pen to a point.</summary>
    /// <param name="first">The first control point in grid units.</param>
    /// <param name="second">The second control point.</param>
    /// <param name="end">The end point.</param>
    internal void CubicTo(IconPoint first, IconPoint second, IconPoint end)
    {
        var fromX = _gridX;
        var fromY = _gridY;
        for (var step = 1; step <= CurveSteps; step++)
        {
            var t = step / (float)CurveSteps;
            var rest = 1F - t;
            var a = rest * rest * rest;
            var b = CubicMiddle * rest * rest * t;
            var c = CubicMiddle * rest * t * t;
            var d = t * t * t;
            LineTo((a * fromX) + (b * first.X) + (c * second.X) + (d * end.X), (a * fromY) + (b * first.Y) + (c * second.Y) + (d * end.Y));
        }
    }

    /// <summary>Draws an arc of an upright ellipse from the pen to a point, as path data describes one.</summary>
    /// <param name="radii">The radii of the ellipse in grid units.</param>
    /// <param name="large">Whether the arc is the longer of the two ways round.</param>
    /// <param name="sweep">Whether the arc turns clockwise on the screen.</param>
    /// <param name="end">The end point.</param>
    internal void ArcTo(IconPoint radii, bool large, bool sweep, IconPoint end)
    {
        var halfX = (_gridX - end.X) * Half;
        var halfY = (_gridY - end.Y) * Half;
        var radiusX = MathF.Abs(radii.X);
        var radiusY = MathF.Abs(radii.Y);
        if (radiusX < Tiny || radiusY < Tiny || (MathF.Abs(halfX) < Tiny && MathF.Abs(halfY) < Tiny))
        {
            LineTo(end.X, end.Y);
            return;
        }

        // Radii too short to reach from one point to the other grow until they just do.
        var reach = MathF.Sqrt(((halfX * halfX) / (radiusX * radiusX)) + ((halfY * halfY) / (radiusY * radiusY)));
        if (reach > 1F)
        {
            radiusX *= reach;
            radiusY *= reach;
        }

        var across = radiusX * radiusX * halfY * halfY;
        var down = radiusY * radiusY * halfX * halfX;
        var factor = MathF.Sqrt(MathF.Max(0F, ((radiusX * radiusX * radiusY * radiusY) - across - down) / (across + down)));
        if (large == sweep)
        {
            factor = -factor;
        }

        var centreX = factor * radiusX * halfY / radiusY;
        var centreY = -factor * radiusY * halfX / radiusX;
        var from = MathF.Atan2((halfY - centreY) / radiusY, (halfX - centreX) / radiusX);
        var turn = Turn(MathF.Atan2((-halfY - centreY) / radiusY, (-halfX - centreX) / radiusX) - from, sweep);
        var middleX = centreX + ((_gridX + end.X) * Half);
        var middleY = centreY + ((_gridY + end.Y) * Half);
        var steps = ArcStepsLeast + (int)(MathF.Abs(turn) * ArcStepsPerRadian);
        for (var step = 1; step < steps; step++)
        {
            var angle = from + (turn * step / steps);
            LineTo(middleX + (radiusX * MathF.Cos(angle)), middleY + (radiusY * MathF.Sin(angle)));
        }

        LineTo(end.X, end.Y);
    }

    /// <summary>Draws a line back to where the open line started.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Close() => Segment(_startX, _startY);

    /// <summary>Makes the angle an arc turns through agree with the way it sweeps.</summary>
    /// <param name="turn">The angle from the start to the end.</param>
    /// <param name="sweep">Whether the arc turns clockwise on the screen.</param>
    /// <returns>The angle, positive for a clockwise arc and negative for the other.</returns>
    private static float Turn(float turn, bool sweep)
    {
        if (sweep && turn < 0F)
        {
            return turn + MathF.Tau;
        }

        return !sweep && turn > 0F ? turn - MathF.Tau : turn;
    }

    /// <summary>Strokes a line from the pen to a point in pixels and moves the pen there.</summary>
    /// <param name="x">The x in pixels.</param>
    /// <param name="y">The y in pixels.</param>
    private void Segment(float x, float y)
    {
        var alongX = x - _x;
        var alongY = y - _y;
        var length = MathF.Sqrt((alongX * alongX) + (alongY * alongY));
        Disc(_x, _y);
        if (length > Tiny)
        {
            var sideX = -alongY / length * halfWidth;
            var sideY = alongX / length * halfWidth;
            buffer.Line(_x + sideX, _y + sideY, x + sideX, y + sideY);
            buffer.Line(x + sideX, y + sideY, x - sideX, y - sideY);
            buffer.Line(x - sideX, y - sideY, _x - sideX, _y - sideY);
            buffer.Line(_x - sideX, _y - sideY, _x + sideX, _y + sideY);
            Disc(x, y);
        }

        _x = x;
        _y = y;
    }

    /// <summary>Adds a disc as wide as the stroke, wound the same way as the box of a line.</summary>
    /// <param name="x">The x of its middle in pixels.</param>
    /// <param name="y">The y of its middle.</param>
    private readonly void Disc(float x, float y)
    {
        var fromX = x + halfWidth;
        var fromY = y;
        for (var side = 1; side <= DiscSides; side++)
        {
            var angle = -MathF.Tau * side / DiscSides;
            var toX = x + (halfWidth * MathF.Cos(angle));
            var toY = y + (halfWidth * MathF.Sin(angle));
            buffer.Line(fromX, fromY, toX, toY);
            fromX = toX;
            fromY = toY;
        }
    }
}
