// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>How a picture is drawn for the screen: where text is placed, how its edges look and how thin lines are fitted to pixels.</summary>
/// <param name="Hinting">Whether text is moved onto whole pixels.</param>
/// <param name="Lcd">Whether text is smoothed with the red, green and blue stripes of a screen's pixels.</param>
/// <param name="TextTable">The table that reshapes the coverage of text, or <see langword="null"/> to leave it.</param>
/// <param name="ShapeTable">The table that reshapes the coverage of shapes and lines, or <see langword="null"/> to leave it.</param>
/// <param name="MinimumLineWidth">The thinnest a line is drawn, in device pixels, or 0 for no limit.</param>
/// <param name="Adjustment">When lines are fitted to whole pixels.</param>
[DebuggerDisplay("RasterStyle: hinting {Hinting}, lcd {Lcd}, lines {Adjustment}/{MinimumLineWidth}")]
internal readonly record struct RasterStyle(PdfTextHinting Hinting, bool Lcd, byte[]? TextTable, byte[]? ShapeTable, float MinimumLineWidth, PdfStrokeAdjustment Adjustment)
{
    /// <summary>The smallest scale a line width is fitted at.</summary>
    private const float MinScale = 1e-6F;

    /// <summary>How far a matrix may lean and still count as upright.</summary>
    private const float AxisTolerance = 1e-3F;

    /// <summary>
    /// Gets a value indicating whether the coverage of small glyphs is kept and used again. Their origins are then moved onto
    /// steps of a pixel, which is why it is only done when the quality asks for glyph masks.
    /// </summary>
    internal bool Masks { get; init; }

    /// <summary>Makes the style of a render quality.</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The style.</returns>
    internal static RasterStyle From(PdfRenderQuality quality) => new(
        quality.TextHinting,
        quality.TextEdges == PdfTextEdges.Lcd,
        quality.TextEdges == PdfTextEdges.Sharp ? CoverageTables.Sharp : CoverageTables.Contrast(quality.EffectiveTextContrast),
        quality.SmoothGraphics ? null : CoverageTables.Sharp,
        quality.EffectiveMinimumLineWidth,
        quality.StrokeAdjustment) { Masks = quality.UsesGlyphMasks };

    /// <summary>Moves a glyph onto whole pixels. Slight hinting moves its baseline onto a pixel row; full hinting moves its origin onto a pixel too.</summary>
    /// <param name="matrix">The matrix from the glyph's space to device pixels.</param>
    /// <returns>The matrix to draw with. Text that is turned or slanted is left where it is.</returns>
    internal Matrix3x2 Place(Matrix3x2 matrix)
    {
        if (Hinting == PdfTextHinting.None || MathF.Abs(matrix.M12) > AxisTolerance || MathF.Abs(matrix.M21) > AxisTolerance)
        {
            return matrix;
        }

        matrix.M32 = MathF.Round(matrix.M32);
        if (Hinting == PdfTextHinting.Full)
        {
            matrix.M31 = MathF.Round(matrix.M31);
        }

        return matrix;
    }

    /// <summary>Fits the width of a line to the pixels it is drawn on.</summary>
    /// <param name="width">The line width in the line's own space.</param>
    /// <param name="matrix">The matrix from the line's space to device pixels.</param>
    /// <param name="pageAdjusts">Whether the page itself asks for its lines to be fitted.</param>
    /// <returns>The width to stroke with, in the line's own space.</returns>
    internal float FitWidth(float width, Matrix3x2 matrix, bool pageAdjusts)
    {
        var adjust = Adjustment == PdfStrokeAdjustment.Always || (Adjustment == PdfStrokeAdjustment.Document && pageAdjusts);
        var scale = MathF.Sqrt(MathF.Abs(matrix.GetDeterminant()));
        if ((!adjust && MinimumLineWidth <= 0) || !(scale > MinScale))
        {
            return width;
        }

        var pixels = width * scale;
        if (adjust)
        {
            // A fitted line covers whole pixels, and at least one, so every line of the same width looks the same.
            pixels = MathF.Max(1, MathF.Round(pixels));
        }

        return MathF.Max(pixels, MinimumLineWidth) / scale;
    }
}
