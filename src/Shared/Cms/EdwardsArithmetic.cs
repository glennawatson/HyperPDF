// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>
/// Point arithmetic on the Edwards curves of RFC 8032, for checking signatures, on caller-supplied limbs laid out as
/// <see cref="EdwardsPoint"/> describes. It does not run in constant time, so it must never touch a private key.
/// </summary>
internal static class EdwardsArithmetic
{
    /// <summary>The limbs of the largest point.</summary>
    private const int MaxPointLimbs = EdwardsPoint.Elements * EdwardsField.MaxLimbs;

    /// <summary>Adds two points with the complete projective formula, which also doubles (Bernstein and Lange, 2007); the result may be either point.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="p">The first point.</param>
    /// <param name="q">The second point.</param>
    /// <param name="result">Receives the sum.</param>
    internal static void Add(EdwardsCurve curve, ReadOnlySpan<ulong> p, ReadOnlySpan<ulong> q, Span<ulong> result)
    {
        var field = curve.Field;
        var n = field.Limbs;
        Span<ulong> a = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> b = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> c = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> d = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> e = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> f = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> g = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> cross = stackalloc ulong[EdwardsField.MaxLimbs];
        a = a[..n];
        b = b[..n];
        c = c[..n];
        d = d[..n];
        e = e[..n];
        f = f[..n];
        g = g[..n];
        cross = cross[..n];

        // Everything read from the two points is taken first, so the result may be written over either of them.
        EdwardsFieldMath.Multiply(field, EdwardsPoint.Z(p, n), EdwardsPoint.Z(q, n), a);
        EdwardsFieldMath.Multiply(field, EdwardsPoint.X(p, n), EdwardsPoint.X(q, n), c);
        EdwardsFieldMath.Multiply(field, EdwardsPoint.Y(p, n), EdwardsPoint.Y(q, n), d);
        EdwardsFieldMath.Add(field, EdwardsPoint.X(p, n), EdwardsPoint.Y(p, n), b);
        EdwardsFieldMath.Add(field, EdwardsPoint.X(q, n), EdwardsPoint.Y(q, n), e);
        EdwardsFieldMath.Multiply(field, b, e, cross);
        EdwardsFieldMath.Subtract(field, cross, c, cross);
        EdwardsFieldMath.Subtract(field, cross, d, cross);

        EdwardsFieldMath.Multiply(field, a, a, b);
        EdwardsFieldMath.Multiply(field, curve.D, c, e);
        EdwardsFieldMath.Multiply(field, e, d, e);
        EdwardsFieldMath.Subtract(field, b, e, f);
        EdwardsFieldMath.Add(field, b, e, g);
        EdwardsFieldMath.Multiply(field, curve.A, c, c);
        EdwardsFieldMath.Subtract(field, d, c, c);

        var x = EdwardsPoint.X(result, n);
        var y = EdwardsPoint.Y(result, n);
        EdwardsFieldMath.Multiply(field, a, f, x);
        EdwardsFieldMath.Multiply(field, x, cross, x);
        EdwardsFieldMath.Multiply(field, a, g, y);
        EdwardsFieldMath.Multiply(field, y, c, y);
        EdwardsFieldMath.Multiply(field, f, g, EdwardsPoint.Z(result, n));
    }

    /// <summary>Multiplies a point by a scalar, most significant bit first.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="point">The point.</param>
    /// <param name="scalar">The non-negative scalar, as limbs.</param>
    /// <param name="result">Receives the product.</param>
    internal static void Multiply(EdwardsCurve curve, ReadOnlySpan<ulong> point, ReadOnlySpan<ulong> scalar, Span<ulong> result)
    {
        var top = EdwardsLimbs.TopBit(scalar);
        if (top < 0)
        {
            SetIdentity(curve, result);
            return;
        }

        // The leading set bit gives the point itself, so the loop starts below it.
        Span<ulong> storage = stackalloc ulong[MaxPointLimbs];
        var accumulator = storage[..EdwardsPoint.Length(curve.Field.Limbs)];
        point.CopyTo(accumulator);
        for (var bit = top - 1; bit >= 0; bit--)
        {
            Add(curve, accumulator, accumulator, accumulator);
            if (EdwardsLimbs.IsSet(scalar, bit))
            {
                Add(curve, accumulator, point, accumulator);
            }
        }

        accumulator.CopyTo(result);
    }

