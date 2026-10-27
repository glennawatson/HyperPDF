// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>A backend font face with outlines expressed as managed PDF paths.</summary>
public interface IPdfFontFace : IDisposable
{
    /// <summary>Gets the resolved family name.</summary>
    string FamilyName { get; }

    /// <summary>Gets the face's native weight from 100 to 900.</summary>
    int Weight { get; }

    /// <summary>Gets whether the face has a native italic or oblique style.</summary>
    bool Italic { get; }

    /// <summary>Gets the number of glyphs.</summary>
    int GlyphCount { get; }

    /// <summary>Gets the ascent in thousandths of an em.</summary>
    float Ascent { get; }

    /// <summary>Gets the descent in thousandths of an em.</summary>
    float Descent { get; }

    /// <summary>Finds the glyph for a Unicode scalar.</summary>
    /// <param name="unicode">The Unicode scalar.</param>
    /// <returns>The glyph identifier, or zero.</returns>
    int GetGlyph(int unicode);

    /// <summary>Gets the glyph advance in thousandths of an em.</summary>
    /// <param name="glyph">The glyph identifier.</param>
    /// <returns>The advance.</returns>
    float GetAdvance(int glyph);

    /// <summary>Gets the bytes of one of the font's OpenType tables, so the library can read the glyph substitutions the font offers.</summary>
    /// <param name="tag">The four-character table tag, such as <c>GSUB</c>.</param>
    /// <returns>The table, or empty when the font has none or the backend cannot give it.</returns>
    ReadOnlyMemory<byte> GetTable(string tag) => default;

    /// <summary>Builds an independent managed glyph outline.</summary>
    /// <param name="glyph">The glyph identifier.</param>
    /// <returns>The outline, or null.</returns>
    PdfPath? BuildOutline(int glyph);
}
