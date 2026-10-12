// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>Smooths drawn signature strokes so saved curves stay round instead of showing pointer steps.</summary>
public static class SignatureStrokes
{
    /// <summary>The points made for each step between two pointer samples.</summary>
    internal const int Subdivisions = 4;

    /// <summary>The fewest points that have a curve to smooth.</summary>
    private const int MinCurvePoints = 3;

    /// <summary>How far ahead the last control point of a segment is.</summary>
    private const int Lookahead = 2;

    /// <summary>Half, from the Catmull-Rom spline's basis.</summary>
    private const float Half = 0.5F;

    /// <summary>The Catmull-Rom weight of the outer points in the constant and squared terms.</summary>
    private const float Twice = 2;

    /// <summary>The Catmull-Rom weight of the start point in the squared term.</summary>
    private const float StartSquareWeight = 5;

    /// <summary>The Catmull-Rom weight of the following point in the squared term.</summary>
    private const float NextSquareWeight = 4;

    /// <summary>The Catmull-Rom weight of the inner points in the cubed term.</summary>
    private const float CubeWeight = 3;

    /// <summary>Gets how many points a stroke has after smoothing.</summary>
    /// <param name="pointCount">The pointer samples in the stroke.</param>
    /// <returns>The smoothed point count.</returns>
    public static int SmoothedLength(int pointCount) => pointCount < MinCurvePoints ? pointCount : checked(((pointCount - 1) * Subdivisions) + 1);

    /// <summary>
    /// Writes a Catmull-Rom curve through every sample of a stroke. The curve passes through each sample, so the
    /// signature keeps its shape; strokes too short to curve are copied.
    /// </summary>
    /// <param name="stroke">The pointer samples.</param>
    /// <param name="destination">Receives <see cref="SmoothedLength"/> points.</param>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static void Smooth(ReadOnlySpan<PagePoint> stroke, Span<PagePoint> destination)
    {
        var length = SmoothedLength(stroke.Length);
        if (destination.Length < length)
        {
            throw new ArgumentException("The destination is too small for the smoothed stroke.", nameof(destination));
        }

        if (stroke.Length < MinCurvePoints)
        {
            stroke.CopyTo(destination);
            return;
        }

        var written = 0;
        var last = stroke.Length - 1;
        for (var i = 0; i < last; i++)
        {
            var before = stroke[Math.Max(0, i - 1)];
            var start = stroke[i];
            var end = stroke[i + 1];
            var after = stroke[Math.Min(last, i + Lookahead)];
            for (var step = 0; step < Subdivisions; step++)
            {
                destination[written] = Interpolate(before, start, end, after, (float)step / Subdivisions);
                written++;
            }
        }

        destination[written] = stroke[last];
    }

    /// <summary>Finds the point a share of the way along one curve segment.</summary>
    /// <param name="before">The sample before the segment.</param>
    /// <param name="start">The segment's start.</param>
    /// <param name="end">The segment's end.</param>
    /// <param name="after">The sample after the segment.</param>
    /// <param name="t">The share, from 0 to 1.</param>
    /// <returns>The point.</returns>
    private static PagePoint Interpolate(PagePoint before, PagePoint start, PagePoint end, PagePoint after, float t) =>
        new(Interpolate(before.X, start.X, end.X, after.X, t), Interpolate(before.Y, start.Y, end.Y, after.Y, t));

    /// <summary>Finds one coordinate a share of the way along one curve segment.</summary>
    /// <param name="before">The coordinate before the segment.</param>
    /// <param name="start">The segment's start.</param>
    /// <param name="end">The segment's end.</param>
    /// <param name="after">The coordinate after the segment.</param>
    /// <param name="t">The share, from 0 to 1.</param>
    /// <returns>The coordinate.</returns>
    private static float Interpolate(float before, float start, float end, float after, float t)
    {
        var square = t * t;
        return Half * (
            (Twice * start)
            + ((end - before) * t)
            + (((Twice * before) - (StartSquareWeight * start) + (NextSquareWeight * end) - after) * square)
            + ((-before + (CubeWeight * start) - (CubeWeight * end) + after) * square * t));
    }
}
