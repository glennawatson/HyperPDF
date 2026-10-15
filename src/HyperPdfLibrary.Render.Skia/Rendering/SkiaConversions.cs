// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Converts library values to Skia values.</summary>
internal static class SkiaConversions
{
    /// <summary>The largest alpha byte.</summary>
    private const float AlphaMax = 255;

    /// <summary>Converts managed rectangle edges.</summary>
    /// <param name="rectangle">The managed rectangle.</param>
    /// <returns>The native rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKRect ToSkRect(PdfRect rectangle) => new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    /// <summary>Converts native rectangle edges.</summary>
    /// <param name="rectangle">The native rectangle.</param>
    /// <returns>The managed rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRect ToPdfRect(SKRect rectangle) => new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    /// <summary>Converts managed color channels.</summary>
    /// <param name="color">The managed color.</param>
    /// <returns>The native color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static SKColor ToSkColor(PdfColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);

    /// <summary>Converts a resolved colour and alpha to a Skia colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha from 0 to 1.</param>
    /// <returns>The Skia colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKColor ToSkColor(in ColorState color, float alpha) =>
        new(color.Red, color.Green, color.Blue, (byte)MathF.Round(Math.Clamp(alpha, 0, 1) * AlphaMax));

    /// <summary>Converts a matrix; both use the row-vector convention, so the entries map across.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The Skia matrix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKMatrix ToSkMatrix(Matrix3x2 matrix) =>
        new(matrix.M11, matrix.M21, matrix.M31, matrix.M12, matrix.M22, matrix.M32, 0, 0, 1);

    /// <summary>Converts a blend mode.</summary>
    /// <param name="mode">The PDF blend mode.</param>
    /// <returns>The Skia blend mode.</returns>
    internal static SKBlendMode ToSkBlend(PdfBlendMode mode) => mode switch
    {
        PdfBlendMode.Multiply => SKBlendMode.Multiply,
        PdfBlendMode.Screen => SKBlendMode.Screen,
        PdfBlendMode.Overlay => SKBlendMode.Overlay,
        PdfBlendMode.Darken => SKBlendMode.Darken,
        PdfBlendMode.Lighten => SKBlendMode.Lighten,
        PdfBlendMode.ColorDodge => SKBlendMode.ColorDodge,
        PdfBlendMode.ColorBurn => SKBlendMode.ColorBurn,
        PdfBlendMode.HardLight => SKBlendMode.HardLight,
        PdfBlendMode.SoftLight => SKBlendMode.SoftLight,
        PdfBlendMode.Difference => SKBlendMode.Difference,
        PdfBlendMode.Exclusion => SKBlendMode.Exclusion,
        PdfBlendMode.Hue => SKBlendMode.Hue,
        PdfBlendMode.Saturation => SKBlendMode.Saturation,
        PdfBlendMode.Color => SKBlendMode.Color,
        PdfBlendMode.Luminosity => SKBlendMode.Luminosity,
        _ => SKBlendMode.SrcOver,
    };
}
