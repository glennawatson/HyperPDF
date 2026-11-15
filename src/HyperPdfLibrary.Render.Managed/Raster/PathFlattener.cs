// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>Turns a path into straight edges in device pixels: it applies the matrix and cuts each curve into enough lines to look smooth.</summary>
internal static class PathFlattener
{
    /// <summary>The farthest a line may stray from the curve it stands for, in pixels. Smaller than the eye resolves at any coverage.</summary>
    private const float Tolerance = 0.1F;

    /// <summary>The factor of Wang's formula for a cubic: three quarters of the largest second difference over the tolerance.</summary>
    private const float CubicFactor = 0.75F;

    /// <summary>The factor of Wang's formula for a quadratic: a quarter of the second difference over the tolerance.</summary>
    private const float QuadraticFactor = 0.25F;

    /// <summary>The most lines one curve is cut into, which bounds the work for a curve with absurd control points.</summary>
    private const int MaxSegments = 512;

    /// <summary>The weight of a control point of a cubic in its Bernstein form.</summary>
    private const float Three = 3F;

    /// <summary>The weight of the control point of a quadratic in its Bernstein form.</summary>
    private const float Two = 2F;

    /// <summary>Adds the edges of a filled path. Every subpath is closed, as a fill closes it.</summary>
    /// <param name="path">The path.</param>
    /// <param name="matrix">The matrix from the path's space to device pixels.</param>
    /// <param name="edges">Receives the edges.</param>
    internal static void AddFill(PdfPath path, Matrix3x2 matrix, EdgeList edges)
    {
        var start = default(Vector2);
        var current = default(Vector2);
        var open = false;
        foreach (ref readonly var command in path.Commands)
        {
            if (command.Kind is PdfPathCommandKind.MoveTo or PdfPathCommandKind.Close)
            {
                if (open)
                {
                    edges.Add(current.X, current.Y, start.X, start.Y);
                }

                // After a close the next segment starts where the subpath began.
                start = command.Kind == PdfPathCommandKind.MoveTo ? Map(command.Point, matrix) : start;
                current = start;
                open = false;
                continue;
            }

            current = AddSegment(command, matrix, current, edges);
            open = true;
        }

        if (open)
        {
            edges.Add(current.X, current.Y, start.X, start.Y);
        }
    }

    /// <summary>Adds the lines of one drawing command and returns where it ends.</summary>
    /// <param name="command">A line or curve command.</param>
    /// <param name="matrix">The matrix to device pixels.</param>
    /// <param name="from">Where the command starts, in device pixels.</param>
    /// <param name="edges">Receives the edges.</param>
    /// <returns>Where the command ends, in device pixels.</returns>
    internal static Vector2 AddSegment(in PdfPathCommand command, Matrix3x2 matrix, Vector2 from, EdgeList edges)
    {
        var to = Map(command.Point, matrix);
        switch (command.Kind)
        {
            case PdfPathCommandKind.QuadraticTo:
                {
                    AddQuadratic(from, Map(command.Control1, matrix), to, edges);
                    break;
                }

            case PdfPathCommandKind.CubicTo:
                {
                    AddCubic(from, Map(command.Control1, matrix), Map(command.Control2, matrix), to, edges);
                    break;
                }

            default:
                {
                    edges.Add(from.X, from.Y, to.X, to.Y);
                    break;
                }
        }

        return to;
    }

    /// <summary>Applies a matrix to a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The point in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Map(PdfPoint point, Matrix3x2 matrix) => Vector2.Transform(new(point.X, point.Y), matrix);

    /// <summary>Adds the lines of a cubic curve.</summary>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The first control point.</param>
    /// <param name="p2">The second control point.</param>
    /// <param name="p3">The end.</param>
    /// <param name="edges">Receives the edges.</param>
    internal static void AddCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeList edges)
    {
        var bend = MathF.Max((p0 - p1 - p1 + p2).Length(), (p1 - p2 - p2 + p3).Length());
        var count = SegmentCount(bend * CubicFactor);
        var step = 1F / count;
        var last = p0;
        for (var i = 1; i < count; i++)
        {
            var t = i * step;
            var u = 1F - t;
            var point = (u * u * u * p0) + (Three * u * u * t * p1) + (Three * u * t * t * p2) + (t * t * t * p3);
            edges.Add(last.X, last.Y, point.X, point.Y);
            last = point;
        }

        edges.Add(last.X, last.Y, p3.X, p3.Y);
    }

    /// <summary>Adds the lines of a quadratic curve.</summary>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The control point.</param>
    /// <param name="p2">The end.</param>
    /// <param name="edges">Receives the edges.</param>
    private static void AddQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, EdgeList edges)
    {
        var count = SegmentCount((p0 - p1 - p1 + p2).Length() * QuadraticFactor);
        var step = 1F / count;
        var last = p0;
        for (var i = 1; i < count; i++)
        {
            var t = i * step;
            var u = 1F - t;
            var point = (u * u * p0) + (Two * u * t * p1) + (t * t * p2);
            edges.Add(last.X, last.Y, point.X, point.Y);
            last = point;
        }

        edges.Add(last.X, last.Y, p2.X, p2.Y);
    }

    /// <summary>Finds how many lines a curve needs to stay within the tolerance.</summary>
    /// <param name="scaledBend">The curve's second difference, scaled by the factor of its degree.</param>
    /// <returns>The number of lines, at least one.</returns>
    private static int SegmentCount(float scaledBend)
    {
        // A control point that is not a number must not turn into an endless loop.
        var count = MathF.Ceiling(MathF.Sqrt(scaledBend / Tolerance));
        return count >= 1F ? (int)MathF.Min(count, MaxSegments) : 1;
    }
}
