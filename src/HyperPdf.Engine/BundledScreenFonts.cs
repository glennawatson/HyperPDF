// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Engine;

/// <summary>
/// The fonts the engine draws the built in PDF families with: Arimo for Helvetica, Tinos for Times and Cousine for Courier. A
/// host shows typed text in the same fonts, so the text does not change width when it is saved, whatever fonts the system has.
/// </summary>
public static class BundledScreenFonts
{
    /// <summary>The family names, which are the names inside the font files.</summary>
    private static readonly string[] FamilyNames = ["Arimo", "Tinos", "Cousine"];

    /// <summary>Gets the family names of the fonts, in the order sans, serif, fixed width.</summary>
    public static IReadOnlyList<string> Families => FamilyNames;

    /// <summary>Reads the font file of a family.</summary>
    /// <param name="family">The family, one of <see cref="Families"/> (not case sensitive).</param>
    /// <param name="bold">Whether to read the bold style.</param>
    /// <param name="italic">Whether to read the italic style.</param>
    /// <returns>A new copy of the TrueType font file, or <see langword="null"/> when the family is not one of ours or the font is damaged.</returns>
    public static byte[]? Read(string family, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(family);
        for (var i = 0; i < FamilyNames.Length; i++)
        {
            if (string.Equals(FamilyNames[i], family, StringComparison.OrdinalIgnoreCase))
            {
                return PdfBundledFonts.GetFontFile((PdfBundledFontFamily)i, bold, italic);
            }
        }

        return null;
    }
}
