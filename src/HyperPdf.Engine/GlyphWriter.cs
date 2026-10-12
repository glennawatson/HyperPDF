// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text.Fonts;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Writes text box glyph codes using the selected font.</summary>
/// <param name="FontResource">The font dictionary or reference.</param>
/// <param name="Subset">The embedded subset, or <see langword="null"/> for a built in font.</param>
/// <param name="Widths">The subset widths by code, or <see langword="null"/>.</param>
/// <param name="Standard">The built in font, when there is no subset.</param>
/// <param name="Font">The font choice.</param>
internal sealed record GlyphWriter(PdfValue FontResource, FontSubset? Subset, float[]? Widths, AppearanceFont Standard, TextBoxFont Font)
{
    /// <summary>Writes a glyph's code to the destination.</summary>
    /// <param name="glyph">The laid out glyph.</param>
    /// <param name="destination">At least two bytes.</param>
    /// <returns>The number of bytes written.</returns>
    internal int Encode(ushort glyph, Span<byte> destination)
    {
        if (Subset is null)
        {
            destination[0] = (byte)glyph;
            return 1;
        }

        var code = Subset.Map(glyph);
        destination[0] = (byte)(code >> HyperPdfAnnotationTextBoxes.HighByteShift);
        destination[1] = (byte)code;
        return HyperPdfAnnotationTextBoxes.CodeBytes;
    }

    /// <summary>Gets a glyph's width in thousandths of an em.</summary>
    /// <param name="glyph">The laid out glyph.</param>
    /// <returns>The width.</returns>
    internal float Width(ushort glyph) => Subset is null
        ? AppearanceFontMetrics.GetAdvance(Standard, (byte)glyph) * HyperPdfAnnotationTextBoxes.Thousand
        : Widths![Subset.Map(glyph)];
}
