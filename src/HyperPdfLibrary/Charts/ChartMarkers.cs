// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Charts;

/// <summary>The shapes and dashes that tell line series apart without relying on colour.</summary>
internal static class ChartMarkers
{
    /// <summary>The kinds of marker before they repeat.</summary>
    private const int MarkerKinds = 4;

    /// <summary>The marker shape of a circle.</summary>
    private const int Circle = 0;

    /// <summary>The marker shape of a square.</summary>
    private const int Square = 1;

    /// <summary>The marker shape of a diamond.</summary>
    private const int Diamond = 2;

    /// <summary>The width of the white edge round a marker.</summary>
    private const float EdgeWidth = 1F;

    /// <summary>The dash patterns before they repeat: solid, long dashes, dots, and dash then dot.</summary>
    private static readonly float[][] DashPatterns =
    [
        [],
        [6F, 3F],
        [1F, 3F],
        [6F, 3F, 1F, 3F],
    ];

    /// <summary>Gets the dash pattern of a line series.</summary>
    /// <param name="series">The series number.</param>
    /// <returns>The dash lengths, empty for a solid line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlyMemory<float> GetDash(int series) => DashPatterns[series % DashPatterns.Length];

    /// <summary>Draws a marker for a series, centred on a point.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="series">The series number, which picks the shape.</param>
    /// <param name="color">The marker colour.</param>
    /// <param name="center">The point.</param>
    /// <param name="radius">The marker's radius.</param>
    internal static void Draw(ref PdfPageCanvas canvas, int series, PdfCanvasColor color, PdfPoint center, float radius)
    {
        var style = new PdfShapeStyle(color, new PdfPen(PdfCanvasColor.White, EdgeWidth));
        var diameter = radius + radius;
        switch (series % MarkerKinds)
        {
            case Circle:
            {
                canvas.DrawEllipse(style, center.X - radius, center.Y - radius, diameter, diameter);
                break;
            }

            case Square:
            {
                canvas.DrawRectangle(style, center.X - radius, center.Y - radius, diameter, diameter);
                break;
            }

            case Diamond:
            {
                canvas.DrawPolygon(style, [new(center.X - radius, center.Y), new(center.X, center.Y + radius), new(center.X + radius, center.Y), new(center.X, center.Y - radius)]);
                break;
            }

            default:
            {
                canvas.DrawPolygon(style, [new(center.X - radius, center.Y - radius), new(center.X, center.Y + radius), new(center.X + radius, center.Y - radius)]);
                break;
            }
        }
    }
}
