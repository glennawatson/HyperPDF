// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// A shaper that reads no layout tables: one glyph for each character from the font's Unicode character map, advanced by
/// the font's own widths. It applies no GSUB or GPOS features, kerning, joining or reordering, so it suits scripts that
/// need none of them and is faster than <see cref="PdfOpenTypeShaper"/>, which is the default and handles Arabic, Indic
/// scripts and other complex text.
/// </summary>
[DebuggerDisplay("PdfBasicShaper: {_source.PostScriptName}")]
public sealed class PdfBasicShaper : IPdfGlyphShaper
{
    /// <summary>The font.</summary>
    private readonly PdfFontSource _source;

    /// <summary>Initializes a new instance of the <see cref="PdfBasicShaper"/> class.</summary>
    /// <param name="source">The font to take glyphs and advances from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public PdfBasicShaper(PdfFontSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, PdfShapingOptions options, List<PdfShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        for (var index = 0; index < text.Length;)
        {
            _ = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
            var glyph = _source.GetGlyph(rune.Value);
            output.Add(new(glyph, index, _source.GetAdvanceAlong(glyph, options.WritingMode) / PdfFontSource.EmDivisor, 0F, 0F));
            index += Math.Max(consumed, 1);
        }

        return false;
    }
}
