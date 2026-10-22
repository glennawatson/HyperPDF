// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia.Fonts;

/// <summary>Copies native glyph contours into managed paths with upward glyph coordinates.</summary>
internal static class SkiaFontOutline
{
    /// <summary>The points needed for one cubic contour segment.</summary>
    private const int SegmentPoints = 4;

    /// <summary>The power of two controlling native conic subdivisions.</summary>
    private const int ConicSubdivisionPower = 2;

    /// <summary>The final point of a quadratic segment.</summary>
    private const int QuadraticEndPoint = 2;

    /// <summary>The final point of a cubic segment.</summary>
    private const int CubicEndPoint = 3;

    /// <summary>The control and end points produced by each conic subdivision.</summary>
    private const int QuadraticStep = 2;

    /// <summary>Copies a native glyph path without retaining its native lifetime.</summary>
    /// <param name="path">The borrowed native path.</param>
    /// <returns>The independent managed path.</returns>
    internal static PdfPath Convert(SKPath path)
    {
        var builder = new PdfPathBuilder { FillRule = path.FillType == SKPathFillType.EvenOdd ? PdfPathFillRule.EvenOdd : PdfPathFillRule.Winding };
        using var iterator = path.CreateRawIterator();
        Span<SKPoint> points = stackalloc SKPoint[SegmentPoints];
        var verb = iterator.Next(points);
        while (verb != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    {
                        builder.MoveTo(points[0].X, -points[0].Y);
                        break;
                    }

                case SKPathVerb.Line:
                    {
                        builder.LineTo(points[1].X, -points[1].Y);
                        break;
                    }

                case SKPathVerb.Quad:
                    {
                        builder.QuadTo(points[1].X, -points[1].Y, points[QuadraticEndPoint].X, -points[QuadraticEndPoint].Y);
                        break;
                    }

                case SKPathVerb.Cubic:
                    {
                        builder.CubicTo(points[1].X, -points[1].Y, points[QuadraticEndPoint].X, -points[QuadraticEndPoint].Y, points[CubicEndPoint].X, -points[CubicEndPoint].Y);
                        break;
                    }

                case SKPathVerb.Conic:
                    {
                        AddConic(builder, points, iterator.ConicWeight());
                        break;
                    }

                case SKPathVerb.Close:
                    {
                        builder.Close();
                        break;
                    }

                case SKPathVerb.Done:
                    {
                        break;
                    }
            }

            verb = iterator.Next(points);
        }

        return builder.Detach();
    }

    /// <summary>Converts a rational quadratic to managed quadratic segments.</summary>
    /// <param name="builder">The path being built.</param>
    /// <param name="points">The contour points.</param>
    /// <param name="weight">The rational quadratic's weight.</param>
    private static void AddConic(PdfPathBuilder builder, ReadOnlySpan<SKPoint> points, float weight)
    {
        var quadratics = SKPath.ConvertConicToQuads(points[0], points[1], points[QuadraticEndPoint], weight, ConicSubdivisionPower);
        for (var index = 1; index + 1 < quadratics.Length; index += QuadraticStep)
        {
            var control = quadratics[index];
            var end = quadratics[index + 1];
            builder.QuadTo(control.X, -control.Y, end.X, -end.Y);
        }
    }
}
