// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <summary>The words in a CSS family name that mean a fixed width font.</summary>
    private static readonly string[] MonoWords = ["courier", "mono", "consolas"];

    /// <summary>The words in a CSS family name that mean a serif font.</summary>
    private static readonly string[] SerifWords = ["times", "serif", "georgia", "garamond"];

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
    /// <returns>The font.</returns>
    internal static FormFont Resolve(PdfObjectStore store, FormFont fieldFont, RichStyle style, List<FormFont> used)
    {
        if (style.Family.Length == 0 && !style.Bold && !style.Italic)
        {
            return Use(fieldFont, used);
        }

        var slot = (FamilyOf(style.Family) * StylesPerFamily) + (style.Bold ? BoldStyle : 0) + (style.Italic ? ItalicStyle : 0);
        foreach (var font in used)
        {
            if (string.Equals(font.Name, Names[slot], StringComparison.Ordinal))
            {
                return font;
            }
        }

        return Use(FormFont.CreateStandard(store, Fonts[slot], Names[slot]), used);
    }

    /// <summary>Adds a font to the list when no font of that name is there.</summary>
    /// <param name="font">The font.</param>
    /// <param name="used">The fonts chosen so far.</param>
    /// <returns>The font in the list with that name.</returns>
    private static FormFont Use(FormFont font, List<FormFont> used)
    {
        foreach (var item in used)
        {
            if (string.Equals(item.Name, font.Name, StringComparison.Ordinal))
            {
                return item;
            }
        }

        used.Add(font);
        return font;
    }

    /// <summary>Finds the standard family closest to a CSS family name.</summary>
    /// <param name="family">The family name; may be empty.</param>
    /// <returns>The family index.</returns>
    private static int FamilyOf(string family)
    {
        if (ContainsAny(family, MonoWords))
        {
            return MonoFamily;
        }

        return ContainsAny(family, SerifWords) && !family.Contains("sans", StringComparison.OrdinalIgnoreCase) ? SerifFamily : SansFamily;
    }

    /// <summary>Determines whether a name contains any of some words, ignoring case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="words">The words.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    private static bool ContainsAny(string name, string[] words)
    {
        foreach (var word in words)
        {
            if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
