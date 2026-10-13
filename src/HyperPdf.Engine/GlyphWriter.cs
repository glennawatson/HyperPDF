// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Writes text box glyph codes using the selected font.</summary>
/// <param name="FontResource">The font dictionary or reference.</param>
/// <param name="Embedded">The embedded installed font, or <see langword="null"/> for a built in font.</param>
/// <param name="Text">The text that was laid out, which the glyphs' clusters index.</param>
/// <param name="Standard">The built in font, when there is no embedded font.</param>
/// <param name="Font">The font choice.</param>
internal sealed record GlyphWriter(PdfValue FontResource, PdfEmbeddedFont? Embedded, string Text, AppearanceFont Standard, TextBoxFont Font)
{
    /// <summary>Writes a built in font's code for a glyph.</summary>
    /// <param name="glyph">The laid out glyph.</param>
    /// <param name="destination">At least one byte.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int Encode(ushort glyph, Span<byte> destination)
    {
        destination[0] = (byte)glyph;
        return 1;
    }

    /// <summary>Gets a built in glyph's width in thousandths of an em.</summary>
    /// <param name="glyph">The laid out glyph.</param>
    /// <returns>The width.</returns>
    internal float Width(ushort glyph) => AppearanceFontMetrics.GetAdvance(Standard, (byte)glyph) * HyperPdfAnnotationTextBoxes.Thousand;

    /// <summary>Completes the embedded font once the text is written; a built in font needs nothing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Finish() => Embedded?.Finish();
}
