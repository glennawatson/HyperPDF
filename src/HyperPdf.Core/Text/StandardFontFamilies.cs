// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text;

/// <summary>
/// The three families every PDF reader has built in. Text in them needs no embedded font, and they are the
/// fallback when an installed font cannot be embedded.
/// </summary>
public static class StandardFontFamilies
{
    /// <summary>Gets the sans serif family.</summary>
    public static string Sans => "Helvetica";

    /// <summary>Gets the serif family.</summary>
    public static string Serif => "Times";

    /// <summary>Gets the fixed width family.</summary>
    public static string Mono => "Courier";

    /// <summary>Gets the three families in menu order.</summary>
    public static IReadOnlyList<string> All { get; } = [Sans, Serif, Mono];

    /// <summary>Determines whether a family is one of the built in three.</summary>
    /// <param name="family">The family.</param>
    /// <returns><see langword="true"/> for Helvetica, Times or Courier.</returns>
    public static bool IsStandard(string family) =>
        string.Equals(family, Sans, StringComparison.OrdinalIgnoreCase)
        || string.Equals(family, Serif, StringComparison.OrdinalIgnoreCase)
        || string.Equals(family, Mono, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the PDF base font name of a standard family in a style, such as "Times-BoldItalic".</summary>
    /// <param name="family">The standard family; anything else is treated as Helvetica.</param>
    /// <param name="bold">Whether bold.</param>
    /// <param name="italic">Whether italic.</param>
    /// <returns>The base font name.</returns>
    public static string BaseFontName(string family, bool bold, bool italic)
    {
        if (string.Equals(family, Serif, StringComparison.OrdinalIgnoreCase))
        {
            return (bold, italic) switch
            {
                (true, true) => "Times-BoldItalic",
                (true, false) => "Times-Bold",
                (false, true) => "Times-Italic",
                _ => "Times-Roman",
            };
        }

        var name = string.Equals(family, Mono, StringComparison.OrdinalIgnoreCase) ? Mono : Sans;
        return (bold, italic) switch
        {
            (true, true) => $"{name}-BoldOblique",
            (true, false) => $"{name}-Bold",
            (false, true) => $"{name}-Oblique",
            _ => name,
        };
    }

    /// <summary>Picks the built in family closest to an installed one, by its name.</summary>
    /// <param name="family">The installed family.</param>
    /// <param name="isSerif">Whether the font is known to have serifs.</param>
    /// <param name="isMonospace">Whether the font is known to be fixed width.</param>
    /// <returns>The closest standard family.</returns>
    public static string Closest(string family, bool isSerif, bool isMonospace)
    {
        if (IsStandard(family))
        {
            return family;
        }

        if (isMonospace || family.Contains("Mono", StringComparison.OrdinalIgnoreCase) || family.Contains("Courier", StringComparison.OrdinalIgnoreCase))
        {
            return Mono;
        }

        var serifName = family.Contains("Serif", StringComparison.OrdinalIgnoreCase) && !family.Contains("Sans", StringComparison.OrdinalIgnoreCase);
        return isSerif || serifName || family.Contains("Times", StringComparison.OrdinalIgnoreCase) ? Serif : Sans;
    }
}
