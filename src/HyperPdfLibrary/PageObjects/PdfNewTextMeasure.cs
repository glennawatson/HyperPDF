// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Measures new text as it will be written, in the built-in font of its look or the font that is embedded when the built-in fonts lack a letter.</summary>
internal static class PdfNewTextMeasure
{
    /// <summary>The most letters of a word measured in a buffer on the stack; a longer word gets an array.</summary>
    private const int StackCodes = 256;

    /// <summary>The first character code outside Latin-1.</summary>
    private const int LatinLimit = 256;

    /// <summary>The width of a letter the standard fonts cannot show, as a multiple of the font size.</summary>
    private const float WideLetter = 0.9F;

    /// <summary>Tells whether text reads right to left, going by which way most of its letters with a direction of their own read; when as many read each way, the first decides.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when most such letters are Hebrew, Arabic or another right-to-left letter.</returns>
    internal static bool ReadsRightToLeft(ReadOnlySpan<char> text)
    {
        var balance = 0;
        var first = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var step = Lean(BidiClasses.Of(rune.Value));
            balance += step;
            if (first == 0)
            {
                first = step;
            }
        }

        return balance == 0 ? first > 0 : balance > 0;
    }

    /// <summary>Measures text with the way its words are written.</summary>
    /// <param name="meter">How the text is written.</param>
    /// <param name="text">The text.</param>
    /// <returns>The width in user space units.</returns>
    internal static float Advance(in WordMeter meter, ReadOnlySpan<char> text) =>
        meter is { Source: { } source, Glyphs: { } glyphs } ? Shaped(source, text, meter.Size, glyphs) : Builtin(meter.Font, text, meter.Size);

    /// <summary>Measures text written in a standard font; the bundled fonts have the same widths, and a letter outside Latin-1 counts as a wide one.</summary>
    /// <param name="font">The look.</param>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The width in user space units.</returns>
    internal static float Builtin(AppearanceFont font, ReadOnlySpan<char> text, float size)
    {
        Span<byte> codes = text.Length <= StackCodes ? stackalloc byte[StackCodes] : new byte[text.Length];
        var count = 0;
        var wide = 0;
        foreach (var letter in text)
        {
            if (letter < LatinLimit)
            {
                codes[count] = (byte)letter;
                count++;
            }
            else
            {
                wide++;
            }
        }

        return (wide * size * WideLetter) + AppearanceFontMetrics.MeasureAdvance(font, codes[..count], size);
    }

    /// <summary>Measures text as the shaper of a font sets it, so the advances are those of the glyphs that are written, whatever the script.</summary>
    /// <param name="source">The font.</param>
    /// <param name="text">The text, in logical order.</param>
    /// <param name="size">The font size.</param>
    /// <param name="glyphs">A list the shaper fills, which is cleared first and can be reused.</param>
    /// <returns>The width in user space units.</returns>
    internal static float Shaped(PdfFontSource source, ReadOnlySpan<char> text, float size, List<PdfShapedGlyph> glyphs)
    {
        glyphs.Clear();
        _ = source.DefaultShaper.Shape(text, PdfShapingOptions.Default, glyphs);
        var advance = 0F;
        foreach (var glyph in CollectionsMarshal.AsSpan(glyphs))
        {
            advance += glyph.Advance;
        }

        return advance * size;
    }

    /// <summary>Tells which way a letter of a bidirectional class pulls a line.</summary>
    /// <param name="type">The class.</param>
    /// <returns>-1 for a left-to-right letter, 1 for a right-to-left letter and 0 for any other.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Lean(BidiClass type)
    {
        if (type == BidiClass.L)
        {
            return -1;
        }

        return type is BidiClass.R or BidiClass.AL ? 1 : 0;
    }

    /// <summary>How words of new text are measured.</summary>
    /// <param name="Font">The look of the text.</param>
    /// <param name="Source">The font the text is embedded with, or <see langword="null"/> when the text is written in the built-in font.</param>
    /// <param name="Size">The font size.</param>
    /// <param name="Glyphs">A list the shaper fills while words are measured, or <see langword="null"/> when the text is written in the built-in font.</param>
    internal readonly record struct WordMeter(AppearanceFont Font, PdfFontSource? Source, float Size, List<PdfShapedGlyph>? Glyphs);
}
