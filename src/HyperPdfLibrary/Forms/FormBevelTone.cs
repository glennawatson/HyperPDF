// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Picks the light and shaded tones of a beveled or inset widget border (ISO 32000-2 12.5.4, Table 168). The standard
/// asks only for a simulated raised or recessed edge, so the tones are the border colour moved halfway towards white
/// (the lit edge) or halfway towards black (the shaded edge). Both stay recognisable as the same hue, and the lit and
/// shaded edges differ in lightness whatever the base colour, which keeps the edge visible without relying on hue.
/// </summary>
internal static class FormBevelTone
{
    /// <summary>The share of the way towards white or black that the lit and shaded edges move.</summary>
    internal const float Shift = 0.5F;

    /// <summary>The grey used as the base when a widget has no border or background colour.</summary>
    private const float DefaultBase = 0.75F;

    /// <summary>Sets the fill or stroke colour to a tone of a base colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="baseColor">The base colour components (gray, RGB or CMYK), or <see langword="null"/> for a mid grey.</param>
    /// <param name="lit">Whether to lighten the base; otherwise it is darkened.</param>
    /// <param name="stroke">Whether to set the stroke colour; otherwise the fill colour.</param>
    /// <returns><see langword="false"/> when the base has a component count that is not a device colour.</returns>
    internal static bool TryWrite(ref PdfContentBuilder builder, PdfArray? baseColor, bool lit, bool stroke)
    {
        if (baseColor is null)
        {
            WriteGray(ref builder, Tone(DefaultBase, lit), stroke);
            return true;
        }

        switch (baseColor.Count)
        {
            case FormAppearanceColor.GrayComponents:
            {
                WriteGray(ref builder, Tone(baseColor.GetSingle(0), lit), stroke);
                return true;
            }

            case FormAppearanceColor.RgbComponents:
            {
                WriteRgb(ref builder, Tone(baseColor.GetSingle(0), lit), Tone(baseColor.GetSingle(1), lit), Tone(baseColor.GetSingle(FormAppearanceColor.BlueSlot), lit), stroke);
                return true;
            }

            case FormAppearanceColor.CmykComponents:
            {
                WriteCmyk(ref builder, baseColor, lit, stroke);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Moves a light-is-high component (gray or RGB) towards white or black.</summary>
    /// <param name="value">The component.</param>
    /// <param name="lit">Whether to move towards white.</param>
    /// <returns>The new component.</returns>
    private static float Tone(float value, bool lit) => lit ? value + ((1 - value) * Shift) : value * (1 - Shift);

    /// <summary>Moves an ink amount (CMYK) towards no ink or full ink.</summary>
    /// <param name="value">The ink amount.</param>
    /// <param name="lit">Whether to move towards no ink.</param>
    /// <returns>The new ink amount.</returns>
    private static float Ink(float value, bool lit) => lit ? value * (1 - Shift) : value + ((1 - value) * Shift);

    /// <summary>Writes a grey fill or stroke colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="gray">The grey.</param>
    /// <param name="stroke">Whether to set the stroke colour.</param>
    private static void WriteGray(ref PdfContentBuilder builder, float gray, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeGray(gray);
        }
        else
        {
            builder.SetFillGray(gray);
        }
    }

    /// <summary>Writes an RGB fill or stroke colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="red">The red.</param>
    /// <param name="green">The green.</param>
    /// <param name="blue">The blue.</param>
    /// <param name="stroke">Whether to set the stroke colour.</param>
    private static void WriteRgb(ref PdfContentBuilder builder, float red, float green, float blue, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeRgb(red, green, blue);
        }
        else
        {
            builder.SetFillRgb(red, green, blue);
        }
    }

    /// <summary>Writes a CMYK fill or stroke colour that is a tone of a base.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="baseColor">The four base components.</param>
    /// <param name="lit">Whether to lighten.</param>
    /// <param name="stroke">Whether to set the stroke colour.</param>
    private static void WriteCmyk(ref PdfContentBuilder builder, PdfArray baseColor, bool lit, bool stroke)
    {
        var cyan = Ink(baseColor.GetSingle(0), lit);
        var magenta = Ink(baseColor.GetSingle(1), lit);
        var yellow = Ink(baseColor.GetSingle(FormAppearanceColor.BlueSlot), lit);
        var black = Ink(baseColor.GetSingle(FormAppearanceColor.BlackSlot), lit);
        if (stroke)
        {
            builder.SetStrokeCmyk(cyan, magenta, yellow, black);
        }
        else
        {
            builder.SetFillCmyk(cyan, magenta, yellow, black);
        }
    }
}
