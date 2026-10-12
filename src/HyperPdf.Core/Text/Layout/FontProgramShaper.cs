// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Core.Text.Layout;

/// <summary>
/// Shapes text with only a font's character map, advances and pair kerning: one glyph per character, no ligatures or
/// joined forms. It needs no native library, so layout and tests run anywhere; the PDF writer uses full shaping.
/// </summary>
[DebuggerDisplay("FontProgramShaper: {_font.Face.Family}")]
public sealed class FontProgramShaper : ITextShaper
{
    /// <summary>The font.</summary>
    private readonly FontProgram _font;

    /// <summary>One em in font units, inverted.</summary>
    private readonly float _perUnit;

    /// <summary>Initializes a new instance of the <see cref="FontProgramShaper"/> class.</summary>
    /// <param name="font">The font.</param>
    public FontProgramShaper(FontProgram font)
    {
        ArgumentNullException.ThrowIfNull(font);
        _font = font;
        _perUnit = 1F / font.UnitsPerEm;
    }

    /// <inheritdoc/>
    public float Ascent => _font.Ascent;

    /// <inheritdoc/>
    public float Descent => _font.Descent;

    /// <inheritdoc/>
    public float UnderlinePosition => _font.UnderlinePosition;

    /// <inheritdoc/>
    public float UnderlineThickness => _font.UnderlineThickness;

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, List<ShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var start = output.Count;
        var index = 0;
        ushort previous = 0;
        while (index < text.Length)
        {
            _ = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
            var glyph = _font.GlyphFor(rune.Value);
            if (output.Count > start && previous != 0 && glyph != 0)
            {
                var kern = _font.Kerning(previous, glyph);
                if (kern != 0)
                {
                    var last = output[^1];
                    output[^1] = last with { Advance = last.Advance + (kern * _perUnit) };
                }
            }

            output.Add(new(glyph, index, _font.Advance(glyph) * _perUnit, 0, 0));
            previous = glyph;
            index += Math.Max(consumed, 1);
        }

        return TextDirection.IsRightToLeft(text);
    }

    /// <inheritdoc/>
    public bool Covers(int codePoint) => _font.GlyphFor(codePoint) != 0;
}
