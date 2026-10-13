// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A device colour read once from an annotation's colour array (ISO 32000-2 12.5.2, <c>/C</c> and <c>/IC</c>): one number is
/// gray, three are RGB and four are CMYK. Any other length, and a missing array, mean no colour.
/// </summary>
/// <param name="Components">How many of the four values are used: 0 for no colour, otherwise 1, 3 or 4.</param>
/// <param name="First">The first component.</param>
/// <param name="Second">The second component.</param>
/// <param name="Third">The third component.</param>
/// <param name="Fourth">The fourth component.</param>
[DebuggerDisplay("PaintColor: {Components} components")]
internal readonly record struct PaintColor(int Components, float First, float Second, float Third, float Fourth)
{
    /// <summary>The components of a gray colour.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bits to shift a packed 0xRRGGBB colour to reach its red.</summary>
    private const int RedShift = 16;

    /// <summary>The bits to shift a packed 0xRRGGBB colour to reach its green.</summary>
    private const int GreenShift = 8;

    /// <summary>The largest value of an 8-bit channel.</summary>
    private const float ChannelMaximum = 255;

    /// <summary>The position of the third component.</summary>
    private const int ThirdSlot = 2;

    /// <summary>The position of the fourth component.</summary>
    private const int FourthSlot = 3;

    /// <summary>Gets the absence of a colour.</summary>
    internal static PaintColor None => default;

    /// <summary>Gets black, the colour of a border or line that names none.</summary>
    internal static PaintColor Black => FromRgb(0);

    /// <summary>Gets a value indicating whether there is a colour to set.</summary>
    internal bool IsSet => Components != 0;

    /// <summary>Makes a colour from packed red, green and blue bytes.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>The colour.</returns>
    internal static PaintColor FromRgb(uint rgb) => new(
        RgbComponents,
        ((rgb >> RedShift) & byte.MaxValue) / ChannelMaximum,
        ((rgb >> GreenShift) & byte.MaxValue) / ChannelMaximum,
        (rgb & byte.MaxValue) / ChannelMaximum,
        0);

    /// <summary>Reads a colour array; a missing array gives the fallback and an array of another length gives no colour.</summary>
    /// <param name="array">The colour array, or <see langword="null"/>.</param>
    /// <param name="fallback">The colour used when there is no array.</param>
    /// <returns>The colour.</returns>
    internal static PaintColor Read(PdfArray? array, PaintColor fallback) => array is null ? fallback : array.Count switch
    {
        GrayComponents => new(GrayComponents, array.GetSingle(0), 0, 0, 0),
        RgbComponents => new(RgbComponents, array.GetSingle(0), array.GetSingle(1), array.GetSingle(ThirdSlot), 0),
        CmykComponents => new(CmykComponents, array.GetSingle(0), array.GetSingle(1), array.GetSingle(ThirdSlot), array.GetSingle(FourthSlot)),
        _ => None,
    };

    /// <summary>Sets the stroking colour.</summary>
    /// <param name="builder">The content.</param>
    internal void SetStroke(ref PdfContentBuilder builder)
    {
        switch (Components)
        {
            case GrayComponents:
            {
                builder.SetStrokeGray(First);
                break;
            }

            case RgbComponents:
            {
                builder.SetStrokeRgb(First, Second, Third);
                break;
            }

            case CmykComponents:
            {
                builder.SetStrokeCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Sets the filling colour.</summary>
    /// <param name="builder">The content.</param>
    internal void SetFill(ref PdfContentBuilder builder)
    {
        switch (Components)
        {
            case GrayComponents:
            {
                builder.SetFillGray(First);
                break;
            }

            case RgbComponents:
            {
                builder.SetFillRgb(First, Second, Third);
                break;
            }

            case CmykComponents:
            {
                builder.SetFillCmyk(First, Second, Third, Fourth);
                break;
            }

            default:
            {
                break;
            }
        }
    }
}
