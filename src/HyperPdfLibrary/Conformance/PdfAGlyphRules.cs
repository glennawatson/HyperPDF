// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on shown glyphs (ISO 19005-1, 6.3.5, 6.3.6 and 6.3.8; ISO 19005-2 and -3, 6.2.11.4.1, 6.2.11.5,
/// 6.2.11.7 and 6.2.11.8; ISO 19005-4, 6.2.10.4.1, 6.2.10.5, 6.2.10.7 and 6.2.10.9): glyphs are in the embedded program,
/// widths agree, .notdef is not shown, and the A and U levels map glyphs to valid Unicode.
/// </summary>
internal static class PdfAGlyphRules
{
    /// <summary>The largest width difference the standard treats as consistent.</summary>
    private const double WidthTolerance = 1;

    /// <summary>The byte order mark.</summary>
    private const char ByteOrderMark = '﻿';

    /// <summary>The swapped byte order mark.</summary>
    private const char SwappedByteOrderMark = '￾';

    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.GlyphNotdef,
        PdfARule.GlyphPresent,
        PdfARule.GlyphWidth,
        PdfARule.GlyphToUnicode,
        PdfARule.GlyphToUnicodeValues,
        PdfARule.GlyphPrivateUseActualText,
    ];

    /// <summary>Checks the glyphs a font shows.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="use">The font use.</param>
    internal static void Check(PdfAValidationContext context, PdfAFontUse use)
    {
        if (use.Glyphs.Count == 0)
        {
            return;
        }

        var font = PdfAGlyphFonts.Create(context, use);
        var where = PdfAFontFacts.Context(context.Document.Objects, font.Primary);
        var checksUnicode = context.Level is not (PdfALevel.PdfA1B or PdfALevel.PdfA2B or PdfALevel.PdfA3B);
        foreach (var (code, shown) in use.Glyphs)
        {
            var facts = Facts(context, font, code);
            var rendered = (shown & PdfAGlyphUse.Rendered) != 0;
            context.Require(facts.Name != PdfAEncodingTables.Notdef, PdfARule.GlyphNotdef, where);
            context.Require(!rendered || facts.Present != false, PdfARule.GlyphPresent, where);
            context.Require(!rendered || WidthsAgree(in facts), PdfARule.GlyphWidth, where);
            if (checksUnicode)
            {
                CheckUnicode(context, PdfAGlyphUnicode.Text(font, code, context.Part), shown, where);
            }
        }
    }

    /// <summary>Gets the facts of a code for the font's kind.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The facts.</returns>
    private static PdfAGlyphFacts Facts(PdfAValidationContext context, PdfAGlyphFont font, int code) => font.Use.Kind switch
    {
        PdfAFontKind.TrueType => PdfATrueTypeGlyphs.Facts(font, code),
        PdfAFontKind.Type1 => PdfAType1Glyphs.Facts(font, code),
        PdfAFontKind.Type3 => PdfAType3FontGlyphs.Facts(font, code, context.CancellationToken),
        _ => PdfACidGlyphs.Facts(font, code),
    };

    /// <summary>Determines whether the program and dictionary widths agree within one unit, or cannot be compared.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns><see langword="true"/> when they agree.</returns>
    private static bool WidthsAgree(in PdfAGlyphFacts facts) =>
        facts.ProgramWidth is not { } program || facts.DictionaryWidth is not { } dictionary || Math.Abs(program - dictionary) <= WidthTolerance;

    /// <summary>Checks a glyph's Unicode text.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="text">The text, or null.</param>
    /// <param name="shown">How the glyph is shown.</param>
    /// <param name="where">The report context.</param>
    private static void CheckUnicode(PdfAValidationContext context, string? text, PdfAGlyphUse shown, string where)
    {
        context.Require(text is not null, PdfARule.GlyphToUnicode, where);
        context.Require(text is null || text.AsSpan().IndexOfAny('\0', SwappedByteOrderMark, ByteOrderMark) < 0, PdfARule.GlyphToUnicodeValues, where);
        context.Require((shown & PdfAGlyphUse.WithoutActualText) == 0 || !PdfAPrivateUse.Contains(text), PdfARule.GlyphPrivateUseActualText, where);
    }
}