    /// <summary>Determines whether two projective points are the same point.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="p">The first point.</param>
    /// <param name="q">The second point.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    internal static bool Equal(EdwardsCurve curve, ReadOnlySpan<ulong> p, ReadOnlySpan<ulong> q)
    {
        var field = curve.Field;
        var n = field.Limbs;
        Span<ulong> first = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> second = stackalloc ulong[EdwardsField.MaxLimbs];
        first = first[..n];
        second = second[..n];
        EdwardsFieldMath.Multiply(field, EdwardsPoint.X(p, n), EdwardsPoint.Z(q, n), first);
        EdwardsFieldMath.Multiply(field, EdwardsPoint.X(q, n), EdwardsPoint.Z(p, n), second);
        if (!first.SequenceEqual(second))
        {
            return false;
        }

        EdwardsFieldMath.Multiply(field, EdwardsPoint.Y(p, n), EdwardsPoint.Z(q, n), first);
        EdwardsFieldMath.Multiply(field, EdwardsPoint.Y(q, n), EdwardsPoint.Z(p, n), second);
        return first.SequenceEqual(second);
    }

    /// <summary>Recovers a point's x from its y and the sign bit (RFC 8032, 5.1.3 and 5.2.3).</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="y">The y coordinate in Montgomery form, below the prime.</param>
    /// <param name="odd">Whether x is the odd root.</param>
    /// <param name="x">Receives the x coordinate in Montgomery form.</param>
    /// <returns><see langword="true"/> when a point with that y exists.</returns>
    internal static bool TryRecoverX(EdwardsCurve curve, ReadOnlySpan<ulong> y, bool odd, Span<ulong> x)
    {
        var field = curve.Field;
        var n = field.Limbs;
        Span<ulong> y2 = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> numerator = stackalloc ulong[EdwardsField.MaxLimbs];
        Span<ulong> denominator = stackalloc ulong[EdwardsField.MaxLimbs];
        y2 = y2[..n];
        numerator = numerator[..n];
        denominator = denominator[..n];
        x.Clear();
        EdwardsFieldMath.Multiply(field, y, y, y2);
        EdwardsFieldMath.Subtract(field, y2, field.One, numerator);
        EdwardsFieldMath.Multiply(field, curve.D, y2, denominator);
        EdwardsFieldMath.Subtract(field, denominator, curve.A, denominator);
        EdwardsFieldMath.Invert(field, denominator, denominator);
        EdwardsFieldMath.Multiply(field, numerator, denominator, numerator);
        if (EdwardsLimbs.IsZero(numerator))
        {
            return !odd;
        }

        if (!EdwardsFieldMath.TrySquareRoot(field, numerator, x))
        {
            return false;
        }

        // The parity is that of the plain number, not of its Montgomery form.
        EdwardsFieldMath.FromMontgomery(field, x, denominator);
        if (((denominator[0] & 1) != 0) != odd)
        {
            // The other root is the prime minus this one; the root is not zero, so the result stays below the prime.
            _ = EdwardsLimbs.Subtract(x, field.Prime, x);
        }

        return true;
    }

    /// <summary>Sets a point to the neutral element (0, 1).</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="point">Receives the point.</param>
    private static void SetIdentity(EdwardsCurve curve, Span<ulong> point)
    {
        var n = curve.Field.Limbs;
        point.Clear();
        curve.Field.One.CopyTo(EdwardsPoint.Y(point, n));
        curve.Field.One.CopyTo(EdwardsPoint.Z(point, n));
    }
}
