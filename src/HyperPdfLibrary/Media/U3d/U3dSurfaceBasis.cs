// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Evaluates the blending functions of one direction of a free-form surface (ECMA-363 11.3).</summary>
internal static class U3dSurfaceBasis
{
    /// <summary>The most degree a surface may have, which keeps the blending functions in stack space.</summary>
    internal const int MaxDegree = 31;

    /// <summary>The distance from the end of the knot list to the start of the last piece.</summary>
    private const int LastPieceOffset = 2;

    /// <summary>Gets the lowest and highest parameter a surface takes in one direction.</summary>
    /// <param name="knots">The knots with repeats written out.</param>
    /// <param name="degree">The degree.</param>
    /// <param name="isBezier"><see langword="true"/> for a Bezier direction.</param>
    /// <returns>The lowest and highest parameter.</returns>
    internal static Domain DomainOf(double[] knots, int degree, bool isBezier) =>
        isBezier ? new(knots[0], knots[^1]) : new(knots[degree], knots[knots.Length - degree - 1]);

    /// <summary>Fills in the blending functions that are not zero at a parameter.</summary>
    /// <param name="knots">The knots with repeats written out.</param>
    /// <param name="degree">The degree.</param>
    /// <param name="count">The number of control points in the direction.</param>
    /// <param name="isBezier"><see langword="true"/> for a Bezier direction.</param>
    /// <param name="parameter">The parameter, which is kept inside the surface's range.</param>
    /// <param name="values">Receives <c>degree + 1</c> values.</param>
    /// <returns>The index of the control point the first value belongs to.</returns>
    internal static int Evaluate(double[] knots, int degree, int count, bool isBezier, double parameter, Span<double> values) =>
        isBezier ? Bernstein(knots, degree, parameter, values) : BSpline(knots, degree, count, parameter, values);

    /// <summary>Evaluates Bernstein polynomials in the piece of a piecewise Bezier curve that holds a parameter.</summary>
    /// <param name="knots">The parameter at each piece boundary.</param>
    /// <param name="degree">The degree of each piece.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="values">Receives the values.</param>
    /// <returns>The index of the first control point of the piece.</returns>
    private static int Bernstein(double[] knots, int degree, double parameter, Span<double> values)
    {
        var piece = 0;
        while (piece < knots.Length - LastPieceOffset && parameter >= knots[piece + 1])
        {
            piece++;
        }

        var width = knots[piece + 1] - knots[piece];
        var t = width > 0 ? Math.Clamp((parameter - knots[piece]) / width, 0, 1) : 0;
        values[0] = 1;
        for (var j = 1; j <= degree; j++)
        {
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                var next = values[r];
                values[r] = saved + ((1 - t) * next);
                saved = t * next;
            }

            values[j] = saved;
        }

        return piece * degree;
    }

    /// <summary>Evaluates B-spline blending functions with the Cox-de Boor recurrence.</summary>
    /// <param name="knots">The knots.</param>
    /// <param name="degree">The degree.</param>
    /// <param name="count">The number of control points.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="values">Receives the values.</param>
    /// <returns>The index of the control point the first value belongs to.</returns>
    private static int BSpline(double[] knots, int degree, int count, double parameter, Span<double> values)
    {
        var span = degree;
        while (span < count - 1 && parameter >= knots[span + 1])
        {
            span++;
        }

        Span<double> left = stackalloc double[MaxDegree + 1];
        Span<double> right = stackalloc double[MaxDegree + 1];
        values[0] = 1;
        for (var j = 1; j <= degree; j++)
        {
            left[j] = parameter - knots[span + 1 - j];
            right[j] = knots[span + j] - parameter;
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                var denominator = right[r + 1] + left[j - r];
                var term = denominator == 0 ? 0 : values[r] / denominator;
                values[r] = saved + (right[r + 1] * term);
                saved = left[j - r] * term;
            }

            values[j] = saved;
        }

        return span - degree;
    }

    /// <summary>A lowest and highest parameter.</summary>
    /// <param name="Low">The lowest.</param>
    /// <param name="High">The highest.</param>
    internal readonly record struct Domain(double Low, double High);
}
