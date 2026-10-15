// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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

    /// <summary>The characters a description needs, with room for long unit names.</summary>
    private const int DescriptionCapacity = 256;

    /// <summary>Room beyond a unit name for the longest numeric descriptions.</summary>
    private const int FallbackCapacityFactor = 2;

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
        if (mode switch
        {
            MeasureMode.Distance or MeasureMode.Perimeter => points.Length < 2,
            MeasureMode.Area => points.Length < 3,
            _ => true,
        })
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[DescriptionCapacity];
        if (TryDescribe(mode, points, scale, buffer, out var written))
        {
            return new(buffer[..written]);
        }

        var rented = ArrayPool<char>.Shared.Rent(scale.RealUnit.Length + (DescriptionCapacity * FallbackCapacityFactor));
        try
        {
            return TryDescribe(mode, points, scale, rented, out written) ? new(rented, 0, written) : string.Empty;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Writes the description of a measurement into a caller buffer, without allocating.</summary>
    /// <param name="mode">What is measured.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="destination">The buffer the text is written to.</param>
    /// <param name="written">The characters written; zero when there are too few points or the buffer is too small.</param>
    /// <returns>Whether a description was written.</returns>
    public static bool TryDescribe(MeasureMode mode, ReadOnlySpan<PagePoint> points, MeasureScale scale, Span<char> destination, out int written)
    {
        var length = 0;
        var described = TryWriteDescription(mode, points, in scale, destination, ref length);
        written = described ? length : 0;
        return described;
    }

    /// <summary>Writes a description as far as the buffer allows.</summary>
    /// <param name="mode">What is measured.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="destination">The buffer the text is written to.</param>
    /// <param name="written">The characters written so far; moved past the text.</param>
    /// <returns>Whether the whole description was written.</returns>
    private static bool TryWriteDescription(MeasureMode mode, ReadOnlySpan<PagePoint> points, in MeasureScale scale, Span<char> destination, ref int written)
    {
        switch (mode)
        {
            case MeasureMode.Distance when points.Length >= 2:
            {
                return TryAppendQuantity(destination, ref written, "Distance ", scale.ToReal(Distance(points[0], points[^1])), scale.RealUnit)
                    && TryAppend(destination, ref written, " at ")
                    && TryAppend(destination, ref written, Angle(points[0], points[^1]), "0")
                    && TryAppend(destination, ref written, "°");
            }

            case MeasureMode.Perimeter when points.Length >= 2:
            {
                return TryAppendQuantity(destination, ref written, "Perimeter ", scale.ToReal(Length(points, false)), scale.RealUnit);
            }

            case MeasureMode.Area when points.Length >= 3:
            {
                var perPoint = scale.RealPerPoint;
                return TryAppendQuantity(destination, ref written, "Area ", Area(points) * perPoint * perPoint, scale.RealUnit)
                    && TryAppend(destination, ref written, "²");
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Appends a label, a number and a unit to a description being written.</summary>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The characters written so far; moved past the quantity.</param>
    /// <param name="label">The label, with its trailing space.</param>
    /// <param name="value">The number.</param>
    /// <param name="unit">The unit.</param>
    /// <returns><see langword="false"/> when the buffer is full.</returns>
    private static bool TryAppendQuantity(Span<char> destination, ref int written, string label, double value, string unit) =>
        TryAppend(destination, ref written, label)
        && TryAppend(destination, ref written, value, "0.##")
        && TryAppend(destination, ref written, " ")
        && TryAppend(destination, ref written, unit);

    /// <summary>Appends text to a description being written.</summary>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The characters written so far; moved past the text.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="false"/> when the buffer is full.</returns>
    private static bool TryAppend(Span<char> destination, ref int written, ReadOnlySpan<char> text)
    {
        if (!text.TryCopyTo(destination[written..]))
        {
            return false;
        }

        written += text.Length;
        return true;
    }

    /// <summary>Appends a number, in the current culture, to a description being written.</summary>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The characters written so far; moved past the number.</param>
    /// <param name="value">The number.</param>
    /// <param name="format">The numeric format.</param>
    /// <returns><see langword="false"/> when the buffer is full.</returns>
    private static bool TryAppend(Span<char> destination, ref int written, double value, string format)
    {
        if (!value.TryFormat(destination[written..], out var count, format, CultureInfo.CurrentCulture))
        {
            return false;
        }

        written += count;
        return true;
    }

    /// <summary>Measures the straight distance between two points.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other.</param>
    /// <returns>The distance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double Distance(PagePoint a, PagePoint b) => Math.Sqrt(((double)(b.X - a.X) * (b.X - a.X)) + ((double)(b.Y - a.Y) * (b.Y - a.Y)));
}
