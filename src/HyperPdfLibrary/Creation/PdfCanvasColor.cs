// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>A colour in one of the device colour spaces. Make one with <see cref="Gray"/>, <see cref="Rgb"/> or <see cref="Cmyk"/>.</summary>
/// <param name="Kind">The colour space.</param>
/// <param name="First">The grey level, the red level, or the cyan amount.</param>
/// <param name="Second">The green level or the magenta amount; zero for grey.</param>
/// <param name="Third">The blue level or the yellow amount; zero for grey.</param>
/// <param name="Fourth">The black amount of a CMYK colour; zero otherwise.</param>
[DebuggerDisplay("PdfCanvasColor: {Kind} {First}, {Second}, {Third}, {Fourth}")]
public readonly record struct PdfCanvasColor(PdfColorKind Kind, float First, float Second, float Third, float Fourth)
{
    /// <summary>Gets black, in the grey space.</summary>
    public static PdfCanvasColor Black => Gray(0F);

    /// <summary>Gets white, in the grey space.</summary>
    public static PdfCanvasColor White => Gray(1F);

    /// <summary>Makes a grey colour.</summary>
    /// <param name="level">The level from 0 (black) to 1 (white).</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0 to 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCanvasColor Gray(float level) => Check(new(PdfColorKind.Gray, level, 0F, 0F, 0F));

    /// <summary>Makes a red-green-blue colour.</summary>
    /// <param name="red">The red level from 0 to 1.</param>
    /// <param name="green">The green level from 0 to 1.</param>
    /// <param name="blue">The blue level from 0 to 1.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A level is outside 0 to 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCanvasColor Rgb(float red, float green, float blue) => Check(new(PdfColorKind.Rgb, red, green, blue, 0F));

    /// <summary>Makes a cyan-magenta-yellow-black colour.</summary>
    /// <param name="cyan">The cyan amount from 0 to 1.</param>
    /// <param name="magenta">The magenta amount from 0 to 1.</param>
    /// <param name="yellow">The yellow amount from 0 to 1.</param>
    /// <param name="black">The black amount from 0 to 1.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An amount is outside 0 to 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCanvasColor Cmyk(float cyan, float magenta, float yellow, float black) => Check(new(PdfColorKind.Cmyk, cyan, magenta, yellow, black));

    /// <summary>Makes a canvas colour from an RGB colour.</summary>
    /// <param name="color">The RGB colour.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfCanvasColor FromRgb(PdfRgbColor color) => Rgb(color.Red, color.Green, color.Blue);

    /// <summary>Sets this colour as the fill colour.</summary>
    /// <param name="builder">The content being written.</param>
    internal void ApplyFill(ref PdfContentBuilder builder)
    {
        switch (Kind)
        {
            case PdfColorKind.Rgb:
            {
                builder.SetFillRgb(First, Second, Third);
                break;
            }

            case PdfColorKind.Cmyk:
            {
                builder.SetFillCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                builder.SetFillGray(First);
                break;
            }
        }
    }

    /// <summary>Sets this colour as the stroke colour.</summary>
    /// <param name="builder">The content being written.</param>
    internal void ApplyStroke(ref PdfContentBuilder builder)
    {
        switch (Kind)
        {
            case PdfColorKind.Rgb:
            {
                builder.SetStrokeRgb(First, Second, Third);
                break;
            }

            case PdfColorKind.Cmyk:
            {
                builder.SetStrokeCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                builder.SetStrokeGray(First);
                break;
            }
        }
    }

    /// <summary>Checks that every level is from 0 to 1.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The same colour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A level is outside 0 to 1 or not a number.</exception>
    private static PdfCanvasColor Check(PdfCanvasColor color)
    {
        CheckLevel(color.First);
        CheckLevel(color.Second);
        CheckLevel(color.Third);
        CheckLevel(color.Fourth);
        return color;
    }

    /// <summary>Checks one level.</summary>
    /// <param name="level">The level.</param>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0 to 1 or not a number.</exception>
    private static void CheckLevel(float level)
    {
        if (float.IsNaN(level) || level < 0F || level > 1F)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Colour levels must be from 0 to 1.");
        }
    }
}
