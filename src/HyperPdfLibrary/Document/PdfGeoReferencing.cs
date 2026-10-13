// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Converts page points in a geospatial viewport to world coordinates (ISO 32000-2 12.10). The viewport's <c>/BBox</c>
/// maps to a unit square, <c>/LPTS</c> gives unit square points and <c>/GPTS</c> the world coordinates that match them,
/// and the mapping is the least-squares affine fit of those pairs. It is exact when the pairs are related by an affine
/// mapping, as the corners of a north-up map are.
/// </summary>
public static class PdfGeoReferencing
{
    /// <summary>The numbers in one point.</summary>
    private const int PairSize = 2;

    /// <summary>The fewest point pairs that fix an affine mapping, and the fewest corners of a bounds polygon.</summary>
    private const int MinimumPairs = 3;

    /// <summary>The numbers in a bounding box.</summary>
    private const int BoxNumbers = 4;

    /// <summary>The position of x2 in a bounding box.</summary>
    private const int BoxRight = 2;

    /// <summary>The position of y2 in a bounding box.</summary>
    private const int BoxTop = 3;

    /// <summary>The smallest determinant, relative to its terms, that is not treated as points in a line.</summary>
    private const double LineTolerance = 1e-12;

    /// <summary>How far in unit square terms a point may be from a bounds edge and still count as on it.</summary>
    private const double EdgeTolerance = 1e-9;

    /// <summary>The bounds when a measure gives none: the whole unit square (ISO 32000-2 Table 269).</summary>
    private static readonly double[] UnitSquare = [0, 0, 0, 1, 1, 1, 1, 0];

    /// <summary>Fits the mapping from a viewport's page space to its world coordinates.</summary>
    /// <param name="viewport">The viewport.</param>
    /// <returns>
    /// The mapping, or <see langword="null"/> when the viewport has no geospatial measure, its box is missing or flat, or
    /// its point arrays are missing, of different or odd lengths, shorter than three pairs, or in a line.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="viewport"/> is null.</exception>
    public static PdfGeoTransform? CreateTransform(PdfViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        if (viewport.Measure?.Geo is not { } geo || !TryUnitSquare(viewport.BoundingBox, out var pageToUnit))
        {
            return null;
        }

        var bounds = geo.Bounds.Length == 0 ? UnitSquare : geo.Bounds;
        if (!IsPointArray(geo.GeoPoints) || geo.LocalPoints.Length != geo.GeoPoints.Length || !IsPointArray(geo.LocalPoints) || !IsPointArray(bounds))
        {
            return null;
        }

        return TryFit(geo.LocalPoints, geo.GeoPoints, out var unitToWorld) ? new(Compose(unitToWorld, pageToUnit), pageToUnit, bounds) : null;
    }

    /// <summary>Converts a page point to its world coordinate.</summary>
    /// <param name="transform">The viewport's mapping.</param>
    /// <param name="x">The x coordinate in the page's user space.</param>
    /// <param name="y">The y coordinate in the page's user space.</param>
    /// <returns>The world coordinate, in the order of the measure's <c>/GPTS</c> pairs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> is null.</exception>
    public static PdfGeoPoint ToWorld(PdfGeoTransform transform, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return Apply(transform.PageToWorld, x, y);
    }

    /// <summary>Determines whether a page point is inside the area where the mapping is valid (the measure's <c>/Bounds</c>, or the neatline of a map).</summary>
    /// <param name="transform">The viewport's mapping.</param>
    /// <param name="x">The x coordinate in the page's user space.</param>
    /// <param name="y">The y coordinate in the page's user space.</param>
    /// <returns><see langword="true"/> when the point is inside the bounds or on their edge.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> is null.</exception>
    public static bool IsInBounds(PdfGeoTransform transform, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var unit = Apply(transform.PageToUnitSquare, x, y);
        return Contains(transform.Bounds, unit.First, unit.Second);
    }

    /// <summary>Applies an affine mapping to a point.</summary>
    /// <param name="affine">The mapping.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The mapped point.</returns>
    private static PdfGeoPoint Apply(in PdfGeoAffine affine, double x, double y) =>
        new((affine.A * x) + (affine.B * y) + affine.C, (affine.D * x) + (affine.E * y) + affine.F);

    /// <summary>Makes the mapping from page space to the unit square of a box written as x1 y1 x2 y2.</summary>
    /// <param name="box">The box numbers in the order written.</param>
    /// <param name="pageToUnit">The mapping: (x1, y1) goes to (0, 0) and (x2, y2) to (1, 1).</param>
    /// <returns><see langword="true"/> when the box has four finite numbers and is not flat.</returns>
    private static bool TryUnitSquare(double[] box, out PdfGeoAffine pageToUnit)
    {
        pageToUnit = default;
        if (box.Length != BoxNumbers || !Array.TrueForAll(box, double.IsFinite))
        {
            return false;
        }

        var width = box[BoxRight] - box[0];
        var height = box[BoxTop] - box[1];
        if (width == 0 || height == 0)
        {
            return false;
        }

        pageToUnit = new(1 / width, 0, -box[0] / width, 0, 1 / height, -box[1] / height);
        return true;
    }

    /// <summary>Determines whether numbers form at least three finite pairs.</summary>
    /// <param name="points">The numbers.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool IsPointArray(double[] points) =>
        points.Length >= PairSize * MinimumPairs && points.Length % PairSize == 0 && Array.TrueForAll(points, double.IsFinite);

