// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>Finds an installed font for a line of new text whose letters neither the built-in fonts nor the bundled fonts have, such as Arabic, Hebrew, Indic or CJK.</summary>
internal static class NewTextFonts
{
    /// <summary>The size the font is asked for; only its look matters.</summary>
    private const float AnySize = 12F;

    /// <summary>The number of looks each family of built-in fonts has.</summary>
    private const int LooksPerFamily = 4;

    /// <summary>The number of the serif family among the built-in fonts.</summary>
    private const int SerifFamily = 1;

    /// <summary>The number of the fixed width family among the built-in fonts.</summary>
    private const int FixedFamily = 2;

    /// <summary>The bit of a font's number that makes it bold.</summary>
    private const int BoldBit = 1;

    /// <summary>The bit of a font's number that makes it slanted.</summary>
    private const int ItalicBit = 2;

    /// <summary>Finds an installed font that has the letters of a line, when the bundled font of its look lacks some.</summary>
    /// <param name="state">The engine's reusable font state, which holds the installed fonts.</param>
    /// <param name="text">The line.</param>
    /// <param name="font">The look of the line.</param>
    /// <returns>The font to embed, or <see langword="null"/> when none is needed or none installed has the letters.</returns>
    internal static PdfFontSource? Find(TextBoxFontState state, string text, AppearanceFont font)
    {
        if (!PdfNewTextRun.NeedsFallback(text, font))
        {
            return null;
        }

        var index = (int)font;
        var family = (index / LooksPerFamily) switch
        {
            SerifFamily => StandardFontFamilies.Serif,
            FixedFamily => StandardFontFamilies.Mono,
            _ => StandardFontFamilies.Sans,
        };
        var format = new TextFormat(family, AnySize, 0) { IsBold = (index & BoldBit) != 0, IsItalic = (index & ItalicBit) != 0 };
        return TextBoxFonts.Resolve(state, state.Catalog, format, text) is { Program: { } program } ? state.GetSource(program).Source : null;
    }
}
