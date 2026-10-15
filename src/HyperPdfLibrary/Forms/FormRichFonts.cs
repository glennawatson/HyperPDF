// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Chooses the font of a stretch of rich text. Text that asks for no family, weight or slant keeps the field's font; the
/// rest is drawn in the closest of the standard 14 fonts, which every reader has.
/// </summary>
internal static class FormRichFonts
{
    /// <summary>The number of styles in a family: regular, bold, italic and bold italic.</summary>
    private const int StylesPerFamily = 4;

    /// <summary>The offset of the bold style within a family.</summary>
    private const int BoldStyle = 1;

    /// <summary>The offset of the italic style within a family.</summary>
    private const int ItalicStyle = 2;

    /// <summary>The family index of Helvetica.</summary>
    private const int SansFamily = 0;

    /// <summary>The family index of Times.</summary>
    private const int SerifFamily = 1;

    /// <summary>The family index of Courier.</summary>
    private const int MonoFamily = 2;

    /// <summary>The resource names of the fonts, in the order of <see cref="Fonts"/>.</summary>
    private static readonly string[] Names =
    [
        "RtHelv", "RtHelvB", "RtHelvI", "RtHelvBI",
        "RtTimes", "RtTimesB", "RtTimesI", "RtTimesBI",
        "RtCour", "RtCourB", "RtCourI", "RtCourBI",
    ];

    /// <summary>Gets the standard fonts by family and style.</summary>
    private static ReadOnlySpan<StandardFont> Fonts =>
    [
        StandardFont.Helvetica, StandardFont.HelveticaBold, StandardFont.HelveticaOblique, StandardFont.HelveticaBoldOblique,
        StandardFont.TimesRoman, StandardFont.TimesBold, StandardFont.TimesItalic, StandardFont.TimesBoldItalic,
        StandardFont.Courier, StandardFont.CourierBold, StandardFont.CourierOblique, StandardFont.CourierBoldOblique,
    ];

    /// <summary>Finds the font for a style.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fieldFont">The field's own font.</param>
    /// <param name="style">The style.</param>
    /// <param name="used">The fonts chosen so far; the chosen font is added if it is not there.</param>
    /// <param name="fonts">The document's font cache, which keeps the standard fonts, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    internal static FormFont Resolve(PdfObjectStore store, FormFont fieldFont, RichStyle style, ref FormFontSet used, PdfFontCache? fonts)
    {
        if (style.Family == RichFamily.Inherit && !style.Bold && !style.Italic)
        {
            return used.Use(fieldFont);
        }

        var slot = (FamilyIndex(style.Family) * StylesPerFamily) + (style.Bold ? BoldStyle : 0) + (style.Italic ? ItalicStyle : 0);
        return used.Find(Names[slot]) ?? used.Use(FormFont.CreateStandard(store, Fonts[slot], Names[slot], fonts));
    }

    /// <summary>Gets the position of a family among the standard families.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The family index; Helvetica's when the style names none.</returns>
    private static int FamilyIndex(RichFamily family) => family switch
    {
        RichFamily.Serif => SerifFamily,
        RichFamily.Mono => MonoFamily,
        _ => SansFamily,
    };
}
