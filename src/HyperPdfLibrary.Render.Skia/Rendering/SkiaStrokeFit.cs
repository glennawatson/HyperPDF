// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Fits strokes to the pixel grid: whole-pixel widths and, for horizontal and vertical lines and rectangle edges, positions
/// that cover whole pixels (ISO 32000-2 10.7.5), and a thinnest line.
/// </summary>
internal static class SkiaStrokeFit
{
    /// <summary>The smallest device scale used when dividing a width by it.</summary>
    private const float MinScale = 1e-6F;

    /// <summary>How far, in pixels, a line may lean and still count as horizontal or vertical.</summary>
    private const float AxisTolerance = 1e-3F;

    /// <summary>Half a pixel, the offset of a pixel's centre.</summary>
    private const float HalfPixel = 0.5F;

    /// <summary>Resolves the width a stroke is drawn with.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <param name="pageAdjust">Whether the page turned stroke adjustment on with <c>/SA</c>.</param>
    /// <param name="lineWidth">The line width in the stroke's space.</param>
    /// <param name="deviceScale">The device pixels one unit of the stroke's space spans.</param>
    /// <returns>The width to draw with, and whether it was fitted.</returns>
    internal static FittedWidth Resolve(PdfRenderQuality quality, bool pageAdjust, float lineWidth, float deviceScale)
    {
        var minimum = quality.EffectiveMinimumLineWidth;
        var adjust = IsAdjusting(quality, pageAdjust);
        if ((!adjust && minimum <= 0) || !(deviceScale > MinScale))
        {
            return new(lineWidth, false);
        }

        var pixels = lineWidth * deviceScale;
        if (adjust)
        {
            pixels = MathF.Max(1, MathF.Round(pixels));
        }

        pixels = MathF.Max(pixels, minimum);
        return new(pixels / deviceScale, true);
    }

    /// <summary>Determines whether strokes are fitted to whole pixels.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <param name="pageAdjust">Whether the page turned stroke adjustment on.</param>
    /// <returns><see langword="true"/> when fitting applies.</returns>
    internal static bool IsAdjusting(PdfRenderQuality quality, bool pageAdjust) =>
        quality.StrokeAdjustment == PdfStrokeAdjustment.Always || (quality.StrokeAdjustment == PdfStrokeAdjustment.Document && pageAdjust);

    /// <summary>Gets the average device pixels one unit of a space spans.</summary>
    /// <param name="matrix">The matrix from the space to device pixels.</param>
    /// <returns>The scale, the square root of the area one unit covers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float DeviceScale(Matrix3x2 matrix) => MathF.Sqrt(MathF.Abs(matrix.GetDeterminant()));

    /// <summary>Moves a horizontal or vertical line so its stroke covers whole pixels.</summary>
    /// <param name="from">The start in the stroke's space.</param>
    /// <param name="to">The end in the stroke's space.</param>
    /// <param name="toDevice">The matrix from the stroke's space to device pixels.</param>
    /// <param name="deviceToPage">The matrix from device pixels back to the space the picture is recorded in.</param>
    /// <param name="pixelWidth">The whole-pixel width the line is drawn with.</param>
    /// <param name="fitted">Receives the line's ends in the picture's space.</param>
    /// <returns><see langword="false"/> when the line is not horizontal or vertical on the device.</returns>
    internal static bool TryFitLine(SKPoint from, SKPoint to, Matrix3x2 toDevice, SKMatrix deviceToPage, int pixelWidth, out FittedLine fitted)
    {
        var start = Vector2.Transform(new(from.X, from.Y), toDevice);
        var end = Vector2.Transform(new(to.X, to.Y), toDevice);
        fitted = default;
        if (MathF.Abs(start.X - end.X) <= AxisTolerance)
        {
            var x = SnapCentre(start.X, pixelWidth);
            fitted = new(deviceToPage.MapPoint(x, start.Y), deviceToPage.MapPoint(x, end.Y));
            return true;
        }

        if (MathF.Abs(start.Y - end.Y) > AxisTolerance)
        {
            return false;
        }

        var y = SnapCentre(start.Y, pixelWidth);
        fitted = new(deviceToPage.MapPoint(start.X, y), deviceToPage.MapPoint(end.X, y));
        return true;
    }

    /// <summary>Moves the edges of an axis-aligned rectangle so its stroke covers whole pixels.</summary>
    /// <param name="rectangle">The rectangle in the stroke's space.</param>
    /// <param name="toDevice">The matrix from the stroke's space to device pixels.</param>
    /// <param name="deviceToPage">The matrix from device pixels back to the space the picture is recorded in.</param>
    /// <param name="pixelWidth">The whole-pixel width the rectangle is stroked with.</param>
    /// <param name="fitted">Receives the rectangle in the picture's space.</param>
    /// <returns><see langword="false"/> when the rectangle is turned or sheared on the device.</returns>
    internal static bool TryFitRectangle(SKRect rectangle, Matrix3x2 toDevice, SKMatrix deviceToPage, int pixelWidth, out SKRect fitted)
    {
        fitted = default;
        if (MathF.Abs(toDevice.M12) > AxisTolerance || MathF.Abs(toDevice.M21) > AxisTolerance)
        {
            return false;
        }

        var a = Vector2.Transform(new(rectangle.Left, rectangle.Top), toDevice);
        var b = Vector2.Transform(new(rectangle.Right, rectangle.Bottom), toDevice);
        var left = SnapCentre(MathF.Min(a.X, b.X), pixelWidth);
        var right = SnapCentre(MathF.Max(a.X, b.X), pixelWidth);
        var top = SnapCentre(MathF.Min(a.Y, b.Y), pixelWidth);
        var bottom = SnapCentre(MathF.Max(a.Y, b.Y), pixelWidth);
        var corner = deviceToPage.MapPoint(left, top);
        var opposite = deviceToPage.MapPoint(right, bottom);
        fitted = new(MathF.Min(corner.X, opposite.X), MathF.Min(corner.Y, opposite.Y), MathF.Max(corner.X, opposite.X), MathF.Max(corner.Y, opposite.Y));
        return true;
    }

    /// <summary>Moves a stroke's centre line so the stroke covers whole pixels: onto a pixel centre for an odd width, onto a pixel edge for an even one.</summary>
    /// <param name="value">The centre line's device coordinate.</param>
    /// <param name="pixelWidth">The whole-pixel width.</param>
    /// <returns>The snapped coordinate.</returns>
    private static float SnapCentre(float value, int pixelWidth) =>
        (pixelWidth & 1) == 1 ? MathF.Floor(value) + HalfPixel : MathF.Round(value);

    /// <summary>A stroke width after fitting.</summary>
    /// <param name="Width">The width in the stroke's space.</param>
    /// <param name="Fitted">Whether the width was fitted, so no extra hairline is needed under it.</param>
    internal readonly record struct FittedWidth(float Width, bool Fitted);

    /// <summary>A line fitted to the pixel grid, in the space the picture is recorded in.</summary>
    /// <param name="From">The start.</param>
    /// <param name="To">The end.</param>
    internal readonly record struct FittedLine(SKPoint From, SKPoint To);
}