    /// <summary>Fits the least-squares affine mapping from unit square points to world points.</summary>
    /// <param name="local">The unit square pairs.</param>
    /// <param name="world">The world pairs, one for each unit square pair.</param>
    /// <param name="fit">The mapping.</param>
    /// <returns><see langword="false"/> when the unit square points are in a line, so no mapping fits.</returns>
    private static bool TryFit(double[] local, double[] world, out PdfGeoAffine fit)
    {
        // Centring on the means keeps the sums well conditioned for coordinates far from zero, such as eastings.
        var count = local.Length / PairSize;
        var meanU = 0.0;
        var meanV = 0.0;
        var meanFirst = 0.0;
        var meanSecond = 0.0;
        for (var i = 0; i < local.Length; i += PairSize)
        {
            meanU += local[i];
            meanV += local[i + 1];
            meanFirst += world[i];
            meanSecond += world[i + 1];
        }

        meanU /= count;
        meanV /= count;
        meanFirst /= count;
        meanSecond /= count;
        var uu = 0.0;
        var uv = 0.0;
        var vv = 0.0;
        var firstByU = 0.0;
        var firstByV = 0.0;
        var secondByU = 0.0;
        var secondByV = 0.0;
        for (var i = 0; i < local.Length; i += PairSize)
        {
            var u = local[i] - meanU;
            var v = local[i + 1] - meanV;
            uu += u * u;
            uv += u * v;
            vv += v * v;
            firstByU += u * (world[i] - meanFirst);
            firstByV += v * (world[i] - meanFirst);
            secondByU += u * (world[i + 1] - meanSecond);
            secondByV += v * (world[i + 1] - meanSecond);
        }

        var determinant = (uu * vv) - (uv * uv);
        if (!(determinant > LineTolerance * uu * vv))
        {
            fit = default;
            return false;
        }

        var a = ((firstByU * vv) - (firstByV * uv)) / determinant;
        var b = ((firstByV * uu) - (firstByU * uv)) / determinant;
        var d = ((secondByU * vv) - (secondByV * uv)) / determinant;
        var e = ((secondByV * uu) - (secondByU * uv)) / determinant;
        fit = new(a, b, meanFirst - (a * meanU) - (b * meanV), d, e, meanSecond - (d * meanU) - (e * meanV));
        return true;
    }

    /// <summary>Composes two affine mappings: <paramref name="inner"/> first, then <paramref name="outer"/>.</summary>
    /// <param name="outer">The mapping applied second.</param>
    /// <param name="inner">The mapping applied first.</param>
    /// <returns>The composed mapping.</returns>
    private static PdfGeoAffine Compose(in PdfGeoAffine outer, in PdfGeoAffine inner) => new(
        (outer.A * inner.A) + (outer.B * inner.D),
        (outer.A * inner.B) + (outer.B * inner.E),
        (outer.A * inner.C) + (outer.B * inner.F) + outer.C,
        (outer.D * inner.A) + (outer.E * inner.D),
        (outer.D * inner.B) + (outer.E * inner.E),
        (outer.D * inner.C) + (outer.E * inner.F) + outer.F);

    /// <summary>Determines whether a point is inside a polygon or on its edge, by the even-odd rule.</summary>
    /// <param name="polygon">The polygon's pairs; it need not repeat its first point.</param>
    /// <param name="u">The point's first coordinate.</param>
    /// <param name="v">The point's second coordinate.</param>
    /// <returns><see langword="true"/> when inside or on an edge.</returns>
    private static bool Contains(double[] polygon, double u, double v)
    {
        var inside = false;
        var previous = polygon.Length - PairSize;
        for (var i = 0; i < polygon.Length; i += PairSize)
        {
            var x1 = polygon[previous];
            var y1 = polygon[previous + 1];
            var x2 = polygon[i];
            var y2 = polygon[i + 1];
            if (IsOnEdge(x1, y1, x2, y2, u, v))
            {
                return true;
            }

            if ((y2 > v) != (y1 > v) && u < (((x1 - x2) * (v - y2)) / (y1 - y2)) + x2)
            {
                inside = !inside;
            }

            previous = i;
        }

        return inside;
    }

    /// <summary>Determines whether a point lies on a polygon edge, within <see cref="EdgeTolerance"/>.</summary>
    /// <param name="x1">The edge's first x.</param>
    /// <param name="y1">The edge's first y.</param>
    /// <param name="x2">The edge's second x.</param>
    /// <param name="y2">The edge's second y.</param>
    /// <param name="u">The point's first coordinate.</param>
    /// <param name="v">The point's second coordinate.</param>
    /// <returns><see langword="true"/> when the point is on the edge.</returns>
    private static bool IsOnEdge(double x1, double y1, double x2, double y2, double u, double v)
    {
        var cross = ((x2 - x1) * (v - y1)) - ((y2 - y1) * (u - x1));
        return Math.Abs(cross) <= EdgeTolerance
            && u >= Math.Min(x1, x2) - EdgeTolerance && u <= Math.Max(x1, x2) + EdgeTolerance
            && v >= Math.Min(y1, y2) - EdgeTolerance && v <= Math.Max(y1, y2) + EdgeTolerance;
    }
}
