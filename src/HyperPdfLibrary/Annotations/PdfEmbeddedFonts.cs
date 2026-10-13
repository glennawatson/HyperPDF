// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Embeds a TrueType font program that was cut down already as a composite font (<c>/Type0</c> with a
/// <c>/CIDFontType2</c> descendant, Identity-H encoding and an identity CID to glyph map), so text is written as two-byte
/// glyph numbers. To embed a whole font file and cut it down to the glyphs shown, use <see cref="PdfEmbeddedFont"/>.
/// </summary>
public static class PdfEmbeddedFonts
{
    /// <summary>The font descriptor flag for a symbolic font, which every Identity-encoded font is.</summary>
    private const int SymbolicFlag = 4;

    /// <summary>The stem width written when the font does not say; readers use it only for substitution.</summary>
    private const int DefaultStemWidth = 80;

    /// <summary>Adds a TrueType font and returns its font dictionary's id, for an appearance's <c>/Font</c> resources.</summary>
    /// <param name="store">The document.</param>
    /// <param name="font">The font.</param>
    /// <returns>The <c>/Type0</c> font's object id.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfObjectId AddTrueTypeFont(PdfObjectStore store, PdfTrueTypeFontInfo font)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(font);
        var cids = new int[font.Widths.Length];
        for (var i = 0; i < cids.Length; i++)
        {
            cids[i] = i;
        }

        var parts = new EmbeddedFontParts(font.BaseFont, EmbeddedProgramKind.TrueType, font.FontFile, font.FontBox, cids, font.Widths)
        {
            Flags = SymbolicFlag,
            ItalicAngle = font.ItalicAngle,
            Ascent = font.Ascent,
            Descent = font.Descent,
            CapHeight = font.CapHeight,
            StemV = DefaultStemWidth,
            ToUnicode = font.ToUnicode,
        };
        var sink = new PdfObjectStoreSink(store);
        return sink.Add(PdfValue.FromDictionary(EmbeddedFontWriter.Write(sink, parts)));
    }
}
