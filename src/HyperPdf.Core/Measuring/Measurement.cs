// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Measuring;

/// <summary>Works out distances, path lengths and areas between points on a page, at a scale.</summary>
public static class Measurement
{
    /// <summary>A full turn in degrees.</summary>
    private const double FullTurn = 360;

    /// <summary>A half turn in degrees.</summary>
    private const double HalfTurn = 180;

    /// <summary>Halves the shoelace sum.</summary>
    private const double Half = 0.5;

    /// <summary>Measures the length of the path through points in page space, in PDF points.</summary>
    /// <param name="points">The points.</param>
    /// <param name="closed">Whether the path returns to its start, as a perimeter does.</param>
    /// <returns>The length in points.</returns>
    public static double Length(ReadOnlySpan<PagePoint> points, bool closed)
    {
        var length = 0.0;
        for (var i = 1; i < points.Length; i++)
        {
            length += Distance(points[i - 1], points[i]);
        }

        return closed && points.Length > 2 ? length + Distance(points[^1], points[0]) : length;
    }

    /// <summary>Measures the area enclosed by points in page space, in square PDF points.</summary>
    /// <param name="points">The corners, in order.</param>
    /// <returns>The area in square points.</returns>
    public static double Area(ReadOnlySpan<PagePoint> points)
    {
        if (points.Length < 3)
        {
            return 0;
        }

        // The shoelace formula: half the absolute sum of the cross products of neighbouring corners.
        var sum = 0.0;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            sum += ((double)a.X * b.Y) - ((double)b.X * a.Y);
        }

        return Math.Abs(sum) * Half;
    }

    /// <summary>Gets the angle of the line from one point to another, in degrees anticlockwise from the right.</summary>
    /// <param name="from">The start, in page space with y growing down.</param>
    /// <param name="to">The end.</param>
    /// <returns>The angle from 0 up to 360.</returns>
    public static double Angle(PagePoint from, PagePoint to)
    {
        var degrees = Math.Atan2(from.Y - to.Y, to.X - from.X) * HalfTurn / Math.PI;
        return degrees < 0 ? degrees + FullTurn : degrees;
    }

    /// <summary>Describes a measurement as it is shown and read out: "Distance 12.4 m at 30°", "Area 6.25 m²".</summary>
    /// <param name="mode">What is measured.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The description, or an empty string when there are too few points.</returns>
    public static string Describe(MeasureMode mode, ReadOnlySpan<PagePoint> points, MeasureScale scale)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (mode)
        {
            case MeasureMode.Distance when points.Length >= 2:
            {
                var length = scale.ToReal(Distance(points[0], points[^1]));
                return string.Create(culture, $"Distance {length:0.##} {scale.RealUnit} at {Angle(points[0], points[^1]):0}°");
            }

            case MeasureMode.Perimeter when points.Length >= 2:
            {
                return string.Create(culture, $"Perimeter {scale.ToReal(Length(points, false)):0.##} {scale.RealUnit}");
            }

            case MeasureMode.Area when points.Length >= 3:
            {
                var perPoint = scale.RealPerPoint;
                return string.Create(culture, $"Area {Area(points) * perPoint * perPoint:0.##} {scale.RealUnit}²");
            }

            default:
            {
                return string.Empty;
            }
        }
    }

    /// <summary>Measures the straight distance between two points.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other.</param>
    /// <returns>The distance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Distance(PagePoint a, PagePoint b) => Math.Sqrt(((double)(b.X - a.X) * (b.X - a.X)) + ((double)(b.Y - a.Y) * (b.Y - a.Y)));
}
